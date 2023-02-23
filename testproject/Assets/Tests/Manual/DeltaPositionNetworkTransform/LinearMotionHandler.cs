using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Components;

#if UNITY_EDITOR
using UnityEditor;
[CustomEditor(typeof(TestProject.ManualTests.LinearMotionHandler))]
public class LinearMotionHandlerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}
#endif

namespace TestProject.ManualTests
{
    /// <summary>
    /// Used with GenericObjects to randomly move them around
    /// </summary>
    public class LinearMotionHandler : IntegrationNetworkTransform
    {
        public bool SimulateClient = false;
        [Range(0f, 100.0f)]
        public float Speed = 5.0f;
        public Directions StartingDirection;
        [Range(0.1f, 1000.0f)]
        public float DirectionDuration = 0.5f;
        public GameObject ClientPositionVisual;
        [Tooltip("When true, it will randomly pick a new direction after DirectionDuration period of time has elapsed.")]
        public bool RandomDirections;

        public Vector3 PositionOffset;

        public Text ServerPosition;
        public Text ServerDelta;
        public Text ServerCurrent;
        public Text ServerFull;
        public Text ClientPosition;
        public Text ClientDelta;
        public Text TimeMoving;
        public Text DirectionText;

        private bool m_StopMoving;
        private float m_TotalTimeMoving;

        public enum Directions
        {
            Forward,
            ForwardRight,
            Right,
            BackwardRight,
            Backward,
            BackwardLeft,
            Left,
            ForwardLeft,
        }

        private Directions m_CurrentDirection;
        private float m_DirectionTimeOffset;

        private Vector3 m_Direction;


        private HalfVector3DeltaPosition m_HalfVector3SimulatedClient = new HalfVector3DeltaPosition();

        private HalfVector3DeltaPosition m_HalfVector3Server = new HalfVector3DeltaPosition();

        protected override void Awake()
        {
            base.Awake();
            Camera.main.transform.parent = transform;
            transform.position += PositionOffset;
            m_ClientPosition = transform.position;
            m_HalfVector3SimulatedClient = new HalfVector3DeltaPosition(m_ClientPosition, 0, new HalfVector3AxisToSynchronize(true));
            m_HalfVector3Server = new HalfVector3DeltaPosition(m_ClientPosition, 0, new HalfVector3AxisToSynchronize(true));
            m_ServerPosition = transform.position;
            m_LastInterpolateState = Interpolate;
            ServerPosition.enabled = false;
            ClientPosition.enabled = false;
            ClientDelta.enabled = false;
            TimeMoving.enabled = false;
            DirectionText.enabled = false;
            ServerDelta.enabled = false;
            ServerCurrent.enabled = false;
            ServerFull.enabled = false;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                ServerPosition.enabled = true;
                ClientPosition.enabled = true;
                ClientDelta.enabled = true;
                TimeMoving.enabled = true;
                DirectionText.enabled = true;
                m_CurrentDirection = StartingDirection;
                if (SimulateClient)
                {
                    m_StopMoving = true;
                    ServerDelta.enabled = true;
                    ServerCurrent.enabled = true;
                    ServerFull.enabled = true;
                    NetworkManager.NetworkTickSystem.Tick += NetworkTickSystem_Tick;
                }
                SetNextDirection(true);
                SetPositionText();
                UpdateTimeMoving();
            }
            else
            {
                ClientPositionVisual.SetActive(false);
                ClientPosition.enabled = true;
                ClientDelta.enabled = true;
            }
        }

        private bool m_LastInterpolateState;

