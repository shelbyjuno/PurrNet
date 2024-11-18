using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PurrNet.Packing;
using Unity.CompilationPipeline.Common.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PurrNet.Codegen
{
    public static class GenerateSerializersProcessor
    {
        enum HandledGenericTypes
        {
            None,
            List,
            Array
        }

        static string PrettyTypeName(TypeDefinition typeDef, TypeReference typeRef)
        {
            // print full name with generic arguments
            switch (typeRef)
            {
                case GenericInstanceType genericInstance:
                {
                    var genericArgs = string.Join(", ", genericInstance.GenericArguments.Select(a => a.Name));
                    var name = $"{typeDef.Name[..^2]}_{genericArgs}";
                    return name;
                }
                case ArrayType arrayType:
                    return $"Array_{PrettyTypeName(typeDef, arrayType.ElementType)}";
                default:
                    return typeDef.Name;
            }
        }
        
        public static void HandleType(AssemblyDefinition assembly, TypeReference type, List<DiagnosticMessage> messages)
        {
            var resolvedType = type.Resolve();
            
            if (resolvedType == null)
            {
                messages.Add(new DiagnosticMessage
                {
                    MessageData = $"Could not resolve type {type.FullName}",
                    DiagnosticType = DiagnosticType.Error
                });
                return;
            }
            
            if (resolvedType.IsInterface)
                return;
            
            bool isNetworkIdentity = PostProcessor.InheritsFrom(resolvedType, typeof(NetworkIdentity).FullName);
            bool isNetworkModule = PostProcessor.InheritsFrom(resolvedType, typeof(NetworkModule).FullName);
            
            if (!isNetworkIdentity && PostProcessor.InheritsFrom(resolvedType, typeof(Object).FullName) &&
                !HasInterface(resolvedType, typeof(INetworkedData)))
            {
                return;
            }
            
            if (isNetworkModule)
            {
                return;
            }
            
            var bitStreamType = assembly.MainModule.GetTypeDefinition(typeof(BitStream)).Import(assembly.MainModule);
            var mainmodule = assembly.MainModule;
            
            string namespaceName = type.Namespace;
            if (string.IsNullOrWhiteSpace(namespaceName))
                 namespaceName = "PurrNet.CodeGen.Serializers";
            else namespaceName += ".PurrNet.CodeGen.Serializers";
            
            // create static class
            var serializerClass = new TypeDefinition(namespaceName, 
                $"{PrettyTypeName(resolvedType, type)}_Serializer",
                TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.Public,
                assembly.MainModule.TypeSystem.Object
            );
            
            assembly.MainModule.Types.Add(serializerClass);

            if (IsGeneric(type, out var genericT))
            {
                var registerMethod = new MethodDefinition("Register", MethodAttributes.Static, assembly.MainModule.TypeSystem.Void);
                var attributeType = assembly.MainModule.GetTypeDefinition<RuntimeInitializeOnLoadMethodAttribute>(); 
                var constructor = attributeType.Resolve().Methods.First(m => m.IsConstructor && m.HasParameters).Import(assembly.MainModule);
                var attribute = new CustomAttribute(constructor);
                registerMethod.CustomAttributes.Add(attribute);
                attribute.ConstructorArguments.Add(new CustomAttributeArgument(assembly.MainModule.TypeSystem.Int32, (int)RuntimeInitializeLoadType.AfterAssembliesLoaded));
                registerMethod.Body = new MethodBody(registerMethod)
                {
                    InitLocals = true
                };
            
                var register = registerMethod.Body.GetILProcessor();
                GenerateRegisterMethod(assembly.MainModule, type, register, genericT);
                serializerClass.Methods.Add(registerMethod);
                return;
            }
            
            if (isNetworkIdentity)
            {
                var registerMethod = new MethodDefinition("Register", MethodAttributes.Static, assembly.MainModule.TypeSystem.Void);
                var attributeType = assembly.MainModule.GetTypeDefinition<RuntimeInitializeOnLoadMethodAttribute>(); 
                var constructor = attributeType.Resolve().Methods.First(m => m.IsConstructor && m.HasParameters).Import(assembly.MainModule);
                var attribute = new CustomAttribute(constructor);
                registerMethod.CustomAttributes.Add(attribute);
                attribute.ConstructorArguments.Add(new CustomAttributeArgument(assembly.MainModule.TypeSystem.Int32, (int)RuntimeInitializeLoadType.AfterAssembliesLoaded));
                registerMethod.Body = new MethodBody(registerMethod)
                {
                    InitLocals = true
                };
            
                var register = registerMethod.Body.GetILProcessor();
                GenerateRegisterMethodForIdentity(type, register);
                serializerClass.Methods.Add(registerMethod);
                return;
            }
            
            // create static write method
            var writeMethod = new MethodDefinition("Write", MethodAttributes.Public | MethodAttributes.Static, assembly.MainModule.TypeSystem.Void);
            var valueArg = new ParameterDefinition("value", ParameterAttributes.None, type);
            writeMethod.Parameters.Add(new ParameterDefinition("stream", ParameterAttributes.None, bitStreamType));
            writeMethod.Parameters.Add(valueArg);
            writeMethod.Body = new MethodBody(writeMethod)
            {
                InitLocals = true
            };
            
            var packerType = mainmodule.GetTypeDefinition(typeof(Packer<>)).Import(mainmodule);
            var readMethodP = packerType.GetMethod("Read").Import(mainmodule);
            var writeMethodP = packerType.GetMethod("Write").Import(mainmodule);
            
            var write = writeMethod.Body.GetILProcessor();
            GenerateMethod(true, writeMethod, writeMethodP, resolvedType, write, mainmodule, valueArg);
            serializerClass.Methods.Add(writeMethod);
            
            // create static read method
            var readMethod = new MethodDefinition("Read", MethodAttributes.Public | MethodAttributes.Static, assembly.MainModule.TypeSystem.Void);
            readMethod.Parameters.Add(new ParameterDefinition("stream", ParameterAttributes.None, bitStreamType));
            readMethod.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, new ByReferenceType(type)));
            
            readMethod.Body = new MethodBody(readMethod)
            {
                InitLocals = true
            };
            
            var read = readMethod.Body.GetILProcessor();
            GenerateMethod(false, readMethod, readMethodP, resolvedType, read, mainmodule, valueArg);
            serializerClass.Methods.Add(readMethod);
            
            RegisterSerializersProcessor.HandleType(type.Module, serializerClass, messages);
        }

        private static void GenerateRegisterMethodForIdentity(TypeReference type, ILProcessor il)
        {
            var packType = type.Module.GetTypeDefinition(typeof(PackNetworkIdentity));
            var registerMethod = packType.GetMethod("RegisterIdentity", true).Import(type.Module);
            
            var genericRegisterMethod = new GenericInstanceMethod(registerMethod);
            genericRegisterMethod.GenericArguments.Add(type);
            
            il.Emit(OpCodes.Call, genericRegisterMethod);
            il.Emit(OpCodes.Ret);
        }

        private static void GenerateRegisterMethod(ModuleDefinition module, TypeReference type, ILProcessor il, HandledGenericTypes handledType)
        {
            var importedType = type.Import(module);
            var packCollectionsType = module.GetTypeDefinition(typeof(PackCollections)).Import(module);
            
            switch (handledType)
            {
                case HandledGenericTypes.List when importedType is GenericInstanceType listType:
                    
                    var registerListMethod = packCollectionsType.GetMethod("RegisterList", true).Import(module);
                    var genericRegisterListMethod = new GenericInstanceMethod(registerListMethod);
                    genericRegisterListMethod.GenericArguments.Add(listType.GenericArguments[0]);
                    
                    il.Emit(OpCodes.Call, genericRegisterListMethod);
                    
                    break;
                case HandledGenericTypes.Array when importedType is ArrayType arrayType:
                    
                    var registerArrayMethod = packCollectionsType.GetMethod("RegisterArray", true).Import(module);
                    var genericRegisterArrayMethod = new GenericInstanceMethod(registerArrayMethod);
                    genericRegisterArrayMethod.GenericArguments.Add(arrayType.ElementType);
                    
                    il.Emit(OpCodes.Call, genericRegisterArrayMethod);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(handledType), handledType, null);
            }
            
            il.Emit(OpCodes.Ret);
        }
        
        public static bool HasInterface(TypeDefinition def, Type interfaceType)
        {
            return def.Interfaces.Any(i => i.InterfaceType.FullName == interfaceType.FullName);
        }

        private static void GenerateMethod(
            bool isWriting, MethodDefinition method, MethodReference serialize, TypeDefinition type, ILProcessor il,
            ModuleDefinition mainmodule, ParameterDefinition valueArg)
        {
            var packerType = mainmodule.GetTypeDefinition(typeof(Packer<>)).Import(mainmodule);

            if (type.IsEnum)
            {
                HandleEnums(isWriting, method, serialize, type, il, packerType, mainmodule);
                return;
            }

            if (HasInterface(type, typeof(INetworkedData)))
            {
                HandleIData(isWriting, type, il, mainmodule, valueArg);
                return;
            }
            
            foreach (var field in type.Fields)
            {
                // make field public
                if (!field.IsPublic)
                {
                    field.Attributes &= ~FieldAttributes.FieldAccessMask;
                    field.Attributes |= FieldAttributes.Assembly;
                }
                
                var genericM = CreateGenericMethod(packerType, field.FieldType, serialize, mainmodule);
                
                // Pack<T>.Write(stream, value.field);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(isWriting ? OpCodes.Ldfld : OpCodes.Ldflda, field);
                il.Emit(OpCodes.Call, genericM);
            }
            
            il.Emit(OpCodes.Ret);
        }

        private static void HandleIData(bool isWriting, TypeDefinition type,
            ILProcessor il, ModuleDefinition mainmodule, ParameterDefinition valueArg)
        {
            if (isWriting)
            {
                var writeData = type.GetMethod("Write").Import(mainmodule);

                il.Emit(OpCodes.Ldarga_S, valueArg);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, writeData);
            }
            else
            {
                var readData = type.GetMethod("Read").Import(mainmodule);

                il.Emit(OpCodes.Ldarg_S, valueArg);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, readData);
            }
            
                
            il.Emit(OpCodes.Ret);
        }

        private static void HandleEnums(bool isWriting, MethodDefinition method, MethodReference serialize,
            TypeDefinition type, ILProcessor il, TypeReference packerType, ModuleDefinition mainmodule)
        {
            var underlyingType = type.GetField("value__").FieldType;
            var enumWriteMethod = CreateGenericMethod(packerType, underlyingType, serialize, mainmodule);
                
            var tmpVar = new VariableDefinition(underlyingType);
                
            if (!isWriting)
            {
                method.Body.Variables.Add(tmpVar);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Stloc_0);
            }
                
            il.Emit(OpCodes.Ldarg_0);

            // load the address of the field
            if (isWriting)
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Call, enumWriteMethod);
            }
            else
            {
                il.Emit(OpCodes.Ldloca, tmpVar);
                il.Emit(OpCodes.Call, enumWriteMethod);

                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldloc_0);
                EmitStindForEnum(il, type);
            }
                
            il.Emit(OpCodes.Ret);
        }

        private static MethodReference CreateGenericMethod(TypeReference packerType, TypeReference type,
            MethodReference writeMethod, ModuleDefinition mainmodule)
        {
            var genericPackerType = new GenericInstanceType(packerType);
            genericPackerType.GenericArguments.Add(type);
                
            var genericWriteMethod = new MethodReference(writeMethod.Name, writeMethod.ReturnType, genericPackerType.Import(mainmodule))
            {
                HasThis = writeMethod.HasThis
            }; 
                
            foreach (var param in writeMethod.Parameters)
                genericWriteMethod.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
            return genericWriteMethod.Import(mainmodule);
        }

        private static bool IsGeneric(TypeReference typeDef, out HandledGenericTypes type)
        {        
            if (typeDef.IsArray)
            {
                type = HandledGenericTypes.Array; 
                return true;
            }
            
            if (IsGeneric(typeDef, typeof(List<>)))
            {
                type = HandledGenericTypes.List;
                return true;
            }

            type = HandledGenericTypes.None;
            return false;
        }
        
        private static bool IsGeneric(TypeReference typeDef, Type type)
        {
            // Ensure method has a generic return type
            if (typeDef is GenericInstanceType genericReturnType)
            {
                // Resolve the element type to compare against Task<>
                var resolvedType = genericReturnType.ElementType.Resolve();

                // Check if the resolved type matches Task<>
                return resolvedType != null && resolvedType.FullName == type.FullName;
            }

            return false;
        }
        
        private static TypeReference GetEnumUnderlyingType(TypeDefinition typeDef)
        {
            if (!typeDef.IsEnum)
                return null;

            var valueField = typeDef.Fields.FirstOrDefault(f => f.Name == "value__");
            return valueField?.FieldType;
        }
        
        private static void EmitStindForEnum(ILProcessor il, TypeDefinition enumType)
        {
            if (!enumType.IsEnum)
            {
                throw new ArgumentException($"{enumType.FullName} is not an enum.");
            }

            // Get the underlying type of the enum
            var underlyingType = GetEnumUnderlyingType(enumType);

            if (underlyingType == null)
            {
                throw new InvalidOperationException($"Unable to determine the underlying type of the enum {enumType.FullName}.");
            }

            // Emit the appropriate Stind opcode based on the underlying type
            switch (underlyingType.FullName)
            {
                case "System.Byte":
                case "System.SByte":
                    il.Emit(OpCodes.Stind_I1); // Store value for 1-byte types
                    break;

                case "System.Int16":
                case "System.UInt16":
                    il.Emit(OpCodes.Stind_I2); // Store value for 2-byte types
                    break;

                case "System.Int32":
                case "System.UInt32":
                    il.Emit(OpCodes.Stind_I4); // Store value for 4-byte types
                    break;

                case "System.Int64":
                case "System.UInt64":
                    il.Emit(OpCodes.Stind_I8); // Store value for 8-byte types
                    break;

                default:
                    throw new NotSupportedException($"Unsupported enum underlying type: {underlyingType.FullName}");
            }
        }
    }
}
