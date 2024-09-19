module RpcServer.ServiceRouter.TestService

open RpcProtocol.Service.TestService
open RpcServer.State
open RpcServer.Server

let registerTestService (router: Router<State>) =
    router.AddRequestHandler echoRequestRoute (fun _ echo -> async {
        return echo  
    })
    router.AddClientEventHandler pingEventRoute (fun state _ -> async {
        state.TestService.Pong()
    })