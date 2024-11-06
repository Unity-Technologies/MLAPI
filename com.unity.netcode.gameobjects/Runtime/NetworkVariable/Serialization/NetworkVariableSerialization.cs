using System;

namespace Unity.Netcode
{
    /// <summary>
    ///     Support methods for reading/writing NetworkVariables
    ///     Because there are multiple overloads of WriteValue/ReadValue based on different generic constraints,
    ///     but there's no way to achieve the same thing with a class, this sets up various read/write schemes
    ///     based on which constraints are met by `T` using reflection, which is done at module load time.
    /// </summary>
    /// <typeparam name="T">The type the associated NetworkVariable is templated on</typeparam>
    [Serializable]
    public static class NetworkVariableSerialization<T>
    {
        internal static INetworkVariableSerializer<T> Serializer = new FallbackSerializer<T>();

        internal static bool IsDistributedAuthority => NetworkManager.IsDistributedAuthority;

        /// <summary>
        /// A callback to check if two values are equal.
        /// </summary>
        public delegate bool EqualsDelegate(ref T a, ref T b);

        /// <summary>
        /// Uses the most efficient mechanism for a given type to determine if two values are equal.
        /// For types that implement <see cref="IEquatable{T}"/>, it will call the Equals() method.
        /// For unmanaged types, it will do a bytewise memory comparison.
        /// For other types, it will call the == operator.
        /// <br/>
        /// <br/>
        /// Note: If you are using this in a custom generic class, please make sure your class is
        /// decorated with <see cref="GenerateSerializationForGenericParameterAttribute"/> so that codegen can
        /// initialize the serialization mechanisms correctly. If your class is NOT
        /// generic, it is better to check their equality yourself.
        /// </summary>
        public static EqualsDelegate AreEqual { get; internal set; }
        /// <summary>
        ///     Serialize a value using the best-known serialization method for a generic value.
        ///     Will reliably serialize any value that is passed to it correctly with no boxing.
        ///     <br />
        ///     <br />
        ///     Note: If you are using this in a custom generic class, please make sure your class is
        ///     decorated with <see cref="GenerateSerializationForGenericParameterAttribute" /> so that codegen can
        ///     initialize the serialization mechanisms correctly. If your class is NOT
        ///     generic, it is better to use FastBufferWriter directly.
        ///     <br />
        ///     <br />
        ///     If the codegen is unable to determine a serializer for a type,
        ///     <see cref="UserNetworkVariableSerialization{T}" />.<see cref="UserNetworkVariableSerialization{T}.WriteValue" /> is called, which, by default,
        ///     will throw an exception, unless you have assigned a user serialization callback to it at runtime.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        public static void Write(FastBufferWriter writer, ref T value)
        {
            if (IsDistributedAuthority)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (!NetworkManager.DisableNotOptimizedSerializedType && !Serializer.IsDistributedAuthorityOptimized)
                {
                    NetworkManager.LogSerializedTypeNotOptimized<T>();
                }
#endif
                Serializer.WriteDistributedAuthority(writer, ref value);
            }
            else
            {
                Serializer.Write(writer, ref value);
            }
        }

        /// <summary>
        ///     Deserialize a value using the best-known serialization method for a generic value.
        ///     Will reliably deserialize any value that is passed to it correctly with no boxing.
        ///     For types whose deserialization can be determined by codegen (which is most types),
        ///     GC will only be incurred if the type is a managed type and the ref value passed in is `null`,
        ///     in which case a new value is created; otherwise, it will be deserialized in-place.
        ///     <br />
        ///     <br />
        ///     Note: If you are using this in a custom generic class, please make sure your class is
        ///     decorated with <see cref="GenerateSerializationForGenericParameterAttribute" /> so that codegen can
        ///     initialize the serialization mechanisms correctly. If your class is NOT
        ///     generic, it is better to use FastBufferReader directly.
        ///     <br />
        ///     <br />
        ///     If the codegen is unable to determine a serializer for a type,
        ///     <see cref="UserNetworkVariableSerialization{T}" />.<see cref="UserNetworkVariableSerialization{T}.ReadValue" /> is called, which, by default,
        ///     will throw an exception, unless you have assigned a user deserialization callback to it at runtime.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="value"></param>
        public static void Read(FastBufferReader reader, ref T value)
        {
            if (IsDistributedAuthority)
            {
                Serializer.ReadDistributedAuthority(reader, ref value);
            }
            else
            {
                Serializer.Read(reader, ref value);
            }
        }

