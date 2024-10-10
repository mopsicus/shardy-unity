using System;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
#if UNITY_IOS
using System.Runtime.InteropServices;
#endif

namespace Shardy {

    /// <summary>
    /// Common connection class
    /// </summary>
    public class Connection {

        /// <summary>
        /// Log tag
        /// </summary>
        const string TAG = "CONNECTION";

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// Cached websocket for WebGL
        /// </summary>
        WebSocket _webglsocket = null;
#else
        /// <summary>
        /// Cached websocket
        /// </summary>
        ClientWebSocket _websocket = null;
#endif

        /// <summary>
        /// Cached socket
        /// </summary>
        Socket _tcpsocket = null;

        /// <summary>
        /// Received buffer array
        /// </summary>
        readonly byte[] _received = null;

        /// <summary>
        /// Connection transport type
        /// </summary>
        readonly TransportType _type = TransportType.Tcp;

        /// <summary>
        /// Cached transport data
        /// </summary>
        ReceivedData _data = new ReceivedData();

        /// <summary>
        /// Cancellation token source
        /// </summary>
        CancellationTokenSource _cancellation = null;

        /// <summary>
        /// Used flag
        /// </summary>
        bool _isUsed = false;

#if UNITY_IOS
        /// <summary>
        /// Check and convert IP to IPv6 on iOS
        /// </summary>
        [DllImport("__Internal")]
        static extern string CheckIPv6(string host);
#endif

        /// <summary>
        /// Create new connection
        /// </summary>
        /// <param name="type">Transport type</param>
        /// <param name="size">Buffer size</param>
        public Connection(TransportType type, int size) {
            _type = type;
#if SHARDY_DEBUG_RAW
            Logger.Info($"new connection: {_type}", TAG);
#endif
            _received = new byte[size];
            CreateSocket();
        }

