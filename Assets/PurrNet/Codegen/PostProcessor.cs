#if UNITY_MONO_CECIL
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PurrNet.Modules;
using PurrNet.Packets;
using PurrNet.Transports;
using PurrNet.Utils;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;
using UnityEngine.Scripting;

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
    
    public struct PendingAddition
    {
        public TypeDefinition target;
        public MethodDefinition method;
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
            
            if (name.StartsWith("UnityEditor."))
                return false;

            if (name.Contains("Editor"))
                return false;
            
            return true;
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
                if (attribute.AttributeType.FullName == typeof(ServerRPCAttribute).FullName)
                {
                    if (attribute.ConstructorArguments.Count != 3)
                    {
                        Error(messages, "ServerRPC attribute must have 3 arguments", method);
                        return null;
                    }
                    
                    var channel = (Channel)attribute.ConstructorArguments[0].Value;
                    var runLocally = (bool)attribute.ConstructorArguments[1].Value;
                    var requireOwnership = (bool)attribute.ConstructorArguments[2].Value;
                    
                    data = new RPCSignature
                    {
                        type = RPCType.ServerRPC,
                        channel = channel,
                        runLocally = runLocally,
                        requireOwnership = requireOwnership,
                        requireServer = false,
                        bufferLast = false,
                        excludeOwner = false
                    };
                    rpcCount++;
                }
                else if (attribute.AttributeType.FullName == typeof(ObserversRPCAttribute).FullName)
                {
                    if (attribute.ConstructorArguments.Count != 5)
                    {
                        Error(messages, "ObserversRPC attribute must have 5 arguments", method);
                        return null;
                    }
                    
                    var channel = (Channel)attribute.ConstructorArguments[0].Value;
                    var runLocally = (bool)attribute.ConstructorArguments[1].Value;
                    var bufferLast = (bool)attribute.ConstructorArguments[2].Value;
                    var requireServer = (bool)attribute.ConstructorArguments[3].Value;
                    var excludeOwner = (bool)attribute.ConstructorArguments[4].Value;
                    
                    data = new RPCSignature
                    {
                        type = RPCType.ObserversRPC,
                        channel = channel,
                        runLocally = runLocally,
                        bufferLast = bufferLast,
                        requireServer = requireServer,
                        requireOwnership = false,
                        excludeOwner = excludeOwner
                    };
                    rpcCount++;
                }
                else if (attribute.AttributeType.FullName == typeof(TargetRPCAttribute).FullName)
                {
                    if (attribute.ConstructorArguments.Count != 4)
                    {
                        Error(messages, "TargetRPC attribute must have 4 arguments", method);
                        return null;
                    }
                    
                    var channel = (Channel)attribute.ConstructorArguments[0].Value;
                    var runLocally = (bool)attribute.ConstructorArguments[1].Value;
                    var bufferLast = (bool)attribute.ConstructorArguments[2].Value;
                    var requireServer = (bool)attribute.ConstructorArguments[3].Value;
                    
                    data = new RPCSignature
                    {
                        type = RPCType.TargetRPC,
                        channel = channel,
                        runLocally = runLocally,
                        bufferLast = bufferLast,
                        requireServer = requireServer,
                        requireOwnership = false,
                        excludeOwner = false
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
        
        private static void Error(ICollection<DiagnosticMessage> messages, string message, MethodDefinition method)
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
        
        private static void HandleRPCReceiver(ModuleDefinition module, TypeDefinition type, IReadOnlyList<RPCMethod> originalRpcs, int offset)
        {
            for (var i = 0; i < originalRpcs.Count; i++)
            {
                var newMethod = new MethodDefinition($"HandleRPCGenerated_{offset + i}",
                    MethodAttributes.Public | MethodAttributes.HideBySig,
                    module.TypeSystem.Void);
                
                var preserveAttribute = module.GetTypeDefinition<PreserveAttribute>();
                var constructor = preserveAttribute.Resolve().Methods.First(m => m.IsConstructor && !m.HasParameters).Import(module);
                newMethod.CustomAttributes.Add(new CustomAttribute(constructor));
                
                var identityType = module.GetTypeDefinition<NetworkIdentity>();
                var streamType = module.GetTypeDefinition<NetworkStream>();
                var packetType = module.GetTypeDefinition<RPCPacket>();
                var rpcInfo = module.GetTypeDefinition<RPCInfo>();

                var readHeaderMethod = identityType.GetMethod("ReadGenericHeader").Import(module);
                var callGenericMethod = identityType.GetMethod("CallGeneric").Import(module);
                var localPlayerProp = identityType.GetProperty("localPlayer");
                var validateReceivingRPC = identityType.GetMethod("ValidateReceivingRPC").Import(module);
                var localPlayerGetter = localPlayerProp.GetMethod.Import(module);
                
                var genericRpcHeaderType = module.GetTypeDefinition<GenericRPCHeader>();
                
                var setInfo = genericRpcHeaderType.GetMethod("SetInfo").Import(module);
                var readGeneric = genericRpcHeaderType.GetMethod("Read").Import(module);
                var readT = genericRpcHeaderType.GetMethod("Read", true).Import(module);
                var setPlayerId = genericRpcHeaderType.GetMethod("SetPlayerId").Import(module);
                
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
                var getId = packetType.GetMethod(RPCPacket.GET_ID_METHOD);
                
                if (getId == null)
                    throw new Exception("Failed to resolve GetID method.");

                var rpc = originalRpcs[i].originalMethod;

                var serializeMethod = streamType.GetMethod("Serialize", true).Import(module);
                
                int paramCount = rpc.Parameters.Count;

                var end = Instruction.Create(OpCodes.Ret);
                
                // Call validateReceivingRPC(this, RPCInfo, RPCSignature, asServer)
                
                code.Append(Instruction.Create(OpCodes.Ldarg_0)); // this
                code.Append(Instruction.Create(OpCodes.Ldarg, info)); // info
                ReturnRPCSignature(module, code, originalRpcs[i]);
                code.Append(Instruction.Create(OpCodes.Ldarg, asServer)); // asServer
                code.Append(Instruction.Create(OpCodes.Call, validateReceivingRPC));
                
                // if returned false, return early
                code.Append(Instruction.Create(OpCodes.Brfalse, end));

                try
                {
                    if (rpc.HasGenericParameters)
                    {
                        HandleGenericRPC(module, originalRpcs, rpc, genericRpcHeaderType, newMethod, serializeMethod,
                            code, stream, info, paramCount, readHeaderMethod, i, setInfo, localPlayerGetter,
                            setPlayerId, readGeneric, readT, callGenericMethod);
                    }
                    else
                    {
                        HandleNonGenericRPC(originalRpcs, rpc, newMethod, i, paramCount, code, info, localPlayerGetter,
                            serializeMethod, stream);
                    }
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to handle RPC: {e.Message}\n{e.StackTrace}");
                }

                code.Append(end);
                type.Methods.Add(newMethod);
            }
        }

        private static void HandleNonGenericRPC(IReadOnlyList<RPCMethod> originalRpcs, MethodDefinition rpc, MethodDefinition newMethod,
            int i, int paramCount, ILProcessor code, ParameterDefinition info, MethodReference localPlayerGetter,
            MethodReference serializeMethod, ParameterDefinition stream)
        {
            for (var p = 0; p < rpc.Parameters.Count; p++)
            {
                var param = rpc.Parameters[p];

                var variable = new VariableDefinition(param.ParameterType);
                newMethod.Body.Variables.Add(variable);

                if (ShouldIgnore(originalRpcs[i].Signature.type, param, p, paramCount, out var specialType))
                {
                    switch (specialType)
                    {
                        case SpecialParamType.RPCInfo:
                            code.Append(Instruction.Create(OpCodes.Ldarg, info));
                            code.Append(Instruction.Create(OpCodes.Stloc, variable));
                            break;
                        case SpecialParamType.SenderId:
                            code.Append(Instruction.Create(OpCodes.Ldarg_0));
                            code.Append(Instruction.Create(OpCodes.Call, localPlayerGetter));
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

            code.Append(Instruction.Create(OpCodes.Ldarg_0));

            var vars = newMethod.Body.Variables;

            for (var j = 0; j < vars.Count; j++)
                code.Append(Instruction.Create(OpCodes.Ldloc, vars[j]));

            code.Append(Instruction.Create(OpCodes.Call, rpc));
        }

        private static void HandleGenericRPC(ModuleDefinition module, IReadOnlyList<RPCMethod> originalRpcs, MethodDefinition rpc,
            TypeReference genericRpcHeaderType, MethodDefinition newMethod, MethodReference serializeMethod, ILProcessor code,
            ParameterDefinition stream, ParameterDefinition info, int paramCount, MethodReference readHeaderMethod, int i,
            MethodReference setInfo, MethodReference localPlayerGetter, MethodReference setPlayerId,
            MethodReference readGeneric, MethodReference readT, MethodReference callGenericMethod)
        {
            int genericParamCount = rpc.GenericParameters.Count;

            var headerValue = new VariableDefinition(genericRpcHeaderType.Import(module));
            newMethod.Body.Variables.Add(headerValue);

            var serializeUint = new GenericInstanceMethod(serializeMethod);
            serializeUint.GenericArguments.Add(module.TypeSystem.UInt32);

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
                var param = rpc.Parameters[p];

                if (ShouldIgnore(originalRpcs[i].Signature.type, param, p, paramCount, out var specialType))
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

                            code.Append(Instruction.Create(OpCodes.Ldarg_0)); // this
                            code.Append(Instruction.Create(OpCodes.Call, localPlayerGetter));

                            code.Append(Instruction.Create(OpCodes.Ldc_I4, p));
                            code.Append(Instruction.Create(OpCodes.Call, setPlayerId));
                            break;
                    }

                    continue;
                }

                if (param.ParameterType.IsGenericParameter)
                {
                    var genericIdx = rpc.GenericParameters.IndexOf((GenericParameter)param.ParameterType);

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
            code.Append(Instruction.Create(OpCodes.Ldarg_0)); // this
            code.Append(Instruction.Create(OpCodes.Ldstr, rpc.Name)); // methodName
            code.Append(Instruction.Create(OpCodes.Ldloc, headerValue)); // rpcHeader
            code.Append(Instruction.Create(OpCodes.Call, callGenericMethod)); // CallGeneric
        }

        private MethodDefinition HandleRPC(ModuleDefinition module, int id, RPCMethod methodRpc, [UsedImplicitly] List<DiagnosticMessage> messages)
        {
            var method = methodRpc.originalMethod;
            
            if (method.ReturnType.FullName != typeof(void).FullName)
            {
                Error(messages, "ServerRPC method must return void", method);
                return null;
            }
            
            string ogName = method.Name;
            method.Name = ogName + "_Original_" + id;
            
            var newMethod = new MethodDefinition(ogName, MethodAttributes.Public | MethodAttributes.HideBySig, method.ReturnType);
            
            foreach (var t in method.GenericParameters)
                newMethod.GenericParameters.Add(new GenericParameter(t.Name, newMethod));
            
            foreach (var param in method.CustomAttributes)
                newMethod.CustomAttributes.Add(param);
            
            method.CustomAttributes.Clear();
            
            foreach (var param in method.Parameters)
                newMethod.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
            
            var code = newMethod.Body.GetILProcessor();
             
            var streamType = module.GetTypeDefinition<NetworkStream>();
            var rpcType = module.GetTypeDefinition<RPCModule>();
            var identityType = module.GetTypeDefinition<NetworkIdentity>();
            var packetType = module.GetTypeDefinition<RPCPacket>();
            var hahserType = module.GetTypeDefinition<Hasher>();

            var allocStreamMethod = rpcType.GetMethod("AllocStream").Import(module);
            var serializeMethod = streamType.GetMethod("Serialize", true).Import(module);
            var buildRawRPCMethod = rpcType.GetMethod("BuildRawRPC").Import(module);
            var freeStreamMethod = rpcType.GetMethod("FreeStream").Import(module);
            var sendRpc = identityType.GetMethod("SendRPC").Import(module);
            
            var getId = identityType.GetProperty("id");
            var getSceneId = identityType.GetProperty("sceneId");
            var getStableHashU32 = hahserType.GetMethod("GetStableHashU32", true).Import(module);
            
            // Declare local variables
            newMethod.Body.InitLocals = true;
            
            var streamVariable = new VariableDefinition(streamType.Import(module));
            var rpcDataVariable = new VariableDefinition(packetType.Import(module));
            var rpcDetailsVariable = new VariableDefinition(module.GetTypeDefinition<RPCSignature>().Import(module));
            var typeHash = new VariableDefinition(module.TypeSystem.UInt32);
            
            newMethod.Body.Variables.Add(streamVariable);
            newMethod.Body.Variables.Add(rpcDataVariable);
            newMethod.Body.Variables.Add(rpcDetailsVariable);
            newMethod.Body.Variables.Add(typeHash);

            var paramCount = newMethod.Parameters.Count;
            
            if (methodRpc.Signature.runLocally)
            {
                MethodReference callMethod = method;
                
                if (method.HasGenericParameters)
                {
                    var genericInstanceMethod = new GenericInstanceMethod(method);
                    
                    for (var i = 0; i < method.GenericParameters.Count; i++)
                        genericInstanceMethod.GenericArguments.Add(newMethod.GenericParameters[i]);
                    
                    callMethod = genericInstanceMethod;
                }
                
                code.Append(Instruction.Create(OpCodes.Ldarg_0)); // this

                for (int i = 0; i < paramCount; ++i)
                {
                    var param = newMethod.Parameters[i];
                    code.Append(Instruction.Create(OpCodes.Ldarg, param)); // param
                }

                code.Append(Instruction.Create(OpCodes.Call, callMethod)); // Call original method
            }
            
            code.Append(Instruction.Create(OpCodes.Ldc_I4, 0));
            code.Append(Instruction.Create(OpCodes.Call, allocStreamMethod));
            code.Append(Instruction.Create(OpCodes.Stloc, streamVariable));

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

            // NetworkStream stream, RPCDetails details, int rpcId, NetworkIdentity identity
            code.Append(Instruction.Create(OpCodes.Ldarg_0));
            code.Append(Instruction.Create(OpCodes.Call, getId.GetMethod.Import(module))); // id
            code.Append(Instruction.Create(OpCodes.Ldarg_0));
            code.Append(Instruction.Create(OpCodes.Call, getSceneId.GetMethod.Import(module))); // sceneId
            code.Append(Instruction.Create(OpCodes.Ldc_I4, id)); // rpcId
            code.Append(Instruction.Create(OpCodes.Ldloc, streamVariable)); // stream
            
            ReturnRPCSignature(module, code, methodRpc);

            code.Append(Instruction.Create(OpCodes.Stloc, rpcDetailsVariable));
            
            // BuildRawRPC(int networkId, SceneID sceneId, byte rpcId, NetworkStream stream, RPCDetails details)
            code.Append(Instruction.Create(OpCodes.Call, buildRawRPCMethod));
            code.Append(Instruction.Create(OpCodes.Stloc, rpcDataVariable)); // rpcPacket
            
            code.Append(Instruction.Create(OpCodes.Ldarg_0)); // this
            code.Append(Instruction.Create(OpCodes.Ldloc, rpcDataVariable)); // rpcPacket
            code.Append(Instruction.Create(OpCodes.Ldloc, rpcDetailsVariable)); // rpcDetails
            code.Append(Instruction.Create(OpCodes.Call, sendRpc));
            
            code.Append(Instruction.Create(OpCodes.Ldloc, streamVariable));
            code.Append(Instruction.Create(OpCodes.Call, freeStreamMethod));

            code.Append(Instruction.Create(OpCodes.Ret));

            return newMethod;
        }

        private static void ReturnRPCSignature(ModuleDefinition module, ILProcessor code, RPCMethod rpc)
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

            if (rpc.Signature.type == RPCType.TargetRPC)
            {
                code.Append(Instruction.Create(OpCodes.Ldarg_1));
                code.Append(Instruction.Create(OpCodes.Call, makeRpcDetailsTarget));
            }
            else
            {
                code.Append(Instruction.Create(OpCodes.Call, makeRpcDetails));
            }
        }

        private static void UpdateMethodReferences(ModuleDefinition module, MethodReference old, MethodReference @new, [UsedImplicitly] List<DiagnosticMessage> messages)
        {
            foreach (var type in module.Types)
            {
                foreach (var method in type.Methods)
                {
                    if (method == @new || method.GetElementMethod() == @new) continue;
                    
                    if (method.Body == null) continue;
                    
                    var processor = method.Body.GetILProcessor();

                    for (var i = 0; i < method.Body.Instructions.Count; i++)
                    {
                        var instruction = method.Body.Instructions[i];
                        
                        if (instruction.OpCode == OpCodes.Call &&
                            instruction.Operand is MethodReference methodReference && methodReference.GetElementMethod() == old)
                        {
                            if (methodReference is GenericInstanceMethod genericInstanceMethod)
                            {
                                var newGenericInstanceMethod = new GenericInstanceMethod(@new);
        
                                foreach (var argument in genericInstanceMethod.GenericArguments)
                                    newGenericInstanceMethod.GenericArguments.Add(argument);

                                processor.Replace(instruction, Instruction.Create(OpCodes.Call, newGenericInstanceMethod));
                            }
                            else
                            {
                                processor.Replace(instruction, Instruction.Create(OpCodes.Call, @new));
                            }
                        }
                    }
                }
            }
        }
        

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            try
            {
                if (!WillProcess(compiledAssembly))
                    return default!;
                
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
                        var type = module.Types[t];

                        if (!type.IsClass)
                            continue;

                        var idFullName = typeof(NetworkIdentity).FullName;

                        if (!InheritsFrom(type, idFullName) && type.FullName != idFullName)
                            continue;
                        
                        List<RPCMethod> _rpcMethods = new();

                        int idOffset = GetIDOffset(type, messages);

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

                        try
                        {
                            GenerateExecuteFunction(type, _rpcMethods);
                        }
                        catch (Exception e)
                        {
                            messages.Add(new DiagnosticMessage
                            {
                                DiagnosticType = DiagnosticType.Error,
                                MessageData = $"GenerateExecuteFunction [{type.Name}]: {e.Message}\n{e.StackTrace}"
                            });
                        }

                        for (var index = 0; index < _rpcMethods.Count; index++)
                        {
                            var method = _rpcMethods[index].originalMethod;

                            try
                            {
                                var newMethod = HandleRPC(module, idOffset + index, _rpcMethods[index], messages);

                                if (newMethod != null)
                                {
                                    type.Methods.Add(newMethod);
                                    UpdateMethodReferences(module, method, newMethod, messages);
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
                                HandleRPCReceiver(module, type, _rpcMethods, idOffset);
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
                    }
                }

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

        private static void GenerateExecuteFunction(TypeDefinition type, List<RPCMethod> rpcMethods)
        {
            var initMethod = new MethodDefinition($"PurrInitMethod_{type.Name}_{type.Namespace}_Generated", MethodAttributes.Private | MethodAttributes.Static, type.Module.TypeSystem.Void);
            type.Methods.Add(initMethod);
            
            var attribute = new CustomAttribute(type.Module.ImportReference(typeof(RuntimeInitializeOnLoadMethodAttribute)
                .GetConstructor(new []
                {
                    typeof(RuntimeInitializeLoadType)
                })));
            
            initMethod.CustomAttributes.Add(attribute);
            attribute.ConstructorArguments.Add(new CustomAttributeArgument(type.Module.TypeSystem.Int32, RuntimeInitializeLoadType.AfterAssembliesLoaded));
            
            initMethod.Body.InitLocals = true;

            var code = initMethod.Body.GetILProcessor();
            
            var networkRegister = type.Module.GetTypeDefinition<NetworkRegister>();
            var registerMethod = networkRegister.GetMethod("Register", true).Import(type.Module);

            var genericRegister = new GenericInstanceMethod(registerMethod);
            genericRegister.GenericArguments.Add(type);
            
            code.Append(Instruction.Create(OpCodes.Call, genericRegister));
            code.Append(Instruction.Create(OpCodes.Ret));
        }
    }
}
#endif