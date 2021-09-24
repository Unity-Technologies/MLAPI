using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

/// <summary>
/// Allows for context based delegate execution.
/// Can specify where you want that lambda executed (client side? server side?) and it'll automatically wait for the end
/// of a clientRPC server side and vice versa.
/// todo this could be used as an in-game tool too? for protocols that require a lot of back and forth?
/// </summary>
public class ExecuteStepInContext : CustomYieldInstruction
{
    public enum StepExecutionContext
    {
        Server,
        Clients
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class MultiprocessContextBasedTestAttribute : NUnitAttribute, IOuterUnityTestAction
    {
        public IEnumerator BeforeTest(ITest test)
        {
            yield return new WaitUntil(() => TestCoordinator.Instance != null && HasRegistered);
        }

        public IEnumerator AfterTest(ITest test)
        {
            yield break;
        }
    }

    private StepExecutionContext m_ActionContext;
    private Action<byte[]> m_StepToExecute;
    private string m_CurrentActionId;

    // as a remote worker, I store all available actions so I can execute them when triggered from RPCs
    public static Dictionary<string, ExecuteStepInContext> AllActions = new Dictionary<string, ExecuteStepInContext>();
    private static Dictionary<string, int> s_MethodIdCounter = new Dictionary<string, int>();

    private NetworkManager m_NetworkManager;
    private bool m_IsRegistering;
    private List<Func<bool>> m_ClientIsFinishedChecks = new List<Func<bool>>();
    private Func<bool> m_AdditionalIsFinishedWaiter;

    private bool m_WaitMultipleUpdates;
    private bool m_IgnoreTimeoutException;

    private float m_Timeout;
    private bool isTimingOut => m_Timeout < Time.time;
    private bool shouldExecuteLocally => (m_ActionContext == StepExecutionContext.Server && m_NetworkManager.IsServer) || (m_ActionContext == StepExecutionContext.Clients && !m_NetworkManager.IsServer);

    public static bool IsRegistering;
    public static bool HasRegistered;
    private static List<object> s_AllClientTestInstances = new List<object>(); // to keep an instance for each tests, so captured context in each step is kept

    /// <summary>
    /// This MUST be called at the beginning of each test in order to use context based steps.
    /// Assumes this is called from same callsite as ExecuteStepInContext (and assumes this is called from IEnumerator, the method full name is unique
    /// even with the same method name and different parameters).
    /// This relies on the name to be unique for each generated IEnumerator state machines
    /// </summary>
    public static void InitializeContextSteps()
    {
        var callerMethod = new StackFrame(1).GetMethod();
        var methodIdentifier = GetMethodIdentifier(callerMethod); // since this is called from IEnumerator, this should be a generated class, making it unique
        s_MethodIdCounter[methodIdentifier] = 0;
    }

    private static string GetMethodIdentifier(MethodBase callerMethod)
    {
        return callerMethod.DeclaringType.FullName;
    }

    internal static void InitializeAllSteps()
    {
        // registering magically all context based steps
        IsRegistering = true;
        var registeredMethods = typeof(TestCoordinator).Assembly.GetTypes().SelectMany(t => t.GetMethods())
            .Where(m => m.GetCustomAttributes(typeof(MultiprocessContextBasedTestAttribute), true).Length > 0)
            .ToArray();
        var typesWithContextMethods = new HashSet<Type>();
        foreach (var method in registeredMethods)
        {
            typesWithContextMethods.Add(method.ReflectedType);
        }

        if (registeredMethods.Length == 0)
        {
            throw new Exception($"Couldn't find any registered methods for multiprocess testing. Is {nameof(TestCoordinator)} in same assembly as test methods?");
        }

        object[] GetParameterValuesToPassFunc(ParameterInfo[] parameterInfo)
        {
            object[] parametersToReturn = new object[parameterInfo.Length];
            for (int i = 0; i < parameterInfo.Length; i++)
            {
                var paramType = parameterInfo[i].GetType();
                object defaultObj = null;
                if (paramType.IsValueType)
                {
                    defaultObj = Activator.CreateInstance(paramType);
                }

                parametersToReturn[i] = defaultObj;
            }

            return parametersToReturn;
        }

        foreach (var contextType in typesWithContextMethods)
        {
            var allConstructors = contextType.GetConstructors();
            if (allConstructors.Length > 1)
            {
                throw new NotImplementedException("Case not implemented where test has more than one constructor");
            }

            var instance = Activator.CreateInstance(contextType, allConstructors.Length > 0 ? GetParameterValuesToPassFunc(allConstructors[0].GetParameters()) : null);
            s_AllClientTestInstances.Add(instance); // keeping that instance so tests can use captured local attributes

            var typeMethodsWithContextSteps = new List<MethodInfo>();
            foreach (var method in contextType.GetMethods())
            {
                if (method.GetCustomAttributes(typeof(MultiprocessContextBasedTestAttribute), true).Length > 0)
                {
                    typeMethodsWithContextSteps.Add(method);
                }
            }

            foreach (var method in typeMethodsWithContextSteps)
            {
                var parametersToPass = GetParameterValuesToPassFunc(method.GetParameters());
                var enumerator = (IEnumerator)method.Invoke(instance, parametersToPass.ToArray());
                while (enumerator.MoveNext()) { }
            }
        }

        IsRegistering = false;
        HasRegistered = true;
    }

