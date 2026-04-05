using System.Reflection;

namespace Baileys.Utils;

/// <summary>
/// Defines the core contract for the Baileys event emitter, mirroring the
/// <c>BaileysEventEmitter</c> in the TypeScript implementation.
/// </summary>
public interface IBaileysEventEmitter
{
    /// <summary>
    /// Subscribes a listener to an event of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the event payload.</typeparam>
    /// <param name="event">The name of the event (e.g., "connection.update").</param>
    /// <param name="listener">The callback to execute when the event is emitted.</param>
    void On<T>(string @event, Action<T> listener);

    /// <summary>
    /// Unsubscribes a listener from an event.
    /// </summary>
    void Off<T>(string @event, Action<T> listener);

    /// <summary>
    /// Removes all listeners for a specific event.
    /// </summary>
    void RemoveAllListeners(string @event);

    /// <summary>
    /// Synchronously executes each of the listeners registered for the event
    /// named <paramref name="event"/>, passing the supplied <paramref name="arg"/>.
    /// </summary>
    /// <returns><see langword="true"/> if the event had listeners, <see langword="false"/> otherwise.</returns>
    bool Emit<T>(string @event, T arg);
}

/// <summary>
/// A thread-safe implementation of <see cref="IBaileysEventEmitter"/> that uses
/// delegates to manage event subscriptions.
/// </summary>
public sealed class BaileysEventEmitter : IBaileysEventEmitter
{
    private readonly Dictionary<string, List<Delegate>> _listeners = new();
    private readonly object _lock = new();

    public void On<T>(string @event, Action<T> listener)
    {
        lock (_lock)
        {
            if (!_listeners.TryGetValue(@event, out var list))
            {
                list = new List<Delegate>();
                _listeners[@event] = list;
            }
            list.Add(listener);
        }
    }

    public void Off<T>(string @event, Action<T> listener)
    {
        lock (_lock)
        {
            if (_listeners.TryGetValue(@event, out var list))
            {
                list.Remove(listener);
                if (list.Count == 0)
                {
                    _listeners.Remove(@event);
                }
            }
        }
    }

    public void RemoveAllListeners(string @event)
    {
        lock (_lock)
        {
            _listeners.Remove(@event);
        }
    }

    public bool Emit<T>(string @event, T arg)
    {
        Delegate[]? toCall;
        lock (_lock)
        {
            if (!_listeners.TryGetValue(@event, out var list))
            {
                return false;
            }
            toCall = list.ToArray();
        }

        foreach (var listener in toCall)
        {
            try
            {
                if (listener is Action<T> action)
                {
                    action(arg);
                }
                else
                {
                    // Fallback for cases where the type T might be different but compatible (e.g., base class/interface)
                    listener.DynamicInvoke(arg);
                }
            }
            catch (TargetInvocationException ex)
            {
                // Re-throw the inner exception to maintain original stack trace if possible
                if (ex.InnerException != null) throw ex.InnerException;
                throw;
            }
        }

        return true;
    }
}
