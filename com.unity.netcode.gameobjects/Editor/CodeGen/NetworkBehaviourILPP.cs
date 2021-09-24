using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.Collections;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using ILPPInterface = Unity.CompilationPipeline.Common.ILPostProcessing.ILPostProcessor;

namespace Unity.Netcode.Editor.CodeGen
{

    internal sealed class NetworkBehaviourILPP : ILPPInterface
    {
        private const string k_ExtensionMethodReadName = "ReadValueSafe";
        private const string k_ExtensionMethodWriteName = "WriteValueSafe";

        public override ILPPInterface GetInstance() => this;

        public override bool WillProcess(ICompiledAssembly compiledAssembly) => compiledAssembly.References.Any(filePath => Path.GetFileNameWithoutExtension(filePath) == CodeGenHelpers.RuntimeAssemblyName);

        private readonly List<DiagnosticMessage> m_Diagnostics = new List<DiagnosticMessage>();

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!WillProcess(compiledAssembly))
            {
                return null;
            }


            m_Diagnostics.Clear();

            // read
            var assemblyDefinition = CodeGenHelpers.AssemblyDefinitionFor(compiledAssembly, out m_AssemblyResolver);
            if (assemblyDefinition == null)
            {
                m_Diagnostics.AddError($"Cannot read assembly definition: {compiledAssembly.Name}");
                return null;
            }

            // process
            var mainModule = assemblyDefinition.MainModule;
            if (mainModule != null)
            {
                m_MainModule = mainModule;
                if (ImportReferences(mainModule))
                {
                    // process `NetworkBehaviour` types
                    try
                    {
                        mainModule.GetTypes()
                            .Where(t => t.IsSubclassOf(CodeGenHelpers.NetworkBehaviour_FullName))
                            .ToList()
                            .ForEach(b => ProcessNetworkBehaviour(b, compiledAssembly.Defines));
                    }
                    catch (Exception e)
                    {
                        m_Diagnostics.AddError((e.ToString() + e.StackTrace.ToString()).Replace("\n", "|").Replace("\r", "|"));
                    }
                }
                else
                {
                    m_Diagnostics.AddError($"Cannot import references into main module: {mainModule.Name}");
                }
            }
            else
            {
                m_Diagnostics.AddError($"Cannot get main module from assembly definition: {compiledAssembly.Name}");
            }

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

        private ModuleDefinition m_MainModule;
        private PostProcessorAssemblyResolver m_AssemblyResolver;

        private MethodReference m_Debug_LogError_MethodRef;
        private TypeReference m_NetworkManager_TypeRef;
        private MethodReference m_NetworkManager_getLocalClientId_MethodRef;
        private MethodReference m_NetworkManager_getIsListening_MethodRef;
        private MethodReference m_NetworkManager_getIsHost_MethodRef;
        private MethodReference m_NetworkManager_getIsServer_MethodRef;
        private MethodReference m_NetworkManager_getIsClient_MethodRef;
        private FieldReference m_NetworkManager_LogLevel_FieldRef;
        private FieldReference m_NetworkManager_rpc_func_table_FieldRef;
        private MethodReference m_NetworkManager_rpc_func_table_Add_MethodRef;
        private FieldReference m_NetworkManager_rpc_name_table_FieldRef;
        private MethodReference m_NetworkManager_rpc_name_table_Add_MethodRef;
        private TypeReference m_NetworkBehaviour_TypeRef;
        private MethodReference m_NetworkBehaviour_SendServerRpc_MethodRef;
        private MethodReference m_NetworkBehaviour_SendClientRpc_MethodRef;
        private FieldReference m_NetworkBehaviour_rpc_exec_stage_FieldRef;
        private MethodReference m_NetworkBehaviour_getNetworkManager_MethodRef;
        private MethodReference m_NetworkBehaviour_getOwnerClientId_MethodRef;
        private MethodReference m_NetworkHandlerDelegateCtor_MethodRef;
        private TypeReference m_RpcParams_TypeRef;
        private FieldReference m_RpcParams_Server_FieldRef;
        private FieldReference m_RpcParams_Client_FieldRef;
        private TypeReference m_ServerRpcParams_TypeRef;
        private FieldReference m_ServerRpcParams_Receive_FieldRef;
        private FieldReference m_ServerRpcParams_Receive_SenderClientId_FieldRef;
        private TypeReference m_ClientRpcParams_TypeRef;

        private TypeReference m_FastBufferWriter_TypeRef;
        private MethodReference m_FastBufferWriter_Constructor;
        private MethodReference m_FastBufferWriter_Dispose;
        private Dictionary<string, MethodReference> m_FastBufferWriter_WriteValue_MethodRefs = new Dictionary<string, MethodReference>();
        private List<MethodReference> m_FastBufferWriter_ExtensionMethodRefs = new List<MethodReference>();

        private TypeReference m_FastBufferReader_TypeRef;
        private Dictionary<string, MethodReference> m_FastBufferReader_ReadValue_MethodRefs = new Dictionary<string, MethodReference>();
        private List<MethodReference> m_FastBufferReader_ExtensionMethodRefs = new List<MethodReference>();

        private const string k_Debug_LogError = nameof(Debug.LogError);
        private const string k_NetworkManager_LocalClientId = nameof(NetworkManager.LocalClientId);
        private const string k_NetworkManager_IsListening = nameof(NetworkManager.IsListening);
        private const string k_NetworkManager_IsHost = nameof(NetworkManager.IsHost);
        private const string k_NetworkManager_IsServer = nameof(NetworkManager.IsServer);
        private const string k_NetworkManager_IsClient = nameof(NetworkManager.IsClient);
        private const string k_NetworkManager_LogLevel = nameof(NetworkManager.LogLevel);
        private const string k_NetworkManager_rpc_func_table = nameof(NetworkManager.__rpc_func_table);
        private const string k_NetworkManager_rpc_name_table = nameof(NetworkManager.__rpc_name_table);

        private const string k_NetworkBehaviour_rpc_exec_stage = nameof(NetworkBehaviour.__rpc_exec_stage);
        private const string k_NetworkBehaviour_SendServerRpc = nameof(NetworkBehaviour.__sendServerRpc);
        private const string k_NetworkBehaviour_SendClientRpc = nameof(NetworkBehaviour.__sendClientRpc);
        private const string k_NetworkBehaviour_NetworkManager = nameof(NetworkBehaviour.NetworkManager);
        private const string k_NetworkBehaviour_OwnerClientId = nameof(NetworkBehaviour.OwnerClientId);

        private const string k_RpcAttribute_Delivery = nameof(RpcAttribute.Delivery);
        private const string k_ServerRpcAttribute_RequireOwnership = nameof(ServerRpcAttribute.RequireOwnership);
        private const string k_RpcParams_Server = nameof(__RpcParams.Server);
        private const string k_RpcParams_Client = nameof(__RpcParams.Client);
        private const string k_ServerRpcParams_Receive = nameof(ServerRpcParams.Receive);
        private const string k_ServerRpcReceiveParams_SenderClientId = nameof(ServerRpcReceiveParams.SenderClientId);

