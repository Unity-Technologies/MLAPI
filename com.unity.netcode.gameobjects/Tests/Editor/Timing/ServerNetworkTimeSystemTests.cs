using NUnit.Framework;
using UnityEngine;

namespace Unity.Netcode.EditorTests
{
    internal class ServerNetworkTimeSystemTests
    {

        /// <summary>
        /// On the server local time should always be equal to server time. This test ensures that this is the case.
        /// </summary>
        [Test]
        public void LocalTimeEqualServerTimeTest()
        {
            var steps = TimingTestHelper.GetRandomTimeSteps(100f, 0.01f, 0.1f, 42);

            var serverTimeSystem = NetworkTimeSystem.ServerTimeSystem();
            var serverTickSystem = new NetworkTickSystem(60, 0, 0);

            serverTimeSystem.Reset(0.5d, 0);

            TimingTestHelper.ApplySteps(serverTimeSystem, serverTickSystem, steps, step =>
            {
                Assert.IsTrue(Mathf.Approximately((float)serverTimeSystem.LocalTime, (float)serverTimeSystem.ServerTime));
                Assert.IsTrue(Mathf.Approximately((float)serverTickSystem.LocalTime.Time, (float)serverTimeSystem.ServerTime));
            });

            Assert.IsTrue(serverTimeSystem.LocalTime > 1d);

        }

    }
}


