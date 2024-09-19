module RpcProtocol.Library

open System
open System.Net.Sockets
open System.Threading
open MessagePack
open MessagePack.FSharp
open MessagePack.Resolvers
open Microsoft.FSharp.Core

// MessagePack options
let private resolver = CompositeResolver.Create(
    FSharpResolver.Instance,
    StandardResolver.Instance
)

let private options = MessagePackSerializerOptions.Standard.WithResolver(resolver)

let serialize<'a> (value: 'a) =
    MessagePackSerializer.Serialize<'a>(value, options)

let deserialize<'a> (value: byte array) =
    MessagePackSerializer.Deserialize<'a>(value, options)

    
// A record that defines a request route. The generic parameters specify the request type and the response type
type RequestRoute<'req, 'res> = { Path: String }

// A helper function to make a RequestRoute
let requestRoute<'req, 'res> path : RequestRoute<'req, 'res> = { Path = path }
     
// A record that defines a server event route. The generic parameter specifies the event type
type ServerEventRoute<'e> = { Path: String }

// A helper function to make a ServerEventRoute
let serverEventRoute<'e> path : ServerEventRoute<'e> = { Path = path }
   
// A record that defines a server event route. The generic parameter specifies the event type
type ClientEventRoute<'e> = { Path: String }

// A helper function to make a ClientEventRoute
let clientEventRoute<'e> path : ClientEventRoute<'e> = { Path = path }
    
    
[<MessagePackObject>]
// Client to Server
type ClientPacketType =
    | ClientRequest of requestId: int
    | ClientEvent

[<MessagePackObject>]
// Server to Client
type ServerPacketType =
    | ServerResponse of requestId: int * success: Boolean
    | ServerEvent
    
    
[<MessagePackObject>]
type ResponseFailure = {
    [<Key(0)>]
    message: String
}
    
    
[<MessagePackObject>]
// Metadata attached to every packet
// 'T is packet type. Either ClientPacketType or ServerPacketType
type PacketMeta<'T> = {
    [<Key(0)>]
    packetType: 'T 
    [<Key(1)>]
    route: String
}

// Get the length of a message and serialize it as raw bytes
let inline private getSerializedLength (data: byte array) = BitConverter.GetBytes(data.Length)

// Serialize length of data as raw bytes and the data itself with message pack
let inline private serializeWithLength (data: 'a) = 
    let messageBuffer = serialize<'a> data
    getSerializedLength messageBuffer, messageBuffer

// Send packet and serialize body
let serializePacket (packetMeta: PacketMeta<'a>) (body: 'b) : byte array =
    let metaLength, metaBytes = serializeWithLength packetMeta
    let bodyLength, bodyBytes = serializeWithLength body
    Array.concat [metaLength; metaBytes; bodyLength; bodyBytes]

// Send packet with body that is already serialized
let serializePacketRaw (packetMeta: PacketMeta<'a>) (bodyBytes: byte array) = 
    let metaLength, metaBytes = serializeWithLength packetMeta
    let bodyLength = getSerializedLength bodyBytes
    Array.concat [metaLength; metaBytes; bodyLength; bodyBytes]


//
let inline private asyncRead (stream: NetworkStream) (token: CancellationToken) (length: int) = async {
    let buffer = Array.zeroCreate length
    do! stream.ReadAsync(buffer, 0, length, token) |> Async.AwaitTask |> Async.Ignore
    return buffer
}
    

// Read packet from stream
let readPacket<'a> (stream: NetworkStream) (token: CancellationToken) = async {
     let asyncRead = asyncRead stream token
     let! metaLengthBuffer = asyncRead 4
     let metaLength = BitConverter.ToInt32(metaLengthBuffer)
     let! metaBuffer = asyncRead metaLength
     let meta = deserialize<PacketMeta<'a>> metaBuffer
     let! bodyLengthBuffer = asyncRead 4
     let bodyLength = BitConverter.ToInt32(bodyLengthBuffer)
     let! body = asyncRead bodyLength
     return (meta, body)
}

// An agent that makes sure there is only one packet being written in the stream at the same time
let writerAgent (client: TcpClient) = new MailboxProcessor<byte array>(fun inbox ->
    let stream = client.GetStream()
    let rec loop () = async {
        let! packet = inbox.Receive()
        do! stream.AsyncWrite(packet)
        return! loop()
    }
    loop()
)