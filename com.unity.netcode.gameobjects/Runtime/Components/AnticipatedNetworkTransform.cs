using Unity.Mathematics;
using UnityEngine;

namespace Unity.Netcode.Components
{

#pragma warning disable IDE0001
    /// <summary>
    /// A subclass of <see cref="NetworkTransform"/> that supports basic client anticipation - the client
    /// can set a value on the belief that the server will update it to reflect the same value in a future update
    /// (i.e., as the result of an RPC call). This value can then be adjusted as new updates from the server come in,
    /// in three basic modes:
    ///
    /// <list type="bullet">
    ///
    /// <item><b>Snap:</b> In this mode (with <see cref="StaleDataHandling"/> set to
    /// <see cref="StaleDataHandling.Ignore"/> and no <see cref="NetworkBehaviour.OnReanticipate"/> callback),
    /// the moment a more up-to-date value is received from the authority, it will simply replace the anticipated value,
    /// resulting in a "snap" to the new value if it is different from the anticipated value.</item>
    ///
    /// <item><b>Smooth:</b> In this mode (with <see cref="StaleDataHandling"/> set to
    /// <see cref="Netcode.StaleDataHandling.Ignore"/> and an <see cref="NetworkBehaviour.OnReanticipate"/> callback that calls
    /// <see cref="Smooth"/> from the anticipated value to the authority value with an appropriate
    /// <see cref="Mathf.Lerp"/>-style smooth function), when a more up-to-date value is received from the authority,
    /// it will interpolate over time from an incorrect anticipated value to the correct authoritative value.</item>
    ///
    /// <item><b>Constant Reanticipation:</b> In this mode (with <see cref="StaleDataHandling"/> set to
    /// <see cref="Netcode.StaleDataHandling.Reanticipate"/> and an <see cref="NetworkBehaviour.OnReanticipate"/> that calculates a
    /// new anticipated value based on the current authoritative value), when a more up-to-date value is received from
    /// the authority, user code calculates a new anticipated value, possibly calling <see cref="Smooth"/> to interpolate
    /// between the previous anticipation and the new anticipation. This is useful for values that change frequently and
    /// need to constantly be re-evaluated, as opposed to values that change only in response to user action and simply
    /// need a one-time anticipation when the user performs that action.</item>
    ///
    /// </list>
    ///
    /// Note that these three modes may be combined. For example, if an <see cref="NetworkBehaviour.OnReanticipate"/> callback
    /// does not call either <see cref="Smooth"/> or one of the Anticipate methods, the result will be a snap to the
    /// authoritative value, enabling for a callback that may conditionally call <see cref="Smooth"/> when the
    /// difference between the anticipated and authoritative values is within some threshold, but fall back to
    /// snap behavior if the difference is too large.
    /// </summary>
#pragma warning restore IDE0001
    [DisallowMultipleComponent]
    [AddComponentMenu("Netcode/Anticipated Network Transform")]
    public class AnticipatedNetworkTransform : NetworkTransform
    {

#if UNITY_EDITOR
        internal override bool HideInterpolateValue => true;
#endif

        public struct TransformState
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        private TransformState m_AuthoritativeTransform = new TransformState();
        private TransformState m_AnticipatedTransform = new TransformState();
        private TransformState m_PreviousAnticipatedTransform = new TransformState();
        private ulong m_LastAnticipaionCounter;
        private ulong m_LastAuthorityUpdateCounter;

        private TransformState m_SmoothFrom;
        private TransformState m_SmoothTo;
        private float m_SmoothDuration;
        private float m_CurrentSmoothTime;

        private bool m_OutstandingAuthorityChange = false;

#if UNITY_EDITOR
        private void Reset()
        {
            // Anticipation + smoothing is a form of interpolation, and adding NetworkTransform's buffered interpolation
            // makes the anticipation get weird, so we default it to false.
            Interpolate = false;
        }
#endif

#pragma warning disable IDE0001
        /// <summary>
        /// Defines what the behavior should be if we receive a value from the server with an earlier associated
        /// time value than the anticipation time value.
        /// <br/><br/>
        /// If this is <see cref="Netcode.StaleDataHandling.Ignore"/>, the stale data will be ignored and the authoritative
        /// value will not replace the anticipated value until the anticipation time is reached. <see cref="OnAuthoritativeValueChanged"/>
        /// and <see cref="OnReanticipate"/> will also not be invoked for this stale data.
        /// <br/><br/>
        /// If this is <see cref="Netcode.StaleDataHandling.Reanticipate"/>, the stale data will replace the anticipated data and
        /// <see cref="OnAuthoritativeValueChanged"/> and <see cref="OnReanticipate"/> will be invoked.
        /// In this case, the authoritativeTime value passed to <see cref="OnReanticipate"/> will be lower than
        /// the anticipationTime value, and that callback can be used to calculate a new anticipated value.
        /// </summary>
#pragma warning restore IDE0001
        public StaleDataHandling StaleDataHandling = StaleDataHandling.Reanticipate;

