using System.Net.Sockets;
using Godot;
using GodotUtilities;
using Microsoft.FSharp.Control;
using RpcClient;
using RpcClient.Service;

namespace Client;

public partial class Main : Node
{
	private TextEdit _echoText = null!;
	private Button _echoButton = null!;
	private Button _pingButton = null!;
	private Label _resultLabel = null!;
	

	private TestService.TestServiceClient _testService = null!;
	
	
	// Called when the node enters the scene tree for the first time.
	public override async void _Ready()
	{
		_echoButton = GetNode<Button>("%EchoButton");
		_pingButton = GetNode<Button>("%PingButton");
		_echoText = GetNode<TextEdit>("%EchoText");
		_resultLabel = GetNode<Label>("%ResultLabel");

		_echoButton.Pressed += async () =>
		{
			var message = new RpcProtocol.Service.TestService.Echo(_echoText.Text);
			Callable.From(
				() => _resultLabel.Text = "Loading..."
			).CallDeferred();
			var result = await _testService.Echo.Invoke(message);
			Callable.From(
				() => _resultLabel.Text = result.message
			).CallDeferred();
		};

		_pingButton.Pressed += () =>
		{
			_testService.Ping.Invoke(null);
		};
		
		
		var tcpClient = new TcpClient();
		await tcpClient.ConnectAsync("127.0.0.1", 8080);
		GD.Print("Connected");
		var rpcClient = new Library.RpcClient(tcpClient);
		_testService = new TestService.TestServiceClient(rpcClient);
		_testService.Pong += (_, _) =>
		{
			Callable.From(
				() => _resultLabel.Text = "Pong!"
			).CallDeferred();
		};
		rpcClient.Start();
	}


}