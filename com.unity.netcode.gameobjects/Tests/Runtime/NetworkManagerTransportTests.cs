using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    internal class NetworkManagerTransportTests
    {
        [Test]
        public void ClientDoesNotStartWhenTransportFails()
        {
            bool callbackInvoked = false;
            Action onTransportFailure = () => { callbackInvoked = true; };

            var manager = new GameObject().AddComponent<NetworkManager>();
            manager.OnTransportFailure += onTransportFailure;

            var transport = manager.gameObject.AddComponent<FailedTransport>();
            transport.FailOnStart = true;

            manager.NetworkConfig = new NetworkConfig() { NetworkTransport = transport };

            LogAssert.Expect(LogType.Error, $"[Netcode] Client is shutting down due to network transport start failure of {transport.GetType().Name}!");

            Assert.False(manager.StartClient());
            Assert.False(manager.IsListening);
            Assert.False(manager.IsConnectedClient);

            Assert.True(callbackInvoked);
        }

        [Test]
        public void HostDoesNotStartWhenTransportFails()
        {
            bool callbackInvoked = false;
            Action onTransportFailure = () => { callbackInvoked = true; };

            var manager = new GameObject().AddComponent<NetworkManager>();
            manager.OnTransportFailure += onTransportFailure;

            var transport = manager.gameObject.AddComponent<FailedTransport>();
            transport.FailOnStart = true;

            manager.NetworkConfig = new NetworkConfig() { NetworkTransport = transport };

            LogAssert.Expect(LogType.Error, $"[Netcode] Host is shutting down due to network transport start failure of {transport.GetType().Name}!");

            Assert.False(manager.StartHost());
            Assert.False(manager.IsListening);

            Assert.True(callbackInvoked);
        }

        [Test]
        public void ServerDoesNotStartWhenTransportFails()
        {
            bool callbackInvoked = false;
            Action onTransportFailure = () => { callbackInvoked = true; };

            var manager = new GameObject().AddComponent<NetworkManager>();
            manager.OnTransportFailure += onTransportFailure;

            var transport = manager.gameObject.AddComponent<FailedTransport>();
            transport.FailOnStart = true;

            manager.NetworkConfig = new NetworkConfig() { NetworkTransport = transport };

            LogAssert.Expect(LogType.Error, $"[Netcode] Server is shutting down due to network transport start failure of {transport.GetType().Name}!");

            Assert.False(manager.StartServer());
            Assert.False(manager.IsListening);

            Assert.True(callbackInvoked);
        }

        [UnityTest]
        public IEnumerator ShutsDownWhenTransportFails()
        {
            bool callbackInvoked = false;
            Action onTransportFailure = () => { callbackInvoked = true; };

            var manager = new GameObject().AddComponent<NetworkManager>();
            manager.OnTransportFailure += onTransportFailure;

            var transport = manager.gameObject.AddComponent<FailedTransport>();
            transport.FailOnNextPoll = true;

            manager.NetworkConfig = new NetworkConfig() { NetworkTransport = transport };

            Assert.True(manager.StartServer());
            Assert.True(manager.IsListening);

            LogAssert.Expect(LogType.Error, $"[Netcode] Server is shutting down due to network transport failure of {transport.GetType().Name}!");

            // Need two updates to actually shut down. First one to see the transport failing, which
            // marks the NetworkManager as shutting down. Second one where actual shutdown occurs.
            yield return null;
            yield return null;

            Assert.False(manager.IsListening);
            Assert.True(callbackInvoked);
        }

        /// <summary>
        /// Does nothing but simulate a transport that can fail at startup and/or when polling events.
        /// </summary>
        internal class FailedTransport : TestingNetworkTransport
        {
            public bool FailOnStart = false;
            public bool FailOnNextPoll = false;

            public override bool StartClient() => !FailOnStart;

            public override bool StartServer() => !FailOnStart;

            public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
            {
                clientId = 0;
                payload = new ArraySegment<byte>();
                receiveTime = 0;

                if (FailOnNextPoll)
                {
                    FailOnNextPoll = false;
                    return NetworkEvent.TransportFailure;
                }
                else
                {
                    return NetworkEvent.Nothing;
                }
            }

            public override ulong ServerClientId => 0;

            public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
            {
            }

            public override void Initialize(NetworkManager networkManager = null)
            {
            }

            public override void Shutdown()
            {
            }

            public override ulong GetCurrentRtt(ulong clientId) => 0;

            public override void DisconnectRemoteClient(ulong clientId)
            {
            }

            public override void DisconnectLocalClient()
            {
            }
        }
    }
}
