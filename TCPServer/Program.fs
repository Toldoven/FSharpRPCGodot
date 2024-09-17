

open System
open System.Collections.Generic
open System.Net
open System.Net.Sockets
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
     
    member this.addRequestHandler (route: RequestRoute<'req, 'res>) (handler: 'state -> 'req -> Async<'res>) =
        
        printfn $"Adding route: {route.Route}"
        
        let handler = fun (state: 'state) (body: byte array) -> async {
            let request = MessagePackSerializer.Deserialize(body, options)
            let! response = handler state request
            let result = MessagePackSerializer.Serialize<'res>(response, options)
            return result
        }
        
        let result = requestRouteDictionary.TryAdd(route.Route, handler)
        
        if not result then raise (
            InvalidOperationException("Tried to add a handler for the same route twice")
        )
        
    member this.addEventHandler (route: EventRoute<'e>) (handler: 'state -> 'e -> Async<unit>) =
        let handler = fun (state: 'state) (body: byte array) -> async {
            let event = MessagePackSerializer.Deserialize(body, options)
            do! handler state event
        }
        let result = eventRouteDictionary.TryAdd(route.Route, handler)
        if not result then raise (
            InvalidOperationException("Tried to add a handler for the same route twice")
        )
 
let handleClient (client: TcpClient) (router: Router<unit>) = async {
    let stream = client.GetStream()
    while true do
        let! meta, body = readPacket<ClientPacketType>(stream)
        match meta.packetType with
        | ClientRequest requestId ->
            // TODO: A new task should be started here. The whole server doesn't have multithreading now
            let! response = router.getRequestHandler meta.route () body
            let packetMeta = {
                packetType = ServerResponse requestId
                route = meta.route
            }
            // /|\ That's why it's also safe to write here without locking the stream
            do! sendRawPacket packetMeta response stream
        | ClientEvent -> failwith "todo"
}


let router = Router<unit>()

router.addRequestHandler echoRequest (fun _ request -> async {
    return request
})

let runServer (listener: TcpListener) = async {
    while true do
        let! client = listener.AcceptTcpClientAsync() |> Async.AwaitTask
        printfn "Client connected"
        Async.Start(handleClient client router)
}

let tcpListener = TcpListener(IPAddress.Any, 8080)
printfn "Server started on port 8080"
tcpListener.Start()

Async.Start(runServer tcpListener)

Async.RunSynchronously(Pepega.testClient())