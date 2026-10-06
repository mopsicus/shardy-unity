using System;
using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shardy.Tests {

    /// <summary>
    /// Request payloads are verified with the server "echo" request; commands have no reply,
    /// so they are checked by the fact that the server accepts them and the connection stays alive.
    /// </summary>
    public class PayloadTests : IntegrationTestBase {

        [Serializable]
        class Sample {

            public int number;

            public float fraction;

            public bool isFlag;

            public string text;

            public int[] items;
        }

        static System.Collections.Generic.IEnumerable<byte[]> PayloadCases() {
            yield return null;
            yield return new byte[0];
            yield return Encoding.UTF8.GetBytes("plain ascii");
            yield return Encoding.UTF8.GetBytes("Привет, мир! 你好 🎮");
            yield return Encoding.UTF8.GetBytes("42");
            yield return Encoding.UTF8.GetBytes("true");
            yield return Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Sample { number = 7, fraction = 1.5f, isFlag = true, text = "обj", items = new[] { 1, 2, 3 } }));
            yield return Encoding.UTF8.GetBytes("[1,2,3,null]");
            yield return new byte[] { 0, 1, 2, 254, 255 };
            yield return Encoding.UTF8.GetBytes(new string('x', 8192));
        }

        [UnityTest]
        public IEnumerator Command_WithPayload_IsAcceptedByServer([ValueSource(nameof(PayloadCases))] byte[] payload) {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                client.Client.Command("notify", payload);
                var response = await client.RequestAsync("status");
                Assert.IsTrue(string.IsNullOrEmpty(response.Error), response.Error);
                Assert.IsTrue(client.Client.IsConnected);
            });
        }

        [UnityTest]
        public IEnumerator Request_Echo_ReturnsSamePayload([ValueSource(nameof(PayloadCases))] byte[] payload) {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                var response = await client.RequestAsync("echo", payload);
                Assert.IsTrue(string.IsNullOrEmpty(response.Error), response.Error);
                Assert.AreEqual("echo", response.Name);
                CollectionAssert.AreEqual(payload ?? new byte[0], response.Data ?? new byte[0]);
            });
        }

        [UnityTest]
        public IEnumerator Request_Echo_StructuredPayloadRoundTrips() {
            return Run(async () => {
                var sample = new Sample { number = -5, fraction = 2.25f, isFlag = true, text = "Привет 🎮", items = new[] { 4, 5, 6 } };
                var client = await CreateReadyClientAsync();
                var response = await client.RequestAsync("echo", Encoding.UTF8.GetBytes(JsonUtility.ToJson(sample)));
                var result = JsonUtility.FromJson<Sample>(Utils.DataToString(response.Data));
                Assert.AreEqual(sample.number, result.number);
                Assert.AreEqual(sample.fraction, result.fraction);
                Assert.AreEqual(sample.isFlag, result.isFlag);
                Assert.AreEqual(sample.text, result.text);
                CollectionAssert.AreEqual(sample.items, result.items);
            });
        }

        [UnityTest]
        public IEnumerator Request_Echo_ConcurrentRequestsGetOwnPayloads() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                var requests = new System.Collections.Generic.List<(string text, System.Threading.Tasks.Task<PayloadData> response)>();
                for (var i = 0; i < 10; i++) {
                    var text = $"message-{i}";
                    requests.Add((text, client.BeginRequest("echo", Encoding.UTF8.GetBytes(text)).response));
                }
                foreach (var (text, task) in requests) {
                    var response = await TestClient.WithTimeout(task, $"echo of '{text}'");
                    Assert.AreEqual(text, Utils.DataToString(response.Data));
                }
            });
        }

        [UnityTest]
        public IEnumerator Request_Error_ResponseCarriesErrorStringWithPayload() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                var response = await client.RequestAsync("fail", Encoding.UTF8.GetBytes("data"));
                Assert.AreEqual("fail_error_code", response.Error);
            });
        }

        [UnityTest]
        public IEnumerator Request_Status_ResponseIsValidJson() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                var response = await client.RequestAsync("status");
                var json = Utils.DataToString(response.Data);
                StringAssert.StartsWith("{", json);
                StringAssert.Contains("\"heapUsed\"", json);
            });
        }
    }
}
