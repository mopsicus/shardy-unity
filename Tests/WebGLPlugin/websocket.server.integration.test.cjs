const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const testHost = process.env.SHARDY_TEST_HOST || "192.168.1.111";
const testPort = Number(process.env.SHARDY_TEST_PORT || 3000);
const operationTimeout = 10000;
const pluginPath = path.resolve(
    __dirname,
    "../../Runtime/Plugins/WebGL/websocket.jslib",
);

function createPlugin() {
    if (typeof WebSocket !== "function") {
        throw new Error("Node.js 22.4+ with the built-in WebSocket API is required");
    }

    const library = {};
    const memory = new Uint8Array(4 * 1024 * 1024);
    const nativeStrings = new Map();
    const freedPointers = [];
    let nextPointer = 64;
    let onClose = () => {};
    let onError = () => {};

    const context = {
        WebSocket,
        LibraryManager: { library },
        autoAddDeps() {},
        mergeInto(target, source) {
            Object.assign(target, source);
        },
        UTF8ToString(pointer) {
            return nativeStrings.get(pointer) ?? pointer;
        },
        HEAPU8: memory,
        _malloc(length) {
            const pointer = nextPointer;
            nextPointer += length;
            if (nextPointer > memory.length) {
                throw new Error("WebGL test heap exhausted");
            }
            return pointer;
        },
        _free(pointer) {
            freedPointers.push(pointer);
        },
        Module: {
            dynCall_vi(callback, ...args) {
                callback(...args);
            },
            dynCall_vii(callback, ...args) {
                callback(...args);
            },
            dynCall_viii(callback, ...args) {
                callback(...args);
            },
        },
        console,
        Date,
        TextDecoder,
        Uint8Array,
        ArrayBuffer,
    };
    vm.runInNewContext(fs.readFileSync(pluginPath, "utf8"), context, {
        filename: pluginPath,
    });
    context.WebSocketResult = library.$WebSocketResult;
    context.WebSocketState = library.$WebSocketState;
    context.WebSockets = library.$WebSockets;
    context.getTimestamp = library.$getTimestamp;

    function toNativeString(value) {
        const pointer = nextPointer;
        nextPointer += 1;
        nativeStrings.set(pointer, value);
        return pointer;
    }

    return {
        ...library,
        memory,
        freedPointers,
        toNativeString,
        setCloseHandler(handler) {
            onClose = handler;
        },
        setErrorHandler(handler) {
            onError = handler;
        },
    };
}

function makeBlock(type, body = Buffer.alloc(0)) {
    const data = Buffer.from(body);
    const frame = Buffer.alloc(4 + data.length);
    frame[0] = type;
    frame.writeUIntBE(data.length, 1, 3);
    data.copy(frame, 4);
    return frame;
}

function decodeBlock(frame) {
    assert.ok(frame.length >= 4, "server block must have a complete header");
    const length = frame.readUIntBE(1, 3);
    assert.equal(frame.length, length + 4, "server block length must match its header");
    return { type: frame[0], body: frame.subarray(4) };
}

function encodePayload(type, name, id, data = Buffer.alloc(0), error = "") {
    return Buffer.from(JSON.stringify({
        type,
        name,
        id,
        data: Buffer.from(data).toString("base64"),
        error,
    }), "utf8");
}

function decodePayload(body) {
    const value = JSON.parse(body.toString("utf8"));
    return {
        ...value,
        data: Buffer.from(value.data, "base64"),
    };
}

function waitFor(promise, operation) {
    let timeout;
    return Promise.race([
        promise,
        new Promise((_, reject) => {
            timeout = setTimeout(() => {
                reject(new Error(
                    `Timed out after ${operationTimeout} ms waiting for ${operation}; ` +
                    `Shardy server ${testHost}:${testPort} must be running with WebSocket transport`,
                ));
            }, operationTimeout);
        }),
    ]).finally(() => clearTimeout(timeout));
}

