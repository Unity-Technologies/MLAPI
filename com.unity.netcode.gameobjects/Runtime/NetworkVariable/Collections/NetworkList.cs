using System;
using System.Collections.Generic;
using Unity.Collections;

namespace Unity.Netcode
{
    /// <summary>
    /// Event based NetworkVariable container for syncing Lists
    /// </summary>
    /// <typeparam name="T">The type for the list</typeparam>
    [GenerateSerializationForGenericParameter(0)]
    public class NetworkList<T> : NetworkVariableBase where T : unmanaged, IEquatable<T>
    {
        private NativeList<T> m_List = new NativeList<T>(64, Allocator.Persistent);
        private NativeList<NetworkListEvent<T>> m_DirtyEvents = new NativeList<NetworkListEvent<T>>(64, Allocator.Persistent);

        /// <summary>
        /// Delegate type for list changed event
        /// </summary>
        /// <param name="changeEvent">Struct containing information about the change event</param>
        public delegate void OnListChangedDelegate(NetworkListEvent<T> changeEvent);

        /// <summary>
        /// The callback to be invoked when the list gets changed
        /// </summary>
        public event OnListChangedDelegate OnListChanged;
        internal override NetworkVariableType Type => NetworkVariableType.NetworkList;

        /// <summary>
        /// Constructor method for <see cref="NetworkList"/>
        /// </summary>
        public NetworkList() { }

        /// <inheritdoc/>
        /// <param name="values"></param>
        /// <param name="readPerm"></param>
        /// <param name="writePerm"></param>
        public NetworkList(IEnumerable<T> values = default,
            NetworkVariableReadPermission readPerm = DefaultReadPerm,
            NetworkVariableWritePermission writePerm = DefaultWritePerm)
            : base(readPerm, writePerm)
        {
            // allow null IEnumerable<T> to mean "no values"
            if (values != null)
            {
                foreach (var value in values)
                {
                    m_List.Add(value);
                }
            }
        }

        /// <inheritdoc />
        public override void ResetDirty()
        {
            base.ResetDirty();
            if (m_DirtyEvents.Length > 0)
            {
                m_DirtyEvents.Clear();
            }
        }

        /// <inheritdoc />
        public override bool IsDirty()
        {
            // we call the base class to allow the SetDirty() mechanism to work
            return base.IsDirty() || m_DirtyEvents.Length > 0;
        }

        internal void MarkNetworkObjectDirty()
        {
            MarkNetworkBehaviourDirty();
        }

        /// <inheritdoc />
        public override void WriteDelta(FastBufferWriter writer)
        {

            if (base.IsDirty())
            {
                writer.WriteValueSafe((ushort)1);
                writer.WriteValueSafe(NetworkListEvent<T>.EventType.Full);
                WriteField(writer);

                return;
            }

            writer.WriteValueSafe((ushort)m_DirtyEvents.Length);
            for (int i = 0; i < m_DirtyEvents.Length; i++)
            {
                var element = m_DirtyEvents.ElementAt(i);
                writer.WriteValueSafe(element.Type);
                switch (element.Type)
                {
                    case NetworkListEvent<T>.EventType.Add:
                        {
                            NetworkVariableSerialization<T>.Serializer.Write(writer, ref element.Value);
                        }
                        break;
                    case NetworkListEvent<T>.EventType.Insert:
                        {
                            BytePacker.WriteValueBitPacked(writer, element.Index);
                            NetworkVariableSerialization<T>.Serializer.Write(writer, ref element.Value);
                        }
                        break;
                    case NetworkListEvent<T>.EventType.Remove:
                        {
                            NetworkVariableSerialization<T>.Serializer.Write(writer, ref element.Value);
                        }
                        break;
                    case NetworkListEvent<T>.EventType.RemoveAt:
                        {
                            BytePacker.WriteValueBitPacked(writer, element.Index);
                        }
                        break;
                    case NetworkListEvent<T>.EventType.Value:
                        {
                            BytePacker.WriteValueBitPacked(writer, element.Index);
                            NetworkVariableSerialization<T>.Serializer.Write(writer, ref element.Value);
                        }
                        break;
                    case NetworkListEvent<T>.EventType.Clear:
                        {
                            //Nothing has to be written
                        }
                        break;
                }
            }
        }

