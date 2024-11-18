#if UNITY_MONO_CECIL
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PurrNet.Modules;
using PurrNet.Packets;
using PurrNet.Utils;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;
using UnityEngine.Scripting;
using Channel = PurrNet.Transports.Channel;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;

namespace PurrNet.Codegen
{
    public enum SpecialParamType
    {
        SenderId,
        RPCInfo
    }

    public struct RPCMethod
    {
        public RPCSignature Signature;
        public MethodDefinition originalMethod;
        public string ogName;
    }
    
    [UsedImplicitly]
    public class PostProcessor : ILPostProcessor
    {
        public override ILPostProcessor GetInstance() => this;
        
        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            var name = compiledAssembly.Name;
            
            if (name.StartsWith("Unity."))
                return false;
            
            if (name.StartsWith("UnityEngine."))
                return false;

            return !name.Contains("Editor");
        }
        
        private static int GetIDOffset(TypeDefinition type, ICollection<DiagnosticMessage> messages)
        {
            try
            {
                var baseType = type.BaseType?.Resolve();

                if (baseType == null)
                    return 0;

                return GetIDOffset(baseType, messages) +
                       baseType.Methods.Count(m => GetMethodRPCType(m, messages).HasValue);
            }
            catch (Exception e)
            {
                messages.Add(new DiagnosticMessage
                {
                    DiagnosticType = DiagnosticType.Error,
                    MessageData = "Failed to get ID offset: " + e.Message
                });
                
                return 0;
            }
        }
        
        private static bool InheritsFrom(TypeDefinition type, string baseTypeName)
        {
            try
            {
                if (type.BaseType == null)
                    return false;

                if (type.BaseType.FullName == baseTypeName)
                    return true;

                var btype = type.BaseType.Resolve();
                return btype != null && InheritsFrom(btype, baseTypeName);
            }
            catch (Exception e)
            {
                throw new Exception($"InheritsFrom : Failed to resolve base type of {type.FullName}, {e.Message}");
            }
        }
        
        private static RPCSignature? GetMethodRPCType(MethodDefinition method, ICollection<DiagnosticMessage> messages)
        {
            RPCSignature data = default;
            int rpcCount = 0;
            
            foreach (var attribute in method.CustomAttributes)
            {
                if (attribute.AttributeType.FullName == typeof(ServerRpcAttribute).FullName)
                {
                    if (attribute.ConstructorArguments.Count != 4)
                    {
                        Error(messages, "ServerRPC attribute must have 4 arguments", method);
                        return null;
                    }
                    
                    var channel = (Channel)attribute.ConstructorArguments[0].Value;
                    var runLocally = (bool)attribute.ConstructorArguments[1].Value;
                    var requireOwnership = (bool)attribute.ConstructorArguments[2].Value;
                    var asyncTimeoutInSec = (float)attribute.ConstructorArguments[3].Value;
                    
                    data = new RPCSignature
                    {
                        type = RPCType.ServerRPC,
                        channel = channel,
                        runLocally = runLocally,
                        requireOwnership = requireOwnership,
                        requireServer = false,
                        bufferLast = false,
                        excludeOwner = false,
                        isStatic = method.IsStatic,
                        asyncTimeoutInSec = asyncTimeoutInSec
                    };
                    rpcCount++;
                }
                else if (attribute.AttributeType.FullName == typeof(ObserversRpcAttribute).FullName)
                {
                    if (attribute.ConstructorArguments.Count != 7)
                    {
                        Error(messages, "ObserversRPC attribute must have 7 arguments", method);
                        return null;
                    }
                    
                    var channel = (Channel)attribute.ConstructorArguments[0].Value;
                    var runLocally = (bool)attribute.ConstructorArguments[1].Value;
                    var bufferLast = (bool)attribute.ConstructorArguments[2].Value;
                    var requireServer = (bool)attribute.ConstructorArguments[3].Value;
                    var excludeOwner = (bool)attribute.ConstructorArguments[4].Value;
                    var excludeSender = (bool)attribute.ConstructorArguments[5].Value;
                    var asyncTimeoutInSec = (float)attribute.ConstructorArguments[6].Value;

                    data = new RPCSignature
                    {
                        type = RPCType.ObserversRPC,
                        channel = channel,
                        runLocally = runLocally,
                        bufferLast = bufferLast,
                        requireServer = requireServer,
                        requireOwnership = false,
                        excludeOwner = excludeOwner,
                        excludeSender = excludeSender,
                        isStatic = method.IsStatic,
                        asyncTimeoutInSec = asyncTimeoutInSec
                    };
                    rpcCount++;
                }
                else if (attribute.AttributeType.FullName == typeof(TargetRpcAttribute).FullName)
                {
                    if (attribute.ConstructorArguments.Count != 5)
                    {
                        Error(messages, "TargetRPC attribute must have 5 arguments", method);
                        return null;
                    }
                    
                    var channel = (Channel)attribute.ConstructorArguments[0].Value;
                    var runLocally = (bool)attribute.ConstructorArguments[1].Value;
                    var bufferLast = (bool)attribute.ConstructorArguments[2].Value;
                    var requireServer = (bool)attribute.ConstructorArguments[3].Value;
                    var asyncTimeoutInSec = (float)attribute.ConstructorArguments[4].Value;

                    data = new RPCSignature
                    {
                        type = RPCType.TargetRPC,
                        channel = channel,
                        runLocally = runLocally,
                        bufferLast = bufferLast,
                        requireServer = requireServer,
                        requireOwnership = false,
                        excludeOwner = false,
                        excludeSender = false,
                        isStatic = method.IsStatic,
                        asyncTimeoutInSec = asyncTimeoutInSec
                    };
                    rpcCount++;
                }
            }
            
            switch (rpcCount)
            {
                case 0:
                    return null;
                case > 1:
                    Error(messages, "Method cannot have multiple RPC attributes", method);
                    return null;
                default: return data;
            }
        }
        
        public static void Error(ICollection<DiagnosticMessage> messages, string message, MethodDefinition method)
        {
            if (method.DebugInformation.HasSequencePoints)
            {
                var first = method.DebugInformation.SequencePoints[0];
                string file = first.Document.Url;
                if (!string.IsNullOrEmpty(file))
                    file = '/' + file[file.IndexOf("Assets", StringComparison.Ordinal)..].Replace('\\', '/');
                else file = string.Empty;
                
                messages.Add(new DiagnosticMessage
                {
                    DiagnosticType = DiagnosticType.Error,
                    MessageData = message,
                    Column = first.StartColumn,
                    Line = first.StartLine,
                    File = file
                });
            }
            else
            {
                messages.Add(new DiagnosticMessage
                {
                    DiagnosticType = DiagnosticType.Error,
                    MessageData = $"[{method.DeclaringType.FullName}] {message}"
                });
            }
        }
        
        static bool ShouldIgnore(RPCType rpcType, ParameterReference param, int index, int count, out SpecialParamType type)
        {
            if (index == count - 1 && param.ParameterType.FullName == typeof(RPCInfo).FullName)
            {
                type = SpecialParamType.RPCInfo;
                return true;
            }

            if (index == 0 && rpcType == RPCType.TargetRPC && param.ParameterType.FullName == typeof(PlayerID).FullName)
            {
                type = SpecialParamType.SenderId;
                return true;
            }

            type = default;
            return false;
        }
        
        private static void HandleRPCReceiver(ModuleDefinition module, TypeDefinition type, IReadOnlyList<RPCMethod> originalRpcs, bool isNetworkClass, int offset)
        {
            for (var i = 0; i < originalRpcs.Count; i++)
            {
                var attributes = MethodAttributes.Private | MethodAttributes.HideBySig;
                
                if (originalRpcs[i].Signature.isStatic)
                    attributes |= MethodAttributes.Static;
                
                bool isValidReturn = ValidateReturnType(originalRpcs[i].originalMethod, out var returnMode);

                if (!isValidReturn)
                    continue;
                
                var voidType = module.TypeSystem.Void;
                var newMethod = new MethodDefinition($"HandleRPCGenerated_{offset + i}", attributes, voidType);
                
                var preserveAttribute = module.GetTypeDefinition<PreserveAttribute>();
                var constructor = preserveAttribute.Resolve().Methods.First(m => m.IsConstructor && !m.HasParameters).Import(module);
                newMethod.CustomAttributes.Add(new CustomAttribute(constructor));
                
                var streamType = module.GetTypeDefinition<NetworkStream>();

                var packetType = originalRpcs[i].Signature.isStatic ? module.GetTypeDefinition<StaticRPCPacket>() : 
                    isNetworkClass ? module.GetTypeDefinition<ChildRPCPacket>() : module.GetTypeDefinition<RPCPacket>();
                
                var rpcInfo = module.GetTypeDefinition<RPCInfo>();
                
                var stream = new ParameterDefinition("stream", ParameterAttributes.None, streamType.Import(module));
                var packet = new ParameterDefinition("packet", ParameterAttributes.None, packetType.Import(module));
                var info = new ParameterDefinition("info", ParameterAttributes.None, rpcInfo.Import(module));
                var asServer = new ParameterDefinition("asServer", ParameterAttributes.None, module.TypeSystem.Boolean);
                
                newMethod.Parameters.Add(stream);
                newMethod.Parameters.Add(packet);
                newMethod.Parameters.Add(info);
                newMethod.Parameters.Add(asServer);
                newMethod.Body.InitLocals = true;
                
                var code = newMethod.Body.GetILProcessor();
                var end = Instruction.Create(OpCodes.Ret);
                
                ValidateReceivingRPC(module, isNetworkClass, originalRpcs[i], code, info, packet, asServer, end);

                try
                {
                    if (originalRpcs[i].originalMethod.HasGenericParameters)
                         HandleGenericRPCReceiver(module, originalRpcs[i], newMethod, stream, info, isNetworkClass);
                    else HandleNonGenericRPCReceiver(module, originalRpcs[i], newMethod, stream, info, returnMode, isNetworkClass);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to handle RPC: {e.Message}\n{e.StackTrace}");
                }

                code.Append(end);
                type.Methods.Add(newMethod);
            }
        }

