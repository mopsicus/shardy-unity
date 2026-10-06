# Real WebGL client integration tests

This runner builds a minimal Unity WebGL player and executes the real Shardy C# `Client` through `WebSocketManager` and `websocket.jslib` in a browser. With the WebGL player defines enabled, `Client` initializes `WebSocketManager`, `Connection` registers each socket through it, and the manager forwards the plugin's open/data/error/close callbacks into the C# client. The tests connect to the real demo server; they do not use mocks. The browser reports a JSON result as `window.__shardyWebGLTestResult` and sets the page title to `Shardy WebGL Tests: passed` or `Shardy WebGL Tests: failed`.

The WebGL runner is the Player-side half of the PlayMode integration coverage. Both suites exercise the same public `Client` API with WebSocket transport:

- PlayMode tests run in the Editor and use the managed `System.Net.WebSockets.ClientWebSocket` implementation.
- The WebGL player runner is compiled with WebGL defines, so `Client` uses `WebSocketManager` and `websocket.jslib`.

The current WebGL test server configuration is in `WebGLIntegrationRunner.cs`:

- Host: `192.168.1.111`
- Port: `3000`
- Transport: WebSocket

The server must be running with the `status`, `echo`, `fail`, `notify`, and `request` handlers and allow WebSocket connections from the browser origin.

## Build and run

Prerequisites: Unity WebGL Build Support for the project's editor version, a browser with WebGL support, and a reachable Shardy WebSocket server.

From the Unity project root, build the player (use an empty output folder):

```sh
SHARDY_WEBGL_BUILD_PATH=/tmp/ShardyWebGLIntegrationBuild \
  /Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath . -buildTarget WebGL \
  -executeMethod Shardy.Tests.WebGLPlayer.Editor.WebGLIntegrationBuild.Build -quit
```

Serve the generated player and open it in a browser:

```sh
python3 -m http.server 8765 --directory /tmp/ShardyWebGLIntegrationBuild
```

Open `http://127.0.0.1:8765/`. Wait for the page title `Shardy WebGL Tests: passed`, or inspect `window.__shardyWebGLTestResult` in the browser console for per-test results and diagnostics. A server connection or operation timeout is reported as a failed test; tests are not skipped.

## Coverage

- WebSocket connection, Shardy handshake/ready event, and disconnect callback/state.
- `status` request/response and response/request ID correlation.
- `echo` payload round trips: UTF-8, binary, structured JSON, and 8 KB payloads.
- `fail` server error response and `notify` command with payload.
- Server-originated `request`, client `Response`, cancellation, and continued use of the connection.

The 8 KB round-trip also verifies that the WebGL receive implementation correctly consumes a WebSocket message larger than its 1024-byte internal read buffer.
