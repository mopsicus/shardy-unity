namespace Shardy {

    /// <summary>
    /// Disconnect reasons
    /// </summary>
    public enum DisconnectReason {

        /// <summary>
        /// Normal disconnect
        /// </summary>
        Normal,

        /// <summary>
        /// Have no answer on ping command
        /// </summary>
        Timeout,

        /// <summary>
        /// Handshake validation failed
        /// </summary>
        Handshake,

        /// <summary>
        /// Server is closed`
        /// </summary>
        ServerDown,

        /// <summary>
        /// Some error occurs
        /// </summary>
        Unknown
    }
}