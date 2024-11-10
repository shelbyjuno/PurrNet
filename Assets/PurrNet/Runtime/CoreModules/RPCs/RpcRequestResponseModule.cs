using System.Collections.Generic;
using System.Threading.Tasks;
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
    }
    
    public class RpcRequestResponseModule : INetworkModule
    {
        private readonly List<RpcRequest> _requests = new();
        
        private uint _nextId;
        
        /*[UsedByIL]
        public static Task<T> WaitTask<T>()
        {
            return Task.FromResult<T>(default);
        }*/

        public void Enable(bool asServer) { }

        public void Disable(bool asServer) { }

        public Task<T> GetNextId<T>(Connection target, float timeout, out RpcRequest request)
        {
            var tcs = new TaskCompletionSource<T>();
            
            request = new RpcRequest
            {
                id = _nextId++,
                target = target,
                timeSent = Time.unscaledTime,
                timeout = timeout,
                tcs = tcs
            };
            
            _requests.Add(request);
            return tcs.Task;
        }
    }
}
