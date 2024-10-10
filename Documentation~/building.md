# 👾 Android and iOS

There are no limitations for using Shardy on Android or iOS. You can create your own app for these platforms and be sure that everything will work well.

Shardy uses the `IPv6` plugin on iOS to convert host and connection parameters. This is [required for AppStore](https://developer.apple.com/library/archive/documentation/NetworkingInternetWeb/Conceptual/NetworkingOverview/UnderstandingandPreparingfortheIPv6Transition/UnderstandingandPreparingfortheIPv6Transition.html#//apple_ref/doc/uid/TP40010220-CH213-SW1) review, and your app may be rejected if you skip this step.

In either case, you can use the [`TCP`](./reference.md#-transporttype) or `WebSocket` transport type as you see fit.

# 💻 WebGL

Shardy can only work in WebGL builds with the [`WebSocket`](./reference.md#-websocket) transport, this is a limitation of Unity and C#.

> [!IMPORTANT] 
> You can’t use any .NET networking classes within the Web platform because JavaScript code doesn’t have direct access to internet protocol (IP) sockets to implement network connectivity. Specifically, Web doesn’t support any .NET classes within the System.Net namespace. [Read Unity docs](https://docs.unity3d.com/Manual/webgl-networking.html).

Since the `System.Net.Sockets` namespace is not supported on this platform, you cannot use the built-in WebSockets. To solve this "snag", Shardy uses a JavaScript plugin under the hood for WebGL builds, but everything is available from C# for the developer. Just use [`TransportType.WebSocket`](./reference.md#-transporttype) for your clients and services.
