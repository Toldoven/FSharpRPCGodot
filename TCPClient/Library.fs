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
    
    member private this.makeRequestRoute<'req, 'res> (route: RequestRoute<'req, 'res>) = fun (request: 'req) -> async {
        do! streamWriteLock.WaitAsync() |> Async.AwaitTask
        
        // Similar to CompletableFuture in Java
        let taskCompletionSource = TaskCompletionSource<byte array>()
        
        // Atomic increment
        let requestId = Interlocked.Increment(&requestId)
        
        assert requestDictionary.TryAdd(requestId, taskCompletionSource)
        
        let packetMeta = {
            packetType = ClientRequest requestId
            route = route.Route
        }
        
        try 
           do! sendPacket packetMeta request stream
        finally
            streamWriteLock.Release() |> ignore
            
        // Wait until the Task is completed
        let! result = taskCompletionSource.Task |> Async.AwaitTask
        
        return MessagePackSerializer.Deserialize<'res>(result, options)
    }
    
    member this.Echo = this.makeRequestRoute echoRequest
    
    //
    //
    // let eventReceived = new Event<ServerEvent>()
    //
    // let mutable requestQueue = Queue()
    //
    // [<CLIEvent>]
    // member this.EventReceived = eventReceived.Publish
    //
    // member this.SendEvent(event: ClientEvent) = task {
    //     printfn $"Sending event to the server: %A{event}"
    //     do! streamSemaphore.WaitAsync() |> Async.AwaitTask
    //     try
    //         do! MessagePack.sendMessage(stream, ClientMessage.Event(event))
    //     finally
    //         streamSemaphore.Release() |> ignore
    // }
    //

    member this.ReadLoop = async {
        while client.Connected do
            let! meta, body = readPacket<ServerPacketType>(stream)
            match meta.packetType with
            | ServerResponse requestId ->
                // Find a task by a requestId and complete it
                let task = requestDictionary[requestId]
                task.SetResult(body)
            | ServerEvent -> failwith "todo"
    }
   
    
    

    
    
module Pepega =
    let testClient () = async {
        use tcpClient = new TcpClient()
        
        do! tcpClient.ConnectAsync("127.0.0.1", 8080) |> Async.AwaitTask
        
        let client = Client(tcpClient)
        
        client.ReadLoop |> Async.Start
        
        // client.EventReceived.Add(fun event ->
        //     printfn $"Received an Event from the server: %A{event}"
        // )
        
        let testPing () = async {
            while true do
                printfn "Sending request..."
                let! response = client.Echo { message = "Hello world!" }
                printfn $"Received response: {response}"
                do! Async.Sleep(1000)
        }
        
        do! testPing()
    }
        