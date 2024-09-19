module RpcClient.Library

open System
open System.Collections.Generic
open System.Net.Sockets
open System.Threading.Tasks
open RpcProtocol
open RpcProtocol.Library

type private RequestAgentMessage =
    | Register of replyChannel: AsyncReplyChannel<int * TaskCompletionSource<byte array>>
    | Complete of completedRequestId: int * responseBody: byte array

type RpcClient(tcpClient: TcpClient) =
    
    // TODO: Replace with SlotMap
    let requestDictionary = Dictionary<int, TaskCompletionSource<byte array>>()
    
    // String is a route path. Value is a handler for the event at the given route
    let eventHandlerDictionary = Dictionary<String, byte array -> unit>()
    
    // A mailbox agent can only process one message at a time
    // Here it is used to make it so that you can only write one packet at the same time
    // You should not write to stream directly, only through this agent
    let writerAgent = writerAgent tcpClient
    
    // This agent is used so that you can only access `requestDictionary` once at the same time
    // Don't access `requestDictionary` directly
    let requestAgent = new MailboxProcessor<RequestAgentMessage>(fun inbox ->
        let rec loop currentRequestId = async {
            let! message = inbox.Receive()
            match message with
            // Register the request and return the associated requestId and TaskCompletionSource
            | Register replyChannel ->
                let task = TaskCompletionSource<byte array>()
                assert requestDictionary.TryAdd(currentRequestId, task)
                replyChannel.Reply(currentRequestId, task)
                // Increment the id, because we used the current request id
                return! loop (currentRequestId + 1)
            // Get a TaskCompletionSource by a requestId and SetResult to the response body we received
            | Complete (completedRequestId, responseBody) ->
                requestDictionary[completedRequestId].SetResult responseBody
                requestDictionary.Remove(completedRequestId) |> ignore
                // Don't increment, because we didn't create a new request
                return! loop currentRequestId
        }
        loop 0
    )
    
    let handlePacket (meta: PacketMeta<ServerPacketType>, body: byte array) =
       match meta.packetType with
        | ServerResponse requestId ->
            // When we receive a response to a request 
            Complete (requestId, body) |> requestAgent.Post
        | ServerEvent ->
            // Get a handler for a route and pass a body into it. This will trigger the event
            body |> eventHandlerDictionary[meta.route]
    
    // Create a new request method for a given route. Returns a function that makes a request when called
    member this.MakeRequest<'req, 'res> (route: RequestRoute<'req, 'res>) = fun (request: 'req) -> async {
    
        // Register a request. Generate a new requestId that is associated with a TaskCompletionSource
        let! requestId, taskCompletionSource = requestAgent.PostAndAsyncReply Register
        
        let packetMeta = {
            // We pass the unique requestId in the request
            // When the server processes the request - it will return a response with the same id
            // That's how we know what
            packetType = ClientRequest requestId
            route = route.Path
        }
        
        writerAgent.Post(
            serializePacket packetMeta request
        )
            
        // Wait until the Task is completed
        // Task completion source is similar to CompletableFuture in Java
        // The completion is handled in the `requestAgent` when we receive the response from the server
        let! result = taskCompletionSource.Task |> Async.AwaitTask
        
        return deserialize<'res> result
    }
    
    // Register a new server event for a given route
    // Returns a published Event: IEvent
    // Assign it to a variable and annotate with [<CLIEvent>]
    // Then you can add listeners to this event
    member this.MakeServerEvent<'e> (route: ServerEventRoute<'e>) =
        let event = new Event<'e>()
        
        let handler = fun (body: byte array) ->
            let message = deserialize body
            event.Trigger message
        
        eventHandlerDictionary.Add(route.Path, handler)
        
        event.Publish   
        
    member this.MakeClientEvent<'e> (route: ClientEventRoute<'e>) = fun (event: 'e) -> async {
        let packetMeta = {
            packetType = ClientEvent
            route = route.Path
        }
        writerAgent.Post(
            serializePacket packetMeta event
        )
    }

    // TODO: Dispose of stream?
    member private this.handle (stream: NetworkStream) = async {
        let! packet = readPacket<ServerPacketType> stream
        handlePacket packet
        match tcpClient.Connected with
        | true -> return! this.handle stream
        | false -> ()
    }
    
    member this.Start() =
        let stream = tcpClient.GetStream()
        writerAgent.Start()
        requestAgent.Start()
        this.handle stream |> Async.Start