        /// <summary>
        /// Contains the current state of this transform on the server side.
        /// Note that, on the server side, this gets updated at the end of the frame, and will not immediately reflect
        /// changes to the transform.
        /// </summary>
        public TransformState AuthoritativeState => m_AuthoritativeTransform;

        /// <summary>
        /// Contains the current anticipated state, which will match the values of this object's
        /// actual <see cref="MonoBehaviour.transform"/>. When a server
        /// update arrives, this value will be overwritten by the new
        /// server value (unless stale data handling is set to "Ignore"
        /// and the update is determined to be stale). This value will
        /// be duplicated in <see cref="PreviousAnticipatedState"/>, which
        /// will NOT be overwritten in server updates.
        /// </summary>
        public TransformState AnticipatedState => m_AnticipatedTransform;

        /// <summary>
        /// Indicates whether this transform currently needs
        /// reanticipation. If this is true, the anticipated value
        /// has been overwritten by the authoritative value from the
        /// server; the previous anticipated value is stored in <see cref="PreviousAnticipatedState"/>
        /// </summary>
        public bool ShouldReanticipate
        {
            get;
            private set;
        }

        /// <summary>
        /// Holds the most recent anticipated state, whatever was
        /// most recently set using the Anticipate methods. Unlike
        /// <see cref="AnticipatedState"/>, this does not get overwritten
        /// when a server update arrives.
        /// </summary>
        public TransformState PreviousAnticipatedState => m_PreviousAnticipatedTransform;

        /// <summary>
        /// Anticipate that, at the end of one round trip to the server, this transform will be in the given
        /// <see cref="newPosition"/>
        /// </summary>
        /// <param name="newPosition"></param>
        public void AnticipateMove(Vector3 newPosition)
        {
            if (NetworkManager.ShutdownInProgress || !NetworkManager.IsListening)
            {
                return;
            }
            transform.position = newPosition;
            m_AnticipatedTransform.Position = newPosition;
            if (CanCommitToTransform)
            {
                m_AuthoritativeTransform.Position = newPosition;
            }

            m_PreviousAnticipatedTransform = m_AnticipatedTransform;

            m_LastAnticipaionCounter = NetworkManager.AnticipationSystem.AnticipationCounter;

            m_SmoothDuration = 0;
            m_CurrentSmoothTime = 0;
        }

        /// <summary>
        /// Anticipate that, at the end of one round trip to the server, this transform will have the given
        /// <see cref="newRotation"/>
        /// </summary>
        /// <param name="newRotation"></param>
        public void AnticipateRotate(Quaternion newRotation)
        {
            if (NetworkManager.ShutdownInProgress || !NetworkManager.IsListening)
            {
                return;
            }
            transform.rotation = newRotation;
            m_AnticipatedTransform.Rotation = newRotation;
            if (CanCommitToTransform)
            {
                m_AuthoritativeTransform.Rotation = newRotation;
            }

            m_PreviousAnticipatedTransform = m_AnticipatedTransform;

            m_LastAnticipaionCounter = NetworkManager.AnticipationSystem.AnticipationCounter;

            m_SmoothDuration = 0;
            m_CurrentSmoothTime = 0;
        }

