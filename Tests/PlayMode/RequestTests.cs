using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shardy.Tests {

    public class RequestTests : IntegrationTestBase {

        /// <summary>
        /// Shape of process.memoryUsage() returned by the "status" request
        /// </summary>
        [Serializable]
        class MemoryStatus {

            public long rss;

            public long heapTotal;

            public long heapUsed;

            public long external;

            public long arrayBuffers;
        }

        [UnityTest]
        public IEnumerator Request_Status_ReturnsDeserializableMemoryStatus() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                var response = await client.RequestAsync("status");
                Assert.IsTrue(string.IsNullOrEmpty(response.Error), response.Error);
                Assert.AreEqual(PayloadType.Response, response.Type);
                Assert.AreEqual("status", response.Name);
                var status = JsonUtility.FromJson<MemoryStatus>(Utils.DataToString(response.Data));
                Assert.Greater(status.rss, 0, "rss");
                Assert.Greater(status.heapTotal, 0, "heapTotal");
                Assert.Greater(status.heapUsed, 0, "heapUsed");
            });
        }

        [UnityTest]
        public IEnumerator Request_ReturnsUniqueIdsAndMatchesResponseToRequest() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                var requests = Enumerable.Range(0, 5).Select(_ => client.BeginRequest("status")).ToArray();
                Assert.AreEqual(requests.Length, requests.Select(r => r.id).Distinct().Count(), "request ids must be unique");
                for (var i = 0; i < requests.Length; i++) {
                    var response = await TestClient.WithTimeout(requests[i].response, $"response to request #{i}");
                    Assert.AreEqual(requests[i].id, response.Id, $"response id for request #{i}");
                    Assert.AreEqual("status", response.Name);
                    Assert.IsTrue(string.IsNullOrEmpty(response.Error), response.Error);
                }
            });
        }

        [UnityTest]
        public IEnumerator Request_Fail_ReturnsServerError() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                var response = await client.RequestAsync("fail");
                Assert.AreEqual("fail_error_code", response.Error);
                Assert.AreEqual("fail", response.Name);
            });
        }

        [UnityTest]
        public IEnumerator Request_Fail_ClientRemainsUsable() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                await client.RequestAsync("fail");
                var response = await client.RequestAsync("status");
                Assert.IsTrue(string.IsNullOrEmpty(response.Error), response.Error);
                Assert.IsTrue(client.Client.IsConnected);
            });
        }

        [UnityTest]
        public IEnumerator Request_WithPayload_ServerRespondsNormally() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                var response = await client.RequestAsync("status", System.Text.Encoding.UTF8.GetBytes("{\"a\":1}"));
                Assert.IsTrue(string.IsNullOrEmpty(response.Error), response.Error);
                Assert.IsNotEmpty(response.Data);
            });
        }

        [UnityTest]
        public IEnumerator Request_UnknownName_ClientRemainsConnectedAndUsable() {
            return Run(async () => {
                // Short timeout: server may answer with an error or not answer at all
                var options = IntegrationConfig.CreateOptions();
                options.RequestTimeout = 1500f;
                var client = await CreateReadyClientAsync(options);
                var response = await client.RequestAsync("no_such_request_name");
                Assert.IsFalse(string.IsNullOrEmpty(response.Error), "unknown request must end with an error (server error or client timeout)");
                var status = await client.RequestAsync("status");
                Assert.IsTrue(string.IsNullOrEmpty(status.Error), status.Error);
            });
        }

        [UnityTest]
        public IEnumerator Request_WithoutServerResponse_FailsWithTimeoutError() {
            return Run(async () => {
                // "notify" is a command handler on the server and never responds to a request
                var options = IntegrationConfig.CreateOptions();
                options.RequestTimeout = 1000f;
                var client = await CreateReadyClientAsync(options);
                var started = DateTime.UtcNow;
                var response = await client.RequestAsync("notify");
                Assert.IsFalse(string.IsNullOrEmpty(response.Error), "timed out request must carry an error");
                Assert.AreEqual("notify", response.Name);
                Assert.GreaterOrEqual((DateTime.UtcNow - started).TotalMilliseconds, 900, "timeout must not fire before RequestTimeout");
                var status = await client.RequestAsync("status");
                Assert.IsTrue(string.IsNullOrEmpty(status.Error), status.Error);
            });
        }

        [UnityTest]
        public IEnumerator Cancel_PreventsPendingRequestFromCompleting() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                var cancelled = client.BeginRequest("status");
                client.Client.Cancel(cancelled.id);
                // Responses arrive in order, so when the marker is answered the cancelled one would have been delivered
                var marker = await client.RequestAsync("status");
                Assert.IsTrue(string.IsNullOrEmpty(marker.Error), marker.Error);
                Assert.IsTrue(await TestClient.StaysPending(cancelled.response, 200), "cancelled request callback must not be invoked");
            });
        }

        [UnityTest]
        public IEnumerator Cancel_OnlyCancelsTargetRequest() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                var cancelled = client.BeginRequest("status");
                var kept = client.BeginRequest("status");
                client.Client.Cancel(cancelled.id);
                var response = await TestClient.WithTimeout(kept.response, "response to non-cancelled request");
                Assert.AreEqual(kept.id, response.Id);
                Assert.IsTrue(await TestClient.StaysPending(cancelled.response, 200));
            });
        }

        [UnityTest]
        public IEnumerator Cancel_ClientRemainsUsable() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                client.Client.Cancel(client.BeginRequest("status").id);
                for (var i = 0; i < 3; i++) {
                    var response = await client.RequestAsync("status");
                    Assert.IsTrue(string.IsNullOrEmpty(response.Error), response.Error);
                }
                Assert.IsTrue(client.Client.IsConnected);
            });
        }

        [UnityTest]
        public IEnumerator Cancel_UnknownId_DoesNothing() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                client.Client.Cancel(long.MaxValue);
                var response = await client.RequestAsync("status");
                Assert.IsTrue(string.IsNullOrEmpty(response.Error), response.Error);
            });
        }

        [UnityTest]
        public IEnumerator ServerRequest_ClientRespondsViaResponse() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                var received = new TaskCompletionSource<PayloadData>();
                client.SubscribeRequest("request", (payload) => {
                    received.TrySetResult(payload);
                    client.Client.Response(payload, System.Text.Encoding.UTF8.GetBytes("some_data_from_client"));
                });
                client.Client.Command("request");
                var request = await TestClient.WithTimeout(received.Task, "server request 'request' triggered by command");
                Assert.AreEqual(PayloadType.Request, request.Type);
                Assert.AreEqual("request", request.Name);
                // Connection must stay alive after responding
                var status = await client.RequestAsync("status");
                Assert.IsTrue(string.IsNullOrEmpty(status.Error), status.Error);
            });
        }

        [UnityTest]
        public IEnumerator OffRequest_StopsHandlerFromBeingInvoked() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                var invoked = new TaskCompletionSource<bool>();
                client.Client.OnRequest("request", (payload) => {
                    invoked.TrySetResult(true);
                    client.Client.Response(payload);
                });
                client.Client.OffRequest("request");
                client.Client.Command("request");
                Assert.IsTrue(await TestClient.StaysPending(invoked.Task, 2000), "handler removed by OffRequest must not be invoked");
            });
        }

        [UnityTest]
        public IEnumerator Request_WhileDisconnected_DoesNotCompleteAndDoesNotThrow() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                client.Client.Disconnect();
                await client.WaitDisconnectAsync();
                Assert.IsFalse(client.Client.IsConnected);
                Assert.DoesNotThrow(() => client.BeginRequest("status"));
                await Task.CompletedTask;
            });
        }
    }
}
