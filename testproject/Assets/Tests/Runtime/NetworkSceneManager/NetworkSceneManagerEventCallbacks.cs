using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TestProject.RuntimeTests
{
    [TestFixture(HostOrServer.DAHost)]
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    public class NetworkSceneManagerEventCallbacks : NetcodeIntegrationTest
    {
        private const string k_SceneToLoad = "EmptyScene";
        protected override int NumberOfClients => 4;
        private Scene m_CurrentScene;
        private bool m_CanStartServerOrClients = false;

        private class SceneEventNotificationTestInfo
        {
            public ulong ClientId;
            public bool ShouldWait;
            public List<SceneEventType> EventsProcessed;
        }

        private class SceneEventCompletedTestInfo
        {
            public string SceneName;
            public SceneEventType EventTypeCompleted;
            public List<ulong> ClientThatCompletedEvent;
        }

        private List<SceneEventNotificationTestInfo> m_ClientNotificationInfo = new List<SceneEventNotificationTestInfo>();
        private List<SceneEventNotificationTestInfo> m_ServerNotificationInfo = new List<SceneEventNotificationTestInfo>();

        private List<SceneEventCompletedTestInfo> m_ClientCompletedTestInfo = new List<SceneEventCompletedTestInfo>();
        private List<SceneEventCompletedTestInfo> m_ServerCompletedTestInfo = new List<SceneEventCompletedTestInfo>();

        public NetworkSceneManagerEventCallbacks(HostOrServer hostOrServer) : base(hostOrServer) { }

        protected override IEnumerator OnSetup()
        {
            m_CanStartServerOrClients = false;
            m_ClientNotificationInfo.Clear();
            m_ServerNotificationInfo.Clear();
            m_ClientCompletedTestInfo.Clear();
            m_ServerCompletedTestInfo.Clear();
            return base.OnSetup();
        }

        protected override bool CanStartServerAndClients()
        {
            return m_CanStartServerOrClients;
        }

        protected override IEnumerator OnStartedServerAndClients()
        {
            // Immediately register for all pertinent event notifications we want to test and validate working
            // For the server:
            m_ServerNetworkManager.SceneManager.OnLoad += Server_OnLoad;
            m_ServerNetworkManager.SceneManager.OnLoadComplete += Server_OnLoadComplete;
            m_ServerNetworkManager.SceneManager.OnLoadEventCompleted += Server_OnLoadEventCompleted;
            m_ServerNetworkManager.SceneManager.OnUnload += Server_OnUnload;
            m_ServerNetworkManager.SceneManager.OnUnloadComplete += Server_OnUnloadComplete;
            m_ServerNetworkManager.SceneManager.OnUnloadEventCompleted += Server_OnUnloadEventCompleted;
            m_ServerNetworkManager.SceneManager.OnSynchronizeComplete += Server_OnSynchronizeComplete;
            m_ServerNetworkManager.SceneManager.OnSceneEvent += Server_OnSceneEvent;

            // For the clients:
            foreach (var client in m_ClientNetworkManagers)
            {
                client.SceneManager.OnLoad += Client_OnLoad;
                client.SceneManager.OnLoadComplete += Client_OnLoadComplete;
                client.SceneManager.OnLoadEventCompleted += Client_OnLoadEventCompleted;
                client.SceneManager.OnUnload += Client_OnUnload;
                client.SceneManager.OnUnloadComplete += Client_OnUnloadComplete;
                client.SceneManager.OnUnloadEventCompleted += Client_OnUnloadEventCompleted;
                client.SceneManager.OnSynchronizeComplete += Client_OnSynchronizeComplete;
                client.SceneManager.OnSynchronize += Client_OnSynchronize;
            }

            return base.OnStartedServerAndClients();
        }

        private bool ValidateSceneIsLoaded()
        {
            if (!NotificationTestShouldWait() && m_CurrentScene.IsValid() && m_CurrentScene.isLoaded)
            {
                if (ValidateCompletedNotifications())
                {
                    return true;
                }
            }
            return false;
        }

        [UnityTest]
        public IEnumerator SceneEventCallbackNotifications()
        {
            m_CanStartServerOrClients = true;
            yield return StartServerAndClients();

            yield return WaitForConditionOrTimeOut(() => !NotificationTestShouldWait());
            AssertOnTimeout($"Timed out waiting for client to synchronize!");

            // Reset for next test
            ResetNotificationInfo();

            //////////////////////////////////////////
            // Testing load event notifications
            Assert.That(m_ServerNetworkManager.SceneManager.LoadScene(k_SceneToLoad, LoadSceneMode.Additive) == SceneEventProgressStatus.Started);

            yield return WaitForConditionOrTimeOut(ValidateSceneIsLoaded);
            AssertOnTimeout($"Timed out waiting for client to load the scene {k_SceneToLoad}!");

            yield return s_DefaultWaitForTick;

            // Reset for next test
            ResetNotificationInfo();
            //////////////////////////////////////////
            // Testing unload event notifications
            var unloadStatus = m_ServerNetworkManager.SceneManager.UnloadScene(m_CurrentScene);
            Assert.That(unloadStatus == SceneEventProgressStatus.Started, $"Unload scene failed to start with a status code of: {unloadStatus}");

            yield return WaitForConditionOrTimeOut(() => (!NotificationTestShouldWait() && !m_CurrentScene.isLoaded) ? true : !ValidateCompletedNotifications());
            AssertOnTimeout($"Timed out waiting for client to unload the scene {k_SceneToLoad}!");

            yield return s_DefaultWaitForTick;
        }


        private void ResetNotificationInfo()
        {
            m_ClientNotificationInfo.Clear();
            m_ServerNotificationInfo.Clear();
            m_ClientCompletedTestInfo.Clear();
            m_ServerCompletedTestInfo.Clear();
        }

        private void ClientProcessedNotification(ulong clientId, SceneEventType notificationType, bool isServer, bool eventComplete = false)
        {
            if (clientId == m_ServerNetworkManager.LocalClientId)
            {
                return;
            }
            var notificationList = isServer ? m_ServerNotificationInfo : m_ClientNotificationInfo;
            var clientSelection = notificationList.Select(c => c).Where(c => c.ClientId == clientId);
            if (clientSelection == null || clientSelection.Count() == 0)
            {
                notificationList.Add(new SceneEventNotificationTestInfo()
                {
                    ClientId = clientId,
                    EventsProcessed = new List<SceneEventType>(),
                });
            }
            var clientNotificationObject = notificationList.Select(c => c).Where(c => c.ClientId == clientId).First();
            clientNotificationObject.EventsProcessed.Add(notificationType);
            clientNotificationObject.ShouldWait = !eventComplete;
        }

        private void ProcessCompletedNotification(string sceneName, List<ulong> clientIds, SceneEventType notificationType, bool isServer)
        {
            var notificationList = isServer ? m_ServerCompletedTestInfo : m_ClientCompletedTestInfo;

            notificationList.Add(new SceneEventCompletedTestInfo()
            {
                SceneName = sceneName,
                EventTypeCompleted = notificationType,
                ClientThatCompletedEvent = new List<ulong>(clientIds)
            });
        }

        private bool ValidateCompletedNotifications()
        {
            var isValidated = m_ClientCompletedTestInfo.Count == NumberOfClients && m_ServerCompletedTestInfo.Count == 1;
            if (isValidated)
            {
                foreach (var client in m_ClientCompletedTestInfo)
                {
                    Assert.That(m_ServerCompletedTestInfo[0].SceneName == client.SceneName);
                    Assert.That(m_ServerCompletedTestInfo[0].EventTypeCompleted == client.EventTypeCompleted);
                    Assert.That(m_ServerCompletedTestInfo[0].ClientThatCompletedEvent.Count == client.ClientThatCompletedEvent.Count);
                    foreach (var clientId in m_ServerCompletedTestInfo[0].ClientThatCompletedEvent)
                    {
                        Assert.That(client.ClientThatCompletedEvent.Contains(clientId));
                    }
                }
                Debug.Log($"{m_ServerCompletedTestInfo[0].EventTypeCompleted} validated!");
            }
            return isValidated;
        }

        private bool NotificationTestShouldWait()
        {
            // if all of our clients are not done we should wait.
            var shouldWait = m_ClientNotificationInfo.Select(c => c).Where(c => c.ShouldWait == false).Count() != m_ClientNotificationInfo.Count;

            // if our client count (server side vs client side) do not match yet we should wait
            shouldWait |= m_ServerNotificationInfo.Count() != m_ClientNotificationInfo.Count();

            // if our client count is zero for either side or both sides we should wait
            shouldWait |= m_ServerNotificationInfo.Count() == 0 || m_ClientNotificationInfo.Count() == 0;

            // Early exit if we should wait at this point
            if (shouldWait)
            {
                return shouldWait;
            }

            foreach (var clientEntry in m_ServerNotificationInfo)
            {
                var clientList = m_ClientNotificationInfo.Select(c => c).Where(c => c.ClientId == clientEntry.ClientId);
                if (clientList == null || clientList.Count() == 0)
                {
                    shouldWait = true;
                }
                else
                {
                    var client = clientList.First();
                    // Compare the events processed to make sure the events are invoked on the client,a message sent to the server,
                    // and the same event is invoked on the server too.
                    foreach (var sceneEventType in clientEntry.EventsProcessed)
                    {
                        if (!client.EventsProcessed.Contains(sceneEventType))
                        {
                            shouldWait = true;
                            break;
                        }
                    }
                }

                if (shouldWait)
                {
                    break;
                }
            }

            return shouldWait;
        }

        private void Client_OnSynchronize(ulong clientId)
        {
            ClientProcessedNotification(clientId, SceneEventType.Synchronize, false);
        }

        private void Client_OnSynchronizeComplete(ulong clientId)
        {
            Debug.Log($"Client {clientId} synchronized.");
            ClientProcessedNotification(clientId, SceneEventType.SynchronizeComplete, false, true);
        }

        private void Client_OnUnloadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            ProcessCompletedNotification(sceneName, clientsCompleted, SceneEventType.UnloadEventCompleted, false);
        }

        private void Client_OnUnloadComplete(ulong clientId, string sceneName)
        {
            Debug.Log($"Client {clientId} unloaded {sceneName}");
            ClientProcessedNotification(clientId, SceneEventType.UnloadComplete, false, true);
        }

        private void Client_OnUnload(ulong clientId, string sceneName, AsyncOperation asyncOperation)
        {
            ClientProcessedNotification(clientId, SceneEventType.Unload, false);
        }

        private void Client_OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            ProcessCompletedNotification(sceneName, clientsCompleted, SceneEventType.LoadEventCompleted, false);
        }

        private void Client_OnLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
        {
            Debug.Log($"Client {clientId} loaded {sceneName} in {loadSceneMode} LoadSceneMode");
            ClientProcessedNotification(clientId, SceneEventType.LoadComplete, false, true);
        }

        private void Client_OnLoad(ulong clientId, string sceneName, LoadSceneMode loadSceneMode, AsyncOperation asyncOperation)
        {
            ClientProcessedNotification(clientId, SceneEventType.Load, false);
        }

        private void Server_OnSceneEvent(SceneEvent sceneEvent)
        {
            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.LoadComplete:
                    {
                        // We have to manually add the loaded scene to the clients when server is done loading
                        // since the clients do not load scenes in MultiInstance tests.
                        if (sceneEvent.ClientId == m_ServerNetworkManager.LocalClientId)
                        {
                            m_CurrentScene = sceneEvent.Scene;
                        }
                        break;
                    }
            }
        }

        private void Server_OnSynchronizeComplete(ulong clientId)
        {
            Debug.Log($"Server Received Client {clientId} synchronized complete.");
            ClientProcessedNotification(clientId, SceneEventType.SynchronizeComplete, true, true);
        }

        private void Server_OnUnloadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            ProcessCompletedNotification(sceneName, clientsCompleted, SceneEventType.UnloadEventCompleted, true);
        }

        private void Server_OnUnloadComplete(ulong clientId, string sceneName)
        {
            ClientProcessedNotification(clientId, SceneEventType.UnloadComplete, true, true);
        }

        private void Server_OnUnload(ulong clientId, string sceneName, AsyncOperation asyncOperation)
        {
            ClientProcessedNotification(clientId, SceneEventType.Unload, true);
        }

        private void Server_OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            ProcessCompletedNotification(sceneName, clientsCompleted, SceneEventType.LoadEventCompleted, true);
        }

        private void Server_OnLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
        {
            ClientProcessedNotification(clientId, SceneEventType.LoadComplete, true, true);
        }

        private void Server_OnLoad(ulong clientId, string sceneName, LoadSceneMode loadSceneMode, AsyncOperation asyncOperation)
        {
            ClientProcessedNotification(clientId, SceneEventType.Load, true);
        }
    }
}
