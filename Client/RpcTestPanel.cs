using Godot;
using RpcProtocol.Service.TestService;

namespace Client;

public partial class RpcTestPanel : Control
{
    [Export] private Rpc _rpc = null!;
    
    private TextEdit _echoText = null!;
    private Button _echoButton = null!;
    private Button _pingButton = null!;
    private Button _connectButton = null!;
    private Button _disconnectButton = null!;
    private Label _resultLabel = null!;

    private void UpdateUi(Rpc.State state)
    {
        var connected = state == Client.Rpc.State.Connected;
        _echoButton.Disabled = !connected;
        _pingButton.Disabled = !connected;
        _connectButton.Disabled = connected;
        _disconnectButton.Disabled = !connected;
    }

    private void SetResult(string result)
    {
        _resultLabel.Text = $"Result: {result}";
    }
    
    public override void _Ready()
    {
        _echoText = GetNode<TextEdit>("%EchoText");
        _echoButton = GetNode<Button>("%EchoButton");
        _pingButton = GetNode<Button>("%PingButton");
        _connectButton = GetNode<Button>("%ConnectButton");
        _disconnectButton = GetNode<Button>("%DisconnectButton")!;
        _resultLabel = GetNode<Label>("%ResultLabel");
        
        UpdateUi(_rpc.CurrentState);
        
        _rpc.StateChanged += UpdateUi;
        
        _echoButton.Pressed += async () =>
        {
            var message = new Echo(_echoText.Text);
            SetResult("Loading...");
            // Safe to call because the button is disabled when the client is not connected
            var result = await _rpc.Test!.Echo(message);
            SetResult(result.message);
        };
        
        _pingButton.Pressed += () =>
        {
            _rpc.Test!.Ping();
        };

        _rpc.Connected += () =>
        {
            _rpc.Test!.Pong += Utils.EventDeferred(() =>
            {
                SetResult("Pong!");
            });
        };

        _connectButton.Pressed += () =>
        {
            _rpc.Connect();
        };

        _disconnectButton.Pressed += () =>
        {
            _rpc.Disconnect();
        };
    }
}