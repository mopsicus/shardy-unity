namespace Shardy.Tests
{

    /// <summary>
    /// Central configuration of the integration server used by all tests.
    /// Change only these values to run the suite against another server or transport.
    /// </summary>
    public static class IntegrationConfig
    {

        /// <summary>
        /// Server host
        /// </summary>
        public const string TestHost = "192.168.1.111";

        /// <summary>
        /// Server port
        /// </summary>
        public const int TestPort = 3000;

        /// <summary>
        /// Server transport: TransportType.Tcp or TransportType.WebSocket
        /// </summary>
        public const TransportType TestTransport = TransportType.WebSocket;

        /// <summary>
        /// Timeout for a single network operation, ms
        /// </summary>
        public const int OperationTimeout = 5000;

        /// <summary>
        /// Time to make sure that something does NOT happen, ms
        /// </summary>
        public const int NegativeTimeout = 1000;

        /// <summary>
        /// Port where nobody listens, used for connection failure test
        /// </summary>
        public const int UnusedPort = 1;

        /// <summary>
        /// Host where nobody listens, used for connection failure test
        /// </summary>
        public const string UnusedHost = "127.0.0.1";

        /// <summary>
        /// Create client options for configured transport
        /// </summary>
        public static ClientOptions CreateOptions()
        {
            return new ClientOptions(TestTransport);
        }

        /// <summary>
        /// Human readable server description for diagnostics
        /// </summary>
        public static string Describe()
        {
            return $"{TestHost}:{TestPort} ({TestTransport})";
        }
    }
}
