using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TestProject.ManualTests
{
    public class InSceneParentChildHandler : NetworkBehaviour
    {
        public static InSceneParentChildHandler AuthorityRootParent;
        public static bool EnableVerboseDebug = true;
        public static bool AddNetworkTransform;
        public static bool WorldPositionStays;
        public static InSceneParentChildHandler RootParent { get; private set; }
        private static bool s_GenerateRandomValues;

        public bool ParentHasNoNetworkObject;
        public bool IsRootParent;
        public bool IsLastChild;
        public bool PreserveLocalSpace;

        public Vector3 PositionMax = new Vector3(5.0f, 4.0f, 5.0f);
        public Vector3 PositionMin = new Vector3(-5.00f, 0.5f, -5.0f);
        public Vector3 RotationMax = new Vector3(359.99f, 359.99f, 359.99f);
        public Vector3 RotationMin = new Vector3(0.01f, 0.01f, 0.01f);
        public float ScaleMax = 3.0f;
        public float ScaleMin = 0.75f;


        private InSceneParentChildHandler m_Parent;
        private InSceneParentChildHandler m_Child;
        private Vector3 m_TargetLocalPosition;
        private Vector3 m_TargetLocalRotation;
        private Vector3 m_TargetLocalScale;

        private NetworkTransform m_NetworkTransform;

        public static Dictionary<ulong, InSceneParentChildHandler> AuthorityRelativeInstances = new Dictionary<ulong, InSceneParentChildHandler>();
        public static Dictionary<ulong, Dictionary<ulong, InSceneParentChildHandler>> ClientRelativeInstances = new Dictionary<ulong, Dictionary<ulong, InSceneParentChildHandler>>();

        public static void ResetInstancesTracking(bool enableVerboseDebug)
        {
            EnableVerboseDebug = enableVerboseDebug;
            AuthorityRelativeInstances.Clear();
            ClientRelativeInstances.Clear();
        }

        public InSceneParentChildHandler GetChild()
        {
            return m_Child;
        }

        private Vector3 GenerateVector3(Vector3 min, Vector3 max)
        {
            var result = Vector3.zero;
            result.x = Random.Range(min.x, max.y);
            result.y = Random.Range(min.x, max.y);
            result.z = Random.Range(min.x, max.y);
            return result;
        }

        private void LogMessage(string message)
        {
            if (EnableVerboseDebug)
            {
                Debug.Log(message);
            }
        }

        private void Start()
        {
            if (IsRootParent)
            {
                Random.InitState((int)Random.Range(Time.deltaTime, Time.realtimeSinceStartup));
                RootParent = this;
            }
        }

        private int CountNestedChildren(Transform currentParent, int count = 0)
        {
            if (currentParent.childCount > 0)
            {
                var child = currentParent.GetChild(0);
                count++;
                CountNestedChildren(child, count);
            }
            return count;
        }

        private Transform GetLastChild(Transform currentParent)
        {
            if (currentParent.childCount > 0)
            {
                var child = currentParent.GetChild(0);
                var childHandler = child.GetComponent<InSceneParentChildHandler>();
                var parentHandler = currentParent.GetComponent<InSceneParentChildHandler>();
                parentHandler.m_Child = childHandler;
                childHandler.m_Parent = parentHandler;
                return GetLastChild(child);
            }
            return currentParent;
        }

        private void RemoveParent(Transform child, bool worldPositionStays = true)
        {
            var childNetworkObject = child.GetComponent<NetworkObject>();
            var parentOfChild = child.parent;
            if (parentOfChild != null)
            {
                if (!childNetworkObject.TryRemoveParent(worldPositionStays))
                {
                    throw new Exception($"[RemoveParent] {child.name} Failed to remove itself from parent {parentOfChild.name}!");
                }
                else
                {
                    RemoveParent(parentOfChild, worldPositionStays);
                }
            }
        }

        /// <summary>
        /// DANGO-TODO: Run test where we remove the DAHost as the authority and see what breaks (if anything)
        /// For now, handle checking authority outselves.
        /// </summary>
        /// <returns></returns>
        private bool CheckForAuthority()
        {
            return NetworkObject.HasAuthority;
        }

        public void DeparentAllChildren(bool worldPositionStays = true)
        {
            if (IsRootParent && HasAuthority)
            {
                var lastChild = GetLastChild(transform);
                if (lastChild != null)
                {
                    RemoveParent(lastChild, worldPositionStays);
                }
            }
        }

        private void ParentChild(InSceneParentChildHandler child, bool worldPositionStays = true)
        {
            if (child != null)
            {
                if (child.m_Parent != null)
                {
                    var childNetworkObject = child.GetComponent<NetworkObject>();
                    if (!childNetworkObject.TrySetParent(child.m_Parent.transform, worldPositionStays))
                    {
                        throw new Exception($"[Parent] {child.name} Failed to parent itself under parent {child.m_Parent.name}!");
                    }
                    else
                    {
                        ParentChild(child.m_Child, worldPositionStays);
                    }
                }
            }
        }

        public void ReParentAllChildren(bool worldPositionStays = true)
        {
            if (IsRootParent && HasAuthority)
            {
                ParentChild(m_Child, worldPositionStays);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (HasAuthority)
            {
                LogMessage($"[{NetworkObjectId}] Pos = ({m_TargetLocalPosition}) | Rotation ({m_TargetLocalRotation}) | Scale ({m_TargetLocalScale})");
                if (AddNetworkTransform)
                {
                    m_NetworkTransform = gameObject.AddComponent<NetworkTransform>();
                    m_NetworkTransform.InLocalSpace = PreserveLocalSpace;
                }
                if (IsRootParent)
                {
                    AuthorityRootParent = this;
                }

                if (!AuthorityRelativeInstances.ContainsKey(NetworkObjectId))
                {
                    AuthorityRelativeInstances.Add(NetworkObjectId, this);
                }
            }
            else
            {
                if (!ClientRelativeInstances.ContainsKey(NetworkManager.LocalClientId))
                {
                    ClientRelativeInstances.Add(NetworkManager.LocalClientId, new Dictionary<ulong, InSceneParentChildHandler>());
                }
                if (!ClientRelativeInstances[NetworkManager.LocalClientId].ContainsKey(NetworkObjectId))
                {
                    ClientRelativeInstances[NetworkManager.LocalClientId].Add(NetworkObjectId, this);
                }
            }

            base.OnNetworkSpawn();
        }

        public void DeparentSetValuesAndReparent()
        {
            if (IsRootParent && HasAuthority)
            {
                // Back to back de-parenting and re-parenting
                s_GenerateRandomValues = true;
                DeparentAllChildren(WorldPositionStays);
                s_GenerateRandomValues = false;
                ReParentAllChildren(WorldPositionStays);
            }
        }

        /// <summary>
        /// This handles applying the final desired transform values before the ParentSyncMessage
        /// is created and sent.
        /// </summary>
        public override void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject)
        {
            if (!HasAuthority || !IsSpawned || parentNetworkObject != null || !s_GenerateRandomValues)
            {
                return;
            }

            m_TargetLocalPosition = GenerateVector3(PositionMin, PositionMax);
            m_TargetLocalRotation = GenerateVector3(RotationMin, RotationMax);
            var scale = Random.Range(ScaleMin, ScaleMax);
            m_TargetLocalScale = Vector3.one * scale;
            transform.position = m_TargetLocalPosition;
            transform.rotation = Quaternion.Euler(m_TargetLocalRotation);
            transform.localScale = m_TargetLocalScale;

            base.OnNetworkObjectParentChanged(parentNetworkObject);
        }

        private bool m_RequestSent;

        private void LateUpdate()
        {
            if (!IsSpawned || !HasAuthority || NetworkManagerTestDisabler.IsIntegrationTest)
            {
                return;
            }

            // De-parent
            if (Input.GetKeyDown(KeyCode.D) && IsRootParent)
            {
                DeparentAllChildren(WorldPositionStays);
            }

            // Re-parent
            if (Input.GetKeyDown(KeyCode.R) && IsRootParent)
            {
                ReParentAllChildren(WorldPositionStays);
            }

            // De-parent, initialize with new values, and re-parent
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                RootParent.DeparentSetValuesAndReparent();
            }


            if (Input.GetKeyDown(KeyCode.Space))
            {
                m_RequestSent = true;
                RequestTransformInfoRpc();
            }
        }

        public void CheckChildren()
        {
            if (!NetworkObject.HasAuthority || m_RequestSent || AuthorityRootParent != this)
            {
                return;
            }

            m_RequestSent = true;
            RequestTransformInfoRpc();
        }

        [Rpc(SendTo.NotOwner)]
        private void RequestTransformInfoRpc()
        {
            var nonAuthInstance = ClientRelativeInstances[NetworkManager.LocalClientId];
            var childrenInfo = new ChildrenInfo()
            {
                Children = new List<ChildInfo>()
            };

            foreach (var instance in nonAuthInstance)
            {
                var childInfo = new ChildInfo()
                {
                    InfoType = ChildInfoType.AuthRelative,
                    Id = instance.Key,
                    Position = instance.Value.transform.position,
                    Rotation = instance.Value.transform.eulerAngles,
                    Scale = instance.Value.transform.localScale,
                };
                childrenInfo.Children.Add(childInfo);
            }

            var autoSync = ParentingAutoSyncManager.ClientInstances[NetworkManager.LocalClientId];
            var count = 0;
            foreach (var instance in autoSync.NetworkObjectAutoSyncOffTransforms)
            {
                var childInfo = new ChildInfo()
                {
                    InfoType = ChildInfoType.AutoSyncOff,
                    Id = (ulong)count,
                    Position = instance.position,
                    Rotation = instance.eulerAngles,
                    Scale = instance.localScale,
                };
                count++;
                childrenInfo.Children.Add(childInfo);
            }
            count = 0;
            foreach (var instance in autoSync.NetworkObjectAutoSyncOnTransforms)
            {
                var childInfo = new ChildInfo()
                {
                    InfoType = ChildInfoType.AutoSyncOn,
                    Id = (ulong)count,
                    Position = instance.position,
                    Rotation = instance.eulerAngles,
                    Scale = instance.localScale,
                };
                count++;
                childrenInfo.Children.Add(childInfo);
            }
            count = 0;
            foreach (var instance in autoSync.GameObjectAutoSyncOffTransforms)
            {
                var childInfo = new ChildInfo()
                {
                    InfoType = ChildInfoType.AutoSyncGameObjectOff,
                    Id = (ulong)count,
                    Position = instance.position,
                    Rotation = instance.eulerAngles,
                    Scale = instance.localScale,
                };
                count++;
                childrenInfo.Children.Add(childInfo);
            }
            count = 0;
            foreach (var instance in autoSync.GameObjectAutoSyncOnTransforms)
            {
                var childInfo = new ChildInfo()
                {
                    InfoType = ChildInfoType.AutoSyncGameObjectOn,
                    Id = (ulong)count,
                    Position = instance.position,
                    Rotation = instance.eulerAngles,
                    Scale = instance.localScale,
                };
                count++;
                childrenInfo.Children.Add(childInfo);
            }
            SendTransformInfoRpc(childrenInfo);
        }

        [Rpc(SendTo.Owner)]
        private void SendTransformInfoRpc(ChildrenInfo childrenInfo)
        {
            m_RequestSent = false;
            var errorCount = 0;
            var autoSync = ParentingAutoSyncManager.ServerInstance;
            var position = Vector3.zero;
            var rotation = Vector3.zero;
            var scale = Vector3.zero;
            var instanceName = "";
            foreach (var childInfo in childrenInfo.Children)
            {
                switch (childInfo.InfoType)
                {
                    case ChildInfoType.AuthRelative:
                        {
                            var instance = AuthorityRelativeInstances[childInfo.Id];
                            instanceName = instance.name;
                            position = instance.transform.position;
                            rotation = instance.transform.eulerAngles;
                            scale = instance.transform.localScale;
                            break;
                        }
                    case ChildInfoType.AutoSyncOff:
                        {
                            instanceName = autoSync.NetworkObjectAutoSyncOffTransforms[(int)childInfo.Id].name;
                            position = autoSync.NetworkObjectAutoSyncOffTransforms[(int)childInfo.Id].position;
                            rotation = autoSync.NetworkObjectAutoSyncOffTransforms[(int)childInfo.Id].eulerAngles;
                            scale = autoSync.NetworkObjectAutoSyncOffTransforms[(int)childInfo.Id].localScale;
                            break;
                        }
                    case ChildInfoType.AutoSyncOn:
                        {
                            instanceName = autoSync.NetworkObjectAutoSyncOnTransforms[(int)childInfo.Id].name;
                            position = autoSync.NetworkObjectAutoSyncOnTransforms[(int)childInfo.Id].position;
                            rotation = autoSync.NetworkObjectAutoSyncOnTransforms[(int)childInfo.Id].eulerAngles;
                            scale = autoSync.NetworkObjectAutoSyncOnTransforms[(int)childInfo.Id].localScale;
                            break;
                        }
                    case ChildInfoType.AutoSyncGameObjectOff:
                        {
                            instanceName = autoSync.GameObjectAutoSyncOffTransforms[(int)childInfo.Id].name;
                            position = autoSync.GameObjectAutoSyncOffTransforms[(int)childInfo.Id].position;
                            rotation = autoSync.GameObjectAutoSyncOffTransforms[(int)childInfo.Id].eulerAngles;
                            scale = autoSync.GameObjectAutoSyncOffTransforms[(int)childInfo.Id].localScale;
                            break;
                        }
                    case ChildInfoType.AutoSyncGameObjectOn:
                        {
                            instanceName = autoSync.GameObjectAutoSyncOnTransforms[(int)childInfo.Id].name;
                            position = autoSync.GameObjectAutoSyncOnTransforms[(int)childInfo.Id].position;
                            rotation = autoSync.GameObjectAutoSyncOnTransforms[(int)childInfo.Id].eulerAngles;
                            scale = autoSync.GameObjectAutoSyncOnTransforms[(int)childInfo.Id].localScale;
                            break;
                        }
                }

                if (!Approximately(position, childInfo.Position))
                {
                    Debug.LogWarning($"[{childInfo.InfoType}][{instanceName}][Position Mismatch] Auth: {position} | NonAuth: {childInfo.Position}");
                    errorCount++;
                }
                if (!Approximately(rotation, childInfo.Rotation))
                {
                    Debug.LogWarning($"[{childInfo.InfoType}][{instanceName}][Rotation Mismatch] Auth: {rotation} | NonAuth: {childInfo.Rotation}");
                    errorCount++;
                }
                if (!Approximately(scale, childInfo.Scale))
                {
                    Debug.LogWarning($"[{childInfo.InfoType}][{instanceName}][Scale Mismatch] Auth: {scale} | NonAuth: {childInfo.Scale}");
                    errorCount++;
                }
            }

            Debug.Log($"Finished checking children with ({errorCount}) mismatch errors.");
        }

        protected bool Approximately(Vector3 a, Vector3 b)
        {
            var deltaVariance = 0.0125f;
            return Math.Round(Mathf.Abs(a.x - b.x), 2) <= deltaVariance &&
                Math.Round(Mathf.Abs(a.y - b.y), 2) <= deltaVariance &&
                Math.Round(Mathf.Abs(a.z - b.z), 2) <= deltaVariance;
        }
    }

    public struct ChildrenInfo : INetworkSerializable
    {
        public List<ChildInfo> Children;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            var count = 0;
            if (serializer.IsWriter)
            {
                count = Children.Count;
            }
            serializer.SerializeValue(ref count);
            if (serializer.IsReader)
            {
                Children = new List<ChildInfo>(count);
            }

            for (int i = 0; i < count; i++)
            {
                var childInfo = new ChildInfo();
                if (serializer.IsWriter)
                {
                    childInfo = Children[i];
                }
                serializer.SerializeValue(ref childInfo);
                if (serializer.IsReader)
                {
                    Children.Add(childInfo);
                }
            }
        }
    }

    public enum ChildInfoType
    {
        AuthRelative,
        AutoSyncOff,
        AutoSyncOn,
        AutoSyncGameObjectOff,
        AutoSyncGameObjectOn,
    }

    public struct ChildInfo : INetworkSerializable
    {
        public ChildInfoType InfoType;
        public ulong Id;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InfoType);
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Scale);
        }
    }
}
