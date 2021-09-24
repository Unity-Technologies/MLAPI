using System;
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ExecuteStepInContext;

namespace Unity.Netcode.MultiprocessRuntimeTests
{
    /// <summary>
    /// Smoke tests for ExecuteStepInContext, to make sure it's working properly before being used in other tests
    /// </summary>
    [TestFixture(1)]
    [TestFixture(2)]
    public class ExecuteStepInContextTests : BaseMultiprocessTests
    {
        private int m_WorkerCountToTest;

        public ExecuteStepInContextTests(int workerCountToTest)
        {
            m_WorkerCountToTest = workerCountToTest;
        }

        protected override int WorkerCount => m_WorkerCountToTest;
        protected override bool IsPerformanceTest => false;

        [UnityTest, MultiprocessContextBasedTest]
        public IEnumerator TestWithSameName([Values(1)] int a)
        {
            // ExecuteStepInContext bases itself on method name to identify steps. We need to make sure that methods with
            // same names, but different signatures behave correctly
            InitializeContextSteps();
            yield return new ExecuteStepInContext(StepExecutionContext.Server, bytes =>
            {
                Assert.That(a, Is.EqualTo(1));
            });
            yield return new ExecuteStepInContext(StepExecutionContext.Clients, bytes =>
            {
                Assert.That(BitConverter.ToInt32(bytes, 0), Is.EqualTo(1));
            }, paramToPass: BitConverter.GetBytes(a));
        }

        [UnityTest, MultiprocessContextBasedTest]
        public IEnumerator TestWithSameName([Values(2)] int a, [Values(3)] int b)
        {
            InitializeContextSteps();
            yield return new ExecuteStepInContext(StepExecutionContext.Server, bytes =>
            {
                Assert.That(b, Is.EqualTo(3));
            });
            yield return new ExecuteStepInContext(StepExecutionContext.Clients, bytes =>
            {
                Assert.That(BitConverter.ToInt32(bytes, 0), Is.EqualTo(3));
            }, paramToPass: BitConverter.GetBytes(b));
        }

        [UnityTest, MultiprocessContextBasedTest]
        public IEnumerator TestWithParameters([Values(1, 2, 3)] int a)
        {
            InitializeContextSteps();

            yield return new ExecuteStepInContext(StepExecutionContext.Server, bytes =>
            {
                Assert.Less(a, 4);
                Assert.Greater(a, 0);
            });
            yield return new ExecuteStepInContext(StepExecutionContext.Clients, bytes =>
            {
                var clientA = BitConverter.ToInt32(bytes, 0);
                Assert.True(!NetworkManager.Singleton.IsServer);
                Assert.Less(clientA, 4);
                Assert.Greater(clientA, 0);
            }, paramToPass: BitConverter.GetBytes(a));
        }

        [UnityTest, MultiprocessContextBasedTest]
        [TestCase(1, 2, ExpectedResult = null)]
        [TestCase(2, 3, ExpectedResult = null)]
        [TestCase(3, 4, ExpectedResult = null)]
        public IEnumerator TestWithParameters(int a, int b)
        {
            InitializeContextSteps();

            yield return new ExecuteStepInContext(StepExecutionContext.Server, bytes =>
            {
                Assert.Less(a, 4);
                Assert.Greater(a, 0);
                Assert.Less(b, 5);
                Assert.Greater(b, 1);
            });
            yield return new ExecuteStepInContext(StepExecutionContext.Clients, bytes =>
            {
                var clientB = BitConverter.ToInt32(bytes, 0);
                Assert.True(!NetworkManager.Singleton.IsServer);
                Assert.Less(clientB, 5);
                Assert.Greater(clientB, 1);
            }, paramToPass: BitConverter.GetBytes(b));
        }

        [UnityTest, MultiprocessContextBasedTest]
        public IEnumerator TestExceptionClientSide()
        {
            InitializeContextSteps();
            const string exceptionMessageToTest = "This is an exception for TestCoordinator that's expected";
            yield return new ExecuteStepInContext(StepExecutionContext.Clients, _ =>
            {
                throw new Exception(exceptionMessageToTest);
            }, ignoreTimeoutException: true);
            yield return new ExecuteStepInContext(StepExecutionContext.Server, _ =>
            {
                for (int i = 0; i < m_WorkerCountToTest; i++)
                {
                    LogAssert.Expect(LogType.Error, new Regex($".*{exceptionMessageToTest}.*"));
                }
            });

            const string exceptionUpdateMessageToTest = "This is an exception for update loop client side that's expected";
            yield return new ExecuteStepInContext(StepExecutionContext.Clients, _ =>
            {
                void UpdateFunc(float _)
                {
                    NetworkManager.Singleton.gameObject.GetComponent<CallbackComponent>().OnUpdate -= UpdateFunc;
                    throw new Exception(exceptionUpdateMessageToTest);
                }

                NetworkManager.Singleton.gameObject.GetComponent<CallbackComponent>().OnUpdate += UpdateFunc;
            }, ignoreTimeoutException: true);
            yield return new ExecuteStepInContext(StepExecutionContext.Server, _ =>
            {
                for (int i = 0; i < m_WorkerCountToTest; i++)
                {
                    LogAssert.Expect(LogType.Error, new Regex($".*{exceptionUpdateMessageToTest}.*"));
                }
            });
        }

