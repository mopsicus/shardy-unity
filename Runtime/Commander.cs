using System;
using System.Collections.Generic;
using System.Threading;

#pragma warning disable 4014

namespace Shardy {

    /// <summary>
    /// Client commander to send/receive commands and requests
    /// </summary>
    class Commander {

        /// <summary>
        /// Log tag
        /// </summary>
        const string TAG = "COMMANDER";

        /// <summary>
        /// Timer timeout loop
        /// </summary>
        const float TIMEOUT_INTERVAL = 1000f;

        /// <summary>
        /// Timeout error code
        /// </summary>
        const string TIMEOUT_ERROR = "timeout";

        /// <summary>
        /// Maximum request ID representable exactly by the server's JSON number format
        /// </summary>
        const long MAX_SAFE_REQUEST_ID = 9007199254740991L;

        /// <summary>
        /// Callback on disconnect
        /// </summary>
        public Action<DisconnectReason> OnDisconnect = delegate { };

        /// <summary>
        /// Cached ready callback
        /// </summary>
        public Action OnReady = delegate { };

        /// <summary>
        /// Dictionary of request id and commands
        /// </summary>
        readonly Dictionary<long, string> _requestNames = null;

        /// <summary>
        /// Dictionary of notify command and their callbacks
        /// </summary>
        readonly Dictionary<string, List<Action<PayloadData>>> _commands = null;

        /// <summary>
        /// Protects command handlers from concurrent subscription changes while receiving data
        /// </summary>
        readonly object _locker = new object();

        /// <summary>
        /// Dictionary of request id and callback
        /// </summary>
        readonly Dictionary<long, Action<PayloadData>> _callbacks = null;

        /// <summary>
        /// Dictionary of request name and callback for requests from server
        /// </summary>
        readonly Dictionary<string, Action<PayloadData>> _requests = null;

        /// <summary>
        /// Dictionary of request id and start using time
        /// </summary>
        readonly Dictionary<long, DateTime> _timeouts = null;

        /// <summary>
        /// Protocol instance
        /// </summary>
        readonly Protocol _protocol = null;

        /// <summary>
        /// Pulse instance
        /// </summary>
        Pulse _pulse = null;

        /// <summary>
        /// Current request counter
        /// </summary>
        long _counter = 0;

        /// <summary>
        /// Validator
        /// </summary>
        readonly IValidator _validator = null;

        /// <summary>
        /// Serializer
        /// </summary>
        readonly ISerializer _serializer = null;

        /// <summary>
        /// Current options
        /// </summary>
        readonly ClientOptions _options = null;

        /// <summary>
        /// Cancellation token source
        /// </summary>
        readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

        /// <summary>
        /// Cached disconnect reason
        /// </summary>
        DisconnectReason _disconnectReason = DisconnectReason.Normal;

        /// <summary>
        /// Creates an instance of Commander
        /// </summary>
        /// <param name="connection">Client connection</param>
        /// <param name="validator">Current validator</param>
        /// <param name="serializer">Current serializer</param>
        /// <param name="options">Client options</param>
        public Commander(Connection connection, IValidator validator, ISerializer serializer, ClientOptions options) {
            _validator = validator;
            _serializer = serializer;
            _options = options;
            _requestNames = new Dictionary<long, string>();
            _commands = new Dictionary<string, List<Action<PayloadData>>>();
            _callbacks = new Dictionary<long, Action<PayloadData>>();
            _requests = new Dictionary<string, Action<PayloadData>>();
            _timeouts = new Dictionary<long, DateTime>();
            _protocol = new Protocol(connection, options.Block);
            _protocol.OnBlock = (block) => OnBlock(block);
            _protocol.OnDisconnect = () => OnClose();
        }

        /// <summary>
        /// Start use
        /// </summary>
        public void Start() {
            Utils.SetTimer(TIMEOUT_INTERVAL, _cancellation, () => TimeoutCheck());
            _pulse = new Pulse(_options.PulseInterval);
            _pulse.OnPulse = () => OnPulse();
            _protocol.Start();
        }

