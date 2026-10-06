using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Shardy.Tests {

    public class ConnectionTests : IntegrationTestBase {

        [UnityTest]
        public IEnumerator Connect_EstablishesConnection() {
            return Run(async () => {
                var client = CreateClient();
                Assert.IsFalse(client.Client.IsConnected, "client must be disconnected before Connect");
                var connected = await client.ConnectAsync();
                Assert.IsTrue(connected, $"OnConnect reported failure for {IntegrationConfig.Describe()}");
                Assert.IsTrue(client.Client.IsConnected);
            });
        }

        [UnityTest]
        public IEnumerator Connect_CompletesHandshakeAndBecomesReady() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                Assert.IsTrue(client.Client.IsConnected);
            });
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        [UnityTest]
        public IEnumerator Connect_ToUnavailableServer_ReportsFailure() {
            return Run(async () => {
                var client = CreateClient();
                var connected = await client.ConnectAsync(IntegrationConfig.UnusedHost, IntegrationConfig.UnusedPort);
                Assert.IsFalse(connected, "OnConnect must report failure");
                Assert.IsFalse(client.Client.IsConnected);
            });
        }
#endif

        [UnityTest]
        public IEnumerator Disconnect_ClosesConnection() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                client.Client.Disconnect();
                var reason = await client.WaitDisconnectAsync();
                Assert.AreEqual(DisconnectReason.Normal, reason);
                Assert.IsFalse(client.Client.IsConnected);
            });
        }

        [UnityTest]
        public IEnumerator Connect_AfterDisconnect_WorksWithNewClient() {
            return Run(async () => {
                var first = await CreateReadyClientAsync();
                first.Client.Disconnect();
                await first.WaitDisconnectAsync();

                var second = await CreateReadyClientAsync();
                var response = await second.RequestAsync("status");
                Assert.IsTrue(string.IsNullOrEmpty(response.Error), response.Error);
            });
        }
    }
}
