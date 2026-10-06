using System;
using System.Collections;
using System.Collections.Generic;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.Sockets;
#endif
using System.Threading.Tasks;
using NUnit.Framework;

namespace Shardy.Tests {

    /// <summary>
    /// Base class for tests: server availability check, client factory and cleanup
    /// </summary>
    public abstract class IntegrationTestBase {

        readonly List<TestClient> _clients = new List<TestClient>();

        [OneTimeSetUp]
        public void CheckServerAvailable() {
#if !UNITY_WEBGL || UNITY_EDITOR
            try {
                using (var tcp = new TcpClient()) {
                    var connect = tcp.ConnectAsync(IntegrationConfig.TestHost, IntegrationConfig.TestPort);
                    if (!connect.Wait(IntegrationConfig.OperationTimeout)) {
                        throw new TimeoutException("connection timed out");
                    }
                }
            } catch (Exception e) {
                Assert.Fail($"Shardy integration server is unavailable at {IntegrationConfig.Describe()}: {e.GetBaseException().Message}. Start the server and check IntegrationConfig.");
            }
#endif
        }

        [TearDown]
        public void DisposeClients() {
            foreach (var client in _clients) {
                client.Dispose();
            }
            _clients.Clear();
        }

        /// <summary>
        /// Create client, it will be disposed after the test
        /// </summary>
        protected TestClient CreateClient(ClientOptions options = null) {
            var client = new TestClient(options);
            _clients.Add(client);
            return client;
        }

        /// <summary>
        /// Create client, connect and wait for handshake
        /// </summary>
        protected async Task<TestClient> CreateReadyClientAsync(ClientOptions options = null) {
            var client = CreateClient(options);
            await client.ConnectAndWaitReadyAsync();
            return client;
        }

        /// <summary>
        /// Run async test body inside UnityTest, exceptions are rethrown
        /// </summary>
        protected static IEnumerator Run(Func<Task> body) {
            var task = body();
            while (!task.IsCompleted) {
                yield return null;
            }
            if (task.IsFaulted) {
                throw task.Exception.GetBaseException();
            }
        }
    }
}