        private void NetworkTickSystem_Tick()
        {
            if (SimulateClient)
            {
                var position = InLocalSpace ? transform.localPosition : transform.position;
                var isPositionDirty = m_LastInterpolateState != Interpolate;
                var deltaPosition = m_HalfVector3Server.GetDeltaPosition();
                ServerDelta.text = $"S-Delta:{GetVector3AsString(ref deltaPosition)}";
                var currentBasePosition = m_HalfVector3Server.GetCurrentBasePosition();
                ServerCurrent.text = $"S-Curr:{GetVector3AsString(ref currentBasePosition)}";
                var fullPosition = m_HalfVector3Server.GetFullPosition();
                ServerFull.text = $"S-Full:{GetVector3AsString(ref fullPosition)}";


                if (isPositionDirty)
                {
                    m_HalfVector3SimulatedClient = new HalfVector3DeltaPosition(position, NetworkManager.ServerTime.Tick, new HalfVector3AxisToSynchronize(true));
                    m_HalfVector3Server = new HalfVector3DeltaPosition(position, NetworkManager.ServerTime.Tick, new HalfVector3AxisToSynchronize(true));
                    m_ClientPosition = position;
                    OnNonAuthorityUpdatePositionServerRpc(m_ClientPosition);
                    m_LastInterpolateState = Interpolate;
                    return;
                }


                var delta = position - m_HalfVector3Server.GetFullPosition();
                for (int i = 0; i < 3; i++)
                {
                    if (Mathf.Abs(delta[i]) >= PositionThreshold)
                    {
                        isPositionDirty = true;
                        break;
                    }
                }
                if (isPositionDirty)
                {
                    m_HalfVector3Server.FromVector3(ref position, NetworkManager.ServerTime.Tick);
                    m_HalfVector3SimulatedClient.Axis = m_HalfVector3Server.Axis;
                    m_ClientPosition = m_HalfVector3SimulatedClient.ToVector3(NetworkManager.ServerTime.Tick);
                    OnNonAuthorityUpdatePositionServerRpc(m_ClientPosition);
                }
            }
        }

        private NetworkTransformStateUpdate m_NetworkTransformStateUpdate = new NetworkTransformStateUpdate();

        protected override void OnNetworkTransformStateUpdate(ref NetworkTransformStateUpdate networkTransformStateUpdate)
        {
            base.OnNetworkTransformStateUpdate(ref networkTransformStateUpdate);
            if (!CanCommitToTransform)
            {
                m_NetworkTransformStateUpdate = networkTransformStateUpdate;
            }
        }

        private string GetVector3AsString(ref Vector3 vector)
        {
            return $"({vector.x:0.000}, {vector.y:0.000}, {vector.z:0.000})";
        }

        private void UpdateClientPositionInfo()
        {
            var targetPosition = m_NetworkTransformStateUpdate.Position;
            var currentPosition = InLocalSpace ? transform.localPosition : transform.position;
            var delta = targetPosition - currentPosition;
            if (Interpolate)
            {
                ClientDelta.text = $"C-Delta: {GetVector3AsString(ref delta)}";
            }
            else
            {
                ClientDelta.text = "--Interpolate Off--";
            }
            ClientPosition.text = $"Client: {GetVector3AsString(ref currentPosition)}";
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
        }


        private void SetNextDirection(bool useCurrent = false)
        {
            if (!useCurrent)
            {
                var directions = System.Enum.GetValues(typeof(Directions));
                // If not using random directions, then move to the next
                // or roll over.
                if (!RandomDirections)
                {
                    int currentDirection = (int)m_CurrentDirection;
                    currentDirection++;

                    currentDirection = currentDirection % (directions.Length - 1);
                    m_CurrentDirection = (Directions)currentDirection;
                }
                else
                {
                    m_CurrentDirection = (Directions)Random.Range(0, directions.Length - 1);
                }
            }

            DirectionText.text = $"Direction: {m_CurrentDirection}";

            switch (m_CurrentDirection)
            {
                case Directions.Forward:
                    {
                        m_Direction = Vector3.forward;
                        break;
                    }
                case Directions.ForwardRight:
                    {
                        m_Direction = Vector3.forward + Vector3.right;
                        break;
                    }
                case Directions.Right:
                    {
                        m_Direction = Vector3.right;
                        break;
                    }
                case Directions.BackwardRight:
                    {
                        m_Direction = Vector3.back + Vector3.right;
                        break;
                    }
                case Directions.Backward:
                    {
                        m_Direction = Vector3.back;
                        break;
                    }
                case Directions.BackwardLeft:
                    {
                        m_Direction = Vector3.back + Vector3.left;
                        break;
                    }
                case Directions.Left:
                    {
                        m_Direction = Vector3.left;
                        break;
                    }
                case Directions.ForwardLeft:
                    {
                        m_Direction = Vector3.forward + Vector3.left;
                        break;
                    }
            }
        }

