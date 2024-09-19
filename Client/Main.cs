using System.Net.Sockets;
using Godot;
using RpcClient.Service.TestService;
using RpcProtocol.Service.TestService;

namespace Client;

public partial class Main : Node
{
	private TextEdit _echoText = null!;
	private Button _echoButton = null!;
	private Button _pingButton = null!;
	private Label _resultLabel = null!;
	
	private TestServiceClient _testService = null!;
	
	public override async void _Ready()
	{
		_echoButton = GetNode<Button>("%EchoButton");
		_pingButton = GetNode<Button>("%PingButton");
		_echoText = GetNode<TextEdit>("%EchoText");
		_resultLabel = GetNode<Label>("%ResultLabel");

		_echoButton.Pressed += async () =>
		{
			var message = new Echo(_echoText.Text);
			_resultLabel.Text = "Loading...";
			var result = await _testService.Echo(message);
			_resultLabel.Text = result.message;
		};

		_pingButton.Pressed += () =>
		{
			_testService.Ping();
		};

		var rpcClient = new RpcClient.RpcClient();

		rpcClient.Connected += Utils.EventDeferred(() => {
			_resultLabel.Text = "Connected!";
		});

		rpcClient.Disconnected += Utils.EventDeferred(() => {
			_resultLabel.Text = "Disconnected!";
		});
		
		await rpcClient.Connect("127.0.0.1", 8080);
		
		_testService = new TestServiceClient(rpcClient);
		
		_testService.Pong += Utils.EventDeferred(() => {
			_resultLabel.Text = "Pong!";
		});
	}
}