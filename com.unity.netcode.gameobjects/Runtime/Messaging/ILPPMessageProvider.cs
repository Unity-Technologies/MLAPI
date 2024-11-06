using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Netcode
{
    internal struct ILPPMessageProvider : INetworkMessageProvider
    {
#pragma warning disable IDE1006 // disable naming rule violation check
        // This is NOT modified by RuntimeAccessModifiersILPP right now, but is populated by ILPP.
        internal static readonly List<NetworkMessageManager.MessageWithHandler> __network_message_types = new List<NetworkMessageManager.MessageWithHandler>();
#pragma warning restore IDE1006 // restore naming rule violation check

        /// <summary>
        /// Enum representing the different types of messages that can be sent over the network.
        /// The values cannot be changed, as they are used to serialize and deserialize messages.
        /// Adding new messages should be done by adding new values to the end of the enum 
        /// using the next free value.
        /// </summary>
        /// !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        /// Add any new Message types to this table at the END with incremented index value
        /// !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        internal enum NetworkMessageTypes : uint
        {
            ConnectionApproved = 0,
            ConnectionRequest = 1,
            ChangeOwnership = 2,
            ClientConnected = 3,
            ClientDisconnected = 4,
            ClientRpc = 5,
            CreateObject = 6,
            DestroyObject = 7,
            DisconnectReason = 8,
            ForwardClientRpc = 9,
            ForwardServerRpc = 10,
            NamedMessage = 11,
            NetworkTransformMessage = 12,
            NetworkVariableDelta = 13,
            ParentSync = 14,
            Proxy = 15,
            Rpc = 16,
            SceneEvent = 17,
            ServerLog = 18,
            ServerRpc = 19,
            SessionOwner = 20,
            TimeSync = 21,
            Unnamed = 22,
            AnticipationCounterSyncPingMessage = 23,
            AnticipationCounterSyncPongMessage = 24,
        }


        // Enable this for integration tests that need no message types defined
        internal static bool IntegrationTestNoMessages;

        public List<NetworkMessageManager.MessageWithHandler> GetMessages()
        {
            // return no message types when defined for integration tests
            if (IntegrationTestNoMessages)
            {
                return new List<NetworkMessageManager.MessageWithHandler>();
            }
            var messageTypeCount = Enum.GetValues(typeof(NetworkMessageTypes)).Length;
            // Assure the allowed types count is the same as our NetworkMessageType enum count
            if (__network_message_types.Count != messageTypeCount)
            {
                throw new Exception($"Allowed types is not equal to the number of message type indices! Allowed Count: {__network_message_types.Count} | Index Count: {messageTypeCount}");
            }

            // Populate with blanks to be replaced later
            var adjustedMessageTypes = new List<NetworkMessageManager.MessageWithHandler>();
            var blank = new NetworkMessageManager.MessageWithHandler();
            for (int i = 0; i < messageTypeCount; i++)
            {
                adjustedMessageTypes.Add(blank);
            }

            // Create a type to enum index lookup table
            // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            // Add new Message types to this table paired with its new NetworkMessageTypes enum
            // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            var messageTypes = new Dictionary<Type, NetworkMessageTypes>
            {
                { typeof(ConnectionApprovedMessage), NetworkMessageTypes.ConnectionApproved }, // This MUST be first
                { typeof(ConnectionRequestMessage), NetworkMessageTypes.ConnectionRequest }, // This MUST be second
                { typeof(ChangeOwnershipMessage), NetworkMessageTypes.ChangeOwnership },
                { typeof(ClientConnectedMessage), NetworkMessageTypes.ClientConnected },
                { typeof(ClientDisconnectedMessage), NetworkMessageTypes.ClientDisconnected },
                { typeof(ClientRpcMessage), NetworkMessageTypes.ClientRpc },
                { typeof(CreateObjectMessage), NetworkMessageTypes.CreateObject },
                { typeof(DestroyObjectMessage), NetworkMessageTypes.DestroyObject },
                { typeof(DisconnectReasonMessage), NetworkMessageTypes.DisconnectReason },
                { typeof(ForwardClientRpcMessage), NetworkMessageTypes.ForwardClientRpc },
                { typeof(ForwardServerRpcMessage), NetworkMessageTypes.ForwardServerRpc },
                { typeof(NamedMessage), NetworkMessageTypes.NamedMessage },
                { typeof(NetworkTransformMessage), NetworkMessageTypes.NetworkTransformMessage },
                { typeof(NetworkVariableDeltaMessage), NetworkMessageTypes.NetworkVariableDelta },
                { typeof(ParentSyncMessage), NetworkMessageTypes.ParentSync },
                { typeof(ProxyMessage), NetworkMessageTypes.Proxy },
                { typeof(RpcMessage), NetworkMessageTypes.Rpc },
                { typeof(SceneEventMessage), NetworkMessageTypes.SceneEvent },
                { typeof(ServerLogMessage), NetworkMessageTypes.ServerLog },
                { typeof(ServerRpcMessage), NetworkMessageTypes.ServerRpc },
                { typeof(TimeSyncMessage), NetworkMessageTypes.TimeSync },
                { typeof(UnnamedMessage), NetworkMessageTypes.Unnamed },
                { typeof(SessionOwnerMessage), NetworkMessageTypes.SessionOwner },
                { typeof(AnticipationCounterSyncPingMessage), NetworkMessageTypes.AnticipationCounterSyncPingMessage},
                { typeof(AnticipationCounterSyncPongMessage), NetworkMessageTypes.AnticipationCounterSyncPongMessage},
            };

            // Assure the type to lookup table count and NetworkMessageType enum count matches (i.e. to catch human error when adding new messages)
            if (messageTypes.Count != messageTypeCount)
            {
                throw new Exception($"Message type to Message type index count mistmatch! Table Count: {messageTypes.Count} | Index Count: {messageTypeCount}");
            }

            // Now order the allowed types list based on the order of the NetworkMessageType enum
            foreach (var messageHandler in __network_message_types)
            {
                if (!messageTypes.ContainsKey(messageHandler.MessageType))
                {
                    throw new Exception($"Missing message type from lookup table: {messageHandler.MessageType}");
                }
                adjustedMessageTypes[(int)messageTypes[messageHandler.MessageType]] = messageHandler;
            }

            // return the NetworkMessageType enum ordered list
            return adjustedMessageTypes;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        public static void NotifyOnPlayStateChange()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                // Clear out the network message types, because ILPP-generated RuntimeInitializeOnLoad code will
                // run again and add more messages to it.
                __network_message_types.Clear();
            }
        }

#endif
    }
}
