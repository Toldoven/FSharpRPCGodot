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
        Connected,
        Connecting,
        Closed,
    }
    
    public State CurrentState = State.Closed;

    private void SetState(State state)
    {
        CurrentState = state;
        EmitSignal(SignalName.StateChanged, (int)CurrentState);
        if (CurrentState == State.Connected)
        {
            EmitSignal(SignalName.Connected);
        }
    }
    
    [Signal]
    public delegate void StateChangedEventHandler(State state);
    
    [Signal]
    public delegate void ConnectedEventHandler();
    
    private RpcClient.RpcClient? _client;
    
    public TestServiceClient? Test;

    public async void Connect()
    {
        if (CurrentState != State.Closed) throw new InvalidOperationException("Can't open RPC that is not closed");
        
        _client = new RpcClient.RpcClient();
        
        _client.Connected += Utils.EventDeferred(() => {
            SetState(State.Connected);
        });

        _client.Disconnected += Utils.EventDeferred(() => {
            SetState(State.Closed);
        });
        
        SetState(State.Connecting);

        try
        {
            await _client.Connect(_address, _port);
            Test = new TestServiceClient(_client);
        }
        catch (Exception e)
        {
            GD.PrintErr($"Couldn't open RPC. Source: {e}");
            SetState(State.Closed);
        }
    }

    public void Disconnect()
    {
        if (CurrentState != State.Connected) throw new InvalidOperationException("Can't close RPC that is not open");
        
        if (_client == null) throw new InvalidOperationException("Client should not be null when it's open");
        
        _client.Close();
    }
    
    public override void _Ready()
    {
        if (_autoConnect) Connect();
    }
}