using System;
using System.Collections.Generic;
using UnityEngine;

namespace OblastZero.Core
{
    /// <summary>Marker interface for services registered into the locator.</summary>
    public interface IService { }

    /// <summary>
    /// Lightweight dependency-injection container. Lets cross-cutting services (save, audio,
    /// scene loading, Steam, localization) be accessed without making every class a singleton
    /// or holding hard references to GameManager.
    ///
    /// Usage:
    ///   ServiceLocator.Register&lt;ISaveService&gt;(new SaveService());
    ///   var save = ServiceLocator.Get&lt;ISaveService&gt;();
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, IService> _services = new();

        public static void Register<T>(T service) where T : class, IService
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] Overwriting existing registration for {type.Name}.");
            }
            _services[type] = service;
            Debug.Log($"[ServiceLocator] Registered {type.Name}");
        }

        public static T Get<T>() where T : class, IService
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                return service as T;
            }
            Debug.LogError($"[ServiceLocator] No service registered for {typeof(T).Name}.");
            return null;
        }

        public static bool TryGet<T>(out T service) where T : class, IService
        {
            service = null;
            if (_services.TryGetValue(typeof(T), out var raw))
            {
                service = raw as T;
                return service != null;
            }
            return false;
        }

        public static void Unregister<T>() where T : class, IService
        {
            _services.Remove(typeof(T));
        }

        public static void ClearAll()
        {
            _services.Clear();
            Debug.Log("[ServiceLocator] All services cleared.");
        }
    }
}
