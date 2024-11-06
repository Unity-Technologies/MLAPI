namespace Unity.Netcode
{
    internal struct ServerLogMessage : INetworkMessage
    {
        public int Version => 0;

        public ulong SenderId;

        public NetworkLog.LogType LogType;
        // It'd be lovely to be able to replace this with FixedString or NativeArray...
        // But it's not really practical. On the sending side, the user is likely to want
        // to work with strings and would need to convert, and on the receiving side,
        // we'd have to convert it to a string to be able to pass it to the log system.
        // So an allocation is unavoidable here on both sides.
        public string Message;

        public void Serialize(FastBufferWriter writer, int targetVersion)
        {
            writer.WriteValueSafe(LogType);
            BytePacker.WriteValuePacked(writer, Message);
            BytePacker.WriteValueBitPacked(writer, SenderId);
        }

        public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
        {
            var networkManager = (NetworkManager)context.SystemOwner;

            if ((networkManager.IsServer || networkManager.LocalClient.IsSessionOwner) && networkManager.NetworkConfig.EnableNetworkLogs)
            {
                reader.ReadValueSafe(out LogType);
                ByteUnpacker.ReadValuePacked(reader, out Message);
                ByteUnpacker.ReadValuePacked(reader, out SenderId);
                // If in distributed authority mode and the DAHost is not the session owner, then the DAHost will just forward the message.
                if (networkManager.DAHost && networkManager.CurrentSessionOwner != networkManager.LocalClientId)
                {
                    var message = this;
                    var size = networkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableFragmentedSequenced, networkManager.CurrentSessionOwner);
                    networkManager.NetworkMetrics.TrackServerLogSent(networkManager.CurrentSessionOwner, (uint)LogType, size);
                    return false;
                }
                return true;
            }
            return false;
        }

        public void Handle(ref NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            var senderId = networkManager.DistributedAuthorityMode ? SenderId : context.SenderId;

            networkManager.NetworkMetrics.TrackServerLogReceived(senderId, (uint)LogType, context.MessageSize);

            switch (LogType)
            {
                case NetworkLog.LogType.Info:
                    NetworkLog.LogInfoServerLocal(Message, senderId);
                    break;
                case NetworkLog.LogType.Warning:
                    NetworkLog.LogWarningServerLocal(Message, senderId);
                    break;
                case NetworkLog.LogType.Error:
                    NetworkLog.LogErrorServerLocal(Message, senderId);
                    break;
            }
        }
    }
}
