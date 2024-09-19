module RpcServer.Server

open System
open System.Collections.Generic
open System.Net.Sockets
open System.Threading
open RpcProtocol.Library


exception UnknownRoute of route: string
    with override this.Message = $"UnknownRoute: Can't find route with the specified name: {this.route}"


type Router () =
    
    let requestRouteDictionary = Dictionary<String, RpcServer -> byte array -> Async<byte array>>()
    
    let eventRouteDictionary = Dictionary<String, RpcServer -> byte array -> Async<unit>>()
    
    member private this.getHandler (dictionary: Dictionary<String, 'V>) route =
        if dictionary.ContainsKey route then
            dictionary[route]
        else
            raise (UnknownRoute(route))
    
    member this.GetRequestHandler (route: String) = this.getHandler requestRouteDictionary route
        
    member this.GetEventHandler (route: String) = this.getHandler eventRouteDictionary route 
     
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
    
    let cancellationToken = new CancellationTokenSource()
    
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
            try
                let! response = router.GetRequestHandler meta.route this body
                let packetMeta = {
                    packetType = ServerResponse (requestId, true)
                    route = meta.route
                }
                writerAgent.Post(
                    serializePacketRaw packetMeta response
                )
            with
            | e ->
                printfn $"Error when processing request at `{meta.route}`: {e}"
                let packetMeta = {
                    packetType = ServerResponse (requestId, false)
                    route = meta.route
                }
                writerAgent.Post(
                    serializePacket packetMeta { message = e.ToString() }
                )

        | ClientEvent ->
            try
                do! router.GetEventHandler meta.route this body
            with
            | e ->
                printfn $"Error when processing event at `{meta.route}`: {e}"
    }
    
    interface IDisposable with
        member this.Dispose() =
            cancellationToken.Cancel()
            cancellationToken.Dispose()
            (writerAgent :> IDisposable).Dispose()
        
        
    member private this.handle () = async {
        try
            use stream = tcpClient.GetStream()
            let! packet = readPacket<ClientPacketType> stream cancellationToken.Token 
            Async.Start(this.handlePacket packet, cancellationToken.Token)
            return! this.handle ()
        with
        | _ ->
            printfn "Client disconnected"
            (this :> IDisposable).Dispose()     
    }
    
    member this.Start() =
        writerAgent.Start()
        this.handle () |> Async.Start
