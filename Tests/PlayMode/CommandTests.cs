using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Shardy.Tests {

    public class CommandTests : IntegrationTestBase {

        [UnityTest]
        public IEnumerator Command_WithoutPayload_KeepsConnectionAlive() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                client.Client.Command("notify");
                var response = await client.RequestAsync("status");
                Assert.IsTrue(string.IsNullOrEmpty(response.Error), response.Error);
                Assert.IsTrue(client.Client.IsConnected);
            });
        }

        [UnityTest]
        public IEnumerator Command_WithPayload_KeepsConnectionAlive() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                client.Client.Command("notify", Encoding.UTF8.GetBytes("hello from unity"));
                var response = await client.RequestAsync("status");
                Assert.IsTrue(string.IsNullOrEmpty(response.Error), response.Error);
            });
        }

        [UnityTest]
        public IEnumerator Command_Unknown_KeepsConnectionAlive() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                client.Client.Command("no_such_command", Encoding.UTF8.GetBytes("x"));
                var response = await client.RequestAsync("status");
                Assert.IsTrue(string.IsNullOrEmpty(response.Error), response.Error);
                Assert.IsTrue(client.Client.IsConnected);
            });
        }

        [UnityTest]
        public IEnumerator On_Timer_ReceivesServerCommand() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                var received = new ConcurrentQueue<PayloadData>();
                var first = new TaskCompletionSource<bool>();
                client.Subscribe("timer", (payload) => {
                    received.Enqueue(payload);
                    first.TrySetResult(true);
                });
                client.Client.Command("timer", Encoding.UTF8.GetBytes("yes"));
                await TestClient.WithTimeout(first.Task, "'timer' command from server");
                Assert.IsTrue(received.TryPeek(out var payload));
                Assert.AreEqual(PayloadType.Command, payload.Type);
                Assert.AreEqual("timer", payload.Name);
            });
        }

        [UnityTest]
        public IEnumerator Off_Timer_StopsReceivingCommands() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                var count = 0;
                var first = new TaskCompletionSource<bool>();
                void Handler(PayloadData payload) {
                    System.Threading.Interlocked.Increment(ref count);
                    first.TrySetResult(true);
                }
                client.Subscribe("timer", Handler);
                client.Client.Command("timer", Encoding.UTF8.GetBytes("yes"));
                await TestClient.WithTimeout(first.Task, "first 'timer' command from server");

                client.Client.Off("timer", Handler);
                client.Client.Command("timer", Encoding.UTF8.GetBytes("no"));
                // Round trip guarantees that the server processed "no" and in-flight commands are delivered
                await client.RequestAsync("status");
                var after = System.Threading.Volatile.Read(ref count);
                await TestClient.Delay(IntegrationConfig.NegativeTimeout);
                Assert.AreEqual(after, System.Threading.Volatile.Read(ref count), "no commands expected after Off");
            });
        }

        [UnityTest]
        public IEnumerator Off_Command_FromHandler_DoesNotInterruptDispatch() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                var removedHandlerCalled = 0;
                var remainingHandler = new TaskCompletionSource<bool>();
                Action<PayloadData> removeSelf = null;
                removeSelf = (_) => {
                    client.Client.Off("timer", removeSelf);
                    System.Threading.Interlocked.Increment(ref removedHandlerCalled);
                };
                client.Subscribe("timer", removeSelf);
                client.Subscribe("timer", (_) => remainingHandler.TrySetResult(true));

                client.Client.Command("timer", Encoding.UTF8.GetBytes("yes"));
                await TestClient.WithTimeout(remainingHandler.Task, "remaining 'timer' handler after unsubscribe");
                Assert.AreEqual(1, System.Threading.Volatile.Read(ref removedHandlerCalled));

                client.Client.Command("timer", Encoding.UTF8.GetBytes("no"));
                await client.RequestAsync("status");
                Assert.AreEqual(1, System.Threading.Volatile.Read(ref removedHandlerCalled));
            });
        }

        [UnityTest]
        public IEnumerator On_MultipleHandlers_AllReceiveCommand() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();
                var a = new TaskCompletionSource<bool>();
                var b = new TaskCompletionSource<bool>();
                client.Subscribe("timer", (_) => a.TrySetResult(true));
                client.Subscribe("timer", (_) => b.TrySetResult(true));
                client.Client.Command("timer", Encoding.UTF8.GetBytes("yes"));
                await TestClient.WithTimeout(a.Task, "first handler of 'timer'");
                await TestClient.WithTimeout(b.Task, "second handler of 'timer'");
            });
        }
    }
}
