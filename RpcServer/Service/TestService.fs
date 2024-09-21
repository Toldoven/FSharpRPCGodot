module RpcServer.Service.TestService

open RpcProtocol.Service.TestService
open RpcServer.Server


let pong (server: RpcServer) = server.MakeServerEvent Route.pongEventRoute ()


let register (router: Router) =
    
    router.AddRequestHandler Route.echoRequestRoute (fun _ echo -> async {
        return echo
    })
    
    router.AddClientEventHandler Route.pingEventRoute (fun server _ -> async {
        server |> pong
    })