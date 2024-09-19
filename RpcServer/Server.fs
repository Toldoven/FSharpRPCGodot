module RpcServer.Server

open System
open System.Collections.Generic
open System.Net.Sockets
open RpcProtocol.Library


type Router () =
    
    let requestRouteDictionary = Dictionary<String, RpcServer -> byte array -> Async<byte array>>()
    
    let eventRouteDictionary = Dictionary<String, RpcServer -> byte array -> Async<unit>>()
    
    member this.GetRequestHandler (route: String) =
        requestRouteDictionary[route]
        
    member this.GetEventHandler (route: String) =
        eventRouteDictionary[route]
     
    member this.AddRequestHandler (route: RequestRoute<'req, 'res>) (handler: RpcServer -> 'req -> Async<'res>) =
        let handler = fun (state: RpcServer) (body: byte array) -> async {
            let! response = deserialize body |> handler state 
            return serialize response
        }
        requestRouteDictionary.Add(route.Path, handler)
        
    member this.AddClientEventHandler (route: ClientEventRoute<'e>) (handler: RpcServer -> 'e -> Async<unit>) =
        let handler = fun (state: RpcServer) (body: byte array) -> async {
            let event = deserialize body
            do! handler state event
        }
        eventRouteDictionary.Add(route.Path, handler)


and RpcServer (tcpClient: TcpClient, router: Router) =
    
    let writerAgent = writerAgent tcpClient
    
    // member private this.state = initState this
    
    member this.MakeServerEvent (route: ServerEventRoute<'e>) = fun (event: 'e) ->
        let packetMeta = {
            packetType = ServerEvent
            route = route.Path
        }
        writerAgent.Post(
            serializePacket packetMeta event
        )
    
    member private this.handlePacket (meta: PacketMeta<ClientPacketType>, body: byte array) = async {
        match meta.packetType with
        | ClientRequest requestId ->
            let! response = router.GetRequestHandler meta.route this body
            let packetMeta = {
                packetType = ServerResponse requestId
                route = meta.route
            }
            writerAgent.Post(
                serializePacketRaw packetMeta response
            )
        | ClientEvent ->
            do! router.GetEventHandler meta.route this body
    }
        
    member this.Handle (stream: NetworkStream) = async {
        try
            let! packet = readPacket<ClientPacketType>(stream)
            this.handlePacket packet |> Async.Start
            if tcpClient.Connected then return! this.Handle stream
        with
        | _ -> printfn "Client disconnected"
    }
    
    member this.Start() =
        let stream = tcpClient.GetStream()
        writerAgent.Start()
        this.Handle(stream) |> Async.Start
