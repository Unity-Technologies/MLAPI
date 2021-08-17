using System;
using Unity.Profiling;
using UnityEngine;

namespace Unity.Netcode
{
    /// <summary>
    /// MessageQueueProcessing
    /// Handles processing of MessageQueues
    /// Inbound to invocation
    /// Outbound to send
    /// </summary>
    internal class MessageQueueProcessor
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static ProfilerMarker s_ProcessReceiveQueue = new ProfilerMarker($"{nameof(MessageQueueProcessor)}.{nameof(ProcessReceiveQueue)}");
        private static ProfilerMarker s_ProcessSendQueue = new ProfilerMarker($"{nameof(MessageQueueProcessor)}.{nameof(ProcessSendQueue)}");
#endif

        // Batcher object used to manage the message batching on the send side
        private readonly MessageBatcher m_MessageBatcher = new MessageBatcher();
        private const int k_BatchThreshold = 512;
        // Selected mostly arbitrarily... Better solution to come soon.
        private const int k_FragmentationThreshold = 1024;

        private bool m_AdvanceInboundFrameHistory = false;

        //The MessageQueueContainer that is associated with this MessageQueueProcessor
        private MessageQueueContainer m_MessageQueueContainer;

        private readonly NetworkManager m_NetworkManager;

        public void Shutdown()
        {
            m_MessageBatcher.Shutdown();
        }

