using NUnit.Framework;
using UnityEngine;

namespace Unity.Netcode.UTP.EditorTests
{
    public class UnityTransportTests
    {
        // Check that starting a server doesn't immediately result in faulted tasks.
        [Test]
        public void BasicInitServer()
        {
            UTPTransport transport = new GameObject().AddComponent<UTPTransport>();
            transport.Initialize();

            var tasks = transport.StartServer();
            Assert.False(tasks.IsDone && !tasks.Success);

            transport.Shutdown();
        }

        // Check that starting a client doesn't immediately result in faulted tasks.
        [Test]
        public void BasicInitClient()
        {
            UTPTransport transport = new GameObject().AddComponent<UTPTransport>();
            transport.Initialize();

            var tasks = transport.StartClient();
            Assert.False(tasks.IsDone && !tasks.Success);

            transport.Shutdown();
        }

        // Check that we can't restart a server.
        [Test]
        public void NoRestartServer()
        {
            UTPTransport transport = new GameObject().AddComponent<UTPTransport>();
            transport.Initialize();

            transport.StartServer();
            var tasks = transport.StartServer();
            Assert.True(tasks.IsDone && !tasks.AnySuccess);

            transport.Shutdown();
        }

        // Check that we can't restart a client.
        [Test]
        public void NoRestartClient()
        {
            UTPTransport transport = new GameObject().AddComponent<UTPTransport>();
            transport.Initialize();

            transport.StartClient();
            var tasks = transport.StartClient();
            Assert.True(tasks.IsDone && !tasks.AnySuccess);

            transport.Shutdown();
        }

        // Check that we can't start both a server and client on the same transport.
        [Test]
        public void NotBothServerAndClient()
        {
            UTPTransport transport;
            SocketTasks tasks;

            // Start server then client.
            transport = new GameObject().AddComponent<UTPTransport>();
            transport.Initialize();

            transport.StartServer();
            tasks = transport.StartClient();
            Assert.True(tasks.IsDone && !tasks.AnySuccess);

            transport.Shutdown();

            // Start client then server.
            transport = new GameObject().AddComponent<UTPTransport>();
            transport.Initialize();

            transport.StartClient();
            tasks = transport.StartServer();
            Assert.True(tasks.IsDone && !tasks.AnySuccess);

            transport.Shutdown();
        }
    }
}