        /// <inheritdoc />
        public override void WriteField(FastBufferWriter writer)
        {
            if (m_NetworkManager.DistributedAuthorityMode)
            {
                writer.WriteValueSafe(NetworkVariableSerialization<T>.Serializer.Type);
                if (NetworkVariableSerialization<T>.Serializer.Type == NetworkVariableType.Unmanaged)
                {
                    // Write the size of the unmanaged serialized type as it has a fixed size. This allows the CMB runtime to correctly read the unmanged type.
                    var placeholder = new T();
                    var startPos = writer.Position;
                    NetworkVariableSerialization<T>.Serializer.Write(writer, ref placeholder);
                    var size = writer.Position - startPos;
                    writer.Seek(startPos);
                    BytePacker.WriteValueBitPacked(writer, size);
                }
            }
            writer.WriteValueSafe((ushort)m_List.Length);
            for (int i = 0; i < m_List.Length; i++)
            {
                NetworkVariableSerialization<T>.Serializer.Write(writer, ref m_List.ElementAt(i));
            }
        }

        /// <inheritdoc />
        public override void ReadField(FastBufferReader reader)
        {
            m_List.Clear();
            if (m_NetworkManager.DistributedAuthorityMode)
            {
                SerializationTools.ReadType(reader, NetworkVariableSerialization<T>.Serializer);
                // Collection item type is used by the DA server, drop value here.
                if (NetworkVariableSerialization<T>.Serializer.Type == NetworkVariableType.Unmanaged)
                {
                    ByteUnpacker.ReadValueBitPacked(reader, out int _);
                }
            }
            reader.ReadValueSafe(out ushort count);
            for (int i = 0; i < count; i++)
            {
                var value = new T();
                NetworkVariableSerialization<T>.Serializer.Read(reader, ref value);
                m_List.Add(value);
            }
        }

