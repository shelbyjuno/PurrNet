#if UNITY_MONO_CECIL
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace PurrNet.Codegen
{
    [UsedImplicitly]
    public class PostProcessor : ILPostProcessor
    {
        public override ILPostProcessor GetInstance() => this;

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            var isEditorDll = compiledAssembly.Name.EndsWith(".Editor");
            return !isEditorDll;
        }
        
        private static bool InheritsFrom(TypeDefinition type, string baseTypeName)
        {
            if (type.BaseType == null)
                return false;
    
            if (type.BaseType.FullName == baseTypeName)
                return true;
    
            var baseType = type.BaseType.Resolve();
            return baseType != null && InheritsFrom(baseType, baseTypeName);
        }
        
        private bool HasCustomAttribute(MethodDefinition method, string attributeName)
        {
            foreach (var attribute in method.CustomAttributes)
            {
                if (attribute.AttributeType.FullName == attributeName)
                    return true;
            }
            return false;
        }

        private static void InsertLog(MemberReference method, ILProcessor code, Instruction before, string message)
        {
            // Load the string message onto the evaluation stack
            code.InsertBefore(before, Instruction.Create(OpCodes.Ldstr, message));
    
            // Ensure the correct Debug.Log method is imported
            var debugType = method.Module.ImportReference(typeof(UnityEngine.Debug)).Resolve();
            var logMethod = debugType.Methods.First(m => m.Name == "Log" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.Object");
            var importedLogMethod = method.Module.ImportReference(logMethod);
            
            // Call the Debug.Log method
            code.InsertBefore(before, Instruction.Create(OpCodes.Call, importedLogMethod));
        }
        
        private static void Error(ICollection<DiagnosticMessage> messages, string message, MethodDefinition method)
        {
            if (method.DebugInformation.HasSequencePoints)
            {
                var first = method.DebugInformation.SequencePoints[0];
                messages.Add(new DiagnosticMessage
                {
                    DiagnosticType = DiagnosticType.Error,
                    MessageData = message,
                    Column = first.StartColumn,
                    Line = first.StartLine,
                    File = first.Document.Url
                });
            }
        }
        
        private void HandleRPC(MethodDefinition method, [UsedImplicitly] List<DiagnosticMessage> messages)
        {
            if (method.ReturnType.FullName != typeof(void).FullName)
            {
                Error(messages, "ServerRPC method must return void", method);
                return;
            }
            
            var body = method.Body;
            var code = body.GetILProcessor();
            var first = body.Instructions[0];
            var fakeReturn = Instruction.Create(OpCodes.Ret);
            code.InsertBefore(first, fakeReturn);
            
            InsertLog(method, code, fakeReturn, "ServerRPC called: " + method.Name);
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

                var assemblyDefinition = AssemblyDefinition.ReadAssembly(peStream, new ReaderParameters
                {
                    ReadSymbols = true,
                    SymbolStream = pdbStream,
                    SymbolReaderProvider = new PortablePdbReaderProvider(),
                    AssemblyResolver = new AssemblyResolver(compiledAssembly)
                });

                for (var m = 0; m < assemblyDefinition.Modules.Count; m++)
                {
                    var module = assemblyDefinition.Modules[m];
                    for (var t = 0; t < module.Types.Count; t++)
                    {
                        var type = module.Types[t];
                        if (!type.IsClass)
                            continue;

                        var idFullName = typeof(NetworkIdentity).FullName;
                        if (type.FullName != idFullName && !InheritsFrom(type, typeof(NetworkIdentity).FullName))
                            continue;

                        for (var i = 0; i < type.Methods.Count; i++)
                        {
                            var method = type.Methods[i];

                            if (HasCustomAttribute(method, typeof(ServerRPCAttribute).FullName))
                            {
                                HandleRPC(method, messages);
                                /*messages.Add(new DiagnosticMessage
                                {
                                    DiagnosticType = DiagnosticType.Warning,
                                    MessageData = "ServerRPC found: " + type.FullName + "." + method.Name,
                                });*/
                            }
                        }

                        // messages.Add(new DiagnosticMessage
                        // {
                        //     DiagnosticType = DiagnosticType.Warning,
                        //     MessageData = "NetworkIdentity found: " + type.FullName,
                        // });
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

                assemblyDefinition.Write(pe, writerParameters);

                return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), messages);
            }
            catch (Exception e)
            {
                
                var messages = new List<DiagnosticMessage> {
                    new()
                    {
                        DiagnosticType = DiagnosticType.Error,
                        MessageData = e.Message,
                    }
                };
                
                return new ILPostProcessResult(compiledAssembly.InMemoryAssembly, messages);
            }
        }
    }
}
#endif