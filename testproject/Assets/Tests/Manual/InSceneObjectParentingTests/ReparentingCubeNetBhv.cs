using Unity.Netcode;
namespace TestProject.RuntimeTests
{
    public class ReparentingCubeNetBhv : NetworkBehaviour
    {
        public NetworkObject ParentNetworkObject { get; private set; }

        public override void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject)
        {
            ParentNetworkObject = parentNetworkObject;
        }
    }
}