        private static void ValidateReceivingRPC(ModuleDefinition module, bool isNetworkClass, RPCMethod originalRpc, ILProcessor code, ParameterDefinition info, ParameterDefinition data, ParameterDefinition asServer, Instruction end)
        {
            var compileTimeSignatureField = info.ParameterType.GetField("compileTimeSignature").Import(module);
            
            code.Append(Instruction.Create(OpCodes.Ldarga, info));
            ReturnRPCSignature(module, code, originalRpc, true);
            code.Append(Instruction.Create(OpCodes.Stfld, compileTimeSignatureField));
            
            MethodReference validateReceivingRPC;
            if (originalRpc.Signature.isStatic)
            {
                var rpcModule = module.GetTypeDefinition<RPCModule>();
                validateReceivingRPC = rpcModule.GetMethod("ValidateReceivingStaticRPC").Import(module);
            }
            else if (isNetworkClass)
            {
                var nclass = module.GetTypeDefinition<NetworkModule>();
                validateReceivingRPC = nclass.GetMethod("ValidateReceivingRPC").Import(module);

                // Call validateReceivingRPC(this, RPCInfo, RPCSignature, asServer)
                code.Append(Instruction.Create(OpCodes.Ldarg_0)); // this
            }
            else
            {
                var identityType = module.GetTypeDefinition<NetworkIdentity>();
                validateReceivingRPC = identityType.GetMethod("ValidateReceivingRPC").Import(module);

                // Call validateReceivingRPC(this, RPCInfo, RPCSignature, asServer)
                code.Append(Instruction.Create(OpCodes.Ldarg_0)); // this
            }
            
            // RPCInfo info, RPCSignature signature, INetworkedData data, bool asServer
            code.Append(Instruction.Create(OpCodes.Ldarg, info)); // info
            
            code.Append(Instruction.Create(OpCodes.Ldarga, info));
            code.Append(Instruction.Create(OpCodes.Ldfld, compileTimeSignatureField));
            
            code.Append(Instruction.Create(OpCodes.Ldarg, data)); // data
            code.Append(Instruction.Create(OpCodes.Box, data.ParameterType));
            code.Append(Instruction.Create(OpCodes.Ldarg, asServer)); // asServer
            code.Append(Instruction.Create(OpCodes.Call, validateReceivingRPC));

            // if returned false, return early
            code.Append(Instruction.Create(OpCodes.Brfalse, end));
        }

        private static void HandleNonGenericRPCReceiver(
            ModuleDefinition module,
            RPCMethod rpcMethod, 
            MethodDefinition newMethod,
            ParameterDefinition stream,
            ParameterDefinition info, 
            ReturnMode returnMode,
            bool isNetworkClass)
        {
            var code = newMethod.Body.GetILProcessor();
            var originalMethod = rpcMethod.originalMethod;
            int paramCount = originalMethod.Parameters.Count;

            var managerType = module.GetTypeDefinition<NetworkManager>();
            var streamType = module.GetTypeDefinition<NetworkStream>();
            var networkModule = module.GetTypeDefinition<NetworkModule>();
            var identityType = module.GetTypeDefinition<NetworkIdentity>();
            var rpcReqRespType = module.GetTypeDefinition<RpcRequestResponseModule>();

            var serializeMethod = streamType.GetMethod("Serialize", true).Import(module);
            
            var rpcModule = originalMethod.DeclaringType.Module.GetTypeDefinition<RPCModule>();
            var getLocalPlayer = rpcModule.GetMethod("GetLocalPlayer");
            var responder = rpcReqRespType.GetMethod("CompleteRequestWithResponse", true).Import(module);
            var responderUniTask = rpcReqRespType.GetMethod("CompleteRequestWithUniTask", true).Import(module);
            var responderWithoutResponse = rpcReqRespType.GetMethod("CompleteRequestWithEmptyResponse").Import(module);
            var responderCoroutine = rpcReqRespType.GetMethod("CompleteRequestWithCoroutine").Import(module);
            var responderUniTaskWithoutResponse = rpcReqRespType.GetMethod("CompleteRequestWithUniTaskEmptyResponse").Import(module);
            
            var localPlayerProp = identityType.GetProperty("localPlayerForced");
            var localPlayerGetter = localPlayerProp.GetMethod.Import(module);
            
            var localPlayerPropModule = networkModule.GetProperty("localPlayerForced");
            var localPlayerGetterModule = localPlayerPropModule.GetMethod.Import(module);
            
            var networkManagerProp = identityType.GetProperty("networkManager");
            var getNetworkManager = networkManagerProp.GetMethod.Import(module);
            
            var networkManagerModuleProp = networkModule.GetProperty("networkManager");
            var getNetworkManagerModule = networkManagerModuleProp.GetMethod.Import(module);
            
            var mainManagerProp = managerType.GetProperty("main");
            var mainManagerGetter = mainManagerProp.GetMethod.Import(module);

            VariableDefinition reqId = null;

            if (returnMode != ReturnMode.Void)
            {
                reqId = new VariableDefinition(module.TypeSystem.UInt32);
                newMethod.Body.Variables.Add(reqId);
                
                var serializeUint = new GenericInstanceMethod(serializeMethod);
                serializeUint.GenericArguments.Add(module.TypeSystem.UInt32);
                
                code.Append(Instruction.Create(OpCodes.Ldarga, stream));
                code.Append(Instruction.Create(OpCodes.Ldloca, reqId));
                code.Append(Instruction.Create(OpCodes.Call, serializeUint));
            }
            
            for (var p = 0; p < originalMethod.Parameters.Count; p++)
            {
                var param = originalMethod.Parameters[p];
                var variable = new VariableDefinition(param.ParameterType);
                newMethod.Body.Variables.Add(variable);

                if (ShouldIgnore(rpcMethod.Signature.type, param, p, paramCount, out var specialType))
                {
                    switch (specialType)
                    {
                        case SpecialParamType.RPCInfo:
                            code.Append(Instruction.Create(OpCodes.Ldarg, info));
                            code.Append(Instruction.Create(OpCodes.Stloc, variable));
                            break;
                        case SpecialParamType.SenderId:
                            if (!rpcMethod.Signature.isStatic)
                            {
                                code.Append(Instruction.Create(OpCodes.Ldarg_0));
                                code.Append(isNetworkClass
                                    ? Instruction.Create(OpCodes.Call, localPlayerGetterModule)
                                    : Instruction.Create(OpCodes.Call, localPlayerGetter));
                            }
                            else
                            {
                                code.Append(Instruction.Create(OpCodes.Call, getLocalPlayer));
                            }

                            code.Append(Instruction.Create(OpCodes.Stloc, variable));
                            break;
                    }

                    continue;
                }

                var serialize = new GenericInstanceMethod(serializeMethod);
                serialize.GenericArguments.Add(param.ParameterType);

                code.Append(Instruction.Create(OpCodes.Ldarga, stream));
                code.Append(Instruction.Create(OpCodes.Ldloca, variable));
                code.Append(Instruction.Create(OpCodes.Call, serialize));
            }

            if (!originalMethod.IsStatic)
                code.Append(Instruction.Create(OpCodes.Ldarg_0));

            var vars = newMethod.Body.Variables;

            for (var j = reqId == null ? 0 : 1; j < vars.Count; j++)
            {
                code.Append(Instruction.Create(OpCodes.Ldloc, vars[j]));
            }

            code.Append(Instruction.Create(OpCodes.Call, GetOriginalMethod(originalMethod)));

            if (reqId != null)
            {
                if (returnMode is ReturnMode.Task or ReturnMode.UniTask && originalMethod.ReturnType is GenericInstanceType genericInstance)
                {
                    if (genericInstance.GenericArguments.Count != 1)
                    {
                        code.Append(Instruction.Create(OpCodes.Pop));
                        return;
                    }

                    code.Append(Instruction.Create(OpCodes.Ldarg, info));
                    code.Append(Instruction.Create(OpCodes.Ldloc, reqId));
                    // load networkManager
                    if (newMethod.IsStatic)
                    {
                        code.Append(Instruction.Create(OpCodes.Call, mainManagerGetter));
                    }
                    else
                    {
                        code.Append(Instruction.Create(OpCodes.Ldarg_0));
                        code.Append(isNetworkClass
                            ? Instruction.Create(OpCodes.Call, getNetworkManagerModule)
                            : Instruction.Create(OpCodes.Call, getNetworkManager));
                    }

                    var genericResponse = new GenericInstanceMethod(returnMode is ReturnMode.Task ? responder : responderUniTask);
                    genericResponse.GenericArguments.Add(genericInstance.GenericArguments[0]);
                    code.Append(Instruction.Create(OpCodes.Call, genericResponse));
                }
                else
                {
                    code.Append(Instruction.Create(OpCodes.Ldarg, info));
                    code.Append(Instruction.Create(OpCodes.Ldloc, reqId));
                    
                    // load networkManager
                    if (newMethod.IsStatic)
                    {
                        code.Append(Instruction.Create(OpCodes.Call, mainManagerGetter));
                    }
                    else
                    {
                        code.Append(Instruction.Create(OpCodes.Ldarg_0));
                        code.Append(isNetworkClass
                            ? Instruction.Create(OpCodes.Call, getNetworkManagerModule)
                            : Instruction.Create(OpCodes.Call, getNetworkManager));
                    }
                    
                    code.Append(returnMode switch
                    {
                        ReturnMode.IEnumerator => Instruction.Create(OpCodes.Call, responderCoroutine),
                        ReturnMode.UniTask => Instruction.Create(OpCodes.Call, responderUniTaskWithoutResponse),
                        ReturnMode.Task => Instruction.Create(OpCodes.Call, responderWithoutResponse),
                        _ => throw new ArgumentOutOfRangeException()
                    });
                }
            }
        }

