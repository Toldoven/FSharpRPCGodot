module RpcServer.Program

open System.Net
open System.Net.Sockets
open RpcServer.Server
open RpcServer.Service
open Serilog
open Serilog.Sinks.SystemConsole.Themes

let router = Router()

TestService.register router
    
Log.Logger <- LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {NewLine}{Exception}", theme = AnsiConsoleTheme.Code)
    .CreateLogger()
    
let port = 8080

let tcpListener = TcpListener(IPAddress.Any, port)
Log.Information $"Listener started on port {port}"
tcpListener.Start()

let rec acceptClientLoop () = async {
    let! client = tcpListener.AcceptTcpClientAsync() |> Async.AwaitTask
    // TODO:
    // Authorize
    // Load state
    // Create a handler after that    
    let handler = new RpcServer(client, router)
    handler.Start()
    return! acceptClientLoop ()
}

acceptClientLoop() |> Async.RunSynchronously