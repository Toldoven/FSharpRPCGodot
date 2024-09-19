using Godot;
using GodotUtilities;
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

    private void UpdateUi(bool enable)
    {
        _echoButton.Disabled = !enable;
        _pingButton.Disabled = !enable;
        _connectButton.Disabled = enable;
        _disconnectButton.Disabled = !enable;
    }

    private void UpdateUi(Rpc.State state)
    {
        switch (state)
        {
            case Client.Rpc.State.Open:
                UpdateUi(true);
                break;
            case Client.Rpc.State.Connecting:
            case Client.Rpc.State.Closed:
            default:
                UpdateUi(false);
                break;
        }
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
            var result = await _rpc.Test.Echo(message);
            SetResult(result.message);
        };
        
        _pingButton.Pressed += () =>
        {
            _rpc.Test.Ping();
        };

        // TODO: Improve API so you don't have to do it like this
        _rpc.StateChanged += state =>
        {
            if (state != Client.Rpc.State.Open) return;

            _rpc.Test.Pong += Utils.EventDeferred(() =>
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