        /// <summary>
        /// Create socket instance
        /// </summary>
        void CreateSocket() {
            _cancellation = new CancellationTokenSource();
            switch (_type) {
                case TransportType.Tcp:
                    _tcpsocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    break;
                case TransportType.WebSocket:
#if UNITY_WEBGL && !UNITY_EDITOR
                    _webglsocket = new WebSocket();
                    _webglsocket.OnError += OnWebSocketError;
                    _webglsocket.OnClose += OnWebSocketClose;
#else
                    _websocket = new ClientWebSocket();
#endif
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Check connection to host
        /// </summary>
        public bool IsConnected {
            get {
                return _type switch {
                    TransportType.Tcp => _tcpsocket.Connected,
#if UNITY_WEBGL && !UNITY_EDITOR
                    TransportType.WebSocket => _webglsocket.State == WebSocketStateCode.Open,
#else
                    TransportType.WebSocket => _websocket.State == WebSocketState.Open,
#endif
                    _ => false,
                };
            }
        }

        /// <summary>
        /// Open connection
        /// </summary>
        /// <param name="host">Host to connect</param>
        /// <param name="port">Port to connect</param>
        public async Task<bool> Open(string host, int port) {
            if (_isUsed) {
                DeleteSocket();
                CreateSocket();
            } else {
                _isUsed = true;
            }
            try {
#if UNITY_IOS && !UNITY_EDITOR
                var ipv6 = CheckIPv6(host);
                if (!string.IsNullOrEmpty(ipv6) && Socket.OSSupportsIPv6) {
                    host = ipv6;
                }
                _tcpsocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
#endif
                switch (_type) {
                    case TransportType.Tcp:
#if SHARDY_DEBUG
                        Logger.Info($"-> connect to {host}:{port}");
#endif                                
                        await _tcpsocket.ConnectAsync(host, port).WithCancellation(_cancellation.Token);
                        break;
                    case TransportType.WebSocket:
                        var url = $"ws://{host}:{port}";
#if SHARDY_DEBUG
                        Logger.Info($"-> connect to {url}");
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
                        await _webglsocket.Connect(url).WithCancellation(_cancellation.Token);
#else
                        await _websocket.ConnectAsync(new Uri(url), CancellationToken.None).WithCancellation(_cancellation.Token); ;
#endif
                        break;
                    default:
                        break;
                }
                return true;
            } catch (OperationCanceledException) {
#if SHARDY_DEBUG_RAW
                Logger.Info("open operation canceled", TAG);
#endif
                return false;
            } catch (SocketException e) {
                if (e.SocketErrorCode == SocketError.ConnectionRefused) {
#if SHARDY_DEBUG_RAW
                    Logger.Info("open socket refused", TAG);
#endif
                } else {
#if SHARDY_DEBUG_RAW
                    Logger.Error($"open socket failed: {e}", TAG);
#endif 
                }
                return false;
            } catch (WebSocketException e) {
                if (e.GetBaseException() is SocketException exception) {
                    if (exception.SocketErrorCode == SocketError.ConnectionRefused) {
#if SHARDY_DEBUG_RAW
                        Logger.Info("open websocket refused", TAG);
#endif
                    } else {
#if SHARDY_DEBUG_RAW
                        Logger.Error($"open websocket as socket failed: {e}", TAG);
#endif                                
                    }
                } else {
#if SHARDY_DEBUG_RAW
                    Logger.Error($"open websocket failed: {e}", TAG);
#endif                       
                }
                return false;
            } catch (Exception e) {
#if SHARDY_DEBUG_RAW
                Logger.Error($"open failed: {e}", TAG);
#endif     
                return false;
            }
        }

        /// <summary>
        /// Close connection
        /// </summary>
        public void Close() {
            switch (_type) {
                case TransportType.Tcp:
                    if (_tcpsocket.Connected) {
                        _tcpsocket.Shutdown(SocketShutdown.Both);
                        _tcpsocket.Close();
                    }
                    break;
                case TransportType.WebSocket:
#if UNITY_WEBGL && !UNITY_EDITOR
                    if (_webglsocket.State == WebSocketStateCode.Open) {
                        _webglsocket.Close(WebSocketCloseCode.Normal);
                    }
#else
                    switch (_websocket.State) {
                        case WebSocketState.Open:
                        case WebSocketState.CloseSent:
                        case WebSocketState.CloseReceived:
                            _websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                            break;
                        default:
                            break;
                    }
#endif
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Send data to socket
        /// </summary>
        /// <param name="buffer">Data to send</param>
        public async Task Send(byte[] buffer) {
            switch (_type) {
                case TransportType.Tcp:
                    await _tcpsocket.SendAsync(buffer, SocketFlags.None).WithCancellation(_cancellation.Token);
                    break;
                case TransportType.WebSocket:
#if UNITY_WEBGL && !UNITY_EDITOR
                    await _webglsocket.Send(buffer).WithCancellation(_cancellation.Token);
#else
                    await _websocket.SendAsync(buffer, WebSocketMessageType.Binary, true, CancellationToken.None).WithCancellation(_cancellation.Token);
#endif
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Receive data from socket
        /// </summary>
        public async Task<ReceivedData> Receive() {
            switch (_type) {
                case TransportType.Tcp:
                    _data.Length = await _tcpsocket.ReceiveAsync(_received, SocketFlags.None).WithCancellation(_cancellation.Token);
                    break;
                case TransportType.WebSocket:
#if UNITY_WEBGL && !UNITY_EDITOR
                    _data.Length = await _webglsocket.Receive(_received).WithCancellation(_cancellation.Token);
#else
                    var result = await _websocket.ReceiveAsync(_received, CancellationToken.None).WithCancellation(_cancellation.Token);
                    _data.Length = (result.MessageType == WebSocketMessageType.Close) ? 0 : result.Count;
#endif
                    break;
                default:
                    break;
            }
            _data.Body = _received;
            return _data;
        }

        /// <summary>
        /// Cancel operations
        /// </summary>
        public void Cancel() {
            _cancellation.Cancel();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// Callback on webglsocket error
        /// </summary>
        /// <param name="code">Websocket error code</param>
        void OnWebSocketError(WebSocketErrorCode code) {
#if SHARDY_DEBUG_RAW
            Logger.Error($"webgl error: {code}", TAG);
#endif
        }

        /// <summary>
        /// Callback on webglsocket close
        /// </summary>
        /// <param name="code">Websocket close code</param>
        void OnWebSocketClose(WebSocketCloseCode code) {
#if SHARDY_DEBUG_RAW
            Logger.Error($"webgl close: {code}", TAG);
#endif
            Cancel();
        }
#endif

        /// <summary>
        /// Delete socket instance
        /// </summary>
        void DeleteSocket() {
            try {
                _cancellation.Dispose();
                switch (_type) {
                    case TransportType.Tcp:
                        _tcpsocket.Dispose();
                        _tcpsocket = null;
                        break;
                    case TransportType.WebSocket:
#if UNITY_WEBGL && !UNITY_EDITOR
                        WebSocketManager.Destroy(_webglsocket.Id);
                        _webglsocket = null;
#else
                        _websocket.Dispose();
                        _websocket = null;
#endif
                        break;
                    default:
                        break;
                }
            } catch (Exception e) {
#if SHARDY_DEBUG_RAW
                Logger.Error($"delete failed: {e}", TAG);
#endif 
            }
        }

        /// <summary>
        /// Destroy connection
        /// </summary>
        public void Destroy() {
#if SHARDY_DEBUG_RAW
            Logger.Info("destroy", TAG);
#endif            
            try {
                Cancel();
                Close();
                DeleteSocket();
            } catch (Exception e) {
#if SHARDY_DEBUG_RAW
                Logger.Error($"destroy failed: {e}", TAG);
#endif        
            }
        }
    }
}