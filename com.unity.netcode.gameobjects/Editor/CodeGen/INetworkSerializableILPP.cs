using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using ILPPInterface = Unity.CompilationPipeline.Common.ILPostProcessing.ILPostProcessor;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace Unity.Netcode.Editor.CodeGen
{

    internal sealed class INetworkSerializableILPP : ILPPInterface
    {
        public override ILPPInterface GetInstance() => this;

        public override bool WillProcess(ICompiledAssembly compiledAssembly) =>
            compiledAssembly.Name == CodeGenHelpers.RuntimeAssemblyName ||
            compiledAssembly.References.Any(filePath => Path.GetFileNameWithoutExtension(filePath) == CodeGenHelpers.RuntimeAssemblyName);

        private readonly List<DiagnosticMessage> m_Diagnostics = new List<DiagnosticMessage>();

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!WillProcess(compiledAssembly))
            {
                return null;
            }

            m_Diagnostics.Clear();

            // read
            var assemblyDefinition = CodeGenHelpers.AssemblyDefinitionFor(compiledAssembly, out var resolver);
            if (assemblyDefinition == null)
            {
                m_Diagnostics.AddError($"Cannot read assembly definition: {compiledAssembly.Name}");
                return null;
            }

            // process
            var mainModule = assemblyDefinition.MainModule;
            if (mainModule != null)
            {
                try
                {
                    if (ImportReferences(mainModule))
                    {
                        var networkSerializableTypes = mainModule.GetTypes()
                            .Where(t => t.Resolve().HasInterface(CodeGenHelpers.INetworkSerializable_FullName) && !t.Resolve().IsAbstract && !t.Resolve().HasGenericParameters && t.Resolve().IsValueType)
                            .ToList();
                        var structTypes = mainModule.GetTypes()
                            .Where(t => t.Resolve().HasInterface(CodeGenHelpers.ISerializeByMemcpy_FullName) && !t.Resolve().IsAbstract && !t.Resolve().HasGenericParameters && t.Resolve().IsValueType)
                            .ToList();
                        var enumTypes = mainModule.GetTypes()
                            .Where(t => t.Resolve().IsEnum && !t.Resolve().IsAbstract && !t.Resolve().HasGenericParameters && t.Resolve().IsValueType)
                            .ToList();
                        if (networkSerializableTypes.Count + structTypes.Count + enumTypes.Count == 0)
                        {
                            return null;
                        }

                        CreateModuleInitializer(assemblyDefinition, networkSerializableTypes, structTypes, enumTypes);
                    }
                    else
                    {
                        m_Diagnostics.AddError($"Cannot import references into main module: {mainModule.Name}");
                    }
                }
                catch (Exception e)
                {
                    m_Diagnostics.AddError((e.ToString() + e.StackTrace.ToString()).Replace("\n", "|").Replace("\r", "|"));
                }
            }
            else
            {
                m_Diagnostics.AddError($"Cannot get main module from assembly definition: {compiledAssembly.Name}");
            }

            mainModule.RemoveRecursiveReferences();

            // write
            var pe = new MemoryStream();
            var pdb = new MemoryStream();

            var writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(),
                SymbolStream = pdb,
                WriteSymbols = true
            };

            assemblyDefinition.Write(pe, writerParameters);

            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), m_Diagnostics);
        }

        private MethodReference m_InitializeDelegatesNetworkSerializable_MethodRef;
        private MethodReference m_InitializeDelegatesStruct_MethodRef;
        private MethodReference m_InitializeDelegatesEnum_MethodRef;

        private const string k_InitializeNetworkSerializableMethodName = nameof(NetworkVariableHelper.InitializeDelegatesNetworkSerializable);
        private const string k_InitializeStructMethodName = nameof(NetworkVariableHelper.InitializeDelegatesStruct);
        private const string k_InitializeEnumMethodName = nameof(NetworkVariableHelper.InitializeDelegatesEnum);

        private bool ImportReferences(ModuleDefinition moduleDefinition)
        {

            var helperType = typeof(NetworkVariableHelper);
            foreach (var methodInfo in helperType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                switch (methodInfo.Name)
                {
                    case k_InitializeNetworkSerializableMethodName:
                        m_InitializeDelegatesNetworkSerializable_MethodRef = moduleDefinition.ImportReference(methodInfo);
                        break;
                    case k_InitializeStructMethodName:
                        m_InitializeDelegatesStruct_MethodRef = moduleDefinition.ImportReference(methodInfo);
                        break;
                    case k_InitializeEnumMethodName:
                        m_InitializeDelegatesEnum_MethodRef = moduleDefinition.ImportReference(methodInfo);
                        break;
                }
            }
            return true;
        }

        private MethodDefinition GetOrCreateStaticConstructor(TypeDefinition typeDefinition)
        {
            var staticCtorMethodDef = typeDefinition.GetStaticConstructor();
            if (staticCtorMethodDef == null)
            {
                staticCtorMethodDef = new MethodDefinition(
                    ".cctor", // Static Constructor (constant-constructor)
                    MethodAttributes.HideBySig |
                    MethodAttributes.SpecialName |
                    MethodAttributes.RTSpecialName |
                    MethodAttributes.Static,
                    typeDefinition.Module.TypeSystem.Void);
                staticCtorMethodDef.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                typeDefinition.Methods.Add(staticCtorMethodDef);
            }

            return staticCtorMethodDef;
        }

        // Creates a static module constructor (which is executed when the module is loaded) that registers all the
        // message types in the assembly with MessagingSystem.
        // This is the same behavior as annotating a static method with [ModuleInitializer] in standardized
        // C# (that attribute doesn't exist in Unity, but the static module constructor still works)
        // https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.moduleinitializerattribute?view=net-5.0
        // https://web.archive.org/web/20100212140402/http://blogs.msdn.com/junfeng/archive/2005/11/19/494914.aspx
        private void CreateModuleInitializer(AssemblyDefinition assembly, List<TypeDefinition> networkSerializableTypes, List<TypeDefinition> structTypes, List<TypeDefinition> enumTypes)
        {
            foreach (var typeDefinition in assembly.MainModule.Types)
            {
                if (typeDefinition.FullName == "<Module>")
                {
                    var staticCtorMethodDef = GetOrCreateStaticConstructor(typeDefinition);

                    var processor = staticCtorMethodDef.Body.GetILProcessor();

                    var instructions = new List<Instruction>();

                    foreach (var type in structTypes)
                    {
                        var method = new GenericInstanceMethod(m_InitializeDelegatesStruct_MethodRef);
                        method.GenericArguments.Add(type);
                        instructions.Add(processor.Create(OpCodes.Call, method));
                    }

                    foreach (var type in networkSerializableTypes)
                    {
                        var method = new GenericInstanceMethod(m_InitializeDelegatesNetworkSerializable_MethodRef);
                        method.GenericArguments.Add(type);
                        instructions.Add(processor.Create(OpCodes.Call, method));
                    }

                    foreach (var type in enumTypes)
                    {
                        var method = new GenericInstanceMethod(m_InitializeDelegatesEnum_MethodRef);
                        method.GenericArguments.Add(type);
                        instructions.Add(processor.Create(OpCodes.Call, method));
                    }

                    instructions.ForEach(instruction => processor.Body.Instructions.Insert(processor.Body.Instructions.Count - 1, instruction));
                    break;
                }
            }
        }
    }
}