        /// <summary>
        /// Anticipate that, at the end of one round trip to the server, this transform will have the given
        /// <see cref="newScale"/>
        /// </summary>
        /// <param name="newScale"></param>
        public void AnticipateScale(Vector3 newScale)
        {
            if (NetworkManager.ShutdownInProgress || !NetworkManager.IsListening)
            {
                return;
            }
            transform.localScale = newScale;
            m_AnticipatedTransform.Scale = newScale;
            if (CanCommitToTransform)
            {
                m_AuthoritativeTransform.Scale = newScale;
            }

            m_PreviousAnticipatedTransform = m_AnticipatedTransform;

            m_LastAnticipaionCounter = NetworkManager.AnticipationSystem.AnticipationCounter;

            m_SmoothDuration = 0;
            m_CurrentSmoothTime = 0;
        }

        /// <summary>
        /// Anticipate that, at the end of one round trip to the server, the transform will have the given
        /// <see cref="newState"/>
        /// </summary>
        /// <param name="newState"></param>
        public void AnticipateState(TransformState newState)
        {
            if (NetworkManager.ShutdownInProgress || !NetworkManager.IsListening)
            {
                return;
            }
            var transform_ = transform;
            transform_.position = newState.Position;
            transform_.rotation = newState.Rotation;
            transform_.localScale = newState.Scale;
            m_AnticipatedTransform = newState;
            if (CanCommitToTransform)
            {
                m_AuthoritativeTransform = newState;
            }

            m_PreviousAnticipatedTransform = m_AnticipatedTransform;

            m_SmoothDuration = 0;
            m_CurrentSmoothTime = 0;
        }

        private void ProcessSmoothing()
        {
            // If not spawned or this instance has authority, exit early
            if (!IsSpawned)
            {
                return;
            }

            if (m_CurrentSmoothTime < m_SmoothDuration)
            {
                m_CurrentSmoothTime += NetworkManager.RealTimeProvider.DeltaTime;
                var transform_ = transform;
                var pct = math.min(m_CurrentSmoothTime / m_SmoothDuration, 1f);

                m_AnticipatedTransform = new TransformState
                {
                    Position = Vector3.Lerp(m_SmoothFrom.Position, m_SmoothTo.Position, pct),
                    Rotation = Quaternion.Lerp(m_SmoothFrom.Rotation, m_SmoothTo.Rotation, pct),
                    Scale = Vector3.Lerp(m_SmoothFrom.Scale, m_SmoothTo.Scale, pct)
                };
                m_PreviousAnticipatedTransform = m_AnticipatedTransform;
                if (!CanCommitToTransform)
                {
                    transform_.position = m_AnticipatedTransform.Position;
                    transform_.localScale = m_AnticipatedTransform.Scale;
                    transform_.rotation = m_AnticipatedTransform.Rotation;
                }
            }
        }

        // TODO: This does not handle OnFixedUpdate
        // This requires a complete overhaul in this class to switch between using
        // NetworkRigidbody's position and rotation values.
        public override void OnUpdate()
        {
            ProcessSmoothing();
            // Do not call the base class implementation...
            // AnticipatedNetworkTransform applies its authoritative state immediately rather than waiting for update
            // This is because AnticipatedNetworkTransforms may need to reference each other in reanticipating
            // and we will want all reanticipation done before anything else wants to reference the transform in
            // OnUpdate()
            //base.OnUpdate();
        }

        /// <summary>
        /// Since authority does not subscribe to updates (OnUpdate or OnFixedUpdate),
        /// we have to update every frame to assure authority processes soothing.
        /// </summary>
        private void Update()
        {
            if (CanCommitToTransform && IsSpawned)
            {
                ProcessSmoothing();
            }
        }

        internal class AnticipatedObject : IAnticipationEventReceiver, IAnticipatedObject
        {
            public AnticipatedNetworkTransform Transform;


            public void SetupForRender()
            {
                if (Transform.CanCommitToTransform)
                {
                    var transform_ = Transform.transform;
                    Transform.m_AuthoritativeTransform = new TransformState
                    {
                        Position = transform_.position,
                        Rotation = transform_.rotation,
                        Scale = transform_.localScale
                    };
                    if (Transform.m_CurrentSmoothTime >= Transform.m_SmoothDuration)
                    {
                        // If we've had a call to Smooth() we'll continue interpolating.
                        // Otherwise we'll go ahead and make the visual and actual locations
                        // match.
                        Transform.m_AnticipatedTransform = Transform.m_AuthoritativeTransform;
                    }

                    transform_.position = Transform.m_AnticipatedTransform.Position;
                    transform_.rotation = Transform.m_AnticipatedTransform.Rotation;
                    transform_.localScale = Transform.m_AnticipatedTransform.Scale;
                }
            }

