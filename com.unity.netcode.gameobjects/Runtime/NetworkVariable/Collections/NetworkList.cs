using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Unity.Netcode
{
    /// <summary>
    /// Event based NetworkVariable container for syncing Lists
    /// </summary>
    /// <typeparam name="T">The type for the list</typeparam>
    public class NetworkList<T> : IList<T>, INetworkVariable
    {
        private readonly IList<T> m_List = new List<T>();
        private readonly List<NetworkListEvent<T>> m_DirtyEvents = new List<NetworkListEvent<T>>();
        private NetworkBehaviour m_NetworkBehaviour;

        /// <summary>
        /// Gets the last time the variable was synced
        /// </summary>
        public NetworkTime LastSyncedTime { get; internal set; }

        /// <summary>
        /// The settings for this container
        /// </summary>
        public readonly NetworkVariableSettings Settings = new NetworkVariableSettings();

        /// <summary>
        /// Delegate type for list changed event
        /// </summary>
        /// <param name="changeEvent">Struct containing information about the change event</param>
        public delegate void OnListChangedDelegate(NetworkListEvent<T> changeEvent);

        /// <summary>
        /// The callback to be invoked when the list gets changed
        /// </summary>
        public event OnListChangedDelegate OnListChanged;

        /// <summary>
        /// Creates a NetworkList with the default value and settings
        /// </summary>
        public NetworkList() { }

        /// <summary>
        /// Creates a NetworkList with the default value and custom settings
        /// </summary>
        /// <param name="settings">The settings to use for the NetworkList</param>
        public NetworkList(NetworkVariableSettings settings)
        {
            Settings = settings;
        }

        /// <summary>
        /// Creates a NetworkList with a custom value and custom settings
        /// </summary>
        /// <param name="settings">The settings to use for the NetworkList</param>
        /// <param name="value">The initial value to use for the NetworkList</param>
        public NetworkList(NetworkVariableSettings settings, IList<T> value)
        {
            Settings = settings;
            m_List = value;
        }

        /// <summary>
        /// Creates a NetworkList with a custom value and the default settings
        /// </summary>
        /// <param name="value">The initial value to use for the NetworkList</param>
        public NetworkList(IList<T> value)
        {
            m_List = value;
        }

        /// <summary>
        /// Gets or sets the name of the network variable's instance
        /// (MemberInfo) where it was declared.
        /// </summary>
        public string Name { get; internal set; }

        /// <inheritdoc />
        public void ResetDirty()
        {
            m_DirtyEvents.Clear();
            LastSyncedTime = m_NetworkBehaviour.NetworkManager.LocalTime;
        }

        /// <inheritdoc />
        public bool IsDirty()
        {
            if (m_DirtyEvents.Count == 0)
            {
                return false;
            }

            if (Settings.SendTickrate == 0)
            {
                return true;
            }

            if (Settings.SendTickrate < 0)
            {
                return false;
            }

            if (m_NetworkBehaviour.NetworkManager.LocalTime.FixedTime - LastSyncedTime.FixedTime >= (1.0 / Settings.SendTickrate))
            {
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public NetworkChannel GetChannel()
        {
            return Settings.SendNetworkChannel;
        }

        /// <inheritdoc />
        public bool CanClientWrite(ulong clientId)
        {
            switch (Settings.WritePermission)
            {
                case NetworkVariableWritePermission.ServerOnly:
                    return false;
                case NetworkVariableWritePermission.OwnerOnly:
                    return m_NetworkBehaviour.OwnerClientId == clientId;
            }

            return true;
        }

        /// <inheritdoc />
        public bool CanClientRead(ulong clientId)
        {
            switch (Settings.ReadPermission)
            {
                case NetworkVariableReadPermission.Everyone:
                    return true;
                case NetworkVariableReadPermission.OwnerOnly:
                    return m_NetworkBehaviour.OwnerClientId == clientId;
            }

            return true;
        }

        /// <inheritdoc />
        public void WriteDelta(Stream stream)
        {
            using (var writer = PooledNetworkWriter.Get(stream))
            {
                writer.WriteUInt16Packed((ushort)m_DirtyEvents.Count);
                for (int i = 0; i < m_DirtyEvents.Count; i++)
                {
                    writer.WriteBits((byte)m_DirtyEvents[i].Type, 3);
                    switch (m_DirtyEvents[i].Type)
                    {
                        case NetworkListEvent<T>.EventType.Add:
                            {
                                writer.WriteObjectPacked(m_DirtyEvents[i].Value); //BOX
                            }
                            break;
                        case NetworkListEvent<T>.EventType.Insert:
                            {
                                writer.WriteInt32Packed(m_DirtyEvents[i].Index);
                                writer.WriteObjectPacked(m_DirtyEvents[i].Value); //BOX
                            }
                            break;
                        case NetworkListEvent<T>.EventType.Remove:
                            {
                                writer.WriteObjectPacked(m_DirtyEvents[i].Value); //BOX
                            }
                            break;
                        case NetworkListEvent<T>.EventType.RemoveAt:
                            {
                                writer.WriteInt32Packed(m_DirtyEvents[i].Index);
                            }
                            break;
                        case NetworkListEvent<T>.EventType.Value:
                            {
                                writer.WriteInt32Packed(m_DirtyEvents[i].Index);
                                writer.WriteObjectPacked(m_DirtyEvents[i].Value); //BOX
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
        }

        /// <inheritdoc />
        public void WriteField(Stream stream)
        {
            using (var writer = PooledNetworkWriter.Get(stream))
            {
                writer.WriteUInt16Packed((ushort)m_List.Count);
                for (int i = 0; i < m_List.Count; i++)
                {
                    writer.WriteObjectPacked(m_List[i]); //BOX
                }
            }
        }

        /// <inheritdoc />
        public void ReadField(Stream stream)
        {
            using (var reader = PooledNetworkReader.Get(stream))
            {
                m_List.Clear();
                ushort count = reader.ReadUInt16Packed();
                for (int i = 0; i < count; i++)
                {
                    m_List.Add((T)reader.ReadObjectPacked(typeof(T))); //BOX
                }
            }
        }

        /// <inheritdoc />
        public void ReadDelta(Stream stream, bool keepDirtyDelta)
        {
            using (var reader = PooledNetworkReader.Get(stream))
            {
                ushort deltaCount = reader.ReadUInt16Packed();
                for (int i = 0; i < deltaCount; i++)
                {
                    var eventType = (NetworkListEvent<T>.EventType)reader.ReadBits(3);
                    switch (eventType)
                    {
                        case NetworkListEvent<T>.EventType.Add:
                            {
                                m_List.Add((T)reader.ReadObjectPacked(typeof(T))); //BOX

                                if (OnListChanged != null)
                                {
                                    OnListChanged(new NetworkListEvent<T>
                                    {
                                        Type = eventType,
                                        Index = m_List.Count - 1,
                                        Value = m_List[m_List.Count - 1]
                                    });
                                }

                                if (keepDirtyDelta)
                                {
                                    m_DirtyEvents.Add(new NetworkListEvent<T>()
                                    {
                                        Type = eventType,
                                        Index = m_List.Count - 1,
                                        Value = m_List[m_List.Count - 1]
                                    });
                                }
                            }
                            break;
                        case NetworkListEvent<T>.EventType.Insert:
                            {
                                int index = reader.ReadInt32Packed();
                                m_List.Insert(index, (T)reader.ReadObjectPacked(typeof(T))); //BOX

                                if (OnListChanged != null)
                                {
                                    OnListChanged(new NetworkListEvent<T>
                                    {
                                        Type = eventType,
                                        Index = index,
                                        Value = m_List[index]
                                    });
                                }

                                if (keepDirtyDelta)
                                {
                                    m_DirtyEvents.Add(new NetworkListEvent<T>()
                                    {
                                        Type = eventType,
                                        Index = index,
                                        Value = m_List[index]
                                    });
                                }
                            }
                            break;
                        case NetworkListEvent<T>.EventType.Remove:
                            {
                                var value = (T)reader.ReadObjectPacked(typeof(T)); //BOX
                                int index = m_List.IndexOf(value);
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

                                if (keepDirtyDelta)
                                {
                                    m_DirtyEvents.Add(new NetworkListEvent<T>()
                                    {
                                        Type = eventType,
                                        Index = index,
                                        Value = value
                                    });
                                }
                            }
                            break;
                        case NetworkListEvent<T>.EventType.RemoveAt:
                            {
                                int index = reader.ReadInt32Packed();
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

                                if (keepDirtyDelta)
                                {
                                    m_DirtyEvents.Add(new NetworkListEvent<T>()
                                    {
                                        Type = eventType,
                                        Index = index,
                                        Value = value
                                    });
                                }
                            }
                            break;
                        case NetworkListEvent<T>.EventType.Value:
                            {
                                int index = reader.ReadInt32Packed();
                                var value = (T)reader.ReadObjectPacked(typeof(T)); //BOX
                                if (index < m_List.Count)
                                {
                                    m_List[index] = value;
                                }

                                if (OnListChanged != null)
                                {
                                    OnListChanged(new NetworkListEvent<T>
                                    {
                                        Type = eventType,
                                        Index = index,
                                        Value = value
                                    });
                                }

                                if (keepDirtyDelta)
                                {
                                    m_DirtyEvents.Add(new NetworkListEvent<T>()
                                    {
                                        Type = eventType,
                                        Index = index,
                                        Value = value
                                    });
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

                                if (keepDirtyDelta)
                                {
                                    m_DirtyEvents.Add(new NetworkListEvent<T>()
                                    {
                                        Type = eventType
                                    });
                                }
                            }
                            break;
                    }
                }
            }
        }

        /// <inheritdoc />
        public void SetNetworkBehaviour(NetworkBehaviour behaviour)
        {
            m_NetworkBehaviour = behaviour;
        }

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            return m_List.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)m_List).GetEnumerator();
        }

        /// <inheritdoc />
        public void Add(T item)
        {
            EnsureInitialized();

            if (m_NetworkBehaviour.NetworkManager.IsServer)
            {
                m_List.Add(item);
            }

            var listEvent = new NetworkListEvent<T>()
            {
                Type = NetworkListEvent<T>.EventType.Add,
                Value = item,
                Index = m_List.Count - 1
            };

            HandleAddListEvent(listEvent);
        }

        /// <inheritdoc />
        public void Clear()
        {
            EnsureInitialized();

            if (m_NetworkBehaviour.NetworkManager.IsServer)
            {
                m_List.Clear();
            }

            var listEvent = new NetworkListEvent<T>()
            {
                Type = NetworkListEvent<T>.EventType.Clear
            };

            HandleAddListEvent(listEvent);
        }

        /// <inheritdoc />
        public bool Contains(T item)
        {
            return m_List.Contains(item);
        }

        /// <inheritdoc />
        public void CopyTo(T[] array, int arrayIndex)
        {
            m_List.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc />
        public bool Remove(T item)
        {
            EnsureInitialized();

            if (m_NetworkBehaviour.NetworkManager.IsServer)
            {
                m_List.Remove(item);
            }

            var listEvent = new NetworkListEvent<T>()
            {
                Type = NetworkListEvent<T>.EventType.Remove,
                Value = item
            };

            HandleAddListEvent(listEvent);
            return true;
        }

        /// <inheritdoc />
        public int Count => m_List.Count;

        /// <inheritdoc />
        public bool IsReadOnly => m_List.IsReadOnly;

        /// <inheritdoc />
        public int IndexOf(T item)
        {
            return m_List.IndexOf(item);
        }

        /// <inheritdoc />
        public void Insert(int index, T item)
        {
            EnsureInitialized();

            if (m_NetworkBehaviour.NetworkManager.IsServer)
            {
                m_List.Insert(index, item);
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
            EnsureInitialized();

            if (m_NetworkBehaviour.NetworkManager.IsServer)
            {
                m_List.RemoveAt(index);
            }

            var listEvent = new NetworkListEvent<T>()
            {
                Type = NetworkListEvent<T>.EventType.RemoveAt,
                Index = index
            };

            HandleAddListEvent(listEvent);
        }


        /// <inheritdoc />
        public T this[int index]
        {
            get => m_List[index];
            set
            {
                EnsureInitialized();

                if (m_NetworkBehaviour.NetworkManager.IsServer)
                {
                    m_List[index] = value;
                }

                var listEvent = new NetworkListEvent<T>()
                {
                    Type = NetworkListEvent<T>.EventType.Value,
                    Index = index,
                    Value = value
                };

                HandleAddListEvent(listEvent);
            }
        }

        private void HandleAddListEvent(NetworkListEvent<T> listEvent)
        {
            if (m_NetworkBehaviour.NetworkManager.IsServer)
            {
                if (m_NetworkBehaviour.NetworkManager.ConnectedClients.Count > 0)
                {
                    m_DirtyEvents.Add(listEvent);
                }

                OnListChanged?.Invoke(listEvent);
            }
            else
            {
                m_DirtyEvents.Add(listEvent);
            }
        }

        public int LastModifiedTick
        {
            get
            {
                // todo: implement proper network tick for NetworkList
                return NetworkTickSystem.NoTick;
            }
        }

        private void EnsureInitialized()
        {
            if (m_NetworkBehaviour == null)
            {
                throw new InvalidOperationException("Cannot access " + nameof(NetworkList<T>) + " before it's initialized");
            }
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
        public enum EventType
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
            Clear
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
        /// the index changed, added or removed if available
        /// </summary>
        public int Index;
    }
}
