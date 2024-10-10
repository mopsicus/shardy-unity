using System;

namespace Shardy {

    /// <summary>
    /// Client class connected to server
    /// </summary>
    public class Client {

        /// <summary>
        /// Log tag
        /// </summary>
        const string TAG = "CLIENT";

        /// <summary>
        /// Event on connected
        /// </summary>
        public Action<bool> OnConnect = delegate { };

        /// <summary>
        /// Event on disconnected
        /// </summary>
        public Action<DisconnectReason> OnDisconnect = delegate { };

        /// <summary>
        /// Event when ready to work
        /// </summary>
        public Action OnReady = delegate { };

        /// <summary>
        /// Commander instance
        /// </summary>
        readonly Commander _commander = null;

        /// <summary>
        /// Client validator
        /// </summary>
        readonly IValidator _validator = null;

        /// <summary>
        /// Client serializer
        /// </summary>
        readonly ISerializer _serializer = null;

        /// <summary>
        /// Current connection
        /// </summary>
        readonly Connection _connection = null;

        /// <summary>
        /// Current options
        /// </summary>
        readonly ClientOptions _options = null;

        /// <summary>
        /// Snd handshake after connect
        /// </summary>
        readonly byte[] _handshake = null;

        /// <summary>
        /// Is client connected
        /// </summary>
        public bool IsConnected {
            get {
                return _connection.IsConnected;
            }
        }

        /// <summary>
        /// Client constructor
        /// </summary>
        /// <param name="validator">Validator</param>
        /// <param name="serializer">Serializer</param>
        /// <param name="options">Client options (optional)</param>
        /// <param name="handshake">Handshake after connect (optional)</param>
        public Client(IValidator validator, ISerializer serializer, ClientOptions options = null, byte[] handshake = null) {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebSocketManager.Init();
#if SHARDY_DEBUG_RAW
            WebSocketManager.SetDebug(true);
#endif
#endif
            _validator = validator;
            _serializer = serializer;
            _options = options ?? new ClientOptions();
            _handshake = handshake;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_options.Type == TransportType.Tcp) {
                Logger.Error($"Unable to use TCP transport for WebGL", TAG);
                return;
            }
#endif
            _connection = new Connection(_options.Type, _options.BufferSize);
            _commander = new Commander(_connection, _validator, _serializer, _options);
            _commander.OnDisconnect = (reason) => OnDisconnect(reason);
            _commander.OnReady = () => OnReady();
        }

        /// <summary>
        /// Connect to server
        /// </summary>
        /// <param name="host">Host to connect</param>
        /// <param name="port">Port to connect</param>
        public async void Connect(string host, int port) {
            var status = await _connection.Open(host, port);
            if (status) {
                _commander.Start();
            }
            OnConnect(status);
            if (_handshake != null && status) {
                Handshake(_handshake);
            }
        }

        /// <summary>
        /// Disconnect from server
        /// </summary>
        public void Disconnect() {
            _commander.Disconnect();
        }

        /// <summary>
        /// Send command (event) to server
        /// </summary>
        /// <param name="command">Command name</param>
        /// <param name="data">Payload data</param>
        public void Command(string command, byte[] data = null) {
            _commander.Command(command, data);
        }

        /// <summary>
        /// Send request to server and wait response
        /// </summary>
        /// <param name="request">Request name</param>
        /// <param name="callback">Answer from server</param>
        /// <returns>Request id</returns>
        public int Request(string request, Action<PayloadData> callback) {
            return Request(request, null, callback);
        }

        /// <summary>
        /// Send response on request from server
        /// </summary>
        /// <param name="request">Request data</param>
        /// <param name="data">Data</param>
        public void Response(PayloadData request, byte[] data = null) {
            _commander.Response(request, data);
        }

        /// <summary>
        /// Send error on request from server
        /// </summary>
        /// <param name="request">Request data</param>
        /// <param name="error">Error message or code</param>
        /// <param name="data">Data</param>
        public void Error(PayloadData request, string error, byte[] data = null) {
            _commander.Error(request, error, data);
        }

        /// <summary>
        /// Send request to server and wait response
        /// </summary>
        /// <param name="request">Request name</param>
        /// <param name="data">Payload data</param>
        /// <param name="callback">Answer from server</param>
        /// <returns>Request id</returns>
        public int Request(string request, byte[] data, Action<PayloadData> callback) {
            return _commander.Request(request, data, callback);
        }

        /// <summary>
        /// Cancel request manually
        /// </summary>
        /// <param name="id">Request id</param>
        public void CancelRequest(int id) {
            _commander.CancelRequest(id);
        }

        /// <summary>
        /// Handshake to verify connection
        /// </summary>
        /// <param name="body">Data for handshake</param>
        public void Handshake(byte[] body = null) {
            _commander.Handshake(_validator.Handshake(body));
        }

        /// <summary>
        /// Subscribe on command from server
        /// </summary>
        /// <param name="command">Command name</param>
        /// <param name="callback">Callback to action</param>
        public void On(string command, Action<PayloadData> callback) {
            _commander.AddCommand(command, callback);
        }

        /// <summary>
        /// Unsubscribe from command
        /// If callback is null -> clear all of them
        /// </summary>
        /// <param name="command">Command name</param>
        /// <param name="callback">Callback to unsubscribe</param>
        public void Off(string command, Action<PayloadData> callback = null) {
            _commander.CancelCommand(command, callback);
        }

        /// <summary>
        /// Subscribe on request from server that wait response
        /// </summary>
        /// <param name="request">Request name</param>
        /// <param name="callback">Callback to action</param>
        public void OnRequest(string request, Action<PayloadData> callback) {
            _commander.AddOnRequest(request, callback);
        }

        /// <summary>
        /// Unsubscribe from request from server that wait response
        /// </summary>
        /// <param name="request">Request name</param>
        /// <param name="callback">Callback to unsubscribe</param>
        public void OffRequest(string request) {
            _commander.CancelOnRequest(request);
        }

        /// <summary>
        /// Destroy all
        /// </summary>
        public void Destroy() {
#if SHARDY_DEBUG_RAW
            Logger.Info("destroy", TAG);
#endif
            _commander.Destroy();
        }
    }
}