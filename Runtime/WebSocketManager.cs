#if UNITY_WEBGL

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;

namespace Shardy {

    /// <summary>
    /// Websocket manager for WebGL
    /// </summary>
    static class WebSocketManager {

        /// <summary>
        /// List of websockets
        /// </summary>
        static readonly Dictionary<int, WebSocket> _list = new Dictionary<int, WebSocket>();

        /// <summary>
        /// On open socket delegate
        /// </summary>
        /// <param name="id">Instance id</param>
        public delegate void OnOpen(int id);

        /// <summary>
        /// On send to socket delegate
        /// </summary>
        /// <param name="id">Instance id</param>
        public delegate void OnSend(int id);

        /// <summary>
        /// On data receive delegate
        /// </summary>
        /// <param name="id">Instance id</param>
        /// <param name="message">Pointer to message</param>
        /// <param name="length">Data length</param>
        public delegate void OnData(int id, IntPtr message, int length);

        /// <summary>
        /// On error receive delegate
        /// </summary>
        /// <param name="id">Instance id</param>
        /// <param name="code">Error code</param> 
        public delegate void OnError(int id, int code);

        /// <summary>
        /// On close socket delegate
        /// </summary>
        /// <param name="id">Instance id</param>
        /// <param name="code">Close code</param>      
        public delegate void OnClose(int id, int code);

        /// <summary>
        /// Set on/off debug mode
        /// </summary>
        [DllImport("__Internal")]
        public static extern int wsSetDebug(bool value);

        /// <summary>
        /// Create new websocket instance
        /// </summary>
        [DllImport("__Internal")]
        public static extern int wsInit();

        /// <summary>
        /// Remove websocket
        /// </summary>
        [DllImport("__Internal")]
        public static extern void wsRemove(int id);

        /// <summary>
        /// Add subprotocol
        /// </summary>
        [DllImport("__Internal")]
        public static extern int wsAddSubProtocol(int id, string subprotocol);

        /// <summary>
        /// Set on open callback for instance
        /// </summary>
        [DllImport("__Internal")]
        public static extern void wsSetOnOpen(OnOpen callback);

        /// <summary>
        /// Set on send callback for instance
        /// </summary>
        [DllImport("__Internal")]
        public static extern void wsSetOnSend(OnSend callback);

        /// <summary>
        /// Set on data receive callback for instance
        /// </summary>
        [DllImport("__Internal")]
        public static extern void wsSetOnData(OnData callback);

        /// <summary>
        /// Set on error receive callback for instance
        /// </summary>
        [DllImport("__Internal")]
        public static extern void wsSetOnError(OnError callback);

        /// <summary>
        /// Set on close callback for instance
        /// </summary>
        [DllImport("__Internal")]
        public static extern void wsSetOnClose(OnClose callback);

        /// <summary>
        /// Flag to check initialized
        /// </summary>
        static bool _isInitialized = false;

        /// <summary>
        /// Set debug mode
        /// </summary>
        public static void SetDebug(bool value) {
            wsSetDebug(value);
        }

        /// <summary>
        /// Set callbacks
        /// </summary>
        public static void Init() {
            if (!_isInitialized) {
                wsSetOnOpen(OnOpenEvent);
                wsSetOnSend(OnSendEvent);
                wsSetOnData(OnDataEvent);
                wsSetOnError(OnErrorEvent);
                wsSetOnClose(OnCloseEvent);
                _isInitialized = true;
            }
        }

        /// <summary>
        /// Add new socket
        /// </summary>
        /// <param name="socket">Websocket instance</param>
        /// <returns>Id</returns>
        public static int Add(WebSocket socket) {
            var id = wsInit();
            _list.Add(id, socket);
            return id;
        }

        /// <summary>
        /// Remove websocket instance
        /// </summary>
        /// <param name="id">Instance id</param>
        public static void Destroy(int id) {
            if (_list.ContainsKey(id)) {
                _list.Remove(id);
                wsRemove(id);
            }
        }

        /// <summary>
        /// On open event from jslib
        /// </summary>
        /// <param name="id">Instance id</param>
        [MonoPInvokeCallback(typeof(OnOpen))]
        public static void OnOpenEvent(int id) {
            if (_list.ContainsKey(id)) {
                _list[id].OnOpen();
            }
        }

        /// <summary>
        /// On send event from jslib
        /// </summary>
        /// <param name="id">Instance id</param>
        [MonoPInvokeCallback(typeof(OnSend))]
        public static void OnSendEvent(int id) {
            if (_list.ContainsKey(id)) {
                _list[id].OnSend();
            }
        }

        /// <summary>
        /// On data receive event from jslib
        /// </summary>
        /// <param name="id">Instance id</param>
        [MonoPInvokeCallback(typeof(OnData))]
        public static void OnDataEvent(int id, IntPtr message, int length) {
            if (_list.ContainsKey(id)) {
                var data = new byte[length];
                Marshal.Copy(message, data, 0, length);
                _list[id].OnData(data, length);
            }
        }

        /// <summary>
        /// On error receive event from jslib
        /// </summary>
        /// <param name="id">Instance id</param>
        /// <param name="code">Error code</param>
        [MonoPInvokeCallback(typeof(OnError))]
        public static void OnErrorEvent(int id, int code) {
            if (_list.ContainsKey(id)) {
                var error = Enum.IsDefined(typeof(WebSocketErrorCode), code) ? (WebSocketErrorCode)code : WebSocketErrorCode.Unknown;
                _list[id].OnError(error);
            }
        }

        /// <summary>
        /// On close event from jslib
        /// </summary>
        /// <param name="id">Instance id</param>
        /// <param name="code">Close code</param>
        [MonoPInvokeCallback(typeof(OnClose))]
        public static void OnCloseEvent(int id, int code) {
            if (_list.ContainsKey(id)) {
                var result = Enum.IsDefined(typeof(WebSocketCloseCode), code) ? (WebSocketCloseCode)code : WebSocketCloseCode.Undefined;
                _list[id].OnClose(result);
            }
        }
    }
}

#endif