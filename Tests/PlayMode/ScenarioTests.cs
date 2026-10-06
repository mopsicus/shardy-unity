using System.Collections;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Shardy.Tests {

    public class ScenarioTests : IntegrationTestBase {

        [UnityTest]
        public IEnumerator FullSession_ConnectCommandRequestCancelRequestDisconnect() {
            return Run(async () => {
                var client = await CreateReadyClientAsync();

                client.Client.Command("notify", Encoding.UTF8.GetBytes("scenario"));

                var status = await client.RequestAsync("status");
                Assert.IsTrue(string.IsNullOrEmpty(status.Error), status.Error);

                var failed = await client.RequestAsync("fail");
                Assert.AreEqual("fail_error_code", failed.Error);

                var cancelled = client.BeginRequest("status");
                client.Client.Cancel(cancelled.id);

                var again = await client.RequestAsync("status");
                Assert.IsTrue(string.IsNullOrEmpty(again.Error), again.Error);
                Assert.IsTrue(await TestClient.StaysPending(cancelled.response, 200), "cancelled request must not complete");

                client.Client.Disconnect();
                Assert.AreEqual(DisconnectReason.Normal, await client.WaitDisconnectAsync());
                Assert.IsFalse(client.Client.IsConnected);
            });
        }

        [UnityTest]
        public IEnumerator TwoClients_WorkIndependently() {
            return Run(async () => {
                var a = await CreateReadyClientAsync();
                var b = await CreateReadyClientAsync();

                var timerOnA = new TaskCompletionSource<bool>();
                var timerOnB = new TaskCompletionSource<bool>();
                a.Subscribe("timer", (_) => timerOnA.TrySetResult(true));
                b.Subscribe("timer", (_) => timerOnB.TrySetResult(true));

                // Only A subscribes on the server side
                a.Client.Command("timer", Encoding.UTF8.GetBytes("yes"));
                await TestClient.WithTimeout(timerOnA.Task, "'timer' command on client A");

                var fromA = a.BeginRequest("status");
                var fromB = b.BeginRequest("fail");
                var responseA = await TestClient.WithTimeout(fromA.response, "client A response");
                var responseB = await TestClient.WithTimeout(fromB.response, "client B response");
                Assert.IsTrue(string.IsNullOrEmpty(responseA.Error), responseA.Error);
                Assert.AreEqual("status", responseA.Name);
                Assert.AreEqual("fail_error_code", responseB.Error);
                Assert.AreEqual("fail", responseB.Name);

                Assert.IsFalse(timerOnB.Task.IsCompleted, "client B did not subscribe on the server and must not receive 'timer'");

                // Disconnect of A must not affect B
                a.Client.Command("timer", Encoding.UTF8.GetBytes("no"));
                await a.RequestAsync("status");
                a.Client.Disconnect();
                // ConnectionTests.Disconnect_ClosesConnection verifies A's disconnect callback.
                // Here the assertion is that closing A does not interrupt B.
                Assert.IsTrue(b.Client.IsConnected);
                var after = await b.RequestAsync("status");
                Assert.IsTrue(string.IsNullOrEmpty(after.Error), after.Error);
            });
        }
    }
}
