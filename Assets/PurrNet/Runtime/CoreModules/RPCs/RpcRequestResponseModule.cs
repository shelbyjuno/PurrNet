using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Packets;
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

        public Type responseType;
        public Connection target;
        
        public float timeSent;
        public float timeout;
        
        public Action timeoutRequest;
        public Action<NetworkStream> respond;
    }

    public partial struct RpcResponse : INetworkedData
    {
        public uint id;
        public ByteData data;
        
        public void Serialize(NetworkStream packer)
        {
            packer.Serialize<uint>(ref id);
            
            if (packer.isReading)
            {
                int length = 0;
                packer.Serialize(ref length, false);
                data = packer.Read(length);
            }
            else
            {
                int length = data.length;
                packer.Serialize(ref length, false);
                packer.Write(data);
            }
        }
    }
    
    public class RpcRequestResponseModule : INetworkModule, IFixedUpdate
    {
        private readonly PlayersManager _playersManager;
        private readonly List<RpcRequest> _requests = new();
        
        private uint _nextId;
        
        public RpcRequestResponseModule(PlayersManager playersManager)
        {
            _playersManager = playersManager;
        }
        
        public void Enable(bool asServer)
        {
            _playersManager.Subscribe<RpcResponse>(OnRpcResponse);
        }

        public void Disable(bool asServer)
        {
            _playersManager.Unsubscribe<RpcResponse>(OnRpcResponse);
        }
        
        private void OnRpcResponse(PlayerID conn, RpcResponse data, bool asserver)
        {
            for (int i = 0; i < _requests.Count; i++)
            {
                var request = _requests[i];
                if (request.id == data.id)
                {
                    _requests.RemoveAt(i);

                    using var stream = RPCModule.AllocStream(true);
                    stream.Write(data.data);
                    stream.ResetPointer();
                    
                    request.respond(stream);
                    break;
                }
            }
        }
        
        [UsedByIL]
        public static Task GetNextIdStatic(RPCType rpcType, float timeout, out RpcRequest request)
        {
            var networkManager = NetworkManager.main;
            request = default;
            
            if (!networkManager)
            {
                return Task.FromException(new InvalidOperationException(
                    "NetworkManager is not initialized. Make sure you have a NetworkManager active."));
            }

            var localClient = networkManager.localClientConnection;
            
            if (!localClient.HasValue)
            {
                return Task.FromException(new InvalidOperationException(
                    "Local client connection is not initialized.."));
            }
            
            bool asServer = rpcType switch
            {
                RPCType.ServerRPC => !networkManager.isClient,
                RPCType.TargetRPC => networkManager.isServer,
                RPCType.ObserversRPC => networkManager.isServer,
                _ => throw new ArgumentOutOfRangeException(nameof(rpcType), rpcType, null)
            };
            
            if (!networkManager.TryGetModule(out RpcRequestResponseModule rpcModule, asServer))
            {
                return Task.FromException(new InvalidOperationException(
                    "RpcRequestResponseModule is not initialized.."));
            }
            
            return rpcModule.GetNextId(localClient.Value, timeout, out request);
        }

        [UsedByIL]
        public static Task<T> GetNextIdStatic<T>(RPCType rpcType, float timeout, out RpcRequest request)
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
            
            bool asServer = rpcType switch
            {
                RPCType.ServerRPC => !networkManager.isClient,
                RPCType.TargetRPC => networkManager.isServer,
                RPCType.ObserversRPC => networkManager.isServer,
                _ => throw new ArgumentOutOfRangeException(nameof(rpcType), rpcType, null)
            };
            
            if (!networkManager.TryGetModule(out RpcRequestResponseModule rpcModule, asServer))
            {
                return Task.FromException<T>(new InvalidOperationException(
                    "RpcRequestResponseModule is not initialized.."));
            }
            
            return rpcModule.GetNextId<T>(localClient.Value, timeout, out request);
        }
        
        public Task GetNextId(Connection target, float timeout, out RpcRequest request)
        {
            var tcs = new TaskCompletionSource<bool>();
            var id = _nextId++;
            
            request = new RpcRequest
            {
                id = id,
                target = target,
                timeSent = Time.unscaledTime,
                timeout = timeout,
                tcs = tcs,
                responseType = typeof(void),
                respond = _ =>
                {
                    tcs.SetResult(true);
                },
                timeoutRequest = () =>
                {
                    tcs.SetException(new TimeoutException($"Async RPC with request id of '{id}' timed out after {timeout} seconds."));
                }
            };
            
            _requests.Add(request);
            return tcs.Task;
        }

        public Task<T> GetNextId<T>(Connection target, float timeout, out RpcRequest request)
        {
            var tcs = new TaskCompletionSource<T>();
            var id = _nextId++;
            
            request = new RpcRequest
            {
                id = id,
                target = target,
                timeSent = Time.unscaledTime,
                timeout = timeout,
                tcs = tcs,
                responseType = typeof(T),
                respond = stream =>
                {
                    T response = default;
                    stream.Serialize(ref response);
                    tcs.SetResult(response);
                },
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

        [UsedByIL]
        public static void CompleteRequestWithObject([CanBeNull] object response, RPCInfo info, uint reqId, NetworkManager manager)
        {
            if (response is Task respTask)
            {
                var type = respTask.GetType();
                if (type.IsGenericType)
                {
                    var responseType = type.GetGenericArguments()[0];
                    var method = typeof(RpcRequestResponseModule).GetMethod(nameof(CompleteRequestWithResponse))?.MakeGenericMethod(responseType);
                    method?.Invoke(null, new object[] {respTask, info, reqId, manager});
                }
                else
                {
                    CompleteRequestWithEmptyResponse(respTask, info, reqId, manager);
                }
            }
        }
        
        [UsedByIL]
        public static async void CompleteRequestWithEmptyResponse(Task response, RPCInfo info, uint reqId, NetworkManager manager)
        {
            try
            {
                await response;
                
                if (manager.TryGetModule<RpcRequestResponseModule>(manager.isServer, out var rpcModule))
                {
                    // rpcModule
                    var responsePacket = new RpcResponse
                    {
                        id = reqId,
                        data = ByteData.empty
                    };
                    
                    var channel = info.compileTimeSignature.channel;
                    
                    if (info.asServer)
                        rpcModule._playersManager.Send(info.sender, responsePacket, channel);
                    else rpcModule._playersManager.SendToServer(responsePacket, channel);
                }
                else
                {
                    PurrLogger.LogError("Failed to get module, response won't be sent and receiver will timeout.");
                }
            }
            catch (Exception ex)
            {
                PurrLogger.LogError($"Error while processing RPC response: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [UsedByIL]
        public static async void CompleteRequestWithResponse<T>(Task<T> response, RPCInfo info, uint reqId, NetworkManager manager)
        {
            try
            {
                var result = await response;
                
                if (manager.TryGetModule<RpcRequestResponseModule>(manager.isServer, out var rpcModule))
                {
                    using var tmpStream = RPCModule.AllocStream(false);
                    
                    tmpStream.Serialize(ref result);
                    
                    // rpcModule
                    var responsePacket = new RpcResponse
                    {
                        id = reqId,
                        data = tmpStream.ToByteData()
                    };
                    
                    var channel = info.compileTimeSignature.channel;

                    if (info.asServer)
                         rpcModule._playersManager.Send(info.sender, responsePacket, channel);
                    else rpcModule._playersManager.SendToServer(responsePacket, channel);
                }
                else
                {
                    PurrLogger.LogError("Failed to get module, response won't be sent and receiver will timeout.");
                }
            }
            catch (Exception ex)
            {
                PurrLogger.LogError($"Error while processing RPC response: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
