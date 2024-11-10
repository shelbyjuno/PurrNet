using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PurrNet.Logging;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet.Modules
{
    public struct RpcRequest
    {
        [UsedByIL]
        public uint id;
        
        [UsedByIL]
        public object tcs;
        
        public Connection target;
        
        public float timeSent;
        public float timeout;
        
        public Action timeoutRequest;
    }
    
    public class RpcRequestResponseModule : INetworkModule, IFixedUpdate
    {
        private readonly List<RpcRequest> _requests = new();
        
        private uint _nextId;
        
        /*[UsedByIL]
        public static Task<T> WaitTask<T>()
        {
            return Task.FromResult<T>(default);
        }*/
        
        private bool _asServer;

        public void Enable(bool asServer)
        {
            _asServer = asServer;
        }

        public void Disable(bool asServer) { }

        public static Task<T> GetNextIdStatic<T>(float timeout, out RpcRequest request)
        {
            var networkManager = NetworkManager.main;
            request = default;
            
            if (!networkManager)
            {
                return Task.FromException<T>(new InvalidOperationException(
                    "NetworkManager is not initialized. Make sure you have a NetworkManager active."));
            }

            var localClient = networkManager.localClientConnection;
            
            if (!localClient.HasValue)
            {
                return Task.FromException<T>(new InvalidOperationException(
                    "Local client connection is not initialized.."));
            }
            
            if (!networkManager.TryGetModule(out RpcRequestResponseModule rpcModule, networkManager.isServer))
            {
                return Task.FromException<T>(new InvalidOperationException(
                    "RpcRequestResponseModule is not initialized.."));
            }
            
            return rpcModule.GetNextId<T>(localClient.Value, timeout, out request);
        }

        public Task<T> GetNextId<T>(Connection target, float timeout, out RpcRequest request)
        {
            PurrLogger.Log($"GetNextId<{typeof(T).Name}>");
            var tcs = new TaskCompletionSource<T>();
            var id = _nextId++;
            
            request = new RpcRequest
            {
                id = id,
                target = target,
                timeSent = Time.unscaledTime,
                timeout = timeout,
                tcs = tcs,
                timeoutRequest = () =>
                {
                    tcs.SetException(new TimeoutException($"Async RPC with request id of '{id}' timed out after {timeout} seconds."));
                }
            };
            
            _requests.Add(request);
            return tcs.Task;
        }

        public void FixedUpdate()
        {
            for (int i = 0; i < _requests.Count; i++)
            {
                var request = _requests[i];
                if (Time.unscaledTime - request.timeSent > request.timeout)
                {
                    _requests.RemoveAt(i);
                    i--;
                    request.timeoutRequest();
                }
            }
        }
    }
}