        private static MethodReference GetOriginalMethod(MethodReference originalMethod)
        {
            if (!originalMethod.DeclaringType.HasGenericParameters)
                return originalMethod;

            var declaringType = new GenericInstanceType(originalMethod.DeclaringType);

            foreach (var t in originalMethod.DeclaringType.GenericParameters)
                declaringType.GenericArguments.Add(t);

            var methodToCall = new MethodReference(
                originalMethod.Name,
                originalMethod.ReturnType,
                declaringType)
            {
                HasThis = originalMethod.HasThis,
                ExplicitThis = originalMethod.ExplicitThis,
                CallingConvention = originalMethod.CallingConvention
            };

            foreach (var parameter in originalMethod.Parameters)
            {
                methodToCall.Parameters.Add(new ParameterDefinition(parameter.Name, parameter.Attributes,
                    parameter.ParameterType));
            }

            foreach (var parameter in originalMethod.GenericParameters)
            {
                methodToCall.GenericParameters.Add(new GenericParameter(parameter.Name, parameter.Owner));
            }

            return methodToCall;
        }

        private static void HandleGenericRPCReceiver(ModuleDefinition module, RPCMethod rpcMethod, MethodDefinition newMethod,
            ParameterDefinition stream, ParameterDefinition info, bool isNetworkClass)
        {
            var streamType = module.GetTypeDefinition<NetworkStream>();
            var identityType = module.GetTypeDefinition<NetworkIdentity>();
            var rpcReqRespType = module.GetTypeDefinition<RpcRequestResponseModule>();
            var managerType = module.GetTypeDefinition<NetworkManager>();
            var networkModule = module.GetTypeDefinition<NetworkModule>();
            
            var responderWithoutResponse = rpcReqRespType.GetMethod("CompleteRequestWithEmptyResponse").Import(module);
            var responderCoroutine = rpcReqRespType.GetMethod("CompleteRequestWithCoroutine").Import(module);
            var responderUniTaskWithoutResponse = rpcReqRespType.GetMethod("CompleteRequestWithUniTaskEmptyResponse").Import(module);
            
            var responderTask = rpcReqRespType.GetMethod("CompleteRequestWithResponseObject", true).Import(module);
            var responderUniTask = rpcReqRespType.GetMethod("CompleteRequestWithUniTaskObject", true).Import(module);
            
            var localPlayerProp = identityType.GetProperty("localPlayerForced");
            var localPlayerGetter = localPlayerProp.GetMethod.Import(module);
            
            var genericRpcHeaderType = module.GetTypeDefinition<GenericRPCHeader>();
            var code = newMethod.Body.GetILProcessor();
            var readHeaderMethod = identityType.GetMethod("ReadGenericHeader").Import(module);

            var setInfo = genericRpcHeaderType.GetMethod("SetInfo").Import(module);
            var readGeneric = genericRpcHeaderType.GetMethod("Read").Import(module);
            var readT = genericRpcHeaderType.GetMethod("Read", true).Import(module);
            var setPlayerId = genericRpcHeaderType.GetMethod("SetPlayerId").Import(module);
            var mainManagerProp = managerType.GetProperty("main");
            var mainManagerGetter = mainManagerProp.GetMethod.Import(module);
            
            var getNetworkManagerModule = networkModule.GetProperty("networkManager").GetMethod.Import(module);
            var getNetworkManager = identityType.GetProperty("networkManager").GetMethod.Import(module);
            
            var rpcModule = module.GetTypeDefinition<RPCModule>();
            var nclassType = module.GetTypeDefinition<NetworkModule>();

            var callGenericMethod = rpcMethod.Signature.isStatic ? 
                rpcModule.GetMethod("CallStaticGeneric").Import(module) :
                isNetworkClass ? nclassType.GetMethod("CallGeneric").Import(module) :
                identityType.GetMethod("CallGeneric").Import(module);

            var originalMethod = rpcMethod.originalMethod;
            int paramCount = originalMethod.Parameters.Count;

            var serializeMethod = streamType.GetMethod("Serialize", true).Import(module);
            int genericParamCount = originalMethod.GenericParameters.Count;

            VariableDefinition requestId = null;
            var headerValue = new VariableDefinition(genericRpcHeaderType.Import(module));
            newMethod.Body.Variables.Add(headerValue);
            
            bool isValidReturn = ValidateReturnType(originalMethod, out var returnMode);

            var serializeUint = new GenericInstanceMethod(serializeMethod);
            serializeUint.GenericArguments.Add(module.TypeSystem.UInt32);
            
            if (!isValidReturn)
            {
                return;
            }
            
            if (returnMode != ReturnMode.Void)
            {
                requestId = new VariableDefinition(module.TypeSystem.UInt32);
                newMethod.Body.Variables.Add(requestId);
                
                code.Append(Instruction.Create(OpCodes.Ldarga, stream));
                code.Append(Instruction.Create(OpCodes.Ldloca, requestId));
                code.Append(Instruction.Create(OpCodes.Call, serializeUint));
            }

            // read header value
            code.Append(Instruction.Create(OpCodes.Ldarg, stream));
            code.Append(Instruction.Create(OpCodes.Ldarg, info));
            code.Append(Instruction.Create(OpCodes.Ldc_I4, genericParamCount));
            code.Append(Instruction.Create(OpCodes.Ldc_I4, paramCount));
            code.Append(Instruction.Create(OpCodes.Ldloca, headerValue));
            code.Append(Instruction.Create(OpCodes.Call, readHeaderMethod));

            // read generic parameters
            for (var p = 0; p < paramCount; p++)
            {
                var param = originalMethod.Parameters[p];

                if (ShouldIgnore(rpcMethod.Signature.type, param, p, paramCount, out var specialType))
                {
                    switch (specialType)
                    {
                        case SpecialParamType.RPCInfo:
                            code.Append(Instruction.Create(OpCodes.Ldloca, headerValue));
                            code.Append(Instruction.Create(OpCodes.Ldc_I4, p));
                            code.Append(Instruction.Create(OpCodes.Call, setInfo));
                            break;
                        case SpecialParamType.SenderId:
                            code.Append(Instruction.Create(OpCodes.Ldloca, headerValue));

                            if (!rpcMethod.Signature.isStatic)
                            {
                                code.Append(Instruction.Create(OpCodes.Ldarg_0));
                                code.Append(Instruction.Create(OpCodes.Call, localPlayerGetter));
                            }
                            else
                            {
                                var getLocalPlayer = rpcModule.GetMethod("GetLocalPlayer");
                                code.Append(Instruction.Create(OpCodes.Call, getLocalPlayer));
                            }

                            code.Append(Instruction.Create(OpCodes.Ldc_I4, p));
                            code.Append(Instruction.Create(OpCodes.Call, setPlayerId));
                            break;
                    }

                    continue;
                }

                var genericIdx = param.ParameterType.IsGenericParameter ? 
                    originalMethod.GenericParameters.IndexOf((GenericParameter)param.ParameterType) : -1;

                if (genericIdx != -1)
                {
                    code.Append(Instruction.Create(OpCodes.Ldloca, headerValue));
                    code.Append(Instruction.Create(OpCodes.Ldc_I4, genericIdx));
                    code.Append(Instruction.Create(OpCodes.Ldc_I4, p));
                    code.Append(Instruction.Create(OpCodes.Call, readGeneric));
                }
                else
                {
                    var readAny = new GenericInstanceMethod(readT);
                    readAny.GenericArguments.Add(param.ParameterType);

                    code.Append(Instruction.Create(OpCodes.Ldloca, headerValue));
                    code.Append(Instruction.Create(OpCodes.Ldc_I4, p));
                    code.Append(Instruction.Create(OpCodes.Call, readAny));
                }
            }

            // call 'CallGeneric'
            code.Append(!rpcMethod.Signature.isStatic
                ? Instruction.Create(OpCodes.Ldarg_0)
                : Instruction.Create(OpCodes.Ldtoken, originalMethod.DeclaringType));
            
            code.Append(Instruction.Create(OpCodes.Ldstr, originalMethod.Name)); // methodName
            code.Append(Instruction.Create(OpCodes.Ldloc, headerValue)); // rpcHeader
            code.Append(Instruction.Create(OpCodes.Call, callGenericMethod)); // CallGeneric

            if (requestId != null)
            {
                if (returnMode is ReturnMode.Task or ReturnMode.UniTask && originalMethod.ReturnType is GenericInstanceType genericInstance)
                {
                    if (genericInstance.GenericArguments.Count != 1)
                    {
                        code.Append(Instruction.Create(OpCodes.Pop));
                        return;
                    }
                    
                    code.Append(Instruction.Create(OpCodes.Ldarg, info));
                    code.Append(Instruction.Create(OpCodes.Ldloc, requestId));
                    // load networkManager
                    if (newMethod.IsStatic)
                    {
                        code.Append(Instruction.Create(OpCodes.Call, mainManagerGetter));
                    }
                    else
                    {
                        code.Append(Instruction.Create(OpCodes.Ldarg_0));
                        code.Append(isNetworkClass
                            ? Instruction.Create(OpCodes.Call, getNetworkManagerModule)
                            : Instruction.Create(OpCodes.Call, getNetworkManager));
                    }

                    if (returnMode == ReturnMode.Task)
                    {
                        var genericResponse = new GenericInstanceMethod(responderTask);
                        genericResponse.GenericArguments.Add(genericInstance.GenericArguments[0]);
                        code.Append(Instruction.Create(OpCodes.Call, genericResponse));
                    }
                    else
                    {
                        var genericResponse = new GenericInstanceMethod(responderUniTask);
                        genericResponse.GenericArguments.Add(genericInstance.GenericArguments[0]);
                        code.Append(Instruction.Create(OpCodes.Call, genericResponse));
                    }
                }
                else if (originalMethod.ReturnType != module.TypeSystem.Void)
                {
                    code.Append(Instruction.Create(OpCodes.Ldarg, info));
                    code.Append(Instruction.Create(OpCodes.Ldloc, requestId));
                        
                    // load networkManager
                    if (newMethod.IsStatic)
                    {
                        code.Append(Instruction.Create(OpCodes.Call, mainManagerGetter));
                    }
                    else
                    {
                        code.Append(Instruction.Create(OpCodes.Ldarg_0));
                        code.Append(isNetworkClass
                            ? Instruction.Create(OpCodes.Call, getNetworkManagerModule)
                            : Instruction.Create(OpCodes.Call, getNetworkManager));
                    }
                    
                    code.Append(returnMode switch
                    {
                        ReturnMode.IEnumerator => Instruction.Create(OpCodes.Call, responderCoroutine),
                        ReturnMode.UniTask => Instruction.Create(OpCodes.Call, responderUniTaskWithoutResponse),
                        ReturnMode.Task => Instruction.Create(OpCodes.Call, responderWithoutResponse),
                        _ => throw new ArgumentOutOfRangeException()
                    });
                }
            }
            else
            {
                code.Append(Instruction.Create(OpCodes.Pop));
            }
        }
        
