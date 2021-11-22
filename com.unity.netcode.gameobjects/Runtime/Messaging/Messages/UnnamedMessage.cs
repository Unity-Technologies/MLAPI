namespace Unity.Netcode
{
    internal struct UnnamedMessage : INetworkMessage
    {
        public FastBufferWriter SendData;
        private FastBufferReader m_ReceivedData;

        public unsafe void Serialize(FastBufferWriter writer)
        {
            writer.WriteBytesSafe(SendData.GetUnsafePtr(), SendData.Length);
        }

        public bool Deserialize(FastBufferReader reader, in NetworkContext context)
        {
            m_ReceivedData = reader;
            return true;
        }

        public void Handle(in NetworkContext context)
        {
            ((NetworkManager)context.SystemOwner).CustomMessagingManager.InvokeUnnamedMessage(context.SenderId, m_ReceivedData, context.SerializedHeaderSize);
        }
    }
}
