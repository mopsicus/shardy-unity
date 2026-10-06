using System;
using System.Threading.Tasks;

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
        public bool IsConnected => _connection.IsConnected;

        /// <summary>
        /// Client constructor
        /// </summary>
        /// <param name="validator">Validator</param>
        /// <param name="serializer">Serializer</param>
        /// <param name="options">Client options (optional)</param>
        /// <param name="handshakePayload">Handshake payload to send after connecting (optional)</param>
        public Client(IValidator validator = null, ISerializer serializer = null, ClientOptions options = null, byte[] handshakePayload = null) {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebSocketManager.Init();
#if SHARDY_DEBUG_RAW
            WebSocketManager.SetDebug(true);
#endif
#endif
            _validator = validator ?? new DefaultValidator();
            _serializer = serializer ?? new DefaultSerializer();
            _options = options ?? new ClientOptions();
            _handshake = handshakePayload;
            if (!Block.Validate(_options.Block)) {
#if SHARDY_DEBUG_RAW
                Logger.Error($"maximum block body size must be between 0 and: {Block.MAX_BLOCK_SIZE}", TAG);
#endif
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_options.Type == TransportType.Tcp) {
                Logger.Error($"unable to use TCP transport for WebGL", TAG);
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
        /// <returns>Task that completes after the connection attempt</returns>
        public async Task Connect(string host, int port) {
            var isConnectionSucceeded = await _connection.Open(host, port);
            if (isConnectionSucceeded) {
                _commander.Start();
            }
            OnConnect(isConnectionSucceeded);
            if (_handshake != null && isConnectionSucceeded) {
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
        /// <param name="commandName">Command name</param>
        /// <param name="commandPayload">Command payload bytes</param>
        public void Command(string commandName, byte[] commandPayload = null) {
            _commander.Command(commandName, commandPayload);
        }

        /// <summary>
        /// Send a request to the server and handle its response with a callback
        /// </summary>
        /// <param name="requestName">Request name</param>
        /// <param name="responseCallback">Callback with the response</param>
        /// <param name="requestPayload">Optional request payload bytes</param>
        /// <returns>Request id</returns>
        public long Request(string requestName, Action<PayloadData> responseCallback, byte[] requestPayload = null) {
            return _commander.Request(requestName, responseCallback, requestPayload);
        }

        /// <summary>
        /// Send response on request from server
        /// </summary>
        /// <param name="requestPayload">Request received from the server</param>
        /// <param name="responsePayload">Response payload bytes</param>
        public void Response(PayloadData requestPayload, byte[] responsePayload = null) {
            _commander.Response(requestPayload, responsePayload);
        }

        /// <summary>
        /// Send error on request from server
        /// </summary>
        /// <param name="requestPayload">Request received from the server</param>
        /// <param name="errorMessage">Error message or code</param>
        /// <param name="responsePayload">Response payload bytes</param>
        public void Error(PayloadData requestPayload, string errorMessage, byte[] responsePayload = null) {
            _commander.Error(requestPayload, errorMessage, responsePayload);
        }

        /// <summary>
        /// Cancel request manually
        /// </summary>
        /// <param name="requestId">Request id</param>
        public void Cancel(long requestId) {
            _commander.CancelRequest(requestId);
        }

        /// <summary>
        /// Handshake to verify connection
        /// </summary>
        /// <param name="handshakePayload">Custom handshake payload</param>
        public void Handshake(byte[] handshakePayload = null) {
            _commander.Handshake(_validator.Handshake(handshakePayload));
        }

        /// <summary>
        /// Subscribe on command from server
        /// </summary>
        /// <param name="commandName">Command name</param>
        /// <param name="commandHandler">Handler for the subscribed command</param>
        public void On(string commandName, Action<PayloadData> commandHandler) {
            _commander.AddCommand(commandName, commandHandler);
        }

        /// <summary>
        /// Unsubscribe from command
        /// If callback is null -> clear all of them
        /// </summary>
        /// <param name="commandName">Command name</param>
        /// <param name="commandHandler">Handler to unsubscribe</param>
        public void Off(string commandName, Action<PayloadData> commandHandler = null) {
            _commander.CancelCommand(commandName, commandHandler);
        }

        /// <summary>
        /// Subscribe on request from server that wait response
        /// </summary>
        /// <param name="requestName">Request name</param>
        /// <param name="requestHandler">Handler for the subscribed request</param>
        public void OnRequest(string requestName, Action<PayloadData> requestHandler) {
            _commander.AddOnRequest(requestName, requestHandler);
        }

        /// <summary>
        /// Unsubscribe from request from server that wait response
        /// </summary>
        /// <param name="requestName">Request name</param>
        public void OffRequest(string requestName) {
            _commander.CancelOnRequest(requestName);
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
