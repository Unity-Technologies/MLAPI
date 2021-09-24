using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Unity.Netcode.MultiprocessRuntimeTests;

/// <summary>
/// TestCoordinator
/// Used for coordinating multiprocess end to end tests. Used to call RPCs on other nodes and gather results
/// This is needed to coordinate server and client execution steps. The current remote player test runner hardcodes test
/// to run in a bootstrap scene before launching the player and doesn't call each tests individually. There's not opportunity
/// to coordinate test execution between client and server with that model.
/// The only per tests communication already existing is to get the results per test as they are running
/// With this test coordinator, it's not possible to start a main test node with the test runner and have that server start other worker nodes
/// on which to execute client tests. We use netcode as both a test framework and as the target of our performance tests.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class TestCoordinator : NetworkBehaviour
{
    public const int PerTestTimeoutSec = 5 * 60; // seconds

    public const float MaxWaitTimeoutSec = 20;
    private const char k_MethodFullNameSplitChar = '@';

    private bool m_ShouldShutdown;
    private float m_TimeSinceLastConnected;
    private float m_TimeSinceLastKeepAlive;

    public static TestCoordinator Instance;

    private Dictionary<ulong, List<float>> m_TestResultsLocal = new Dictionary<ulong, List<float>>(); // this isn't super efficient, but since it's used for signaling around the tests, shouldn't be too bad
    private Dictionary<ulong, bool> m_ClientIsFinished = new Dictionary<ulong, bool>();

    public static List<ulong> AllClientIdsWithResults => Instance.m_TestResultsLocal.Keys.ToList();
    public static List<ulong> AllClientIdsExceptMine => NetworkManager.Singleton.ConnectedClients.Keys.ToList().FindAll(client => client != NetworkManager.Singleton.LocalClientId);

    private void Awake()
    {
        BaseMultiprocessTests.MultiProcessLog("Awake");
        if (Instance != null)
        {
            Debug.LogError("Multiple test coordinator, destroying this instance");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void Start()
    {
        BaseMultiprocessTests.MultiProcessLog("Start");
        bool isClient = Environment.GetCommandLineArgs().Any(value => value == MultiprocessOrchestration.IsWorkerArg);
        if (isClient)
        {
            BaseMultiprocessTests.MultiProcessLog("starting netcode client");
            NetworkManager.Singleton.StartClient();
            BaseMultiprocessTests.MultiProcessLog($"started netcode client {NetworkManager.Singleton.IsConnectedClient}");
        }
        BaseMultiprocessTests.MultiProcessLog("Initialize All Steps");
        ExecuteStepInContext.InitializeAllSteps();
        BaseMultiprocessTests.MultiProcessLog($"Initialize All Steps... done");
        BaseMultiprocessTests.MultiProcessLog($"IsInvoking: {NetworkManager.Singleton.IsInvoking()}");
        BaseMultiprocessTests.MultiProcessLog($"IsActiveAndEnabled: {NetworkManager.Singleton.isActiveAndEnabled}");
        BaseMultiprocessTests.MultiProcessLog($"NetworkManager.NetworkConfig.NetworkTransport.name {NetworkManager.NetworkConfig.NetworkTransport.name}");
    }

    public void Update()
    {
        if (Time.time - m_TimeSinceLastKeepAlive > PerTestTimeoutSec)
        {
            QuitApplication();
            Assert.Fail("Stayed idle too long");
        }

        if ((IsServer && NetworkManager.Singleton.IsListening) || (IsClient && NetworkManager.Singleton.IsConnectedClient))
        {
            m_TimeSinceLastConnected = Time.time;
        }
        else if (Time.time - m_TimeSinceLastConnected > MaxWaitTimeoutSec || m_ShouldShutdown)
        {
            // Make sure we don't have zombie processes
            BaseMultiprocessTests.MultiProcessLog($"quitting application, shouldShutdown set to {m_ShouldShutdown}, is listening {NetworkManager.Singleton.IsListening}, is connected client {NetworkManager.Singleton.IsConnectedClient}");
            if (!m_ShouldShutdown)
            {
                QuitApplication();
                Assert.Fail($"something wrong happened, was not connected for {Time.time - m_TimeSinceLastConnected} seconds");
            }
        }
    }

    private static void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void TestRunTeardown()
    {
        m_TestResultsLocal.Clear();
    }

    public void OnEnable()
    {
        BaseMultiprocessTests.MultiProcessLog("OnEnable - Setting OnClientDisconnectCallback");
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
    }

    public void OnDisable()
    {
        if (IsSpawned && NetworkObject != null && NetworkObject.NetworkManager != null)
        {
            BaseMultiprocessTests.MultiProcessLog("OnDisable - Removing OnClientDisconnectCallback");
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnectCallback;
        }
    }

    private static void OnClientDisconnectCallback(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.ServerClientId || clientId == NetworkManager.Singleton.LocalClientId)
        {
            // if disconnect callback is for me or for server, quit, we're done here
            Debug.Log($"received disconnect from {clientId}, quitting");
            QuitApplication();
        }
    }

    private static string GetMethodInfo(Action<byte[]> method)
    {
        return $"{method.Method.DeclaringType.FullName}{k_MethodFullNameSplitChar}{method.Method.Name}";
    }

    private static string GetMethodInfo(Action method)
    {
        return $"{method.Method.DeclaringType.FullName}{k_MethodFullNameSplitChar}{method.Method.Name}";
    }

    public static IEnumerable<(ulong clientId, float result)> ConsumeCurrentResult()
    {
        foreach (var kv in Instance.m_TestResultsLocal)
        {
            while (kv.Value.Count > 0)
            {
                var toReturn = (kv.Key, kv.Value[0]);
                kv.Value.RemoveAt(0);
                yield return toReturn;
            }
        }
    }

    public static IEnumerable<float> ConsumeCurrentResult(ulong clientId)
    {
        var allResults = Instance.m_TestResultsLocal[clientId];
        while (allResults.Count > 0)
        {
            var toReturn = allResults[0];
            allResults.RemoveAt(0);
            yield return toReturn;
        }
    }

    public static float PeekLatestResult(ulong clientId)
    {
        if (Instance.m_TestResultsLocal.ContainsKey(clientId) && Instance.m_TestResultsLocal[clientId].Count > 0)
        {
            return Instance.m_TestResultsLocal[clientId].Last();
        }

        return float.NaN;
    }

    /// <summary>
    /// Returns appropriate lambda according to parameters
    /// Includes time check to make sure this times out
    /// </summary>
    /// <param name="useTimeoutException"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static Func<bool> ResultIsSet(bool useTimeoutException = true)
    {
        var startWaitTime = Time.time;
        return () =>
        {
            if (Time.time - startWaitTime > MaxWaitTimeoutSec)
            {
                if (useTimeoutException)
                {
                    throw new Exception($"timeout while waiting for results, didn't get results for {Time.time - startWaitTime} seconds");
                }

                return true;
            }

            foreach (var clientIdAndTestResultList in Instance.m_TestResultsLocal)
            {
                if (clientIdAndTestResultList.Value.Count > 0)
                {
                    return true;
                }
            }

            return false;
        };
    }

    public static Func<bool> ConsumeClientIsFinished(ulong clientId, bool useTimeoutException = true)
    {
        var startWaitTime = Time.time;
        return () =>
        {
            if (Time.time - startWaitTime > MaxWaitTimeoutSec)
            {
                if (useTimeoutException)
                {
                    throw new Exception($"timeout while waiting for client finished, didn't get results for {Time.time - startWaitTime} seconds");
                }
                else
                {
                    return true;
                }
            }

            if (Instance.m_ClientIsFinished.ContainsKey(clientId) && Instance.m_ClientIsFinished[clientId])
            {
                Instance.m_ClientIsFinished[clientId] = false; // consume
                return true;
            }

            return false;
        };
    }

    [ServerRpc(RequireOwnership = false)]
    public void ClientFinishedServerRpc(ServerRpcParams p = default)
    {
        // signal from clients to the server to say the client is done with it's task
        m_ClientIsFinished[p.Receive.SenderClientId] = true;
    }

    public void InvokeFromMethodActionRpc(Action<byte[]> methodInfo, params byte[] args)
    {
        var methodInfoString = GetMethodInfo(methodInfo);
        InvokeFromMethodNameClientRpc(methodInfoString, args, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = AllClientIdsExceptMine.ToArray() } });
    }

    public void InvokeFromMethodActionRpc(Action methodInfo)
    {
        var methodInfoString = GetMethodInfo(methodInfo);
        InvokeFromMethodNameClientRpc(methodInfoString, null, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = AllClientIdsExceptMine.ToArray() } });
    }

    [ClientRpc]
    public void TriggerActionIdClientRpc(string actionId, byte[] args, bool ignorException, ClientRpcParams clientRpcParams = default)
    {
        BaseMultiprocessTests.MultiProcessLog($"received RPC from server, client side triggering action ID {actionId}");
        try
        {
            ExecuteStepInContext.AllActions[actionId].Invoke(args);
        }
        catch (Exception e)
        {
            WriteErrorServerRpc(e.Message);
            if (!ignorException)
            {
                throw;
            }
            else
            {
                Instance.ClientFinishedServerRpc();
            }
        }
    }

    [ClientRpc]
    public void InvokeFromMethodNameClientRpc(string methodInfoString, byte[] args, ClientRpcParams clientRpcParams = default)
    {
        try
        {
            var split = methodInfoString.Split(k_MethodFullNameSplitChar);
            var (classToExecute, staticMethodToExecute) = (split[0], split[1]);

            var foundType = Type.GetType(classToExecute);
            if (foundType == null)
            {
                throw new Exception($"couldn't find {classToExecute}");
            }

            var foundMethod = foundType.GetMethod(staticMethodToExecute, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (foundMethod == null)
            {
                throw new MissingMethodException($"couldn't find method {staticMethodToExecute}");
            }

            foundMethod.Invoke(null, args != null ? new object[] { args } : null);
        }
        catch (Exception e)
        {
            WriteErrorServerRpc(e.Message);
            throw;
        }
    }

    [ClientRpc]
    public void CloseRemoteClientRpc()
    {
        try
        {
            NetworkManager.Singleton.Shutdown();
            m_ShouldShutdown = true; // wait until isConnectedClient is false to run Application Quit in next update
            BaseMultiprocessTests.MultiProcessLog("Quitting player cleanly");
            Application.Quit();
        }
        catch (Exception e)
        {
            WriteErrorServerRpc(e.Message);
            throw;
        }
    }

    [ClientRpc]
    public void KeepAliveClientRpc()
    {
        m_TimeSinceLastKeepAlive = Time.time;
    }

    private ulong[] m_TargetClient = new ulong[1] { 0 };
    
    [ServerRpc(RequireOwnership = false)]
    public void WriteTestResultsServerRpc(float result, ServerRpcParams receiveParams = default)
    {
        var senderId = receiveParams.Receive.SenderClientId;
        Debug.Log($"Server received result [{result}] from sender [{senderId}]");
        if (!m_TestResultsLocal.ContainsKey(senderId))
        {
            m_TestResultsLocal[senderId] = new List<float>();
        }

        m_TestResultsLocal[senderId].Add(result);

        // Now send the results received verification
        m_TargetClient[0] = senderId;
        var clientParams = new ClientRpcParams();
        clientParams.Send.TargetClientIds = m_TargetClient;
        ServerReceivedResultsResponseClientRpc(result, clientParams);
    }

    public delegate void OnServerReceivedResultsResponseDelegateHandler(float resultReceived);

    public OnServerReceivedResultsResponseDelegateHandler OnServerReceivedResultsResponse;
    private void ServerReceivedResultsResponse(float resultReceived)
    {
        BaseMultiprocessTests.MultiProcessLog($"ServerReceivedResultsReponse {resultReceived}");
        if (OnServerReceivedResultsResponse != null)
        {
            OnServerReceivedResultsResponse.Invoke(resultReceived);
        }
        else
        {
            Debug.LogWarning("Current test is not validating results were received (adding this validation will assure proper test synchronization and timing).");
        }
    }

    [ClientRpc]
    public void ServerReceivedResultsResponseClientRpc(float resultReceived, ClientRpcParams clientRpcParams = default)
    {
        ServerReceivedResultsResponse(resultReceived);
    }


    [ServerRpc(RequireOwnership = false)]
    public void WriteErrorServerRpc(string errorMessage, ServerRpcParams receiveParams = default)
    {
        Debug.LogError($"Got Exception client side {errorMessage}, from client {receiveParams.Receive.SenderClientId}");
    }
}
