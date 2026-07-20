using System;
using System.Collections.Generic;
using UnityEngine;

namespace OblastZero.Core
{
    /// <summary>
    /// Strongly-typed static publish/subscribe bus.
    /// Usage:
    ///   EventBus.Subscribe&lt;RunStartedEvent&gt;(OnRunStarted);
    ///   EventBus.Raise(new RunStartedEvent { RunId = "x" });
    ///   EventBus.Unsubscribe&lt;RunStartedEvent&gt;(OnRunStarted);  // in OnDestroy
    ///
    /// IMPORTANT: Subscribers MUST unsubscribe when destroyed or you'll leak references
    /// across scene loads. The pattern: subscribe in OnEnable, unsubscribe in OnDisable.
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> _handlers = new();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            var type = typeof(T);

            if (_handlers.TryGetValue(type, out var existing))
            {
                _handlers[type] = Delegate.Combine(existing, handler);
            }
            else
            {
                _handlers[type] = handler;
            }

            if (BalanceConstants.VERBOSE_EVENT_BUS_LOGGING)
            {
                Debug.Log($"[EventBus] Subscribed to {type.Name}. Total handlers: {((Delegate)_handlers[type]).GetInvocationList().Length}");
            }
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            var type = typeof(T);

            if (!_handlers.TryGetValue(type, out var existing)) return;

            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null)
            {
                _handlers.Remove(type);
            }
            else
            {
                _handlers[type] = remaining;
            }
        }

        public static void Raise<T>(T evt) where T : struct
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var handler)) return;

            try
            {
                ((Action<T>)handler).Invoke(evt);

                if (BalanceConstants.VERBOSE_EVENT_BUS_LOGGING)
                {
                    Debug.Log($"[EventBus] Raised {type.Name}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventBus] Handler threw on {type.Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>Clears every subscriber. Call only when shutting down or in test teardown.</summary>
        public static void ClearAll()
        {
            _handlers.Clear();
            Debug.Log("[EventBus] All handlers cleared.");
        }
    }
}
