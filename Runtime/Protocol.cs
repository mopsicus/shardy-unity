using System;

namespace Shardy {

    /// <summary>
    /// Protocol state
    /// </summary>
    enum ProtocolState {

        /// <summary>
        /// Init state, wait for handshake
        /// </summary>        
        Start,

        /// <summary>
        /// Handshake is in progress
        /// </summary>        
        Handshake,

        /// <summary>
        /// Work state after success handshake
        /// </summary>        
        Work,

        /// <summary>
        /// Protocol closed, any actions ignored
        /// </summary>        
        Closed
    }


    /// <summary>
    /// Protocol to manage all data from transport, and send to connection handlers
    /// </summary>
    class Protocol {

        /// <summary>
        /// Log tag
        /// </summary>
        const string TAG = "PROTOCOL";

        /// <summary>
        /// Data callback
        /// </summary>
        public Action<BlockData> OnBlock = delegate { };

        /// <summary>
        /// Disconnect callback
        /// </summary>
        public Action OnDisconnect = delegate { };

        /// <summary>
        /// Current protocol state
        /// </summary>
        ProtocolState _state = ProtocolState.Closed;

        /// <summary>
        /// Current transporter
        /// </summary>
        readonly Transport _transport = null;

        /// <summary>
        /// Protocol constructor
        /// </summary>
        /// <param name="connection">Current connection</param>
        public Protocol(Connection connection) {
            _transport = new Transport(connection);
            _transport.OnData = (data) => OnData(data);
            _transport.OnDisconnect = () => OnClose();
        }

        /// <summary>
        /// Start protocol and transport
        /// </summary>
        public void Start() {
            _state = ProtocolState.Start;
            _transport.Start();
        }

        /// <summary>
        /// Send data to transport
        /// </summary>
        /// <param name="type">Type of block data</param>
        /// <param name="body">Data for transfer</param>
        internal void Dispatch(BlockType type, byte[] body = null) {
            if (_state == ProtocolState.Closed) {
#if SHARDY_DEBUG_RAW
                Logger.Warning("send data to closed protocol", TAG);
#endif
                return;
            }
            body ??= (new byte[0]);
#if SHARDY_DEBUG_RAW
            Logger.Info($"dispatch type: {type}, body: {Utils.DataToDebug(body)}", TAG);
#endif
            var data = Block.Encode(type, body);
            _transport.Dispatch(data);
        }

        /// <summary>
        /// Send data to connection
        /// </summary>
        /// <param name="body">Serialized command data</param>
        public void Send(byte[] body) {
#if SHARDY_DEBUG_RAW
            Logger.Info($"send data: {Utils.DataToDebug(body)}", TAG);
#endif
            Dispatch(BlockType.Data, body);
        }

        /// <summary>
        /// Send heartbeat to connection
        /// </summary>
        public void Heartbeat() {
#if SHARDY_DEBUG_RAW
            Logger.Info("send heartbeat", TAG);
#endif
            Dispatch(BlockType.Heartbeat);
        }

        /// <summary>
        /// Send handshake to connection
        /// </summary>
        /// <param name="body">Handshake data</param>
        public void Handshake(byte[] body) {
#if SHARDY_DEBUG_RAW
            Logger.Info("send handshake", TAG);
#endif
            _state = ProtocolState.Handshake;
            Dispatch(BlockType.Handshake, body);
        }

        /// <summary>
        /// Send acknowledgement
        /// </summary>
        /// <param name="body">Acknowledge data</param>
        public void Acknowledge(byte[] body) {
#if SHARDY_DEBUG_RAW
            Logger.Info("send acknowledge", TAG);
#endif
            _state = ProtocolState.Work;
            Dispatch(BlockType.HandshakeAcknowledgement, body);
        }

        /// <summary>
        /// Disconnect from server
        /// </summary>
        public void Disconnect() {
            _state = ProtocolState.Closed;
#if SHARDY_DEBUG_RAW
            Logger.Info("disconnect", TAG);
#endif
            _transport.Close();
        }

        /// <summary>
        /// Callback from transport when disconnected
        /// </summary>
        internal void OnClose() {
            _state = ProtocolState.Closed;
            OnDisconnect();
        }

        /// <summary>
        /// Decode data to package
        /// Select behaviour by package type
        /// </summary>
        /// <param name="data">Buffer bytes array</param>
        internal void OnData(byte[] data) {
            if (_state == ProtocolState.Closed) {
#if SHARDY_DEBUG_RAW
                Logger.Warning("received data to closed protocol", TAG);
#endif
                return;
            }
            var block = Block.Decode(data);
            if (!Block.Check(block.Type)) {
#if SHARDY_DEBUG_RAW
                Logger.Warning($"invalid block type: {block.Type}, state: {_state}", TAG);
#endif
                return;
            }
#if SHARDY_DEBUG_RAW
            Logger.Info($"received block: {block.Type}, data: {Utils.DataToDebug(block.Body)}, state: {_state}", TAG);
#endif
            switch (_state) {
                case ProtocolState.Start:
                    if (block.Type == BlockType.Heartbeat) {
                        OnBlock(block);
                    } else {
                        CatchBlockForState(block.Type);
                    }
                    break;
                case ProtocolState.Work:
                    switch (block.Type) {
                        case BlockType.Heartbeat:
                        case BlockType.Kick:
                        case BlockType.Data:
                            OnBlock(block);
                            break;
                        default:
                            CatchBlockForState(block.Type);
                            break;
                    }
                    break;
                case ProtocolState.Handshake:
                    switch (block.Type) {
                        case BlockType.HandshakeAcknowledgement:
                        case BlockType.Kick:
                            OnBlock(block);
                            break;
                        default:
                            CatchBlockForState(block.Type);
                            break;
                    }
                    break;
                default:
                    CatchBlockForState(block.Type);
                    break;
            }
        }

        /// <summary>
        /// Catch and log incorrect situation
        /// </summary>
        /// <param name="type">Block type</param>
        void CatchBlockForState(BlockType type) {
#if SHARDY_DEBUG_RAW
            Logger.Warning($"invalid block type: {type}, state: {_state}", TAG);
#endif
        }

        /// <summary>
        /// Destroy all
        /// </summary>
        public void Destroy() {
#if SHARDY_DEBUG_RAW
            Logger.Info("destroy", TAG);
#endif            
            _state = ProtocolState.Closed;
            _transport.Destroy();
        }
    }
}