using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace Unity.Netcode
{

    internal static class SerializationTools
    {
        public delegate void WriteDelegate<T>(FastBufferWriter writer, ref T value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithSize<T>(WriteDelegate<T> writeMethod, FastBufferWriter writer, ref T value)
        {
            var writePos = writer.Position;
            // Note: This value can't be packed because we don't know how large it will be in advance
            // we reserve space for it, then write the data, then come back and fill in the space
            // to pack here, we'd have to write data to a temporary buffer and copy it in - which
            // isn't worth possibly saving one byte if and only if the data is less than 63 bytes long...
            // The way we do packing, any value > 63 in a ushort will use the full 2 bytes to represent.
            writer.WriteValueSafe((ushort)0);
            var startPos = writer.Position;
            writeMethod(writer, ref value);
            var size = writer.Position - startPos;
            writer.Seek(writePos);
            writer.WriteValueSafe((ushort)size);
            writer.Seek(startPos + size);
        }

        public delegate void ReadDelegate<T>(FastBufferReader writer, ref T value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadWithSize<T>(ReadDelegate<T> readMethod, FastBufferReader reader, ref T value)
        {
            reader.ReadValueSafe(out ushort _);
            readMethod(reader, ref value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteType(FastBufferWriter writer, NetworkVariableType type) => writer.WriteValueSafe(type);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadType<T>(FastBufferReader reader, INetworkVariableSerializer<T> serializer)
        {
            reader.ReadValueSafe(out NetworkVariableType type);
            if (type != serializer.Type)
            {
                throw new SerializationException();
            }
        }


    }

}
