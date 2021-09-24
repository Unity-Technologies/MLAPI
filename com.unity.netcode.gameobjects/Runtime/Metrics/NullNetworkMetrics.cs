using System.Collections.Generic;

namespace Unity.Netcode
{
    internal class NullNetworkMetrics : INetworkMetrics
    {
        public void TrackTransportBytesSent(long bytesCount)
        {
        }

        public void TrackTransportBytesReceived(long bytesCount)
        {
        }

        public void TrackNetworkMessageSent(ulong receivedClientId, string messageType, long bytesCount)
        {
        }

        public void TrackNetworkMessageReceived(ulong senderClientId, string messageType, long bytesCount)
        {
        }

        public void TrackNetworkObject(NetworkObject networkObject)
        {
        }

        public void TrackNamedMessageSent(ulong receiverClientId, string messageName, long bytesCount)
        {
        }

        public void TrackNamedMessageSent(IReadOnlyCollection<ulong> receiverClientIds, string messageName, long bytesCount)
        {
        }

        public void TrackNamedMessageReceived(ulong senderClientId, string messageName, long bytesCount)
        {
        }

        public void TrackUnnamedMessageSent(ulong receiverClientId, long bytesCount)
        {
        }

        public void TrackUnnamedMessageSent(IReadOnlyCollection<ulong> receiverClientIds, long bytesCount)
        {
        }

        public void TrackUnnamedMessageReceived(ulong senderClientId, long bytesCount)
        {
        }

        public void TrackNetworkVariableDeltaSent(
            ulong receiverClientId,
            ulong networkObjectId,
            string gameObjectName,
            string variableName,
            string networkBehaviourName,
            long bytesCount)
        {
        }

        public void TrackNetworkVariableDeltaReceived(
            ulong senderClientId,
            ulong networkObjectId,
            string gameObjectName,
            string variableName,
            string networkBehaviourName,
            long bytesCount)
        {
        }

        public void TrackOwnershipChangeSent(ulong receiverClientId, ulong networkObjectId, string gameObjectName, long bytesCount)
        {
        }

        public void TrackOwnershipChangeReceived(ulong senderClientId, ulong networkObjectId, string gameObjectName, long bytesCount)
        {
        }

        public void TrackObjectSpawnSent(ulong receiverClientId, ulong networkObjectId, string gameObjectName, long bytesCount)
        {
        }

        public void TrackObjectSpawnReceived(ulong senderClientId, ulong networkObjectId, string gameObjectName, long bytesCount)
        {
        }

        public void TrackObjectDestroySent(ulong senderClientId, ulong networkObjectId, string gameObjectName, long bytesCount)
        {
        }

        public void TrackObjectDestroySent(IReadOnlyCollection<ulong> receiverClientIds, ulong networkObjectId, string gameObjectName, long bytesCount)
        {
        }

        public void TrackObjectDestroyReceived(ulong senderClientId, ulong networkObjectId, string gameObjectName, long bytesCount)
        {
        }

        public void TrackRpcSent(
            ulong receiverClientId,
            ulong networkObjectId,
            string rpcName,
            string networkBehaviourName,
            long bytesCount)
        {
        }

        public void TrackRpcSent(
            ulong[] receiverClientIds,
            ulong networkObjectId,
            string rpcName,
            string networkBehaviourName,
            long bytesCount)
        {
        }

        public void TrackRpcReceived(
            ulong senderClientId,
            ulong networkObjectId,
            string rpcName,
            string networkBehaviourName,
            long bytesCount)
        {
        }

        public void TrackServerLogSent(ulong receiverClientId, uint logType, long bytesCount)
        {
        }

        public void TrackServerLogReceived(ulong senderClientId, uint logType, long bytesCount)
        {
        }

        public void TrackSceneEventSent(IReadOnlyList<ulong> receiverClientIds, uint sceneEventType, string sceneName, long bytesCount)
        {
        }

        public void TrackSceneEventSent(ulong receiverClientId, uint sceneEventType, string sceneName, long bytesCount)
        {
        }

        public void TrackSceneEventReceived(ulong senderClientId, uint sceneEventType, string sceneName, long bytesCount)
        {
        }

        public void DispatchFrame()
        {
        }
    }
}