        /// <summary>
        /// Timer check for timeout RPC call
        /// </summary>
        void TimeoutCheck() {
            var requestIds = new List<long>(_timeouts.Keys);
            foreach (var requestId in requestIds) {
                if (!_timeouts.TryGetValue(requestId, out var requestStart)) {
                    continue;
                }
                var span = DateTime.Now - requestStart;
                if (span.TotalMilliseconds > _options.RequestTimeout) {
                    var payload = new PayloadData();
                    payload.Type = PayloadType.Response;
                    payload.Id = requestId;
                    payload.Name = _requestNames[requestId];
                    payload.Data = new byte[0];
                    payload.Error = TIMEOUT_ERROR;
                    OnPayload(payload);
                }
            }
        }

        /// <summary>
        /// Add callback for request
        /// </summary>
        /// <param name="requestId">Request id</param>
        /// <param name="requestName">Request name</param>
        /// <param name="responseCallback">Callback for request</param>
        void AddRequest(long requestId, string requestName, Action<PayloadData> responseCallback) {
            if (_callbacks.ContainsKey(requestId)) {
#if SHARDY_DEBUG_RAW
                Logger.Warning($"callback already exists: {requestId}, method: {_callbacks[requestId].Method}", TAG);
#endif
            } else {
                if (requestId < 0 || requestId > MAX_SAFE_REQUEST_ID) {
#if SHARDY_DEBUG_RAW
                    Logger.Warning($"request id is outside the safe integer range: {requestId}", TAG);
#endif
                    return;
                }
                _requestNames.Add(requestId, requestName);
                _callbacks.Add(requestId, responseCallback);
                _timeouts.Add(requestId, DateTime.Now);
            }
        }

        /// <summary>
        /// Remove request from list
        /// </summary>
        /// <param name="requestId">Request id</param>
        public void CancelRequest(long requestId) {
            if (!_callbacks.ContainsKey(requestId)) {
#if SHARDY_DEBUG_RAW
                Logger.Warning($"unknown callback to cancel: {requestId}", TAG);
#endif
                return;
            }
            _requestNames.Remove(requestId);
            _callbacks.Remove(requestId);
            _timeouts.Remove(requestId);
        }

        /// <summary>
        /// Exec callback
        /// </summary>
        /// <param name="payload">Data for callback</param>
        void InvokeRequest(PayloadData payload) {
            if (!_callbacks.ContainsKey(payload.Id)) {
#if SHARDY_DEBUG_RAW
                Logger.Warning($"unknown callback to execute: {payload.Id}", TAG);
#endif
                return;
            }
            var callback = _callbacks[payload.Id];
            RemoveRequest(payload.Id);
            try {
                callback.Invoke(payload);
            } catch (Exception e) {
#if SHARDY_DEBUG_RAW
                Logger.Error($"callback execute failed: {payload.Id}, error: {e}", TAG);
#endif       
            }
        }

        /// <summary>
        /// Remove a request from the internal tracking lists
        /// </summary>
        /// <param name="requestId">ID of the request to remove</param>
        void RemoveRequest(long requestId) {
            _requestNames.Remove(requestId);
            _callbacks.Remove(requestId);
            _timeouts.Remove(requestId);
        }

        /// <summary>
        /// Subscribe callback on command
        /// </summary>
        /// <param name="commandName">Command name</param>
        /// <param name="commandHandler">Handler for the command</param>
        public void AddCommand(string commandName, Action<PayloadData> commandHandler) {
            lock (_locker) {
                if (_commands.TryGetValue(commandName, out var list)) {
                    list.Add(commandHandler);
                } else {
                    list = new List<Action<PayloadData>>();
                    list.Add(commandHandler);
                    _commands.Add(commandName, list);
                }
            }
        }

