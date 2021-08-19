using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Unity.Netcode.Interest
{
    public class InterestNodeStatic <TClient, TObject> : InterestNode<TClient, TObject>
    {
        public List<InterestKernel<TClient, TObject>> InterestKernels = new List<InterestKernel<TClient, TObject>>();

        // these are the objects under my purview
        protected HashSet<TObject> ManagedObjects;

        public InterestNodeStatic()
        {
            ManagedObjects = new HashSet<TObject>();
        }

        public override void AddObject(in TObject obj)
        {
            ManagedObjects.Add(obj);
        }

        public override void RemoveObject(in TObject obj)
        {
            ManagedObjects.Remove(obj);
        }

        public override void QueryFor(in TClient client, HashSet<TObject> results)
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

        public override void UpdateObject(in TObject obj)
        {
        }
    }
}
