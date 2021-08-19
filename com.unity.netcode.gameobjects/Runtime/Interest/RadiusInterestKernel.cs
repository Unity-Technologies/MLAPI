using System;
using System.Collections.Generic;
using Unity.Netcode.Interest;
using UnityEngine;

namespace Unity.Netcode
{
    [CreateAssetMenu(fileName = "RadiusInterestKernel", menuName = "Interest/Kernels/Radius", order = 1)]
    [Serializable]
    public class RadiusInterestKernel : InterestKernel
    {
        public float Radius = 0.0f;
        public override void QueryFor(in NetworkClient client, in NetworkObject obj, HashSet<NetworkObject> results)
        {
            if (Vector3.Distance(obj.transform.position, client.PlayerObject.transform.position) <= Radius)
            {
                results.Add(obj.GetComponent<NetworkObject>());
            }
        }
    }
}
