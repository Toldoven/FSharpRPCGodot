namespace TCPClient

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Net.Sockets
open System.Threading.Tasks
open MessagePack
open Protocol

type Client(client: TcpClient) =
    let stream = client.GetStream()
    
    // TODO: Replace with SlotMap
    let requestDictionary = ConcurrentDictionary<int, TaskCompletionSource<byte array>>()
    
    // Route path to event handler
    let eventHandlerDictionary = Dictionary<String, byte array -> unit>()
    
    let writerAgent = writerAgent client
    
    let requestGeneratorAgent = new MailboxProcessor<AsyncReplyChannel<int * TaskCompletionSource<byte array>>>(fun inbox ->
        let rec loop requestId = async {
            let! replyChannel = inbox.Receive()
            let requestId, task = requestId, TaskCompletionSource<byte array>()
            assert requestDictionary.TryAdd(requestId, task)
            replyChannel.Reply(requestId, task)
            return! loop (requestId + 1)
        }
        loop 0
    )
    
    let generateNewRequest () = requestGeneratorAgent.PostAndAsyncReply id
    
    
    let handlePacket (meta: PacketMeta<ServerPacketType>, body: byte array) =
       match meta.packetType with
        | ServerResponse requestId ->
            // Find a task by a requestId and complete it
            requestDictionary[requestId].SetResult body
            requestDictionary.Remove(requestId) |> ignore
        | ServerEvent ->
            // Get a handler for a route and pass a body into it. This will trigger the event
            eventHandlerDictionary[meta.route] body
    
    
    member private this.makeRequest<'req, 'res> (route: RequestRoute<'req, 'res>) = fun (request: 'req) -> async {
    
        let! requestId, taskCompletionSource = generateNewRequest()
        
        let packetMeta = {
            packetType = ClientRequest requestId
            route = route.Path
        }
        
        writerAgent.Post(
            serializePacket packetMeta request
        )
            
        // Wait until the Task is completed
        let! result = taskCompletionSource.Task |> Async.AwaitTask
        
        return MessagePackSerializer.Deserialize<'res>(result, options)
    }
    
    member private this.makeServerEvent<'e> (route: ServerEventRoute<'e>) =
        let event = new Event<'e>()
        
        let handler = fun (body: byte array) ->
            let message = MessagePackSerializer.Deserialize<'e>(body, options)
            event.Trigger message
        
        eventHandlerDictionary.Add(route.Path, handler)
        
        event.Publish
        
        
    member private this.makeClientEvent<'e> (route: ClientEventRoute<'e>) = fun (event: 'e) -> async {
        let packetMeta = {
            packetType = ClientEvent
            route = route.Path
        }
        writerAgent.Post(
            serializePacket packetMeta event
        )
    }
    
    member this.Echo = this.makeRequest echoRequest
    
    member this.Ping = this.makeClientEvent pingEvent
    
    [<CLIEvent>]
    member this.Pong = this.makeServerEvent pongEvent
    

    member this.Handle () = async {
        let! packet = readPacket<ServerPacketType>(stream)
        handlePacket packet
        if client.Connected then return! this.Handle()
    }
    
    member this.Start() =
        writerAgent.Start()
        requestGeneratorAgent.Start()
        this.Handle() |> Async.Start
   
    
module Pepega =
    let testClient () = async {
        use tcpClient = new TcpClient()
        
        do! tcpClient.ConnectAsync("127.0.0.1", 8080) |> Async.AwaitTask
        
        let client = Client(tcpClient)
        
        client.Start()
        
        use _ = client.Pong |> Observable.subscribe (fun _ ->
            printfn $"Pong!"
        )
        
        let random = Random()
        
        let testTask (thread: int) = async {
            while true do
                printfn $"Thread: {thread}. Sending request..."
                let! response = client.Echo { message = "Hello world!" }
                printfn $"Thread: {thread}. Received response: {response}"
                do! Async.Sleep(random.Next(100, 500))
                printfn $"Thread: {thread}. Ping..."
                do! client.Ping(())
                do! Async.Sleep(random.Next(100, 500))
        }
        
        do! [1..100]
            |> List.map testTask
            |> Async.Parallel
            |> Async.Ignore
        
    }
        