        public enum ReturnMode
        {
            Void,
            Task,
            UniTask,
            IEnumerator
        }
        
        private static bool IsGeneric(MethodReference method, Type type)
        {
            // Ensure method has a generic return type
            if (method.ReturnType is GenericInstanceType genericReturnType)
            {
                // Resolve the element type to compare against Task<>
                var resolvedType = genericReturnType.ElementType.Resolve();

                // Check if the resolved type matches Task<>
                return resolvedType != null && resolvedType.FullName == type.FullName;
            }

            return false;
        }
        
        static bool ValidateReturnType(MethodDefinition method, out ReturnMode mode)
        {
            mode = ReturnMode.Void;
            
            if (method.ReturnType.FullName == typeof(void).FullName)
                return true;
            
            bool isIEnumerator = method.ReturnType.FullName == typeof(IEnumerator).FullName;
            
            if (isIEnumerator)
            {
                mode = ReturnMode.IEnumerator;
                return true;
            }
            
            bool isTask = method.ReturnType.FullName == typeof(Task).FullName;

            if (isTask)
            {
                mode = ReturnMode.Task;
                return true;
            }
            
            if (IsGeneric(method, typeof(Task<>)))
            {
                mode = ReturnMode.Task;
                return true;
            }
            
            bool isUniTask = method.ReturnType.FullName == typeof(UniTask).FullName;
            
            if (isUniTask)
            {
                mode = ReturnMode.UniTask;
                return true;
            }
            
            if (IsGeneric(method, typeof(UniTask<>)))
            {
                mode = ReturnMode.UniTask;
                return true;
            }
            
            return false;
        }
        
        static bool IsTaskOrInheritsFromTask(TypeReference type)
        {
            if (type.FullName == typeof(Task).FullName)
                return true;
            
            return type.Resolve().BaseType?.FullName == typeof(Task).FullName;
        }
        
        static bool IsConcreteType(TypeReference type, out TypeReference concreteType)
        {
            concreteType = type;

            if (type.ContainsGenericParameter)
                return false;
            
            if (IsTaskOrInheritsFromTask(type) && type is GenericInstanceType genericInstanceType)
            {
                concreteType = genericInstanceType.GenericArguments[0];
                return true;
            }
            
            return true;
        }

