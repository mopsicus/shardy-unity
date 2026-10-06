#if UNITY_WEBGL && !UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Shardy.Tests.WebGLPlayer {

    /// <summary>
    /// Runs real Shardy client integration checks inside the WebGL player and reports results to the browser.
    /// </summary>
    public sealed class WebGLIntegrationRunner : MonoBehaviour {

        const string Host = "192.168.1.111";

        const int Port = 3000;

        const int OperationTimeout = 10000;

        [Serializable]
        sealed class Result {

            public string name;

            public string status;

            public string message;
        }

        [Serializable]
        sealed class Report {

            public string status;

            public string host;

            public int port;

            public string transport;

            public List<Result> tests = new List<Result>();

            public string summary;
        }

        [DllImport("__Internal")]
        static extern void ShardyWebGLTestSetResult(string json);

        readonly Report _report = new Report {
            status = "running",
            host = Host,
            port = Port,
            transport = "WebSocketManager (WebGL)"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartRunner() {
            if (FindObjectOfType<WebGLIntegrationRunner>() == null) {
                new GameObject("Shardy WebGL Integration Tests").AddComponent<WebGLIntegrationRunner>();
            }
        }

        async void Start() {
            Publish();
            await RunTest("WebSocketManagerConnectReadyDisconnect", ConnectionLifecycle);
            await RunTest("RequestsErrorsAndCorrelation", RequestRoundTrips);
            await RunTest("EchoPayloads", PayloadRoundTrips);
            await RunTest("Commands", Commands);
            await RunTest("ServerRequestAndResponse", ServerRequestRoundTrip);
            await RunTest("CancelAndReuseConnection", Cancellation);

            var passed = 0;
            foreach (var result in _report.tests) {
                if (result.status == "passed") {
                    passed++;
                }
            }
            _report.status = passed == _report.tests.Count ? "passed" : "failed";
            _report.summary = $"{passed}/{_report.tests.Count} tests passed";
            Publish();
        }

        async Task RunTest(string name, Func<Task> test) {
            var result = new Result { name = name };
            _report.tests.Add(result);
            Publish();
            try {
                await test();
                result.status = "passed";
            } catch (Exception e) {
                result.status = "failed";
                result.message = e.ToString();
                Debug.LogError($"[Shardy WebGL integration] {name} failed: {e}");
            }
            Publish();
        }

        async Task ConnectionLifecycle() {
            var session = await Connect();
            try {
                Check(session.Client.IsConnected, "Client must report connected after handshake");
                await session.Disconnect();
                Check(!session.Client.IsConnected, "Client must report disconnected after OnDisconnect");
            } finally {
                session.Destroy();
            }
        }

        async Task RequestRoundTrips() {
            await WithConnectedClient(async (session) => {
                var requests = new[] { session.BeginRequest("status"), session.BeginRequest("echo", Bytes("correlation")) };
                var status = await Wait(requests[0].response, "response to status request");
                var echo = await Wait(requests[1].response, "response to echo request");

                Check(status.Id == requests[0].id, "status response must match its request id");
                Check(status.Name == "status" && string.IsNullOrEmpty(status.Error), "status request must succeed");
                Check(JsonUtility.FromJson<MemoryStatus>(Text(status.Data)).heapUsed > 0, "status response must deserialize");
                Check(echo.Id == requests[1].id, "echo response must match its request id");
                Check(Text(echo.Data) == "correlation", "echo response must preserve request data");

                var failure = await session.Request("fail");
                Check(failure.Error == "fail_error_code", "fail request must expose the server error");
                var afterFailure = await session.Request("status");
                Check(string.IsNullOrEmpty(afterFailure.Error), "client must remain usable after an error response");
            });
        }

        async Task PayloadRoundTrips() {
            await WithConnectedClient(async (session) => {
                var cases = new[] {
                    new byte[0],
                    Bytes("Привет, WebGL 🎮"),
                    new byte[] { 0, 1, 127, 254, 255 },
                    Bytes("{\"number\":42,\"enabled\":true,\"items\":[1,2,3]}"),
                    Bytes(new string('x', 8192))
                };

                for (var i = 0; i < cases.Length; i++) {
                    var response = await session.Request("echo", cases[i]);
                    Check(string.IsNullOrEmpty(response.Error), $"echo payload #{i + 1} must succeed; server/client error was '{response.Error}'");
                    Check(EqualBytes(cases[i], response.Data), $"echo payload #{i + 1} must round-trip byte-for-byte");
                }
            });
        }

        async Task Commands() {
            await WithConnectedClient(async (session) => {
                session.Client.Command("notify", Bytes("command payload from WebGL"));
                var response = await session.Request("status");
                Check(string.IsNullOrEmpty(response.Error), "status after notify command must succeed");
                Check(session.Client.IsConnected, "client must remain connected after command");
            });
        }

        async Task ServerRequestRoundTrip() {
            await WithConnectedClient(async (session) => {
                var received = new TaskCompletionSource<PayloadData>();
                Action<PayloadData> handler = (payload) => {
                    session.Client.Response(payload, Bytes("response from WebGL client"));
                    received.TrySetResult(payload);
                };
                session.Client.OnRequest("request", handler);
                try {
                    session.Client.Command("request");
                    var request = await Wait(received.Task, "server-initiated request");
                    Check(request.Name == "request", "server request name must be preserved");
                    Check(request.Type == PayloadType.Request, "server message must deserialize as a request");
                    var marker = await session.Request("status");
                    Check(string.IsNullOrEmpty(marker.Error), "client must remain usable after responding to a server request");
                } finally {
                    session.Client.OffRequest("request");
                }
            });
        }

        async Task Cancellation() {
            await WithConnectedClient(async (session) => {
                var cancelled = session.BeginRequest("status");
                session.Client.Cancel(cancelled.id);

                var marker = await session.Request("status");
                Check(string.IsNullOrEmpty(marker.Error), "marker request after cancellation must succeed");
                Check(!cancelled.response.IsCompleted, "cancelled request callback must not complete");

                var next = await session.Request("echo", Bytes("still alive"));
                Check(Text(next.Data) == "still alive", "subsequent request must still round-trip");
            });
        }

        async Task WithConnectedClient(Func<ClientSession, Task> body) {
            var session = await Connect();
            try {
                await body(session);
            } finally {
                try {
                    await session.Close();
                } finally {
                    session.Destroy();
                }
            }
        }

        async Task<ClientSession> Connect() {
            var session = new ClientSession();
            try {
                await Wait(session.Client.Connect(Host, Port), $"connect to {Host}:{Port}");
                Check(await Wait(session.connected.Task, "OnConnect callback"), $"server {Host}:{Port} must accept WebSocket connection");
                await Wait(session.ready.Task, "OnReady handshake callback");
                return session;
            } catch {
                session.Destroy();
                throw;
            }
        }

        async Task<T> Wait<T>(Task<T> task, string operation) {
            var delay = Utils.SetDelay(OperationTimeout);
            if (await Task.WhenAny(task, delay) != task) {
                throw new TimeoutException($"Timed out after {OperationTimeout} ms waiting for '{operation}' on {Host}:{Port} (WebSocket). Verify that the Shardy integration server is running and accepts browser WebSocket connections.");
            }
            return await task;
        }

        async Task Wait(Task task, string operation) {
            var delay = Utils.SetDelay(OperationTimeout);
            if (await Task.WhenAny(task, delay) != task) {
                throw new TimeoutException($"Timed out after {OperationTimeout} ms waiting for '{operation}' on {Host}:{Port} (WebSocket). Verify that the Shardy integration server is running and accepts browser WebSocket connections.");
            }
            await task;
        }

        static void Check(bool isCondition, string message) {
            if (!isCondition) {
                throw new InvalidOperationException(message);
            }
        }

        static byte[] Bytes(string value) {
            return Encoding.UTF8.GetBytes(value);
        }

        static string Text(byte[] value) {
            return value == null ? string.Empty : Encoding.UTF8.GetString(value);
        }

        static bool EqualBytes(byte[] left, byte[] right) {
            if (left == null || right == null) {
                return left == right;
            }
            if (left.Length != right.Length) {
                return false;
            }
            for (var i = 0; i < left.Length; i++) {
                if (left[i] != right[i]) {
                    return false;
                }
            }
            return true;
        }

        void Publish() {
            ShardyWebGLTestSetResult(JsonUtility.ToJson(_report));
        }

        [Serializable]
        sealed class MemoryStatus {

            public long heapUsed;
        }

        sealed class ClientSession {

            // In a WebGL player, Client initializes WebSocketManager and Connection routes
            // socket operations through its .jslib callbacks. Editor PlayMode tests use
            // the managed ClientWebSocket implementation instead.
            public readonly Client Client = new Client(options: new ClientOptions(TransportType.WebSocket));

            public readonly TaskCompletionSource<bool> connected = new TaskCompletionSource<bool>();

            public readonly TaskCompletionSource<bool> ready = new TaskCompletionSource<bool>();

            public readonly TaskCompletionSource<DisconnectReason> disconnected = new TaskCompletionSource<DisconnectReason>();

            bool _isDestroyed;

            public ClientSession() {
                Client.OnConnect += OnConnect;
                Client.OnReady += OnReady;
                Client.OnDisconnect += OnDisconnect;
            }

            void OnConnect(bool isConnected) {
                connected.TrySetResult(isConnected);
                if (isConnected) {
                    Client.Handshake();
                }
            }

            void OnReady() {
                ready.TrySetResult(true);
            }

            void OnDisconnect(DisconnectReason reason) {
                disconnected.TrySetResult(reason);
            }

            public (long id, Task<PayloadData> response) BeginRequest(string name, byte[] data = null) {
                var response = new TaskCompletionSource<PayloadData>();
                var id = Client.Request(name, payload => response.TrySetResult(payload), data);
                return (id, response.Task);
            }

            public async Task<PayloadData> Request(string name, byte[] data = null) {
                var request = BeginRequest(name, data);
                return await WaitStatic(request.response, $"response to '{name}'");
            }

            public async Task Disconnect() {
                Client.Disconnect();
                await WaitStatic(disconnected.Task, "OnDisconnect callback");
            }

            static async Task<T> WaitStatic<T>(Task<T> task, string operation) {
                var delay = Utils.SetDelay(OperationTimeout);
                if (await Task.WhenAny(task, delay) != task) {
                    throw new TimeoutException($"Timed out after {OperationTimeout} ms waiting for '{operation}' on {Host}:{Port} (WebSocket).");
                }
                return await task;
            }

            public async Task Close() {
                if (Client.IsConnected) {
                    await Disconnect();
                }
            }

            public void Destroy() {
                if (_isDestroyed) {
                    return;
                }
                _isDestroyed = true;
                Client.OnConnect -= OnConnect;
                Client.OnReady -= OnReady;
                Client.OnDisconnect -= OnDisconnect;
                Client.Destroy();
            }
        }
    }
}

#endif
