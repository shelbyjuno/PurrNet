using System;
using System.Collections.Generic;
using UnityEngine;

namespace PurrNet
{
    public class InstanceHandler
    {
        private static Dictionary<Type, object> _instances = new();
        
        /// <summary>
        /// Returns the NetworkManager instance. It will dynamically find it if it's null.
        /// </summary>
        public static NetworkManager NetworkManager
        {
            get
            {
                if (NetworkManager == null)
                    PopulateNetworkManager();
                return NetworkManager;
            }
            private set => NetworkManager = value;
        }
        
        private static void PopulateNetworkManager()
        {
            NetworkManager = GameObject.FindAnyObjectByType<NetworkManager>();
            if (!NetworkManager)
                Debug.LogError($"{nameof(InstanceHandler)}: No {nameof(NetworkManager)} found in scene!");
        }
        
        /// <summary>
        /// Clears every instance in the handler.
        /// </summary>
        public static void ClearAll()
        {
            _instances.Clear();
            NetworkManager = null;
        }

        
        /// <summary>
        /// Registers a instance of the given type, in order to use GetInstance<T> later.
        /// </summary>
        /// <param name="instance">Instance to register</param>
        /// <typeparam name="T"></typeparam>
        public static void RegisterInstance<T>(T instance) where T : class
        {
            _instances[typeof(T)] = instance;
        }
        
        /// <summary>
        /// Unregisters a instance of the given type.
        /// </summary>
        /// <typeparam name="T">Type to unregister</typeparam>
        public static void UnregisterInstance<T>() where T : class
        {
            if (!_instances.ContainsKey(typeof(T)))
                return;
            _instances.Remove(typeof(T));
        }
        
        /// <summary>
        /// Get a registered instance of the given type
        /// </summary>
        /// <typeparam name="T">Type to get the instance of</typeparam>
        /// <returns>Instance of the given type</returns>
        /// <exception cref="KeyNotFoundException">Throws an exception if the given type has not been registered</exception>
        public static T GetInstance<T>() where T : class
        {
            if (!_instances.TryGetValue(typeof(T), out var instance))
                throw new KeyNotFoundException($"Singleton of type {typeof(T)} not found");
            
            return (T)instance;
        }
    }
}
