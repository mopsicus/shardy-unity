# 🌐 Connecting to server

To connect to the Shardy-service you have to use [`Client`](./reference.md#-client) class. Client's contructor receives some params: 

- [validator](./reference.md#-ivalidator) – custom validation class for handshake stages
- [serializer](./reference.md#-iserializer) – custom serializer of all transmitted data
- [options](./reference.md#clientoptions) (optional) – connection parameters, such as transport type and timeouts
- handshake (optional) – handshake data could be passed as last param

> [!IMPORTANT] 
> Make sure the serializer and handshake validator are the same as the service you are connecting to.  

When `Client` is created, add callbacks for events and call `connect` to connect to the server.

```csharp
_client = new Client(new MyHandshake(), new MySerializer());
_client.OnConnect += OnConnect;
_client.OnDisconnect += OnDisconnect;
_client.OnReady += OnReady;

void OnConnect(bool status) {
// connect event
// status is a flag indicating whether the connection is successful or not

// here you can send the handshake data if it was not passed through constructor
//
// if handshake data passed through constructor you shouldn't invoke Handshake() method manually
    if (status) {
        var data = Encoding.UTF8.GetBytes("handshake_data");
        _client.Handshake(data);
    }
}

void OnDisconnect(DisconnectReason reason) {
// disconnect event with reason
}

void OnReady() {
// ready event, client has successfully completed the handshake
//
// ready for receive and send data
}

_client.Connect("127.0.0.1", 30000);
```

# ⛓️ Make request 

> [!NOTE]
> The general difference between requests and commands that is other side must respond to requests and doesn't respond to commands. So this means that when you make a request, you have a callback with response data. And when you send a command, you are simply notifying the other side about something.

To make a request - just use the `Request` method of the [`Client`](./reference.md#-client) and pass the name of the request:

```csharp
_client.Request("test", (response) => {
    Console.WriteLine($"received test data: ${response.ToString()}");
});
```

The second parameter is a callback with response data. You can use it as a lambda expression, as in the snippet above, or make an external method for it:

```csharp
_client.Request("test", OnTest);

void OnTest(PayloadData data) {
    Console.WriteLine($"received test data: ${response.ToString()}");
}
```

If the service does not respond or the request takes too long to complete, you will get an error in [`PayloadData`](./reference.md#payloaddata). The `Error` property is usually empty, but if an error occurred, it contains an error code or message.

And of course you can make requests with param:

```csharp
var data = Encoding.UTF8.GetBytes("some_data");
_client.Request("test", data, (response) => {
    if (!string.IsNullOrEmpty(response.Error)) {
        // handle error here
        return;
    }    
    Console.WriteLine($"received test data: ${response.ToString()}");
});
```

Shardy accept byte array as param, so you can pass any data to request. Make sure you can deserialize and read this data on the server side 🙄

# 💨 Send command

To send a command - use the `Command` method of the `Client` and pass the name of the command:

```csharp
_client.Command("start");
```

Sending command with param:

```csharp
var data = Encoding.UTF8.GetBytes("some_data");
_client.Command("start", data);
```

The command has no callback, you just send an event/notification and don't wait for a response.

# 📭 Subscribe on command

You can subscribe to a command from the server and process it each time it arrives. This is a popular method of updating data. 

```csharp
_client.On("lookup", (data) => {
    Console.WriteLine($"received lookup data: ${data.ToString()}");
});
```

Also you can make an external method for it:

```csharp
_client.On("lookup", OnLookup);

void OnLookup(PayloadData data) {
    Console.WriteLine($"received lookup data: ${data.ToString()}");
}
```

And you can unsubscribe from it and no longer process these events:

```csharp
_client.Off("lookup", OnLookup);
```

# 📮 Subscribe on request

In rare cases you can make a request from the server to the client, this is also possible. When the server makes a request, it waits for a response, so the client must respond when it receives it, otherwise the server will timeout.

To subscribe to a request, use the `OnRequest` method:

```csharp
_client.OnRequest("status", (data) => {
    Console.WriteLine($"received request data: ${data.ToString()}");    
    // if comment lines below, you will give timeout on your backend
    _client.Response(data, Encoding.UTF8.GetBytes("some_data_from_client"));
    // or you can send error on received request
    _client.Error(data, "error_code");
});
```

> [!IMPORTANT] 
> When you make a response or send an error to a received request from the server, you must pass the received payload data as the first parameter because it contains the necessary data from the request.

And you can also unsubscribe from it:

```csharp
_client.OffRequest("status", OnRequestCallback);
```