        /// <inheritdoc />
        public override void ReadDelta(FastBufferReader reader, bool keepDirtyDelta)
        {
            /// This is only invoked by <see cref="NetworkVariableDeltaMessage"/> and the only time
            /// keepDirtyDelta is set is when it is the server processing. To be able to handle previous
            /// versions, we use IsServer to keep the dirty states received and the keepDirtyDelta to
            /// actually mark this as dirty and add it to the list of <see cref="NetworkObject"/>s to
            /// be updated. With the forwarding of deltas being handled by <see cref="NetworkVariableDeltaMessage"/>,
            /// once all clients have been forwarded the dirty events, we clear them by invoking <see cref="PostDeltaRead"/>.
            var isServer = m_NetworkManager.IsServer;
            reader.ReadValueSafe(out ushort deltaCount);
            for (int i = 0; i < deltaCount; i++)
            {
                reader.ReadValueSafe(out NetworkListEvent<T>.EventType eventType);
                switch (eventType)
                {
                    case NetworkListEvent<T>.EventType.Add:
                        {
                            var value = new T();
                            NetworkVariableSerialization<T>.Serializer.Read(reader, ref value);
                            m_List.Add(value);

                            if (OnListChanged != null)
                            {
                                OnListChanged(new NetworkListEvent<T>
                                {
                                    Type = eventType,
                                    Index = m_List.Length - 1,
                                    Value = m_List[m_List.Length - 1]
                                });
                            }

                            if (isServer)
                            {
                                m_DirtyEvents.Add(new NetworkListEvent<T>()
                                {
                                    Type = eventType,
                                    Index = m_List.Length - 1,
                                    Value = m_List[m_List.Length - 1]
                                });
                                // Preserve the legacy way of handling this
                                if (keepDirtyDelta)
                                {
                                    MarkNetworkObjectDirty();
                                }
                            }
                        }
                        break;
                    case NetworkListEvent<T>.EventType.Insert:
                        {
                            ByteUnpacker.ReadValueBitPacked(reader, out int index);
                            var value = new T();
                            NetworkVariableSerialization<T>.Serializer.Read(reader, ref value);

                            if (index < m_List.Length)
                            {
                                m_List.InsertRangeWithBeginEnd(index, index + 1);
                                m_List[index] = value;
                            }
                            else
                            {
                                m_List.Add(value);
                            }

                            if (OnListChanged != null)
                            {
                                OnListChanged(new NetworkListEvent<T>
                                {
                                    Type = eventType,
                                    Index = index,
                                    Value = m_List[index]
                                });
                            }

                            if (isServer)
                            {
                                m_DirtyEvents.Add(new NetworkListEvent<T>()
                                {
                                    Type = eventType,
                                    Index = index,
                                    Value = m_List[index]
                                });
                                // Preserve the legacy way of handling this
                                if (keepDirtyDelta)
                                {
                                    MarkNetworkObjectDirty();
                                }
                            }
                        }
                        break;
                    case NetworkListEvent<T>.EventType.Remove:
                        {
                            var value = new T();
                            NetworkVariableSerialization<T>.Serializer.Read(reader, ref value);
                            int index = m_List.IndexOf(value);
                            if (index == -1)
                            {
                                break;
                            }

                            m_List.RemoveAt(index);

                            if (OnListChanged != null)
                            {
                                OnListChanged(new NetworkListEvent<T>
                                {
                                    Type = eventType,
                                    Index = index,
                                    Value = value
                                });
                            }

                            if (isServer)
                            {
                                m_DirtyEvents.Add(new NetworkListEvent<T>()
                                {
                                    Type = eventType,
                                    Index = index,
                                    Value = value
                                });
                                // Preserve the legacy way of handling this
                                if (keepDirtyDelta)
                                {
                                    MarkNetworkObjectDirty();
                                }
                            }
                        }
                        break;
                    case NetworkListEvent<T>.EventType.RemoveAt:
                        {
                            ByteUnpacker.ReadValueBitPacked(reader, out int index);
                            T value = m_List[index];
                            m_List.RemoveAt(index);

                            if (OnListChanged != null)
                            {
                                OnListChanged(new NetworkListEvent<T>
                                {
                                    Type = eventType,
                                    Index = index,
                                    Value = value
                                });
                            }

                            if (isServer)
                            {
                                m_DirtyEvents.Add(new NetworkListEvent<T>()
                                {
                                    Type = eventType,
                                    Index = index,
                                    Value = value
                                });
                                // Preserve the legacy way of handling this
                                if (keepDirtyDelta)
                                {
                                    MarkNetworkObjectDirty();
                                }
                            }
                        }
                        break;
                    case NetworkListEvent<T>.EventType.Value:
                        {
                            ByteUnpacker.ReadValueBitPacked(reader, out int index);
                            var value = new T();
                            NetworkVariableSerialization<T>.Serializer.Read(reader, ref value);
                            if (index >= m_List.Length)
                            {
                                throw new Exception("Shouldn't be here, index is higher than list length");
                            }

                            var previousValue = m_List[index];
                            m_List[index] = value;

                            if (OnListChanged != null)
                            {
                                OnListChanged(new NetworkListEvent<T>
                                {
                                    Type = eventType,
                                    Index = index,
                                    Value = value,
                                    PreviousValue = previousValue
                                });
                            }

                            if (isServer)
                            {
                                m_DirtyEvents.Add(new NetworkListEvent<T>()
                                {
                                    Type = eventType,
                                    Index = index,
                                    Value = value,
                                    PreviousValue = previousValue
                                });
                                // Preserve the legacy way of handling this
                                if (keepDirtyDelta)
                                {
                                    MarkNetworkObjectDirty();
                                }
                            }
                        }
                        break;
                    case NetworkListEvent<T>.EventType.Clear:
                        {
                            //Read nothing
                            m_List.Clear();

                            if (OnListChanged != null)
                            {
                                OnListChanged(new NetworkListEvent<T>
                                {
                                    Type = eventType,
                                });
                            }

                            if (isServer)
                            {
                                m_DirtyEvents.Add(new NetworkListEvent<T>()
                                {
                                    Type = eventType
                                });

                                // Preserve the legacy way of handling this
                                if (keepDirtyDelta)
                                {
                                    MarkNetworkObjectDirty();
                                }
                            }
                        }
                        break;
                    case NetworkListEvent<T>.EventType.Full:
                        {
                            ReadField(reader);
                            ResetDirty();
                        }
                        break;
                }
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// For NetworkList, we just need to reset dirty if a server has read deltas
        /// </remarks>
        internal override void PostDeltaRead()
        {
            if (m_NetworkManager.IsServer)
            {
                ResetDirty();
            }
        }

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            return m_List.GetEnumerator();
        }

        /// <inheritdoc />
        public void Add(T item)
        {
            // check write permissions
            if (!CanClientWrite(m_NetworkManager.LocalClientId))
            {
                LogWritePermissionError();
                return;
            }

            m_List.Add(item);

            var listEvent = new NetworkListEvent<T>()
            {
                Type = NetworkListEvent<T>.EventType.Add,
                Value = item,
                Index = m_List.Length - 1
            };

            HandleAddListEvent(listEvent);
        }

        /// <inheritdoc />
        public void Clear()
        {
            // check write permissions
            if (!CanClientWrite(m_NetworkManager.LocalClientId))
            {
                LogWritePermissionError();
                return;
            }

            m_List.Clear();

            var listEvent = new NetworkListEvent<T>()
            {
                Type = NetworkListEvent<T>.EventType.Clear
            };

            HandleAddListEvent(listEvent);
        }

        /// <inheritdoc />
        public bool Contains(T item)
        {
            int index = m_List.IndexOf(item);
            return index != -1;
        }

        /// <inheritdoc />
        public bool Remove(T item)
        {
            // check write permissions
            if (!CanClientWrite(m_NetworkManager.LocalClientId))
            {
                LogWritePermissionError();
                return false;
            }

            int index = m_List.IndexOf(item);
            if (index == -1)
            {
                return false;
            }

            m_List.RemoveAt(index);
            var listEvent = new NetworkListEvent<T>()
            {
                Type = NetworkListEvent<T>.EventType.Remove,
                Value = item
            };

            HandleAddListEvent(listEvent);
            return true;
        }

        /// <inheritdoc />
        public int Count => m_List.Length;

        /// <inheritdoc />
        public int IndexOf(T item)
        {
            return m_List.IndexOf(item);
        }

        /// <inheritdoc />
        public void Insert(int index, T item)
        {
            // check write permissions
            if (!CanClientWrite(m_NetworkManager.LocalClientId))
            {
                LogWritePermissionError();
                return;
            }

            if (index < m_List.Length)
            {
                m_List.InsertRangeWithBeginEnd(index, index + 1);
                m_List[index] = item;
            }
            else
            {
                m_List.Add(item);
            }

            var listEvent = new NetworkListEvent<T>()
            {
                Type = NetworkListEvent<T>.EventType.Insert,
                Index = index,
                Value = item
            };

            HandleAddListEvent(listEvent);
        }

        /// <inheritdoc />
        public void RemoveAt(int index)
        {
            // check write permissions
            if (!CanClientWrite(m_NetworkManager.LocalClientId))
            {
                LogWritePermissionError();
                return;
            }

            var value = m_List[index];
            m_List.RemoveAt(index);

            var listEvent = new NetworkListEvent<T>()
            {
                Type = NetworkListEvent<T>.EventType.RemoveAt,
                Index = index,
                Value = value
            };

            HandleAddListEvent(listEvent);
        }



        /// <inheritdoc />
        public T this[int index]
        {
            get => m_List[index];
            set
            {
                // check write permissions
                if (!CanClientWrite(m_NetworkManager.LocalClientId))
                {
                    LogWritePermissionError();
                    return;
                }

                var previousValue = m_List[index];
                m_List[index] = value;

                var listEvent = new NetworkListEvent<T>()
                {
                    Type = NetworkListEvent<T>.EventType.Value,
                    Index = index,
                    Value = value,
                    PreviousValue = previousValue
                };

                HandleAddListEvent(listEvent);
            }
        }

        private void HandleAddListEvent(NetworkListEvent<T> listEvent)
        {
            m_DirtyEvents.Add(listEvent);
            MarkNetworkObjectDirty();
            OnListChanged?.Invoke(listEvent);
        }

        /// <summary>
        /// This is actually unused left-over from a previous interface
        /// </summary>
        public int LastModifiedTick
        {
            get
            {
                // todo: implement proper network tick for NetworkList
                return NetworkTickSystem.NoTick;
            }
        }

        /// <summary>
        /// Overridden <see cref="IDisposable"/> implementation.
        /// CAUTION: If you derive from this class and override the <see cref="Dispose"/> method,
        /// you **must** always invoke the base.Dispose() method!
        /// </summary>
        public override void Dispose()
        {
            m_List.Dispose();
            m_DirtyEvents.Dispose();
            base.Dispose();
        }
    }

