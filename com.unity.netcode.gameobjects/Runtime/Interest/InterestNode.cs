using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Unity.Netcode.Interest
{
    public abstract class InterestNode<TClient, TObject>
    {
        public abstract void QueryFor(in TClient client, HashSet<TObject> results);
        public abstract void AddObject(in TObject obj);
        public abstract void RemoveObject(in TObject obj);
        public abstract void UpdateObject(in TObject obj);
    };

    public abstract class InterestKernel<TClient, TObject>
    {
        public abstract void QueryFor(in TClient client, in TObject obj, HashSet<TObject> results);
    }
}
