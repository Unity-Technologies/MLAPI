namespace Unity.Netcode
{
    internal struct SessionOwnerMessage : INetworkMessage
    {
        public int Version => 0;

        public ulong SessionOwner;

        public void Serialize(FastBufferWriter writer, int targetVersion)
        {
            BytePacker.WriteValuePacked(writer, SessionOwner);
        }

        public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
        {
            ByteUnpacker.ReadValuePacked(reader, out SessionOwner);
            return true;
        }

        public unsafe void Handle(ref NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            networkManager.SetSessionOwner(SessionOwner);
        }
    }
}
