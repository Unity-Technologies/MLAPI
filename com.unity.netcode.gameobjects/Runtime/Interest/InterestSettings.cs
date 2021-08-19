using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Netcode.Interest
{
    [CreateAssetMenu(fileName = "ReplicationSettings", menuName = "Interest/Settings/InterestSettings", order = 1)]

    // these are settings used by the Interest management system to
    // - adjust how it decides whether an item is replicated
    // - adjust how prioritization occurs
    public class InterestSettings : ScriptableObject
    {
        // TBD - add default interest settings here
    }
}