            public void SetupForUpdate()
            {
                if (Transform.CanCommitToTransform)
                {
                    var transform_ = Transform.transform;
                    transform_.position = Transform.m_AuthoritativeTransform.Position;
                    transform_.rotation = Transform.m_AuthoritativeTransform.Rotation;
                    transform_.localScale = Transform.m_AuthoritativeTransform.Scale;
                }
            }

            public void Update()
            {
                // No need to do this, it's handled by NetworkTransform.OnUpdate
            }

            public void ResetAnticipation()
            {
                Transform.ShouldReanticipate = false;
            }

            public NetworkObject OwnerObject => Transform.NetworkObject;
        }

        private AnticipatedObject m_AnticipatedObject = null;

        private void ResetAnticipatedState()
        {
            var transform_ = transform;
            m_AuthoritativeTransform = new TransformState
            {
                Position = transform_.position,
                Rotation = transform_.rotation,
                Scale = transform_.localScale
            };
            m_AnticipatedTransform = m_AuthoritativeTransform;
            m_PreviousAnticipatedTransform = m_AnticipatedTransform;

            m_SmoothDuration = 0;
            m_CurrentSmoothTime = 0;
        }

        /// <summary>
        /// (This replaces the first OnSynchronize for NetworkTransforms)
        /// This is needed to initialize when fully synchronized since non-authority instances
        /// don't apply the initial synchronization (new client synchronization) until after
        /// everything has been spawned and synchronized.
        /// </summary>
        protected internal override void InternalOnNetworkSessionSynchronized()
        {
            var wasSynchronizing = SynchronizeState.IsSynchronizing;
            base.InternalOnNetworkSessionSynchronized();
            if (!CanCommitToTransform && wasSynchronizing && !SynchronizeState.IsSynchronizing)
            {
                m_OutstandingAuthorityChange = true;
                ApplyAuthoritativeState();
                ResetAnticipatedState();

                m_AnticipatedObject = new AnticipatedObject { Transform = this };
                NetworkManager.AnticipationSystem.RegisterForAnticipationEvents(m_AnticipatedObject);
                NetworkManager.AnticipationSystem.AllAnticipatedObjects.Add(m_AnticipatedObject);
            }
        }

