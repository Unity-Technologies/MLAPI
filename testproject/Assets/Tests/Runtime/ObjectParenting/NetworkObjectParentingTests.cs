using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TestProject.RuntimeTests
{
    [TestFixture(NetworkTopologyTypes.DistributedAuthority)]
    [TestFixture(NetworkTopologyTypes.ClientServer)]
    public class NetworkObjectParentingTests
    {
        private const int k_ClientInstanceCount = 1;

        private NetworkManager m_ServerNetworkManager;
        private NetworkManager[] m_ClientNetworkManagers;

        private Transform[] m_Dude_NetObjs;
        private Transform[] m_Dude_LeftArm_NetObjs;
        private Transform[] m_Dude_RightArm_NetObjs;
        private Transform[] m_Dude_LeftLeg_NetObjs;
        private Transform[] m_Dude_RightLeg_NetObjs;
        private Transform[] m_Cube_NetObjs;
        private ReparentingCubeNetBhv[] m_Cube_NetBhvs;
        private Transform[] m_Pickup_NetObjs;
        private Transform[] m_Pickup_Back_NetObjs;

        private Scene m_InitScene;
        private Scene m_TestScene;

        private NetworkTopologyTypes m_NetworkTopologyType;
        public NetworkObjectParentingTests(NetworkTopologyTypes networkTopologyType)
        {
            m_NetworkTopologyType = networkTopologyType;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == nameof(NetworkObjectParentingTests))
            {
                m_TestScene = scene;
            }
        }

        private void InvokeBeforeClientsStart()
        {
            m_ServerNetworkManager.SceneManager.ClientSynchronizationMode = LoadSceneMode.Additive;
        }

        [UnitySetUp]
        public IEnumerator Setup()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;

            Assert.That(NetcodeIntegrationTestHelpers.Create(k_ClientInstanceCount, out m_ServerNetworkManager, out m_ClientNetworkManagers));

            const string sceneName = nameof(NetworkObjectParentingTests);

            m_InitScene = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            Assert.That(m_TestScene.isLoaded, Is.True);
            SceneManager.SetActiveScene(m_TestScene);

            const int setCount = k_ClientInstanceCount + 1;

            Assert.That(m_ServerNetworkManager, Is.Not.Null);
            Assert.That(m_ClientNetworkManagers, Is.Not.Null);
            Assert.That(m_ClientNetworkManagers.Length, Is.EqualTo(k_ClientInstanceCount));

            m_ServerNetworkManager.NetworkConfig.NetworkTopology = m_NetworkTopologyType;
            m_ServerNetworkManager.NetworkConfig.AutoSpawnPlayerPrefabClientSide = m_NetworkTopologyType == NetworkTopologyTypes.DistributedAuthority;

            foreach (var client in m_ClientNetworkManagers)
            {
                client.NetworkConfig.NetworkTopology = m_NetworkTopologyType;
                client.NetworkConfig.AutoSpawnPlayerPrefabClientSide = m_NetworkTopologyType == NetworkTopologyTypes.DistributedAuthority;
            }

            m_Dude_NetObjs = new Transform[setCount];
            m_Dude_LeftArm_NetObjs = new Transform[setCount];
            m_Dude_RightArm_NetObjs = new Transform[setCount];
            m_Dude_LeftLeg_NetObjs = new Transform[setCount];
            m_Dude_RightLeg_NetObjs = new Transform[setCount];
            m_Cube_NetObjs = new Transform[setCount];
            m_Cube_NetBhvs = new ReparentingCubeNetBhv[setCount];
            m_Pickup_NetObjs = new Transform[setCount];
            m_Pickup_Back_NetObjs = new Transform[setCount];

            var serverSet = m_TestScene.GetRootGameObjects().FirstOrDefault(g => g.name == "Set");
            Assert.That(serverSet, Is.Not.Null);

            SetupSet(serverSet.transform, 0, m_ServerNetworkManager);
            serverSet.name = "Set0 (Server)";


            for (int setIndex = 0; setIndex < k_ClientInstanceCount; setIndex++)
            {
                var clientSet = Object.Instantiate(serverSet);
                SetupSet(clientSet.transform, setIndex + 1, m_ClientNetworkManagers[setIndex]);
                clientSet.name = $"Set{setIndex + 1} (Client)";
            }

            // Start server and client NetworkManager instances
            Assert.That(NetcodeIntegrationTestHelpers.Start(true, m_ServerNetworkManager, m_ClientNetworkManagers, InvokeBeforeClientsStart));

            // Wait for connection on client side
            yield return NetcodeIntegrationTestHelpers.WaitForClientsConnected(m_ClientNetworkManagers);

            // Wait for connection on server side
            yield return NetcodeIntegrationTestHelpers.WaitForClientConnectedToServer(m_ServerNetworkManager);
        }

        public void SetupSet(Transform rootTransform, int setIndex, NetworkManager networkManager)
        {
            foreach (Transform childTransform0 in rootTransform)
            {
                if (childTransform0.name == "Dude (NetObj)")
                {
                    m_Dude_NetObjs[setIndex] = childTransform0;
                    m_Dude_NetObjs[setIndex].GetComponent<NetworkObject>().NetworkManagerOwner = networkManager;
                    foreach (Transform childTransform1 in childTransform0)
                    {
                        if (childTransform1.name == "Arms")
                        {
                            foreach (Transform childTransform2 in childTransform1)
                            {
                                if (childTransform2.name == "LeftArm (NetObj)")
                                {
                                    m_Dude_LeftArm_NetObjs[setIndex] = childTransform2;
                                    m_Dude_LeftArm_NetObjs[setIndex].GetComponent<NetworkObject>().NetworkManagerOwner = networkManager;
                                }
                                else if (childTransform2.name == "RightArm (NetObj)")
                                {
                                    m_Dude_RightArm_NetObjs[setIndex] = childTransform2;
                                    m_Dude_RightArm_NetObjs[setIndex].GetComponent<NetworkObject>().NetworkManagerOwner = networkManager;
                                }
                            }
                        }
                        else if (childTransform1.name == "Legs")
                        {
                            foreach (Transform childTransform2 in childTransform1)
                            {
                                if (childTransform2.name == "LeftLeg (NetObj)")
                                {
                                    m_Dude_LeftLeg_NetObjs[setIndex] = childTransform2;
                                    m_Dude_LeftLeg_NetObjs[setIndex].GetComponent<NetworkObject>().NetworkManagerOwner = networkManager;
                                }
                                else if (childTransform2.name == "RightLeg (NetObj)")
                                {
                                    m_Dude_RightLeg_NetObjs[setIndex] = childTransform2;
                                    m_Dude_RightLeg_NetObjs[setIndex].GetComponent<NetworkObject>().NetworkManagerOwner = networkManager;
                                }
                            }
                        }
                    }
                }
                else if (childTransform0.name == "Cube (NetObj)")
                {
                    m_Cube_NetObjs[setIndex] = childTransform0;

                    var networkObject = childTransform0.GetComponent<NetworkObject>();
                    var networkBehaviour = childTransform0.GetComponent<ReparentingCubeNetBhv>();

                    networkObject.NetworkManagerOwner = networkManager;
                    m_Cube_NetBhvs[setIndex] = networkBehaviour;
                }
                else if (childTransform0.name == "Pickup (NetObj)")
                {
                    m_Pickup_NetObjs[setIndex] = childTransform0;
                    m_Pickup_NetObjs[setIndex].GetComponent<NetworkObject>().NetworkManagerOwner = networkManager;
                    foreach (Transform childTransform1 in childTransform0)
                    {
                        if (childTransform1.name == "Body")
                        {
                            foreach (Transform childTransform2 in childTransform1)
                            {
                                if (childTransform2.name == "Back (NetObj)")
                                {
                                    m_Pickup_Back_NetObjs[setIndex] = childTransform2;
                                    m_Pickup_Back_NetObjs[setIndex].GetComponent<NetworkObject>().NetworkManagerOwner = networkManager;
                                }
                            }
                        }
                    }
                }
            }

            Assert.That(m_Dude_NetObjs[setIndex], Is.Not.Null);
            Assert.That(m_Dude_LeftArm_NetObjs[setIndex], Is.Not.Null);
            Assert.That(m_Dude_RightArm_NetObjs[setIndex], Is.Not.Null);
            Assert.That(m_Dude_LeftLeg_NetObjs[setIndex], Is.Not.Null);
            Assert.That(m_Dude_RightLeg_NetObjs[setIndex], Is.Not.Null);
            Assert.That(m_Cube_NetObjs[setIndex], Is.Not.Null);
            Assert.That(m_Cube_NetBhvs[setIndex], Is.Not.Null);
            Assert.That(m_Cube_NetBhvs[setIndex].ParentNetworkObject, Is.Null);
            Assert.That(m_Pickup_NetObjs[setIndex], Is.Not.Null);
            Assert.That(m_Pickup_Back_NetObjs[setIndex], Is.Not.Null);

            LogAssert.Expect(LogType.Exception, new Regex("start a server or host", RegexOptions.IgnoreCase));
            var cachedParent = m_Cube_NetObjs[setIndex].parent;
            m_Cube_NetObjs[setIndex].parent = m_Pickup_NetObjs[setIndex];
            Assert.That(m_Cube_NetObjs[setIndex].parent, Is.EqualTo(cachedParent), $"Transform {m_Cube_NetObjs[setIndex].parent.name} is not equal to transform {cachedParent.name}");
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            NetcodeIntegrationTestHelpers.Destroy();

            if (m_TestScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(m_TestScene);
                SceneManager.SetActiveScene(m_InitScene);
            }
        }

        private bool AllClientsSetCubeToPickupBackCube()
        {
            for (int setIndex = 0; setIndex < k_ClientInstanceCount; setIndex++)
            {
                if (m_Cube_NetObjs[setIndex + 1].parent != m_Pickup_Back_NetObjs[setIndex + 1])
                {
                    return false;
                }

                if (m_Cube_NetBhvs[setIndex + 1].ParentNetworkObject != m_Pickup_Back_NetObjs[setIndex + 1].GetComponent<NetworkObject>())
                {
                    return false;
                }
            }

            return true;
        }

        [UnityTest]
        public IEnumerator SetParentDirect()
        {
            var timeoutHelper = new TimeoutHelper();
            // Server: Set/Cube -> Set/Pickup/Back/Cube
            m_Cube_NetObjs[0].parent = m_Pickup_Back_NetObjs[0];
            Assert.That(m_Cube_NetBhvs[0].ParentNetworkObject, Is.EqualTo(m_Pickup_Back_NetObjs[0].GetComponent<NetworkObject>()));

            // Client[n]: Set/Cube -> Set/Pickup/Back/Cube
            yield return NetcodeIntegrationTest.WaitForConditionOrTimeOut(AllClientsSetCubeToPickupBackCube, timeoutHelper);
            Assert.False(timeoutHelper.TimedOut, "TImed out waiting for Client[n]: Set/Cube -> Set/Pickup/Back/Cube");

            // Server: Set/Pickup/Back -> Root/Cube
            m_Cube_NetObjs[0].parent = null;
            Assert.That(m_Cube_NetBhvs[0].ParentNetworkObject, Is.EqualTo(null));

            bool CheckClientSideCubeForNull()
            {
                // Client[n]: Set/Pickup/Back -> Root/Cube
                for (int setIndex = 0; setIndex < k_ClientInstanceCount; setIndex++)
                {
                    if (m_Cube_NetObjs[setIndex + 1].parent != null || m_Cube_NetBhvs[setIndex + 1].ParentNetworkObject != null)
                    {
                        return false;
                    }
                }
                return true;
            }
            yield return NetcodeIntegrationTest.WaitForConditionOrTimeOut(CheckClientSideCubeForNull, timeoutHelper);
            Assert.False(timeoutHelper.TimedOut, $"Timed out waiting for client parent to be null!");

            // Server: Root/Cube -> Set/Dude/Arms/RightArm/Cube
            m_Cube_NetObjs[0].parent = m_Dude_RightArm_NetObjs[0];
            Assert.That(m_Cube_NetBhvs[0].ParentNetworkObject, Is.EqualTo(m_Dude_RightArm_NetObjs[0].GetComponent<NetworkObject>()));

            bool CheckClientSideCubeForParentAssignment()
            {
                // Client[n]: Root/Cube -> Set/Dude/Arms/RightArm/Cube
                for (int setIndex = 0; setIndex < k_ClientInstanceCount; setIndex++)
                {
                    var transformToCheckFor = m_Dude_RightArm_NetObjs[setIndex + 1];
                    if (m_Cube_NetObjs[setIndex + 1].parent != transformToCheckFor || m_Cube_NetBhvs[setIndex + 1].ParentNetworkObject != transformToCheckFor.GetComponent<NetworkObject>())
                    {
                        return false;
                    }
                }
                return true;
            }

            yield return NetcodeIntegrationTest.WaitForConditionOrTimeOut(CheckClientSideCubeForParentAssignment, timeoutHelper);
            Assert.False(timeoutHelper.TimedOut, $"Timed out waiting for client parent to be assigned!");
        }

        [UnityTest]
        public IEnumerator SetParentTryAPI()
        {
            // Try(Cube -> MainCamera/Cube): false
            var serverCachedParent = m_Cube_NetObjs[0].parent;
            var clientCachedParents = new Transform[k_ClientInstanceCount];
            for (int setIndex = 0; setIndex < k_ClientInstanceCount; setIndex++)
            {
                clientCachedParents[setIndex] = m_Cube_NetObjs[setIndex + 1].parent;
            }

            Assert.That(!m_Cube_NetObjs[0].GetComponent<NetworkObject>().TrySetParent(Camera.main.transform));

            int nextFrameNumber = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

            Assert.That(m_Cube_NetObjs[0].parent, Is.EqualTo(serverCachedParent));
            for (int setIndex = 0; setIndex < k_ClientInstanceCount; setIndex++)
            {
                Assert.That(m_Cube_NetObjs[setIndex + 1].parent, Is.EqualTo(clientCachedParents[setIndex]));
            }


            // Try(Cube -> Set/Dude/Arms/LeftArm/Cube): true
            Assert.That(m_Cube_NetObjs[0].GetComponent<NetworkObject>().TrySetParent(m_Dude_LeftArm_NetObjs[0]));
            Assert.That(m_Cube_NetBhvs[0].ParentNetworkObject, Is.EqualTo(m_Dude_LeftArm_NetObjs[0].GetComponent<NetworkObject>()));

            nextFrameNumber = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

            for (int setIndex = 0; setIndex < k_ClientInstanceCount; setIndex++)
            {
                Assert.That(m_Cube_NetObjs[setIndex + 1].parent, Is.EqualTo(m_Dude_LeftArm_NetObjs[setIndex + 1]));
                Assert.That(m_Cube_NetBhvs[setIndex + 1].ParentNetworkObject, Is.EqualTo(m_Dude_LeftArm_NetObjs[setIndex + 1].GetComponent<NetworkObject>()));
            }

            Transform nullTransform = null;
            GameObject nullGameObject = null;
            NetworkObject nullNetworkObject = null;


            Assert.That(m_Cube_NetObjs[0].GetComponent<NetworkObject>().TrySetParent(nullTransform));
            Assert.That(m_Cube_NetBhvs[0].ParentNetworkObject, Is.EqualTo(null));

            nextFrameNumber = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

            for (int setIndex = 0; setIndex < k_ClientInstanceCount; setIndex++)
            {
                Assert.That(m_Cube_NetObjs[setIndex + 1].parent, Is.EqualTo(null));
                Assert.That(m_Cube_NetBhvs[setIndex + 1].ParentNetworkObject, Is.EqualTo(null));
            }


            Assert.That(m_Cube_NetObjs[0].GetComponent<NetworkObject>().TrySetParent(nullGameObject));
            Assert.That(m_Cube_NetBhvs[0].ParentNetworkObject, Is.EqualTo(null));

            nextFrameNumber = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

            for (int setIndex = 0; setIndex < k_ClientInstanceCount; setIndex++)
            {
                Assert.That(m_Cube_NetObjs[setIndex + 1].parent, Is.EqualTo(null));
                Assert.That(m_Cube_NetBhvs[setIndex + 1].ParentNetworkObject, Is.EqualTo(null));
            }


            Assert.That(m_Cube_NetObjs[0].GetComponent<NetworkObject>().TrySetParent(nullNetworkObject));
            Assert.That(m_Cube_NetBhvs[0].ParentNetworkObject, Is.EqualTo(null));

            nextFrameNumber = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

            for (int setIndex = 0; setIndex < k_ClientInstanceCount; setIndex++)
            {
                Assert.That(m_Cube_NetObjs[setIndex + 1].parent, Is.EqualTo(null));
                Assert.That(m_Cube_NetBhvs[setIndex + 1].ParentNetworkObject, Is.EqualTo(null));
            }
        }
    }
}
