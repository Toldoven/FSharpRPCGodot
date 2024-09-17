

open System
open System.Collections.Generic
open System.Net
open System.Net.Sockets
open System.Threading
open MessagePack
open Protocol
open TCPClient

type RequestHandler<'state> = 'state -> byte array -> Async<byte array>

type EventHandler<'state> = 'state -> byte array -> Async<unit>


type Router<'state>() =
    let requestRouteDictionary = Dictionary<String, 'state -> byte array -> Async<byte array>>()
    
    let eventRouteDictionary = Dictionary<String, 'state -> byte array -> Async<unit>>()
    
    member this.getRequestHandler (route: String) =
        requestRouteDictionary[route]
        
    member this.getEventHandler (route: String) =
        eventRouteDictionary[route]
     
    member this.addRequestHandler (route: RequestRoute<'req, 'res>) (handler: 'state -> 'req -> Async<'res>) =
        let handler = fun (state: 'state) (body: byte array) -> async {
            let request = MessagePackSerializer.Deserialize(body, options)
            let! response = handler state request
            let result = MessagePackSerializer.Serialize<'res>(response, options)
            return result
        }
        requestRouteDictionary.Add(route.Path, handler)
        
    member this.addClientEventHandler (route: ClientEventRoute<'e>) (handler: 'state -> 'e -> Async<unit>) =
        let handler = fun (state: 'state) (body: byte array) -> async {
            let event = MessagePackSerializer.Deserialize(body, options)
            do! handler state event
        }
        eventRouteDictionary.Add(route.Path, handler)
  
  
type ClientHandler (client: TcpClient, router: Router<ClientHandler>) =
    let stream = client.GetStream()
    
    // We need to make sure there is only one writer at the same time
    // We never have more than one reader, so we don't need to lock when reading
    let streamWriteLock = new SemaphoreSlim(1)
    
    member private this.makeServerEvent<'e> (route: ServerEventRoute<'e>) = fun (event: 'e) -> async {
        
        let packetMeta = {
            packetType = ServerEvent
            route = route.Path
        }
        
        do! streamWriteLock.WaitAsync() |> Async.AwaitTask
        
        try 
           do! sendPacket packetMeta event stream
        finally
            streamWriteLock.Release() |> ignore
    }
    
    member this.Pong = this.makeServerEvent pongEvent
        
    member this.Handle = async {
        while client.Connected do
            let! meta, body = readPacket<ClientPacketType>(stream)
            match meta.packetType with
            | ClientRequest requestId ->
                // TODO: A new task should be started here
                let! response = router.getRequestHandler meta.route this body
                let packetMeta = {
                    packetType = ServerResponse requestId
                    route = meta.route
                }
                do! streamWriteLock.WaitAsync() |> Async.AwaitTask
                try 
                    do! sendRawPacket packetMeta response stream
                finally
                    streamWriteLock.Release() |> ignore
               
            | ClientEvent ->
                // TODO: A new task should be started here
                do! router.getEventHandler meta.route this body
    }
   
let router = Router<ClientHandler>()

router.addRequestHandler echoRequest (fun _ request -> async {
    return request
})

router.addClientEventHandler pingEvent (fun state _ -> async {
    do! state.Pong()
})

let tcpListener = TcpListener(IPAddress.Any, 8080)
printfn "Server started on port 8080"
tcpListener.Start()

async {
    while true do
        let! client = tcpListener.AcceptTcpClientAsync() |> Async.AwaitTask
        printfn "Client connected"
        let clientHandler = ClientHandler(client, router)
        Async.Start(clientHandler.Handle)
} |> Async.Start

Async.RunSynchronously(Pepega.testClient())