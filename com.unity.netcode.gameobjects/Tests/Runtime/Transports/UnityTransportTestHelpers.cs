using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using UnityEngine;

namespace Unity.Netcode.RuntimeTests
{
    internal static class UnityTransportTestHelpers
    {
        // Half a second might seem like a very long time to wait for a network event, but in CI
        // many of the machines are underpowered (e.g. old Android devices or Macs) and there are
        // sometimes very high lag spikes. PS4 and Switch are particularly sensitive in this regard
        // so we allow even more time for these platforms.
        public const float MaxNetworkEventWaitTime = 0.5f;

        private static List<UnityTransport> s_UnityTransports = new List<UnityTransport>();
        public static void RegisterTransportInstance(UnityTransport instance)
        {
            s_UnityTransports.Add(instance);
        }

        public static void DeregisterTransportInstance(UnityTransport instance)
        {
            s_UnityTransports.Remove(instance);
        }

        public static void ClearRegisteredTransportInstances()
        {
            s_UnityTransports.Clear();
        }

        public static void InvokeEarlyUpdate()
        {
            foreach (var transport in s_UnityTransports)
            {
                transport.EarlyUpdate();
            }
        }

        public static void InvokePostLateUpdate()
        {
            foreach (var transport in s_UnityTransports)
            {
                transport.PostLateUpdate();
            }
        }

        private static TimeoutHelper s_GlobalTimeoutHelper = new TimeoutHelper(MaxNetworkEventWaitTime);

        public static void AssertOnTimeout(string timeOutErrorMessage, TimeoutHelper assignedTimeoutHelper = null)
        {
            var timeoutHelper = assignedTimeoutHelper ?? s_GlobalTimeoutHelper;
            Assert.False(timeoutHelper.TimedOut, timeOutErrorMessage);
        }

        public static IEnumerator WaitForConditionOrTimeOut(Func<bool> checkForCondition, TimeoutHelper timeOutHelper = null)
        {
            if (checkForCondition == null)
            {
                throw new ArgumentNullException($"checkForCondition cannot be null!");
            }

            // If none is provided we use the default global time out helper
            if (timeOutHelper == null)
            {
                timeOutHelper = s_GlobalTimeoutHelper;
            }

            var waitUntilEndofFrame = new WaitForEndOfFrame();
            var waitForNextFrame = new WaitForFixedUpdate();

            // Start checking for a timeout
            timeOutHelper.Start();
            while (!timeOutHelper.HasTimedOut())
            {
                yield return waitForNextFrame;
                InvokeEarlyUpdate();
                // Update and check to see if the condition has been met
                if (checkForCondition.Invoke())
                {
                    break;
                }
                yield return waitUntilEndofFrame;
                InvokePostLateUpdate();
            }

            // Stop checking for a timeout
            timeOutHelper.Stop();
        }


        // Wait for an event to appear in the given event list (must be the very next event).
        public static IEnumerator WaitForNetworkEvent(NetworkEvent type, List<TransportEvent> events, float timeout = MaxNetworkEventWaitTime)
        {
            int initialCount = events.Count;
            float timedOut = Time.realtimeSinceStartup + timeout;
            var waitUntilEndofFrame = new WaitForEndOfFrame();
            var waitForNextFrame = new WaitForFixedUpdate();
            var success = false;
            while (timedOut > Time.realtimeSinceStartup)
            {
                yield return waitForNextFrame;
                InvokeEarlyUpdate();
                if (events.Count > initialCount)
                {
                    Assert.AreEqual(type, events[initialCount].Type);
                    success = true;

                }
                yield return waitUntilEndofFrame;
                InvokePostLateUpdate();
            }

            Assert.IsTrue(success, "Timed out while waiting for network event.");
        }

        // Common code to initialize a UnityTransport that logs its events.
        public static void InitializeTransport(out UnityTransport transport, out List<TransportEvent> events,
            int maxPayloadSize = UnityTransport.InitialMaxPayloadSize, int maxSendQueueSize = 0, NetworkFamily family = NetworkFamily.Ipv4)
        {
            var logger = new TransportEventLogger();
            events = logger.Events;

            transport = new GameObject().AddComponent<UnityTransport>();

            transport.OnTransportEvent += logger.HandleEvent;
            transport.MaxPayloadSize = maxPayloadSize;
            transport.MaxSendQueueSize = maxSendQueueSize;

            if (family == NetworkFamily.Ipv6)
            {
                transport.SetConnectionData("::1", 7777);
            }

            transport.Initialize();
            RegisterTransportInstance(transport);
        }

        // Information about an event generated by a transport (basically just the parameters that
        // are normally passed along to a TransportEventDelegate).
        internal struct TransportEvent
        {
            public NetworkEvent Type;
            public ulong ClientID;
            public ArraySegment<byte> Data;
            public float ReceiveTime;
        }
        // Utility class that logs events generated by a UnityTransport. Set it up by adding the
        // HandleEvent method as an OnTransportEvent delegate of the transport. The list of events
        // (in order in which they were generated) can be accessed through the Events property.
        internal class TransportEventLogger
        {
            private readonly List<TransportEvent> m_Events = new List<TransportEvent>();
            public List<TransportEvent> Events => m_Events;
            public void HandleEvent(NetworkEvent type, ulong clientID, ArraySegment<byte> data, float receiveTime)
            {
                // Copy the data since the backing array will be reused for future messages.
                if (data != default(ArraySegment<byte>))
                {
                    var dataCopy = new byte[data.Count];
                    Array.Copy(data.Array, data.Offset, dataCopy, 0, data.Count);
                    data = new ArraySegment<byte>(dataCopy);
                }
                m_Events.Add(new TransportEvent
                {
                    Type = type,
                    ClientID = clientID,
                    Data = data,
                    ReceiveTime = receiveTime
                });
            }
        }
    }
}
