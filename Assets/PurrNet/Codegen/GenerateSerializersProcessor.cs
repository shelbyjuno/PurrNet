using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PurrNet.Packets;
using PurrNet.Packing;
using Unity.CompilationPipeline.Common.Diagnostics;
using UnityEngine;
using INetworkedData = PurrNet.Packing.INetworkedData;
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

        static bool ValideType(TypeReference type)
        {
            // Check if the type itself is an interface
            if (type.Resolve()?.IsInterface == true)
            {
                return false;
            }

            // Check if the type is a generic instance
            if (type is GenericInstanceType genericInstance)
            {
                // Recursively validate all generic arguments
                foreach (var argument in genericInstance.GenericArguments)
                {
                    if (argument.ContainsGenericParameter || argument.Resolve()?.IsInterface == true || !ValideType(argument))
                    {
                        return false;
                    }
                }
            }
            else if (type.ContainsGenericParameter)
            {
                // If the type itself contains generic parameters (e.g., T)
                return false;
            }

            // If no open generics or interfaces are found, return true
            return true;
        }
        
        static string MakeFullNameValidCSharp(string name)
        {
            return name.Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace(" ", "_").Replace(".", "_").Replace("`", "_");
        }
        
        public static void HandleType(bool hashOnly, AssemblyDefinition assembly, TypeReference type, HashSet<string> visited, List<DiagnosticMessage> messages)
        {
            if (!visited.Add(type.FullName))
                return;

            if (!ValideType(type))
                return;
            
            if (!PostProcessor.IsTypeInOwnModule(type, assembly.MainModule))
                return;
            
            string namespaceName = type.Namespace;
            if (string.IsNullOrWhiteSpace(namespaceName))
                namespaceName = "PurrNet.CodeGen.Serializers";
            else namespaceName += ".PurrNet.CodeGen.Serializers";
            
            // create static class
            var serializerClass = new TypeDefinition(namespaceName, 
                $"{MakeFullNameValidCSharp(type.FullName)}_Serializer",
                TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.Public,
                assembly.MainModule.TypeSystem.Object
            );
            
            var resolvedType = type.Resolve();
            
            if (resolvedType == null)
                return;
            
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
                return;
            
            var bitStreamType = assembly.MainModule.GetTypeDefinition(typeof(BitPacker)).Import(assembly.MainModule);
            var mainmodule = assembly.MainModule;
            
            assembly.MainModule.Types.Add(serializerClass);

            if (hashOnly)
            {
                HandleHashOnly(assembly, type, serializerClass);
                return;
            }

            if (IsGeneric(type, out var genericT))
            {
                HandleGenerics(assembly, type, genericT, serializerClass);
                return;
            }
            
            if (isNetworkIdentity)
            {
                HandleNetworkIdentity(assembly, type, serializerClass);
                return;
            }
            
            // create static write method
            var writeMethod = new MethodDefinition("Write", MethodAttributes.Public | MethodAttributes.Static, assembly.MainModule.TypeSystem.Void);
            var valueArg = new ParameterDefinition("value", ParameterAttributes.None, type);
            var streamArg = new ParameterDefinition("stream", ParameterAttributes.None, bitStreamType);
            writeMethod.Parameters.Add(streamArg);
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

        private static void HandleHashOnly(AssemblyDefinition assembly, TypeReference type, TypeDefinition serializerClass)
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
            
            var il = registerMethod.Body.GetILProcessor();
            
            var networkRegister = assembly.MainModule.GetTypeDefinition(typeof(NetworkRegister)).Import(assembly.MainModule);
            var hashMethod = networkRegister.GetMethod("Hash").Import(assembly.MainModule);
            
            // NetworkRegister.Hash(RuntimeTypeHandle handle);
            il.Emit(OpCodes.Ldtoken, type);
            il.Emit(OpCodes.Call, hashMethod);
            il.Emit(OpCodes.Ret);
            
            serializerClass.Methods.Add(registerMethod);
        }

        private static void HandleNetworkIdentity(AssemblyDefinition assembly, TypeReference type,
            TypeDefinition serializerClass)
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
        }

        private static void HandleGenerics(AssemblyDefinition assembly, TypeReference type, HandledGenericTypes genericT,
            TypeDefinition serializerClass)
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

        private static MethodReference CreateSetterMethod(TypeDefinition parent, FieldDefinition field)
        {
            var method = new MethodDefinition($"Purrnet_Set_{field.Name}", MethodAttributes.Public, parent.Module.TypeSystem.Void);
            var valueArg = new ParameterDefinition("value", ParameterAttributes.None, field.FieldType);
            method.Parameters.Add(valueArg);
            
            var setter = method.Body.GetILProcessor();
            
            setter.Emit(OpCodes.Ldarg_0);
            setter.Emit(OpCodes.Ldarg_1);
            setter.Emit(OpCodes.Stfld, field);
            
            setter.Emit(OpCodes.Ret);
            
            parent.Methods.Add(method);
            return method;
        }
        
        private static MethodReference CreateGetterMethod(TypeDefinition parent, FieldDefinition field)
        {
            var method = new MethodDefinition($"Purrnet_Get_{field.Name}", MethodAttributes.Public, field.FieldType);
            var getter = method.Body.GetILProcessor();
            
            getter.Emit(OpCodes.Ldarg_0);
            getter.Emit(OpCodes.Ldfld, field);
            getter.Emit(OpCodes.Ret);
            
            parent.Methods.Add(method);
            return method;
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
                var genericM = CreateGenericMethod(packerType, field.FieldType, serialize, mainmodule);


                // make field public
                if (!field.IsPublic)
                {
                    if (isWriting)
                    {
                        var getter = CreateGetterMethod(type, field);
                        
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldarga_S, valueArg);
                        il.Emit(OpCodes.Call, getter);
                        il.Emit(OpCodes.Call, genericM);
                    }
                    else
                    {
                        var variable = new VariableDefinition(field.FieldType);
                        method.Body.Variables.Add(variable);
                        
                        var setter = CreateSetterMethod(type, field);
                         
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldloca, variable);
                        il.Emit(OpCodes.Call, genericM);
                        
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Ldloc, variable);
                        il.Emit(OpCodes.Call, setter);
                    }
                }
                else
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(isWriting ? OpCodes.Ldfld : OpCodes.Ldflda, field);
                    il.Emit(OpCodes.Call, genericM);
                }

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
