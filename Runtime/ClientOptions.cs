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
        /// Maximum block body size in bytes
        /// Configure to match the server
        /// </summary>
        public int Block = 1024 * 1024;

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
        /// <param name="bufferSize">Transport buffer size, bytes</param>
        /// <param name="requestTimeout">Timeout for RPC request, ms</param>
        /// <param name="pulseInterval">Interval for checking server, ms</param>
        public ClientOptions(TransportType type, int bufferSize, float requestTimeout, float pulseInterval) {
            Type = type;
            BufferSize = bufferSize;
            RequestTimeout = requestTimeout;
            PulseInterval = pulseInterval;
        }

        /// <summary>
        /// Options constructor
        /// </summary>
        /// <param name="bufferSize">Transport buffer size, bytes</param>
        /// <param name="requestTimeout">Timeout for RPC request, ms</param>
        /// <param name="pulseInterval">Interval for checking server, ms</param>
        /// <param name="block">Maximum block body size in bytes</param>
        public ClientOptions(TransportType type, int bufferSize, float requestTimeout, float pulseInterval, int block) {
            Type = type;
            BufferSize = bufferSize;
            RequestTimeout = requestTimeout;
            PulseInterval = pulseInterval;
            Block = block;
        }
    }
}
