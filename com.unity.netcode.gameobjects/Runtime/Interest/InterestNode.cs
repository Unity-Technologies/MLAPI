using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Unity.Netcode.Interest
{
    public abstract class InterestNode : ScriptableObject
    {
        public abstract void QueryFor(in NetworkClient client, HashSet<NetworkObject> results);
        public abstract void AddObject(in NetworkObject obj);
        public abstract void RemoveObject(in NetworkObject obj);
        public abstract void UpdateObject(in NetworkObject obj);
    };

    public abstract class InterestKernel : ScriptableObject
    {
        public abstract void QueryFor(in NetworkClient client, in NetworkObject obj, HashSet<NetworkObject> results);
    }
}
