namespace Shardy {

    /// <summary>
    /// Block type to encode
    /// </summary>
    public enum BlockType {

        /// <summary>
        /// Handshake process
        /// </summary>        
        Handshake,

        /// <summary>
        /// Acknowledgement for success verify
        /// </summary>
        HandshakeAcknowledgement,

        /// <summary>
        /// Ping
        /// </summary>        
        Heartbeat,

        /// <summary>
        /// Data for command, request, response
        /// </summary>        
        Data,

        /// <summary>
        /// Kick from server, disconnect
        /// </summary>        
        Kick
    }
}