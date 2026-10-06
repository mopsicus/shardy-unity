# 🧱 Block

The `Block` is one of the main parts of Shardy - it encodes and decodes raw data.

Block is composed of two parts: **header** and **body**. The header part describes type and length of the block while body contains the binary payload.

## Structure

*type* – block type, 1 byte
- 0x00: handshake – handshake request from client to server and handshake response
- 0x01: handshake acknowledgement – handshake acknowledgement on request
- 0x02: heartbeat – empty block for check connection heartbeat
- 0x03: data – block with some data
- 0x04: kick – disconnect signal
  
*length* – length of body, 3 bytes big-endian integer

*body* – binary payload

All transmitted data encoded to byte array and decoded to [`BlockData`](#blockdata) in [Protocol](#️-protocol), from where it is futher passed to [Commander](#-commander).

Block headers are 4 bytes. The three-byte big-endian body length permits up to `0xFFFFFF` bytes; the default client limit is 1 MiB. Set `ClientOptions.Block` to match a server configured with a custom block limit.

`Block` exposes the following constants and static methods:

```csharp
/// <summary>
/// Encode block for transporting
/// 
/// First byte is type
/// Next - length, and data
/// </summary>
/// <param name="type">Block type: data, kick or heartbeat</param>
/// <param name="body">Body to send</param>
/// <returns>Encoded type + body</returns>
public static byte[] Encode(BlockType type, byte[] body);

/// <summary>
/// Decode block data
/// </summary>
/// <param name="buffer">Buffer with data to decode</param>
/// <returns>Result with type as BlockType and body as byte array</returns>
public static BlockData Decode(byte[] data);

/// <summary>
/// Check received block
/// </summary>
/// <param name="type">Byte index for BlockType</param>
/// <returns>Is correct block or not</returns>
public static bool Check(BlockType type);

/// <summary>
/// Validate a maximum block body size
/// </summary>
public static bool Validate(int blockBodySize);
```

```csharp
public const int BLOCK_HEAD = 4;
public const int MAX_BLOCK_SIZE = 0xFFFFFF;
```

## BlockData

`BlockData` is the structure of a single received block of data. The `Protocol` receives the type of received block and switch [`ProtocolState`](#protocolstate) if necessary, or passes the data to the `Commander` for block processing, handshaking or disconnection.

```csharp
/// <summary>
/// Block result after decode
/// </summary>
public struct BlockData {

    /// <summary>
    /// Type of block
    /// </summary>
    public BlockType Type;

    /// <summary>
    /// Data length
    /// </summary>
    public int Length;

    /// <summary>
    /// Data bytes array
    /// </summary>
    public byte[] Body;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="type">Block type</param>
    /// <param name="body">Data for block</param>
    public BlockData(BlockType type, byte[] body) {
        Type = type;
        Length = body.Length;
        Body = body;
    }
}
```

## BlockType

All received blocks are typed. There are now 5 types of blocks available, as described in [structure](#structure): 

```csharp
/// <summary>
/// Block type to encode
/// </summary>
public enum BlockType {

    /// <summary>
    /// Handshake process
    /// </summary>        
    Handshake,

    /// <summary>
    /// Acknowledgement for success verify
    /// </summary>
    HandshakeAcknowledgement,

    /// <summary>
    /// Ping
    /// </summary>        
    Heartbeat,

    /// <summary>
    /// Data for command, request, response
    /// </summary>        
    Data,

    /// <summary>
    /// Kick from server, disconnect
    /// </summary>        
    Kick
}
```

# 👤 Client

`Client` is the main class for connecting and interacting with Shardy-services. When you want to send a request, command or subscribe to an event from the server, you have to use the `Client` class.

The `Client` constructor receives 4 parameters: 

- [validator](#-ivalidator) – an instance of your handshake validation class
- [serializer](#-iserializer) – an instance of your serializer data class
- [client options](#clientoptions) (optional) – connection params such as [transport type](#-transporttype) and timeouts
- handshake data (optional) – prepared handshake data to be sent after a successful connection

```csharp
/// <summary>
/// Client constructor
/// </summary>
/// <param name="validator">Validator</param>
/// <param name="serializer">Serializer</param>
/// <param name="options">Client options (optional)</param>
/// <param name="handshakePayload">Handshake payload to send after connecting (optional)</param>
public Client(IValidator validator = null, ISerializer serializer = null, ClientOptions options = null, byte[] handshakePayload = null);
```

By default `Client` uses the `TCP` transport type, but this can be changed to `WebSocket` via [`ClientOptions`](#clientoptions).

Read how to create a `Client` and connect to the Shardy-service in the [Run HelloWorld](./welcome.md#-run-helloworld) section or [Connecting to server](./using.md#-connecting-to-server) section.

Available methods:

```csharp
/// <summary>
/// Connect to server
/// </summary>
/// <param name="host">Host to connect</param>
/// <param name="port">Port to connect</param>
public Task Connect(string host, int port);

/// <summary>
/// Disconnect from server
/// </summary>
public void Disconnect();

/// <summary>
/// Send command (event) to server
/// </summary>
/// <param name="commandName">Command name</param>
/// <param name="commandPayload">Command payload bytes</param>
public void Command(string commandName, byte[] commandPayload = null);

/// <summary>
/// Send a request and handle its response in a callback
/// </summary>
/// <param name="requestName">Request name</param>
/// <param name="responseCallback">Callback with the response</param>
/// <param name="requestPayload">Optional request payload bytes</param>
/// <returns>Request id</returns>
public long Request(string requestName, Action<PayloadData> responseCallback, byte[] requestPayload = null);

/// <summary>
/// Send response on request from server
/// </summary>
/// <param name="requestPayload">Request received from the server</param>
/// <param name="responsePayload">Response payload bytes</param>
public void Response(PayloadData requestPayload, byte[] responsePayload = null);

/// <summary>
/// Send error on request from server
/// </summary>
/// <param name="requestPayload">Request received from the server</param>
/// <param name="errorMessage">Error message or code</param>
/// <param name="responsePayload">Response payload bytes</param>
public void Error(PayloadData requestPayload, string errorMessage, byte[] responsePayload = null);

/// <summary>
/// Cancel request manually
/// </summary>
/// <param name="requestId">Request id</param>
public void Cancel(long requestId);

/// <summary>
/// Handshake to verify connection
/// </summary>
/// <param name="handshakePayload">Custom handshake payload</param>
public void Handshake(byte[] handshakePayload = null);

/// <summary>
/// Subscribe on command from server
/// </summary>
/// <param name="commandName">Command name</param>
/// <param name="commandHandler">Handler for the subscribed command</param>
public void On(string commandName, Action<PayloadData> commandHandler);

/// <summary>
/// Unsubscribe from command
/// If callback is null -> clear all of them
/// </summary>
/// <param name="commandName">Command name</param>
/// <param name="commandHandler">Handler to unsubscribe</param>
public void Off(string commandName, Action<PayloadData> commandHandler = null);

/// <summary>
/// Subscribe on request from server that wait response
/// </summary>
/// <param name="requestName">Request name</param>
/// <param name="requestHandler">Handler for the subscribed request</param>
public void OnRequest(string requestName, Action<PayloadData> requestHandler);

/// <summary>
/// Unsubscribe from request from server that wait response
/// </summary>
/// <param name="requestName">Request name</param>
public void OffRequest(string requestName);

/// <summary>
/// Destroy all
/// </summary>
public void Destroy();
```

In addition, `Client` has three callbacks to manage connection and handshake statuses:

```csharp
/// <summary>
/// Event on connected
/// </summary>
public Action<bool> OnConnect = delegate { };

/// <summary>
/// Event on disconnected
/// </summary>
public Action<DisconnectReason> OnDisconnect = delegate { };

/// <summary>
/// Event when ready to work
/// </summary>
public Action OnReady = delegate { };
```

There is also a property to check the current state of the connection:

```csharp
/// <summary>
/// Is client connected
/// </summary>
public bool IsConnected { get; }
```

## ClientOptions

`ClientOptions` is used to pass pre-configured connection parameters to `Client`:

- transport type – type of connection transport: tcp or websocket
- buffer size – lenght of loaded data
- request timeout – timeout (ms) for RPC requests
- pulse interval – interval (ms) for checking connection heartbeat

```csharp
/// <summary>
/// Client and connection options
/// </summary>
public class ClientOptions {

    /// <summary>
    /// Transport type: TCP or WebSocket
    /// </summary>
    public TransportType Type = TransportType.Tcp;

    /// <summary>
    /// Transport buffer size
    /// </summary>
    public int BufferSize = 1024;

    /// <summary>
    /// Maximum block body size in bytes; default is 1 MiB
    /// </summary>
    public int Block = global::Shardy.Block.DEFAULT_BLOCK_SIZE;

    /// <summary>
    /// Timeout for RPC request (ms)
    /// </summary>
    public float RequestTimeout = 10000f;

    /// <summary>
    /// Interval for checking server (ms)
    /// </summary>
    public float PulseInterval = 1000f;

    /// <summary>
    /// Options constructor
    /// </summary>
    /// <param name="type">Transport type</param>
    public ClientOptions(TransportType type = TransportType.Tcp) {
        Type = type;
    }

    /// <summary>
    /// Options constructor
    /// </summary>
    /// <param name="type">Transport type</param>
    /// <param name="bufferSize">Transport buffer size, bytes</param>
    /// <param name="requestTimeout">Timeout for RPC request, ms</param>
    /// <param name="pulseInterval">Interval for checking server, ms</param>
    public ClientOptions(TransportType type, int bufferSize, float requestTimeout, float pulseInterval) {
        Type = type;
        BufferSize = bufferSize;
        RequestTimeout = requestTimeout;
        PulseInterval = pulseInterval;
    }

    /// <summary>
    /// Options constructor with a custom maximum block body size
    /// </summary>
    /// <param name="bufferSize">Transport buffer size, bytes</param>
    /// <param name="requestTimeout">Timeout for RPC request, ms</param>
    /// <param name="pulseInterval">Interval for checking server, ms</param>
    /// <param name="block">Maximum block body size in bytes</param>
    public ClientOptions(TransportType type, int bufferSize, float requestTimeout, float pulseInterval, int block) {
        Type = type;
        BufferSize = bufferSize;
        RequestTimeout = requestTimeout;
        PulseInterval = pulseInterval;
        Block = block;
    }
}
```

# 🔩 Commander

The `Commander` is the next important part of Shardy – it controls how to receive and send blocks, what command or request to invoke, and when to disconnect a client or send a heartbeat.

This is a private class for [`Client`](#-client) and all methods are not called directly, only from the `Client` instance.

```csharp
/// <summary>
/// Start use
/// </summary>
public void Start();

/// <summary>
/// Add callback for request
/// </summary>
/// <param name="requestId">Request id</param>
/// <param name="requestName">Request name</param>
/// <param name="responseCallback">Callback for request</param>
void AddRequest(long requestId, string requestName, Action<PayloadData> responseCallback);

/// <summary>
/// Remove request from list
/// </summary>
/// <param name="requestId">Request id</param>
public void CancelRequest(long requestId);

/// <summary>
/// Subscribe callback on command
/// </summary>
/// <param name="commandName">Command name</param>
/// <param name="commandHandler">Handler for the command</param>
public void AddCommand(string commandName, Action<PayloadData> commandHandler);

/// <summary>
/// Unsubscribe callback from command
/// If callback is null -> clear all of them
/// </summary>
/// <param name="commandName">Command name</param>
/// <param name="commandHandler">Handler to remove</param>
public void CancelCommand(string commandName, Action<PayloadData> commandHandler);

/// <summary>
/// Subscribe to request from server that wait response
/// </summary>
/// <param name="requestName">Request name</param>
/// <param name="requestHandler">Handler for the request</param>
public void AddOnRequest(string requestName, Action<PayloadData> requestHandler);

/// <summary>
/// Unsubscribe from request from server that wait response
/// </summary>
/// <param name="requestName">Request name</param>
public void CancelOnRequest(string requestName);

/// <summary>
/// Exec callback for RPC from server
/// </summary>
/// <param name="payload">Request payload</param>
public void InvokeOnRequest(PayloadData payload);

/// <summary>
/// Send handshake
/// </summary>
/// <param name="handshakePayload">Handshake payload bytes</param>
public void Handshake(byte[] handshakePayload);

/// <summary>
/// Send acknowledge
/// </summary>
/// <param name="acknowledgementPayload">Acknowledgement payload bytes</param>
public void Acknowledge(byte[] acknowledgementPayload);

/// <summary>
/// Disconnect from server
/// </summary>
public void Disconnect();

/// <summary>
/// Send command (event) to server with params
/// </summary>
/// <param name="commandName">Command name</param>
/// <param name="commandPayload">Command payload bytes</param>
public void Command(string commandName, byte[] commandPayload);

/// <summary>
/// Send a request and handle its response in a callback
/// </summary>
/// <param name="requestName">Request name</param>
/// <param name="responseCallback">Callback for the response</param>
/// <param name="requestPayload">Optional request payload bytes</param>
/// <returns>Request id</returns>
public long Request(string requestName, Action<PayloadData> responseCallback, byte[] requestPayload = null);

/// <summary>
/// Send response on request from server
/// </summary>
/// <param name="requestPayload">Request received from the server</param>
/// <param name="responsePayload">Response payload bytes</param>
public void Response(PayloadData requestPayload, byte[] responsePayload = null);

/// <summary>
/// Send error on request from server
/// </summary>
/// <param name="requestPayload">Request received from the server</param>
/// <param name="errorMessage">Error message or code</param>
/// <param name="responsePayload">Response payload bytes</param>
public void Error(PayloadData requestPayload, string errorMessage, byte[] responsePayload = null);

/// <summary>
/// Clear all events
/// </summary>
public void Clear();

/// <summary>
/// Destroy all
/// </summary>
public void Destroy();
```

# 🌐 Connection

`Сonnection` determines how to read and write to the socket depending on which [`TransportType`](#transporttype) is selected.

`Сonnection` is a private class used in [`Transport`](#-transport) to receive and send data. It also detects connection loss and converts IP to IPv6 on iOS clients.

## DisconnectReason

In normal cases, when a client disconnects, `Client` returns the disconnect code `Normal`. If disconnection occurs at the handshake stage, the `Handshake` code will be returned.

```csharp
/// <summary>
/// Disconnect reasons
/// </summary>
public enum DisconnectReason {

    /// <summary>
    /// Normal disconnect
    /// </summary>
    Normal,

    /// <summary>
    /// Have no answer on ping command
    /// </summary>
    Timeout,

    /// <summary>
    /// Handshake validation failed
    /// </summary>
    Handshake,

    /// <summary>
    /// Server is closed`
    /// </summary>
    ServerDown,

    /// <summary>
    /// Some error occurs
    /// </summary>
    Unknown
}
```

# 🏗️ ISerializer

Shardy supports custom serialization of transmitted data. You can use JSON, MessagePack, Protobuf, FlatBuffers, etc. or your own serializer. `ISerializer` is just an interface for your own serializer implementation.

The main goal of this class is to encode [`PayloadData`](#payloaddata) to byte array and decode it back.

```csharp
/// <summary>
/// Serializer interface, uses in Payload
/// </summary>
public interface ISerializer {

    /// <summary>
    /// Serialize data to byte array
    /// </summary>
    /// <param name="body">Target data</param>
    /// <returns>Encoded data</returns>
    byte[] Encode(PayloadData body);

    /// <summary>
    /// Deserialize data
    /// </summary>
    /// <param name="body">Encoded data</param>
    /// <returns>Data to use</returns>
    PayloadData Decode(byte[] body);

}
```

# 🪪 IValidator

Shardy provides an interface for handshake validation. You can implement your own handshake data structure and validation for all stages.

Encode and validate any client-side or server-side data in the handshake process to control connected users and allow or deny them to invoke commands.

> [!IMPORTANT] 
> If your implementation does not need to do a two-step handshake, you can set "stubs" on these methods.

When a client connects to the server, it must successfully complete the handshake before it can begin. Shardy uses a two-step handshake for connections.

Stages of handshake:

1. The client sends a handshake to the server
2. Server receives and verifies it:
    - Sends an acknowledgement to the client
    - Disconnects the client, if the verification fails
3.  The client receives the acknowledgement and verifies it:
    - Sends a reply acknowledgement to the server
    - Disconnects, if the verification fails
4. After verifying the handshake and acknowledgement, the client and server can communicate with each other

```csharp
/// <summary>
/// Handshake interface for client-server validation
/// </summary>
public interface IValidator {

    /// <summary>
    /// Validate acknowledgement data
    /// </summary>
    /// <param name="body">Data for validate</param>
    /// <returns>Validation result</returns>
    ValidatorState VerifyAcknowledgement(byte[] body);

    /// <summary>
    /// Get handshake data for send
    /// </summary>
    /// <param name="body">Data for handshake</param>
    /// <returns>Data for handshake</returns>
    byte[] Handshake(byte[] body = null);

    /// <summary>
    /// Get acknowledgement data for send
    /// </summary>
    /// <param name="body">Data from handshake</param>
    /// <returns>Data for acknowledge</returns>
    byte[] Acknowledgement(byte[] body);

}
```

## ValidatorState

Each verify method must return a typed `ValidatorState` response after processing.

```csharp
/// <summary>
/// Validator state
/// </summary>
public enum ValidatorState {

    /// <summary>
    /// Handshake passed
    /// </summary>
    Success,

    /// <summary>
    /// Handshake failed
    /// </summary>
    Failed
}
```

# 📝 Logger

Shardy provides a simple built-in logger. This is a wrapper for the `Console.WriteLine` and `UnityEngine.Debug.Log` methods with a few useful additions.

```csharp
/// <summary>
/// Log info
/// </summary>
/// <param name="data">Data to log</param>
/// <param name="label">Log label</param>
/// <param name="color">Log color</param>
public static void Info(object data, string label = "", LogColor color = LogColor.Default);

/// <summary>
/// Log warning
/// </summary>
/// <param name="data">Data to log</param>
/// <param name="label">Log label</param>
/// <param name="color">Log color</param>
public static void Warning(object data, string label = "", LogColor color = LogColor.Yellow);

/// <summary>
/// Log error
/// </summary>
/// <param name="data">Data to log</param>
/// <param name="label">Log label</param>
/// <param name="color">Log color</param>
public static void Error(object data, string label = "", LogColor color = LogColor.Red);
```

`LogColor` is an enum associated with preset colors. These colors are chosen for better visualization in the Unity console.

```csharp
/// <summary>
/// Get HEX color for log
/// </summary>
/// <param name="color">Color index</param>
static string GetColor(LogColor color) {
    return color switch {
        LogColor.Red => "#EE4B2B",
        LogColor.Orange => "#FFAC1C",
        LogColor.Yellow => "#FFFF8F",
        LogColor.Green => "#50C878",
        LogColor.Blue => "#89CFF0",
        LogColor.Purple => "#BF40BF",
        _ => "#CCCCCC",
    };
}
```

# ℹ️ Payload

`Payload` is a static class that handles the encoding and decoding of raw data to and from [`PayloadData`](#payloaddata). It is a private class for [`Commander`](#-commander), which encodes data before sending it and decodes it after receiving it.

```csharp
/// <summary>
/// Encode data for transfer
/// </summary>
/// <param name="serializer">Service serializer</param>
/// <param name="payloadType">Payload type</param>
/// <param name="payloadName">Command or request name</param>
/// <param name="requestId">Request id</param>
/// <param name="payloadData">Payload bytes</param>
/// <param name="errorMessage">Error message or code</param>
/// <returns>Encoded data</returns>
public static byte[] Encode(ISerializer serializer, PayloadType payloadType, string payloadName, long requestId, byte[] payloadData, string errorMessage);

/// <summary>
/// Decode received block
/// </summary>
/// <param name="serializer">Service serializer</param>
/// <param name="encodedPayload">Encoded payload bytes</param>
/// <returns>Payload data to use in commander</returns>
public static PayloadData Decode(ISerializer serializer, byte[] encodedPayload);

/// <summary>
/// Check payload for available type
/// </summary>
/// <param name="payloadData">Payload data to check</param>
public static bool Check(PayloadData payloadData);
```

The `check` method controls that the received data is correct and available for processing.

## PayloadData

When [`Client`](#-client) receives data, `PayloadData` is the last structure of the entire data processing chain. `PayloadData` is the data containing the command/request meta-info and the received data, if it exists. The [`PayloadType`](#payloadtype) type in `PayloadData` determines how the data will be processed.

```csharp
public struct PayloadData {

    /// <summary>
    /// Type of data
    /// </summary>
    public PayloadType Type;

    /// <summary>
    /// Command or request name
    /// </summary>
    public string Name;

    /// <summary>
    /// Request id
    /// </summary>
    public long Id;

    /// <summary>
    /// Data
    /// </summary>
    public byte[] Data;

    /// <summary>
    /// Error message or code
    /// </summary>
    public string Error;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="payloadType">Payload type</param>
    /// <param name="payloadName">Command or request name</param>
    /// <param name="requestId">Request id</param>
    /// <param name="payloadData">Payload bytes</param>
    /// <param name="errorMessage">Error message or code</param>
    public PayloadData(PayloadType payloadType, string payloadName, long requestId, byte[] payloadData, string errorMessage) {
        Type = payloadType;
        Id = requestId;
        Name = payloadName;
        Data = payloadData;
        Error = errorMessage;
    }
}
```

## PayloadType

The `PayloadType` defines how the data will be processed.

```csharp
/// <summary>
/// Payload type command
/// </summary>
public enum PayloadType {

    /// <summary>
    /// Request and expect answer
    /// </summary>
    Request,

    /// <summary>
    /// Command without answer, event
    /// </summary>
    Command,

    /// <summary>
    /// Response on request
    /// </summary>
    Response
}
```

# ⛓️ Protocol

`Protocol` is an another internal class that links [`Commander`](#-commander) with [`Transport`](#-transport) and determines state of app: start, handshake or work. Each received [`Block`](#-block) from [`Transport`](#-transport) is detected, decoded to `BlockData` and passed to the `Commander` depending on the current [`Protocol State`](#-protocolstate).

`Commander` can invoke these public methods:

```csharp
/// <summary>
/// Start protocol and transport
/// </summary>
public void Start() {

/// <summary>
/// Send data to connection
/// </summary>
/// <param name="body">Serialized command data</param>
public void Send(byte[] body);

/// <summary>
/// Send heartbeat to connection
/// </summary>
public void Heartbeat();

/// <summary>
/// Send handshake to connection
/// </summary>
/// <param name="body">Handshake data</param>
public void Handshake(byte[] body);

/// <summary>
/// Send acknowledgement
/// </summary>
/// <param name="body">Acknowledge data</param>
public void Acknowledge(byte[] body);

/// <summary>
/// Disconnect from server
/// </summary>
public void Disconnect();   

/// <summary>
/// Destroy all
/// </summary>
public void Destroy();
```

## ProtocolState

The `Commander` can only accept requests and commands if the client has passed the handshake and the protocol state is set to `Work`.

```csharp
/// <summary>
/// Protocol state
/// </summary>
enum ProtocolState {

    /// <summary>
    /// Init state, wait for handshake
    /// </summary>        
    Start,

    /// <summary>
    /// Handshake is in progress
    /// </summary>        
    Handshake,

    /// <summary>
    /// Work state after success handshake
    /// </summary>        
    Work,

    /// <summary>
    /// Protocol closed, any actions ignored
    /// </summary>        
    Closed
}
```

# 💓 Pulse

`Pulse` is an internal class for handling the heartbeat of a connection. It uses param `PulseInterval` from the [`ClientOptions`](#clientoptions).

When Shardy receives any command, handshake or heartbeat, the `checks` counter in `Pulse` is reset. Every `PulseInterval` this class checks the `checks` counter and if the counter value is greater than one, it invokes a callback to send a heartbeat.

```csharp
/// <summary>
/// Reset timer when commands received
/// </summary>
public void Reset();

/// <summary>
/// Stop and switch off service
/// </summary>
public void Clear();
```

The `Clear` method is used to stop the timer when the connection is destroyed.

## ReceivedData

`ReceivedData` is a temporary structure for received data. It is used in [`Connection`](#-connection) and [`Transport`](#-transport) to control the length of the received data.

```csharp
/// <summary>
/// Data received from socket
/// </summary>
public struct ReceivedData {

    /// <summary>
    /// Data length
    /// </summary>
    public int Length;

    /// <summary>
    /// Data bytes array
    /// </summary>
    public byte[] Body;

}
```

# 🚄 Transport

`Transport` is one of the main parts of Shardy – it controls how data will be sent and received.

`Transport` receives data from [`Connection`](#-connection), determines the size of the [`Block`](#-block), checks its type, and starts receiving its entire length. Once received, it passes the data to [`Protocol`](#️-protocol).

The [`Protocol`](#️-protocol) can use these public methods or `Transport`: the `start` method is used to set initial state and begin receiveing data, the `dispatch` method is used to send data, the `close` method is used to terminate transmission, and the `destroy` method is used when the connection is destroyed.

```csharp
/// <summary>
/// Start receiving data
/// </summary>
public void Start();

/// <summary>
/// Send data to socket
/// </summary>
/// <param name="buffer">Bytes array</param>
public async void Dispatch(byte[] buffer);

/// <summary>
/// Close this transport
/// </summary>
public void Close();

/// <summary>
/// Destroy all
/// </summary>
public void Destroy();                                    
```

Under the hood, the `Transport` monitors its `TransportState` and reads the right part of the data or stops the transmission.

```csharp
/// <summary>
/// Transporter state
/// </summary>
enum TransportState {

    /// <summary>
    /// Receive head data
    /// </summary>        
    Head,

    /// <summary>
    /// Receive body data
    /// </summary>        
    Body,

    /// <summary>
    /// Transport is closed, no more data received
    /// </summary>        
    Closed
}
```

## TransportType

Shardy can work with `TCP` sockets or `WebSockets`. When you configure your client, pass the desired transport type via [`ClientOptions`](#clientoptions).

```csharp
/// <summary>
/// Type of transport
/// </summary>
public enum TransportType {

    /// <summary>
    /// TCP sockets transport
    /// </summary>
    Tcp,

    /// <summary>
    /// Websockets transport
    /// </summary>
    WebSocket
}
```

# 🛠️ Utils

`Utils` is a small static class with a few useful functions. 

```csharp
/// <summary>
/// Custom timer for WebGL compatibility
/// </summary>
/// <param name="delay">Interval for timer, ms</param>
/// <param name="cancellation">Cancellation source</param>
/// <param name="callback">Callback on tick</param>
public static async Task SetTimer(float interval, CancellationTokenSource cancellation, Action callback);

/// <summary>
/// Make delay
/// Hack to prevent using Task.Delay in WebGL
/// </summary>
/// <param name="delay">Delay in ms</param>
public static async Task SetDelay(float delay);

/// <summary>
/// Extension to add cancellation for task
/// </summary>
public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken);

/// <summary>
/// Extension to add cancellation for task
/// </summary>
public static async Task WithCancellation(this Task task, CancellationToken cancellationToken);

/// <summary>
/// Convert data to string
/// </summary>
public static string DataToString(byte[] data);

/// <summary>
/// Convert data to log string
/// </summary>
public static string DataToDebug(byte[] data);

/// <summary>
/// Escape string for log
/// </summary>
/// <param name="data">Input string</param>
static string ToLiteral(string data);
```

# 🔌 WebSocket

`WebSocket` class is used only for [WebGL](./building.md#-webgl) builds. This is a wrapper for JS plugin and used in [`WebSocketManager`](#️-websocketmanager) as an unit for websocket connections.

> [!NOTE] 
> When you create an app with the WebSocket transport type on Android or iOS, a native implementation of WebSocket is used.

Available methods:

```csharp
/// <summary>
/// Create websocket
/// </summary>
public WebSocket();

/// <summary>
/// Create websocket
/// </summary>
/// <param name="subprotocol">Optional subprotocol</param>
public WebSocket(string subprotocol);

/// <summary>
/// Create websocket
/// </summary>
/// <param name="subprotocol">Optional subprotocols list</param>
public WebSocket(List<string> subprotocols);

/// <summary>
/// Current state
/// </summary>
public WebSocketStateCode State;

/// <summary>
/// Connect to server
/// </summary>
public async Task<bool> Connect(string url);

/// <summary>
/// Close connection
/// </summary>
/// <param name="code">Close code</param>
/// <param name="reason">Reason</param>
public bool Close(WebSocketCloseCode code = WebSocketCloseCode.Normal, string reason = null)
;
/// <summary>
/// Send data as buffer
/// </summary>
/// <param name="data">Array of bytes</param>
public async Task<bool> Send(byte[] data);

/// <summary>
/// Receive data from socket
/// </summary>
/// <param name="buffer">Buffer to write</param>
/// <returns>Received length</returns>
public async Task<int> Receive(byte[] buffer);
```

It also uses the standard WebSocket status and error codes:

```csharp
/// <summary>
/// Websocket state codes
/// </summary>
enum WebSocketStateCode {
    Connecting,
    Open,
    Closing,
    Closed,
    Error
}

/// <summary>
/// Return error codes
/// </summary>
enum WebSocketErrorCode {
    NotFound = -1,
    AlreadyConnected = -2,
    NotConnected = -3,
    AlreadyClosing = -4,
    AlreadyClosed = -5,
    NotOpened = -6,
    CloseFail = -7,
    SendFail = -8,
    Unknown = -999
}

/// <summary>
/// Websocket code for close
/// </summary>
enum WebSocketCloseCode {
    NotSet = 0,
    Normal = 1000,
    Away = 1001,
    ProtocolError = 1002,
    UnsupportedData = 1003,
    Undefined = 1004,
    NoStatus = 1005,
    Abnormal = 1006,
    InvalidData = 1007,
    PolicyViolation = 1008,
    TooBig = 1009,
    MandatoryExtension = 1010,
    ServerError = 1011,
    TlsHandshakeFailure = 1015
}
```

# 🎛️ WebSocketManager

`WebSocketManager` is an internal static class for managing all [`WebSocket`](#-websocket) connections, only for WebGL builds.

`WebSocketManager` creates websockets in the JS plugin, sets identifiers and callbacks from the plugin to the C# wrapper.

```csharp
/// <summary>
/// Set debug mode
/// </summary>
public static void SetDebug(bool isDebug);

/// <summary>
/// Set callbacks
/// </summary>
public static void Init();

/// <summary>
/// Add new socket
/// </summary>
/// <param name="socket">Websocket instance</param>
/// <returns>Id</returns>
public static int Add(WebSocket socket);

/// <summary>
/// Remove websocket instance
/// </summary>
/// <param name="id">Instance id</param>
public static void Destroy(int id);
```

`SetDebug` takes the value `true` if the `SHARDY_DEBUG_RAW` condition is enabled.