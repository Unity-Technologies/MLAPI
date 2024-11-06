using System;
using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// The RpcQueue unit tests validate:
    ///     - Maximum buffer size that can be sent (currently 1MB is the default maximum `MessageQueueHistoryFrame` size)
    ///     - That all RPCs invoke at the appropriate `NetworkUpdateStage` (Client and Server)
    ///     - A lower level `MessageQueueContainer` test that validates `MessageQueueFrameItems` after they have been put into the queue
    /// </summary>
    internal class RpcQueueTests
    {
        [SetUp]
        public void Setup()
        {
            // Create, instantiate, and host
            Assert.IsTrue(NetworkManagerHelper.StartNetworkManager(out _));
        }

        /// <summary>
        /// This tests the RPC Queue outbound and inbound buffer capabilities.
        /// </summary>
        /// <returns>IEnumerator</returns>
        [UnityTest, Order(2)]
        public IEnumerator BufferDataValidation()
        {
            Guid gameObjectId = NetworkManagerHelper.AddGameNetworkObject("GrowingBufferObject");

            var growingRpcBufferSizeComponent = NetworkManagerHelper.AddComponentToObject<BufferDataValidationComponent>(gameObjectId);

            NetworkManagerHelper.SpawnNetworkObject(gameObjectId);

            // Start Testing
            growingRpcBufferSizeComponent.EnableTesting = true;

            var testsAreComplete = growingRpcBufferSizeComponent.IsTestComplete();

            // Wait for the RPC pipeline test to complete or if we exceeded the maximum iterations bail
            while (!testsAreComplete)
            {
                yield return new WaitForSeconds(0.003f);

                testsAreComplete = growingRpcBufferSizeComponent.IsTestComplete();
            }

            // Stop Testing
            growingRpcBufferSizeComponent.EnableTesting = false;

            // Just disable this once we are done.
            growingRpcBufferSizeComponent.gameObject.SetActive(false);

            Assert.IsTrue(testsAreComplete);
        }

        [TearDown]
        public void TearDown()
        {
            // Stop, shutdown, and destroy
            NetworkManagerHelper.ShutdownNetworkManager();
        }
    }
}
