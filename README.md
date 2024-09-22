# F# RPC Protocol + C# Godot Client

Example of F# TCP-based RPC server and protocol integrated with C# Godot Client.

Note that the protocol is not a good universal solution. It is a solution for a [specific game](https://penpalsdelight.com/) we are making.


https://github.com/user-attachments/assets/7e394c8a-5073-4784-9d45-c372f32acdce

## Running the example

Requires [Net 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) and [Godot 4.3](https://godotengine.org/download/archive/4.3-stable/)

1. Clone the repo and open the folder
2. Run the server using `dotnet run --project RpcServer` command
3. Open `Client/project.godot` with [Godot 4.3](https://godotengine.org/download/archive/4.3-stable/) and launch the game

## Requirements

- Type-safe. Define data and routes once.
- Modular. The routes are split into modules.
- Request-response, bi-directional events.
- Persistent connection.
    - The server needs to be stateful. Each client has its own state. We want to load it when the player connects and save it when the player disconnects.
- Not for real-time games.
    - The [game we are building](https://penpalsdelight.com/) features asynchronous multiplayer, so TCP is well-suited for it.
- Dead simple and easily modifiable.
    - The protocol, server, and client combined are 308 lines of code. (comments and blanks not counted)
    - The protocol is not a good universal solution for any game. But because it's so simple — it's easy to modify it as the requirements for the project evolve.

## Protocol

The protocol is implemented on top of TCP.

Each packet is structured like this:

```
Meta Length (4 Bytes) | Meta (N Bytes) | Body Length (4 Bytes) | Body (M Bytes)
```

Meta and Body are encoded with the [MessagePack](https://msgpack.org/index.html) serialization format.

Packet meta is defined as this:

```fsharp
type PacketMeta<'T> = {
    packetType: 'T
    route: String
}
```

The packet types are defined separately for the server and for the client.

```fsharp
// Client to Server
type ClientPacketType =
| ClientRequest of requestId: int
| ClientEvent

// Server to Client
type ServerPacketType =
| ServerResponse of requestId: int * success: Boolean
| ServerEvent
```

Since packet type is a [discriminated union](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions) — each packet type can hold unique associated data. 

## Usage

There are 3 ways to exchange data with this protocol:

* Request and Response
    * Client makes a request, server returns a response
    * requestId is used to associate a request with a response.
* Client to server event (ClientEvent)
* Server to client event (ServerEvent)

### Define a service

You define data and routes in a service.

```fsharp
// RPCProtocol/Service/TestService.fs

namespace RpcProtocol.Service.TestService

open RpcProtocol.Protocol
open MessagePack

// Define data and make it serializable with MessagePack
// This data can be used as request, response, or event body
[<MessagePackObject>]
type Echo = {
[<Key(0)>]
    message: string
}

module Route =

    // You specify request and response type with generics
    // String is used as a key so that both client and the server can implement the same route
    let echoRequestRoute = requestRoute<Echo, Echo> "test:echo"
    
    // Event body is specified with a generic
    // Here the body is empty, so it's just a unit
    let pingEventRoute = clientEventRoute<unit> "test:ping"

    let pongEventRoute = serverEventRoute<unit> "test:pong"
```

### Make a client for the service

```fsharp
// RpcClient/Service/TestService.fs

namespace RpcClient.Service.TestService

open RpcClient
open RpcProtocol.Service.TestService

// We define the client as class, because we need good C# interop
type TestServiceClient(client: RpcClient) =

    // The types for the function are infered based on the route we defined earlier
    // In this case - `this.Echo` is an async function that takes in Echo and returns Echo
    // It's all type-safe
    member this.Echo echo = client.MakeRequest Route.echoRequestRoute echo
    
    // A function that you call to send an event to the server
    member this.Ping () = client.MakeClientEvent Route.pingEventRoute ()
    
    // Here we return an event that you can subscribe to
    [<CLIEvent>]
    member this.Pong = client.MakeServerEvent Route.pongEventRoute
```

It should be possible to generate this code automatically, but for now, it’s not that big of a deal that we need to make it manually. 

### Implement the server part of the service

```fsharp
// RpcServer/Service/TestService.fs

module RpcServer.Service.TestService

open RpcProtocol.Service.TestService
open RpcServer.Server

let pong (server: RpcServer) = server.MakeServerEvent Route.pongEventRoute ()

let register (router: Router) =
    
    router.AddRequestHandler Route.echoRequestRoute (fun _ echo -> async {
        return echo
    })
    
    router.AddClientEventHandler Route.pingEventRoute (fun server _ -> async {
        server |> pong
    })
```

And register it.

```fsharp
// RpcServer/Program.fs

// ...

let router = Router()

TestService.register router

// ...
```

Again, it would be great to generate a stub for the service implementation automatically in the future.

### Now you can use the client

A minimal C# example looks like this:

```csharp
var client = new RpcClient.RpcClient("127.0.0.1", 8080);

await client.Connect()

var test = TestServiceClient(client);

var request = new Echo("Hello world!");

var response = await test.Echo(request);

Console.WriteLine(response.message); // Hello world!

test.Pong += (_, _) =>
{
    Console.WriteLine("Pong!");
};

test.Ping();

// ...

// Pong!
```
