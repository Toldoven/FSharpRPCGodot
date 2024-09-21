using Godot;
using GodotUtilities;
using RpcProtocol.Service.TestService;

namespace Client;

[Scene]
public partial class RpcTestPanel : Control
{
    [Export] private Rpc _rpc = null!;
    
    [Node] private TextEdit _echoText = null!;
    [Node] private Button _echoButton = null!;
    [Node] private Button _pingButton = null!;
    [Node] private Button _connectButton = null!;
    [Node] private Button _disconnectButton = null!;
    [Node] private Label _resultLabel = null!;

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
        WireNodes();
        
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