        private MethodDefinition HandleRPC(ModuleDefinition module, int id, RPCMethod methodRpc, bool isNetworkClass, HashSet<TypeReference> usedTypes, [UsedImplicitly] List<DiagnosticMessage> messages)
        {
            var method = methodRpc.originalMethod;
            bool isValidReturn = ValidateReturnType(method, out var returnMode);
            
            if (!isValidReturn)
            {
                Error(messages, $"RPC '{method.Name}' RPC must return <b>void</b>, <b>Task</b> or <b>UniTask</b>", method);
                return null;
            }

            if (returnMode == ReturnMode.IEnumerator && methodRpc.Signature.type == RPCType.ObserversRPC)
            {
                Error(messages, $"ObserversRPC '{method.Name}' method cannot return IEnumerator", method);
                return null;
            }
            
            if (returnMode == ReturnMode.Task && methodRpc.Signature.type == RPCType.ObserversRPC)
            {
                Error(messages, $"ObserversRPC '{method.Name}' method cannot return Task", method);
                return null;
            }

            if (IsConcreteType(method.ReturnType, out var concreteType))
                usedTypes.Add(concreteType);
            
            string ogName = method.Name;
            method.Name = ogName + "_Original_" + id;
            
            var attributes = MethodAttributes.Public | MethodAttributes.HideBySig;
            
            if (methodRpc.Signature.isStatic)
                attributes |= MethodAttributes.Static;

            var newMethod = new MethodDefinition(ogName, attributes, method.ReturnType);
            
            foreach (var t in method.GenericParameters)
                newMethod.GenericParameters.Add(new GenericParameter(t.Name, newMethod));

            foreach (var param in method.CustomAttributes)
                newMethod.CustomAttributes.Add(param);

            newMethod.CallingConvention = method.CallingConvention;
            method.CustomAttributes.Clear();

            foreach (var param in method.Parameters)
            {
                if (IsConcreteType(param.ParameterType, out var concreteParam))
                    usedTypes.Add(concreteParam);
                
                newMethod.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
            }
            
            var code = newMethod.Body.GetILProcessor();
             
            var streamType = module.GetTypeDefinition<NetworkStream>();
            var rpcType = module.GetTypeDefinition<RPCModule>();
            var identityType = module.GetTypeDefinition<NetworkIdentity>();
            var moduleType = module.GetTypeDefinition<NetworkModule>();
            var hahserType = module.GetTypeDefinition<Hasher>();
            var rpcRequestType = module.GetTypeDefinition<RpcRequest>();
            var rpcSignatureType = module.GetTypeDefinition<RPCSignature>();
            var reqRespModule = module.GetTypeDefinition<RpcRequestResponseModule>();
            
            var allocStreamMethod = rpcType.GetMethod("AllocStream").Import(module);
            var serializeMethod = streamType.GetMethod("Serialize", true).Import(module);
            var freeStreamMethod = rpcType.GetMethod("FreeStream").Import(module);
            
            var getId = identityType.GetProperty("id");
            var getSceneId = identityType.GetProperty("sceneId");
            var getStableHashU32 = hahserType.GetMethod("GetStableHashU32", true).Import(module);
            var getParent = moduleType.GetProperty("parent").GetMethod.Import(module);
            var targetPlayerField = rpcSignatureType.GetField("targetPlayer").Import(module);
            
            var getNextId = identityType.GetMethod("GetNextId", true).Import(module);
            var getNextIdNonGeneric = identityType.GetMethod("GetNextId").Import(module);
            var getNextIdStatic = reqRespModule.GetMethod("GetNextIdStatic", true).Import(module);
            var getNextIdStaticNonGeneric = reqRespModule.GetMethod("GetNextIdStatic").Import(module);
            var getNextIdUniTaskNonGeneric = identityType.GetMethod("GetNextIdUniTask").Import(module);
            var getNextIdUniTask = identityType.GetMethod("GetNextIdUniTask", true).Import(module);

            var getNextIdUniTaskStatic = reqRespModule.GetMethod("GetNextIdUniTaskStatic", true).Import(module);
            var getNextIdUniTaskStaticNonGeneric = reqRespModule.GetMethod("GetNextIdUniTaskStatic").Import(module);
            
            var waitForTask = reqRespModule.GetMethod("WaitForTask").Import(module);
            
            var reqIdField = rpcRequestType.GetField("id").Import(module);
            
            // Declare local variables
            newMethod.Body.InitLocals = true;
            
            var packetType = methodRpc.Signature.isStatic ? module.GetTypeDefinition<StaticRPCPacket>() : 
                isNetworkClass ? module.GetTypeDefinition<ChildRPCPacket>() : module.GetTypeDefinition<RPCPacket>();
            
            var streamVariable = new VariableDefinition(streamType.Import(module));
            var rpcDataVariable = new VariableDefinition(packetType.Import(module));
            var typeHash = new VariableDefinition(module.TypeSystem.UInt32);
            var rpcRequest = new VariableDefinition(rpcRequestType.Import(module));
            var taskWithReturnType = new VariableDefinition(newMethod.ReturnType);
            var rpcSignature = new VariableDefinition(rpcSignatureType.Import(module));

            if (returnMode != ReturnMode.Void)
            {
                newMethod.Body.Variables.Add(taskWithReturnType);
                newMethod.Body.Variables.Add(rpcRequest);
            }

            newMethod.Body.Variables.Add(streamVariable);
            newMethod.Body.Variables.Add(rpcDataVariable);
            newMethod.Body.Variables.Add(typeHash);
            newMethod.Body.Variables.Add(rpcSignature);

            var paramCount = newMethod.Parameters.Count;
            
            ReturnRPCSignature(module, code, methodRpc, false);
            code.Append(Instruction.Create(OpCodes.Stloc, rpcSignature));
            
            if (returnMode != ReturnMode.Void)
            {
                // Task<bool> nextId = GetNextId<bool>(base.networkManager.localClientConnection, 5f, out request);

                if (methodRpc.Signature.isStatic)
                {
                    var getNextIdRef = returnMode == ReturnMode.UniTask ?
                        getNextIdUniTaskStaticNonGeneric : 
                        getNextIdStaticNonGeneric;
                    
                    if (returnMode is ReturnMode.Task or ReturnMode.UniTask && newMethod.ReturnType is GenericInstanceType genericInstance)
                    {
                        if (genericInstance.GenericArguments.Count != 1)
                        {
                            Error(messages, "Task must have a single generic argument", method);
                            return null;
                        }

                        var param = genericInstance.GenericArguments[0];

                        if (returnMode == ReturnMode.Task)
                        {
                            var newGetNextId = new GenericInstanceMethod(getNextIdStatic);
                            newGetNextId.GenericArguments.Add(param);
                            getNextIdRef = newGetNextId;
                        }
                        else
                        {
                            var newGetNextId = new GenericInstanceMethod(getNextIdUniTaskStatic);
                            newGetNextId.GenericArguments.Add(param);
                            getNextIdRef = newGetNextId;
                        }
                    }

                    // get targetPlayerField
                    code.Append(Instruction.Create(OpCodes.Ldloca, rpcSignature));
                    code.Append(Instruction.Create(OpCodes.Ldfld, targetPlayerField));
                    
                    code.Append(Instruction.Create(OpCodes.Ldc_I4, (int)methodRpc.Signature.type));
                    code.Append(Instruction.Create(OpCodes.Ldc_R4, methodRpc.Signature.asyncTimeoutInSec)); // timeout
                    code.Append(Instruction.Create(OpCodes.Ldloca, rpcRequest)); // out request
                    code.Append(Instruction.Create(OpCodes.Call, getNextIdRef)); // GetNextIdStatic
                }
                else
                {
                    code.Append(Instruction.Create(OpCodes.Ldarg_0)); // this
                    
                    if (isNetworkClass)
                        code.Append(Instruction.Create(OpCodes.Call, getParent)); // parent

                    // this
                    code.Append(Instruction.Create(OpCodes.Ldc_I4, (int)methodRpc.Signature.type));

                    // get targetPlayerField
                    code.Append(Instruction.Create(OpCodes.Ldloca, rpcSignature));
                    code.Append(Instruction.Create(OpCodes.Ldfld, targetPlayerField));
                    
                    // localClientConnection
                    code.Append(Instruction.Create(OpCodes.Ldc_R4, methodRpc.Signature.asyncTimeoutInSec)); // timeout
                    code.Append(Instruction.Create(OpCodes.Ldloca, rpcRequest)); // out request

                    var getNextIdRef = returnMode == ReturnMode.UniTask ? getNextIdUniTaskNonGeneric : getNextIdNonGeneric;

                    if (returnMode is ReturnMode.Task or ReturnMode.UniTask && newMethod.ReturnType is GenericInstanceType genericInstance)
                    {
                        if (genericInstance.GenericArguments.Count != 1)
                        {
                            Error(messages, "Task must have a single generic argument", method);
                            return null;
                        }

                        var param = genericInstance.GenericArguments[0];

                        if (returnMode == ReturnMode.Task)
                        {
                            var newGetNextId = new GenericInstanceMethod(getNextId);
                            newGetNextId.GenericArguments.Add(param);
                            getNextIdRef = newGetNextId;
                        }
                        else
                        {
                            var newGetNextId = new GenericInstanceMethod(getNextIdUniTask);
                            newGetNextId.GenericArguments.Add(param);
                            getNextIdRef = newGetNextId;
                        }
                    }

                    code.Append(Instruction.Create(OpCodes.Call, getNextIdRef)); // GetNextId
                }

                if (returnMode == ReturnMode.IEnumerator)
                    code.Append(Instruction.Create(OpCodes.Call, waitForTask));
                
                code.Append(Instruction.Create(OpCodes.Stloc, taskWithReturnType)); // taskWithReturnType
            }

            code.Append(Instruction.Create(OpCodes.Ldc_I4, 0));
            code.Append(Instruction.Create(OpCodes.Call, allocStreamMethod));
            code.Append(Instruction.Create(OpCodes.Stloc, streamVariable));

            if (returnMode != ReturnMode.Void)
            {
                var serializeGenericMethod = new GenericInstanceMethod(serializeMethod);
                serializeGenericMethod.GenericArguments.Add(module.TypeSystem.UInt32);
                
                code.Append(Instruction.Create(OpCodes.Ldloca, streamVariable));
                code.Append(Instruction.Create(OpCodes.Ldloca, rpcRequest));
                code.Append(Instruction.Create(OpCodes.Ldflda, reqIdField));
                code.Append(Instruction.Create(OpCodes.Call, serializeGenericMethod));
            }

            for (var i = 0; i < newMethod.GenericParameters.Count; i++)
            {
                var param = newMethod.GenericParameters[i];
                
                var getStableHashU32Generic = new GenericInstanceMethod(getStableHashU32);
                getStableHashU32Generic.GenericArguments.Add(param);
                
                code.Append(Instruction.Create(OpCodes.Call, getStableHashU32Generic));
                code.Append(Instruction.Create(OpCodes.Stloc, typeHash));
                
                var serializeGenericMethod = new GenericInstanceMethod(serializeMethod);
                serializeGenericMethod.GenericArguments.Add(module.TypeSystem.UInt32);
                    
                code.Append(Instruction.Create(OpCodes.Ldloca, streamVariable));
                code.Append(Instruction.Create(OpCodes.Ldloca, typeHash));
                code.Append(Instruction.Create(OpCodes.Call, serializeGenericMethod));
            }

            for (var i = 0; i < paramCount; i++)
            {
                var param = newMethod.Parameters[i];
                
                if (methodRpc.Signature.type == RPCType.TargetRPC && i == 0)
                {
                    if (param.ParameterType.IsGenericParameter || param.ParameterType.FullName != typeof(PlayerID).FullName)
                    {
                        Error(messages, "TargetRPC method must have a 'PlayerID' as the first parameter", method);
                        return null;
                    }
                    continue;
                }

                if (ShouldIgnore(methodRpc.Signature.type, param, i, paramCount, out _))
                    continue;
                
                var serializeGenericMethod = new GenericInstanceMethod(serializeMethod);
                serializeGenericMethod.GenericArguments.Add(param.ParameterType);
                
                code.Append(Instruction.Create(OpCodes.Ldloca, streamVariable));
                code.Append(Instruction.Create(OpCodes.Ldarga, param));
                code.Append(Instruction.Create(OpCodes.Call, serializeGenericMethod));
            }

            if (methodRpc.Signature.isStatic)
            {
                var buildRawRPCMethod = rpcType.GetMethod("BuildStaticRawRPC", true).Import(module);
                var genericInstanceMethod = new GenericInstanceMethod(buildRawRPCMethod);
                genericInstanceMethod.GenericArguments.Add(method.DeclaringType);

                // rpcId, stream
                code.Append(Instruction.Create(OpCodes.Ldc_I4, id));
                code.Append(Instruction.Create(OpCodes.Ldloc, streamVariable));
                
                // BuildStaticRawRPC(int rpcId, NetworkStream stream)
                code.Append(Instruction.Create(OpCodes.Call, genericInstanceMethod));
            }
            else if (isNetworkClass)
            {
                var buildChildRpc = module.GetTypeDefinition<NetworkModule>().GetMethod("BuildRPC").Import(module);
                
                code.Append(Instruction.Create(OpCodes.Ldarg_0));
                code.Append(Instruction.Create(OpCodes.Ldc_I4, id));
                code.Append(Instruction.Create(OpCodes.Ldloc, streamVariable));
                code.Append(Instruction.Create(OpCodes.Call, buildChildRpc));
            }
            else
            {
                var buildRawRPCMethod = rpcType.GetMethod("BuildRawRPC").Import(module);

                code.Append(Instruction.Create(OpCodes.Ldarg_0));
                code.Append(Instruction.Create(OpCodes.Call, getId.GetMethod.Import(module))); // id
                code.Append(Instruction.Create(OpCodes.Ldarg_0));
                code.Append(Instruction.Create(OpCodes.Call, getSceneId.GetMethod.Import(module))); // sceneId
                code.Append(Instruction.Create(OpCodes.Ldc_I4, id)); // rpcId
                code.Append(Instruction.Create(OpCodes.Ldloc, streamVariable)); // stream

                // BuildRawRPC(int networkId, SceneID sceneId, byte rpcId, NetworkStream stream, RPCDetails details)
                code.Append(Instruction.Create(OpCodes.Call, buildRawRPCMethod));
            }

            code.Append(Instruction.Create(OpCodes.Stloc, rpcDataVariable)); // rpcPacket

            if (!methodRpc.Signature.isStatic)
                code.Append(Instruction.Create(OpCodes.Ldarg_0)); // this
            
            code.Append(Instruction.Create(OpCodes.Ldloc, rpcDataVariable)); // rpcPacket
            code.Append(Instruction.Create(OpCodes.Ldloc, rpcSignature)); // rpcDetails

            if (methodRpc.Signature.isStatic)
            {
                var sendRpc = rpcType.GetMethod("SendStaticRPC").Import(module);
                code.Append(Instruction.Create(OpCodes.Call, sendRpc));
            }
            else if (isNetworkClass)
            {
                var sendRpc = module.GetTypeDefinition<NetworkModule>().GetMethod("SendRPC").Import(module);
                code.Append(Instruction.Create(OpCodes.Call, sendRpc));
            }
            else
            {
                var sendRpc = identityType.GetMethod("SendRPC").Import(module);
                code.Append(Instruction.Create(OpCodes.Call, sendRpc));
            }
            
            code.Append(Instruction.Create(OpCodes.Ldloc, streamVariable));
            code.Append(Instruction.Create(OpCodes.Call, freeStreamMethod));
            
            var endOfRunLocallyCheck = Instruction.Create(OpCodes.Nop);
            
            code.Append(Instruction.Create(OpCodes.Ldloc, rpcSignature));
            code.Append(Instruction.Create(OpCodes.Ldfld, module.GetTypeDefinition<RPCSignature>().GetField("runLocally").Import(module)));
            code.Append(Instruction.Create(OpCodes.Brfalse, endOfRunLocallyCheck));
            
            var callMethod = GetOriginalMethod(method);
            
            if (method.HasGenericParameters)
            {
                var genericInstanceMethod = new GenericInstanceMethod(callMethod);

                for (var i = 0; i < method.GenericParameters.Count; i++)
                {
                    var gp = method.GenericParameters[i];
                    genericInstanceMethod.GenericArguments.Add(gp);
                }

                callMethod = genericInstanceMethod;
            }

            if (!methodRpc.Signature.isStatic)
                code.Append(Instruction.Create(OpCodes.Ldarg_0)); // this

            for (int i = 0; i < paramCount; ++i)
            {
                var param = newMethod.Parameters[i];
                code.Append(Instruction.Create(OpCodes.Ldarg, param)); // param
            }

            code.Append(Instruction.Create(OpCodes.Call, callMethod)); // Call original method
            
            // Pop return value if not void for now
            code.Append(Instruction.Create(OpCodes.Ret));
            code.Append(endOfRunLocallyCheck);
            
            if (returnMode != ReturnMode.Void)
            {
                code.Append(Instruction.Create(OpCodes.Ldloc, taskWithReturnType));
                code.Append(Instruction.Create(OpCodes.Ret));
            }

            code.Append(Instruction.Create(OpCodes.Ret));

            return newMethod;
        }