        private bool ImportReferences(ModuleDefinition moduleDefinition)
        {
            var debugType = typeof(Debug);
            foreach (var methodInfo in debugType.GetMethods())
            {
                switch (methodInfo.Name)
                {
                    case k_Debug_LogError:
                        if (methodInfo.GetParameters().Length == 1)
                        {
                            m_Debug_LogError_MethodRef = moduleDefinition.ImportReference(methodInfo);
                        }

                        break;
                }
            }

            var networkManagerType = typeof(NetworkManager);
            m_NetworkManager_TypeRef = moduleDefinition.ImportReference(networkManagerType);
            foreach (var propertyInfo in networkManagerType.GetProperties())
            {
                switch (propertyInfo.Name)
                {
                    case k_NetworkManager_LocalClientId:
                        m_NetworkManager_getLocalClientId_MethodRef = moduleDefinition.ImportReference(propertyInfo.GetMethod);
                        break;
                    case k_NetworkManager_IsListening:
                        m_NetworkManager_getIsListening_MethodRef = moduleDefinition.ImportReference(propertyInfo.GetMethod);
                        break;
                    case k_NetworkManager_IsHost:
                        m_NetworkManager_getIsHost_MethodRef = moduleDefinition.ImportReference(propertyInfo.GetMethod);
                        break;
                    case k_NetworkManager_IsServer:
                        m_NetworkManager_getIsServer_MethodRef = moduleDefinition.ImportReference(propertyInfo.GetMethod);
                        break;
                    case k_NetworkManager_IsClient:
                        m_NetworkManager_getIsClient_MethodRef = moduleDefinition.ImportReference(propertyInfo.GetMethod);
                        break;
                }
            }

            foreach (var fieldInfo in networkManagerType.GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                switch (fieldInfo.Name)
                {
                    case k_NetworkManager_LogLevel:
                        m_NetworkManager_LogLevel_FieldRef = moduleDefinition.ImportReference(fieldInfo);
                        break;
                    case k_NetworkManager_rpc_func_table:
                        m_NetworkManager_rpc_func_table_FieldRef = moduleDefinition.ImportReference(fieldInfo);
                        m_NetworkManager_rpc_func_table_Add_MethodRef = moduleDefinition.ImportReference(fieldInfo.FieldType.GetMethod("Add"));
                        break;
                    case k_NetworkManager_rpc_name_table:
                        m_NetworkManager_rpc_name_table_FieldRef = moduleDefinition.ImportReference(fieldInfo);
                        m_NetworkManager_rpc_name_table_Add_MethodRef = moduleDefinition.ImportReference(fieldInfo.FieldType.GetMethod("Add"));
                        break;
                }
            }

            var networkBehaviourType = typeof(NetworkBehaviour);
            m_NetworkBehaviour_TypeRef = moduleDefinition.ImportReference(networkBehaviourType);
            foreach (var propertyInfo in networkBehaviourType.GetProperties())
            {
                switch (propertyInfo.Name)
                {
                    case k_NetworkBehaviour_NetworkManager:
                        m_NetworkBehaviour_getNetworkManager_MethodRef = moduleDefinition.ImportReference(propertyInfo.GetMethod);
                        break;
                    case k_NetworkBehaviour_OwnerClientId:
                        m_NetworkBehaviour_getOwnerClientId_MethodRef = moduleDefinition.ImportReference(propertyInfo.GetMethod);
                        break;
                }
            }

            foreach (var methodInfo in networkBehaviourType.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                switch (methodInfo.Name)
                {
                    case k_NetworkBehaviour_SendServerRpc:
                        m_NetworkBehaviour_SendServerRpc_MethodRef = moduleDefinition.ImportReference(methodInfo);
                        break;
                    case k_NetworkBehaviour_SendClientRpc:
                        m_NetworkBehaviour_SendClientRpc_MethodRef = moduleDefinition.ImportReference(methodInfo);
                        break;
                }
            }

            foreach (var fieldInfo in networkBehaviourType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                switch (fieldInfo.Name)
                {
                    case k_NetworkBehaviour_rpc_exec_stage:
                        m_NetworkBehaviour_rpc_exec_stage_FieldRef = moduleDefinition.ImportReference(fieldInfo);
                        break;
                }
            }

            var networkHandlerDelegateType = typeof(NetworkManager.RpcReceiveHandler);
            m_NetworkHandlerDelegateCtor_MethodRef = moduleDefinition.ImportReference(networkHandlerDelegateType.GetConstructor(new[] { typeof(object), typeof(IntPtr) }));

            var rpcParamsType = typeof(__RpcParams);
            m_RpcParams_TypeRef = moduleDefinition.ImportReference(rpcParamsType);
            foreach (var fieldInfo in rpcParamsType.GetFields())
            {
                switch (fieldInfo.Name)
                {
                    case k_RpcParams_Server:
                        m_RpcParams_Server_FieldRef = moduleDefinition.ImportReference(fieldInfo);
                        break;
                    case k_RpcParams_Client:
                        m_RpcParams_Client_FieldRef = moduleDefinition.ImportReference(fieldInfo);
                        break;
                }
            }

            var serverRpcParamsType = typeof(ServerRpcParams);
            m_ServerRpcParams_TypeRef = moduleDefinition.ImportReference(serverRpcParamsType);
            foreach (var fieldInfo in serverRpcParamsType.GetFields())
            {
                switch (fieldInfo.Name)
                {
                    case k_ServerRpcParams_Receive:
                        foreach (var recvFieldInfo in fieldInfo.FieldType.GetFields())
                        {
                            switch (recvFieldInfo.Name)
                            {
                                case k_ServerRpcReceiveParams_SenderClientId:
                                    m_ServerRpcParams_Receive_SenderClientId_FieldRef = moduleDefinition.ImportReference(recvFieldInfo);
                                    break;
                            }
                        }

                        m_ServerRpcParams_Receive_FieldRef = moduleDefinition.ImportReference(fieldInfo);
                        break;
                }
            }

            var clientRpcParamsType = typeof(ClientRpcParams);
            m_ClientRpcParams_TypeRef = moduleDefinition.ImportReference(clientRpcParamsType);

            var fastBufferWriterType = typeof(FastBufferWriter);
            m_FastBufferWriter_TypeRef = moduleDefinition.ImportReference(fastBufferWriterType);

            m_FastBufferWriter_Constructor = moduleDefinition.ImportReference(
                fastBufferWriterType.GetConstructor(new[] { typeof(int), typeof(Allocator), typeof(int) }));
            m_FastBufferWriter_Dispose = moduleDefinition.ImportReference(fastBufferWriterType.GetMethod("Dispose"));

            var fastBufferReaderType = typeof(FastBufferReader);
            m_FastBufferReader_TypeRef = moduleDefinition.ImportReference(fastBufferReaderType);

            // Find all extension methods for FastBufferReader and FastBufferWriter to enable user-implemented
            // methods to be called.
            var assemblies = new List<AssemblyDefinition>();
            assemblies.Add(m_MainModule.Assembly);
            foreach (var reference in m_MainModule.AssemblyReferences)
            {
                assemblies.Add(m_AssemblyResolver.Resolve(reference));
            }

            var extensionConstructor =
                moduleDefinition.ImportReference(typeof(ExtensionAttribute).GetConstructor(new Type[] { }));
            foreach (var assembly in assemblies)
            {
                foreach (var module in assembly.Modules)
                {
                    foreach (var type in module.Types)
                    {
                        var resolvedType = type.Resolve();
                        if (!resolvedType.IsSealed || !resolvedType.IsAbstract || resolvedType.IsNested)
                        {
                            continue;
                        }
                        foreach (var method in type.Methods)
                        {
                            if (!method.IsStatic)
                            {
                                continue;
                            }

                            var isExtension = false;

                            foreach (var attr in method.CustomAttributes)
                            {
                                if (attr.Constructor.Resolve() == extensionConstructor.Resolve())
                                {
                                    isExtension = true;
                                }
                            }

                            if (!isExtension)
                            {
                                continue;
                            }

                            var parameters = method.Parameters;

                            if (parameters.Count == 2
                                && parameters[0].ParameterType.Resolve() == m_FastBufferWriter_TypeRef.MakeByReferenceType().Resolve())
                            {
                                m_FastBufferWriter_ExtensionMethodRefs.Add(m_MainModule.ImportReference(method));
                            }
                            else if (parameters.Count == 2
                                && parameters[0].ParameterType.Resolve() == m_FastBufferReader_TypeRef.MakeByReferenceType().Resolve())
                            {
                                m_FastBufferReader_ExtensionMethodRefs.Add(m_MainModule.ImportReference(method));
                            }
                        }
                    }
                }
            }

            return true;
        }

