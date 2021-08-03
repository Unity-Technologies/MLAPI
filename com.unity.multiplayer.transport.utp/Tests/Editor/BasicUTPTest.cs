using UnityEngine;
using NUnit.Framework;

namespace Unity.Multiplayer.Netcode.UTP.RuntimeTests
{
    public class BasicUTPTest : MonoBehaviour
    {
        [Test]
        public void BasicUTPInitializationTest()
        {
            var o = new GameObject();
            var utpTransport = (UTPTransport)o.AddComponent(typeof(UTPTransport));
            utpTransport.Init();

            Assert.True(utpTransport.ServerClientId == 0);

            utpTransport.Shutdown();
        }
    }
}


