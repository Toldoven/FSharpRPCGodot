namespace RpcProtocol.Service.TestService

open RpcProtocol.Protocol
open MessagePack

[<MessagePackObject>]
type Echo = {
    [<Key(0)>]
    message: string
}

module Route =
    
    let echoRequestRoute = requestRoute<Echo, Echo> "test:echo"

    let pingEventRoute = clientEventRoute<unit> "test:ping"

    let pongEventRoute = serverEventRoute<unit> "test:pong"