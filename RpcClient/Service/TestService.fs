module RpcClient.Service.TestService

open RpcClient.Library
open RpcProtocol.Service.TestService

type TestServiceClient(client: RpcClient) =
    
    member this.Echo = client.MakeRequest echoRequestRoute
    
    member this.Ping = client.MakeClientEvent pingEventRoute
    
    [<CLIEvent>]
    member this.Pong = client.MakeServerEvent pongEventRoute