        private void ProcessNetworkBehaviour(TypeDefinition typeDefinition, string[] assemblyDefines)
        {
            var rpcHandlers = new List<(uint RpcMethodId, MethodDefinition RpcHandler)>();
            var rpcNames = new List<(uint RpcMethodId, string RpcMethodName)>();

            bool isEditorOrDevelopment = assemblyDefines.Contains("UNITY_EDITOR") || assemblyDefines.Contains("DEVELOPMENT_BUILD");

            foreach (var methodDefinition in typeDefinition.Methods)
            {
                var rpcAttribute = CheckAndGetRpcAttribute(methodDefinition);
                if (rpcAttribute == null)
                {
                    continue;
                }

                var rpcMethodId = methodDefinition.Hash();
                if (rpcMethodId == 0)
                {
                    continue;
                }

                InjectWriteAndCallBlocks(methodDefinition, rpcAttribute, rpcMethodId);

                rpcHandlers.Add((rpcMethodId, GenerateStaticHandler(methodDefinition, rpcAttribute)));

                if (isEditorOrDevelopment)
                {
                    rpcNames.Add((rpcMethodId, methodDefinition.Name));
                }
            }

            if (rpcHandlers.Count > 0 || rpcNames.Count > 0)
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

                var instructions = new List<Instruction>();
                var processor = staticCtorMethodDef.Body.GetILProcessor();

                foreach (var (rpcMethodId, rpcHandler) in rpcHandlers)
                {
                    typeDefinition.Methods.Add(rpcHandler);

                    // NetworkManager.__rpc_func_table.Add(RpcMethodId, HandleFunc);
                    instructions.Add(processor.Create(OpCodes.Ldsfld, m_NetworkManager_rpc_func_table_FieldRef));
                    instructions.Add(processor.Create(OpCodes.Ldc_I4, unchecked((int)rpcMethodId)));
                    instructions.Add(processor.Create(OpCodes.Ldnull));
                    instructions.Add(processor.Create(OpCodes.Ldftn, rpcHandler));
                    instructions.Add(processor.Create(OpCodes.Newobj, m_NetworkHandlerDelegateCtor_MethodRef));
                    instructions.Add(processor.Create(OpCodes.Call, m_NetworkManager_rpc_func_table_Add_MethodRef));
                }

                foreach (var (rpcMethodId, rpcMethodName) in rpcNames)
                {
                    // NetworkManager.__rpc_name_table.Add(RpcMethodId, RpcMethodName);
                    instructions.Add(processor.Create(OpCodes.Ldsfld, m_NetworkManager_rpc_name_table_FieldRef));
                    instructions.Add(processor.Create(OpCodes.Ldc_I4, unchecked((int)rpcMethodId)));
                    instructions.Add(processor.Create(OpCodes.Ldstr, rpcMethodName));
                    instructions.Add(processor.Create(OpCodes.Call, m_NetworkManager_rpc_name_table_Add_MethodRef));
                }

                instructions.Reverse();
                instructions.ForEach(instruction => processor.Body.Instructions.Insert(0, instruction));
            }

            // override NetworkBehaviour.__getTypeName() method to return concrete type
            {
                var networkBehaviour_TypeDef = m_NetworkBehaviour_TypeRef.Resolve();
                var baseGetTypeNameMethod = networkBehaviour_TypeDef.Methods.First(p => p.Name.Equals(nameof(NetworkBehaviour.__getTypeName)));

                var newGetTypeNameMethod = new MethodDefinition(
                    nameof(NetworkBehaviour.__getTypeName),
                    (baseGetTypeNameMethod.Attributes & ~MethodAttributes.NewSlot) | MethodAttributes.ReuseSlot,
                    baseGetTypeNameMethod.ReturnType)
                {
                    ImplAttributes = baseGetTypeNameMethod.ImplAttributes,
                    SemanticsAttributes = baseGetTypeNameMethod.SemanticsAttributes
                };

                var processor = newGetTypeNameMethod.Body.GetILProcessor();
                processor.Body.Instructions.Add(processor.Create(OpCodes.Ldstr, typeDefinition.Name));
                processor.Body.Instructions.Add(processor.Create(OpCodes.Ret));

                typeDefinition.Methods.Add(newGetTypeNameMethod);
            }

            // Weird behavior from Cecil: When importing a reference to a specific implementation of a generic
            // method, it's importing the main module as a reference into itself. This causes Unity to have issues
            // when attempting to iterate the assemblies to discover unit tests, as it goes into infinite recursion
            // and eventually hits a stack overflow. I wasn't able to find any way to stop Cecil from importing the module
            // into itself, so at the end of it all, we're just going to go back and remove it again.
            var moduleName = m_MainModule.Name;
            if (moduleName.EndsWith(".dll") || moduleName.EndsWith(".exe"))
            {
                moduleName = moduleName.Substring(0, moduleName.Length - 4);
            }

