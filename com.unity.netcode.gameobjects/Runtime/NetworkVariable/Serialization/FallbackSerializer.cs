using System;
using Unity.Collections;

namespace Unity.Netcode
{
    /// <summary>
    /// This class is instantiated for types that we can't determine ahead of time are serializable - types
    /// that don't meet any of the constraints for methods that are available on FastBufferReader and
    /// FastBufferWriter. These types may or may not be serializable through extension methods. To ensure
    /// the user has time to pass in the delegates to UserNetworkVariableSerialization, the existence
    /// of user serialization isn't checked until it's used, so if no serialization is provided, this
    /// will throw an exception when an object containing the relevant NetworkVariable is spawned.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class FallbackSerializer<T> : INetworkVariableSerializer<T>
    {
        public NetworkVariableType Type => NetworkVariableType.Unknown;
        public bool IsDistributedAuthorityOptimized => true;

        private void ThrowArgumentError()
        {
            throw new ArgumentException($"Serialization has not been generated for type {typeof(T).FullName}. This can be addressed by adding a [{nameof(GenerateSerializationForGenericParameterAttribute)}] to your generic class that serializes this value (if you are using one), adding [{nameof(GenerateSerializationForTypeAttribute)}(typeof({typeof(T).FullName})] to the class or method that is attempting to serialize it, or creating a field on a {nameof(NetworkBehaviour)} of type {nameof(NetworkVariable<T>)}. If this error continues to appear after doing one of those things and this is a type you can change, then either implement {nameof(INetworkSerializable)} or mark it as serializable by memcpy by adding {nameof(INetworkSerializeByMemcpy)} to its interface list to enable automatic serialization generation. If not, assign serialization code to {nameof(UserNetworkVariableSerialization<T>)}.{nameof(UserNetworkVariableSerialization<T>.WriteValue)}, {nameof(UserNetworkVariableSerialization<T>)}.{nameof(UserNetworkVariableSerialization<T>.ReadValue)}, and {nameof(UserNetworkVariableSerialization<T>)}.{nameof(UserNetworkVariableSerialization<T>.DuplicateValue)}, or if it's serializable by memcpy (contains no pointers), wrap it in {typeof(ForceNetworkSerializeByMemcpy<>).Name}.");
        }

        public void Write(FastBufferWriter writer, ref T value)
        {
            if (UserNetworkVariableSerialization<T>.ReadValue == null || UserNetworkVariableSerialization<T>.WriteValue == null || UserNetworkVariableSerialization<T>.DuplicateValue == null)
            {
                ThrowArgumentError();
            }
            UserNetworkVariableSerialization<T>.WriteValue(writer, value);
        }
        public void Read(FastBufferReader reader, ref T value)
        {
            if (UserNetworkVariableSerialization<T>.ReadValue == null || UserNetworkVariableSerialization<T>.WriteValue == null || UserNetworkVariableSerialization<T>.DuplicateValue == null)
            {
                ThrowArgumentError();
            }
            UserNetworkVariableSerialization<T>.ReadValue(reader, out value);
        }

        public void WriteDelta(FastBufferWriter writer, ref T value, ref T previousValue)
        {
            if (UserNetworkVariableSerialization<T>.ReadValue == null || UserNetworkVariableSerialization<T>.WriteValue == null || UserNetworkVariableSerialization<T>.DuplicateValue == null)
            {
                ThrowArgumentError();
            }

            if (UserNetworkVariableSerialization<T>.WriteDelta == null || UserNetworkVariableSerialization<T>.ReadDelta == null)
            {
                UserNetworkVariableSerialization<T>.WriteValue(writer, value);
                return;
            }
            UserNetworkVariableSerialization<T>.WriteDelta(writer, value, previousValue);
        }

        public void ReadDelta(FastBufferReader reader, ref T value)
        {
            if (UserNetworkVariableSerialization<T>.ReadValue == null || UserNetworkVariableSerialization<T>.WriteValue == null || UserNetworkVariableSerialization<T>.DuplicateValue == null)
            {
                ThrowArgumentError();
            }

            if (UserNetworkVariableSerialization<T>.WriteDelta == null || UserNetworkVariableSerialization<T>.ReadDelta == null)
            {
                UserNetworkVariableSerialization<T>.ReadValue(reader, out value);
                return;
            }
            UserNetworkVariableSerialization<T>.ReadDelta(reader, ref value);
        }

        void INetworkVariableSerializer<T>.ReadWithAllocator(FastBufferReader reader, out T value, Allocator allocator)
        {
            throw new NotImplementedException();
        }

        public void Duplicate(in T value, ref T duplicatedValue)
        {
            if (UserNetworkVariableSerialization<T>.ReadValue == null || UserNetworkVariableSerialization<T>.WriteValue == null || UserNetworkVariableSerialization<T>.DuplicateValue == null)
            {
                ThrowArgumentError();
            }
            UserNetworkVariableSerialization<T>.DuplicateValue(value, ref duplicatedValue);
        }

        public void WriteDistributedAuthority(FastBufferWriter writer, ref T value) => ThrowArgumentError();
        public void ReadDistributedAuthority(FastBufferReader reader, ref T value) => ThrowArgumentError();
        public void WriteDeltaDistributedAuthority(FastBufferWriter writer, ref T value, ref T previousValue) => ThrowArgumentError();
        public void ReadDeltaDistributedAuthority(FastBufferReader reader, ref T value) => ThrowArgumentError();
    }

    // RuntimeAccessModifiersILPP will make this `public`
    // This is just pass-through to NetworkVariableSerialization<T> but is here because I could not get ILPP
    // to generate code that would successfully call Type<T>.Method(T), but it has no problem calling Type.Method<T>(T)
    internal class RpcFallbackSerialization
    {
        public static void Write<T>(FastBufferWriter writer, ref T value)
        {
            NetworkVariableSerialization<T>.Write(writer, ref value);
        }

        public static void Read<T>(FastBufferReader reader, ref T value)
        {
            NetworkVariableSerialization<T>.Read(reader, ref value);
        }
    }
}
