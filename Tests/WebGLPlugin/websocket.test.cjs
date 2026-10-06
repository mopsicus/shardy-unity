const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const pluginPath = path.resolve(
    __dirname,
    "../../Runtime/Plugins/WebGL/websocket.jslib",
);

class MockWebSocket {
    static CONNECTING = 0;
    static OPEN = 1;
    static CLOSING = 2;
    static CLOSED = 3;

    constructor(url, protocols) {
        this.url = url;
        this.protocols = protocols;
        this.readyState = MockWebSocket.CONNECTING;
        this.binaryType = "blob";
        this.sent = [];
        this.closeCalls = [];
        MockWebSocket.instances.push(this);
    }

    open() {
        this.readyState = MockWebSocket.OPEN;
        this.onopen?.();
    }

    send(data) {
        this.sent.push(data);
    }

    close(code, reason) {
        this.closeCalls.push({ code, reason });
        this.readyState = MockWebSocket.CLOSING;
    }

    receive(data) {
        this.onmessage?.({ data });
    }

    fail(code) {
        this.onerror?.({ code });
    }

    finishClose(code = 1000) {
        this.readyState = MockWebSocket.CLOSED;
        this.onclose?.({ code });
    }
}

MockWebSocket.instances = [];

function createHarness() {
    const library = {};
    const stringPointers = new Map();
    const freedPointers = [];
    let nextPointer = 32;
    const memory = new Uint8Array(1024 * 1024);

    const context = {
        WebSocket: MockWebSocket,
        LibraryManager: { library },
        autoAddDeps() {},
        mergeInto(target, source) {
            Object.assign(target, source);
        },
        UTF8ToString(value) {
            return stringPointers.get(value) ?? value;
        },
        HEAPU8: memory,
        _malloc(length) {
            const pointer = nextPointer;
            nextPointer += length;
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
        console: {
            log() {},
            warn() {},
            error() {},
        },
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
        const bytes = new TextEncoder().encode(value);
        const pointer = nextPointer;
        nextPointer += bytes.length;
        memory.set(bytes, pointer);
        stringPointers.set(pointer, value);
        return pointer;
    }

    return {
        library,
        freedPointers,
        memory,
        toNativeString,
        ...library,
    };
}

test("initializes unique socket ids and reports missing instances", () => {
    const h = createHarness();
    const firstId = h.wsInit();
    const secondId = h.wsInit();

    assert.notEqual(firstId, secondId);
    assert.equal(h.wsGetState(firstId), h.$WebSocketState.Closed);
    assert.equal(h.wsGetState(999), h.$WebSocketResult.NotFound);
    assert.equal(h.wsRemove(999), h.$WebSocketResult.Default);
});

test("stores subprotocols and opens a native websocket", () => {
    const h = createHarness();
    const id = h.wsInit();
    const openEvents = [];
    h.wsSetOnOpen((socketId) => openEvents.push(socketId));
    h.wsAddSubProtocol(id, h.toNativeString("shardy.v1"));

    assert.equal(h.wsConnect(id, h.toNativeString("ws://localhost:3000")), 0);

    const socket = MockWebSocket.instances.at(-1);
    assert.equal(socket.url, "ws://localhost:3000");
    assert.deepEqual(Array.from(socket.protocols), ["shardy.v1"]);
    assert.equal(socket.binaryType, "arraybuffer");
    assert.deepEqual(openEvents, []);

    socket.open();
    assert.deepEqual(openEvents, [id]);
    assert.equal(h.wsGetState(id), h.$WebSocketState.Open);
    assert.equal(h.wsConnect(id, h.toNativeString("ws://localhost:3000")), h.$WebSocketResult.AlreadyConnected);
});

test("reports websocket events through the registered callbacks", () => {
    const h = createHarness();
    const id = h.wsInit();
    const received = [];
    const errors = [];
    const closed = [];
    h.wsSetOnData((socketId, pointer, length) => {
        received.push({
            id: socketId,
            bytes: Array.from(h.memory.slice(pointer, pointer + length)),
        });
    });
    h.wsSetOnError((socketId, code) => errors.push([socketId, code]));
    h.wsSetOnClose((socketId, code) => closed.push([socketId, code]));
    h.wsConnect(id, h.toNativeString("ws://localhost:3000"));

    const socket = MockWebSocket.instances.at(-1);
    socket.open();
    socket.receive(new Uint8Array([0, 1, 127, 255]).buffer);
    socket.receive("text frames are ignored");
    socket.fail(1006);
    socket.finishClose(1001);

    assert.deepEqual(received, [{ id, bytes: [0, 1, 127, 255] }]);
    assert.deepEqual(errors, [[id, 1006]]);
    assert.deepEqual(closed, [[id, 1001]]);
    assert.equal(h.freedPointers.length, 1, "allocated receive buffer must be released");
    assert.equal(h.wsGetState(id), h.$WebSocketState.Closed);
});

test("releases receive memory even if the managed callback throws", () => {
    const h = createHarness();
    const id = h.wsInit();
    h.wsSetOnData(() => {
        throw new Error("callback failed");
    });
    h.wsConnect(id, h.toNativeString("ws://localhost:3000"));

    const socket = MockWebSocket.instances.at(-1);
    socket.open();
    socket.receive(new Uint8Array([1, 2, 3]).buffer);

    assert.equal(h.freedPointers.length, 1);
});

test("does not allocate for binary messages when no data callback is registered", () => {
    const h = createHarness();
    const id = h.wsInit();
    h.wsConnect(id, h.toNativeString("ws://localhost:3000"));

    const socket = MockWebSocket.instances.at(-1);
    socket.open();
    socket.receive(new Uint8Array([1, 2, 3]).buffer);

    assert.deepEqual(h.freedPointers, []);
});

test("sends the requested bytes and reports send completion", () => {
    const h = createHarness();
    const id = h.wsInit();
    const sentEvents = [];
    h.wsSetOnSend((socketId) => sentEvents.push(socketId));
    h.wsConnect(id, h.toNativeString("ws://localhost:3000"));

    const socket = MockWebSocket.instances.at(-1);
    assert.equal(h.wsSend(id, 0, 0), h.$WebSocketResult.NotOpened);
    socket.open();

    const source = new Uint8Array([4, 0, 255, 8]);
    h.memory.set(source, 200);
    assert.equal(h.wsSend(id, 200, source.length), h.$WebSocketResult.Default);
    assert.deepEqual(Array.from(new Uint8Array(socket.sent[0])), Array.from(source));
    assert.deepEqual(sentEvents, [id]);
    assert.equal(h.wsSend(999, 200, source.length), h.$WebSocketResult.NotFound);
});

test("returns expected close errors and closes with the requested code and reason", () => {
    const h = createHarness();
    const id = h.wsInit();

    assert.equal(h.wsClose(id, 1000, 0), h.$WebSocketResult.NotConnected);
    assert.equal(h.wsClose(999, 1000, 0), h.$WebSocketResult.NotFound);
    h.wsConnect(id, h.toNativeString("ws://localhost:3000"));
    const socket = MockWebSocket.instances.at(-1);
    socket.open();

    assert.equal(h.wsClose(id, 4000, h.toNativeString("test close")), 0);
    assert.deepEqual(socket.closeCalls, [{ code: 4000, reason: "test close" }]);
    assert.equal(h.wsClose(id, 1000, 0), h.$WebSocketResult.AlreadyClosing);
    socket.finishClose(4000);
    assert.equal(h.wsClose(id, 1000, 0), h.$WebSocketResult.NotConnected);
});

test("removes socket instances and closes sockets that are still active", () => {
    const h = createHarness();
    const id = h.wsInit();
    h.wsConnect(id, h.toNativeString("ws://localhost:3000"));
    const socket = MockWebSocket.instances.at(-1);
    socket.open();

    assert.equal(h.wsRemove(id), h.$WebSocketResult.Default);
    assert.equal(socket.closeCalls.length, 1);
    assert.equal(h.wsGetState(id), h.$WebSocketResult.NotFound);
});

test("reports a close failure without changing the websocket instance", () => {
    const h = createHarness();
    const id = h.wsInit();
    h.wsConnect(id, h.toNativeString("ws://localhost:3000"));
    const socket = MockWebSocket.instances.at(-1);
    socket.open();
    socket.close = () => {
        throw new Error("native close failed");
    };

    assert.equal(h.wsClose(id, 1000, 0), h.$WebSocketResult.CloseFail);
    assert.equal(h.wsGetState(id), h.$WebSocketState.Open);
});
