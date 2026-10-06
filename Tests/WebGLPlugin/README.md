# WebGL WebSocket plugin tests

These Node.js tests execute the actual `Runtime/Plugins/WebGL/websocket.jslib` file with an Emscripten API harness. The unit tests use a controlled WebSocket to verify callback wiring, binary memory copying/freeing, state/error codes, sending, closing, and cleanup.

The server integration tests use Node's real WebSocket implementation and connect to the Shardy demo server. They perform the Shardy handshake and exercise the plugin's WebSocket exports against the real server for connect/disconnect, requests, commands, payload round trips, errors, and server-originated requests. The Shardy wire framing and payload envelope are encoded by the test harness; this verifies the plugin/network boundary, not the Unity C# `Client` or a compiled WebGL player.

## Prerequisite and command

The unit tests require Node.js 18 or newer. Run them from the Unity project root:

```sh
node --test Assets/Shardy/Tests/WebGLPlugin/websocket.test.cjs
```

The server integration tests require Node.js 22.4 or newer (for its built-in WebSocket API) and a running Shardy server configured for WebSocket transport with `status`, `echo`, `fail`, `notify`, and `request` handlers:

```sh
node --test Assets/Shardy/Tests/WebGLPlugin/websocket.server.integration.test.cjs
```

The defaults are `192.168.1.111:3000`. Override them with `SHARDY_TEST_HOST` and `SHARDY_TEST_PORT`. Tests fail with an operation-specific timeout if the server is unavailable; they are not skipped.

To test the compiled Unity WebGL player and C# client path, install WebGL Build Support for the project's Unity Editor version and run a WebGL player build in a browser. That is a separate validation layer from these Node tests.

The actual Unity WebGL player integration suite, which runs the C# `Client` through the plugin against the demo server, is documented in [WebGLPlayer README](../WebGLPlayer/README.md).