            foreach (var reference in m_MainModule.AssemblyReferences)
            {
                var referenceName = reference.Name.Split(',')[0];
                if (referenceName.EndsWith(".dll") || referenceName.EndsWith(".exe"))
                {
                    referenceName = referenceName.Substring(0, referenceName.Length - 4);
                }

                if (moduleName == referenceName)
                {
                    try
                    {
                        m_MainModule.AssemblyReferences.Remove(reference);
                        break;
                    }
                    catch (Exception e)
                    {
                        //
                    }
                }
            }
        }

        private CustomAttribute CheckAndGetRpcAttribute(MethodDefinition methodDefinition)
        {
            CustomAttribute rpcAttribute = null;
            bool isServerRpc = false;
            foreach (var customAttribute in methodDefinition.CustomAttributes)
            {
                var customAttributeType_FullName = customAttribute.AttributeType.FullName;

                if (customAttributeType_FullName == CodeGenHelpers.ServerRpcAttribute_FullName ||
                    customAttributeType_FullName == CodeGenHelpers.ClientRpcAttribute_FullName)
                {
                    bool isValid = true;

                    if (methodDefinition.IsStatic)
                    {
                        m_Diagnostics.AddError(methodDefinition, "RPC method must not be static!");
                        isValid = false;
                    }

                    if (methodDefinition.IsAbstract)
                    {
                        m_Diagnostics.AddError(methodDefinition, "RPC method must not be abstract!");
                        isValid = false;
                    }

                    if (methodDefinition.ReturnType != methodDefinition.Module.TypeSystem.Void)
                    {
                        m_Diagnostics.AddError(methodDefinition, "RPC method must return `void`!");
                        isValid = false;
                    }

                    if (customAttributeType_FullName == CodeGenHelpers.ServerRpcAttribute_FullName &&
                        !methodDefinition.Name.EndsWith("ServerRpc", StringComparison.OrdinalIgnoreCase))
                    {
                        m_Diagnostics.AddError(methodDefinition, "ServerRpc method must end with 'ServerRpc' suffix!");
                        isValid = false;
                    }

                    if (customAttributeType_FullName == CodeGenHelpers.ClientRpcAttribute_FullName &&
                        !methodDefinition.Name.EndsWith("ClientRpc", StringComparison.OrdinalIgnoreCase))
                    {
                        m_Diagnostics.AddError(methodDefinition, "ClientRpc method must end with 'ClientRpc' suffix!");
                        isValid = false;
                    }

                    if (isValid)
                    {
                        isServerRpc = customAttributeType_FullName == CodeGenHelpers.ServerRpcAttribute_FullName;
                        rpcAttribute = customAttribute;
                    }
                }
            }

            if (rpcAttribute == null)
            {
                if (methodDefinition.Name.EndsWith("ServerRpc", StringComparison.OrdinalIgnoreCase))
                {
                    m_Diagnostics.AddError(methodDefinition, "ServerRpc method must be marked with 'ServerRpc' attribute!");
                }
                else if (methodDefinition.Name.EndsWith("ClientRpc", StringComparison.OrdinalIgnoreCase))
                {
                    m_Diagnostics.AddError(methodDefinition, "ClientRpc method must be marked with 'ClientRpc' attribute!");
                }

                return null;
            }
            // Checks for IsSerializable are moved to later as the check is now done by dynamically seeing if any valid
            // serializer OR extension method exists for it.
            return rpcAttribute;
        }

        private MethodReference GetFastBufferWriterWriteMethod(string name, TypeReference paramType)
        {
            foreach (var method in m_FastBufferWriter_TypeRef.Resolve().Methods)
            {
                if (method.Name == name)
                {
                    var parameters = method.Parameters;

                    if (parameters.Count == 0 || (parameters.Count > 1 && !parameters[1].IsOptional))
                    {
                        continue;
                    }

                    if (parameters[0].ParameterType.IsArray != paramType.IsArray)
                    {
                        continue;
                    }

                    var checkType = paramType.Resolve();
                    if (paramType.IsArray)
                    {
                        checkType = paramType.GetElementType().Resolve();
                    }

                    if (
                        (parameters[0].ParameterType.Resolve() == checkType
                        || (parameters[0].ParameterType.Resolve() == checkType.MakeByReferenceType().Resolve() && parameters[0].IsIn)))
                    {
                        return method;
                    }
                    if (method.HasGenericParameters && method.GenericParameters.Count == 1)
                    {
                        if (method.GenericParameters[0].HasConstraints)
                        {
                            foreach (var constraint in method.GenericParameters[0].Constraints)
                            {
                                var resolvedConstraint = constraint.Resolve();

                                if (
                                    (resolvedConstraint.IsInterface &&
                                     checkType.HasInterface(resolvedConstraint.FullName))
                                    || (resolvedConstraint.IsClass &&
                                        checkType.Resolve().IsSubclassOf(resolvedConstraint.FullName)))
                                {
                                    var instanceMethod = new GenericInstanceMethod(method);
                                    instanceMethod.GenericArguments.Add(checkType);
                                    return instanceMethod;
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        private bool GetWriteMethodForParameter(TypeReference paramType, out MethodReference methodRef)
        {
            var assemblyQualifiedName = paramType.FullName + ", " + paramType.Resolve().Module.Assembly.FullName;
            var foundMethodRef = m_FastBufferWriter_WriteValue_MethodRefs.TryGetValue(assemblyQualifiedName, out methodRef);

            if (!foundMethodRef)
            {
                foreach (var method in m_FastBufferWriter_ExtensionMethodRefs)
                {
                    var parameters = method.Resolve().Parameters;

                    if (method.Name == k_ExtensionMethodWriteName)
                    {
                        if (parameters[1].IsIn)
                        {
                            if (parameters[1].ParameterType.Resolve() == paramType.MakeByReferenceType().Resolve()
                                && ((ByReferenceType)parameters[1].ParameterType).ElementType.IsArray == paramType.IsArray)
                            {
                                methodRef = method;
                                m_FastBufferWriter_WriteValue_MethodRefs[assemblyQualifiedName] = methodRef;
                                return true;
                            }
                        }
                        else
                        {

                            if (parameters[1].ParameterType.Resolve() == paramType.Resolve()
                                && parameters[1].ParameterType.IsArray == paramType.IsArray)
                            {
                                methodRef = method;
                                m_FastBufferWriter_WriteValue_MethodRefs[assemblyQualifiedName] = methodRef;
                                return true;
                            }
                        }
                    }
                }

                // Try NetworkSerializable first because INetworkSerializable may also be valid for WriteValueSafe
                // and that would cause boxing if so.
                var typeMethod = GetFastBufferWriterWriteMethod("WriteNetworkSerializable", paramType);
                if (typeMethod == null)
                {
                    typeMethod = GetFastBufferWriterWriteMethod(k_ExtensionMethodWriteName, paramType);
                }
                if (typeMethod != null)
                {
                    methodRef = m_MainModule.ImportReference(typeMethod);
                    m_FastBufferWriter_WriteValue_MethodRefs[assemblyQualifiedName] = methodRef;
                    foundMethodRef = true;
                }
            }

            return foundMethodRef;
        }
        private MethodReference GetFastBufferReaderReadMethod(string name, TypeReference paramType)
        {
            foreach (var method in m_FastBufferReader_TypeRef.Resolve().Methods)
            {
                var paramTypeDef = paramType.Resolve();
                if (method.Name == name)
                {
                    var parameters = method.Parameters;

                    if (parameters.Count == 0 || (parameters.Count > 1 && !parameters[1].IsOptional))
                    {
                        continue;
                    }

                    if (!parameters[0].IsOut)
                    {
                        return null;
                    }

                    var methodParam = ((ByReferenceType)parameters[0].ParameterType).ElementType;

                    if (methodParam.IsArray != paramType.IsArray)
                    {
                        continue;
                    }

                    var checkType = paramType.Resolve();
                    if (paramType.IsArray)
                    {
                        checkType = paramType.GetElementType().Resolve();
                    }

                    if (methodParam.Resolve() == checkType.Resolve() || methodParam.Resolve() == checkType.MakeByReferenceType().Resolve())
                    {
                        return method;
                    }
                    if (method.HasGenericParameters && method.GenericParameters.Count == 1)
                    {
                        if (method.GenericParameters[0].HasConstraints)
                        {
                            foreach (var constraint in method.GenericParameters[0].Constraints)
                            {
                                var resolvedConstraint = constraint.Resolve();

                                if (
                                    (resolvedConstraint.IsInterface &&
                                     checkType.HasInterface(resolvedConstraint.FullName))
                                    || (resolvedConstraint.IsClass &&
                                        checkType.Resolve().IsSubclassOf(resolvedConstraint.FullName)))
                                {
                                    var instanceMethod = new GenericInstanceMethod(method);
                                    instanceMethod.GenericArguments.Add(checkType);
                                    return instanceMethod;
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        private bool GetReadMethodForParameter(TypeReference paramType, out MethodReference methodRef)
        {
            var assemblyQualifiedName = paramType.FullName + ", " + paramType.Resolve().Module.Assembly.FullName;

            var foundMethodRef = m_FastBufferReader_ReadValue_MethodRefs.TryGetValue(assemblyQualifiedName, out methodRef);
            if (!foundMethodRef)
            {
                foreach (var method in m_FastBufferReader_ExtensionMethodRefs)
                {
                    var parameters = method.Resolve().Parameters;
                    if (
                        method.Name == k_ExtensionMethodReadName
                        && parameters[1].IsOut
                        && parameters[1].ParameterType.Resolve() == paramType.MakeByReferenceType().Resolve()
                        && ((ByReferenceType)parameters[1].ParameterType).ElementType.IsArray == paramType.IsArray)
                    {
                        methodRef = method;
                        m_FastBufferReader_ReadValue_MethodRefs[assemblyQualifiedName] = methodRef;
                        return true;
                    }
                }

                // Try NetworkSerializable first because INetworkSerializable may also be valid for ReadValueSafe
                // and that would cause boxing if so.
                var typeMethod = GetFastBufferReaderReadMethod("ReadNetworkSerializable", paramType);
                if (typeMethod == null)
                {
                    typeMethod = GetFastBufferReaderReadMethod(k_ExtensionMethodReadName, paramType);
                }
                if (typeMethod != null)
                {
                    methodRef = m_MainModule.ImportReference(typeMethod);
                    m_FastBufferReader_ReadValue_MethodRefs[assemblyQualifiedName] = methodRef;
                    foundMethodRef = true;
                }
            }

            return foundMethodRef;
        }

        private void InjectWriteAndCallBlocks(MethodDefinition methodDefinition, CustomAttribute rpcAttribute, uint rpcMethodId)
        {
            var typeSystem = methodDefinition.Module.TypeSystem;
            var instructions = new List<Instruction>();
            var processor = methodDefinition.Body.GetILProcessor();
            var isServerRpc = rpcAttribute.AttributeType.FullName == CodeGenHelpers.ServerRpcAttribute_FullName;
            var requireOwnership = true; // default value MUST be = `ServerRpcAttribute.RequireOwnership`
            var rpcDelivery = RpcDelivery.Reliable; // default value MUST be = `RpcAttribute.Delivery`
            foreach (var attrField in rpcAttribute.Fields)
            {
                switch (attrField.Name)
                {
                    case k_RpcAttribute_Delivery:
                        rpcDelivery = (RpcDelivery)attrField.Argument.Value;
                        break;
                    case k_ServerRpcAttribute_RequireOwnership:
                        requireOwnership = attrField.Argument.Type == typeSystem.Boolean && (bool)attrField.Argument.Value;
                        break;
                }
            }

            var paramCount = methodDefinition.Parameters.Count;
            var hasRpcParams =
                paramCount > 0 &&
                ((isServerRpc && methodDefinition.Parameters[paramCount - 1].ParameterType.FullName == CodeGenHelpers.ServerRpcParams_FullName) ||
                 (!isServerRpc && methodDefinition.Parameters[paramCount - 1].ParameterType.FullName == CodeGenHelpers.ClientRpcParams_FullName));

            methodDefinition.Body.InitLocals = true;
            // NetworkManager networkManager;
            methodDefinition.Body.Variables.Add(new VariableDefinition(m_NetworkManager_TypeRef));
            int netManLocIdx = methodDefinition.Body.Variables.Count - 1;
            // NetworkSerializer serializer;
            methodDefinition.Body.Variables.Add(new VariableDefinition(m_FastBufferWriter_TypeRef));
            int serializerLocIdx = methodDefinition.Body.Variables.Count - 1;

            // XXXRpcParams
            if (!hasRpcParams)
            {
                methodDefinition.Body.Variables.Add(new VariableDefinition(isServerRpc ? m_ServerRpcParams_TypeRef : m_ClientRpcParams_TypeRef));
            }
            int rpcParamsIdx = !hasRpcParams ? methodDefinition.Body.Variables.Count - 1 : -1;

            {
                var returnInstr = processor.Create(OpCodes.Ret);
                var lastInstr = processor.Create(OpCodes.Nop);

                // networkManager = this.NetworkManager;
                instructions.Add(processor.Create(OpCodes.Ldarg_0));
                instructions.Add(processor.Create(OpCodes.Call, m_NetworkBehaviour_getNetworkManager_MethodRef));
                instructions.Add(processor.Create(OpCodes.Stloc, netManLocIdx));

                // if (networkManager == null || !networkManager.IsListening) return;
                instructions.Add(processor.Create(OpCodes.Ldloc, netManLocIdx));
                instructions.Add(processor.Create(OpCodes.Brfalse, returnInstr));
                instructions.Add(processor.Create(OpCodes.Ldloc, netManLocIdx));
                instructions.Add(processor.Create(OpCodes.Callvirt, m_NetworkManager_getIsListening_MethodRef));
                instructions.Add(processor.Create(OpCodes.Brtrue, lastInstr));

                instructions.Add(returnInstr);
                instructions.Add(lastInstr);
            }

            {
                var beginInstr = processor.Create(OpCodes.Nop);
                var endInstr = processor.Create(OpCodes.Nop);
                var lastInstr = processor.Create(OpCodes.Nop);

                // if (__rpc_exec_stage != __RpcExecStage.Server) -> ServerRpc
                // if (__rpc_exec_stage != __RpcExecStage.Client) -> ClientRpc
                instructions.Add(processor.Create(OpCodes.Ldarg_0));
                instructions.Add(processor.Create(OpCodes.Ldfld, m_NetworkBehaviour_rpc_exec_stage_FieldRef));
                instructions.Add(processor.Create(OpCodes.Ldc_I4, (int)(isServerRpc ? NetworkBehaviour.__RpcExecStage.Server : NetworkBehaviour.__RpcExecStage.Client)));
                instructions.Add(processor.Create(OpCodes.Ceq));
                instructions.Add(processor.Create(OpCodes.Ldc_I4, 0));
                instructions.Add(processor.Create(OpCodes.Ceq));
                instructions.Add(processor.Create(OpCodes.Brfalse, lastInstr));

                // if (networkManager.IsClient || networkManager.IsHost) { ... } -> ServerRpc
                // if (networkManager.IsServer || networkManager.IsHost) { ... } -> ClientRpc
                instructions.Add(processor.Create(OpCodes.Ldloc, netManLocIdx));
                instructions.Add(processor.Create(OpCodes.Callvirt, isServerRpc ? m_NetworkManager_getIsClient_MethodRef : m_NetworkManager_getIsServer_MethodRef));
                instructions.Add(processor.Create(OpCodes.Brtrue, beginInstr));
                instructions.Add(processor.Create(OpCodes.Ldloc, netManLocIdx));
                instructions.Add(processor.Create(OpCodes.Callvirt, m_NetworkManager_getIsHost_MethodRef));
                instructions.Add(processor.Create(OpCodes.Brfalse, lastInstr));

                instructions.Add(beginInstr);

                if (isServerRpc)
                {
                    // ServerRpc

                    if (requireOwnership)
                    {
                        var roReturnInstr = processor.Create(OpCodes.Ret);
                        var roLastInstr = processor.Create(OpCodes.Nop);

                        // if (this.OwnerClientId != networkManager.LocalClientId) { ... } return;
                        instructions.Add(processor.Create(OpCodes.Ldarg_0));
                        instructions.Add(processor.Create(OpCodes.Call, m_NetworkBehaviour_getOwnerClientId_MethodRef));
                        instructions.Add(processor.Create(OpCodes.Ldloc, netManLocIdx));
                        instructions.Add(
                            processor.Create(OpCodes.Callvirt, m_NetworkManager_getLocalClientId_MethodRef));
                        instructions.Add(processor.Create(OpCodes.Ceq));
                        instructions.Add(processor.Create(OpCodes.Ldc_I4, 0));
                        instructions.Add(processor.Create(OpCodes.Ceq));
                        instructions.Add(processor.Create(OpCodes.Brfalse, roLastInstr));

                        var logNextInstr = processor.Create(OpCodes.Nop);

                        // if (LogLevel.Normal > networkManager.LogLevel)
                        instructions.Add(processor.Create(OpCodes.Ldloc, netManLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldfld, m_NetworkManager_LogLevel_FieldRef));
                        instructions.Add(processor.Create(OpCodes.Ldc_I4, (int)LogLevel.Normal));
                        instructions.Add(processor.Create(OpCodes.Cgt));
                        instructions.Add(processor.Create(OpCodes.Ldc_I4, 0));
                        instructions.Add(processor.Create(OpCodes.Ceq));
                        instructions.Add(processor.Create(OpCodes.Brfalse, logNextInstr));

                        // Debug.LogError(...);
                        instructions.Add(processor.Create(OpCodes.Ldstr,
                            "Only the owner can invoke a ServerRpc that requires ownership!"));
                        instructions.Add(processor.Create(OpCodes.Call, m_Debug_LogError_MethodRef));

                        instructions.Add(logNextInstr);

                        instructions.Add(roReturnInstr);
                        instructions.Add(roLastInstr);
                    }
                }

                // var writer = new FastBufferWriter(1285, Allocator.Temp, 63985);
                instructions.Add(processor.Create(OpCodes.Ldloca, serializerLocIdx));
                instructions.Add(processor.Create(OpCodes.Ldc_I4, 1300 - sizeof(byte) - sizeof(ulong) - sizeof(uint) - sizeof(ushort)));
                instructions.Add(processor.Create(OpCodes.Ldc_I4_2));
                instructions.Add(processor.Create(OpCodes.Ldc_I4, 64000 - sizeof(byte) - sizeof(ulong) - sizeof(uint) - sizeof(ushort)));
                instructions.Add(processor.Create(OpCodes.Call, m_FastBufferWriter_Constructor));

                var firstInstruction = processor.Create(OpCodes.Nop);
                instructions.Add(firstInstruction);

                // write method parameters into stream
                for (int paramIndex = 0; paramIndex < paramCount; ++paramIndex)
                {
                    var paramDef = methodDefinition.Parameters[paramIndex];
                    var paramType = paramDef.ParameterType;
                    // ServerRpcParams
                    if (paramType.FullName == CodeGenHelpers.ServerRpcParams_FullName && isServerRpc && paramIndex == paramCount - 1)
                    {
                        continue;
                    }
                    // ClientRpcParams
                    if (paramType.FullName == CodeGenHelpers.ClientRpcParams_FullName && !isServerRpc && paramIndex == paramCount - 1)
                    {
                        continue;
                    }

                    Instruction jumpInstruction = null;

                    if (!paramType.IsValueType)
                    {
                        if (!GetWriteMethodForParameter(typeSystem.Boolean, out var boolMethodRef))
                        {
                            m_Diagnostics.AddError(methodDefinition, $"Couldn't find boolean serializer! Something's wrong!");
                            return;
                        }

                        methodDefinition.Body.Variables.Add(new VariableDefinition(typeSystem.Boolean));
                        int isSetLocalIndex = methodDefinition.Body.Variables.Count - 1;

                        // bool isSet = (param != null);
                        instructions.Add(processor.Create(OpCodes.Ldarg, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Ldnull));
                        instructions.Add(processor.Create(OpCodes.Cgt_Un));
                        instructions.Add(processor.Create(OpCodes.Stloc, isSetLocalIndex));

                        // writer.WriteValueSafe(isSet);
                        instructions.Add(processor.Create(OpCodes.Ldloca, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldloca, isSetLocalIndex));
                        instructions.Add(processor.Create(OpCodes.Call, boolMethodRef));

                        // if(isSet) {
                        jumpInstruction = processor.Create(OpCodes.Nop);
                        instructions.Add(processor.Create(OpCodes.Ldloc, isSetLocalIndex));
                        instructions.Add(processor.Create(OpCodes.Brfalse, jumpInstruction));
                    }

                    var foundMethodRef = GetWriteMethodForParameter(paramType, out var methodRef);
                    if (foundMethodRef)
                    {
                        // writer.WriteNetworkSerializable(param) for INetworkSerializable, OR
                        // writer.WriteNetworkSerializable(param, -1, 0) for INetworkSerializable arrays, OR
                        // writer.WriteValueSafe(param) for value types, OR
                        // writer.WriteValueSafe(param, -1, 0) for arrays of value types, OR
                        // writer.WriteValueSafe(param, false) for strings
                        instructions.Add(processor.Create(OpCodes.Ldloca, serializerLocIdx));
                        var method = methodRef.Resolve();
                        var checkParameter = method.Parameters[0];
                        var isExtensionMethod = false;
                        if (checkParameter.ParameterType.Resolve() ==
                            m_FastBufferWriter_TypeRef.MakeByReferenceType().Resolve())
                        {
                            isExtensionMethod = true;
                            checkParameter = method.Parameters[1];
                        }
                        if (checkParameter.IsIn)
                        {
                            instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        }
                        else
                        {
                            instructions.Add(processor.Create(OpCodes.Ldarg, paramIndex + 1));
                        }
                        // Special handling for WriteValue() on arrays and strings since they have additional arguments.
                        if (paramType.IsArray
                            && ((!isExtensionMethod && methodRef.Parameters.Count == 3)
                                || (isExtensionMethod && methodRef.Parameters.Count == 4)))
                        {
                            instructions.Add(processor.Create(OpCodes.Ldc_I4_M1));
                            instructions.Add(processor.Create(OpCodes.Ldc_I4_0));
                        }
                        else if (paramType == typeSystem.String
                             && ((!isExtensionMethod && methodRef.Parameters.Count == 2)
                                 || (isExtensionMethod && methodRef.Parameters.Count == 3)))
                        {
                            instructions.Add(processor.Create(OpCodes.Ldc_I4_0));
                        }
                        instructions.Add(processor.Create(OpCodes.Call, methodRef));
                    }
                    else
                    {
                        m_Diagnostics.AddError(methodDefinition, $"Don't know how to serialize {paramType.Name} - implement {nameof(INetworkSerializable)} or add an extension method for {nameof(FastBufferWriter)}.{k_ExtensionMethodWriteName} to define serialization.");
                        continue;
                    }

                    if (jumpInstruction != null)
                    {
                        instructions.Add(jumpInstruction);
                    }
                }

                instructions.Add(endInstr);

                // __sendServerRpc(ref serializer, rpcMethodId, serverRpcParams, rpcDelivery) -> ServerRpc
                // __sendClientRpc(ref serializer, rpcMethodId, clientRpcParams, rpcDelivery) -> ClientRpc
                if (isServerRpc)
                {
                    // ServerRpc
                    // __sendServerRpc(ref serializer, rpcMethodId, serverRpcParams, rpcDelivery);
                    instructions.Add(processor.Create(OpCodes.Ldarg_0));

                    // serializer
                    instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));

                    // rpcMethodId
                    instructions.Add(processor.Create(OpCodes.Ldc_I4, unchecked((int)rpcMethodId)));

                    if (hasRpcParams)
                    {
                        // rpcParams
                        instructions.Add(processor.Create(OpCodes.Ldarg, paramCount));
                    }
                    else
                    {
                        // default
                        instructions.Add(processor.Create(OpCodes.Ldloc, rpcParamsIdx));
                    }

                    // rpcDelivery
                    instructions.Add(processor.Create(OpCodes.Ldc_I4, (int)rpcDelivery));

                    // EndSendServerRpc
                    instructions.Add(processor.Create(OpCodes.Call, m_NetworkBehaviour_SendServerRpc_MethodRef));
                }
                else
                {
                    // ClientRpc
                    // __sendClientRpc(ref serializer, rpcMethodId, clientRpcParams, rpcDelivery);
                    instructions.Add(processor.Create(OpCodes.Ldarg_0));

                    // serializer
                    instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));

                    // rpcMethodId
                    instructions.Add(processor.Create(OpCodes.Ldc_I4, unchecked((int)rpcMethodId)));

                    if (hasRpcParams)
                    {
                        // rpcParams
                        instructions.Add(processor.Create(OpCodes.Ldarg, paramCount));
                    }
                    else
                    {
                        // default
                        instructions.Add(processor.Create(OpCodes.Ldloc, rpcParamsIdx));
                    }

                    // rpcDelivery
                    instructions.Add(processor.Create(OpCodes.Ldc_I4, (int)rpcDelivery));

                    // EndSendClientRpc
                    instructions.Add(processor.Create(OpCodes.Call, m_NetworkBehaviour_SendClientRpc_MethodRef));
                }

                {
                    // TODO: Figure out why try/catch here cause the try block not to execute at all.
                    // End try block
                    //instructions.Add(processor.Create(OpCodes.Leave, lastInstr));

                    // writer.Dispose();
                    var handlerFirst = processor.Create(OpCodes.Ldloca, serializerLocIdx);
                    instructions.Add(handlerFirst);
                    instructions.Add(processor.Create(OpCodes.Call, m_FastBufferWriter_Dispose));

                    // End finally block
                    //instructions.Add(processor.Create(OpCodes.Endfinally));

                    // try { ... serialization code ... } finally { writer.Dispose(); }
                    /*var handler = new ExceptionHandler(ExceptionHandlerType.Finally)
                    {
                        TryStart = firstInstruction,
                        TryEnd = handlerFirst,
                        HandlerStart = handlerFirst,
                        HandlerEnd = lastInstr
                    };
                    processor.Body.ExceptionHandlers.Add(handler);*/
                }

                instructions.Add(lastInstr);
            }

            {
                var returnInstr = processor.Create(OpCodes.Ret);
                var lastInstr = processor.Create(OpCodes.Nop);

                // if (__rpc_exec_stage == __RpcExecStage.Server) -> ServerRpc
                // if (__rpc_exec_stage == __RpcExecStage.Client) -> ClientRpc
                instructions.Add(processor.Create(OpCodes.Ldarg_0));
                instructions.Add(processor.Create(OpCodes.Ldfld, m_NetworkBehaviour_rpc_exec_stage_FieldRef));
                instructions.Add(processor.Create(OpCodes.Ldc_I4, (int)(isServerRpc ? NetworkBehaviour.__RpcExecStage.Server : NetworkBehaviour.__RpcExecStage.Client)));
                instructions.Add(processor.Create(OpCodes.Ceq));
                instructions.Add(processor.Create(OpCodes.Brfalse, returnInstr));

                // if (networkManager.IsServer || networkManager.IsHost) -> ServerRpc
                // if (networkManager.IsClient || networkManager.IsHost) -> ClientRpc
                instructions.Add(processor.Create(OpCodes.Ldloc, netManLocIdx));
                instructions.Add(processor.Create(OpCodes.Callvirt, isServerRpc ? m_NetworkManager_getIsServer_MethodRef : m_NetworkManager_getIsClient_MethodRef));
                instructions.Add(processor.Create(OpCodes.Brtrue, lastInstr));
                instructions.Add(processor.Create(OpCodes.Ldloc, netManLocIdx));
                instructions.Add(processor.Create(OpCodes.Callvirt, m_NetworkManager_getIsHost_MethodRef));
                instructions.Add(processor.Create(OpCodes.Brtrue, lastInstr));

                instructions.Add(returnInstr);
                instructions.Add(lastInstr);
            }

            instructions.Reverse();
            instructions.ForEach(instruction => processor.Body.Instructions.Insert(0, instruction));
        }

        private MethodDefinition GenerateStaticHandler(MethodDefinition methodDefinition, CustomAttribute rpcAttribute)
        {
            var typeSystem = methodDefinition.Module.TypeSystem;
            var nhandler = new MethodDefinition(
                $"{methodDefinition.Name}__nhandler",
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig,
                methodDefinition.Module.TypeSystem.Void);
            nhandler.Parameters.Add(new ParameterDefinition("target", ParameterAttributes.None, m_NetworkBehaviour_TypeRef));
            nhandler.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, m_FastBufferReader_TypeRef));
            nhandler.Parameters.Add(new ParameterDefinition("rpcParams", ParameterAttributes.None, m_RpcParams_TypeRef));

            var processor = nhandler.Body.GetILProcessor();
            var isServerRpc = rpcAttribute.AttributeType.FullName == CodeGenHelpers.ServerRpcAttribute_FullName;
            var requireOwnership = true; // default value MUST be = `ServerRpcAttribute.RequireOwnership`
            foreach (var attrField in rpcAttribute.Fields)
            {
                switch (attrField.Name)
                {
                    case k_ServerRpcAttribute_RequireOwnership:
                        requireOwnership = attrField.Argument.Type == typeSystem.Boolean && (bool)attrField.Argument.Value;
                        break;
                }
            }

            nhandler.Body.InitLocals = true;
            // NetworkManager networkManager;
            nhandler.Body.Variables.Add(new VariableDefinition(m_NetworkManager_TypeRef));
            int netManLocIdx = nhandler.Body.Variables.Count - 1;

            {
                var returnInstr = processor.Create(OpCodes.Ret);
                var lastInstr = processor.Create(OpCodes.Nop);

                // networkManager = this.NetworkManager;
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, m_NetworkBehaviour_getNetworkManager_MethodRef);
                processor.Emit(OpCodes.Stloc, netManLocIdx);

                // if (networkManager == null || !networkManager.IsListening) return;
                processor.Emit(OpCodes.Ldloc, netManLocIdx);
                processor.Emit(OpCodes.Brfalse, returnInstr);
                processor.Emit(OpCodes.Ldloc, netManLocIdx);
                processor.Emit(OpCodes.Callvirt, m_NetworkManager_getIsListening_MethodRef);
                processor.Emit(OpCodes.Brtrue, lastInstr);

                processor.Append(returnInstr);
                processor.Append(lastInstr);
            }

            if (isServerRpc && requireOwnership)
            {
                var roReturnInstr = processor.Create(OpCodes.Ret);
                var roLastInstr = processor.Create(OpCodes.Nop);

                // if (rpcParams.Server.Receive.SenderClientId != target.OwnerClientId) { ... } return;
                processor.Emit(OpCodes.Ldarg_2);
                processor.Emit(OpCodes.Ldfld, m_RpcParams_Server_FieldRef);
                processor.Emit(OpCodes.Ldfld, m_ServerRpcParams_Receive_FieldRef);
                processor.Emit(OpCodes.Ldfld, m_ServerRpcParams_Receive_SenderClientId_FieldRef);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, m_NetworkBehaviour_getOwnerClientId_MethodRef);
                processor.Emit(OpCodes.Ceq);
                processor.Emit(OpCodes.Ldc_I4, 0);
                processor.Emit(OpCodes.Ceq);
                processor.Emit(OpCodes.Brfalse, roLastInstr);

                var logNextInstr = processor.Create(OpCodes.Nop);

                // if (LogLevel.Normal > networkManager.LogLevel)
                processor.Emit(OpCodes.Ldloc, netManLocIdx);
                processor.Emit(OpCodes.Ldfld, m_NetworkManager_LogLevel_FieldRef);
                processor.Emit(OpCodes.Ldc_I4, (int)LogLevel.Normal);
                processor.Emit(OpCodes.Cgt);
                processor.Emit(OpCodes.Ldc_I4, 0);
                processor.Emit(OpCodes.Ceq);
                processor.Emit(OpCodes.Brfalse, logNextInstr);

                // Debug.LogError(...);
                processor.Emit(OpCodes.Ldstr, "Only the owner can invoke a ServerRpc that requires ownership!");
                processor.Emit(OpCodes.Call, m_Debug_LogError_MethodRef);

                processor.Append(logNextInstr);

                processor.Append(roReturnInstr);
                processor.Append(roLastInstr);
            }

            // read method parameters from stream
            int paramCount = methodDefinition.Parameters.Count;
            int[] paramLocalMap = new int[paramCount];
            for (int paramIndex = 0; paramIndex < paramCount; ++paramIndex)
            {
                var paramDef = methodDefinition.Parameters[paramIndex];
                var paramType = paramDef.ParameterType;

                // local variable
                nhandler.Body.Variables.Add(new VariableDefinition(paramType));
                int localIndex = nhandler.Body.Variables.Count - 1;
                paramLocalMap[paramIndex] = localIndex;

                // ServerRpcParams, ClientRpcParams
                {
                    // ServerRpcParams
                    if (paramType.FullName == CodeGenHelpers.ServerRpcParams_FullName)
                    {
                        processor.Emit(OpCodes.Ldarg_2);
                        processor.Emit(OpCodes.Ldfld, m_RpcParams_Server_FieldRef);
                        processor.Emit(OpCodes.Stloc, localIndex);
                        continue;
                    }

                    // ClientRpcParams
                    if (paramType.FullName == CodeGenHelpers.ClientRpcParams_FullName)
                    {
                        processor.Emit(OpCodes.Ldarg_2);
                        processor.Emit(OpCodes.Ldfld, m_RpcParams_Client_FieldRef);
                        processor.Emit(OpCodes.Stloc, localIndex);
                        continue;
                    }
                }

                Instruction jumpInstruction = null;

                if (!paramType.IsValueType)
                {
                    if (!GetReadMethodForParameter(typeSystem.Boolean, out var boolMethodRef))
                    {
                        m_Diagnostics.AddError(methodDefinition, $"Couldn't find boolean deserializer! Something's wrong!");
                    }

                    // reader.ReadValueSafe(out bool isSet)
                    nhandler.Body.Variables.Add(new VariableDefinition(typeSystem.Boolean));
                    int isSetLocalIndex = nhandler.Body.Variables.Count - 1;
                    processor.Emit(OpCodes.Ldarga, 1);
                    processor.Emit(OpCodes.Ldloca, isSetLocalIndex);
                    processor.Emit(OpCodes.Call, boolMethodRef);

                    // paramType param = null;
                    processor.Emit(OpCodes.Ldnull);
                    processor.Emit(OpCodes.Stloc, localIndex);

                    // if(isSet) {
                    jumpInstruction = processor.Create(OpCodes.Nop);
                    processor.Emit(OpCodes.Ldloc, isSetLocalIndex);
                    processor.Emit(OpCodes.Brfalse, jumpInstruction);
                }

                var foundMethodRef = GetReadMethodForParameter(paramType, out var methodRef);
                if (foundMethodRef)
                {
                    // reader.ReadValueSafe(out localVar);
                    processor.Emit(OpCodes.Ldarga, 1);
                    processor.Emit(OpCodes.Ldloca, localIndex);
                    if (paramType == typeSystem.String)
                    {
                        processor.Emit(OpCodes.Ldc_I4_0);
                    }
                    processor.Emit(OpCodes.Call, methodRef);
                }
                else
                {
                    m_Diagnostics.AddError(methodDefinition, $"Don't know how to deserialize {paramType.Name} - implement {nameof(INetworkSerializable)} or add an extension method for {nameof(FastBufferReader)}.{k_ExtensionMethodReadName} to define serialization.");
                    continue;
                }

                if (jumpInstruction != null)
                {
                    processor.Append(jumpInstruction);
                }
            }

            // NetworkBehaviour.__rpc_exec_stage = __RpcExecStage.Server; -> ServerRpc
            // NetworkBehaviour.__rpc_exec_stage = __RpcExecStage.Client; -> ClientRpc
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldc_I4, (int)(isServerRpc ? NetworkBehaviour.__RpcExecStage.Server : NetworkBehaviour.__RpcExecStage.Client));
            processor.Emit(OpCodes.Stfld, m_NetworkBehaviour_rpc_exec_stage_FieldRef);

            // NetworkBehaviour.XXXRpc(...);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Castclass, methodDefinition.DeclaringType);
            Enumerable.Range(0, paramCount).ToList().ForEach(paramIndex => processor.Emit(OpCodes.Ldloc, paramLocalMap[paramIndex]));
            processor.Emit(OpCodes.Callvirt, methodDefinition);

            // NetworkBehaviour.__rpc_exec_stage = __RpcExecStage.None;
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldc_I4, (int)NetworkBehaviour.__RpcExecStage.None);
            processor.Emit(OpCodes.Stfld, m_NetworkBehaviour_rpc_exec_stage_FieldRef);

            processor.Emit(OpCodes.Ret);
            return nhandler;
        }
    }
}
