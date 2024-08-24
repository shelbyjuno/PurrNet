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
                        excludeOwner = false,
                        isStatic = method.IsStatic
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
                        excludeOwner = excludeOwner,
                        isStatic = method.IsStatic
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
                        excludeOwner = false,
                        isStatic = method.IsStatic
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
        
        private static void HandleRPCReceiver(ModuleDefinition module, TypeDefinition type, IReadOnlyList<RPCMethod> originalRpcs, bool isNetworkClass, int offset)
        {
            for (var i = 0; i < originalRpcs.Count; i++)
            {
                var attributes = MethodAttributes.Private | MethodAttributes.HideBySig;
                
                if (originalRpcs[i].Signature.isStatic)
                    attributes |= MethodAttributes.Static;
                
                var newMethod = new MethodDefinition($"HandleRPCGenerated_{offset + i}", attributes, originalRpcs[i].originalMethod.ReturnType);
                
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
                
                ValidateReceivingRPC(module, isNetworkClass, originalRpcs[i], code, info, asServer, end);

                try
                {
                    if (originalRpcs[i].originalMethod.HasGenericParameters)
                         HandleGenericRPCReceiver(module, originalRpcs[i], newMethod, stream, info, isNetworkClass);
                    else HandleNonGenericRPCReceiver(module, originalRpcs[i], newMethod, stream, info);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to handle RPC: {e.Message}\n{e.StackTrace}");
                }

                code.Append(end);
                type.Methods.Add(newMethod);
            }
        }

        private static void ValidateReceivingRPC(ModuleDefinition module, bool isNetworkClass, RPCMethod originalRpc, ILProcessor code, ParameterDefinition info, ParameterDefinition asServer, Instruction end)
        {
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
            
            code.Append(Instruction.Create(OpCodes.Ldarg, info)); // info
            ReturnRPCSignature(module, code, originalRpc);
            code.Append(Instruction.Create(OpCodes.Ldarg, asServer)); // asServer
            code.Append(Instruction.Create(OpCodes.Call, validateReceivingRPC));

            // if returned false, return early
            code.Append(Instruction.Create(OpCodes.Brfalse, end));
        }

        private static void HandleNonGenericRPCReceiver(ModuleDefinition module, RPCMethod rpcMethod, MethodDefinition newMethod, ParameterDefinition stream, ParameterDefinition info)
        {
            var code = newMethod.Body.GetILProcessor();
            var originalMethod = rpcMethod.originalMethod;
            int paramCount = originalMethod.Parameters.Count;

            var streamType = module.GetTypeDefinition<NetworkStream>();
            var identityType = module.GetTypeDefinition<NetworkIdentity>();

            var serializeMethod = streamType.GetMethod("Serialize", true).Import(module);
            
            var rpcModule = originalMethod.DeclaringType.Module.GetTypeDefinition<RPCModule>();
            var getLocalPlayer = rpcModule.GetMethod("GetLocalPlayer");
            
            var localPlayerProp = identityType.GetProperty("localPlayer");
            var localPlayerGetter = localPlayerProp.GetMethod.Import(module);
            
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
                                code.Append(Instruction.Create(OpCodes.Call, localPlayerGetter));
                                code.Append(Instruction.Create(OpCodes.Stloc, variable));
                            }
                            else
                            {
                                code.Append(Instruction.Create(OpCodes.Call, getLocalPlayer));
                                code.Append(Instruction.Create(OpCodes.Stloc, variable));
                            }
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

            for (var j = 0; j < vars.Count; j++)
                code.Append(Instruction.Create(OpCodes.Ldloc, vars[j]));

            code.Append(Instruction.Create(OpCodes.Call, GetOriginalMethod(originalMethod)));
        }

        private static MethodReference GetOriginalMethod(MethodReference originalMethod)
        {
            if (!originalMethod.ContainsGenericParameter)
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
            
            var localPlayerProp = identityType.GetProperty("localPlayer");
            var localPlayerGetter = localPlayerProp.GetMethod.Import(module);
            
            var genericRpcHeaderType = module.GetTypeDefinition<GenericRPCHeader>();
            var code = newMethod.Body.GetILProcessor();
            var readHeaderMethod = identityType.GetMethod("ReadGenericHeader").Import(module);

            var setInfo = genericRpcHeaderType.GetMethod("SetInfo").Import(module);
            var readGeneric = genericRpcHeaderType.GetMethod("Read").Import(module);
            var readT = genericRpcHeaderType.GetMethod("Read", true).Import(module);
            var setPlayerId = genericRpcHeaderType.GetMethod("SetPlayerId").Import(module);

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
            if (!rpcMethod.Signature.isStatic)
            {
                code.Append(Instruction.Create(OpCodes.Ldarg_0)); // this
                code.Append(Instruction.Create(OpCodes.Ldstr, originalMethod.Name)); // methodName
                code.Append(Instruction.Create(OpCodes.Ldloc, headerValue)); // rpcHeader
                code.Append(Instruction.Create(OpCodes.Call, callGenericMethod)); // CallGeneric
            }
            else
            {
                code.Append(Instruction.Create(OpCodes.Ldtoken, originalMethod.DeclaringType)); // methodName
                code.Append(Instruction.Create(OpCodes.Ldstr, originalMethod.Name)); // methodName
                code.Append(Instruction.Create(OpCodes.Ldloc, headerValue)); // rpcHeader
                code.Append(Instruction.Create(OpCodes.Call, callGenericMethod)); // CallGeneric
            }
        }

        private MethodDefinition HandleRPC(ModuleDefinition module, int id, RPCMethod methodRpc, bool isNetworkClass, [UsedImplicitly] List<DiagnosticMessage> messages)
        {
            var method = methodRpc.originalMethod;
            
            if (method.ReturnType.FullName != typeof(void).FullName)
            {
                Error(messages, "ServerRPC method must return void", method);
                return null;
            }
            
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
                newMethod.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
            
            var code = newMethod.Body.GetILProcessor();
             
            var streamType = module.GetTypeDefinition<NetworkStream>();
            var rpcType = module.GetTypeDefinition<RPCModule>();
            var identityType = module.GetTypeDefinition<NetworkIdentity>();
            var hahserType = module.GetTypeDefinition<Hasher>();

            var allocStreamMethod = rpcType.GetMethod("AllocStream").Import(module);
            var serializeMethod = streamType.GetMethod("Serialize", true).Import(module);
            var freeStreamMethod = rpcType.GetMethod("FreeStream").Import(module);
            
            var getId = identityType.GetProperty("id");
            var getSceneId = identityType.GetProperty("sceneId");
            var getStableHashU32 = hahserType.GetMethod("GetStableHashU32", true).Import(module);
            
            // Declare local variables
            newMethod.Body.InitLocals = true;
            
            var packetType = methodRpc.Signature.isStatic ? module.GetTypeDefinition<StaticRPCPacket>() : 
                isNetworkClass ? module.GetTypeDefinition<ChildRPCPacket>() : module.GetTypeDefinition<RPCPacket>();
            
            var streamVariable = new VariableDefinition(streamType.Import(module));
            var rpcDataVariable = new VariableDefinition(packetType.Import(module));
            var typeHash = new VariableDefinition(module.TypeSystem.UInt32);
            
            newMethod.Body.Variables.Add(streamVariable);
            newMethod.Body.Variables.Add(rpcDataVariable);
            newMethod.Body.Variables.Add(typeHash);

            var paramCount = newMethod.Parameters.Count;
            
            if (methodRpc.Signature.runLocally)
            {
                MethodReference callMethod = GetOriginalMethod(method);
                
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
                code.Append(Instruction.Create(OpCodes.Stloc, rpcDataVariable)); // rpcPacket
            }
            else if (isNetworkClass)
            {
                var buildChildRpc = module.GetTypeDefinition<NetworkModule>().GetMethod("BuildRPC").Import(module);
                
                code.Append(Instruction.Create(OpCodes.Ldarg_0));
                code.Append(Instruction.Create(OpCodes.Ldc_I4, id));
                code.Append(Instruction.Create(OpCodes.Ldloc, streamVariable));
                code.Append(Instruction.Create(OpCodes.Call, buildChildRpc));
                code.Append(Instruction.Create(OpCodes.Stloc, rpcDataVariable)); // rpcPacket
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
                code.Append(Instruction.Create(OpCodes.Stloc, rpcDataVariable)); // rpcPacket
            }

            if (!methodRpc.Signature.isStatic)
                code.Append(Instruction.Create(OpCodes.Ldarg_0)); // this
            
            code.Append(Instruction.Create(OpCodes.Ldloc, rpcDataVariable)); // rpcPacket
            ReturnRPCSignature(module, code, methodRpc); // rpcDetails

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
            code.Append(Instruction.Create(OpCodes.Ldc_I4, rpc.Signature.isStatic ? 1 : 0));

            if (rpc.Signature.type == RPCType.TargetRPC)
            {
                code.Append(rpc.Signature.isStatic
                    ? Instruction.Create(OpCodes.Ldarg_0)
                    : Instruction.Create(OpCodes.Ldarg_1));
                
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
                            var newRef = new MethodReference(@new.Name, @new.ReturnType, 
                                methodReference.DeclaringType)
                            {
                                HasThis = methodReference.HasThis,
                                ExplicitThis = methodReference.ExplicitThis,
                                CallingConvention = methodReference.CallingConvention,
                            };

                            foreach (var parameter in methodReference.Parameters)
                            {
                                newRef.Parameters.Add(new ParameterDefinition(parameter.Name, parameter.Attributes,
                                    parameter.ParameterType));
                            }

                            foreach (var parameter in methodReference.GenericParameters)
                            {
                                newRef.GenericParameters.Add(new GenericParameter(parameter.Name, parameter.Owner));
                            }

                            if (methodReference is GenericInstanceMethod genericInstanceMethod)
                            {
                                var newGenericInstanceMethod = new GenericInstanceMethod(newRef);
        
                                foreach (var argument in genericInstanceMethod.GenericArguments)
                                    newGenericInstanceMethod.GenericArguments.Add(argument);

                                newRef = newGenericInstanceMethod;
                            }

                            processor.Replace(instruction, Instruction.Create(OpCodes.Call, newRef));
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
                        var classFullName = typeof(NetworkModule).FullName;
                        
                        bool inheritsFromNetworkIdentity = type.FullName == idFullName || InheritsFrom(type, idFullName);
                        bool inheritsFromNetworkClass = type.FullName == classFullName || InheritsFrom(type, classFullName);

                        List<RPCMethod> _rpcMethods = new();

                        int idOffset = GetIDOffset(type, messages);

                        if (inheritsFromNetworkIdentity)
                        {
                            List<FieldDefinition> _networkFields = new();

                            for (var i = 0; i < type.Fields.Count; i++)
                            {
                                var field = type.Fields[i];
                                var fieldType = field.FieldType.Resolve();
                                var isNetworkClass = fieldType.FullName == classFullName || InheritsFrom(fieldType, classFullName);

                                if (!isNetworkClass) continue;
                                
                                _networkFields.Add(field);
                            }
                            
                            for (int i = 0; i < _networkFields.Count; i++)
                            {
                                HandleNetworkField(module, i, type, _networkFields[i]);
                            }

                            if (_networkFields.Count > 0)
                            {
                                CreateSyncVarInitMethod(module, type, _networkFields);
                            }
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
                        
                        try
                        {
                            HashSet<TypeReference> usedTypes = new();
                            FindUsedTypes(module, _rpcMethods, usedTypes);
                            GenerateExecuteFunction(module, type, usedTypes, inheritsFromNetworkIdentity);
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
                                var newMethod = HandleRPC(module, idOffset + index, _rpcMethods[index], inheritsFromNetworkClass, messages);

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

        private void CreateSyncVarInitMethod(ModuleDefinition module, TypeDefinition type, List<FieldDefinition> networkFields)
        {
            var newMethod = new MethodDefinition($"__{type.Name}_CodeGen_Initialize", 
                MethodAttributes.Private | MethodAttributes.HideBySig, module.TypeSystem.Void);

            var preserveAttribute = module.GetTypeDefinition<PreserveAttribute>();
            var constructor = preserveAttribute.Resolve().Methods.First(m => m.IsConstructor && !m.HasParameters).Import(module);
            newMethod.CustomAttributes.Add(new CustomAttribute(constructor));

            type.Methods.Add(newMethod);
            
            newMethod.Body.InitLocals = true;
            
            var code = newMethod.Body.GetILProcessor();
            var setparentmethod = module.GetTypeDefinition<NetworkModule>().Import(module)
            .GetMethod("SetParent").Import(module);

            for (int i = 0; i < networkFields.Count; i++)
            {
                FieldDefinition field = networkFields[i];
                
                code.Append(Instruction.Create(OpCodes.Ldarg_0));
                code.Append(Instruction.Create(OpCodes.Ldfld, field));
             
                code.Append(Instruction.Create(OpCodes.Ldarg_0));
                code.Append(Instruction.Create(OpCodes.Ldc_I4, i));
                code.Append(Instruction.Create(OpCodes.Call, setparentmethod));
            }

            code.Append(Instruction.Create(OpCodes.Ret));
        }

        private static void HandleNetworkField(ModuleDefinition module, int offset, TypeDefinition type, FieldDefinition networkField)
        {
            var newMethod = new MethodDefinition($"HandleRPCGenerated_GetNetworkClass_{offset}", 
                MethodAttributes.Private | MethodAttributes.HideBySig, networkField.FieldType);
                
            var preserveAttribute = module.GetTypeDefinition<PreserveAttribute>();
            var constructor = preserveAttribute.Resolve().Methods.First(m => m.IsConstructor && !m.HasParameters).Import(module);
            newMethod.CustomAttributes.Add(new CustomAttribute(constructor));
            
            newMethod.Body.InitLocals = true;
            
            var code = newMethod.Body.GetILProcessor();
            
            code.Append(Instruction.Create(OpCodes.Ldarg_0));
            code.Append(Instruction.Create(OpCodes.Ldfld, networkField));
            code.Append(Instruction.Create(OpCodes.Ret));
            
            type.Methods.Add(newMethod);
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

        private static void GenerateExecuteFunction(ModuleDefinition module, TypeDefinition type, HashSet<TypeReference> usedTypes, bool inheritsFromIdentity)
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
            }
            
            code.Append(Instruction.Create(OpCodes.Ldtoken, type));
            code.Append(Instruction.Create(OpCodes.Call, hashMethod));
            
            code.Append(Instruction.Create(OpCodes.Ret));
        }
    }
}
#endif