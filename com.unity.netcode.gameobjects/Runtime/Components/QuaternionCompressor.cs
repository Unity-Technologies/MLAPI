using System.Runtime.CompilerServices;
using UnityEngine;

namespace Unity.Netcode
{
    /// <summary>
    /// A Smallest Three Quaternion Compressor Implementation
    /// </summary>
    /// <remarks>
    /// Explanation of why "The smallest three":
    /// Since a normalized Quaternion's unit value is 1.0f:
    /// x*x + y*y + z*z + w*w = M*M (where M is the magnitude of the vector)
    /// If w was the largest value and the quaternion is normalized:
    /// M = 1.0f (which M * M would still yield 1.0f)
    /// w*w = M*M - (x*x + y*y + z*z) or Mathf.Sqrt(1.0f - (x*x + y*y + z*z))
    /// w = Math.Sqrt(1.0f - (x*x + y*y + z*z))
    /// Using the largest number avoids potential loss of precision in the smallest three values.
    /// </remarks>
    public static class QuaternionCompressor
    {
        private const ushort k_PrecisionMask = (1 << 9) - 1;

        // Square root of 2 over 2 (Mathf.Sqrt(2.0f) / 2.0f == 1.0f / Mathf.Sqrt(2.0f))
        // This provides encoding the smallest three components into a (+/-) Mathf.Sqrt(2.0f) / 2.0f range
        private const float k_SqrtTwoOverTwoEncoding = 0.70710678118654752440084436210485f;

        // We can further improve the encoding compression by dividing k_SqrtTwoOverTwo into 1.0f and multiplying that
        // by the precision mask (minor reduction of runtime calculations)
        private const float k_CompressionEcodingMask = (1.0f / k_SqrtTwoOverTwoEncoding) * k_PrecisionMask;

        // Used to shift the negative bit to the 10th bit position when compressing and encoding
        private const ushort k_ShiftNegativeBit = 9;

        // We can do the same for our decoding and decompression by dividing k_PrecisionMask into 1.0 and multiplying
        // that by k_SqrtTwoOverTwo (minor reduction of runtime calculations)
        private const float k_DcompressionDecodingMask = (1.0f / k_PrecisionMask) * k_SqrtTwoOverTwoEncoding;

        // The sign bit position (10th bit) used when decompressing and decoding
        private const ushort k_NegShortBit = 0x200;

        // Negative bit set values
        private const ushort k_True = 1;
        private const ushort k_False = 0;

        // Used to store the absolute value of the 4 quaternion elements
        private static Quaternion s_QuatAbsValues = Quaternion.identity;

        /// <summary>
        /// Compresses a Quaternion into an unsigned integer
        /// </summary>
        /// <param name="quaternion">the <see cref="Quaternion"/> to be compressed</param>
        /// <returns>the <see cref="Quaternion"/> compressed as an unsigned integer</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint CompressQuaternion(ref Quaternion quaternion)
        {
            // Store off the absolute value for each Quaternion element
            s_QuatAbsValues[0] = Mathf.Abs(quaternion[0]);
            s_QuatAbsValues[1] = Mathf.Abs(quaternion[1]);
            s_QuatAbsValues[2] = Mathf.Abs(quaternion[2]);
            s_QuatAbsValues[3] = Mathf.Abs(quaternion[3]);

            // Get the largest element value of the quaternion to know what the remaining "Smallest Three" values are
            var quatMax = Mathf.Max(s_QuatAbsValues[0], s_QuatAbsValues[1], s_QuatAbsValues[2], s_QuatAbsValues[3]);

            // Find the index of the largest element so we can skip that element while compressing and decompressing
            var indexToSkip = (ushort)(s_QuatAbsValues[0] == quatMax ? 0 : s_QuatAbsValues[1] == quatMax ? 1 : s_QuatAbsValues[2] == quatMax ? 2 : 3);

            // Get the sign of the largest element which is all that is needed when calculating the sum of squares of a normalized quaternion.

            var quatMaxSign = (quaternion[indexToSkip] < 0 ? k_True : k_False);

            // Start with the index to skip which will be shifted to the highest two bits
            var compressed = (uint)indexToSkip;

            // Step 1: Start with the first element
            var currentIndex = 0;

            // Step 2: If we are on the index to skip preserve the current compressed value, otherwise proceed to step 3 and 4
            // Step 3: Get the sign of the element we are processing. If it is the not the same as the largest value's sign bit then we set the bit
            // Step 4: Get the compressed and encoded value by multiplying the absolute value of the current element by k_CompressionEcodingMask and round that result up
            compressed = currentIndex != indexToSkip ? (compressed << 10) | (uint)((quaternion[currentIndex] < 0 ? k_True : k_False) != quatMaxSign ? k_True : k_False) << k_ShiftNegativeBit | (ushort)Mathf.Round(k_CompressionEcodingMask * s_QuatAbsValues[currentIndex]) : compressed;
            currentIndex++;
            // Repeat the last 3 steps for the remaining elements
            compressed = currentIndex != indexToSkip ? (compressed << 10) | (uint)((quaternion[currentIndex] < 0 ? k_True : k_False) != quatMaxSign ? k_True : k_False) << k_ShiftNegativeBit | (ushort)Mathf.Round(k_CompressionEcodingMask * s_QuatAbsValues[currentIndex]) : compressed;
            currentIndex++;
            compressed = currentIndex != indexToSkip ? (compressed << 10) | (uint)((quaternion[currentIndex] < 0 ? k_True : k_False) != quatMaxSign ? k_True : k_False) << k_ShiftNegativeBit | (ushort)Mathf.Round(k_CompressionEcodingMask * s_QuatAbsValues[currentIndex]) : compressed;
            currentIndex++;
            compressed = currentIndex != indexToSkip ? (compressed << 10) | (uint)((quaternion[currentIndex] < 0 ? k_True : k_False) != quatMaxSign ? k_True : k_False) << k_ShiftNegativeBit | (ushort)Mathf.Round(k_CompressionEcodingMask * s_QuatAbsValues[currentIndex]) : compressed;

            // Return the compress quaternion
            return compressed;
        }

        /// <summary>
        /// Decompress a compressed quaternion
        /// </summary>
        /// <param name="quaternion">quaternion to store the decompressed values within</param>
        /// <param name="compressed">the compressed quaternion</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DecompressQuaternion(ref Quaternion quaternion, uint compressed)
        {
            // Get the last two bits for the index to skip (0-3)
            var indexToSkip = (int)(compressed >> 30);

            // Reverse out the values while skipping over the largest value index
            var sumOfSquaredMagnitudes = 0.0f;
            for (int i = 3; i >= 0; --i)
            {
                if (i == indexToSkip)
                {
                    continue;
                }
                // Check the negative bit and multiply that result with the decompressed and decoded value
                quaternion[i] = ((compressed & k_NegShortBit) > 0 ? -1.0f : 1.0f) * ((compressed & k_PrecisionMask) * k_DcompressionDecodingMask);
                sumOfSquaredMagnitudes += quaternion[i] * quaternion[i];
                compressed = compressed >> 10;
            }
            // Since a normalized quaternion's magnitude is 1.0f, we subtract the sum of the squared smallest three from the unit value and take
            // the square root of the difference to find the final largest value
            quaternion[indexToSkip] = Mathf.Sqrt(1.0f - sumOfSquaredMagnitudes);
        }
    }
}
