module Protocol

open System
open System.Net.Sockets
open MessagePack
open MessagePack.FSharp
open MessagePack.Resolvers
open Microsoft.FSharp.Core

// MessagePack options

let resolver = CompositeResolver.Create(
    FSharpResolver.Instance,
    StandardResolver.Instance
)

let options = MessagePackSerializerOptions.Standard.WithResolver(resolver)

// Route
    
type RequestRoute<'req, 'res>(path: String) =  
    member val Path = path
    
type ServerEventRoute<'e>(path: String) =   
    member val Path = path
    
type ClientEventRoute<'e>(path: String) =   
    member val Path = path
    
// Packet

[<MessagePackObject>]
type ClientPacketType =
    | ClientRequest of requestId: int
    | ClientEvent

[<MessagePackObject>]
type ServerPacketType =
    | ServerResponse of requestId: int
    | ServerEvent
    
[<MessagePackObject>]    
type PacketMeta<'T> = {
    [<Key(0)>]
    packetType: 'T
    [<Key(1)>]
    route: String
}
    
let private sendWithLengthRaw (data: byte array) (stream: NetworkStream) = async {
     let dataLengthBuffer = BitConverter.GetBytes(data.Length)
     do! stream.AsyncWrite(dataLengthBuffer)
     do! stream.AsyncWrite(data)
}
   
let private sendWithLength (data: 'a) (stream: NetworkStream) = async {
     let messageBuffer = MessagePackSerializer.Serialize<'a>(data, options)
     do! sendWithLengthRaw messageBuffer stream
}


// Send packet and serialize body
let sendPacket (packetMeta: PacketMeta<'a>) (body: 'b) (stream: NetworkStream) = async {
    do! sendWithLength packetMeta stream
    do! sendWithLength body stream
}

// Send packet with body that is already serialized
let sendRawPacket (packetMeta: PacketMeta<'a>) (body: byte array) (stream: NetworkStream) = async {
    do! sendWithLength packetMeta stream
    do! sendWithLengthRaw body stream
}

// Read packet from stream
let readPacket<'a> (stream: NetworkStream) = async {
     let! metaLengthBuffer = stream.AsyncRead(4)
     let metaLength = BitConverter.ToInt32(metaLengthBuffer)
     let! metaBuffer = stream.AsyncRead(metaLength)
     let meta = MessagePackSerializer.Deserialize<PacketMeta<'a>>(metaBuffer, options)
     let! bodyLengthBuffer = stream.AsyncRead(4)
     let bodyLength = BitConverter.ToInt32(bodyLengthBuffer)
     let! body = stream.AsyncRead(bodyLength)
     return (meta, body)
}

[<MessagePackObject>]
type Echo = {
    [<Key(0)>]
    message: string
}

let echoRequest = RequestRoute<Echo, Echo>("echo")

let pingEvent = ClientEventRoute<unit>("ping")

let pongEvent = ServerEventRoute<unit>("pong")