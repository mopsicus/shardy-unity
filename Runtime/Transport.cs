using System;
using System.Net.WebSockets;

namespace Shardy {

    /// <summary>
    /// Transporter state
    /// </summary>
    enum TransportState {

        /// <summary>
        /// Receive head data
        /// </summary>        
        Head,

        /// <summary>
        /// Receive body data
        /// </summary>        
        Body,

        /// <summary>
        /// Transport is closed, no more data received
        /// </summary>        
        Closed
    }

    /// <summary>
    /// Transporter for protocol
    /// Manage how data should send and receive to protocol
    /// </summary>
    class Transport {

        /// <summary>
        /// Log tag
        /// </summary>
        const string TAG = "TRANSPORT";

        /// <summary>
        /// Callback to return data to handler
        /// </summary>
        public Action<byte[]> OnData = delegate { };

        /// <summary>
        /// Callback to return disconnect state
        /// </summary>
        public Action OnDisconnect = delegate { };

        /// <summary>
        /// Current transporter state
        /// </summary>
        volatile TransportState _state = TransportState.Closed;

        /// <summary>
        /// Indicates whether the disconnection event has been notified to the listener
        /// </summary>
        bool _isDisconnectNotified = false;

        /// <summary>
        /// Common buffer array
        /// </summary>
        byte[] _buffer = null;

        /// <summary>
        /// Current frame header
        /// </summary>
        readonly byte[] _head = new byte[Block.BLOCK_HEAD];

        /// <summary>
        /// Offset to write buffer
        /// </summary>
        int _offset = 0;

        /// <summary>
        /// Length of current package
        /// </summary>
        int _package = 0;

        /// <summary>
        /// Current connection
        /// </summary>
        Connection _connection = null;

        /// <summary>
        /// Maximum allowed block body size
        /// </summary>
        readonly int _maxBlockBodySize = 0;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connection">Connection instance</param>
        public Transport(Connection connection, int maxBlockBodySize) {
            _connection = connection;
            _maxBlockBodySize = maxBlockBodySize;
        }

        /// <summary>
        /// Start receiving data
        /// </summary>
        public void Start() {
            _state = TransportState.Head;
            Receive();
        }

        /// <summary>
        /// Send data to socket
        /// </summary>
        /// <param name="buffer">Bytes array</param>
        public async void Dispatch(byte[] buffer) {
            if (!_connection.IsConnected) {
#if SHARDY_DEBUG_RAW
                Logger.Warning("try send data with disconnected", TAG);
#endif                   
                return;
            }
            if (_state != TransportState.Closed) {
                try {
                    await _connection.Send(buffer);
                } catch (OperationCanceledException) {
#if SHARDY_DEBUG_RAW
                    Logger.Info("send operation canceled", TAG);
#endif
                } catch (Exception e) {
#if SHARDY_DEBUG_RAW
                    Logger.Error($"send failed: {e}", TAG);
#endif    
                }
            } else {
#if SHARDY_DEBUG_RAW
                Logger.Warning($"send data when state closed: {Utils.DataToDebug(buffer)}", TAG);
#endif
            }
        }

        /// <summary>
        /// Close this transport
        /// </summary>
        public void Close() {
            _state = TransportState.Closed;
            try {
                if (_connection != null) {
                    _connection.Cancel();
                    _connection.Close();
                }
            } catch (Exception e) {
#if SHARDY_DEBUG_RAW
                Logger.Error($"close failed: {e}", TAG);
#endif               
            }
        }

        /// <summary>
        /// Begin receive data from socket
        /// </summary>
        public async void Receive() {
            if (_state == TransportState.Closed) {
#if SHARDY_DEBUG_RAW
                Logger.Warning("received data on closed state", TAG);
#endif     
                return;
            }
            ReceivedData data = default;
            try {
                data = await _connection.Receive();
            } catch (OperationCanceledException) {
#if SHARDY_DEBUG_RAW
                Logger.Info("receive operation canceled", TAG);
#endif
                ReceiveEnd();
                return;
            } catch (WebSocketException e) {
                if (_state == TransportState.Closed || e.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
#if SHARDY_DEBUG_RAW
                    Logger.Info("receive websocket closed prematurely", TAG);
#endif
                } else {
#if SHARDY_DEBUG_RAW
                    Logger.Error($"receive websocket failed: {e}", TAG);
#endif           
                }
                ReceiveEnd();
                return;
            } catch (Exception e) {
                if (_state == TransportState.Closed) {
#if SHARDY_DEBUG_RAW
                    Logger.Info("receive operation ended while closing", TAG);
#endif
                } else if (e.GetBaseException() is WebSocketException exception) {
                    if (exception.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
#if SHARDY_DEBUG_RAW
                        Logger.Info("receive as websocket closed prematurely", TAG);
#endif
                    } else {
#if SHARDY_DEBUG_RAW
                        Logger.Error($"receive as websocket failed: {e}", TAG);
#endif                      
                    }
                } else {
#if SHARDY_DEBUG_RAW
                    Logger.Error($"receive failed: {e}", TAG);
#endif
                }
                ReceiveEnd();
                return;
            }
            if (_connection == null) {
                return;
            }
            if (_state == TransportState.Closed) {
                ReceiveEnd();
                return;
            }
            if (!_connection.IsConnected && data.Length > 0) {
#if SHARDY_DEBUG_RAW
                Logger.Warning("try receive data with disconnected", TAG);
#endif
                return;
            }
#if SHARDY_DEBUG_RAW
            Logger.Info($"received size: {data.Length}, data: {Utils.DataToDebug(data.Body)}", TAG);
#endif
            if (data.Length > 0) {
                ProcessData(data.Body, 0, data.Length);
                if (_state == TransportState.Closed) {
                    ReceiveEnd();
                } else {
                    Receive();
                }
            } else {
                ReceiveEnd();
            }
        }

