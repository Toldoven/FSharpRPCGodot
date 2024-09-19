using Godot;
using Microsoft.FSharp.Core;

namespace Client;

public static class Utils
{
    public static EventHandler<T> EventDeferred<T>(Action<T> action) => (_, e) =>
        Callable.From(() => action(e)).CallDeferred(); 
    
    public static EventHandler<Unit> EventDeferred(Action action) => (_, _) =>
        Callable.From(action).CallDeferred(); 
}