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
                var unityProxyType = module.GetTypeReference<UnityProxy>().Import(module);

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

                        var targetMethod = GetInstantiateDefinition(module, methodReference.Name, methodReference, unityProxyType.Resolve());
                        var targerRef = module.ImportReference(targetMethod);
                        
                        if (methodReference is GenericInstanceMethod genericInstanceMethod)
                        {
                            var genRef = new GenericInstanceMethod(targerRef);
                            
                            for (var j = 0; j < genericInstanceMethod.GenericArguments.Count; j++)
                                genRef.GenericArguments.Add(genericInstanceMethod.GenericArguments[j]);

                            for (var j = 0; j < genRef.GenericParameters.Count; j++)
                                genRef.GenericParameters.Add(genRef.GenericParameters[j]);
                            
                            targerRef = module.ImportReference(genRef);
                            
                            messages.Add(new DiagnosticMessage
                            {
                                MessageData = $"Generic method: {methodReference.FullName}, {targerRef.FullName}",
                                DiagnosticType = DiagnosticType.Error
                            });
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
            ModuleDefinition module,
            string name,
            MethodReference originalMethod,
            TypeDefinition declaringType)
        {
            // Match the method name and parameter count
            var proxyMethod = declaringType.Methods.FirstOrDefault(m =>
                m.Name == name &&
                m.Parameters.Count == originalMethod.Parameters.Count &&
                m.HasGenericParameters == originalMethod.HasGenericParameters);

            if (proxyMethod == null)
            {
                throw new InvalidOperationException($"Could not find method '{name}' in '{declaringType.FullName}'.");
            }

            // For generic methods, ensure generic parameter matching
            if (proxyMethod.HasGenericParameters && originalMethod is GenericInstanceMethod genericInstanceMethod)
            {
                // Check that the generic arguments align
                var proxyGenericParams = proxyMethod.GenericParameters;
                if (proxyGenericParams.Count != genericInstanceMethod.GenericArguments.Count)
                {
                    throw new InvalidOperationException(
                        $"Generic parameter mismatch: {proxyGenericParams.Count} expected, {genericInstanceMethod.GenericArguments.Count} provided.");
                }

                // Verify that constraints (if any) match the generic arguments
                for (int i = 0; i < proxyGenericParams.Count; i++)
                {
                    var proxyParam = proxyGenericParams[i];
                    var originalArg = genericInstanceMethod.GenericArguments[i].Import(module);

                    // Verify the constraints for the generic parameter
                    foreach (var constraint in proxyParam.Constraints)
                    {
                        if (!constraint.IsAssignableFrom(originalArg))
                        {
                            throw new InvalidOperationException(
                                $"Generic argument '{originalArg.FullName}' does not satisfy the constraint '{constraint.FullName}'.");
                        }
                    }
                }
            }

            return proxyMethod;
        }

    }
}
