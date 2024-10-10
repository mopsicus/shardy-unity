#if UNITY_WEBGL

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Shardy {

    /// <summary>
    /// Condition key for wait callback
    /// </summary>
    enum WaitCondition {
        Open,
        Send,
        Close
    }

    /// <summary>
    /// Websocket state codes
    /// </summary>
    enum WebSocketStateCode {
        Connecting,
        Open,
        Closing,
        Closed,
        Error
    }

    /// <summary>
    /// Return error codes
    /// </summary>
    enum WebSocketErrorCode {
        NotFound = -1,
        AlreadyConnected = -2,
        NotConnected = -3,
        AlreadyClosing = -4,
        AlreadyClosed = -5,
        NotOpened = -6,
        CloseFail = -7,
        SendFail = -8,
        Unknown = -999
    }

    /// <summary>
    /// Websocket code for close
    /// </summary>
    enum WebSocketCloseCode {
        NotSet = 0,
        Normal = 1000,
        Away = 1001,
        ProtocolError = 1002,
        UnsupportedData = 1003,
        Undefined = 1004,
        NoStatus = 1005,
        Abnormal = 1006,
        InvalidData = 1007,
        PolicyViolation = 1008,
        TooBig = 1009,
        MandatoryExtension = 1010,
        ServerError = 1011,
        TlsHandshakeFailure = 1015
    }

    /// <summary>
    /// WebSocket class for WebGL using
    /// </summary>
    class WebSocket {

        /// <summary>
        /// Log tag
        /// </summary>
        const string TAG = "WEBSOCKET";

        /// <summary>
        /// Delay, ms
        /// </summary>
        const float DELAY = 10f;

        /// <summary>
        /// Callback on socket open
        /// </summary>
        public Action OnOpen = delegate { };

        /// <summary>
        /// Callback on socket send
        /// </summary>
        public Action OnSend = delegate { };

        /// <summary>
        ///Callback on message receive
        /// </summary>
        public Action<byte[], int> OnData = delegate { };

        /// <summary>
        /// Callback on error receive
        /// </summary>
        public Action<WebSocketErrorCode> OnError = delegate { };

        /// <summary>
        /// Callback on close socket
        /// </summary>
        public Action<WebSocketCloseCode> OnClose = delegate { };

        /// <summary>
        /// Connect to socket
        /// </summary>
        [DllImport("__Internal")]
        public static extern int wsConnect(int id, string url);

        /// <summary>
        /// Close socket
        /// </summary>
        [DllImport("__Internal")]
        public static extern int wsClose(int id, int code, string reason);

        /// <summary>
        /// Send buffer to socket
        /// </summary>
        [DllImport("__Internal")]
        public static extern int wsSend(int id, byte[] data, int length);

        /// <summary>
        /// Get current state
        /// </summary>
        [DllImport("__Internal")]
        public static extern int wsGetState(int id);

        /// <summary>
        /// Dict for check waitable vars
        /// </summary>
        readonly Dictionary<WaitCondition, bool> _conditions = new Dictionary<WaitCondition, bool>(3);

        /// <summary>
        /// Received buffer
        /// </summary>
        List<ReceivedData> _buffer = new List<ReceivedData>();

        /// <summary>
        /// Current socket instance id
        /// </summary>
        protected int _id = -1;

        /// <summary>
        /// Create websocket
        /// </summary>
        public WebSocket() {
            _id = WebSocketManager.Add(this);
            Subscribe();
        }

        /// <summary>
        /// Create websocket
        /// </summary>
        /// <param name="subprotocol">Optional subprotocol</param>
        public WebSocket(string subprotocol) {
            _id = WebSocketManager.Add(this);
            WebSocketManager.wsAddSubProtocol(_id, subprotocol);
            Subscribe();
        }

        /// <summary>
        /// Create websocket
        /// </summary>
        /// <param name="subprotocol">Optional subprotocols list</param>
        public WebSocket(List<string> subprotocols) {
            _id = WebSocketManager.Add(this);
            foreach (var subprotocol in subprotocols) {
                WebSocketManager.wsAddSubProtocol(_id, subprotocol);
            }
            Subscribe();
        }

        /// <summary>
        /// Subscribe for inner callbacks
        /// </summary>
        void Subscribe() {
            _conditions[WaitCondition.Open] = false;
            _conditions[WaitCondition.Send] = false;
            _conditions[WaitCondition.Close] = false;
            OnOpen += () => _conditions[WaitCondition.Open] = true;
            OnSend += () => _conditions[WaitCondition.Send] = true;
            OnClose += (WebSocketCloseCode code) => _conditions[WaitCondition.Close] = true;
            OnData += (byte[] data, int length) => {
                _buffer.Add(new ReceivedData(data, length));
            };
        }

        /// <summary>
        /// Remove websocket
        /// </summary>
        ~WebSocket() {
#if SHARDY_DEBUG_RAW
            Logger.Info("destroy", TAG);
#endif
            WebSocketManager.wsRemove(_id);
        }

        /// <summary>
        /// Current socket id
        /// </summary>
        public int Id {
            get {
                return _id;
            }
        }

        /// <summary>
        /// Current state
        /// </summary>
        public WebSocketStateCode State {
            get {
                var state = wsGetState(_id);
                return state switch {
                    0 => WebSocketStateCode.Connecting,
                    1 => WebSocketStateCode.Open,
                    2 => WebSocketStateCode.Closing,
                    3 => WebSocketStateCode.Closed,
                    _ => WebSocketStateCode.Error,
                };
            }
        }

        /// <summary>
        /// Check return code state on error
        /// </summary>
        /// <param name="state">Return state</param>
        /// <returns>Has error or not</returns>
        bool CheckState(int state) {
            if (state < 0) {
#if SHARDY_DEBUG
                var error = Enum.IsDefined(typeof(WebSocketErrorCode), state) ? (WebSocketErrorCode)state : WebSocketErrorCode.Unknown;
                Logger.Error($"state error: {error}", TAG);
#endif
                return false;
            }
            return true;
        }

        /// <summary>
        /// Connect to server
        /// </summary>
        public async Task<bool> Connect(string url) {
            var state = wsConnect(_id, url);
            while (!_conditions[WaitCondition.Open]) {
                await Utils.SetDelay(DELAY);
            }
            return CheckState(state);
        }

        /// <summary>
        /// Close connection
        /// </summary>
        /// <param name="code">Close code</param>
        /// <param name="reason">Reason</param>
        public bool Close(WebSocketCloseCode code = WebSocketCloseCode.Normal, string reason = null) {
            var state = wsClose(_id, (int)code, reason);
            return CheckState(state);
        }

        /// <summary>
        /// Send data as buffer
        /// </summary>
        /// <param name="data">Array of bytes</param>
        public async Task<bool> Send(byte[] data) {
            var state = wsSend(_id, data, data.Length);
            while (!_conditions[WaitCondition.Send]) {
                await Utils.SetDelay(DELAY);
            }
            _conditions[WaitCondition.Send] = false;
            return CheckState(state);
        }

        /// <summary>
        /// Receive data from socket
        /// </summary>
        /// <param name="buffer">Buffer to write</param>
        /// <returns>Received length</returns>
        public async Task<int> Receive(byte[] buffer) {
            while (_buffer.Count == 0) {
                await Utils.SetDelay(DELAY);
            }
            var data = _buffer[0];
            _buffer.RemoveAt(0);
            Array.Copy(data.Body, 0, buffer, 0, data.Length);
            return data.Length;
        }
    }
}

#endif