using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace PurrNet.Codegen
{
    public static class UnityProxyProcessor
    {
        public static void Process(TypeDefinition type, [UsedImplicitly] List<DiagnosticMessage> messages)
        {
            try
            {
                bool isProxyItself = type.FullName == typeof(UnityProxy).FullName;
                
                if (isProxyItself)
                    return;
                
                var module = type.Module;
                
                string objectClassFullName = typeof(UnityEngine.Object).FullName;
                var unityProxyType = module.GetTypeReference<UnityProxy>().Import(module).Resolve();

                foreach (var method in type.Methods)
                {
                    if (method.Body == null) continue;

                    var processor = method.Body.GetILProcessor();

                    for (var i = 0; i < method.Body.Instructions.Count; i++)
                    {
                        var instruction = method.Body.Instructions[i];

                        if (instruction.Operand is not MethodReference methodReference)
                            continue;

                        if (methodReference.DeclaringType.FullName != objectClassFullName)
                            continue;

                        if (methodReference.Name != "Instantiate")
                            continue;

                        var targetMethod = GetInstantiateDefinition(methodReference, unityProxyType);
                        var targerRef = module.ImportReference(targetMethod);
                        
                        if (methodReference is GenericInstanceMethod genericInstanceMethod)
                        {
                            var genRef = new GenericInstanceMethod(targerRef);
                            
                            for (var j = 0; j < genericInstanceMethod.GenericArguments.Count; j++)
                                genRef.GenericArguments.Add(genericInstanceMethod.GenericArguments[j]);

                            for (var j = 0; j < genRef.GenericParameters.Count; j++)
                                genRef.GenericParameters.Add(genRef.GenericParameters[j]);
                            
                            targerRef = module.ImportReference(genRef);
                            
                            /*messages.Add(new DiagnosticMessage
                            {
                                MessageData = $"Generic method: {methodReference.FullName}, {targerRef.FullName}",
                                DiagnosticType = DiagnosticType.Error
                            });*/
                        }
                        processor.Replace(instruction, processor.Create(OpCodes.Call, targerRef));
                    }
                }
            }
            catch (Exception e)
            {
                messages.Add(new DiagnosticMessage
                {
                    MessageData = $"Failed to process UnityProxy: {e.Message}",
                    DiagnosticType = DiagnosticType.Error
                });
            }
        }
        
        static MethodDefinition GetInstantiateDefinition(
            MethodReference originalMethod,
            TypeDefinition declaringType)
        {
            foreach (var method in declaringType.Methods)
            {
                if (method.Name != originalMethod.Name)
                    continue;

                if (method.Parameters.Count != originalMethod.Parameters.Count)
                    continue;
                
                if (originalMethod.HasGenericParameters != method.HasGenericParameters)
                    continue;
                
                if (originalMethod.GenericParameters.Count != method.GenericParameters.Count)
                    continue;
                
                return method;
            }

            return null;
        }

    }
}