        /// <summary>
        /// Unsubscribe callback from command
        /// If callback is null -> clear all of them
        /// </summary>
        /// <param name="commandName">Command name</param>
        /// <param name="commandHandler">Handler to remove</param>
        public void CancelCommand(string commandName, Action<PayloadData> commandHandler) {
            lock (_locker) {
                if (!_commands.TryGetValue(commandName, out var list)) {
#if SHARDY_DEBUG_RAW
                    Logger.Warning($"unknown command to unsubscribe: {commandName}", TAG);
#endif
                    return;
                }
                if (commandHandler == null) {
                    list.Clear();
                } else {
                    for (var i = list.Count - 1; i >= 0; i--) {
                        if (list[i].Equals(commandHandler)) {
                            list.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Exec callback on command
        /// </summary>
        /// <param name="payload">Data for command</param>
        void InvokeCommand(PayloadData payload) {
            Action<PayloadData>[] handlers;
            lock (_locker) {
                if (!_commands.TryGetValue(payload.Name, out var list)) {
#if SHARDY_DEBUG_RAW
                    Logger.Warning($"unknown command to execute: {payload.Name}", TAG);
#endif
                    return;
                }
                handlers = list.ToArray();
            }
            foreach (var action in handlers) {
                try {
                    action.Invoke(payload);
                } catch (Exception e) {
#if SHARDY_DEBUG_RAW
                    Logger.Error($"command execute failed: {payload.Name}, error: {e}", TAG);
#endif        
                }
            }
        }

        /// <summary>
        /// Subscribe to request from server that wait response
        /// </summary>
        /// <param name="requestName">Request name</param>
        /// <param name="requestHandler">Handler for the request</param>
        public void AddOnRequest(string requestName, Action<PayloadData> requestHandler) {
            if (_requests.ContainsKey(requestName)) {
#if SHARDY_DEBUG_RAW
                Logger.Warning($"request already exists: {requestName}, method: {_requests[requestName].Method}", TAG);
#endif
            } else {
                _requests.Add(requestName, requestHandler);
            }
        }

        /// <summary>
        /// Unsubscribe from request from server that wait response
        /// </summary>
        /// <param name="requestName">Request name</param>
        public void CancelOnRequest(string requestName) {
            if (!_requests.ContainsKey(requestName)) {
#if SHARDY_DEBUG_RAW
                Logger.Warning($"unknown request to cancel: {requestName}", TAG);
#endif
                return;
            }
            _requests.Remove(requestName);
        }

        /// <summary>
        /// Exec callback for RPC from server
        /// </summary>
        /// <param name="payload">Request data</param>
        public void InvokeOnRequest(PayloadData payload) {
            if (!_requests.ContainsKey(payload.Name)) {
#if SHARDY_DEBUG_RAW
                Logger.Warning($"unknown request to execute: {payload.Name}", TAG);
#endif
                Error(payload, "unknown request");
                return;
            }
            try {
                _requests[payload.Name].Invoke(payload);
            } catch (Exception e) {
#if SHARDY_DEBUG_RAW
                Logger.Error($"request execute failed: {payload.Name}, error: {e}", TAG);
#endif     
                Error(payload, e.Message);
            }
        }

        /// <summary>
        /// Send ping
        /// </summary>
        void Heartbeat() {
#if SHARDY_DEBUG
            Logger.Info("-> heartbeat");
#endif
            _protocol.Heartbeat();
        }

        /// <summary>
        /// Send handshake
        /// </summary>
        /// <param name="data">Data to handshake</param>
        public void Handshake(byte[] handshakePayload) {
#if SHARDY_DEBUG
            Logger.Info("-> handshake");
#endif
            _protocol.Handshake(handshakePayload);
        }

        /// <summary>
        /// Send acknowledge
        /// </summary>
        /// <param name="data">Data to acknowledge</param>
        public void Acknowledge(byte[] acknowledgementPayload) {
#if SHARDY_DEBUG
            Logger.Info("-> acknowledge");
#endif
            _protocol.Acknowledge(acknowledgementPayload);
        }

        /// <summary>
        /// Disconnect from server
        /// </summary>
        public void Disconnect() {
#if SHARDY_DEBUG
            Logger.Info("-> disconnect");
#endif
            _protocol.Disconnect();
        }

        /// <summary>
        /// Send command (event) to server with params
        /// </summary>
        /// <param name="commandName">Command name</param>
        /// <param name="commandPayload">Command payload bytes</param>
        public void Command(string commandName, byte[] commandPayload) {
#if SHARDY_DEBUG
            Logger.Info($"-> command: {commandName}");
#endif
            var payload = Payload.Encode(_serializer, PayloadType.Command, commandName, 0, commandPayload, "");
            _protocol.Send(payload);
        }

        /// <summary>
        /// Send response on request from server
        /// </summary>
        /// <param name="requestPayload">Request received from the server</param>
        /// <param name="responsePayload">Response payload bytes</param>
        public void Response(PayloadData requestPayload, byte[] responsePayload = null) {
#if SHARDY_DEBUG
            Logger.Info($"-> response: {requestPayload.Id}.{requestPayload.Name}");
#endif
            var payload = Payload.Encode(_serializer, PayloadType.Response, requestPayload.Name, requestPayload.Id, responsePayload, "");
            _protocol.Send(payload);
        }

        /// <summary>
        /// Send error on request from server
        /// </summary>
        /// <param name="requestPayload">Request received from the server</param>
        /// <param name="errorMessage">Error message or code</param>
        /// <param name="responsePayload">Response payload bytes</param>
        public void Error(PayloadData requestPayload, string errorMessage, byte[] responsePayload = null) {
#if SHARDY_DEBUG
            Logger.Info($"-> error: {requestPayload.Id}.{requestPayload.Name}, error: {errorMessage}");
#endif
            var payload = Payload.Encode(_serializer, PayloadType.Response, requestPayload.Name, requestPayload.Id, responsePayload, errorMessage);
            _protocol.Send(payload);
        }

        /// <summary>
        /// Clear all events
        /// </summary>
        public void Clear() {
            _cancellation.Cancel();
            var pendingCallbacks = new List<KeyValuePair<long, Action<PayloadData>>>(_callbacks);
            var pendingNames = new Dictionary<long, string>(_requestNames);
            _requestNames.Clear();
            _callbacks.Clear();
            _timeouts.Clear();
            lock (_locker) {
                _commands.Clear();
            }
            _requests.Clear();
            _pulse?.Clear();
            foreach (var pending in pendingCallbacks) {
                if (pendingNames.TryGetValue(pending.Key, out var requestName)) {
                    try {
                        pending.Value.Invoke(new PayloadData(PayloadType.Response, requestName, pending.Key, new byte[0], "closed"));
                    } catch (Exception e) {
#if SHARDY_DEBUG_RAW
                        Logger.Error($"request callback failed: {e}", TAG);
#endif
                    }
                }
            }
        }

        /// <summary>
        /// Send a request to the server and register a response callback
        /// </summary>
        /// <param name="requestName">Name of the request</param>
        /// <param name="responseCallback">Callback to handle the response</param>
        /// <param name="requestPayload">Payload of the request</param>
        /// <returns>Request ID</returns>
        public long Request(string requestName, Action<PayloadData> responseCallback, byte[] requestPayload = null) {
            if (_counter > MAX_SAFE_REQUEST_ID) {
#if SHARDY_DEBUG_RAW
                Logger.Warning($"maximum safe request id has been reached: {_counter}", TAG);
#endif
                return -1;
            }
            var requestId = _counter++;
#if SHARDY_DEBUG
            Logger.Info($"-> request: {requestId}.{requestName}");
#endif
            var payload = Payload.Encode(_serializer, PayloadType.Request, requestName, requestId, requestPayload, string.Empty);
            AddRequest(requestId, requestName, responseCallback);
            try {
                _protocol.Send(payload);
            } catch {
                RemoveRequest(requestId);
#if SHARDY_DEBUG_RAW
                Logger.Warning($"request failed: {requestId}.{requestName}");
#endif
                return -1;
            }
            return requestId;
        }

        /// <summary>
        /// Process data from protocol
        /// </summary>
        /// <param name="block">Block type and possible data</param>
        void OnBlock(BlockData block) {
#if SHARDY_DEBUG_RAW
            Logger.Info($"block: {block.Type}, data: {Utils.DataToDebug(block.Body)}", TAG);
#endif
            switch (block.Type) {
                case BlockType.Heartbeat:
                    OnHeartbeat();
                    break;
                case BlockType.Kick:
                    OnKick(block);
                    break;
                case BlockType.HandshakeAcknowledgement:
                    OnAcknowledgement(block);
                    break;
                case BlockType.Data:
                    try {
                        var payload = Payload.Decode(_serializer, block.Body);
                        if (Payload.Check(payload)) {
                            OnPayload(payload);
                        } else {
#if SHARDY_DEBUG_RAW
                            Logger.Warning("invalid payload", TAG);
#endif
                            _disconnectReason = DisconnectReason.Unknown;
                            Disconnect();
                        }
                    } catch (Exception e) {
#if SHARDY_DEBUG_RAW
                        Logger.Error($"payload decode failed: {e}", TAG);
#endif
                        _disconnectReason = DisconnectReason.Unknown;
                        Disconnect();
                    }
                    break;
                default:
#if SHARDY_DEBUG_RAW
                    Logger.Warning($"not implemented block type: {block.Type}", TAG);
#endif
                    break;
            }
        }

        /// <summary>
        /// Process payload with commands/request/responses
        /// When received data, send heartbeat for ok
        /// </summary>
        /// <param name="payload">payload Decoded payload data</param>
        void OnPayload(PayloadData payload) {
            _pulse.Reset();
            Heartbeat();
            switch (payload.Type) {
                case PayloadType.Command:
#if SHARDY_DEBUG
                    Logger.Info($"<- command: {payload.Name}, data: {Utils.DataToDebug(payload.Data)}");
#endif
                    InvokeCommand(payload);
                    break;
                case PayloadType.Request:
#if SHARDY_DEBUG
                    Logger.Info($"<- request: {payload.Id}.{payload.Name}, data: {Utils.DataToDebug(payload.Data)}");
#endif
                    InvokeOnRequest(payload);
                    break;
                case PayloadType.Response:
#if SHARDY_DEBUG
                    if (string.IsNullOrEmpty(payload.Error)) {
                        Logger.Info($"<- response: {payload.Id}.{payload.Name}, data: {Utils.DataToDebug(payload.Data)}");
                    } else {
                        Logger.Info($"<- error: {payload.Id}.{payload.Name}, error: {payload.Error}, data: {Utils.DataToDebug(payload.Data)}");
                    }
#endif
                    InvokeRequest(payload);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Event from protocol when connection closed
        /// </summary>
        void OnClose() {
#if SHARDY_DEBUG
            Logger.Info("<- disconnect");
#endif            
            Clear();
            OnDisconnect(_disconnectReason);
        }

        /// <summary>
        /// Process acknowledgement
        /// </summary>
        /// <param name="block">Acknowledgement data</param>
        void OnAcknowledgement(BlockData block) {
#if SHARDY_DEBUG
            Logger.Info("<- acknowledge");
#endif
            _pulse.Reset();
            var state = ValidatorState.Failed;
            try {
                state = _validator.VerifyAcknowledgement(block.Body);
            } catch (Exception e) {
#if SHARDY_DEBUG_RAW
                Logger.Error($"acknowledgement validation failed: {e}", TAG);
#endif
                _disconnectReason = DisconnectReason.Handshake;
                Disconnect();
                return;
            }
#if SHARDY_DEBUG_RAW
            Logger.Info($"acknowledgement data: {Utils.DataToDebug(block.Body)}, validation state: {state}", TAG);
#endif
            if (state == ValidatorState.Success) {
                try {
                    Acknowledge(_validator.Acknowledgement(block.Body));
                } catch (Exception e) {
#if SHARDY_DEBUG_RAW
                    Logger.Error($"acknowledgement creation failed: {e}", TAG);
#endif
                    _disconnectReason = DisconnectReason.Handshake;
                    Disconnect();
                    return;
                }
#if SHARDY_DEBUG
                Logger.Info("ready to work");
#endif
                OnReady();
            } else {
                _disconnectReason = DisconnectReason.Handshake;
                Disconnect();
            }
        }

        /// <summary>
        /// Process heartbeat from server
        /// </summary>
        void OnHeartbeat() {
#if SHARDY_DEBUG
            Logger.Info("<- heartbeat");
#endif
            _pulse.Reset();
        }

        /// <summary>
        /// Process kick
        /// </summary>
        /// <param name="block">Kick reason data</param>
        void OnKick(BlockData block) {
            if (int.TryParse(Utils.DataToString(block.Body), out var index) && Enum.IsDefined(typeof(DisconnectReason), index)) {
                _disconnectReason = (DisconnectReason)index;
            } else {
                _disconnectReason = DisconnectReason.Unknown;
            }
#if SHARDY_DEBUG
            Logger.Info($"<- kick: {_disconnectReason}");
#endif
            _pulse.Reset();
        }

        /// <summary>
        /// No answer from connection
        /// </summary>
        void OnPulse() {
#if SHARDY_DEBUG
            Logger.Info("pulse timeout, send heartbeat");
#endif
            Heartbeat();
        }

        /// <summary>
        /// Destroy all
        /// </summary>
        public void Destroy() {
#if SHARDY_DEBUG_RAW
            Logger.Info("destroy", TAG);
#endif            
            Clear();
            _protocol.Destroy();
        }
    }
}
