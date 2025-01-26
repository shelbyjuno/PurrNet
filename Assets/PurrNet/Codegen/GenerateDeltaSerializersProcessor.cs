using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PurrNet.Packing;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace PurrNet.Codegen
{
    public static class GenerateDeltaSerializersProcessor
    {
        public static void HandleType(AssemblyDefinition assembly, TypeReference type, TypeDefinition generatedClass, List<DiagnosticMessage> messages)
        {
            var bitStreamType = assembly.MainModule.GetTypeDefinition(typeof(BitPacker)).Import(assembly.MainModule);
            var writeMethod = new MethodDefinition("WriteDelta", MethodAttributes.Public | MethodAttributes.Static, assembly.MainModule.TypeSystem.Void);
            var readMethod = new MethodDefinition("ReadDelta", MethodAttributes.Public | MethodAttributes.Static, assembly.MainModule.TypeSystem.Void);
            
            CreateWriteMethod(writeMethod, type, bitStreamType);
            CreateReadMethod(readMethod, type, bitStreamType);
            
            generatedClass.Methods.Add(writeMethod);
            generatedClass.Methods.Add(readMethod);
        }

        private static void CreateReadMethod(MethodDefinition readMethod, TypeReference type, TypeReference bitStreamType)
        {
            var streamArg = new ParameterDefinition("stream", ParameterAttributes.None, bitStreamType);
            var oldValueArg = new ParameterDefinition("oldValue", ParameterAttributes.None, type);
            var valueArg = new ParameterDefinition("value", ParameterAttributes.None, new ByReferenceType(type));
            
            readMethod.Parameters.Add(streamArg);
            readMethod.Parameters.Add(oldValueArg);
            readMethod.Parameters.Add(valueArg);
            readMethod.Body = new MethodBody(readMethod)
            {
                InitLocals = true
            };
            
            var il = readMethod.Body.GetILProcessor();
            
            il.Emit(OpCodes.Ret);
        }

        private static void CreateWriteMethod(MethodDefinition writeMethod, TypeReference type, TypeReference bitStreamType)
        {
            var streamArg = new ParameterDefinition("stream", ParameterAttributes.None, bitStreamType);
            var oldValueArg = new ParameterDefinition("oldValue", ParameterAttributes.None, type);
            var valueArg = new ParameterDefinition("value", ParameterAttributes.None, type);
            
            writeMethod.Parameters.Add(streamArg);
            writeMethod.Parameters.Add(oldValueArg);
            writeMethod.Parameters.Add(valueArg);
            writeMethod.Body = new MethodBody(writeMethod)
            {
                InitLocals = true
            };
            
            var il = writeMethod.Body.GetILProcessor();
            
            il.Emit(OpCodes.Ret);
        }

        public static void HandleGenericType(AssemblyDefinition assembly, TypeReference type, HandledGenericTypes genericT, List<DiagnosticMessage> messages)
        {
            // TODO: Implement
        }
    }
}
