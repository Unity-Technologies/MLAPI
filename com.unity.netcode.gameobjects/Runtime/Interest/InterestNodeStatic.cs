using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Unity.Netcode.Interest
{
    [CreateAssetMenu(fileName = "StaticInterestNode", menuName = "Interest/Nodes/Static", order = 1)]
    public class InterestNodeStatic : InterestNode
    {
        public List<InterestKernel> InterestKernels = new List<InterestKernel>();

        // these are the objects under my purview
        protected HashSet<NetworkObject> ManagedObjects;

        public void OnEnable()
        {
            ManagedObjects = new HashSet<NetworkObject>();
        }

        public override void AddObject(in NetworkObject obj)
        {
            ManagedObjects.Add(obj);
        }

        public override void RemoveObject(in NetworkObject obj)
        {
            ManagedObjects.Remove(obj);
        }

        public override void QueryFor(in NetworkClient client, HashSet<NetworkObject> results)
        {
            if (InterestKernels.Count > 0)
            {
                foreach (var obj in ManagedObjects)
                {
                    foreach (var ik in InterestKernels)
                    {
                        ik.QueryFor(client, obj, results);
                    }
                }
            }
            else
            {
                results.UnionWith(ManagedObjects);
            }
        }

        public override void UpdateObject(in NetworkObject obj)
        {
        }
    }
}
