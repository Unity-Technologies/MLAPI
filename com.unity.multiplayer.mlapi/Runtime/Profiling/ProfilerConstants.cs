namespace Unity.Multiplayer.Netcode
{
    public static class ProfilerConstants
    {
        public const string Connections = nameof(Connections);
        public const string ReceiveTickRate = nameof(ReceiveTickRate);
        public const string NamedMessageReceived = nameof(NamedMessageReceived);
        public const string UnnamedMessageReceived = nameof(UnnamedMessageReceived);
        public const string NamedMessageSent = nameof(NamedMessageSent);
        public const string UnnamedMessageSent = nameof(UnnamedMessageSent);
        public const string ByteSent = nameof(ByteSent);
        public const string ByteReceived = nameof(ByteReceived);
        public const string NetworkVarDeltas = nameof(NetworkVarDeltas);
        public const string NetworkVarUpdates = nameof(NetworkVarUpdates);
        public const string RpcSent = nameof(RpcSent);
        public const string RpcReceived = nameof(RpcReceived);
    }
}