        public void ProcessMessage(in MessageFrameItem item)
        {
            try
            {
                switch (item.MessageType)
                {
                    case MessageQueueContainer.MessageType.ConnectionRequest:
                        if (m_NetworkManager.IsServer)
                        {
                            m_NetworkManager.MessageHandler.HandleConnectionRequest(item.NetworkId, item.NetworkBuffer);
                        }

                        break;
                    case MessageQueueContainer.MessageType.ConnectionApproved:
                        if (m_NetworkManager.IsClient)
                        {
                            m_NetworkManager.MessageHandler.HandleConnectionApproved(item.NetworkId, item.NetworkBuffer, item.Timestamp);
                        }

                        break;
                    case MessageQueueContainer.MessageType.ClientRpc:
                    case MessageQueueContainer.MessageType.ServerRpc:
                        // Can rely on currentStage == the original updateStage in the buffer
                        // After all, that's the whole point of it being in the buffer.
                        m_NetworkManager.InvokeRpc(item, item.UpdateStage);
                        break;
                    case MessageQueueContainer.MessageType.CreateObject:
                        if (m_NetworkManager.IsClient)
                        {
                            m_NetworkManager.MessageHandler.HandleAddObject(item.NetworkId, item.NetworkBuffer);
                        }

                        break;
                    case MessageQueueContainer.MessageType.DestroyObject:
                        if (m_NetworkManager.IsClient)
                        {
                            m_NetworkManager.MessageHandler.HandleDestroyObject(item.NetworkId, item.NetworkBuffer);
                        }

                        break;
                    case MessageQueueContainer.MessageType.ChangeOwner:
                        if (m_NetworkManager.IsClient)
                        {
                            m_NetworkManager.MessageHandler.HandleChangeOwner(item.NetworkId, item.NetworkBuffer);
                        }

                        break;
                    case MessageQueueContainer.MessageType.TimeSync:
                        if (m_NetworkManager.IsClient)
                        {
                            m_NetworkManager.MessageHandler.HandleTimeSync(item.NetworkId, item.NetworkBuffer);
                        }

                        break;

                    case MessageQueueContainer.MessageType.UnnamedMessage:
                        m_NetworkManager.MessageHandler.HandleUnnamedMessage(item.NetworkId, item.NetworkBuffer);
                        break;
                    case MessageQueueContainer.MessageType.NamedMessage:
                        m_NetworkManager.MessageHandler.HandleNamedMessage(item.NetworkId, item.NetworkBuffer);
                        break;
                    case MessageQueueContainer.MessageType.ServerLog:
                        if (m_NetworkManager.IsServer && m_NetworkManager.NetworkConfig.EnableNetworkLogs)
                        {
                            m_NetworkManager.MessageHandler.HandleNetworkLog(item.NetworkId, item.NetworkBuffer);
                        }

                        break;
                    case MessageQueueContainer.MessageType.SnapshotData:
                        InternalMessageHandler.HandleSnapshot(item.NetworkId, item.NetworkBuffer);
                        break;
                    case MessageQueueContainer.MessageType.NetworkVariableDelta:
                        m_NetworkManager.MessageHandler.HandleNetworkVariableDelta(item.NetworkId, item.NetworkBuffer);
                        break;
                    case MessageQueueContainer.MessageType.SwitchScene:
                        if (m_NetworkManager.IsClient)
                        {
                            m_NetworkManager.MessageHandler.HandleSwitchScene(item.NetworkId, item.NetworkBuffer);
                        }

                        break;
                    case MessageQueueContainer.MessageType.ClientSwitchSceneCompleted:
                        if (m_NetworkManager.IsServer && m_NetworkManager.NetworkConfig.EnableSceneManagement)
                        {
                            m_NetworkManager.MessageHandler.HandleClientSwitchSceneCompleted(item.NetworkId, item.NetworkBuffer);
                        }
                        else if (!m_NetworkManager.NetworkConfig.EnableSceneManagement)
                        {
                            NetworkLog.LogWarning($"Server received {MessageQueueContainer.MessageType.ClientSwitchSceneCompleted} from client id {item.NetworkId.ToString()}");
                        }

                        break;
                    case MessageQueueContainer.MessageType.AllClientsLoadedScene:
                        if (m_NetworkManager.IsClient)
                        {
                            m_NetworkManager.MessageHandler.HandleAllClientsSwitchSceneCompleted(item.NetworkId, item.NetworkBuffer);
                        }

                        break;
                    case MessageQueueContainer.MessageType.ParentSync:
                        if (m_NetworkManager.IsClient)
                        {
                            var networkObjectId = item.NetworkReader.ReadUInt64Packed();
                            var (isReparented, latestParent) = NetworkObject.ReadNetworkParenting(item.NetworkReader);
                            if (m_NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(networkObjectId))
                            {
                                var networkObject = m_NetworkManager.SpawnManager.SpawnedObjects[networkObjectId];
                                networkObject.SetNetworkParenting(isReparented, latestParent);
                                networkObject.ApplyNetworkParenting();
                            }
                            else if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                            {
                                NetworkLog.LogWarning($"Read {item.MessageType} for {nameof(NetworkObject)} #{networkObjectId} but could not find it in the {nameof(m_NetworkManager.SpawnManager.SpawnedObjects)}");
                            }
                        }

                        break;

                    default:
                        NetworkLog.LogWarning($"Received unknown message {((int)item.MessageType).ToString()}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);

                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning($"A {item.MessageType} threw an exception while executing! Please check Unity logs for more information.");
                }
            }
        }

        /// <summary>
        /// ProcessReceiveQueue
        /// Public facing interface method to start processing all messages in the current inbound frame
        /// </summary>
        public void ProcessReceiveQueue(NetworkUpdateStage currentStage, bool isTesting)
        {
            if (!ReferenceEquals(m_MessageQueueContainer, null))
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                s_ProcessReceiveQueue.Begin();
#endif
                var currentFrame = m_MessageQueueContainer.GetQueueHistoryFrame(MessageQueueHistoryFrame.QueueFrameType.Inbound, currentStage);
                var nextFrame = m_MessageQueueContainer.GetQueueHistoryFrame(MessageQueueHistoryFrame.QueueFrameType.Inbound, currentStage, true);
                if (nextFrame.IsDirty && nextFrame.HasLoopbackData)
                {
                    m_AdvanceInboundFrameHistory = true;
                }

                if (currentFrame != null && currentFrame.IsDirty)
                {
                    var currentQueueItem = currentFrame.GetFirstQueueItem();
                    while (currentQueueItem.MessageType != MessageQueueContainer.MessageType.None)
                    {
                        m_AdvanceInboundFrameHistory = true;

                        if (!isTesting)
                        {
                            currentQueueItem.UpdateStage = currentStage;
                            ProcessMessage(currentQueueItem);
                        }

                        currentQueueItem = currentFrame.GetNextQueueItem();
                    }

                    //We call this to dispose of the shared stream writer and stream
                    currentFrame.CloseQueue();
                }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                s_ProcessReceiveQueue.End();
#endif
            }
        }

