module RpcServer.Service.TestService

open RpcServer.Server
open RpcProtocol.Service.TestService

type TestServiceClient<'state>(server: RpcServer<'state>) =
    member this.Pong = server.MakeServerEvent pongEventRoute
    