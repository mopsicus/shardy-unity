namespace Shardy {

    /// <summary>
    /// Data received from socket
    /// </summary>
    public struct ReceivedData {

        /// <summary>
        /// Data length
        /// </summary>
        public int Length;

        /// <summary>
        /// Data bytes array
        /// </summary>
        public byte[] Body;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="body">Data for block</param>
        /// <param name="length">Data length</param>
        public ReceivedData(byte[] body, int length) {
            Body = body;
            Length = length;
        }
    }
}