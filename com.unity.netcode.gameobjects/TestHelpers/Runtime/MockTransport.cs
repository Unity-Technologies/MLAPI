using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace Unity.Netcode.TestHelpers.Runtime
{
    internal class MockTransport : NetworkTransport
    {
        private struct MessageData
        {
            public ulong FromClientId;
            public ArraySegment<byte> Payload;
            public NetworkEvent Event;
            public float AvailableTime;
        }

        private static Dictionary<ulong, Queue<MessageData>> s_MessageQueue = new Dictionary<ulong, Queue<MessageData>>();

        public override ulong ServerClientId { get; } = 0;

        public static ulong HighTransportId = 0;
        public ulong TransportId = 0;
        public float SimulatedLatencySeconds;
        public float PacketDropRate;
        public float LatencyJitter;

        public NetworkManager NetworkManager;

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
        {
            if (Random.Range(0, 1) < PacketDropRate)
            {
                return;
            }
            var copy = new byte[payload.Array.Length];
            Array.Copy(payload.Array, copy, payload.Array.Length);
            s_MessageQueue[clientId].Enqueue(new MessageData
            {
                FromClientId = TransportId,
                Payload = new ArraySegment<byte>(copy, payload.Offset, payload.Count),
                Event = NetworkEvent.Data,
                AvailableTime =
                NetworkManager.RealTimeProvider.UnscaledTime + SimulatedLatencySeconds + Random.Range(-LatencyJitter, LatencyJitter)
            });
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            if (s_MessageQueue[TransportId].Count > 0)
            {
                var data = s_MessageQueue[TransportId].Peek();
                if (data.AvailableTime > NetworkManager.RealTimeProvider.UnscaledTime)
                {
                    clientId = 0;
                    payload = new ArraySegment<byte>();
                    receiveTime = 0;
                    return NetworkEvent.Nothing;
                }

                s_MessageQueue[TransportId].Dequeue();
                clientId = data.FromClientId;
                payload = data.Payload;
                receiveTime = NetworkManager.RealTimeProvider.RealTimeSinceStartup;
                if (NetworkManager.IsServer && data.Event == NetworkEvent.Connect)
                {
                    s_MessageQueue[data.FromClientId].Enqueue(new MessageData { Event = NetworkEvent.Connect, FromClientId = ServerClientId, Payload = new ArraySegment<byte>() });
                }
                return data.Event;
            }
            clientId = 0;
            payload = new ArraySegment<byte>();
            receiveTime = 0;
            return NetworkEvent.Nothing;
        }

        public override bool StartClient()
        {
            TransportId = ++HighTransportId;
            s_MessageQueue[TransportId] = new Queue<MessageData>();
            s_MessageQueue[ServerClientId].Enqueue(new MessageData { Event = NetworkEvent.Connect, FromClientId = TransportId, Payload = new ArraySegment<byte>() });
            return true;
        }

        public override bool StartServer()
        {
            s_MessageQueue[ServerClientId] = new Queue<MessageData>();
            return true;
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            s_MessageQueue[clientId].Enqueue(new MessageData { Event = NetworkEvent.Disconnect, FromClientId = TransportId, Payload = new ArraySegment<byte>() });
        }

        public override void DisconnectLocalClient()
        {
            s_MessageQueue[ServerClientId].Enqueue(new MessageData { Event = NetworkEvent.Disconnect, FromClientId = TransportId, Payload = new ArraySegment<byte>() });
        }

        public override ulong GetCurrentRtt(ulong clientId)
        {
            return 0;
        }

        public override void Shutdown()
        {
        }

        protected override NetworkTopologyTypes OnCurrentTopology()
        {
            return NetworkManager != null ? NetworkManager.NetworkConfig.NetworkTopology : NetworkTopologyTypes.ClientServer;
        }

        public override void Initialize(NetworkManager networkManager = null)
        {
            NetworkManager = networkManager;
        }

        public static void Reset()
        {
            s_MessageQueue.Clear();
            HighTransportId = 0;
        }

        public static void ClearQueues()
        {
            foreach (var kvp in s_MessageQueue)
            {
                kvp.Value.Clear();
            }
        }
    }
}
