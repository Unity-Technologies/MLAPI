using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


namespace TestProject.ManualTests
{
    /// <summary>
    /// Helper class that builds 4 transform lists based on parent-child hierarchy
    /// for the <see cref="ParentingInSceneObjectsTests.InSceneNestedAutoSyncObjectTest"/>
    /// </summary>
    public class ParentingAutoSyncManager : NetworkBehaviour
    {
        public static ParentingAutoSyncManager ServerInstance;
        public static Dictionary<ulong, ParentingAutoSyncManager> ClientInstances = new Dictionary<ulong, ParentingAutoSyncManager>();

        public GameObject WithNetworkObjectAutoSyncOn;
        public GameObject WithNetworkObjectAutoSyncOff;
        public GameObject GameObjectAutoSyncOn;
        public GameObject GameObjectAutoSyncOff;

        public List<Transform> NetworkObjectAutoSyncOnTransforms = new List<Transform>();
        public List<Transform> NetworkObjectAutoSyncOffTransforms = new List<Transform>();
        public List<Transform> GameObjectAutoSyncOnTransforms = new List<Transform>();
        public List<Transform> GameObjectAutoSyncOffTransforms = new List<Transform>();

        public static void Reset()
        {
            ServerInstance = null;
            ClientInstances.Clear();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                ServerInstance = this;
            }
            else
            {
                ClientInstances.Add(NetworkManager.LocalClientId, this);
            }
            var currentRoot = WithNetworkObjectAutoSyncOn.transform;
            NetworkObjectAutoSyncOnTransforms.Add(currentRoot);
            NetworkObjectAutoSyncOnTransforms.Add(currentRoot.GetChild(0));
            NetworkObjectAutoSyncOnTransforms.Add(currentRoot.GetChild(0).GetChild(0));

            currentRoot = WithNetworkObjectAutoSyncOff.transform;
            NetworkObjectAutoSyncOffTransforms.Add(currentRoot);
            NetworkObjectAutoSyncOffTransforms.Add(currentRoot.GetChild(0));
            NetworkObjectAutoSyncOffTransforms.Add(currentRoot.GetChild(0).GetChild(0));

            currentRoot = GameObjectAutoSyncOn.transform;
            GameObjectAutoSyncOnTransforms.Add(currentRoot);
            GameObjectAutoSyncOnTransforms.Add(currentRoot.GetChild(0));
            GameObjectAutoSyncOnTransforms.Add(currentRoot.GetChild(0).GetChild(0));

            currentRoot = GameObjectAutoSyncOff.transform;
            GameObjectAutoSyncOffTransforms.Add(currentRoot);
            GameObjectAutoSyncOffTransforms.Add(currentRoot.GetChild(0));
            GameObjectAutoSyncOffTransforms.Add(currentRoot.GetChild(0).GetChild(0));
        }
    }
}
