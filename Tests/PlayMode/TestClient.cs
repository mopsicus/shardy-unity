using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shardy.Tests {

    /// <summary>
    /// Wrapper around the real Shardy Client: collects events and gives awaitable operations with timeouts
    /// </summary>
    public class TestClient : IDisposable {

        /// <summary>
        /// Real client
        /// </summary>
        public readonly Client Client;

        readonly TaskCompletionSource<bool> _connected = new TaskCompletionSource<bool>();
        readonly TaskCompletionSource<bool> _ready = new TaskCompletionSource<bool>();
        readonly TaskCompletionSource<DisconnectReason> _disconnected = new TaskCompletionSource<DisconnectReason>();
        readonly List<(string name, Action<PayloadData> handler)> _subscriptions = new List<(string, Action<PayloadData>)>();
        readonly List<string> _requestHandlers = new List<string>();
        bool _isDisposed;

        public TestClient(ClientOptions options = null) {
            Client = new Client(options: options ?? IntegrationConfig.CreateOptions());
            Client.OnConnect += OnConnect;
            Client.OnReady += OnReady;
            Client.OnDisconnect += OnDisconnect;
        }

        void OnConnect(bool isConnected) {
            _connected.TrySetResult(isConnected);
            if (isConnected) {
                Client.Handshake();
            }
        }

        void OnReady() {
            _ready.TrySetResult(true);
        }

        void OnDisconnect(DisconnectReason reason) {
            _disconnected.TrySetResult(reason);
        }

        /// <summary>
        /// Connect to the configured server and wait for OnConnect
        /// </summary>
        public async Task<bool> ConnectAsync(string host = IntegrationConfig.TestHost, int port = IntegrationConfig.TestPort) {
            await WithTimeout(Client.Connect(host, port), $"Connect to {host}:{port}");
            return await WithTimeout(_connected.Task, "OnConnect callback");
        }

        /// <summary>
        /// Connect and wait until handshake is completed
        /// </summary>
        public async Task ConnectAndWaitReadyAsync() {
            var isConnected = await ConnectAsync();
            if (!isConnected) {
                throw new Exception($"Integration server is unavailable: {IntegrationConfig.Describe()}");
            }
            await WaitReadyAsync();
        }

        public Task WaitReadyAsync() {
            return WithTimeout(_ready.Task, "OnReady callback (handshake)");
        }

        public Task<DisconnectReason> WaitDisconnectAsync() {
            return WithTimeout(_disconnected.Task, "OnDisconnect callback");
        }

        public Task<DisconnectReason> DisconnectedTask => _disconnected.Task;

        /// <summary>
        /// Send request and wait for the response
        /// </summary>
        public async Task<PayloadData> RequestAsync(string name, byte[] payload = null) {
            var (_, task) = BeginRequest(name, payload);
            return await WithTimeout(task, $"response to request '{name}'");
        }

        /// <summary>
        /// Send request, return id and task completed by the callback
        /// </summary>
        public (long id, Task<PayloadData> response) BeginRequest(string name, byte[] payload = null) {
            var source = new TaskCompletionSource<PayloadData>();
            var id = Client.Request(name, (data) => source.TrySetResult(data), payload);
            return (id, source.Task);
        }

        /// <summary>
        /// Subscribe on server command, handler is removed on dispose
        /// </summary>
        public void Subscribe(string name, Action<PayloadData> handler) {
            _subscriptions.Add((name, handler));
            Client.On(name, handler);
        }

        /// <summary>
        /// Subscribe on server request, handler is removed on dispose
        /// </summary>
        public void SubscribeRequest(string name, Action<PayloadData> handler) {
            _requestHandlers.Add(name);
            Client.OnRequest(name, handler);
        }

        /// <summary>
        /// Wait for task or fail with a message which operation was awaited
        /// </summary>
        public static async Task WithTimeout(Task task, string operation, int timeout = IntegrationConfig.OperationTimeout) {
            var completed = await Task.WhenAny(task, Delay(timeout));
            if (completed != task) {
                throw new TimeoutException($"Timed out after {timeout} ms waiting for: {operation}. Server: {IntegrationConfig.Describe()}. Is the Shardy server running?");
            }
            await task;
        }

        public static async Task<T> WithTimeout<T>(Task<T> task, string operation, int timeout = IntegrationConfig.OperationTimeout) {
            await WithTimeout((Task)task, operation, timeout);
            return task.Result;
        }

        /// <summary>
        /// Returns true if task is NOT completed during the timeout
        /// </summary>
        public static async Task<bool> StaysPending(Task task, int timeout = IntegrationConfig.NegativeTimeout) {
            var completed = await Task.WhenAny(task, Delay(timeout));
            return completed != task;
        }

        public static Task Delay(int milliseconds) {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Utils.SetDelay(milliseconds);
#else
            return Task.Delay(milliseconds);
#endif
        }

        public void Dispose() {
            if (_isDisposed) {
                return;
            }
            _isDisposed = true;
            try {
                foreach (var (name, handler) in _subscriptions) {
                    Client.Off(name, handler);
                }
                foreach (var name in _requestHandlers) {
                    Client.OffRequest(name);
                }
                Client.OnConnect -= OnConnect;
                Client.OnReady -= OnReady;
                Client.OnDisconnect -= OnDisconnect;
                if (Client.IsConnected) {
                    Client.Disconnect();
                }
            } finally {
                Client.Destroy();
            }
        }
    }
}