    /// <summary>
    /// Executes an action with the specified context. This allows writing tests all in the same sequential flow,
    /// making it more readable. This allows not having to jump between static client methods and test method
    /// </summary>
    /// <param name="actionContext">context to use. for example, should execute client side? server side?</param>
    /// <param name="stepToExecute">action to execute</param>
    /// <param name="ignoreTimeoutException">waits for timeout and just finishes step execution silently</param>
    /// <param name="paramToPass">parameters to pass to action</param>
    /// <param name="networkManager"></param>
    /// <param name="waitMultipleUpdates"> waits multiple frames before allowing the execution to continue. This means ClientFinishedServerRpc must be called manually</param>
    /// <param name="additionalIsFinishedWaiter"></param>
    public ExecuteStepInContext(StepExecutionContext actionContext, Action<byte[]> stepToExecute, bool ignoreTimeoutException = false, byte[] paramToPass = default,
        NetworkManager networkManager = null, bool waitMultipleUpdates = false, Func<bool> additionalIsFinishedWaiter = null)
    {
        m_Timeout = Time.time + TestCoordinator.MaxWaitTimeoutSec;
        m_IsRegistering = IsRegistering;
        m_ActionContext = actionContext;
        m_StepToExecute = stepToExecute;
        m_WaitMultipleUpdates = waitMultipleUpdates;
        m_IgnoreTimeoutException = ignoreTimeoutException;

        if (additionalIsFinishedWaiter != null)
        {
            m_AdditionalIsFinishedWaiter = additionalIsFinishedWaiter;
        }

        if (networkManager == null)
        {
            networkManager = NetworkManager.Singleton;
        }

        m_NetworkManager = networkManager; // todo test using this for multiinstance tests too?

        var callerMethod = new StackFrame(1).GetMethod(); // one skip frame for current method

        var methodId = GetMethodIdentifier(callerMethod); // assumes called from IEnumerator MoveNext, which should be the case since we're a CustomYieldInstruction. This will return a generated class name which should be unique
        if (!s_MethodIdCounter.ContainsKey(methodId))
        {
            s_MethodIdCounter[methodId] = 0;
        }

        string currentActionId = $"{methodId}-{s_MethodIdCounter[methodId]++}";
        m_CurrentActionId = currentActionId;

        if (m_IsRegistering)
        {
            Assert.That(AllActions, Does.Not.Contain(currentActionId)); // sanity check
            AllActions[currentActionId] = this;
        }
        else
        {
            if (shouldExecuteLocally)
            {
                m_StepToExecute.Invoke(paramToPass);
            }
            else
            {
                if (networkManager.IsServer)
                {
                    TestCoordinator.Instance.TriggerActionIdClientRpc(currentActionId, paramToPass, ignoreTimeoutException, clientRpcParams: new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = TestCoordinator.AllClientIdsExceptMine.ToArray() }
                    });
                    foreach (var clientId in TestCoordinator.AllClientIdsExceptMine)
                    {
                        m_ClientIsFinishedChecks.Add(TestCoordinator.ConsumeClientIsFinished(clientId));
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }
    }

    public void Invoke(byte[] args)
    {
        m_StepToExecute.Invoke(args);

        if (!m_WaitMultipleUpdates)
        {
            if (!m_NetworkManager.IsServer)
            {
                TestCoordinator.Instance.ClientFinishedServerRpc();
            }
            else
            {
                throw new NotImplementedException("todo implement");
            }
        }
    }

    public override bool keepWaiting
    {
        get
        {
            if (isTimingOut)
            {
                if (m_IgnoreTimeoutException)
                {
                    Debug.LogWarning($"Timeout ignored for action ID {m_CurrentActionId}");
                    return false;
                }

                throw new Exception($"timeout for Context Step with action ID {m_CurrentActionId}");
            }

            if (m_AdditionalIsFinishedWaiter != null)
            {
                var isFinished = m_AdditionalIsFinishedWaiter.Invoke();
                if (!isFinished)
                {
                    return true;
                }
            }

            if (m_IsRegistering || shouldExecuteLocally || m_ClientIsFinishedChecks == null)
            {
                return false;
            }

            for (int i = m_ClientIsFinishedChecks.Count - 1; i >= 0; i--)
            {
                if (m_ClientIsFinishedChecks[i].Invoke())
                {
                    m_ClientIsFinishedChecks.RemoveAt(i);
                }
                else
                {
                    return true;
                }
            }

            return false;
        }
    }
}
