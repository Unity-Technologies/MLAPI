namespace Unity.Netcode.RuntimeTests
{
    internal class NetworkVisibilityComponent : NetworkBehaviour
    {
        public void Hide()
        {
            GetComponent<NetworkObject>().CheckObjectVisibility += HandleCheckObjectVisibility;
        }

        protected virtual bool HandleCheckObjectVisibility(ulong clientId) => false;

    }
}
