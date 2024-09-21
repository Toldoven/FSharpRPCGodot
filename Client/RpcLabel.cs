using Godot;

namespace Client;

public partial class RpcLabel : Label
{
    [Export] private Rpc _rpc = null!;

    private static string StateString(Rpc.State state) => state switch
    {
        Client.Rpc.State.Connected => "Open",
        Client.Rpc.State.Connecting => "Connecting",
        Client.Rpc.State.Closed => "Closed",
        _ => "Unknown"
    };

    public override void _Ready()
    {
        _rpc.StateChanged += state =>
        {
            var stateString = StateString(state);
            Text = $"Client State: {stateString}";
        };
    }
}