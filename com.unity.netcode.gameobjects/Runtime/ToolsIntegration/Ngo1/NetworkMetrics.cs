using System;
using System.Collections.Generic;
using Unity.Multiplayer.Tools.MetricTypes;
using Unity.Multiplayer.Tools.NetStats;
using Unity.Profiling;

namespace Unity.Netcode
{

    internal class NetworkMetrics : INetworkMetrics
    {
        private const ulong k_MaxMetricsPerFrame = 1000L;
        private static Dictionary<uint, string> s_SceneEventTypeNames;
        private static ProfilerMarker s_FrameDispatch = new ProfilerMarker($"{nameof(NetworkMetrics)}.DispatchFrame");

        static NetworkMetrics()
        {
            s_SceneEventTypeNames = new Dictionary<uint, string>();
            foreach (SceneEventType type in Enum.GetValues(typeof(SceneEventType)))
            {
                s_SceneEventTypeNames[(uint)type] = type.ToString();
            }
        }

        private static string GetSceneEventTypeName(uint typeCode)
        {
            if (!s_SceneEventTypeNames.TryGetValue(typeCode, out string name))
            {
                name = "Unknown";
            }

            return name;
        }

        private ulong m_NumberOfMetricsThisFrame;

        internal DefaultMetricCollection Collector { get; } = new();

        internal event Action<IMetricCollection> OnMetricsDispatched;

        private bool CanSendMetrics => m_NumberOfMetricsThisFrame < k_MaxMetricsPerFrame;

        public void SetConnectionId(ulong connectionId)
        {
            Collector.SetConnectionId(connectionId);
        }

        public void TrackTransportBytesSent(long bytesCount)
        {
            Collector.IncrementTransportBytesSent(bytesCount);
        }

        public void TrackTransportBytesReceived(long bytesCount)
        {
            Collector.IncrementTransportBytesReceived(bytesCount);
        }

        public void TrackNetworkMessageSent(ulong receivedClientId, string messageType, long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackNetworkMessageSent(receivedClientId, messageType, bytesCount);
            IncrementMetricCount();
        }

        public void TrackNetworkMessageReceived(ulong senderClientId, string messageType, long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackNetworkMessageReceived(senderClientId, StringConversionUtility.ConvertToFixedString(messageType), bytesCount);
            IncrementMetricCount();
        }

        public void TrackNamedMessageSent(ulong receiverClientId, string messageName, long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackNamedMessageSent(receiverClientId, messageName, bytesCount);
            IncrementMetricCount();
        }

        public void TrackNamedMessageSent(IReadOnlyCollection<ulong> receiverClientIds, string messageName, long bytesCount)
        {
            foreach (var receiver in receiverClientIds)
            {
                TrackNamedMessageSent(receiver, messageName, bytesCount);
            }
        }

        public void TrackNamedMessageReceived(ulong senderClientId, string messageName, long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackNamedMessageReceived(senderClientId, messageName, bytesCount);
            IncrementMetricCount();
        }

        public void TrackUnnamedMessageSent(ulong receiverClientId, long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackUnnamedMessageSent(receiverClientId, bytesCount);
            // m_UnnamedMessageSentEvent.Mark(new UnnamedMessageEvent(new ConnectionInfo(receiverClientId), bytesCount));
            IncrementMetricCount();
        }

        public void TrackUnnamedMessageSent(IReadOnlyCollection<ulong> receiverClientIds, long bytesCount)
        {
            foreach (var receiverClientId in receiverClientIds)
            {
                TrackUnnamedMessageSent(receiverClientId, bytesCount);
            }
        }

        public void TrackUnnamedMessageReceived(ulong senderClientId, long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackUnnamedMessageReceived(senderClientId, bytesCount);
            IncrementMetricCount();
        }

        public void TrackNetworkVariableDeltaSent(
            ulong receiverClientId,
            NetworkObject networkObject,
            string variableName,
            string networkBehaviourName,
            long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackNetworkVariableDeltaSent(receiverClientId,
                StringConversionUtility.ConvertToFixedString(networkObject.GetNameForMetrics()),
                networkObject.NetworkObjectId,
                StringConversionUtility.ConvertToFixedString(variableName),
                StringConversionUtility.ConvertToFixedString(networkBehaviourName),
                bytesCount);
            IncrementMetricCount();
        }

        public void TrackNetworkVariableDeltaReceived(
            ulong senderClientId,
            NetworkObject networkObject,
            string variableName,
            string networkBehaviourName,
            long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }
            Collector.TrackNetworkVariableDeltaReceived(senderClientId,
                StringConversionUtility.ConvertToFixedString(networkObject.GetNameForMetrics()),
                networkObject.NetworkObjectId,
                StringConversionUtility.ConvertToFixedString(variableName),
                StringConversionUtility.ConvertToFixedString(networkBehaviourName),
                bytesCount);
            IncrementMetricCount();
        }

        public void TrackOwnershipChangeSent(ulong receiverClientId, NetworkObject networkObject, long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackOwnershipChangeSent(receiverClientId,
                StringConversionUtility.ConvertToFixedString(networkObject.GetNameForMetrics()),
                networkObject.NetworkObjectId,
                bytesCount);
            IncrementMetricCount();
        }

        public void TrackOwnershipChangeReceived(ulong senderClientId, NetworkObject networkObject, long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackOwnershipChangeReceived(senderClientId,
                StringConversionUtility.ConvertToFixedString(networkObject.GetNameForMetrics()),
                networkObject.NetworkObjectId,
                bytesCount);
            IncrementMetricCount();
        }

