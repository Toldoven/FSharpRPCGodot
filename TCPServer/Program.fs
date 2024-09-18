

open System
open System.Collections.Generic
open System.Net
open System.Net.Sockets
open System.Threading
open MessagePack
open Protocol
open TCPClient


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
   
    let writerAgent = writerAgent client
    
    member private this.handlePacket (meta: PacketMeta<ClientPacketType>, body: byte array) = async {
        match meta.packetType with
        | ClientRequest requestId ->
            let! response = router.getRequestHandler meta.route this body
            let packetMeta = {
                packetType = ServerResponse requestId
                route = meta.route
            }
            writerAgent.Post(
                serializePacketRaw packetMeta response
            )
        | ClientEvent ->
            do! router.getEventHandler meta.route this body
    }
    
        
    member private this.makeServerEvent (route: ServerEventRoute<'e>) = fun (event: 'e) ->
        let packetMeta = {
            packetType = ServerEvent
            route = route.Path
        }
        writerAgent.Post(
            serializePacket packetMeta event
        )
    
    member this.Pong = this.makeServerEvent pongEvent
        
    member this.Handle() = async {
        let! packet = readPacket<ClientPacketType>(stream)
        this.handlePacket packet |> Async.Start
        if client.Connected then return! this.Handle()
    }
    
    member this.Start() =
        writerAgent.Start()
        this.Handle() |> Async.Start

   
let router = Router<ClientHandler>()

router.addRequestHandler echoRequest (fun _ request -> async {
    return request
})

router.addClientEventHandler pingEvent (fun state _ -> async {
    state.Pong()
})

let tcpListener = TcpListener(IPAddress.Any, 8080)
printfn "Server started on port 8080"
tcpListener.Start()

let rec acceptClientLoop () = async {
    let! client = tcpListener.AcceptTcpClientAsync() |> Async.AwaitTask
    printfn "Client connected"
    let clientHandler = ClientHandler(client, router)
    clientHandler.Start()
    return! acceptClientLoop()
}

acceptClientLoop() |> Async.Start

Async.RunSynchronously(Pepega.testClient())