        /// <summary>
        /// Actions when receive finished
        /// </summary>
        void ReceiveEnd() {
            _state = TransportState.Closed;
            Close();
            NotifyDisconnect();
        }

        /// <summary>
        /// Notify the disconnection event to the listener
        /// </summary>
        void NotifyDisconnect() {
            if (_isDisconnectNotified) {
                return;
            }
            _isDisconnectNotified = true;
            OnDisconnect();
        }

        /// <summary>
        /// Detect what to read and read it
        /// </summary>
        /// <param name="bytes">Data array</param>
        /// <param name="offset">Offet to read</param>
        /// <param name="limit">Length to read</param>
        internal void ProcessData(byte[] bytes, int offset, int limit) {
            switch (_state) {
                case TransportState.Head:
                    ReadHead(bytes, offset, limit);
                    break;
                case TransportState.Body:
                    ReadBody(bytes, offset, limit);
                    break;
                default:
#if SHARDY_DEBUG_RAW
                    Logger.Warning($"process bytes on incorrect state: {_state}", TAG);
#endif                       
                    break;
            }
        }

        /// <summary>
        /// Calculate frame body size from head
        /// </summary>
        /// <param name="buffer">Head buffer</param>
        int CalculateFrameBodySize(byte[] buffer) {
            var result = 0;
            for (var i = 1; i < Block.BLOCK_HEAD; i++) {
                if (i > 1) {
                    result <<= 8;
                }
                result += buffer[i];
            }
            return result;
        }

        /// <summary>
        /// Read header
        /// </summary>
        /// <param name="bytes">Data array</param>
        /// <param name="offset">Offet to read</param>
        /// <param name="limit">Length to read</param>
        /// <returns>Correct header or not</returns>
        bool ReadHead(byte[] bytes, int offset, int limit) {
            var length = limit - offset;
            var size = Block.BLOCK_HEAD - _offset;
            var bytesToCopy = Math.Min(length, size);
            WriteBytes(bytes, offset, bytesToCopy, _offset, _head);
            _offset += bytesToCopy;
            offset += bytesToCopy;
            if (_offset == Block.BLOCK_HEAD) {
                _package = CalculateFrameBodySize(_head);
                if (_package > _maxBlockBodySize) {
#if SHARDY_DEBUG_RAW
                    Logger.Warning($"frame body size exceeds limit: {_package}", TAG);
#endif
                    _state = TransportState.Closed;
                    _connection.Destroy();
                    NotifyDisconnect();
                    return false;
                }
                _buffer = new byte[Block.BLOCK_HEAD + _package];
                WriteBytes(_head, 0, Block.BLOCK_HEAD, _buffer);
                _offset = Block.BLOCK_HEAD;
                _state = TransportState.Body;
                if (_package == 0) {
                    OnData(_buffer);
                    Reset();
                }
                if (offset < limit) {
                    ProcessData(bytes, offset, limit);
                }
                return true;
            } else {
                return false;
            }
        }

        /// <summary>
        /// Read body
        /// </summary>
        /// <param name="bytes">Data array</param>
        /// <param name="offset">Offet to read</param>
        /// <param name="limit">Length to read</param>
        void ReadBody(byte[] bytes, int offset, int limit) {
            var length = _package + Block.BLOCK_HEAD - _offset;
            if ((offset + length) <= limit) {
                WriteBytes(bytes, offset, length, _offset, _buffer);
                offset += length;
                OnData(_buffer);
                Reset();
                if (offset < limit) {
                    ProcessData(bytes, offset, limit);
                }
            } else {
                WriteBytes(bytes, offset, limit - offset, _offset, _buffer);
                _offset += limit - offset;
                _state = TransportState.Body;
            }
        }

        /// <summary>
        /// Reset all data after receive full package
        /// </summary>
        void Reset() {
            _offset = 0;
            _package = 0;
            if (_state != TransportState.Closed) {
                _state = TransportState.Head;
            }
            Array.Clear(_head, 0, _head.Length);
        }

        /// <summary>
        /// Write to buffer
        /// </summary>
        /// <param name="source">Source array</param>
        /// <param name="start">Start offset</param>
        /// <param name="length">Length to write</param>
        /// <param name="target">Target to write</param>
        void WriteBytes(byte[] source, int start, int length, byte[] target) {
            WriteBytes(source, start, length, 0, target);
        }

        /// Write to buffer
        /// </summary>
        /// <param name="source">Source array</param>
        /// <param name="start">Start offset</param>
        /// <param name="length">Length to write</param>
        /// <param name="offset">Offset on target array</param> 
        /// <param name="target">Target to write</param> 
        void WriteBytes(byte[] source, int start, int length, int offset, byte[] target) {
            for (var i = 0; i < length; i++) {
                target[offset + i] = source[start + i];
            }
        }

        /// <summary>
        /// Destroy all
        /// </summary>
        public void Destroy() {
#if SHARDY_DEBUG_RAW
            Logger.Info("destroy", TAG);
#endif            
            _state = TransportState.Closed;
            try {
                _connection.Destroy();
                _connection = null;
            } catch (Exception e) {
#if SHARDY_DEBUG_RAW
                Logger.Error($"destroy failed: {e}", TAG);
#endif
            }
        }
    }
}
