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
    
type RequestRoute<'req, 'res> = { Path: String }

let requestRoute<'req, 'res> path : RequestRoute<'req, 'res> = { Path = path }
    
    
type ServerEventRoute<'e> = { Path: String }

let serverEventRoute<'e> path : ServerEventRoute<'e> = { Path = path }
   
   
type ClientEventRoute<'e> = { Path: String }

let clientEventRoute<'e> path : ClientEventRoute<'e> = { Path = path }
    
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

let private serializeWithLengthRaw (data: byte array) = 
    let dataLengthBuffer = BitConverter.GetBytes(data.Length)
    Array.concat [dataLengthBuffer; data]

let private serializeWithLength (data: 'a) = 
    let messageBuffer = MessagePackSerializer.Serialize<'a>(data, options)
    serializeWithLengthRaw messageBuffer

// Send packet and serialize body
let serializePacket (packetMeta: PacketMeta<'a>) (body: 'b) = 
    let packetMetaBytes = serializeWithLength packetMeta
    let bodyBytes = serializeWithLength body
    Array.concat [packetMetaBytes; bodyBytes]

// Send packet with body that is already serialized
let serializePacketRaw (packetMeta: PacketMeta<'a>) (body: byte array) = 
    let packetMetaBytes = serializeWithLength packetMeta
    let bodyBytes = serializeWithLengthRaw body
    Array.concat [packetMetaBytes; bodyBytes]



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

let echoRequest = requestRoute<Echo, Echo> "echo"

let pingEvent = clientEventRoute<unit> "ping"

let pongEvent = serverEventRoute<unit> "pong"



let writerAgent (client: TcpClient) = new MailboxProcessor<byte array>(fun inbox ->
    let stream = client.GetStream()
    let rec loop () = async {
        let! packet = inbox.Receive()
        do! stream.AsyncWrite(packet)
        return! loop()
    }
    loop()
)