        public void TrackObjectSpawnSent(ulong receiverClientId, NetworkObject networkObject, long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackObjectSpawnSent(receiverClientId,
                StringConversionUtility.ConvertToFixedString(networkObject.GetNameForMetrics()),
                networkObject.NetworkObjectId,
                bytesCount);
            IncrementMetricCount();
        }

        public void TrackObjectSpawnReceived(ulong senderClientId, NetworkObject networkObject, long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackObjectSpawnReceived(senderClientId,
                StringConversionUtility.ConvertToFixedString(networkObject.GetNameForMetrics()),
                networkObject.NetworkObjectId,
                bytesCount);
            IncrementMetricCount();
        }

        public void TrackObjectDestroySent(ulong receiverClientId, NetworkObject networkObject, long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackObjectDestroySent(receiverClientId,
                StringConversionUtility.ConvertToFixedString(networkObject.GetNameForMetrics()),
                networkObject.NetworkObjectId,
                bytesCount);
            IncrementMetricCount();
        }

        public void TrackObjectDestroyReceived(ulong senderClientId, NetworkObject networkObject, long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackObjectDestroyReceived(senderClientId,
                StringConversionUtility.ConvertToFixedString(networkObject.GetNameForMetrics()),
                networkObject.NetworkObjectId,
                bytesCount);
            IncrementMetricCount();
        }

        public void TrackRpcSent(
            ulong receiverClientId,
            NetworkObject networkObject,
            string rpcName,
            string networkBehaviourName,
            long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackRpcSent(receiverClientId,
                StringConversionUtility.ConvertToFixedString(networkObject.GetNameForMetrics()),
                networkObject.NetworkObjectId,
                StringConversionUtility.ConvertToFixedString(rpcName),
                StringConversionUtility.ConvertToFixedString(networkBehaviourName),
                bytesCount);
            IncrementMetricCount();
        }

        public void TrackRpcSent(
            ulong[] receiverClientIds,
            NetworkObject networkObject,
            string rpcName,
            string networkBehaviourName,
            long bytesCount)
        {
            foreach (var receiverClientId in receiverClientIds)
            {
                TrackRpcSent(receiverClientId, networkObject, rpcName, networkBehaviourName, bytesCount);
            }
        }

        public void TrackRpcReceived(
            ulong senderClientId,
            NetworkObject networkObject,
            string rpcName,
            string networkBehaviourName,
            long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackRpcReceived(senderClientId,
                StringConversionUtility.ConvertToFixedString(networkObject.GetNameForMetrics()),
                networkObject.NetworkObjectId,
                StringConversionUtility.ConvertToFixedString(rpcName),
                StringConversionUtility.ConvertToFixedString(networkBehaviourName),
                bytesCount);
            IncrementMetricCount();
        }

        public void TrackServerLogSent(ulong receiverClientId, uint logType, long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackServerLogSent(receiverClientId, logType, bytesCount);
            IncrementMetricCount();
        }

        public void TrackServerLogReceived(ulong senderClientId, uint logType, long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackServerLogReceived(senderClientId, logType, bytesCount);
            IncrementMetricCount();
        }

        public void TrackSceneEventSent(IReadOnlyList<ulong> receiverClientIds, uint sceneEventType, string sceneName, long bytesCount)
        {
            foreach (var receiverClientId in receiverClientIds)
            {
                TrackSceneEventSent(receiverClientId, sceneEventType, sceneName, bytesCount);
            }
        }

        public void TrackSceneEventSent(ulong receiverClientId, uint sceneEventType, string sceneName, long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackSceneEventSent(receiverClientId,
                // TODO: overload that returns a FixedString64Bytes without conversion?
                StringConversionUtility.ConvertToFixedString(GetSceneEventTypeName(sceneEventType)),
                StringConversionUtility.ConvertToFixedString(sceneName),
                bytesCount);
            IncrementMetricCount();
        }

        public void TrackSceneEventReceived(ulong senderClientId, uint sceneEventType, string sceneName, long bytesCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.TrackSceneEventReceived(senderClientId,
                StringConversionUtility.ConvertToFixedString(GetSceneEventTypeName(sceneEventType)),
                StringConversionUtility.ConvertToFixedString(sceneName),
                bytesCount);
            IncrementMetricCount();
        }

        public void TrackPacketSent(uint packetCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.IncrementPacketSent(packetCount);
            IncrementMetricCount();
        }

        public void TrackPacketReceived(uint packetCount)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.IncrementPacketReceived(packetCount);
            IncrementMetricCount();
        }

        public void UpdateRttToServer(int rttMilliseconds)
        {
            if (!CanSendMetrics)
            {
                return;
            }
            var rttSeconds = rttMilliseconds * 1e-3;
            Collector.SetRttToServer(rttSeconds);
        }

        public void UpdateNetworkObjectsCount(int count)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.SetNetworkObjectCount(count);
        }

        public void UpdateConnectionsCount(int count)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.SetConnectionCount(count);
        }

        public void UpdatePacketLoss(float packetLoss)
        {
            if (!CanSendMetrics)
            {
                return;
            }

            Collector.SetPacketLoss(packetLoss);
        }

        public void DispatchFrame()
        {
            s_FrameDispatch.Begin();
            Collector.PreDispatch();
            OnMetricsDispatched?.Invoke(Collector);
            Collector.PostDispatch();
            s_FrameDispatch.End();
            m_NumberOfMetricsThisFrame = 0;
        }

        private void IncrementMetricCount()
        {
            m_NumberOfMetricsThisFrame++;
        }
    }
}
