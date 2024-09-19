module RpcServer.Program

open System.Net
open System.Net.Sockets
open RpcServer.Server
open RpcClient.Test
open RpcServer.Service

let router = Router()

TestService.register router

let tcpListener = TcpListener(IPAddress.Any, 8080)
printfn "Server started on port 8080"
tcpListener.Start()


let rec acceptClientLoop () = async {
    let! client = tcpListener.AcceptTcpClientAsync() |> Async.AwaitTask
    printfn "Client connected"
    // TODO:
    // Authorize
    // Load state
    // Create a handler after that    
    let handler = new RpcServer(client, router)
    handler.Start()
    return! acceptClientLoop()
}

acceptClientLoop() |> Async.RunSynchronously

// Async.RunSynchronously(testClient())