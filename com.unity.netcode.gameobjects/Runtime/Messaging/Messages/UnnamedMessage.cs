namespace Unity.Netcode
{
    internal struct UnnamedMessage : INetworkMessage
    {
        public int Version => 0;

        public FastBufferWriter SendData;
        private FastBufferReader m_ReceivedData;

        public unsafe void Serialize(FastBufferWriter writer, int targetVersion)
        {
            writer.WriteBytesSafe(SendData.GetUnsafePtr(), SendData.Length);
        }

        public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
        {
            m_ReceivedData = reader;
            return true;
        }

        public void Handle(ref NetworkContext context)
        {
            ((NetworkManager)context.SystemOwner).CustomMessagingManager?.InvokeUnnamedMessage(context.SenderId, m_ReceivedData, context.SerializedHeaderSize);
        }
    }
}