        /// <summary>
        /// (This replaces the any subsequent OnSynchronize for NetworkTransforms post client synchronization)
        /// This occurs on already connected clients when dynamically spawning a NetworkObject for
        /// non-authoritative instances.
        /// </summary>
        protected internal override void InternalOnNetworkPostSpawn()
        {
            base.InternalOnNetworkPostSpawn();
            if (!CanCommitToTransform && NetworkManager.IsConnectedClient && !SynchronizeState.IsSynchronizing)
            {
                m_OutstandingAuthorityChange = true;
                ApplyAuthoritativeState();
                ResetAnticipatedState();
                m_AnticipatedObject = new AnticipatedObject { Transform = this };
                NetworkManager.AnticipationSystem.RegisterForAnticipationEvents(m_AnticipatedObject);
                NetworkManager.AnticipationSystem.AllAnticipatedObjects.Add(m_AnticipatedObject);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (NetworkManager.DistributedAuthorityMode)
            {
                Debug.LogWarning($"This component is not currently supported in distributed authority.");
            }
            base.OnNetworkSpawn();

            // Non-authoritative instances exit early if the synchronization has yet to
            // be applied at this point
            if (SynchronizeState.IsSynchronizing && !CanCommitToTransform)
            {
                return;
            }
            m_OutstandingAuthorityChange = true;
            ApplyAuthoritativeState();
            ResetAnticipatedState();

            m_AnticipatedObject = new AnticipatedObject { Transform = this };
            NetworkManager.AnticipationSystem.RegisterForAnticipationEvents(m_AnticipatedObject);
            NetworkManager.AnticipationSystem.AllAnticipatedObjects.Add(m_AnticipatedObject);
        }

        public override void OnNetworkDespawn()
        {
            if (m_AnticipatedObject != null)
            {
                NetworkManager.AnticipationSystem.DeregisterForAnticipationEvents(m_AnticipatedObject);
                NetworkManager.AnticipationSystem.AllAnticipatedObjects.Remove(m_AnticipatedObject);
                NetworkManager.AnticipationSystem.ObjectsToReanticipate.Remove(m_AnticipatedObject);
                m_AnticipatedObject = null;
            }
            ResetAnticipatedState();

            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            if (m_AnticipatedObject != null)
            {
                NetworkManager.AnticipationSystem.DeregisterForAnticipationEvents(m_AnticipatedObject);
                NetworkManager.AnticipationSystem.AllAnticipatedObjects.Remove(m_AnticipatedObject);
                NetworkManager.AnticipationSystem.ObjectsToReanticipate.Remove(m_AnticipatedObject);
                m_AnticipatedObject = null;
            }

            base.OnDestroy();
        }

        /// <summary>
        /// Interpolate between the transform represented by <see cref="from"/> to the transform represented by
        /// <see cref="to"/> over <see cref="durationSeconds"/> of real time. The duration uses
        /// <see cref="Time.deltaTime"/>, so it is affected by <see cref="Time.timeScale"/>.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="durationSeconds"></param>
        public void Smooth(TransformState from, TransformState to, float durationSeconds)
        {
            var transform_ = transform;
            if (durationSeconds <= 0)
            {
                m_AnticipatedTransform = to;
                m_PreviousAnticipatedTransform = m_AnticipatedTransform;
                transform_.position = to.Position;
                transform_.rotation = to.Rotation;
                transform_.localScale = to.Scale;
                m_SmoothDuration = 0;
                m_CurrentSmoothTime = 0;
                return;
            }
            m_AnticipatedTransform = from;
            m_PreviousAnticipatedTransform = m_AnticipatedTransform;

            if (!CanCommitToTransform)
            {
                transform_.position = from.Position;
                transform_.rotation = from.Rotation;
                transform_.localScale = from.Scale;
            }

            m_SmoothFrom = from;
            m_SmoothTo = to;
            m_SmoothDuration = durationSeconds;
            m_CurrentSmoothTime = 0;
        }

        protected override void OnBeforeUpdateTransformState()
        {
            // this is called when new data comes from the server
            m_LastAuthorityUpdateCounter = NetworkManager.AnticipationSystem.LastAnticipationAck;
            m_OutstandingAuthorityChange = true;
        }

        protected override void OnNetworkTransformStateUpdated(ref NetworkTransformState oldState, ref NetworkTransformState newState)
        {
            base.OnNetworkTransformStateUpdated(ref oldState, ref newState);
            ApplyAuthoritativeState();
        }

        protected override void OnTransformUpdated()
        {
            if (CanCommitToTransform || m_AnticipatedObject == null)
            {
                return;
            }
            // this is called pretty much every frame and will change the transform
            // If we've overridden the transform with an anticipated state, we need to be able to change it back
            // to the anticipated state (while updating the authority state accordingly) or else
            // mark this transform for reanticipation
            var transform_ = transform;

            var previousAnticipatedTransform = m_AnticipatedTransform;

            // Update authority state to catch any possible interpolation data
            m_AuthoritativeTransform.Position = transform_.position;
            m_AuthoritativeTransform.Rotation = transform_.rotation;
            m_AuthoritativeTransform.Scale = transform_.localScale;

            if (!m_OutstandingAuthorityChange)
            {
                // Keep the anticipated value unchanged, we have no updates from the server at all.
                transform_.position = previousAnticipatedTransform.Position;
                transform_.localScale = previousAnticipatedTransform.Scale;
                transform_.rotation = previousAnticipatedTransform.Rotation;
                return;
            }

            if (StaleDataHandling == StaleDataHandling.Ignore && m_LastAnticipaionCounter > m_LastAuthorityUpdateCounter)
            {
                // Keep the anticipated value unchanged because it is more recent than the authoritative one.
                transform_.position = previousAnticipatedTransform.Position;
                transform_.localScale = previousAnticipatedTransform.Scale;
                transform_.rotation = previousAnticipatedTransform.Rotation;
                return;
            }

            m_SmoothDuration = 0;
            m_CurrentSmoothTime = 0;
            m_OutstandingAuthorityChange = false;
            m_AnticipatedTransform = m_AuthoritativeTransform;

            ShouldReanticipate = true;
            NetworkManager.AnticipationSystem.ObjectsToReanticipate.Add(m_AnticipatedObject);
        }
    }
}
