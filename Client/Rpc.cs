using Godot;
using RpcClient.Service.TestService;

namespace Client;

public partial class Rpc : Node
{
    [Export]
    private string _address = "127.0.0.1";
    [Export]
    private int _port = 8080;
    [Export]
    private bool _autoConnect = true;
    
    public enum State
    {
        Open,
        Connecting,
        Closed,
    }
    
    public State CurrentState = State.Closed;

    private void SetState(State state)
    {
        CurrentState = state;
        EmitSignal(SignalName.StateChanged, (int)CurrentState);
    }
    
    [Signal]
    public delegate void StateChangedEventHandler(State state);
    
    public RpcClient.RpcClient? Client;
    
    public TestServiceClient Test = null!;

    public async void Connect()
    {
        if (CurrentState != State.Closed) throw new InvalidOperationException("Can't open RPC that is not closed");
        
        Client = new RpcClient.RpcClient();
        
        Client.Connected += Utils.EventDeferred(() => {
            SetState(State.Open);
        });

        Client.Disconnected += Utils.EventDeferred(() => {
            SetState(State.Closed);
        });
        
        SetState(State.Connecting);

        try
        {
            await Client.Connect(_address, _port);
            Test = new TestServiceClient(Client);
        }
        catch (Exception e)
        {
            GD.PrintErr($"Couldn't open RPC. Source: {e}");
            SetState(State.Closed);
        }
    }

    public void Disconnect()
    {
        if (CurrentState != State.Open) throw new InvalidOperationException("Can't close RPC that is not open");
        
        if (Client == null) throw new InvalidOperationException("Client should not be null when it's open");
        
        Client.Close();
    }
    
    public override void _Ready()
    {
        if (_autoConnect) Connect();
    }
}