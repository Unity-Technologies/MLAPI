using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.Assertions.Comparers;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// Runtime tests to test the network time system with the Unity player loop.
    /// </summary>
    internal class NetworkTimeSystemTests
    {
        private MonoBehaviourTest<PlayerLoopFixedTimeTestComponent> m_PlayerLoopFixedTimeTestComponent; // cache for teardown
        private MonoBehaviourTest<PlayerLoopTimeTestComponent> m_PlayerLoopTimeTestComponent; // cache for teardown

        private float m_OriginalTimeScale = 1.0f;

        [SetUp]
        public void Setup()
        {
            m_OriginalTimeScale = Time.timeScale;

            // Create, instantiate, and host
            Assert.IsTrue(NetworkManagerHelper.StartNetworkManager(out _));
        }

        /// <summary>
        /// Tests whether time is accessible and has correct values inside Update/FixedUpdate.
        /// This test applies only when <see cref="Time.timeScale"> is 1.
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator PlayerLoopFixedTimeTest()
        {
            m_PlayerLoopFixedTimeTestComponent = new MonoBehaviourTest<PlayerLoopFixedTimeTestComponent>();

            yield return m_PlayerLoopFixedTimeTestComponent;
        }

        /// <summary>
        /// Tests whether time is accessible and has correct values inside Update, for multiples <see cref="Time.timeScale"/> values.
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator PlayerLoopTimeTest_WithDifferentTimeScale([Values(0.0f, 0.1f, 0.5f, 1.0f, 2.0f, 5.0f)] float timeScale)
        {
            Time.timeScale = timeScale;

            m_PlayerLoopTimeTestComponent = new MonoBehaviourTest<PlayerLoopTimeTestComponent>();

            yield return m_PlayerLoopTimeTestComponent;
        }

        /// <summary>
        /// Tests whether the time system invokes the correct amount of ticks over a period of time.
        /// Note we cannot test against Time.Time directly because of floating point precision. Our time is more precise leading to different results.
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator CorrectAmountTicksTest()
        {
            NetworkTickSystem tickSystem = NetworkManager.Singleton.NetworkTickSystem;
            float delta = tickSystem.LocalTime.FixedDeltaTime;
            int previous_localTickCalculated = 0;
            int previous_serverTickCalculated = 0;

            while (tickSystem.LocalTime.Time < 3f)
            {
                yield return null;

                var tickCalculated = tickSystem.LocalTime.Time / delta;
                previous_localTickCalculated = (int)tickCalculated;

                // This check is needed due to double division imprecision of large numbers
                if ((tickCalculated - previous_localTickCalculated) >= 0.999999999999)
                {
                    previous_localTickCalculated++;
                }


                tickCalculated = NetworkManager.Singleton.ServerTime.Time / delta;
                previous_serverTickCalculated = (int)tickCalculated;

                // This check is needed due to double division imprecision of large numbers
                if ((tickCalculated - previous_serverTickCalculated) >= 0.999999999999)
                {
                    previous_serverTickCalculated++;
                }

                Assert.AreEqual(previous_localTickCalculated, NetworkManager.Singleton.LocalTime.Tick, $"Calculated local tick {previous_localTickCalculated} does not match local tick {NetworkManager.Singleton.LocalTime.Tick}!");
                Assert.AreEqual(previous_serverTickCalculated, NetworkManager.Singleton.ServerTime.Tick, $"Calculated server tick {previous_serverTickCalculated} does not match server tick {NetworkManager.Singleton.ServerTime.Tick}!");
                Assert.AreEqual((float)NetworkManager.Singleton.LocalTime.Time, (float)NetworkManager.Singleton.ServerTime.Time, $"Local time {(float)NetworkManager.Singleton.LocalTime.Time} is not approximately server time {(float)NetworkManager.Singleton.ServerTime.Time}!", FloatComparer.s_ComparerWithDefaultTolerance);
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Stop, shutdown, and destroy
            NetworkManagerHelper.ShutdownNetworkManager();

            Time.timeScale = m_OriginalTimeScale;

            if (m_PlayerLoopFixedTimeTestComponent != null)
            {
                Object.DestroyImmediate(m_PlayerLoopFixedTimeTestComponent.gameObject);
                m_PlayerLoopFixedTimeTestComponent = null;
            }

            if (m_PlayerLoopTimeTestComponent != null)
            {
                Object.DestroyImmediate(m_PlayerLoopTimeTestComponent.gameObject);
                m_PlayerLoopTimeTestComponent = null;
            }
        }
    }

    internal class PlayerLoopFixedTimeTestComponent : MonoBehaviour, IMonoBehaviourTest
    {
        public const int Passes = 100;

        private int m_UpdatePasses = 0;

        private int m_LastFixedUpdateTick = 0;
        private int m_TickOffset = -1;

        private NetworkTime m_LocalTimePreviousUpdate;
        private NetworkTime m_ServerTimePreviousUpdate;
        private NetworkTime m_LocalTimePreviousFixedUpdate;

        private void Start()
        {
            // Run fixed update at same rate as network tick
            Time.fixedDeltaTime = NetworkManager.Singleton.LocalTime.FixedDeltaTime;

            // Uncap fixed time else we might skip fixed updates
            Time.maximumDeltaTime = float.MaxValue;
        }

        private void Update()
        {
            // This must run first else it wont run if there is an exception
            m_UpdatePasses++;

            NetworkTime localTime = NetworkManager.Singleton.LocalTime;
            NetworkTime serverTime = NetworkManager.Singleton.ServerTime;

            // time should have advanced on the host/server
            Assert.Less(m_LocalTimePreviousUpdate.Time, localTime.Time);
            Assert.Less(m_ServerTimePreviousUpdate.Time, serverTime.Time);

            // time should be further then last fixed step in update
            Assert.Less(m_LocalTimePreviousFixedUpdate.FixedTime, localTime.Time);

            // we should be in same or further tick then fixed update
            Assert.LessOrEqual(m_LocalTimePreviousFixedUpdate.Tick, localTime.Tick);

            // fixed update should result in same amounts of tick as network time
            if (m_TickOffset == -1)
            {
                m_TickOffset = serverTime.Tick - m_LastFixedUpdateTick;
            }
            else
            {
                // offset of 1 is ok, this happens due to different tick duration offsets
                Assert.LessOrEqual(Mathf.Abs(serverTime.Tick - m_TickOffset - m_LastFixedUpdateTick), 1);
            }

            m_LocalTimePreviousUpdate = localTime;
            m_ServerTimePreviousUpdate = serverTime;
        }

        private void FixedUpdate()
        {
            m_LocalTimePreviousFixedUpdate = NetworkManager.Singleton.LocalTime;

            Assert.AreEqual(Time.fixedDeltaTime, m_LocalTimePreviousFixedUpdate.FixedDeltaTime);
            Assert.AreEqual((float)NetworkManager.Singleton.LocalTime.Time, (float)NetworkManager.Singleton.ServerTime.Time, null, FloatComparer.s_ComparerWithDefaultTolerance);
            m_LastFixedUpdateTick++;
        }

        public bool IsTestFinished => m_UpdatePasses >= Passes;
    }

    internal class PlayerLoopTimeTestComponent : MonoBehaviour, IMonoBehaviourTest
    {
        public const int Passes = 100;

        private int m_UpdatePasses = 0;

        private NetworkTime m_LocalTimePreviousUpdate;
        private NetworkTime m_ServerTimePreviousUpdate;
        private NetworkTime m_LocalTimePreviousFixedUpdate;

        private void Update()
        {
            // This must run first else it wont run if there is an exception
            m_UpdatePasses++;

            NetworkTime localTime = NetworkManager.Singleton.LocalTime;
            NetworkTime serverTime = NetworkManager.Singleton.ServerTime;

            // time should have advanced on the host/server
            Assert.Less(m_LocalTimePreviousUpdate.Time, localTime.Time);
            Assert.Less(m_ServerTimePreviousUpdate.Time, serverTime.Time);

            // time should be further then last fixed step in update
            Assert.Less(m_LocalTimePreviousFixedUpdate.FixedTime, localTime.Time);

            // we should be in same or further tick then fixed update
            Assert.LessOrEqual(m_LocalTimePreviousFixedUpdate.Tick, localTime.Tick);

            m_LocalTimePreviousUpdate = localTime;
            m_ServerTimePreviousUpdate = serverTime;
        }

        private void FixedUpdate()
        {
            m_LocalTimePreviousFixedUpdate = NetworkManager.Singleton.LocalTime;
        }

        public bool IsTestFinished => m_UpdatePasses >= Passes;
    }

}
