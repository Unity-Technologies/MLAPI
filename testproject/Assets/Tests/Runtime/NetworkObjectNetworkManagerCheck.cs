using System.Collections;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.TestTools;
using Unity.Netcode.RuntimeTests;
using Unity.Netcode;

namespace TestProject.RuntimeTests
{
    /// <summary>
    /// Unit Test to make sure that a NetworkObject cannot be a child of a GameObject with a NetworkManager component
    /// </summary>
    public class NetworkObjectNetworkManagerCheck : BaseMultiInstanceTest
    {
        protected override int NbClients => 1;

        [UnityTest]
        public IEnumerator CheckNetworkManagerAsParent()
        {
            var serverClientPlayerResult = new MultiInstanceHelpers.CoroutineResultWrapper<NetworkObject>();
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation((x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId), m_ServerNetworkManager, serverClientPlayerResult));

            serverClientPlayerResult.Result.transform.parent = m_ServerNetworkManager.transform;

            var waitUntilFrameNumber = Time.frameCount + 3;
            yield return new WaitUntil(() => Time.frameCount >= waitUntilFrameNumber);

            Assert.Null(serverClientPlayerResult.Result.transform.parent);
        }
    }
}