        public void AdvanceFrameHistoryIfNeeded()
        {
            if (m_AdvanceInboundFrameHistory)
            {
                m_MessageQueueContainer.AdvanceFrameHistory(MessageQueueHistoryFrame.QueueFrameType.Inbound);
                m_AdvanceInboundFrameHistory = false;
            }
        }

        /// <summary>
        /// ProcessSendQueue
        /// Called to send both performance RPC and internal messages and then flush the outbound queue
        /// </summary>
        internal void ProcessSendQueue(bool isListening)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_ProcessSendQueue.Begin();
#endif

            MessageQueueSendAndFlush(isListening);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_ProcessSendQueue.End();
#endif
        }

        /// <summary>
        /// Sends all message queue items in the current outbound frame
        /// </summary>
        /// <param name="isListening">if flase it will just process through the queue items but attempt to send</param>
        private void MessageQueueSendAndFlush(bool isListening)
        {
            var advanceFrameHistory = false;
            if (!ReferenceEquals(m_MessageQueueContainer, null))
            {
                var currentFrame = m_MessageQueueContainer.GetCurrentFrame(MessageQueueHistoryFrame.QueueFrameType.Outbound, NetworkUpdateStage.PostLateUpdate);
                if (currentFrame != null)
                {
                    var currentQueueItem = currentFrame.GetFirstQueueItem();
                    while (currentQueueItem.MessageType != MessageQueueContainer.MessageType.None)
                    {
                        advanceFrameHistory = true;
                        if (isListening)
                        {
                            if (m_MessageQueueContainer.IsUsingBatching())
                            {
                                m_MessageBatcher.QueueItem(currentQueueItem, k_BatchThreshold, SendCallback);
                            }
                            else
                            {
                                SendFrameQueueItem(currentQueueItem);
                            }
                        }

                        currentQueueItem = currentFrame.GetNextQueueItem();
                    }

                    //If the size is < m_BatchThreshold then just send the messages
                    if (isListening && advanceFrameHistory && m_MessageQueueContainer.IsUsingBatching())
                    {
                        m_MessageBatcher.SendItems(0, SendCallback);
                    }
                }

                //If we processed any messages, then advance to the next frame
                if (advanceFrameHistory)
                {
                    m_MessageQueueContainer.AdvanceFrameHistory(MessageQueueHistoryFrame.QueueFrameType.Outbound);
                }
            }
        }

        /// <summary>
        /// This is the callback from the batcher when it need to send a batch
        /// </summary>
        /// <param name="clientId"> clientId to send to</param>
        /// <param name="sendStream"> the stream to send</param>
        private void SendCallback(ulong clientId, MessageBatcher.SendStream sendStream)
        {
            var length = (int)sendStream.Buffer.Length;
            var bytes = sendStream.Buffer.GetBuffer();
            var sendBuffer = new ArraySegment<byte>(bytes, 0, length);

            var channel = sendStream.NetworkChannel;
            m_MessageQueueContainer.NetworkManager.NetworkConfig.NetworkTransport.Send(clientId, sendBuffer, channel);
        }

        /// <summary>
        /// SendFrameQueueItem
        /// Sends the Message Queue Item to the specified destination
        /// </summary>
        /// <param name="item">Information on what to send</param>
        private void SendFrameQueueItem(MessageFrameItem item)
        {
            var channel = item.NetworkChannel;
            switch (item.MessageType)
            {
                case MessageQueueContainer.MessageType.ServerRpc:
                    // TODO: Can we remove this special case for server RPCs?
                    {
                        m_MessageQueueContainer.NetworkManager.NetworkConfig.NetworkTransport.Send(item.NetworkId, item.MessageData, channel);
                        break;
                    }
                default:
                    {
                        foreach (ulong clientid in item.ClientNetworkIds)
                        {
                            m_MessageQueueContainer.NetworkManager.NetworkConfig.NetworkTransport.Send(clientid, item.MessageData, channel);
                        }

                        break;
                    }
            }
        }

        internal MessageQueueProcessor(MessageQueueContainer messageQueueContainer, NetworkManager networkManager)
        {
            m_MessageQueueContainer = messageQueueContainer;
            m_NetworkManager = networkManager;
        }
    }
}