        private static void ReturnRPCSignature(ModuleDefinition module, ILProcessor code, RPCMethod rpc, bool isReceiving)
        {
            var rpcDetails = module.GetTypeDefinition<RPCSignature>();
            var makeRpcDetails = rpcDetails.GetMethod("Make").Import(module);
            var makeRpcDetailsTarget = rpcDetails.GetMethod("MakeWithTarget").Import(module);
            
            // RPCDetails Make(RPCType type, Channel channel, bool runLocally, bool bufferLast, bool requireServer, bool excludeOwner)
            code.Append(Instruction.Create(OpCodes.Ldc_I4, (int)rpc.Signature.type));
            code.Append(Instruction.Create(OpCodes.Ldc_I4, (int)rpc.Signature.channel));
            code.Append(Instruction.Create(OpCodes.Ldc_I4, rpc.Signature.runLocally ? 1 : 0));
            code.Append(Instruction.Create(OpCodes.Ldc_I4, rpc.Signature.requireOwnership ? 1 : 0));
            code.Append(Instruction.Create(OpCodes.Ldc_I4, rpc.Signature.bufferLast ? 1 : 0));
            code.Append(Instruction.Create(OpCodes.Ldc_I4, rpc.Signature.requireServer ? 1 : 0));
            code.Append(Instruction.Create(OpCodes.Ldc_I4, rpc.Signature.excludeOwner ? 1 : 0));
            code.Append(Instruction.Create(OpCodes.Ldstr, rpc.ogName));
            code.Append(Instruction.Create(OpCodes.Ldc_I4, rpc.Signature.isStatic ? 1 : 0));
            code.Append(Instruction.Create(OpCodes.Ldc_R4, rpc.Signature.asyncTimeoutInSec));
            code.Append(Instruction.Create(OpCodes.Ldc_I4, rpc.Signature.excludeSender ? 1 : 0));

            if (rpc.Signature.type == RPCType.TargetRPC)
            {
                if (!isReceiving)
                {
                    code.Append(rpc.Signature.isStatic
                        ? Instruction.Create(OpCodes.Ldarg_0)
                        : Instruction.Create(OpCodes.Ldarg_1));
                }
                else
                {
                    if (!rpc.Signature.isStatic)
                    {
                        var localPlayerProp = module.GetTypeDefinition<NetworkIdentity>().GetProperty("localPlayerForced").GetMethod.Import(module);

                        code.Append(Instruction.Create(OpCodes.Ldarg_0));
                        code.Append(Instruction.Create(OpCodes.Call, localPlayerProp));
                    }
                    else
                    {
                        var rpcModule = module.GetTypeDefinition<RPCModule>();
                        var getLocalPlayer = rpcModule.GetMethod("GetLocalPlayer");
                        code.Append(Instruction.Create(OpCodes.Call, getLocalPlayer));
                    }
                }
                
                code.Append(Instruction.Create(OpCodes.Call, makeRpcDetailsTarget));
            }
            else
            {
                code.Append(Instruction.Create(OpCodes.Call, makeRpcDetails));
            }
        }

        private static bool UpdateMethodReferences(ModuleDefinition module, MethodReference old, MethodReference @new, [UsedImplicitly] List<DiagnosticMessage> messages)
        {
            List<TypeDefinition> types = new();
            
            var startLocalExecutionFlag = module.GetTypeDefinition(typeof(PurrCompilerFlags)).GetMethod("EnterLocalExecution").FullName;
            var exitLocalExecutionFlag = module.GetTypeDefinition(typeof(PurrCompilerFlags)).GetMethod("ExitLocalExecution").FullName;
            
            types.AddRange(module.Types);
            foreach (var type in module.Types)
            {
                types.AddRange(type.NestedTypes);
            }
            
            bool isSkipping = false;

            for (var tidx = 0; tidx < types.Count; tidx++)
            {
                var type = types[tidx];
                foreach (var method in type.Methods)
                {
                    if (method == @new || method.GetElementMethod() == @new) continue;

                    if (method.Body == null) continue;

                    var processor = method.Body.GetILProcessor();

                    for (var i = 0; i < method.Body.Instructions.Count; i++)
                    {
                        var instruction = method.Body.Instructions[i];
                        
                        if (instruction.OpCode == OpCodes.Call && instruction.Operand is MethodReference flag)
                        {
                            if (flag.FullName == startLocalExecutionFlag)
                            {
                                processor.Replace(instruction, Instruction.Create(OpCodes.Nop));
                                if (isSkipping)
                                {
                                    Error(messages, "Local mode flag was already set, avoid nesting these flags.", method);
                                    return false;
                                }
                                isSkipping = true;
                                continue;
                            }
                            
                            if (flag.FullName == exitLocalExecutionFlag)
                            {
                                processor.Replace(instruction, Instruction.Create(OpCodes.Nop));
                                if (!isSkipping)
                                {
                                    Error(messages, "Local mode flag was not set, you should first call <b>PurrCompilerFlags.EnterLocalExecution()</b>", method);
                                    return false;
                                }
                                isSkipping = false;
                                continue;
                            }
                        }
                        
                        if (isSkipping)
                            continue;

                        if (instruction.Operand is MethodReference methodReference &&
                            methodReference.GetElementMethod() == old)
                        {
                            var newRef = GenerateNewRef(@new, methodReference);
                            processor.Replace(instruction, Instruction.Create(instruction.OpCode, newRef));
                        }
                    }

                    if (isSkipping)
                    {
                        Error(messages, "Local mode flag was not unset, you should call <b>PurrCompilerFlags.ExitLocalExecution()</b>", method);
                        return false;
                    }
                }
            }

            return true;
        }
        
