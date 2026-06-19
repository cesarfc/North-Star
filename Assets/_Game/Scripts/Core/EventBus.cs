using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decoupled publish/subscribe event system. All inter-module communication
/// must go through EventBus. Never hold direct references to other modules.
/// </summary>
public static class EventBus
{
    private static readonly Dictionary<Type, List<Delegate>> _handlers
        = new Dictionary<Type, List<Delegate>>();

    /// <summary>
    /// Subscribe a handler to receive events of type T.
    /// Always call Unsubscribe in OnDestroy to prevent memory leaks.
    /// </summary>
    public static void Subscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (!_handlers.ContainsKey(type))
            _handlers[type] = new List<Delegate>();

        _handlers[type].Add(handler);
    }

    /// <summary>
    /// Unsubscribe a previously registered handler.
    /// </summary>
    public static void Unsubscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (_handlers.ContainsKey(type))
            _handlers[type].Remove(handler);
    }

    /// <summary>
    /// Publish an event to all subscribers. Fired synchronously.
    /// </summary>
    public static void Publish<T>(T eventData)
    {
        var type = typeof(T);
        if (!_handlers.ContainsKey(type)) return;

        // Copy list to avoid mutation during iteration
        var handlers = new List<Delegate>(_handlers[type]);
        foreach (var handler in handlers)
        {
            try
            {
                ((Action<T>)handler)?.Invoke(eventData);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventBus] Exception in handler for {type.Name}: {e}");
            }
        }
    }

    /// <summary>
    /// Clear all subscriptions. Call on scene unload if needed.
    /// </summary>
    public static void ClearAll()
    {
        _handlers.Clear();
    }
}
