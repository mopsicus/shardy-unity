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
        readonly Dictionary<int, string> _names = null;

        /// <summary>
        /// Dictionary of notify command and their callbacks
        /// </summary>
        readonly Dictionary<string, List<Action<PayloadData>>> _commands = null;

        /// <summary>
        /// Dictionary of request id and callback
        /// </summary>
        readonly Dictionary<int, Action<PayloadData>> _callbacks = null;

        /// <summary>
        /// Dictionary of request name and callback for requests from server
        /// </summary>
        readonly Dictionary<string, Action<PayloadData>> _requests = null;

        /// <summary>
        /// Dictionary of request id and start using time
        /// </summary>
        readonly Dictionary<int, DateTime> _timeouts = null;

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
        int _counter = 1;

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
            _names = new Dictionary<int, string>();
            _commands = new Dictionary<string, List<Action<PayloadData>>>();
            _callbacks = new Dictionary<int, Action<PayloadData>>();
            _requests = new Dictionary<string, Action<PayloadData>>();
            _timeouts = new Dictionary<int, DateTime>();
            _protocol = new Protocol(connection);
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
            foreach (var id in _timeouts.Keys) {
                var span = DateTime.Now - _timeouts[id];
                var timeout = (int)span.TotalMilliseconds;
                if (timeout > _options.RequestTimeout) {
                    var payload = new PayloadData();
                    payload.Type = PayloadType.Response;
                    payload.Id = id;
                    payload.Name = _names[id];
                    payload.Error = TIMEOUT_ERROR;
                    OnPayload(payload);
                }
            }
        }

        /// <summary>
        /// Add callback for request
        /// </summary>
        /// <param name="id">Request id</param>
        /// <param name="command">Request command</param>
        /// <param name="param">Request's params</param>
        /// <param name="callback">Callback for request</param>
        void AddRequest(int id, string command, byte[] param, Action<PayloadData> callback) {
            if (_callbacks.ContainsKey(id)) {
#if SHARDY_DEBUG_RAW
                Logger.Warning($"callback already exists: {id}, method: {_callbacks[id].Method}", TAG);
#endif
            } else {
                if (id <= 0) {
#if SHARDY_DEBUG_RAW
                    Logger.Warning($"callback id can't be less 1: {id}", TAG);
#endif
                    return;
                }
                _names.Add(id, command);
                _callbacks.Add(id, callback);
                _timeouts.Add(id, DateTime.Now);
            }
        }

        /// <summary>
        /// Remove request from list
        /// </summary>
        /// <param name="id">Request id</param>
        public void CancelRequest(int id) {
            if (!_callbacks.ContainsKey(id)) {
#if SHARDY_DEBUG_RAW
                Logger.Warning($"unknown callback to cancel: {id}", TAG);
#endif
                return;
            }
            _names.Remove(id);
            _callbacks.Remove(id);
            _timeouts.Remove(id);
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
            try {
                _callbacks[payload.Id].Invoke(payload);
            } catch (Exception e) {
#if SHARDY_DEBUG_RAW
                Logger.Error($"callback execute failed: {payload.Id}, error: {e}", TAG);
#endif       
            }
            CancelRequest(payload.Id);
        }

        /// <summary>
        /// Subscribe callback on command
        /// </summary>
        /// <param name="command">Command name</param>
        /// <param name="callback">Callback for command</param>
        public void AddCommand(string command, Action<PayloadData> callback) {
            if (_commands.TryGetValue(command, out var list)) {
                list.Add(callback);
                _commands[command] = list;
            } else {
                list = new List<Action<PayloadData>>();
                list.Add(callback);
                _commands.Add(command, list);
            }
        }

        /// <summary>
        /// Unsubscribe callback from command
        /// If callback is null -> clear all of them
        /// </summary>
        /// <param name="command">Command name</param>
        /// <param name="callback">Callback for command</param>
        public void CancelCommand(string command, Action<PayloadData> callback) {
            if (!_commands.ContainsKey(command)) {
#if SHARDY_DEBUG_RAW
                Logger.Warning($"unknown command to unsubscribe: {command}", TAG);
#endif
                return;
            }
            if (callback == null) {
                _commands[command].Clear();
            } else {
                var list = _commands[command];
                for (var i = list.Count - 1; i >= 0; i--) {
                    if (list[i].Equals(callback)) {
                        list.RemoveAt(i);
                        break;
                    }
                    _commands[command] = list;
                }
            }
        }

        /// <summary>
        /// Exec callback on command
        /// </summary>
        /// <param name="payload">Data for command</param>
        void InvokeCommand(PayloadData payload) {
            if (!_commands.ContainsKey(payload.Name)) {
#if SHARDY_DEBUG_RAW
                Logger.Warning($"unknown command to execute: {payload.Name}", TAG);
#endif
                return;
            }
            var list = _commands[payload.Name];
            foreach (var action in list) {
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
        /// <param name="request">Request name</param>
        /// <param name="callback">Callback on RPC</param>
        public void AddOnRequest(string request, Action<PayloadData> callback) {
            if (_requests.ContainsKey(request)) {
#if SHARDY_DEBUG_RAW
                Logger.Warning($"request already exists: {request}, method: {_requests[request].Method}", TAG);
#endif
            } else {
                _requests.Add(request, callback);
            }
        }

        /// <summary>
        /// Unsubscribe from request from server that wait response
        /// </summary>
        /// <param name="request">Request name</param>
        public void CancelOnRequest(string request) {
            if (!_requests.ContainsKey(request)) {
#if SHARDY_DEBUG_RAW
                Logger.Warning($"unknown request to cancel: {request}", TAG);
#endif
                return;
            }
            _requests.Remove(request);
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
                return;
            }
            try {
                _requests[payload.Name].Invoke(payload);
            } catch (Exception e) {
#if SHARDY_DEBUG_RAW
                Logger.Error($"request execute failed: {payload.Name}, error: {e}", TAG);
#endif     
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
        public void Handshake(byte[] data) {
#if SHARDY_DEBUG
            Logger.Info("-> handshake");
#endif
            _protocol.Handshake(data);
        }

        /// <summary>
        /// Send acknowledge
        /// </summary>
        /// <param name="data">Data to acknowledge</param>
        public void Acknowledge(byte[] data) {
#if SHARDY_DEBUG
            Logger.Info("-> acknowledge");
#endif
            _protocol.Acknowledge(data);
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
        /// <param name="command">Command name</param>
        /// <param name="data">Payload data</param>
        public void Command(string command, byte[] data) {
#if SHARDY_DEBUG
            Logger.Info($"-> command: {command}");
#endif
            var payload = Payload.Encode(_serializer, PayloadType.Command, command, 0, data, "");
            _protocol.Send(payload);
        }

        /// <summary>
        /// Send request to server and wait response
        /// </summary>
        /// <param name="request">Request name</param>
        /// <param name="data">Payload data</param>
        /// <param name="callback">Answer from server</param>
        /// <returns>Request id</returns>
        public int Request(string request, byte[] data, Action<PayloadData> callback) {
#if SHARDY_DEBUG
            Logger.Info($"-> request: {_counter}.{request}");
#endif
            var payload = Payload.Encode(_serializer, PayloadType.Request, request, _counter, data, "");
            AddRequest(_counter, request, data, callback);
            _protocol.Send(payload);
            _counter++;
            return _counter;
        }

        /// <summary>
        /// Send response on request from server
        /// </summary>
        /// <param name="request">Request data</param>
        /// <param name="data">Data</param>
        public void Response(PayloadData request, byte[] data = null) {
#if SHARDY_DEBUG
            Logger.Info($"-> response: {request.Id}.{request.Name}");
#endif
            var payload = Payload.Encode(_serializer, PayloadType.Response, request.Name, request.Id, data, "");
            _protocol.Send(payload);
        }

        /// <summary>
        /// Send error on request from server
        /// </summary>
        /// <param name="request">Request data</param>
        /// <param name="error">Error message or code</param>
        /// <param name="data">Data</param>
        public void Error(PayloadData request, string error, byte[] data = null) {
#if SHARDY_DEBUG
            Logger.Info($"-> error: {request.Id}.{request.Name}, error: {error}");
#endif
            var payload = Payload.Encode(_serializer, PayloadType.Response, request.Name, request.Id, data, error);
            _protocol.Send(payload);
        }

        /// <summary>
        /// Clear all events
        /// </summary>
        public void Clear() {
            _cancellation.Cancel();
            _names.Clear();
            _callbacks.Clear();
            _timeouts.Clear();
            _commands.Clear();
            _requests.Clear();
            _pulse?.Clear();
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
                    var payload = Payload.Decode(_serializer, block.Body);
                    if (Payload.Check(payload)) {
                        OnPayload(payload);
                    } else {
#if SHARDY_DEBUG_RAW
                        Logger.Warning($"invalid payload: {payload}", TAG);
#endif
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
            var state = _validator.VerifyAcknowledgement(block.Body);
#if SHARDY_DEBUG_RAW
            Logger.Info($"acknowledgement data: {Utils.DataToDebug(block.Body)}, validation state: {state}", TAG);
#endif
            if (state == ValidatorState.Success) {
                Acknowledge(_validator.Acknowledgement(block.Body));
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
            int.TryParse(Utils.DataToString(block.Body), out var index);
            _disconnectReason = (DisconnectReason)index;
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