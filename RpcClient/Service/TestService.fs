namespace RpcClient.Service.TestService

open RpcClient
open RpcProtocol.Service.TestService

type TestServiceClient(client: RpcClient) =
    
    member this.Echo echo = client.MakeRequest Route.echoRequestRoute echo
    
    member this.Ping () = client.MakeClientEvent Route.pingEventRoute ()
    
    [<CLIEvent>]
    member this.Pong = client.MakeServerEvent Route.pongEventRoute