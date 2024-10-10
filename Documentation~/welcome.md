# 🙌 Welcome to Shardy

Shardy is a simple backend framework for Node.js written on TypeScript.

This package is a Unity client for Shardy. It provides an RPC framework with a simple user-friendly API: requests, commands and subscribe for communication with Shardy microservices. It uses its own transport package structure, handshake stages, and custom serializer. You can't use it without [Shardy backend](https://github.com/mopsicus/shardy) because all the features are designed to work in tandem.

# 🪪 Validator

Shardy provides a flexible handshake procedure: 

1. The client sends a handshake to the server
2. Server receives and verifies it:
    - Sends an acknowledgement to the client
    - Disconnects the client, if the verification fails
3.  The client receives the acknowledgement and verifies it:
    - Sends a reply acknowledgement to the server
    - Disconnects, if the verification fails
4. After verifying the handshake and acknowledgement, the client and server can communicate with each other

```csharp
using Shardy;

class MyHandshake : IValidator {

    public ValidatorState VerifyAcknowledgement(byte[] body) {
        // vefify acknowledgement data
    }

    public byte[] Handshake(byte[] body = null) {
        // data for initial handshake
    }

    public byte[] Acknowledgement(byte[] body) {
        // data for acknowledgement after handshake validation passed
    }
}
```

If your implementation does not need to do a two-step handshake, you can set "stubs" on these methods. [See demo](../Samples/Demo/) for that. The last step of the handshake procedure invokes `VerifyAcknowledgement` and returns [`ValidatorState`](./reference.md#validatorstate): success or failure, there you can set the stub if needed.

Your handshake [validator](./reference.md#-ivalidator) class is passed to the [`Client`](./reference.md#-client) constructor as the first parameter, the next is the serializer.

# 🧱 Serializer

Shardy uses a custom serializer for all transmitted data. You can use JSON, MessagePack, Protobuf, FlatBuffers, etc. or your own serializer. You have to create your own serializer class by inheriting the [`Serializer`](./reference.md#-iserializer) class, implement encode/decode methods and pass it to your service and client. The main goal – encode [`PayloadData`](./reference.md#payloaddata) to byte array before sending and back after receiving.

```csharp
using Shardy;

class MyJsonSerializer : ISerializer {

    public byte[] Encode(PayloadData body) {
        // encode PayloadData to Buffer for transporting
    }

    public PayloadData Decode(byte[] body) {
        // decode recevied data and serialize to PayloadData
    }
}
```

I have plans to do some tutorials on the most popular serializers for Unity and their use with Shardy. Stay tuned.

> [!IMPORTANT] 
> Make sure your serialization and validation are the same on the server and client

# ✨ Run HelloWorld

Let's create a simple project. Do it in your favorite version of Unity, but don't use a version less than 2020.3.x :)

1. Create project
2. Install Shardy

    Get it from [releases page](https://github.com/mopsicus/shardy-unity/releases) or add the line to `Packages/manifest.json` and module will be installed directly from Git url:

    ```
    "com.mopsicus.shardy": "https://github.com/mopsicus/shardy-unity.git",
    ```

3. Implement your validator and serializer classes
4. Create `Client` and connect to server
    ```csharp
    void Awake() {
        _client = new Client(new MyValidator(), new MySerializer());
        ...
    }

    void Connect() {
        _client.Connect("127.0.0.1", 30000);
    }
    ```

5. Make a request or send command
    ```csharp
    void TestRequest() {
        _client.Request("test", (response) => {
            Console.WriteLine($"received test data: ${response.ToString()}");
        });
    }    
    ```

> [!NOTE] 
> This is a simple example, of course, without a backend you cannot connect and send a command

Read how to run the [test backend](https://github.com/mopsicus/shardy/blob/main/docs/service.md#-using-template) on Shardy and explore how to [make requests and handle errors](./using.md#️-make-request) in the Unity client.

# 📝 Debug and logging

Shardy uses its own static class [`Logger`](./reference.md#-logger) with several addons:

1. Labeling
2. Time formatting
3. Coloring

```csharp
public static void Info(object data, string label = "", LogColor color = LogColor.Default);
```

You can set a label/tag for your message to group them, and set a custom color if desired. Shardy logger contains the common methods: *info*, *warning* and *error*.

The time format is pre-configured and is similar to a timestamp with a time zone:

```csharp
/// <summary>
/// Date/time format for log
/// </summary>
const string DATE_FORMAT = "yyyy-MM-ddTHH:mm:ss.msZ";
```

All debug outputs are wrapped with preprocessor directives and can be enabled or disabled. Add `SHARDY_DEBUG` to show all log messages without errors and stacktraces. For more detailed logs, add `SHARDY_DEBUG_RAW` to the project scripting define symbols.