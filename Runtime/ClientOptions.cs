namespace Shardy {

    /// <summary>
    /// Client and connection options
    /// </summary>
    public class ClientOptions {

        /// <summary>
        /// Transport type: TCP or WebSocket
        /// </summary>
        public TransportType Type = TransportType.Tcp;

        /// <summary>
        /// Transport buffer size
        /// </summary>
        public int BufferSize = 1024;

        /// <summary>
        /// Timeout for RPC request (ms)
        /// </summary>
        public float RequestTimeout = 10000f;

        /// <summary>
        /// Interval for checking server (ms)
        /// </summary>
        public float PulseInterval = 1000f;

        /// <summary>
        /// Options constructor
        /// </summary>
        /// <param name="type">Transport type</param>
        public ClientOptions(TransportType type = TransportType.Tcp) {
            Type = type;
        }

        /// <summary>
        /// Options constructor
        /// </summary>
        /// <param name="type">Transport type</param>
        /// <param name="buffer">Transport buffer size, Kb</param>
        /// <param name="timeout">Timeout for RPC request, ms</param>
        /// <param name="pulse">Interval for checking server, ms</param>
        public ClientOptions(TransportType type, int buffer, float timeout, float pulse) {
            Type = type;
            BufferSize = buffer;
            RequestTimeout = timeout;
            PulseInterval = pulse;
        }
    }
}