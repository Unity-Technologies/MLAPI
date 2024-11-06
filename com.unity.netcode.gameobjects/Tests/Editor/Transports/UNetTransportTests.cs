#if UNITY_UNET_PRESENT
#pragma warning disable 618 // disable is obsolete
using NUnit.Framework;
using Unity.Netcode.Transports.UNET;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.EditorTests
{
    internal class UNetTransportTests
    {
        [Test]
        public void StartServerReturnsFalseOnFailure()
        {
            UNetTransport unet1 = null;
            UNetTransport unet2 = null;

            try
            {
                // Arrange

                // We're expecting an error from UNET, but don't care to validate the specific message
                LogAssert.ignoreFailingMessages = true;

                var go = new GameObject();
                unet1 = go.AddComponent<UNetTransport>();
                unet1.ServerListenPort = 1;
                unet1.Initialize();
                unet1.StartServer();
                unet2 = go.AddComponent<UNetTransport>();
                unet2.ServerListenPort = 1;
                unet2.Initialize();

                // Act
                var result = unet2.StartServer();

                // Assert
                Assert.IsFalse(result, "UNET fails to initialize against port already in use");
            }
            finally
            {
                unet1?.Shutdown();
                unet2?.Shutdown();
            }
        }
    }
}
#pragma warning restore 618
#endif
