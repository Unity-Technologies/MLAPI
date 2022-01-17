using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Netcode.UTP.RuntimeTests
{
    public static class RuntimeTestsHelpers
    {
        // Half a second might seem like a very long time to wait for a network event, but in CI
        // many of the machines are underpowered (e.g. old Android devices or Macs) and there are
        // sometimes very high lag spikes. PS4 and Switch are particularly sensitive in this regard
        // so we allow even more time for these platforms.
#if UNITY_PS4 || UNITY_SWITCH
        public const float MaxNetworkEventWaitTime = 2.0f;
#else
        public const float MaxNetworkEventWaitTime = 0.5f;
#endif

        // Wait for an event to appear in the given event list (must be the very next event).
        public static IEnumerator WaitForNetworkEvent(NetworkEvent type, List<TransportEvent> events)
        {
            int initialCount = events.Count;
            float startTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startTime < MaxNetworkEventWaitTime)
            {
                if (events.Count > initialCount)
                {
                    Assert.AreEqual(type, events[initialCount].Type);
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Timed out while waiting for network event.");
        }

        // Common code to initialize a UnityTransport that logs its events.
        public static void InitializeTransport(out UnityTransport transport, out List<TransportEvent> events,
            int maxPayloadSize = UnityTransport.InitialBatchQueueSize)
        {
            var logger = new TransportEventLogger();
            events = logger.Events;

            transport = new GameObject().AddComponent<UnityTransport>();
            transport.OnTransportEvent += logger.HandleEvent;
            transport.SetMaxPayloadSize(maxPayloadSize);
            transport.Initialize();
        }

        // Information about an event generated by a transport (basically just the parameters that
        // are normally passed along to a TransportEventDelegate).
        public struct TransportEvent
        {
            public NetworkEvent Type;
            public ulong ClientID;
            public ArraySegment<byte> Data;
            public float ReceiveTime;
        }

        // Utility class that logs events generated by a UnityTransport. Set it up by adding the
        // HandleEvent method as an OnTransportEvent delegate of the transport. The list of events
        // (in order in which they were generated) can be accessed through the Events property.
        public class TransportEventLogger
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
