using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.Netcode
{

    internal class InvalidMessageStructureException : SystemException
    {
        public InvalidMessageStructureException() { }
        public InvalidMessageStructureException(string issue) : base(issue) { }
    }

    internal class MessagingSystem : IDisposable
    {
        private struct ReceiveQueueItem
        {
            public FastBufferReader Reader;
            public MessageHeader Header;
            public ulong SenderId;
            public float Timestamp;
        }

        private struct SendQueueItem
        {
            public BatchHeader BatchHeader;
            public FastBufferWriter Writer;
            public readonly NetworkDelivery NetworkDelivery;

            public SendQueueItem(NetworkDelivery delivery, int writerSize, Allocator writerAllocator, int maxWriterSize = -1)
            {
                Writer = new FastBufferWriter(writerSize, writerAllocator, maxWriterSize);
                NetworkDelivery = delivery;
                BatchHeader = default;
            }
        }

        internal delegate void MessageHandler(FastBufferReader reader, in NetworkContext context);

        private NativeList<ReceiveQueueItem> m_IncomingMessageQueue = new NativeList<ReceiveQueueItem>(16, Allocator.Persistent);

        private MessageHandler[] m_MessageHandlers = new MessageHandler[255];
        private Type[] m_ReverseTypeMap = new Type[255];

        private Dictionary<Type, byte> m_MessageTypes = new Dictionary<Type, byte>();
        private Dictionary<ulong, NativeList<SendQueueItem>> m_SendQueues = new Dictionary<ulong, NativeList<SendQueueItem>>();

        private List<INetworkHooks> m_Hooks = new List<INetworkHooks>();

        private byte m_HighMessageType;
        private object m_Owner;
        private IMessageSender m_MessageSender;
        private ulong m_LocalClientId;
        private bool m_Disposed;

        internal Type[] MessageTypes => m_ReverseTypeMap;
        internal MessageHandler[] MessageHandlers => m_MessageHandlers;
        internal int MessageHandlerCount => m_HighMessageType;

        internal byte GetMessageType(Type t)
        {
            return m_MessageTypes[t];
        }

        public const int NON_FRAGMENTED_MESSAGE_MAX_SIZE = 1300;
        public const int FRAGMENTED_MESSAGE_MAX_SIZE = 64000;

        public MessagingSystem(IMessageSender messageSender, object owner, ulong localClientId = long.MaxValue)
        {
            try
            {
                m_LocalClientId = localClientId;
                m_MessageSender = messageSender;
                m_Owner = owner;

                var interfaceType = typeof(INetworkMessage);
                var implementationTypes = new List<Type>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.IsInterface || type.IsAbstract)
                        {
                            continue;
                        }

                        if (interfaceType.IsAssignableFrom(type))
                        {
                            var attributes = type.GetCustomAttributes(typeof(IgnoreMessageIfSystemOwnerIsNotOfTypeAttribute), false);
                            // If [Bind(ownerType)] isn't provided, it defaults to being bound to NetworkManager
                            // This is technically a breach of domain by having MessagingSystem know about the existence
                            // of NetworkManager... but ultimately, Bind is provided to support testing, not to support
                            // general use of MessagingSystem outside of Netcode for GameObjects, so having MessagingSystem
                            // know about NetworkManager isn't so bad. Especially since it's just a default value.
                            // This is just a convenience to keep us and our users from having to use
                            // [Bind(typeof(NetworkManager))] on every message - only tests that don't want to use
                            // the full NetworkManager need to worry about it.
                            var allowedToBind = attributes.Length == 0 && m_Owner is NetworkManager;
                            for (var i = 0; i < attributes.Length; ++i)
                            {
                                var bindAttribute = (IgnoreMessageIfSystemOwnerIsNotOfTypeAttribute)attributes[i];
                                if (
                                    (bindAttribute.BoundType != null &&
                                     bindAttribute.BoundType.IsInstanceOfType(m_Owner)) ||
                                    (m_Owner == null && bindAttribute.BoundType == null))
                                {
                                    allowedToBind = true;
                                    break;
                                }
                            }

                            if (!allowedToBind)
                            {
                                continue;
                            }

                            implementationTypes.Add(type);
                        }
                    }
                }

                implementationTypes.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
                foreach (var type in implementationTypes)
                {
                    RegisterMessageType(type);
                }
            }
            catch (Exception)
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            // Can't just iterate SendQueues or SendQueues.Keys because ClientDisconnected removes
            // from the queue.
            foreach (var kvp in m_SendQueues)
            {
                CleanupDisconnectedClient(kvp.Key);
            }
            m_IncomingMessageQueue.Dispose();
            m_Disposed = true;
        }

        ~MessagingSystem()
        {
            Dispose();
        }

        public void SetLocalClientId(ulong localClientId)
        {
            m_LocalClientId = localClientId;
        }

        public void Hook(INetworkHooks hooks)
        {
            m_Hooks.Add(hooks);
        }

        private void RegisterMessageType(Type messageType)
        {
            if (!typeof(INetworkMessage).IsAssignableFrom(messageType))
            {
                throw new ArgumentException("RegisterMessageType types must be INetworkMessage types.");
            }

            var method = messageType.GetMethod("Receive");
            if (method == null)
            {
                throw new InvalidMessageStructureException(
                    $"{messageType.FullName}: All INetworkMessage types must implement public static void Receive(FastBufferReader reader, in NetworkContext context)");
            }

            var asDelegate = Delegate.CreateDelegate(typeof(MessageHandler), method, false);
            if (asDelegate == null)
            {
                throw new InvalidMessageStructureException(
                    $"{messageType.FullName}: All INetworkMessage types must implement public static void Receive(FastBufferReader reader, in NetworkContext context)");
            }

            m_MessageHandlers[m_HighMessageType] = (MessageHandler)asDelegate;
            m_ReverseTypeMap[m_HighMessageType] = messageType;
            m_MessageTypes[messageType] = m_HighMessageType++;
        }

        internal void HandleIncomingData(ulong clientId, ArraySegment<byte> data, float receiveTime)
        {
            unsafe
            {
                fixed (byte* nativeData = data.Array)
                {
                    var batchReader =
                        new FastBufferReader(nativeData, Allocator.None, data.Count, data.Offset);
                    if (!batchReader.TryBeginRead(sizeof(BatchHeader)))
                    {
                        NetworkLog.LogWarning("Received a packet too small to contain a BatchHeader. Ignoring it.");
                        return;
                    }

                    batchReader.ReadValue(out BatchHeader batchHeader);

                    for (var hookIdx = 0; hookIdx < m_Hooks.Count; ++hookIdx)
                    {
                        m_Hooks[hookIdx].OnBeforeReceiveBatch(clientId, batchHeader.BatchSize, batchReader.Length);
                    }

                    for (var messageIdx = 0; messageIdx < batchHeader.BatchSize; ++messageIdx)
                    {
                        if (!batchReader.TryBeginRead(sizeof(MessageHeader)))
                        {
                            NetworkLog.LogWarning("Received a batch that didn't have enough data for all of its batches, ending early!");
                            return;
                        }
                        batchReader.ReadValue(out MessageHeader messageHeader);

                        if (!batchReader.TryBeginRead(messageHeader.MessageSize))
                        {
                            NetworkLog.LogWarning("Received a message that claimed a size larger than the packet, ending early!");
                            return;
                        }
                        m_IncomingMessageQueue.Add(new ReceiveQueueItem
                        {
                            Header = messageHeader,
                            SenderId = clientId,
                            Timestamp = receiveTime,
                            // Copy the data for this message into a new FastBufferReader that owns that memory.
                            // We can't guarantee the memory in the ArraySegment stays valid because we don't own it,
                            // so we must move it to memory we do own.
                            Reader = new FastBufferReader(batchReader.GetUnsafePtrAtCurrentPosition(), Allocator.TempJob, messageHeader.MessageSize)
                        });
                        batchReader.Seek(batchReader.Position + messageHeader.MessageSize);
                    }
                    for (var hookIdx = 0; hookIdx < m_Hooks.Count; ++hookIdx)
                    {
                        m_Hooks[hookIdx].OnAfterReceiveBatch(clientId, batchHeader.BatchSize, batchReader.Length);
                    }
                }
            }
        }

        private bool CanReceive(ulong clientId, Type messageType)
        {
            for (var hookIdx = 0; hookIdx < m_Hooks.Count; ++hookIdx)
            {
                if (!m_Hooks[hookIdx].OnVerifyCanReceive(clientId, messageType))
                {
                    return false;
                }
            }

            return true;
        }

        public void HandleMessage(in MessageHeader header, FastBufferReader reader, ulong senderId, float timestamp)
        {
            if (header.MessageType >= m_HighMessageType)
            {
                Debug.LogWarning($"Received a message with invalid message type value {header.MessageType}");
                reader.Dispose();
                return;
            }
            var context = new NetworkContext
            {
                SystemOwner = m_Owner,
                SenderId = senderId,
                Timestamp = timestamp,
                Header = header
            };
            var type = m_ReverseTypeMap[header.MessageType];
            if (!CanReceive(senderId, type))
            {
                reader.Dispose();
                return;
            }

            for (var hookIdx = 0; hookIdx < m_Hooks.Count; ++hookIdx)
            {
                m_Hooks[hookIdx].OnBeforeReceiveMessage(senderId, type, reader.Length);
            }
            var handler = m_MessageHandlers[header.MessageType];
            using (reader)
            {
                // No user-land message handler exceptions should escape the receive loop.
                // If an exception is throw, the message is ignored.
                // Example use case: A bad message is received that can't be deserialized and throws
                // an OverflowException because it specifies a length greater than the number of bytes in it
                // for some dynamic-length value.
                try
                {
                    handler.Invoke(reader, context);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            for (var hookIdx = 0; hookIdx < m_Hooks.Count; ++hookIdx)
            {
                m_Hooks[hookIdx].OnAfterReceiveMessage(senderId, type, reader.Length);
            }
        }

        internal unsafe void ProcessIncomingMessageQueue()
        {
            for (var i = 0; i < m_IncomingMessageQueue.Length; ++i)
            {
                // Avoid copies...
                ref var item = ref m_IncomingMessageQueue.GetUnsafeList()->ElementAt(i);
                HandleMessage(item.Header, item.Reader, item.SenderId, item.Timestamp);
            }

            m_IncomingMessageQueue.Clear();
        }

        internal void ClientConnected(ulong clientId)
        {
            if (m_SendQueues.ContainsKey(clientId))
            {
                return;
            }
            m_SendQueues[clientId] = new NativeList<SendQueueItem>(16, Allocator.Persistent);
        }

        internal void ClientDisconnected(ulong clientId)
        {
            if (!m_SendQueues.ContainsKey(clientId))
            {
                return;
            }
            CleanupDisconnectedClient(clientId);
            m_SendQueues.Remove(clientId);
        }

        private unsafe void CleanupDisconnectedClient(ulong clientId)
        {
            var queue = m_SendQueues[clientId];
            for (var i = 0; i < queue.Length; ++i)
            {
                queue.GetUnsafeList()->ElementAt(i).Writer.Dispose();
            }

            queue.Dispose();
        }

        private bool CanSend(ulong clientId, Type messageType, NetworkDelivery delivery)
        {
            for (var hookIdx = 0; hookIdx < m_Hooks.Count; ++hookIdx)
            {
                if (!m_Hooks[hookIdx].OnVerifyCanSend(clientId, messageType, delivery))
                {
                    return false;
                }
            }

            return true;
        }

        internal unsafe int SendMessage<TMessageType, TClientIdListType>(in TMessageType message, NetworkDelivery delivery, in TClientIdListType clientIds)
            where TMessageType : INetworkMessage
            where TClientIdListType : IReadOnlyList<ulong>
        {
            var maxSize = delivery == NetworkDelivery.ReliableFragmentedSequenced ? FRAGMENTED_MESSAGE_MAX_SIZE : NON_FRAGMENTED_MESSAGE_MAX_SIZE;
            var tmpSerializer = new FastBufferWriter(NON_FRAGMENTED_MESSAGE_MAX_SIZE - sizeof(MessageHeader), Allocator.Temp, maxSize - sizeof(MessageHeader));
            using (tmpSerializer)
            {
                message.Serialize(tmpSerializer);

                for (var i = 0; i < clientIds.Count; ++i)
                {
                    var clientId = clientIds[i];

                    if (!CanSend(clientId, typeof(TMessageType), delivery))
                    {
                        continue;
                    }

                    Debug.Log($"Sending {typeof(TMessageType)} to {clientId}");

                    for (var hookIdx = 0; hookIdx < m_Hooks.Count; ++hookIdx)
                    {
                        m_Hooks[hookIdx].OnBeforeSendMessage(clientId, typeof(TMessageType), delivery);
                    }

                    var sendQueueItem = m_SendQueues[clientId];
                    if (sendQueueItem.Length == 0)
                    {
                        sendQueueItem.Add(new SendQueueItem(delivery, NON_FRAGMENTED_MESSAGE_MAX_SIZE, Allocator.TempJob,
                            maxSize));
                        sendQueueItem.GetUnsafeList()->ElementAt(0).Writer.Seek(sizeof(BatchHeader));
                    }
                    else
                    {
                        ref var lastQueueItem = ref sendQueueItem.GetUnsafeList()->ElementAt(sendQueueItem.Length - 1);
                        if (lastQueueItem.NetworkDelivery != delivery ||
                            lastQueueItem.Writer.MaxCapacity - lastQueueItem.Writer.Position
                            < tmpSerializer.Length + sizeof(MessageHeader))
                        {
                            sendQueueItem.Add(new SendQueueItem(delivery, NON_FRAGMENTED_MESSAGE_MAX_SIZE, Allocator.TempJob,
                                maxSize));
                            sendQueueItem.GetUnsafeList()->ElementAt(sendQueueItem.Length - 1).Writer.Seek(sizeof(BatchHeader));
                        }
                    }

                    ref var writeQueueItem = ref sendQueueItem.GetUnsafeList()->ElementAt(sendQueueItem.Length - 1);
                    writeQueueItem.Writer.TryBeginWrite(sizeof(MessageHeader) + tmpSerializer.Length);
                    var header = new MessageHeader
                    {
                        MessageSize = (short)tmpSerializer.Length,
                        MessageType = m_MessageTypes[typeof(TMessageType)],
                    };

                    writeQueueItem.Writer.WriteValue(header);
                    writeQueueItem.Writer.WriteBytes(tmpSerializer.GetUnsafePtr(), tmpSerializer.Length);
                    writeQueueItem.BatchHeader.BatchSize++;
                    for (var hookIdx = 0; hookIdx < m_Hooks.Count; ++hookIdx)
                    {
                        m_Hooks[hookIdx].OnAfterSendMessage(clientId, typeof(TMessageType), delivery, tmpSerializer.Length + sizeof(MessageHeader));
                    }
                }

                return tmpSerializer.Length;
            }
        }

        private struct PointerListWrapper<T> : IReadOnlyList<T>
            where T : unmanaged
        {
            private unsafe T* m_Value;
            private int m_Length;

            internal unsafe PointerListWrapper(T* ptr, int length)
            {
                m_Value = ptr;
                m_Length = length;
            }

            public int Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => m_Length;
            }

            public unsafe T this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => m_Value[index];
            }

            public IEnumerator<T> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        internal unsafe int SendMessage<T>(in T message, NetworkDelivery delivery,
            ulong* clientIds, int numClientIds)
            where T : INetworkMessage
        {
            return SendMessage(message, delivery, new PointerListWrapper<ulong>(clientIds, numClientIds));
        }

        internal unsafe int SendMessage<T>(in T message, NetworkDelivery delivery, ulong clientId)
            where T : INetworkMessage
        {
            ulong* clientIds = stackalloc ulong[] { clientId };
            return SendMessage(message, delivery, new PointerListWrapper<ulong>(clientIds, 1));
        }

        internal unsafe int SendMessage<T>(in T message, NetworkDelivery delivery, in NativeArray<ulong> clientIds)
            where T : INetworkMessage
        {
            return SendMessage(message, delivery, new PointerListWrapper<ulong>((ulong*)clientIds.GetUnsafePtr(), clientIds.Length));
        }

        internal unsafe void ProcessSendQueues()
        {
            foreach (var kvp in m_SendQueues)
            {
                var clientId = kvp.Key;
                var sendQueueItem = kvp.Value;
                for (var i = 0; i < sendQueueItem.Length; ++i)
                {
                    ref var queueItem = ref sendQueueItem.GetUnsafeList()->ElementAt(i);
                    if (queueItem.BatchHeader.BatchSize == 0)
                    {
                        queueItem.Writer.Dispose();
                        continue;
                    }

                    for (var hookIdx = 0; hookIdx < m_Hooks.Count; ++hookIdx)
                    {
                        m_Hooks[hookIdx].OnBeforeSendBatch(clientId, queueItem.BatchHeader.BatchSize, queueItem.Writer.Length, queueItem.NetworkDelivery);
                    }

                    queueItem.Writer.Seek(0);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    // Skipping the Verify and sneaking the write mark in because we know it's fine.
                    queueItem.Writer.Handle->AllowedWriteMark = 2;
#endif
                    queueItem.Writer.WriteValue(queueItem.BatchHeader);

                    try
                    {
                        m_MessageSender.Send(clientId, queueItem.NetworkDelivery, queueItem.Writer);
                    }
                    finally
                    {
                        queueItem.Writer.Dispose();
                    }

                    for (var hookIdx = 0; hookIdx < m_Hooks.Count; ++hookIdx)
                    {
                        m_Hooks[hookIdx].OnAfterSendBatch(clientId, queueItem.BatchHeader.BatchSize, queueItem.Writer.Length, queueItem.NetworkDelivery);
                    }
                }
                sendQueueItem.Clear();
            }
        }
    }
}
