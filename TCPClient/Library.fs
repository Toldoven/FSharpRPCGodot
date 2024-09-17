namespace TCPClient

open System
open System.Collections.Generic
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open MessagePack
open Protocol

type Client(client: TcpClient) =
    let stream = client.GetStream()
    
    // We need to make sure there is only one writer at the same time
    // We never have more than one reader, so we don't need to lock when reading
    let streamWriteLock = new SemaphoreSlim(1)
    
    // TODO: Replace with SlotMap
    let mutable requestId = 0
    let requestDictionary = Dictionary<int, TaskCompletionSource<byte array>>()
    
    
    // Route path to event handler
    let eventHandlerDictionary = Dictionary<String, byte array -> unit>()
    
    member private this.makeRequest<'req, 'res> (route: RequestRoute<'req, 'res>) = fun (request: 'req) -> async {
        
        // Similar to CompletableFuture in Java
        let taskCompletionSource = TaskCompletionSource<byte array>()
        
        // Atomic increment
        let requestId = Interlocked.Increment(&requestId)
        
        assert requestDictionary.TryAdd(requestId, taskCompletionSource)
        
        let packetMeta = {
            packetType = ClientRequest requestId
            route = route.Path
        }
        
        do! streamWriteLock.WaitAsync() |> Async.AwaitTask
        
        try 
           do! sendPacket packetMeta request stream
        finally
            streamWriteLock.Release() |> ignore
            
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
        
        do! streamWriteLock.WaitAsync() |> Async.AwaitTask
        
        try 
           do! sendPacket packetMeta event stream
        finally
            streamWriteLock.Release() |> ignore
    }
    
    member this.Echo = this.makeRequest echoRequest
    
    member this.Ping = this.makeClientEvent pingEvent
    
    [<CLIEvent>]
    member this.Pong = this.makeServerEvent pongEvent

    member this.Handle = async {
        while client.Connected do
            let! meta, body = readPacket<ServerPacketType>(stream)
            match meta.packetType with
            | ServerResponse requestId ->
                // Find a task by a requestId and complete it
                let task = requestDictionary[requestId]
                task.SetResult(body)
            | ServerEvent ->
                // Get a handler for a route and pass a body into it. This will trigger the event
                eventHandlerDictionary[meta.route] body
    }
   
    
module Pepega =
    let testClient () = async {
        use tcpClient = new TcpClient()
        
        do! tcpClient.ConnectAsync("127.0.0.1", 8080) |> Async.AwaitTask
        
        let client = Client(tcpClient)
        
        client.Handle |> Async.Start
        
        client.Pong.Add(fun _ ->
            printfn $"Pong!"
        )
        
        let testPing () = async {
            while true do
                printfn "Sending request..."
                let! response = client.Echo { message = "Hello world!" }
                printfn $"Received response: {response}"
                do! Async.Sleep(1000)
                printfn "Ping..."
                do! client.Ping(())
                do! Async.Sleep(1000)
        }
        
        do! testPing()
    }
        