        private static MethodReference GenerateNewRef(MethodReference @new, MethodReference methodReference)
        {
            // Check if methodReference is a MethodDefinition for a deeper copy if possible
            var methodDefinition = methodReference.Resolve();

            // Start with a MethodReference pointing to the new definition, copying name and return type
            var newRef = new MethodReference(@new.Name, @new.ReturnType, @new.DeclaringType)
            {
                HasThis = methodReference.HasThis,
                ExplicitThis = methodReference.ExplicitThis,
                CallingConvention = methodReference.CallingConvention,
            };

            // Clone parameters with exact types and attributes
            foreach (var parameter in methodDefinition.Parameters)
            {
                var newParameterType = parameter.ParameterType;
                if (newParameterType is GenericParameter && newRef.GenericParameters.Count > 0)
                {
                    // Ensure matching GenericParameter
                    var matchedParameter = newRef.GenericParameters.FirstOrDefault(p => p.Name == newParameterType.Name);
                    if (matchedParameter != null) newParameterType = matchedParameter;
                }
                newRef.Parameters.Add(new ParameterDefinition(parameter.Name, parameter.Attributes, newParameterType));
            }

            // Handle generic parameters exactly as defined in the MethodDefinition
            foreach (var genericParameter in methodDefinition.GenericParameters)
            {
                var newGenericParameter = new GenericParameter(genericParameter.Name, newRef);
                newRef.GenericParameters.Add(newGenericParameter);
            }

            // If the methodReference is a GenericInstanceMethod, convert newRef to match
            if (methodReference is GenericInstanceMethod ogGenericMethodRef)
            {
                var newGenericInstanceMethod = new GenericInstanceMethod(newRef);

                // Match each generic argument exactly
                foreach (var argument in ogGenericMethodRef.GenericArguments)
                    newGenericInstanceMethod.GenericArguments.Add(argument);

                // Assign the generic instance method back to newRef
                newRef = newGenericInstanceMethod;
            }

            return newRef;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            try
            {
                if (!WillProcess(compiledAssembly))
                    return default!;
                
                HashSet<TypeReference> typesToGenerateSerializer = new();
                
                var messages = new List<DiagnosticMessage>();

                using var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData);
                using var pdbStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData);
                var resolver = new AssemblyResolver(compiledAssembly);
                
                var assemblyDefinition = AssemblyDefinition.ReadAssembly(peStream, new ReaderParameters
                {
                    ReadSymbols = true,
                    SymbolStream = pdbStream,
                    SymbolReaderProvider = new PortablePdbReaderProvider(),
                    AssemblyResolver = resolver
                });
                
                resolver.SetSelf(assemblyDefinition);

