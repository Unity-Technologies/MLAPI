#if COM_UNITY_MODULES_PHYSICS
using UnityEngine;

namespace Unity.Netcode.Components
{
    /// <summary>
    /// NetworkRigidbody allows for the use of <see cref="Rigidbody"/> on network objects. By controlling the kinematic
    /// mode of the <see cref="Rigidbody"/> and disabling it on all peers but the authoritative one.
    /// </summary>
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Netcode/Network Rigidbody")]
    public class NetworkRigidbody : NetworkRigidbodyBase
    {

        public Rigidbody Rigidbody => m_InternalRigidbody;

        protected virtual void Awake()
        {
            Initialize(RigidbodyTypes.Rigidbody);
        }
    }
}
#endif // COM_UNITY_MODULES_PHYSICS
