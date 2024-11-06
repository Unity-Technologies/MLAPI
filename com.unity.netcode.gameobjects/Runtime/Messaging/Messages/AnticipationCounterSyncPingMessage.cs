namespace Unity.Netcode
{
    internal struct AnticipationCounterSyncPingMessage : INetworkMessage
    {
        public int Version => 0;

        public ulong Counter;
        public double Time;

        public void Serialize(FastBufferWriter writer, int targetVersion)
        {
            BytePacker.WriteValuePacked(writer, Counter);
            writer.WriteValueSafe(Time);
        }

        public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            if (!networkManager.IsServer)
            {
                return false;
            }
            ByteUnpacker.ReadValuePacked(reader, out Counter);
            reader.ReadValueSafe(out Time);
            return true;
        }

        public void Handle(ref NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            if (networkManager.IsListening && !networkManager.ShutdownInProgress && networkManager.ConnectedClients.ContainsKey(context.SenderId))
            {
                var message = new AnticipationCounterSyncPongMessage { Counter = Counter, Time = Time };
                networkManager.MessageManager.SendMessage(ref message, NetworkDelivery.Reliable, context.SenderId);
            }
        }
    }
    internal struct AnticipationCounterSyncPongMessage : INetworkMessage
    {
        public int Version => 0;

        public ulong Counter;
        public double Time;

        public void Serialize(FastBufferWriter writer, int targetVersion)
        {
            BytePacker.WriteValuePacked(writer, Counter);
            writer.WriteValueSafe(Time);
        }

        public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            if (!networkManager.IsClient)
            {
                return false;
            }
            ByteUnpacker.ReadValuePacked(reader, out Counter);
            reader.ReadValueSafe(out Time);
            return true;
        }

        public void Handle(ref NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            networkManager.AnticipationSystem.LastAnticipationAck = Counter;
            networkManager.AnticipationSystem.LastAnticipationAckTime = Time;
        }
    }
}