function createConnection() {
    const plugin = createPlugin();
    const socketId = plugin.wsInit();
    const blocks = [];
    const waiters = [];
    let closeResolve;
    let errorReject;
    const closed = new Promise((resolve) => {
        closeResolve = resolve;
    });
    const socketError = new Promise((_, reject) => {
        errorReject = reject;
    });

    plugin.wsSetOnData((id, pointer, length) => {
        assert.equal(id, socketId);
        const frame = Buffer.from(plugin.memory.slice(pointer, pointer + length));
        try {
            const block = decodeBlock(frame);
            const waiterIndex = waiters.findIndex((waiter) => waiter.predicate(block));
            if (waiterIndex >= 0) {
                waiters.splice(waiterIndex, 1)[0].resolve(block);
            } else {
                blocks.push(block);
            }
        } catch (error) {
            while (waiters.length) {
                waiters.shift().reject(error);
            }
        }
    });
    plugin.wsSetOnClose((id, code) => {
        if (id === socketId) {
            closeResolve(code);
        }
    });
    plugin.wsSetOnError((id, code) => {
        if (id === socketId) {
            errorReject(new Error(`WebSocket error (${code}) from ${testHost}:${testPort}`));
        }
    });

    function receiveBlock(predicate, operation) {
        const existing = blocks.findIndex(predicate);
        if (existing >= 0) {
            return Promise.resolve(blocks.splice(existing, 1)[0]);
        }
        const response = new Promise((resolve, reject) => {
            waiters.push({ predicate, resolve, reject });
        });
        return waitFor(Promise.race([response, socketError]), operation);
    }

    function sendBlock(type, body) {
        const frame = makeBlock(type, body);
        const pointer = 2048;
        plugin.memory.set(frame, pointer);
        assert.equal(
            plugin.wsSend(socketId, pointer, frame.length),
            plugin.$WebSocketResult.Default,
            `sending block type ${type} should succeed`,
        );
    }

    function sendPayload(type, name, id, data, error) {
        sendBlock(3, encodePayload(type, name, id, data, error));
    }

    async function connectAndHandshake() {
        const opened = new Promise((resolve) => {
            plugin.wsSetOnOpen((id) => {
                if (id === socketId) {
                    resolve();
                }
            });
        });
        const result = plugin.wsConnect(
            socketId,
            plugin.toNativeString(`ws://${testHost}:${testPort}`),
        );
        assert.equal(result, plugin.$WebSocketResult.Default, "plugin should start the websocket connection");
        await waitFor(Promise.race([opened, socketError]), "WebSocket open");

        const nonce = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
        sendBlock(0, Buffer.from(JSON.stringify({
            version: 1,
            timestamp: Date.now(),
            nonce,
            payload: null,
        }), "utf8"));

        const acknowledgement = await receiveBlock(
            (block) => block.type === 1,
            "Shardy handshake acknowledgement",
        );
        const serverAck = JSON.parse(acknowledgement.body.toString("utf8"));
        assert.equal(serverAck.received, true, "server must accept the Shardy handshake");
        assert.equal(serverAck.nonce, nonce, "server must echo the handshake nonce");
        sendBlock(1, Buffer.from(JSON.stringify({ received: true, nonce: serverAck.nonce }), "utf8"));
    }

    return {
        plugin,
        socketId,
        closed,
        connectAndHandshake,
        receiveBlock,
        sendPayload,
        async request(name, data = Buffer.alloc(0), id = 1) {
            sendPayload(0, name, id, data);
            const block = await receiveBlock(
                (candidate) => candidate.type === 3 &&
                    decodePayload(candidate.body).type === 2 &&
                    decodePayload(candidate.body).id === id,
                `response to '${name}' request ${id}`,
            );
            return decodePayload(block.body);
        },
        command(name, data = Buffer.alloc(0)) {
            sendPayload(1, name, 0, data);
        },
        async disconnect() {
            const result = plugin.wsClose(socketId, 1000, 0);
            assert.equal(result, plugin.$WebSocketResult.Default, "plugin should start a normal websocket close");
            const code = await waitFor(closed, "WebSocket close callback");
            assert.equal(code, 1000);
            assert.equal(plugin.wsGetState(socketId), plugin.$WebSocketState.Closed);
        },
        cleanup() {
            plugin.wsRemove(socketId);
        },
    };
}

async function withConnection(body) {
    const connection = createConnection();
    try {
        await connection.connectAndHandshake();
        await body(connection);
    } finally {
        connection.cleanup();
    }
}

test("real WebGL plugin connects, handshakes, requests status, and disconnects", { timeout: 30000 }, async () => {
    await withConnection(async (connection) => {
        assert.equal(connection.plugin.wsGetState(connection.socketId), connection.plugin.$WebSocketState.Open);
        const response = await connection.request("status");
        assert.equal(response.type, 2);
        assert.equal(response.name, "status");
        assert.equal(response.error, "");
        const status = JSON.parse(response.data.toString("utf8"));
        assert.ok(status.rss > 0);
        assert.ok(status.heapUsed > 0);
        await connection.disconnect();
    });
});

test("real WebGL plugin round-trips echo payloads through the server", { timeout: 30000 }, async () => {
    await withConnection(async (connection) => {
        const payloads = [
            Buffer.alloc(0),
            Buffer.from("WebGL websocket: Привет 🎮", "utf8"),
            Buffer.from([0, 1, 127, 254, 255]),
            Buffer.from(JSON.stringify({ number: 42, enabled: true, values: [1, 2, 3] }), "utf8"),
            Buffer.from("x".repeat(8192), "utf8"),
        ];
        for (let i = 0; i < payloads.length; i++) {
            const response = await connection.request("echo", payloads[i], i + 1);
            assert.equal(response.name, "echo");
            assert.equal(response.error, "");
            assert.deepEqual(response.data, payloads[i], `echo payload ${i + 1}`);
        }
        await connection.disconnect();
    });
});

test("real WebGL plugin delivers command and request errors from the server", { timeout: 30000 }, async () => {
    await withConnection(async (connection) => {
        const failure = await connection.request("fail", Buffer.from("test"), 11);
        assert.equal(failure.type, 2);
        assert.equal(failure.name, "fail");
        assert.equal(failure.error, "fail_error_code");

        connection.command("notify", Buffer.from("plugin integration command", "utf8"));
        const status = await connection.request("status", Buffer.alloc(0), 12);
        assert.equal(status.error, "");
        assert.ok(JSON.parse(status.data.toString("utf8")).heapTotal > 0);
        await connection.disconnect();
    });
});

test("real WebGL plugin receives server commands and responds to server requests", { timeout: 30000 }, async () => {
    await withConnection(async (connection) => {
        connection.command("request");
        const serverRequest = await connection.receiveBlock(
            (block) => block.type === 3 &&
                decodePayload(block.body).type === 0 &&
                decodePayload(block.body).name === "request",
            "server 'request' event",
        );
        const payload = decodePayload(serverRequest.body);
        assert.ok(payload.id >= 0, `invalid server request identifier: ${JSON.stringify(payload)}`);
        connection.sendPayload(2, payload.name, payload.id, Buffer.from("response from plugin test"));

        const status = await connection.request("status", Buffer.alloc(0), 22);
        assert.equal(status.error, "");
        await connection.disconnect();
    });
});
