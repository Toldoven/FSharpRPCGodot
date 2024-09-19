module RpcServer.Program

open System.Net
open System.Net.Sockets
open RpcClient.Service
open RpcClient.Service.TestService
open RpcServer.Server
open RpcClient.Test
open RpcServer.Service.TestService
open RpcServer.ServiceRouter.TestService
open RpcServer.State

let router = Router<State>()

registerTestService router

let tcpListener = TcpListener(IPAddress.Any, 8080)
printfn "Server started on port 8080"
tcpListener.Start()

let initState = (fun server ->
    {
        TestService = RpcServer.Service.TestService.TestServiceClient(server) 
    }
)

let rec acceptClientLoop () = async {
    let! client = tcpListener.AcceptTcpClientAsync() |> Async.AwaitTask
    printfn "Client connected"
    let clientHandler = RpcServer(client, router, initState)
    clientHandler.Start()
    return! acceptClientLoop()
}

acceptClientLoop() |> Async.Start

Async.RunSynchronously(testClient())