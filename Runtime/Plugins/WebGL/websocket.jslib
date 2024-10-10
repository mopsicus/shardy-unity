/**
 * WebSocket library for WebGL
 */
var WebSocketLibrary = {

    /**
     * Codes for websocket results
     */
    $WebSocketResult: {
        Default: 0,
        NotFound: -1,
        AlreadyConnected: -2,
        NotConnected: -3,
        AlreadyClosing: -4,
        AlreadyClosed: -5,
        NotOpened: -6,
        CloseFail: -7,
        SendFail: -8,
    },

    /**
     * Codes for websocket states
     */
    $WebSocketState: {
        Connecting: 0,
        Open: 1,
        Closing: 2,
        Closed: 3
    },

    /**
     * Manager
     */
    $WebSockets: {
        list: {},
        counter: 0,
        onOpen: null,
        onData: null,
        onSend: null,
        onError: null,
        onClose: null,
        isDebug: false
    },

    /**
     * Get timestamp
     * 
     * @returns Formatted date time
     */
    $getTimestamp: function () {
        var date = new Date();
        return new Date(date.getTime() - (date.getTimezoneOffset() * 60000)).toISOString();
    },

    /**
     * Callback on open connection
     */
    wsSetOnOpen: function (callback) {
        WebSockets.onOpen = callback;
    },

    /**
     * Callback on receive data
     */
    wsSetOnData: function (callback) {
        WebSockets.onData = callback;
    },

    /**
     * Callback on send data
     */
    wsSetOnSend: function (callback) {
        WebSockets.onSend = callback;
    },

    /**
     * Callback on get error
     */
    wsSetOnError: function (callback) {
        WebSockets.onError = callback;
    },

    /**
     * Callback on close
     */
    wsSetOnClose: function (callback) {
        WebSockets.onClose = callback;
    },

    /**
     * Switch on/off debug mode
     * 
     * @param state True or false
     */
    wsSetDebug: function (state) {
        WebSockets.isDebug = state;
    },

    /**
     * Create new websocket connection
     * 
     * @returns Instance id
     */
    wsInit: function () {
        var id = WebSockets.counter++;
        WebSockets.list[id] = {
            subprotocols: [],
            ws: null
        };
        if (WebSockets.isDebug) {
            console.log(`${getTimestamp()} [WEBSOCKET] (${id}) init`);
        }
        return id;
    },

    /**
     * Add subprotocol for connection
     * 
     * @param id Instance id
     * @param subprotocol Subprotocol
     */
    wsAddSubProtocol: function (id, subprotocol) {
        var data = UTF8ToString(subprotocol);
        if (WebSockets.isDebug) {
            console.log(`${getTimestamp()} [WEBSOCKET] (${id}) add subprotocol: ${data}`);
        }
        WebSockets.list[id].subprotocols.push(data);
    },

    /**
     * Remove connection
     * 
     * @param id Instance id
     * @returns 
     */
    wsRemove: function (id) {
        if (WebSockets.isDebug) {
            console.log(`${getTimestamp()} [WEBSOCKET] (${id}) remove`);
        }
        var instance = WebSockets.list[id];
        if (!instance) {
            return WebSocketResult.Default;
        };
        if (instance.ws && instance.ws.readyState < WebSocketState.Closing) {
            instance.ws.close();
        }
        delete WebSockets.list[id];
        return WebSocketResult.Default;
    },

    /**
     * Open connection
     * 
     * @param id Instance id
     * @param url Url to connect
     * @returns Result code
     */
    wsConnect: function (id, url) {
        var address = UTF8ToString(url);
        if (WebSockets.isDebug) {
            console.log(`${getTimestamp()} [WEBSOCKET] (${id}) connecting: ${address}`);
        }
        var instance = WebSockets.list[id];
        if (!instance) {
            return WebSocketResult.NotFound;
        }
        if (instance.ws !== null) {
            return WebSocketResult.AlreadyConnected;
        }
        instance.ws = new WebSocket(address, instance.subprotocols);
        instance.ws.binaryType = 'arraybuffer';
        instance.ws.onopen = function () {
            if (WebSockets.isDebug) {
                console.log(`${getTimestamp()} [WEBSOCKET] (${id}) connected`);
            }
            if (WebSockets.onOpen) {
                Module.dynCall_vi(WebSockets.onOpen, id);
            }
        };
        instance.ws.onmessage = function (data) {
            if (WebSockets.isDebug) {
                console.log(`${getTimestamp()} [WEBSOCKET] (${id}) received data: ${new TextDecoder().decode(data.data)}`);
            }
            if (WebSockets.onData === null) {
                return;
            }
            if (data.data instanceof ArrayBuffer) {
                var dataBuffer = new Uint8Array(data.data);
                var buffer = _malloc(dataBuffer.length);
                HEAPU8.set(dataBuffer, buffer);
                try {
                    Module.dynCall_viii(WebSockets.onData, id, buffer, dataBuffer.length);
                } catch (error) {
                    console.error(`${getTimestamp()} [WEBSOCKET] (${id}) received failed: ${error.message}`);
                } finally {
                    _free(buffer);
                }
            } else if (WebSockets.isDebug) {
                console.warn(`${getTimestamp()} [WEBSOCKET] (${id}) received not buffer data: ${data.data}`);
            }
        };
        instance.ws.onerror = function (data) {
            if (WebSockets.isDebug) {
                var error = (data.code === undefined) ? 'unknown' : data.code;
                console.error(`${getTimestamp()} [WEBSOCKET] (${id}) error: ${error}`);
            }
            if (WebSockets.onError) {
                Module.dynCall_vii(WebSockets.onError, id, data.code);
            }
        };
        instance.ws.onclose = function (data) {
            if (WebSockets.isDebug) {
                console.log(`${getTimestamp()} [WEBSOCKET] (${id}) closed: ${data.code}`);
            }
            if (WebSockets.onClose) {
                Module.dynCall_vii(WebSockets.onClose, id, data.code);
            }
            delete instance.ws;
        };
        return WebSocketResult.Default;
    },

    /**
     * Close connection
     * 
     * @param id Instance id
     * @param code 
     * @param reason
     * @returns Result code
     */
    wsClose: function (id, code, reason) {
        if (WebSockets.isDebug) {
            console.log(`${getTimestamp()} [WEBSOCKET] (${id}) close: ${code}, reason: ${reason}`);
        }
        var instance = WebSockets.list[id];
        if (!instance) {
            return WebSocketResult.NotFound;
        }
        if (!instance.ws) {
            return WebSocketResult.NotConnected;
        }
        if (instance.ws.readyState === WebSocketState.Closing) {
            return WebSocketResult.AlreadyClosing;
        }
        if (instance.ws.readyState === WebSocketState.Closed) {
            return WebSocketResult.AlreadyClosed;
        }
        var result = (reason ? UTF8ToString(reason) : undefined);
        try {
            instance.ws.close(code, result);
        } catch (error) {
            console.error(`${getTimestamp()} [WEBSOCKET] (${id}) close error: ${error.message}`);
            return WebSocketResult.CloseFail;
        }
        return WebSocketResult.Default;
    },

    /**
     * Send buffer
     * @param id Instance id
     * @param buffer Data
     * @param length Data length
     * @returns Result code
     */
    wsSend: function (id, data, length) {
        if (WebSockets.isDebug) {
            console.log(`${getTimestamp()} [WEBSOCKET] (${id}) send ${length} bytes: ${data}`);
        }
        var instance = WebSockets.list[id];
        if (!instance) {
            return WebSocketResult.NotFound;
        }
        if (!instance.ws) {
            return WebSocketResult.NotConnected;
        }
        if (instance.ws.readyState !== WebSocketState.Open) {
            return WebSocketResult.NotOpened;
        }
        try {
            instance.ws.send(HEAPU8.buffer.slice(data, data + length));
        } catch (error) {
            console.error(`${getTimestamp()} [WEBSOCKET] (${id}) send error: ${error.message}`);
            return WebSocketResult.SendFail;
        }
        if (WebSockets.onSend) {
            Module.dynCall_vi(WebSockets.onSend, id);
        }
        return WebSocketResult.Default;
    },

    /**
     * Get connection state
     * @param id Instance id
     * @returns Result code
     */
    wsGetState: function (id) {
        if (WebSockets.isDebug) {
            console.log(`${getTimestamp()} [WEBSOCKET] (${id}) get state`);
        }
        var instance = WebSockets.list[id];
        if (!instance) {
            return WebSocketResult.NotFound;
        }
        if (instance.ws) {
            return instance.ws.readyState;
        } else {
            return WebSocketState.Closed;
        }
    }

};

autoAddDeps(WebSocketLibrary, '$WebSockets');
autoAddDeps(WebSocketLibrary, '$WebSocketResult');
autoAddDeps(WebSocketLibrary, '$WebSocketState');
autoAddDeps(WebSocketLibrary, '$getTimestamp');
mergeInto(LibraryManager.library, WebSocketLibrary);