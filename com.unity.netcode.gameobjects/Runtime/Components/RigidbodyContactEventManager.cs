#if COM_UNITY_MODULES_PHYSICS
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Unity.Netcode.Components
{
    /// <summary>
    /// Information a <see cref="Rigidbody"/> returns to <see cref="RigidbodyContactEventManager"/> via <see cref="IContactEventHandlerWithInfo.GetContactEventHandlerInfo"/> <br />
    /// if the <see cref="Rigidbody"/> registers itself with <see cref="IContactEventHandlerWithInfo"/> as opposed to <see cref="IContactEventHandler"/>.
    /// </summary>
    public struct ContactEventHandlerInfo
    {
        /// <summary>
        /// When set to true, the <see cref="RigidbodyContactEventManager"/> will include non-Rigidbody based contact events.<br />
        /// When the <see cref="RigidbodyContactEventManager"/> invokes the <see cref="IContactEventHandler.ContactEvent"/> it will return null in place <br />
        /// of the collidingBody parameter if the contact event occurred with a collider that is not registered with the <see cref="RigidbodyContactEventManager"/>.
        /// </summary>
        public bool ProvideNonRigidBodyContactEvents;
        /// <summary>
        /// When set to true, the <see cref="RigidbodyContactEventManager"/> will prioritize invoking <see cref="IContactEventHandler.ContactEvent(ulong, Vector3, Rigidbody, Vector3, bool, Vector3)"/> <br /></br>
        /// if it is the 2nd colliding body in the contact pair being processed. With distributed authority, setting this value to true when a <see cref="NetworkObject"/> is owned by the local client <br />
        /// will assure <see cref="IContactEventHandler.ContactEvent(ulong, Vector3, Rigidbody, Vector3, bool, Vector3)"/> is only invoked on the authoritative side.
        /// </summary>
        public bool HasContactEventPriority;
    }

    /// <summary>
    /// Default implementation required to register a <see cref="Rigidbody"/> with a <see cref="RigidbodyContactEventManager"/> instance. 
    /// </summary>
    /// <remarks>
    /// Recommended to implement this method on a <see cref="NetworkBehaviour"/> component
    /// </remarks>
    public interface IContactEventHandler
    {
        /// <summary>
        /// Should return a <see cref="Rigidbody"/>.
        /// </summary>
        Rigidbody GetRigidbody();

        /// <summary>
        /// Invoked by the <see cref="RigidbodyContactEventManager"/> instance.
        /// </summary>
        /// <param name="eventId">A unique contact event identifier.</param>
        /// <param name="averagedCollisionNormal">The average normal of the collision between two colliders.</param>
        /// <param name="collidingBody">If not null, this will be a registered <see cref="Rigidbody"/> that was part of the collision contact event.</param>
        /// <param name="contactPoint">The world space location of the contact event.</param>
        /// <param name="hasCollisionStay">Will be set if this is a collision stay contact event (i.e. it is not the first contact event and continually has contact)</param>
        /// <param name="averagedCollisionStayNormal">The average normal of the collision stay contact over time.</param>
        void ContactEvent(ulong eventId, Vector3 averagedCollisionNormal, Rigidbody collidingBody, Vector3 contactPoint, bool hasCollisionStay = false, Vector3 averagedCollisionStayNormal = default);
    }

    /// <summary>
    /// This is an extended version of <see cref="IContactEventHandler"/> and can be used to register a <see cref="Rigidbody"/> with a <see cref="RigidbodyContactEventManager"/> instance. <br />
    /// This provides additional <see cref="ContactEventHandlerInfo"/> information to the <see cref="RigidbodyContactEventManager"/> for each set of contact events it is processing.
    /// </summary>
    public interface IContactEventHandlerWithInfo : IContactEventHandler
    {
        /// <summary>
        /// Invoked by <see cref="RigidbodyContactEventManager"/> for each set of contact events it is processing (prior to processing).
        /// </summary>
        /// <returns><see cref="ContactEventHandlerInfo"/></returns>
        ContactEventHandlerInfo GetContactEventHandlerInfo();
    }

    /// <summary>
    /// Add this component to an in-scene placed GameObject to provide faster collision event processing between <see cref="Rigidbody"/> instances and optionally static colliders.
    /// <see cref="IContactEventHandler"/> <br />
    /// <see cref="IContactEventHandlerWithInfo"/> <br />
    /// <see cref="ContactEventHandlerInfo"/> <br />
    /// </summary>
    [AddComponentMenu("Netcode/Rigidbody Contact Event Manager")]
    public class RigidbodyContactEventManager : MonoBehaviour
    {
        public static RigidbodyContactEventManager Instance { get; private set; }

        private struct JobResultStruct
        {
            public bool HasCollisionStay;
            public int ThisInstanceID;
            public int OtherInstanceID;
            public Vector3 AverageNormal;
            public Vector3 AverageCollisionStayNormal;
            public Vector3 ContactPoint;
        }

        private NativeArray<JobResultStruct> m_ResultsArray;
        private int m_Count = 0;
        private JobHandle m_JobHandle;

        private readonly Dictionary<int, Rigidbody> m_RigidbodyMapping = new Dictionary<int, Rigidbody>();
        private readonly Dictionary<int, IContactEventHandler> m_HandlerMapping = new Dictionary<int, IContactEventHandler>();
        private readonly Dictionary<int, ContactEventHandlerInfo> m_HandlerInfo = new Dictionary<int, ContactEventHandlerInfo>();

        private void OnEnable()
        {
            m_ResultsArray = new NativeArray<JobResultStruct>(16, Allocator.Persistent);
            Physics.ContactEvent += Physics_ContactEvent;
            if (Instance != null)
            {
                NetworkLog.LogError($"[Invalid][Multiple Instances] Found more than one instance of {nameof(RigidbodyContactEventManager)}: {name} and {Instance.name}");
                NetworkLog.LogError($"[Disable][Additional Instance] Disabling {name} instance!");
                gameObject.SetActive(false);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Any <see cref="IContactEventHandler"/> implementation can register a <see cref="Rigidbody"/> to be handled by this <see cref="RigidbodyContactEventManager"/> instance.
        /// </summary>
        /// <remarks>
        /// You should enable <see cref="Collider.providesContacts"/> for each <see cref="Collider"/> associated with the <see cref="Rigidbody"/> being registered.<br/>
        /// You can enable this during run time or within the editor's inspector view.
        /// </remarks>
        /// <param name="contactEventHandler"><see cref="IContactEventHandler"/> or <see cref="IContactEventHandlerWithInfo"/></param>
        /// <param name="register">true to register and false to remove from being registered</param>
        public void RegisterHandler(IContactEventHandler contactEventHandler, bool register = true)
        {
            var rigidbody = contactEventHandler.GetRigidbody();
            var instanceId = rigidbody.GetInstanceID();
            if (register)
            {
                if (!m_RigidbodyMapping.ContainsKey(instanceId))
                {
                    m_RigidbodyMapping.Add(instanceId, rigidbody);
                }

                if (!m_HandlerMapping.ContainsKey(instanceId))
                {
                    m_HandlerMapping.Add(instanceId, contactEventHandler);
                }

                if (!m_HandlerInfo.ContainsKey(instanceId))
                {
                    var handlerInfo = new ContactEventHandlerInfo()
                    {
                        HasContactEventPriority = true,
                        ProvideNonRigidBodyContactEvents = false,
                    };
                    var handlerWithInfo = contactEventHandler as IContactEventHandlerWithInfo;

                    if (handlerWithInfo != null)
                    {
                        handlerInfo = handlerWithInfo.GetContactEventHandlerInfo();
                    }
                    m_HandlerInfo.Add(instanceId, handlerInfo);
                }
            }
            else
            {
                m_RigidbodyMapping.Remove(instanceId);
                m_HandlerMapping.Remove(instanceId);
            }
        }

        private void OnDisable()
        {
            m_JobHandle.Complete();
            m_ResultsArray.Dispose();

            Physics.ContactEvent -= Physics_ContactEvent;

            m_RigidbodyMapping.Clear();
            Instance = null;
        }

        private bool m_HasCollisions;
        private int m_CurrentCount = 0;

        private void ProcessCollisions()
        {
            foreach (var contactEventHandler in m_HandlerMapping)
            {
                var handlerWithInfo = contactEventHandler.Value as IContactEventHandlerWithInfo;

                if (handlerWithInfo != null)
                {
                    m_HandlerInfo[contactEventHandler.Key] = handlerWithInfo.GetContactEventHandlerInfo();
                }
                else
                {
                    var info = m_HandlerInfo[contactEventHandler.Key];
                    info.HasContactEventPriority = !m_RigidbodyMapping[contactEventHandler.Key].isKinematic;
                    m_HandlerInfo[contactEventHandler.Key] = info;
                }
            }

            ContactEventHandlerInfo contactEventHandlerInfo0;
            ContactEventHandlerInfo contactEventHandlerInfo1;

            // Process all collisions
            for (int i = 0; i < m_Count; i++)
            {
                var thisInstanceID = m_ResultsArray[i].ThisInstanceID;
                var otherInstanceID = m_ResultsArray[i].OtherInstanceID;
                var contactHandler0 = (IContactEventHandler)null;
                var contactHandler1 = (IContactEventHandler)null;
                var preferredContactHandler = (IContactEventHandler)null;
                var preferredContactHandlerNonRigidbody = false;
                var preferredRigidbody = (Rigidbody)null;
                var otherContactHandler = (IContactEventHandler)null;
                var otherRigidbody = (Rigidbody)null;

                var otherContactHandlerNonRigidbody = false;

                if (m_RigidbodyMapping.ContainsKey(thisInstanceID))
                {
                    contactHandler0 = m_HandlerMapping[thisInstanceID];
                    contactEventHandlerInfo0 = m_HandlerInfo[thisInstanceID];
                    if (contactEventHandlerInfo0.HasContactEventPriority)
                    {
                        preferredContactHandler = contactHandler0;
                        preferredContactHandlerNonRigidbody = contactEventHandlerInfo0.ProvideNonRigidBodyContactEvents;
                        preferredRigidbody = m_RigidbodyMapping[thisInstanceID];
                    }
                    else
                    {
                        otherContactHandler = contactHandler0;
                        otherContactHandlerNonRigidbody = contactEventHandlerInfo0.ProvideNonRigidBodyContactEvents;
                        otherRigidbody = m_RigidbodyMapping[thisInstanceID];
                    }
                }

                if (m_RigidbodyMapping.ContainsKey(otherInstanceID))
                {
                    contactHandler1 = m_HandlerMapping[otherInstanceID];
                    contactEventHandlerInfo1 = m_HandlerInfo[otherInstanceID];
                    if (contactEventHandlerInfo1.HasContactEventPriority && preferredContactHandler == null)
                    {
                        preferredContactHandler = contactHandler1;
                        preferredContactHandlerNonRigidbody = contactEventHandlerInfo1.ProvideNonRigidBodyContactEvents;
                        preferredRigidbody = m_RigidbodyMapping[otherInstanceID];
                    }
                    else
                    {
                        otherContactHandler = contactHandler1;
                        otherContactHandlerNonRigidbody = contactEventHandlerInfo1.ProvideNonRigidBodyContactEvents;
                        otherRigidbody = m_RigidbodyMapping[otherInstanceID];
                    }
                }

                if (preferredContactHandler == null && otherContactHandler != null)
                {
                    preferredContactHandler = otherContactHandler;
                    preferredContactHandlerNonRigidbody = otherContactHandlerNonRigidbody;
                    preferredRigidbody = otherRigidbody;
                    otherContactHandler = null;
                    otherContactHandlerNonRigidbody = false;
                    otherRigidbody = null;
                }

                if (preferredContactHandler == null || (preferredContactHandler != null && otherContactHandler == null && !preferredContactHandlerNonRigidbody))
                {
                    continue;
                }

                if (m_ResultsArray[i].HasCollisionStay)
                {
                    preferredContactHandler.ContactEvent(m_EventId, m_ResultsArray[i].AverageNormal, otherRigidbody, m_ResultsArray[i].ContactPoint, m_ResultsArray[i].HasCollisionStay, m_ResultsArray[i].AverageCollisionStayNormal);
                }
                else
                {
                    preferredContactHandler.ContactEvent(m_EventId, m_ResultsArray[i].AverageNormal, otherRigidbody, m_ResultsArray[i].ContactPoint);
                }
            }
        }

        private void FixedUpdate()
        {
            // Only process new collisions
            if (!m_HasCollisions && m_CurrentCount == 0)
            {
                return;
            }

            // This assures we won't process the same collision
            // set after it has been processed.
            if (m_HasCollisions)
            {
                m_CurrentCount = m_Count;
                m_HasCollisions = false;
                m_JobHandle.Complete();
            }
            ProcessCollisions();
        }

        private void LateUpdate()
        {
            m_CurrentCount = 0;
        }

        private ulong m_EventId;
        private void Physics_ContactEvent(PhysicsScene scene, NativeArray<ContactPairHeader>.ReadOnly pairHeaders)
        {
            m_EventId++;
            m_HasCollisions = true;
            int n = pairHeaders.Length;
            if (m_ResultsArray.Length < n)
            {
                m_ResultsArray.Dispose();
                m_ResultsArray = new NativeArray<JobResultStruct>(Mathf.NextPowerOfTwo(n), Allocator.Persistent);
            }
            m_Count = n;
            var job = new GetCollisionsJob()
            {
                PairedHeaders = pairHeaders,
                ResultsArray = m_ResultsArray
            };
            m_JobHandle = job.Schedule(n, 256);
        }

        private struct GetCollisionsJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<ContactPairHeader>.ReadOnly PairedHeaders;

            public NativeArray<JobResultStruct> ResultsArray;

            public void Execute(int index)
            {
                Vector3 averageNormal = Vector3.zero;
                Vector3 averagePoint = Vector3.zero;
                Vector3 averageCollisionStay = Vector3.zero;
                int count = 0;
                int collisionStaycount = 0;
                int positionCount = 0;
                for (int j = 0; j < PairedHeaders[index].pairCount; j++)
                {
                    ref readonly var pair = ref PairedHeaders[index].GetContactPair(j);

                    if (pair.isCollisionExit)
                    {
                        continue;
                    }

                    for (int k = 0; k < pair.contactCount; k++)
                    {
                        ref readonly var contact = ref pair.GetContactPoint(k);
                        averagePoint += contact.position;
                        positionCount++;
                        if (!pair.isCollisionStay)
                        {
                            averageNormal += contact.normal;
                            count++;
                        }
                        else
                        {
                            averageCollisionStay += contact.normal;
                            collisionStaycount++;
                        }
                    }
                }

                if (count != 0)
                {
                    averageNormal /= count;
                }

                if (collisionStaycount != 0)
                {
                    averageCollisionStay /= collisionStaycount;
                }

                if (positionCount != 0)
                {
                    averagePoint /= positionCount;
                }

                var result = new JobResultStruct()
                {
                    ThisInstanceID = PairedHeaders[index].bodyInstanceID,
                    OtherInstanceID = PairedHeaders[index].otherBodyInstanceID,
                    AverageNormal = averageNormal,
                    HasCollisionStay = collisionStaycount != 0,
                    AverageCollisionStayNormal = averageCollisionStay,
                    ContactPoint = averagePoint
                };

                ResultsArray[index] = result;
            }
        }
    }
}
#endif
