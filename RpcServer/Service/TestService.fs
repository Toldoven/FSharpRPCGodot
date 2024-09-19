module RpcServer.Service.TestService

open RpcProtocol.Service.TestService
open RpcServer.Server


let pong (server: RpcServer) event = server.MakeServerEvent pongEventRoute event


let register (router: Router) =
    
    router.AddRequestHandler echoRequestRoute (fun _ echo -> async {
        return echo  
    })
    
    router.AddClientEventHandler pingEventRoute (fun server _ -> async {
        pong server ()
    })