    /// <summary>
    /// Struct containing event information about changes to a NetworkList.
    /// </summary>
    /// <typeparam name="T">The type for the list that the event is about</typeparam>
    public struct NetworkListEvent<T>
    {
        /// <summary>
        /// Enum representing the different operations available for triggering an event.
        /// </summary>
        public enum EventType : byte
        {
            /// <summary>
            /// Add
            /// </summary>
            Add,

            /// <summary>
            /// Insert
            /// </summary>
            Insert,

            /// <summary>
            /// Remove
            /// </summary>
            Remove,

            /// <summary>
            /// Remove at
            /// </summary>
            RemoveAt,

            /// <summary>
            /// Value changed
            /// </summary>
            Value,

            /// <summary>
            /// Clear
            /// </summary>
            Clear,

            /// <summary>
            /// Full list refresh
            /// </summary>
            Full
        }

        /// <summary>
        /// Enum representing the operation made to the list.
        /// </summary>
        public EventType Type;

        /// <summary>
        /// The value changed, added or removed if available.
        /// </summary>
        public T Value;

        /// <summary>
        /// The previous value when "Value" has changed, if available.
        /// </summary>
        public T PreviousValue;

        /// <summary>
        /// the index changed, added or removed if available
        /// </summary>
        public int Index;
    }
}
