using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    internal class OwnershipPermissionsTests : IntegrationTestWithApproximation
    {
        private GameObject m_PermissionsObject;

        private StringBuilder m_ErrorLog = new StringBuilder();

        protected override int NumberOfClients => 4;

        public OwnershipPermissionsTests() : base(HostOrServer.DAHost)
        {
        }

        protected override IEnumerator OnSetup()
        {
            m_ObjectToValidate = null;
            OwnershipPermissionsTestHelper.CurrentOwnedInstance = null;
            return base.OnSetup();
        }

        protected override void OnServerAndClientsCreated()
        {
            m_PermissionsObject = CreateNetworkObjectPrefab("PermObject");
            m_PermissionsObject.AddComponent<OwnershipPermissionsTestHelper>();

            base.OnServerAndClientsCreated();
        }

        private NetworkObject m_ObjectToValidate;

        private bool ValidateObjectSpawnedOnAllClients()
        {
            m_ErrorLog.Clear();

            var networkObjectId = m_ObjectToValidate.NetworkObjectId;
            var name = m_ObjectToValidate.name;
            if (!UseCMBService() && !m_ServerNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(networkObjectId))
            {
                m_ErrorLog.Append($"Client-{m_ServerNetworkManager.LocalClientId} has not spawned {name}!");
                return false;
            }

            foreach (var client in m_ClientNetworkManagers)
            {
                if (!client.SpawnManager.SpawnedObjects.ContainsKey(networkObjectId))
                {
                    m_ErrorLog.Append($"Client-{client.LocalClientId} has not spawned {name}!");
                    return false;
                }
            }
            return true;
        }

        private bool ValidatePermissionsOnAllClients()
        {
            var currentPermissions = (ushort)m_ObjectToValidate.Ownership;
            var otherPermissions = (ushort)0;
            var networkObjectId = m_ObjectToValidate.NetworkObjectId;
            var objectName = m_ObjectToValidate.name;
            m_ErrorLog.Clear();
            if (!UseCMBService())
            {
                otherPermissions = (ushort)m_ServerNetworkManager.SpawnManager.SpawnedObjects[networkObjectId].Ownership;
                if (currentPermissions != otherPermissions)
                {
                    m_ErrorLog.Append($"Client-{m_ServerNetworkManager.LocalClientId} permissions for {objectName} is {otherPermissions} when it should be {currentPermissions}!");
                    return false;
                }
            }

            foreach (var client in m_ClientNetworkManagers)
            {
                otherPermissions = (ushort)client.SpawnManager.SpawnedObjects[networkObjectId].Ownership;
                if (currentPermissions != otherPermissions)
                {
                    m_ErrorLog.Append($"Client-{client.LocalClientId} permissions for {objectName} is {otherPermissions} when it should be {currentPermissions}!");
                    return false;
                }
            }
            return true;
        }

        private bool ValidateAllInstancesAreOwnedByClient(ulong clientId)
        {
            var networkObjectId = m_ObjectToValidate.NetworkObjectId;
            var otherNetworkObject = (NetworkObject)null;
            m_ErrorLog.Clear();
            if (!UseCMBService())
            {
                otherNetworkObject = m_ServerNetworkManager.SpawnManager.SpawnedObjects[networkObjectId];
                if (otherNetworkObject.OwnerClientId != clientId)
                {
                    m_ErrorLog.Append($"[Client-{m_ServerNetworkManager.LocalClientId}][{otherNetworkObject.name}] Expected owner to be {clientId} but it was {otherNetworkObject.OwnerClientId}!");
                    return false;
                }
            }

            foreach (var client in m_ClientNetworkManagers)
            {
                otherNetworkObject = client.SpawnManager.SpawnedObjects[networkObjectId];
                if (otherNetworkObject.OwnerClientId != clientId)
                {
                    m_ErrorLog.Append($"[Client-{client.LocalClientId}][{otherNetworkObject.name}] Expected owner to be {clientId} but it was {otherNetworkObject.OwnerClientId}!");
                    return false;
                }
            }
            return true;
        }

        [UnityTest]
        public IEnumerator ValidateOwnershipPermissionsTest()
        {
            var firstInstance = SpawnObject(m_PermissionsObject, m_ClientNetworkManagers[0]).GetComponent<NetworkObject>();
            OwnershipPermissionsTestHelper.CurrentOwnedInstance = firstInstance;
            var firstInstanceHelper = firstInstance.GetComponent<OwnershipPermissionsTestHelper>();
            var networkObjectId = firstInstance.NetworkObjectId;
            m_ObjectToValidate = OwnershipPermissionsTestHelper.CurrentOwnedInstance;
            yield return WaitForConditionOrTimeOut(ValidateObjectSpawnedOnAllClients);
            AssertOnTimeout($"[Failed To Spawn] {firstInstance.name}: \n {m_ErrorLog}");

            // Validate the base non-assigned persmissions value for all instances are the same.
            yield return WaitForConditionOrTimeOut(ValidatePermissionsOnAllClients);
            AssertOnTimeout($"[Permissions Mismatch] {firstInstance.name}: \n {m_ErrorLog}");

            //////////////////////////////////////
            // Setting & Removing Ownership Flags:
            //////////////////////////////////////

            // Now, cycle through all permissions and validate that when the owner changes them the change
            // is synchronized on all non-owner clients.
            foreach (var permissionObject in Enum.GetValues(typeof(NetworkObject.OwnershipStatus)))
            {
                var permission = (NetworkObject.OwnershipStatus)permissionObject;
                // Add the status
                firstInstance.SetOwnershipStatus(permission);
                // Validate the persmissions value for all instances are the same.
                yield return WaitForConditionOrTimeOut(ValidatePermissionsOnAllClients);
                AssertOnTimeout($"[Add][Permissions Mismatch] {firstInstance.name}: \n {m_ErrorLog}");

                // Remove the status unless it is None (ignore None).
                if (permission == NetworkObject.OwnershipStatus.None)
                {
                    continue;
                }
                firstInstance.RemoveOwnershipStatus(permission);
                // Validate the persmissions value for all instances are the same.
                yield return WaitForConditionOrTimeOut(ValidatePermissionsOnAllClients);
                AssertOnTimeout($"[Remove][Permissions Mismatch] {firstInstance.name}: \n {m_ErrorLog}");
            }

            //Add multiple flags at the same time
            var multipleFlags = NetworkObject.OwnershipStatus.Transferable | NetworkObject.OwnershipStatus.Distributable | NetworkObject.OwnershipStatus.RequestRequired;
            firstInstance.SetOwnershipStatus(multipleFlags, true);
            Assert.IsTrue(firstInstance.HasOwnershipStatus(multipleFlags), $"[Set][Multi-flag Failure] Expected: {(ushort)multipleFlags} but was {(ushort)firstInstance.Ownership}!");

            // Validate the persmissions value for all instances are the same.
            yield return WaitForConditionOrTimeOut(ValidatePermissionsOnAllClients);
            AssertOnTimeout($"[Set Multiple][Permissions Mismatch] {firstInstance.name}: \n {m_ErrorLog}");

            // Remove multiple flags at the same time
            multipleFlags = NetworkObject.OwnershipStatus.Transferable | NetworkObject.OwnershipStatus.RequestRequired;
            firstInstance.RemoveOwnershipStatus(multipleFlags);
            // Validate the two flags no longer are set
            Assert.IsFalse(firstInstance.HasOwnershipStatus(multipleFlags), $"[Remove][Multi-flag Failure] Expected: {(ushort)NetworkObject.OwnershipStatus.Distributable} but was {(ushort)firstInstance.Ownership}!");
            // Validate that the Distributable flag is still set
            Assert.IsTrue(firstInstance.HasOwnershipStatus(NetworkObject.OwnershipStatus.Distributable), $"[Remove][Multi-flag Failure] Expected: {(ushort)NetworkObject.OwnershipStatus.Distributable} but was {(ushort)firstInstance.Ownership}!");

            // Validate the persmissions value for all instances are the same.
            yield return WaitForConditionOrTimeOut(ValidatePermissionsOnAllClients);
            AssertOnTimeout($"[Set Multiple][Permissions Mismatch] {firstInstance.name}: \n {m_ErrorLog}");

            //////////////////////
            // Changing Ownership:
            //////////////////////

            // Clear the flags, set the permissions to transferrable, and lock ownership in one pass.
            firstInstance.SetOwnershipStatus(NetworkObject.OwnershipStatus.Transferable, true, NetworkObject.OwnershipLockActions.SetAndLock);

            // Validate the persmissions value for all instances are the same.
            yield return WaitForConditionOrTimeOut(ValidatePermissionsOnAllClients);
            AssertOnTimeout($"[Reset][Permissions Mismatch] {firstInstance.name}: \n {m_ErrorLog}");

            var secondInstance = m_ClientNetworkManagers[1].SpawnManager.SpawnedObjects[networkObjectId];
            var secondInstanceHelper = secondInstance.GetComponent<OwnershipPermissionsTestHelper>();

            secondInstance.ChangeOwnership(m_ClientNetworkManagers[1].LocalClientId);
            Assert.IsTrue(secondInstanceHelper.OwnershipPermissionsFailureStatus == NetworkObject.OwnershipPermissionsFailureStatus.Locked,
                $"Expected {secondInstance.name} to return {NetworkObject.OwnershipPermissionsFailureStatus.Locked} but its permission failure" +
                $" status is {secondInstanceHelper.OwnershipPermissionsFailureStatus}!");

            firstInstance.SetOwnershipLock(false);
            // Validate the persmissions value for all instances are the same.
            yield return WaitForConditionOrTimeOut(ValidatePermissionsOnAllClients);
            AssertOnTimeout($"[Unlock][Permissions Mismatch] {firstInstance.name}: \n {m_ErrorLog}");

            // Sanity check to assure this client's instance isn't already the owner.
            Assert.True(!secondInstance.IsOwner, $"[Ownership Check] Client-{m_ClientNetworkManagers[1].LocalClientId} already is the owner!");
            // Now try to acquire ownership
            secondInstance.ChangeOwnership(m_ClientNetworkManagers[1].LocalClientId);

            // Validate the persmissions value for all instances are the same
            yield return WaitForConditionOrTimeOut(() => secondInstance.IsOwner);
            AssertOnTimeout($"[Acquire Ownership Failed] Client-{m_ClientNetworkManagers[1].LocalClientId} failed to get ownership!");

            m_ObjectToValidate = OwnershipPermissionsTestHelper.CurrentOwnedInstance;
            // Validate all other client instances are showing the same owner
            yield return WaitForConditionOrTimeOut(() => ValidateAllInstancesAreOwnedByClient(m_ClientNetworkManagers[1].LocalClientId));
            AssertOnTimeout($"[Ownership Mismatch] {secondInstance.name}: \n {m_ErrorLog}");

            // Clear the flags, set the permissions to RequestRequired, and lock ownership in one pass.
            secondInstance.SetOwnershipStatus(NetworkObject.OwnershipStatus.RequestRequired, true);

            // Validate the persmissions value for all instances are the same.
            yield return WaitForConditionOrTimeOut(ValidatePermissionsOnAllClients);
            AssertOnTimeout($"[Unlock][Permissions Mismatch] {secondInstance.name}: \n {m_ErrorLog}");

            // Attempt to acquire ownership by just changing it
            firstInstance.ChangeOwnership(firstInstance.NetworkManager.LocalClientId);

            // Assure we are denied ownership due to it requiring ownership be requested
            Assert.IsTrue(firstInstanceHelper.OwnershipPermissionsFailureStatus == NetworkObject.OwnershipPermissionsFailureStatus.RequestRequired,
                $"Expected {secondInstance.name} to return {NetworkObject.OwnershipPermissionsFailureStatus.RequestRequired} but its permission failure" +
                $" status is {secondInstanceHelper.OwnershipPermissionsFailureStatus}!");

            //////////////////////////////////
            // Test for single race condition:
            //////////////////////////////////

            // Start with a request for the client we expect to be given ownership
            var requestStatus = firstInstance.RequestOwnership();
            Assert.True(requestStatus == NetworkObject.OwnershipRequestStatus.RequestSent, $"Client-{firstInstance.NetworkManager.LocalClientId} was unabled to send a request for ownership because: {requestStatus}!");

            // Get the 3rd client to send a request at the "relatively" same time
            var thirdInstance = m_ClientNetworkManagers[2].SpawnManager.SpawnedObjects[networkObjectId];
            var thirdInstanceHelper = thirdInstance.GetComponent<OwnershipPermissionsTestHelper>();

            // At the same time send a request by the third client.
            requestStatus = thirdInstance.RequestOwnership();

            // We expect the 3rd client's request should be able to be sent at this time as well (i.e. creates the race condition between two clients)
            Assert.True(requestStatus == NetworkObject.OwnershipRequestStatus.RequestSent, $"Client-{m_ServerNetworkManager.LocalClientId} was unabled to send a request for ownership because: {requestStatus}!");

            // We expect the first requesting client to be given ownership
            yield return WaitForConditionOrTimeOut(() => firstInstance.IsOwner);
            AssertOnTimeout($"[Acquire Ownership Failed] Client-{firstInstance.NetworkManager.LocalClientId} failed to get ownership! ({firstInstanceHelper.OwnershipRequestResponseStatus})(Owner: {OwnershipPermissionsTestHelper.CurrentOwnedInstance.OwnerClientId}");
            m_ObjectToValidate = OwnershipPermissionsTestHelper.CurrentOwnedInstance;

            // Just do a sanity check to assure ownership has changed on all clients.
            yield return WaitForConditionOrTimeOut(() => ValidateAllInstancesAreOwnedByClient(firstInstance.NetworkManager.LocalClientId));
            AssertOnTimeout($"[Ownership Mismatch] {firstInstance.name}: \n {m_ErrorLog}");

            // Now, the third client should get a RequestInProgress returned as their request response
            yield return WaitForConditionOrTimeOut(() => thirdInstanceHelper.OwnershipRequestResponseStatus == NetworkObject.OwnershipRequestResponseStatus.RequestInProgress);
            AssertOnTimeout($"[Request In Progress Failed] Client-{thirdInstanceHelper.NetworkManager.LocalClientId} did not get the right request denied reponse!");

            // Validate the persmissions value for all instances are the same.
            yield return WaitForConditionOrTimeOut(ValidatePermissionsOnAllClients);
            AssertOnTimeout($"[Unlock][Permissions Mismatch] {firstInstance.name}: \n {m_ErrorLog}");

            ///////////////////////////////////////////////
            // Test for multiple ownership race conditions:
            ///////////////////////////////////////////////

            // Get the 4th client's instance
            var fourthInstance = m_ClientNetworkManagers[3].SpawnManager.SpawnedObjects[networkObjectId];
            var fourthInstanceHelper = fourthInstance.GetComponent<OwnershipPermissionsTestHelper>();

            // Send out a request from three clients at the same time
            // The first one sent (and received for this test) gets ownership
            requestStatus = secondInstance.RequestOwnership();
            Assert.True(requestStatus == NetworkObject.OwnershipRequestStatus.RequestSent, $"Client-{secondInstance.NetworkManager.LocalClientId} was unabled to send a request for ownership because: {requestStatus}!");
            requestStatus = thirdInstance.RequestOwnership();
            Assert.True(requestStatus == NetworkObject.OwnershipRequestStatus.RequestSent, $"Client-{thirdInstance.NetworkManager.LocalClientId} was unabled to send a request for ownership because: {requestStatus}!");
            requestStatus = fourthInstance.RequestOwnership();
            Assert.True(requestStatus == NetworkObject.OwnershipRequestStatus.RequestSent, $"Client-{fourthInstance.NetworkManager.LocalClientId} was unabled to send a request for ownership because: {requestStatus}!");

            // The 2nd and 3rd client should be denied and the 4th client should be approved
            yield return WaitForConditionOrTimeOut(() =>
            (fourthInstanceHelper.OwnershipRequestResponseStatus == NetworkObject.OwnershipRequestResponseStatus.RequestInProgress) &&
            (thirdInstanceHelper.OwnershipRequestResponseStatus == NetworkObject.OwnershipRequestResponseStatus.RequestInProgress) &&
            (secondInstanceHelper.OwnershipRequestResponseStatus == NetworkObject.OwnershipRequestResponseStatus.Approved)
            );
            AssertOnTimeout($"[Targeted Owner] Client-{secondInstanceHelper.NetworkManager.LocalClientId} did not get the right request denied reponse: {secondInstanceHelper.OwnershipRequestResponseStatus}!");
            m_ObjectToValidate = OwnershipPermissionsTestHelper.CurrentOwnedInstance;
            // Just do a sanity check to assure ownership has changed on all clients.
            yield return WaitForConditionOrTimeOut(() => ValidateAllInstancesAreOwnedByClient(secondInstance.NetworkManager.LocalClientId));
            AssertOnTimeout($"[Ownership Mismatch] {secondInstance.name}: \n {m_ErrorLog}");

            // Validate the persmissions value for all instances are the same.
            yield return WaitForConditionOrTimeOut(ValidatePermissionsOnAllClients);
            AssertOnTimeout($"[Unlock][Permissions Mismatch] {secondInstance.name}: \n {m_ErrorLog}");

            ///////////////////////////////////////////////
            // Test for targeted ownership request:
            ///////////////////////////////////////////////

            // Now get the DAHost's client's instance
            var daHostInstance = m_ServerNetworkManager.SpawnManager.SpawnedObjects[networkObjectId];
            var daHostInstanceHelper = daHostInstance.GetComponent<OwnershipPermissionsTestHelper>();

            secondInstanceHelper.AllowOwnershipRequest = true;
            secondInstanceHelper.OnlyAllowTargetClientId = true;
            secondInstanceHelper.ClientToAllowOwnership = daHostInstance.NetworkManager.LocalClientId;

            // Send out a request from all three clients
            requestStatus = firstInstance.RequestOwnership();
            Assert.True(requestStatus == NetworkObject.OwnershipRequestStatus.RequestSent, $"Client-{firstInstance.NetworkManager.LocalClientId} was unabled to send a request for ownership because: {requestStatus}!");
            requestStatus = thirdInstance.RequestOwnership();
            Assert.True(requestStatus == NetworkObject.OwnershipRequestStatus.RequestSent, $"Client-{thirdInstance.NetworkManager.LocalClientId} was unabled to send a request for ownership because: {requestStatus}!");
            requestStatus = fourthInstance.RequestOwnership();
            Assert.True(requestStatus == NetworkObject.OwnershipRequestStatus.RequestSent, $"Client-{fourthInstance.NetworkManager.LocalClientId} was unabled to send a request for ownership because: {requestStatus}!");
            requestStatus = daHostInstance.RequestOwnership();
            Assert.True(requestStatus == NetworkObject.OwnershipRequestStatus.RequestSent, $"Client-{daHostInstance.NetworkManager.LocalClientId} was unabled to send a request for ownership because: {requestStatus}!");

            // The server and the 2nd client should be denied and the third client should be approved
            yield return WaitForConditionOrTimeOut(() =>
            (firstInstanceHelper.OwnershipRequestResponseStatus == NetworkObject.OwnershipRequestResponseStatus.Denied) &&
            (thirdInstanceHelper.OwnershipRequestResponseStatus == NetworkObject.OwnershipRequestResponseStatus.Denied) &&
            (fourthInstanceHelper.OwnershipRequestResponseStatus == NetworkObject.OwnershipRequestResponseStatus.Denied) &&
            (daHostInstanceHelper.OwnershipRequestResponseStatus == NetworkObject.OwnershipRequestResponseStatus.Approved)
            );
            AssertOnTimeout($"[Targeted Owner] Client-{daHostInstance.NetworkManager.LocalClientId} did not get the right request reponse: {daHostInstanceHelper.OwnershipRequestResponseStatus} Expecting: {NetworkObject.OwnershipRequestResponseStatus.Approved}!");
        }

        internal class OwnershipPermissionsTestHelper : NetworkBehaviour
        {
            public static NetworkObject CurrentOwnedInstance;

            public static Dictionary<ulong, Dictionary<ulong, List<NetworkObject>>> DistributedObjects = new Dictionary<ulong, Dictionary<ulong, List<NetworkObject>>>();

            public bool AllowOwnershipRequest = true;
            public bool OnlyAllowTargetClientId = false;
            public ulong ClientToAllowOwnership;

            public NetworkObject.OwnershipRequestResponseStatus OwnershipRequestResponseStatus { get; private set; }

            public NetworkObject.OwnershipPermissionsFailureStatus OwnershipPermissionsFailureStatus { get; private set; }

            public NetworkObject.OwnershipRequestResponseStatus ExpectOwnershipRequestResponseStatus { get; set; }

            public override void OnNetworkSpawn()
            {
                NetworkObject.OnOwnershipRequested = OnOwnershipRequested;
                NetworkObject.OnOwnershipRequestResponse = OnOwnershipRequestResponse;
                NetworkObject.OnOwnershipPermissionsFailure = OnOwnershipPermissionsFailure;

                base.OnNetworkSpawn();
            }

            private bool OnOwnershipRequested(ulong clientId)
            {
                // If we are not allowing any client to request (without locking), then deny all requests
                if (!AllowOwnershipRequest)
                {
                    return false;
                }

                // If we are only allowing a specific client and the requesting client is not the target,
                // then deny the request
                if (OnlyAllowTargetClientId && clientId != ClientToAllowOwnership)
                {
                    return false;
                }

                // Otherwise, approve the request
                return true;
            }

            private void OnOwnershipRequestResponse(NetworkObject.OwnershipRequestResponseStatus ownershipRequestResponseStatus)
            {
                OwnershipRequestResponseStatus = ownershipRequestResponseStatus;
            }

            private void OnOwnershipPermissionsFailure(NetworkObject.OwnershipPermissionsFailureStatus ownershipPermissionsFailureStatus)
            {
                OwnershipPermissionsFailureStatus = ownershipPermissionsFailureStatus;
            }

            public override void OnNetworkDespawn()
            {
                NetworkObject.OnOwnershipRequested = null;
                NetworkObject.OnOwnershipRequestResponse = null;
                base.OnNetworkSpawn();
            }

            protected override void OnOwnershipChanged(ulong previous, ulong current)
            {
                if (current == NetworkManager.LocalClientId)
                {
                    CurrentOwnedInstance = NetworkObject;
                }
                base.OnOwnershipChanged(previous, current);
            }
        }
    }
}