        private bool ShouldRun()
        {
            return (NetworkManager.ConnectedClients.Count > (IsHost ? 1 : 0)) || SimulateClient;
        }


        // We just apply our current direction with magnitude to our current position during fixed update
        private void FixedUpdate()
        {
            if (!IsSpawned)
            {
                return;
            }
            if (IsServer && ShouldRun())
            {
                var position = transform.position;
                var rotation = transform.rotation;
                var yAxis = position.y;
                if (!m_StopMoving)
                {
                    position += (m_Direction * Speed);
                }
                else
                {
                    position += m_DecayToStop;
                    m_DecayToStop = Vector3.Lerp(m_DecayToStop, Vector3.zero, Time.fixedDeltaTime * 16.0f);
                    if (m_DecayToStop.magnitude < 0.01f)
                    {
                        m_DecayToStop = Vector3.zero;
                    }
                }
                position.y = yAxis;
                position = Vector3.Lerp(transform.position, position, Time.fixedDeltaTime);
                rotation = Quaternion.LookRotation(m_Direction);
                transform.position = position;
                transform.rotation = rotation;
            }
        }



        private void UpdateTimeMoving()
        {
            m_TotalTimeMoving += Time.deltaTime;
            TimeMoving.text = $"Time Moving: {m_TotalTimeMoving}";
        }

        private Vector3 m_DecayToStop;
        private void LateUpdate()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (!IsServer)
            {
                var position = InLocalSpace ? transform.localPosition : transform.position;
                OnNonAuthorityUpdatePositionServerRpc(position);

                UpdateClientPositionInfo();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                m_StopMoving = !m_StopMoving;
                if (m_StopMoving)
                {
                    m_DecayToStop = (m_Direction * Speed);
                }
            }

            if (!m_StopMoving && ShouldRun())
            {
                UpdateTimeMoving();
            }

            if ((m_TotalTimeMoving - m_DirectionTimeOffset) >= DirectionDuration * 60.0f)
            {
                m_DirectionTimeOffset = m_TotalTimeMoving;
                SetNextDirection();
            }

            ClientPositionVisual.transform.position = m_ClientPosition;
            ClientPositionVisual.transform.rotation = InLocalSpace ? transform.localRotation : transform.rotation;
        }

        private Vector3 m_ServerPosition;
        private Vector3 m_ClientPosition;
        private Vector3 m_ClientDelta;


        private void SetPositionText()
        {
            ServerPosition.text = $"Server: {GetVector3AsString(ref m_ServerPosition)}";
            ClientPosition.text = $"Client: {GetVector3AsString(ref m_ClientPosition)}";
            ClientDelta.text = $"C-Delta: {GetVector3AsString(ref m_ClientDelta)}";
        }

        [ServerRpc(RequireOwnership = false)]
        private void OnNonAuthorityUpdatePositionServerRpc(Vector3 position)
        {
            m_ClientPosition = position;
            UpdatePositionValidation();
        }

        private void UpdatePositionValidation()
        {
            m_ServerPosition = InLocalSpace ? transform.localPosition : transform.position;
            m_ClientDelta = m_ClientPosition - m_ServerPosition;

            SetPositionText();
        }
    }
}
