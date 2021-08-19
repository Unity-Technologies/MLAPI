using System;
using System.Collections.Generic;
using Unity.Netcode.Interest;
using UnityEngine;

namespace Unity.Netcode
{
    public class RadiusInterestKernel : InterestKernel<NetworkClient, NetworkObject>
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
