module RpcServer.Service.TestService

open RpcProtocol.Service.TestService
open RpcServer.Server


let pong (server: RpcServer) event = server.MakeServerEvent Route.pongEventRoute event


let register (router: Router) =
    
    router.AddRequestHandler Route.echoRequestRoute (fun _ echo -> async {
        return echo  
    })
    
    router.AddClientEventHandler Route.pingEventRoute (fun server _ -> async {
        pong server ()
    })