                for (var m = 0; m < assemblyDefinition.Modules.Count; m++)
                {
                    var module = assemblyDefinition.Modules[m];

                    for (var t = 0; t < module.Types.Count; t++)
                    {
                        RegisterSerializersProcessor.HandleType(module, module.Types[t], messages);
                        
                        var type = module.Types[t];

                        if (!type.IsClass)
                            continue;

                        var idFullName = typeof(NetworkIdentity).FullName;
                        var classFullName = typeof(NetworkModule).FullName;
                        
                        bool inheritsFromNetworkIdentity = type.FullName == idFullName || InheritsFrom(type, idFullName);
                        bool inheritsFromNetworkClass = type.FullName == classFullName || InheritsFrom(type, classFullName);

                        List<RPCMethod> _rpcMethods = new();

                        int idOffset = GetIDOffset(type, messages);

                        if (inheritsFromNetworkIdentity || inheritsFromNetworkClass)
                        {
                            List<FieldDefinition> _networkFields = new();

                            FindNetworkModules(type, classFullName, _networkFields);
                            CreateSyncVarInitMethod(inheritsFromNetworkIdentity, module, type, _networkFields);
                        }

                        for (var i = 0; i < type.Methods.Count; i++)
                        {
                            try
                            {
                                var method = type.Methods[i];

                                if (method.DeclaringType.FullName != type.FullName)
                                    continue;
                                
                                var rpcType = GetMethodRPCType(method, messages);

                                if (rpcType == null)
                                    continue;
                                
                                if (!rpcType.Value.isStatic && !inheritsFromNetworkIdentity && !inheritsFromNetworkClass)
                                {
                                    Error(messages, "RPC must be static if not inheriting from NetworkIdentity or NetworkClass", method);
                                    continue;
                                }
                                
                                _rpcMethods.Add(new RPCMethod
                                {
                                    Signature = rpcType.Value, originalMethod = method, ogName = method.Name
                                });
                            }
                            catch (Exception e)
                            {
                                Error(messages, e.Message + "\n" + e.StackTrace, type.Methods[i]);
                            }
                        }

                        if (!inheritsFromNetworkIdentity && !inheritsFromNetworkClass && _rpcMethods.Count == 0)
                            continue;
                        
                        HashSet<TypeReference> usedTypes = new();

                        for (var index = 0; index < _rpcMethods.Count; index++)
                        {
                            var method = _rpcMethods[index].originalMethod;
                            
                            try
                            {
                                var newMethod = HandleRPC(module, idOffset + index, _rpcMethods[index], inheritsFromNetworkClass, usedTypes, messages);

                                if (newMethod != null)
                                {
                                    type.Methods.Add(newMethod);
                                    if (!UpdateMethodReferences(module, method, newMethod, messages))
                                    {
                                        return new ILPostProcessResult(compiledAssembly.InMemoryAssembly, messages);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Error(messages, e.Message + "\n" + e.StackTrace, method);
                            }
                        }

                        try
                        {
                            if (_rpcMethods.Count > 0)
                            {
                                HandleRPCReceiver(module, type, _rpcMethods, inheritsFromNetworkClass, idOffset);
                            }
                        }
                        catch (Exception e)
                        {
                            messages.Add(new DiagnosticMessage
                            {
                                DiagnosticType = DiagnosticType.Error,
                                MessageData = $"HandleRPCReceiver [{type.Name}]: {e.Message}\n{e.StackTrace}"
                            });
                        }
                        
                        try
                        {
                            FindUsedTypes(module, _rpcMethods, usedTypes);
                            GenerateExecuteFunction(module, type, usedTypes, inheritsFromNetworkIdentity, typesToGenerateSerializer);
                        }
                        catch (Exception e)
                        {
                            messages.Add(new DiagnosticMessage
                            {
                                DiagnosticType = DiagnosticType.Error,
                                MessageData = $"GenerateExecuteFunction [{type.Name}]: {e.Message}\n{e.StackTrace}"
                            });
                        }
                    }
                }

                ExpandNested(assemblyDefinition, typesToGenerateSerializer);
                
                foreach (var typeRef in typesToGenerateSerializer)
                    GenerateSerializersProcessor.HandleType(assemblyDefinition, typeRef, messages);

                var pe = new MemoryStream();
                var pdb = new MemoryStream();

                var writerParameters = new WriterParameters
                {
                    WriteSymbols = true,
                    SymbolStream = pdb,
                    SymbolWriterProvider = new PortablePdbWriterProvider()
                };

                try
                {
                    assemblyDefinition.Write(pe, writerParameters);
                }
                catch (Exception e)
                {
                    messages.Add(new DiagnosticMessage
                    {
                        DiagnosticType = DiagnosticType.Error,
                        MessageData = $"Failed to write assembly ({compiledAssembly.Name}): {e.Message}",
                    });
                }

                return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), messages);
            }
            catch (Exception e)
            {
                
                var messages = new List<DiagnosticMessage> {
                    new()
                    {
                        DiagnosticType = DiagnosticType.Error,
                        MessageData = $"Unhandled exception {e.Message}\n{e.StackTrace}",
                    }
                };
                
                return new ILPostProcessResult(compiledAssembly.InMemoryAssembly, messages);
            }
        }

        private static void FindNetworkModules(TypeDefinition type, string classFullName, List<FieldDefinition> _networkFields)
        {
            for (var i = 0; i < type.Fields.Count; i++)
            {
                var field = type.Fields[i];

                if (field.IsStatic) continue;

                var fieldType = field.FieldType.Resolve();
                
                if (fieldType == null) continue;
                
                var isNetworkClass = fieldType.FullName == classFullName || InheritsFrom(fieldType, classFullName);

                if (!isNetworkClass) continue;
                                
                _networkFields.Add(field);
            }
        }

        private static void ExpandNested(AssemblyDefinition assembly, HashSet<TypeReference> typesToHandle)
        {
            HashSet<TypeReference> visited = new();
            var copy = typesToHandle.ToArray();

            for (var i = 0; i < copy.Length; i++)
            {
                var type = copy[i];
                var resolved = type.Resolve();
                AddNestedTypes(assembly, resolved, typesToHandle, visited);
            }
        }

        private static void AddNestedTypes(AssemblyDefinition assembly, TypeDefinition resolved, HashSet<TypeReference> typesToHandle, HashSet<TypeReference> visited)
        {
            if (resolved == null)
                return;
            
            foreach (var field in resolved.Fields)
            {
                if (!visited.Add(field.FieldType))
                    continue;

                if (field.FieldType.IsGenericInstance)
                {
                    var genericInstance = (GenericInstanceType) field.FieldType;
                    bool containsMyStuff = false;
                    
                    for (int i = 0; i < genericInstance.GenericArguments.Count; i++)
                    {
                        var argument = genericInstance.GenericArguments[i];
                        
                        if (!visited.Add(argument))
                            continue;

                        if (IsTypeInOwnModule(argument, assembly.MainModule))
                        {
                            typesToHandle.Add(argument);
                            containsMyStuff = true;
                        }
                        
                        AddNestedTypes(assembly, argument.Resolve(), typesToHandle, visited);
                    }
                    
                    if (containsMyStuff)
                    {
                        typesToHandle.Add(field.FieldType);
                    }
                }
                else if (IsTypeInOwnModule(field.FieldType, assembly.MainModule))
                {
                    typesToHandle.Add(field.FieldType);
                    AddNestedTypes(assembly, field.FieldType.Resolve(), typesToHandle, visited);
                }
            }
        }

        private static void CreateSyncVarInitMethod(bool isNetworkIdentity, ModuleDefinition module, TypeDefinition type, List<FieldDefinition> networkFields)
        {
            var newMethod = new MethodDefinition($"__{type.Name}_CodeGen_Initialize", 
                MethodAttributes.Public | MethodAttributes.HideBySig, module.TypeSystem.Void);

            var preserveAttribute = module.GetTypeDefinition<PreserveAttribute>();
            var constructor = preserveAttribute.Resolve().Methods.First(m => m.IsConstructor && !m.HasParameters).Import(module);
            newMethod.CustomAttributes.Add(new CustomAttribute(constructor));

            var parentStr = new ParameterDefinition("parent", ParameterAttributes.None, module.TypeSystem.String);
            
            if (!isNetworkIdentity)
                newMethod.Parameters.Add(parentStr);

            type.Methods.Add(newMethod);
            
            newMethod.Body.InitLocals = true;
            
            var code = newMethod.Body.GetILProcessor();

            var parentType =
                (isNetworkIdentity
                    ? module.GetTypeDefinition<NetworkIdentity>()
                    : module.GetTypeDefinition<NetworkModule>()).Import(module);
            
            var registerModule = parentType.GetMethod("RegisterModuleInternal").Import(module);
            var concatMethod = module.TypeSystem.String.Resolve()
                .GetMethod("Concat", module.TypeSystem.String, module.TypeSystem.String).Import(module);

            for (int i = 0; i < networkFields.Count; i++)
            {
                var field = networkFields[i];

                code.Append(Instruction.Create(OpCodes.Ldarg_0));
                code.Append(Instruction.Create(OpCodes.Ldstr, field.Name));
                code.Append(Instruction.Create(OpCodes.Ldstr, field.FieldType.Name));
                code.Append(Instruction.Create(OpCodes.Ldarg_0));
                code.Append(Instruction.Create(OpCodes.Ldfld, field));
                code.Append(Instruction.Create(OpCodes.Ldc_I4, isNetworkIdentity ? 1 : 0));
                code.Append(Instruction.Create(OpCodes.Call, registerModule));

                var endInstruction = Instruction.Create(OpCodes.Nop);
                
                // if not null
                code.Append(Instruction.Create(OpCodes.Ldarg_0));
                code.Append(Instruction.Create(OpCodes.Ldfld, field));
                code.Append(Instruction.Create(OpCodes.Brfalse, endInstruction));

                // call init method
                var initMethodName = $"__{field.FieldType.Name}_CodeGen_Initialize";
                var codeGenInitRef = new MethodReference(initMethodName, module.TypeSystem.Void, field.FieldType)
                {
                    HasThis = true
                };
                
                codeGenInitRef.Parameters.Add(parentStr);
                
                code.Append(Instruction.Create(OpCodes.Ldarg_0));
                code.Append(Instruction.Create(OpCodes.Ldfld, field));
                if (isNetworkIdentity)
                {
                    code.Append(Instruction.Create(OpCodes.Ldstr, field.Name));
                }
                else
                {
                    code.Append(Instruction.Create(OpCodes.Ldarg_1));
                    code.Append(Instruction.Create(OpCodes.Ldstr, '.' + field.Name));
                    code.Append(Instruction.Create(OpCodes.Call, concatMethod));
                }
                
                code.Append(Instruction.Create(OpCodes.Call, codeGenInitRef));
                code.Append(endInstruction);

                if (!isNetworkIdentity)
                {
                    // if null
                    var endInstruction2 = Instruction.Create(OpCodes.Nop);
                    code.Append(Instruction.Create(OpCodes.Ldarg_0));
                    code.Append(Instruction.Create(OpCodes.Ldfld, field));
                    code.Append(Instruction.Create(OpCodes.Brtrue, endInstruction2));

                    // call error
                    var errorMethod = module.GetTypeDefinition<NetworkModule>().GetMethod("Error").Import(module);

                    code.Append(Instruction.Create(OpCodes.Ldarg_0));
                    code.Append(Instruction.Create(OpCodes.Ldarg_1));
                    code.Append(Instruction.Create(OpCodes.Ldstr, '.' + field.Name));
                    code.Append(Instruction.Create(OpCodes.Call, concatMethod));
                    code.Append(Instruction.Create(OpCodes.Call, errorMethod));

                    code.Append(endInstruction2);
                }
            }

            code.Append(Instruction.Create(OpCodes.Ret));
        }

        private static void FindUsedTypes(ModuleDefinition module, List<RPCMethod> methods, HashSet<TypeReference> types)
        {
            for (int i = 0; i < module.Types.Count; i++)
            {
                var type = module.Types[i];

                for (int j = 0; j < type.Methods.Count; j++)
                {
                    var method = type.Methods[j];

                    if (method.Body == null) continue;

                    var body = method.Body;
                    
                    for (int k = 0; k < body.Instructions.Count; k++)
                    {
                        var instruction = body.Instructions[k];
                        
                        if (instruction.OpCode == OpCodes.Call && instruction.Operand is GenericInstanceMethod currentMethod)
                        {
                            bool isRpcMethod = false;
                            
                            foreach (var rpcMethod in methods)
                            {
                                if (rpcMethod.originalMethod.GetElementMethod() == currentMethod.GetElementMethod())
                                {
                                    isRpcMethod = true;
                                    break;
                                }
                            }

                            if (!isRpcMethod)
                                continue;

                            foreach (var argument in currentMethod.GenericArguments)
                            {
                                if (!argument.IsGenericParameter)
                                    types.Add(argument);
                            }
                        }
                    }
                }
            }
        }
        
        public static bool IsTypeInOwnModule(TypeReference typeReference, ModuleDefinition ownModule)
        {
            // Check if the type's module matches our own module
            if (typeReference.Module != ownModule)
                return false;

            // Check if the type is primitive or belongs to the core library (e.g., System, mscorlib)
            if (typeReference.IsPrimitive || typeReference.Scope.Name == "mscorlib" || typeReference.Scope.Name == "System.Private.CoreLib")
                return false;

            // Check if the type is an external reference by comparing the assembly name
            if (typeReference.Scope is AssemblyNameReference assemblyRef && assemblyRef.Name != ownModule.Assembly.Name.Name)
                return false;

            return true;
        }

        private static void GenerateExecuteFunction(ModuleDefinition module, TypeDefinition type, HashSet<TypeReference> usedTypes, bool inheritsFromIdentity, HashSet<TypeReference> typesToGenSerializer)
        {
            var initMethod = new MethodDefinition($"PurrInitMethod_{type.Name}_{type.Namespace}_Generated", 
                MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Static, module.TypeSystem.Void);
            type.Methods.Add(initMethod);
            
            var attributeType = module.GetTypeDefinition<RuntimeInitializeOnLoadMethodAttribute>(); 
            var constructor = attributeType.Resolve().Methods.First(m => m.IsConstructor && m.HasParameters).Import(module);
            var attribute = new CustomAttribute(constructor);
            
            initMethod.CustomAttributes.Add(attribute);
            attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.Int32, (int)RuntimeInitializeLoadType.AfterAssembliesLoaded));
            
            initMethod.Body.InitLocals = true;

            var code = initMethod.Body.GetILProcessor();
            
            var networkRegister = type.Module.GetTypeDefinition<NetworkRegister>();
            var registerMethod = networkRegister.GetMethod("Register", true).Import(type.Module);
            var hashMethod = networkRegister.GetMethod("Hash").Import(type.Module);

            if (inheritsFromIdentity)
            {
                var genericRegister = new GenericInstanceMethod(registerMethod);
                genericRegister.GenericArguments.Add(type);
                code.Append(Instruction.Create(OpCodes.Call, genericRegister));
            }
            
            foreach (var usedType in usedTypes)
            {
                code.Append(Instruction.Create(OpCodes.Ldtoken, usedType));
                code.Append(Instruction.Create(OpCodes.Call, hashMethod));
                
                if (IsTypeInOwnModule(usedType, module))
                    typesToGenSerializer.Add(usedType);
            }
            
            code.Append(Instruction.Create(OpCodes.Ldtoken, type));
            code.Append(Instruction.Create(OpCodes.Call, hashMethod));
            
            code.Append(Instruction.Create(OpCodes.Ret));
        }
    }
}
#endif