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
    /// Null handlers are ignored.
    /// </summary>
    public static void Subscribe<T>(Action<T> handler)
    {
        if (handler == null) return;

        var type = typeof(T);
        if (!_handlers.TryGetValue(type, out var list))
        {
            list = new List<Delegate>();
            _handlers[type] = list;
        }

        list.Add(handler);
    }

    /// <summary>
    /// Unsubscribe a previously registered handler. Safe to call even if the
    /// handler was never subscribed. Null handlers are ignored.
    /// </summary>
    public static void Unsubscribe<T>(Action<T> handler)
    {
        if (handler == null) return;

        var type = typeof(T);
        if (_handlers.TryGetValue(type, out var list))
        {
            list.Remove(handler);
            if (list.Count == 0)
                _handlers.Remove(type);
        }
    }

    /// <summary>
    /// Publish an event to all subscribers, synchronously and in subscription order.
    /// Each handler is invoked inside its own try/catch so one throwing subscriber
    /// cannot prevent the remaining subscribers from receiving the event.
    /// </summary>
    public static void Publish<T>(T eventData)
    {
        var type = typeof(T);
        if (!_handlers.TryGetValue(type, out var list) || list.Count == 0) return;

        // Copy the list so a handler that subscribes/unsubscribes during dispatch
        // does not mutate the collection we are iterating.
        var handlers = list.ToArray();
        foreach (var handler in handlers)
        {
            try
            {
                ((Action<T>)handler)?.Invoke(eventData);
            }
            catch (Exception e)
            {
                // Isolate the failure: log it and continue notifying the rest.
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
