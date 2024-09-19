namespace RpcProtocol.Service.TestService

open RpcProtocol.Library
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