        /// <summary>
        ///     Serialize a value using the best-known serialization method for a generic value.
        ///     Will reliably serialize any value that is passed to it correctly with no boxing.
        ///     <br />
        ///     <br />
        ///     Note: If you are using this in a custom generic class, please make sure your class is
        ///     decorated with <see cref="GenerateSerializationForGenericParameterAttribute" /> so that codegen can
        ///     initialize the serialization mechanisms correctly. If your class is NOT
        ///     generic, it is better to use FastBufferWriter directly.
        ///     <br />
        ///     <br />
        ///     If the codegen is unable to determine a serializer for a type,
        ///     <see cref="UserNetworkVariableSerialization{T}" />.<see cref="UserNetworkVariableSerialization{T}.WriteValue" /> is called, which, by default,
        ///     will throw an exception, unless you have assigned a user serialization callback to it at runtime.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        public static void WriteDelta(FastBufferWriter writer, ref T value, ref T previousValue)
        {
            if (IsDistributedAuthority)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (!NetworkManager.DisableNotOptimizedSerializedType && !Serializer.IsDistributedAuthorityOptimized)
                {
                    NetworkManager.LogSerializedTypeNotOptimized<T>();
                }
#endif
                Serializer.WriteDeltaDistributedAuthority(writer, ref value, ref previousValue);
            }
            else
            {
                Serializer.WriteDelta(writer, ref value, ref previousValue);
            }
        }

        /// <summary>
        ///     Deserialize a value using the best-known serialization method for a generic value.
        ///     Will reliably deserialize any value that is passed to it correctly with no boxing.
        ///     For types whose deserialization can be determined by codegen (which is most types),
        ///     GC will only be incurred if the type is a managed type and the ref value passed in is `null`,
        ///     in which case a new value is created; otherwise, it will be deserialized in-place.
        ///     <br />
        ///     <br />
        ///     Note: If you are using this in a custom generic class, please make sure your class is
        ///     decorated with <see cref="GenerateSerializationForGenericParameterAttribute" /> so that codegen can
        ///     initialize the serialization mechanisms correctly. If your class is NOT
        ///     generic, it is better to use FastBufferReader directly.
        ///     <br />
        ///     <br />
        ///     If the codegen is unable to determine a serializer for a type,
        ///     <see cref="UserNetworkVariableSerialization{T}" />.<see cref="UserNetworkVariableSerialization{T}.ReadValue" /> is called, which, by default,
        ///     will throw an exception, unless you have assigned a user deserialization callback to it at runtime.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="value"></param>
        public static void ReadDelta(FastBufferReader reader, ref T value)
        {
            if (IsDistributedAuthority)
            {
                Serializer.ReadDeltaDistributedAuthority(reader, ref value);
            }
            else
            {
                Serializer.ReadDelta(reader, ref value);
            }
        }

        /// <summary>
        ///     Duplicates a value using the most efficient means of creating a complete copy.
        ///     For most types this is a simple assignment or memcpy.
        ///     For managed types, this is will serialize and then deserialize the value to ensure
        ///     a correct copy.
        ///     <br />
        ///     <br />
        ///     Note: If you are using this in a custom generic class, please make sure your class is
        ///     decorated with <see cref="GenerateSerializationForGenericParameterAttribute" /> so that codegen can
        ///     initialize the serialization mechanisms correctly. If your class is NOT
        ///     generic, it is better to duplicate it directly.
        ///     <br />
        ///     <br />
        ///     If the codegen is unable to determine a serializer for a type,
        ///     <see cref="UserNetworkVariableSerialization{T}" />.<see cref="UserNetworkVariableSerialization{T}.DuplicateValue" /> is called, which, by default,
        ///     will throw an exception, unless you have assigned a user duplication callback to it at runtime.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="duplicatedValue"></param>
        public static void Duplicate(in T value, ref T duplicatedValue)
        {
            Serializer.Duplicate(value, ref duplicatedValue);
        }
    }
}