        [UnityTest, MultiprocessContextBasedTest]
        public IEnumerator ContextTestWithAdditionalWait()
        {
            InitializeContextSteps();

            const int maxValue = 10;
            const float resendResultTime = 2.0f;
            bool exitOnFailure = false;
            int count = 0;
            bool sendNextResult = true;
            float lastResultSentTime = 0;
            yield return new ExecuteStepInContext(StepExecutionContext.Clients, _ =>
            {
                void UpdateFunc(float _)
                {
                    if (!exitOnFailure)
                    {
                        // We will send if we have another result to send =or= if we haven't received a response back from the server yet.
                        if (sendNextResult || lastResultSentTime < Time.realtimeSinceStartup)
                        {
                            if (!sendNextResult && lastResultSentTime < Time.realtimeSinceStartup)
                            {
                                Debug.LogWarning($"{nameof(ContextTestWithAdditionalWait)} is resending due to timeout receiving result received response for count value = {count}!");
                            }

                            TestCoordinator.Instance.WriteTestResultsServerRpc(count);
                            lastResultSentTime = resendResultTime + Time.realtimeSinceStartup;
                            sendNextResult = false;
                        }

                        // if we're finished sending our test count, then remove ourselves from the update loop
                        if (count >= maxValue)
                        {
                            NetworkManager.Singleton.gameObject.GetComponent<CallbackComponent>().OnUpdate -= UpdateFunc;
                            TestCoordinator.Instance.OnServerReceivedResultsResponse = null;
                        }
                    }
                }

                void TestResultReceived(float result)
                {
                    if (result != count)
                    {
                        TestCoordinator.Instance.WriteErrorServerRpc($"Received result {result} but was expected {count} for test {nameof(ContextTestWithAdditionalWait)}!");
                        NetworkManager.Singleton.gameObject.GetComponent<CallbackComponent>().OnUpdate -= UpdateFunc;
                        TestCoordinator.Instance.OnServerReceivedResultsResponse = null;
                        exitOnFailure = true;
                    }
                    else
                    {
                        count++;
                        sendNextResult = true;
                    }
                }
                NetworkManager.Singleton.gameObject.GetComponent<CallbackComponent>().OnUpdate += UpdateFunc;
                TestCoordinator.Instance.OnServerReceivedResultsResponse = TestResultReceived;

            }, additionalIsFinishedWaiter: () =>
            {
                int nbFinished = 0;
                if (!exitOnFailure)
                {
                    for (int i = 0; i < m_WorkerCountToTest; i++)
                    {
                        if (TestCoordinator.PeekLatestResult(TestCoordinator.AllClientIdsExceptMine[i]) == maxValue)
                        {
                            nbFinished++;
                        }
                    }
                }
                return nbFinished == m_WorkerCountToTest;
            });
            yield return new ExecuteStepInContext(StepExecutionContext.Server, _ =>
            {
                Assert.That(TestCoordinator.AllClientIdsExceptMine.Count, Is.EqualTo(m_WorkerCountToTest));
                foreach (var clientId in TestCoordinator.AllClientIdsExceptMine)
                {
                    var current = 0;
                    foreach (var res in TestCoordinator.ConsumeCurrentResult(clientId))
                    {
                        Assert.That(res, Is.EqualTo(current++));
                    }

                    Assert.That(current - 1, Is.EqualTo(maxValue));
                }
            });
        }

        [UnityTest, MultiprocessContextBasedTest]
        public IEnumerator TestExecuteInContext()
        {
            InitializeContextSteps();

            int stepCountExecuted = 0;
            yield return new ExecuteStepInContext(StepExecutionContext.Server, args =>
            {
                stepCountExecuted++;
                int count = BitConverter.ToInt32(args, 0);
                Assert.That(count, Is.EqualTo(1));
            }, paramToPass: BitConverter.GetBytes(1));

            yield return new ExecuteStepInContext(StepExecutionContext.Clients, args =>
            {
                int count = BitConverter.ToInt32(args, 0);
                Assert.That(count, Is.EqualTo(2));
                TestCoordinator.Instance.WriteTestResultsServerRpc(12345);
#if UNITY_EDITOR
                Assert.Fail("Should not be here!! This should only execute on client!!");
#endif
            }, paramToPass: BitConverter.GetBytes(2));

            yield return new ExecuteStepInContext(StepExecutionContext.Server, _ =>
            {
                stepCountExecuted++;
                int resultCountFromWorkers = 0;
                foreach (var res in TestCoordinator.ConsumeCurrentResult())
                {
                    resultCountFromWorkers++;
                    Assert.AreEqual(12345, res.result);
                }

                Assert.That(resultCountFromWorkers, Is.EqualTo(WorkerCount));
            });

            const int timeToWait = 4;
            yield return new ExecuteStepInContext(StepExecutionContext.Clients, _ =>
            {
                void UpdateFunc(float _)
                {
                    if (Time.time > timeToWait)
                    {
                        NetworkManager.Singleton.gameObject.GetComponent<CallbackComponent>().OnUpdate -= UpdateFunc;
                        TestCoordinator.Instance.WriteTestResultsServerRpc(Time.time);

                        TestCoordinator.Instance.ClientFinishedServerRpc(); // since finishOnInvoke is false, we need to do this manually
                    }
                }

                NetworkManager.Singleton.gameObject.GetComponent<CallbackComponent>().OnUpdate += UpdateFunc;
            }, waitMultipleUpdates: true); // waits multiple frames before allowing the next action to continue.

            yield return new ExecuteStepInContext(StepExecutionContext.Server, args =>
            {
                stepCountExecuted++;
                int count = 0;
                foreach (var res in TestCoordinator.ConsumeCurrentResult())
                {
                    count++;
                    Assert.GreaterOrEqual(res.result, timeToWait);
                }

                Assert.Greater(count, 0);
            });

            if (!IsRegistering)
            {
                Assert.AreEqual(3, stepCountExecuted);
            }
        }
    }
}
