using UnityEngine;

namespace Unity.Netcode
{
    /// <summary>
    /// A component for syncing variables
    /// Initial goal: allow an FPS-style snapshot
    /// with variables updating at specific place in the frame
    /// </summary>
    [AddComponentMenu("Netcode/" + nameof(SyncTransform))]
    // todo: check inheriting from NetworkBehaviour. Currently needed for IsOwner, to synchronize position
    public class SyncTransform : NetworkBehaviour
    {
        private NetworkVariable<Vector3> m_VarPos = new NetworkVariable<Vector3>();
        private NetworkVariableQuaternion m_VarRot = new NetworkVariableQuaternion();
        private const float k_Epsilon = 0.001f;

        private bool m_Interpolate = true;

        // data structures for interpolation
        private Vector3[] m_PosStore = new Vector3[2];
        private Quaternion[] m_RotStore = new Quaternion[2];
        private float[] m_PosTimes = new float[2];
        private float[] m_RotTimes = new float[2];
        private float m_LastSent = 0.0f;

        public SyncTransform()
        {
            m_PosTimes[0] = -1.0f;
            m_PosTimes[1] = -1.0f;
            m_RotTimes[0] = -1.0f;
            m_RotTimes[1] = -1.0f;

            m_VarPos.OnValueChanged = SyncPosChanged;
            m_VarRot.OnValueChanged = SyncRotChanged;
        }

        private void SyncPosChanged(Vector3 before, Vector3 after)
        {
            if (!IsOwner)
            {
                m_PosTimes[0] = m_PosTimes[1];
                m_PosTimes[1] = Time.time;
                m_PosStore[0] = m_PosStore[1];
                m_PosStore[1] = after;

                if (!m_Interpolate)
                {
                    gameObject.transform.position = after;
                }
            }
        }

        private void SyncRotChanged(Quaternion before, Quaternion after)
        {
            // todo: this is problematic. Why couldn't this filtering be done server-side?
            if (!IsOwner)
            {
                m_RotTimes[0] = m_RotTimes[1];
                m_RotTimes[1] = Time.time;
                m_RotStore[0] = m_RotStore[1];
                m_RotStore[1] = after;

                if (!m_Interpolate)
                {
                    gameObject.transform.rotation = after;
                }
            }
        }

        private void Start()
        {
            m_VarPos.Settings.WritePermission = m_VarRot.Settings.WritePermission = NetworkVariableWritePermission.ServerOnly;
        }

        private void FixedUpdate()
        {
            float now = Time.time;
            if (m_LastSent == 0.0f)
            {
                m_LastSent = now;
            }

            // if this.gameObject is local let's send its position
            if (IsOwner)
            {
                m_VarPos.Value = gameObject.transform.position;
                m_VarRot.Value = gameObject.transform.rotation;
            }
            else
            {
                if (!m_Interpolate)
                {
                    return;
                }

                // let's m_Interpolate the last received transform
                if (m_PosTimes[0] >= 0.0 && m_PosTimes[1] >= 0.0)
                {
                    var before = gameObject.transform.position;

                    if (m_PosTimes[1] - m_PosTimes[0] > k_Epsilon)
                    {
                        if ((now - m_PosTimes[0]) / (m_PosTimes[1] - m_PosTimes[0]) < 2.0)
                        {
                            gameObject.transform.position = Vector3.LerpUnclamped(
                                m_PosStore[0],
                                m_PosStore[1],
                                (now - m_PosTimes[0]) / (m_PosTimes[1] - m_PosTimes[0]));
                        }
                    }
                    else
                    {
                        gameObject.transform.position = m_PosStore[1];
                    }
                }

                if (m_RotTimes[0] >= 0.0 && m_RotTimes[1] >= 0.0)
                {
                    var before = gameObject.transform.rotation;

                    if (m_RotTimes[1] - m_RotTimes[0] > k_Epsilon)
                    {
                        if ((now - m_RotTimes[0]) / (m_RotTimes[1] - m_RotTimes[0]) < 2.0)
                        {
                            gameObject.transform.rotation = Quaternion.SlerpUnclamped(
                                m_RotStore[0],
                                m_RotStore[1],
                                (now - m_RotTimes[0]) / (m_RotTimes[1] - m_RotTimes[0]));
                        }
                    }
                    else
                    {
                        gameObject.transform.rotation = m_RotStore[1];
                    }
                }
            }
        }
    }
}
