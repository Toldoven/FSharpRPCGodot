module RpcClient.Test

open System
open System.Net.Sockets
open RpcClient.Library
open RpcClient.Service.TestService

let testClient () = async {
    use tcpClient = new TcpClient()
    
    do! tcpClient.ConnectAsync("127.0.0.1", 8080) |> Async.AwaitTask
    
    let client = RpcClient(tcpClient)
    
    let testService = TestServiceClient(client)
        
    client.Start()
    
    use _ = testService.Pong |> Observable.subscribe (fun _ ->
        printfn $"Pong!"
    )
    
    let random = Random()
    
    let testTask (thread: int) = async {
        while true do
            printfn $"Thread: {thread}. Sending request..."
            let! response = testService.Echo { message = "Hello world!" } |> Async.AwaitTask
            printfn $"Thread: {thread}. Received response: {response}"
            do! Async.Sleep(random.Next(100, 500))
            printfn $"Thread: {thread}. Ping..."
            testService.Ping(())
            do! Async.Sleep(random.Next(100, 500))
    }
    
    do! [1..2]
        |> List.map testTask
        |> Async.Parallel
        |> Async.Ignore
    
}
