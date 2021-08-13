#if MULTIPLAYER_TOOLS
using System;

namespace Unity.Netcode.RuntimeTests.Metrics.Utlity
{
    public class NetworkVariableComponent : NetworkBehaviour
    {
        public NetworkVariableString MyNetworkVariable { get; } = new NetworkVariableString();

        void Update()
        {
            if (IsServer)
            {
                MyNetworkVariable.Value = Guid.NewGuid().ToString();
            }
        }
    }
}
#endif
