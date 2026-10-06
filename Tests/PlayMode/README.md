# Shardy integration tests (PlayMode)

Real integration tests: the Unity client talks to a **running Shardy test server** (no mocks).

## Prerequisites
- Shardy test server is running and exposes the handlers `status`, `echo`, `notify`, `fail`, `timer`, `request`
  (`status` returns `process.memoryUsage()` JSON, `fail` responds with error `fail_error_code`).
- The server transport matches `IntegrationConfig.TestTransport`.
- If the server is unreachable the whole suite fails with "Shardy integration server is unavailable".

## Configuration
All settings are in [IntegrationConfig.cs](./IntegrationConfig.cs):

| Constant | Default |
|---|---|
| `TestHost` | `192.168.1.111` |
| `TestPort` | `3000` |
| `TestTransport` | `TransportType.Tcp` |

To run against WebSocket, start the server in WebSocket mode and set `TestTransport = TransportType.WebSocket`
(and `TestPort` if it differs). No other change is needed.

## Running
- Unity: Window > General > Test Runner > PlayMode > Run All.
- CLI (close the editor first):
  `Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -assemblyNames Shardy.Tests.PlayMode -testResults results.xml`
- With `TestTransport = TransportType.WebSocket`, Editor PlayMode exercises managed `ClientWebSocket`. To exercise WebGL `WebSocketManager`, build and run the browser player described in [WebGLPlayer README](../WebGLPlayer/README.md).
- Real WebGL player tests (runs the C# client and `websocket.jslib` in a browser): see [WebGLPlayer README](../WebGLPlayer/README.md).

## Notes
- `Client` has no `Fetch`/reconnect API: request/response is covered via `Request`, reconnect via a new `Client` after `Disconnect`.
- Payloads are verified via the server `echo` request; commands have no reply, so they are checked as "accepted by server, connection alive".
- Cancel is deterministic: `Cancel` removes the callback synchronously, and a following request acts as an ordering marker.
- Each test's clients are disconnected and destroyed in `TearDown`.
- The timeout test sends the `notify` command name as a request; the server never responds, so the client reports error `timeout`.
