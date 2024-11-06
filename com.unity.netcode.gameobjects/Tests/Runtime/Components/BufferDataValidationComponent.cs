using System.Collections.Generic;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// Used in conjunction with the RpcQueueTest to validate from 1 byte to (n) MaximumBufferSize
    /// - Sending and Receiving a continually growing buffer up to (MaximumBufferSize)
    /// - Default maximum buffer size is 1MB
    /// </summary>
    internal class BufferDataValidationComponent : NetworkBehaviour
    {
        /// <summary>
        /// Allows the external RPCQueueTest to begin testing or stop it
        /// </summary>
        public bool EnableTesting;

        /// <summary>
        /// The maximum size of the buffer to send
        /// </summary>
        public int MaximumBufferSize = 1 << 15;

        /// <summary>
        /// The rate at which the buffer size increases until it reaches MaximumBufferSize
        /// (the default starting buffer size is 1 bytes)
        /// </summary>
        public int BufferSizeStart = 1;

        /// <summary>
        /// Is checked to determine if the test exited because it failed
        /// </summary>
        public bool TestFailed { get; internal set; }

        private bool m_WaitForValidation;
        private int m_CurrentBufferSize;

        private List<byte> m_SendBuffer;
        private List<byte> m_PreCalculatedBufferValues;

        // Start is called before the first frame update
        private void Start()
        {
            m_WaitForValidation = false;
            m_CurrentBufferSize = BufferSizeStart;
            m_SendBuffer = new List<byte>(MaximumBufferSize + 1);
            m_PreCalculatedBufferValues = new List<byte>(MaximumBufferSize + 1);
            while (m_PreCalculatedBufferValues.Count <= MaximumBufferSize)
            {
                m_PreCalculatedBufferValues.Add((byte)Random.Range(0, 255));
            }
        }

        /// <summary>
        /// Returns back whether the test has completed the total number of iterations
        /// </summary>
        /// <returns></returns>
        public bool IsTestComplete()
        {
            if (m_CurrentBufferSize > MaximumBufferSize || TestFailed)
            {
                return true;
            }
            return false;
        }

        // Update is called once per frame
        private void Update()
        {
            if (NetworkManager.Singleton.IsListening && EnableTesting && !IsTestComplete() && !m_WaitForValidation)
            {
                m_SendBuffer.Clear();
                //Keep the current contents of the bufffer and fill the buffer with the delta difference of the buffer's current size and new size from the m_PreCalculatedBufferValues
                m_SendBuffer.AddRange(m_PreCalculatedBufferValues.GetRange(0, m_CurrentBufferSize));

                //Make sure we don't do anything until we finish validating buffer
                m_WaitForValidation = true;

                //Send the buffer
                SendBufferServerRpc(m_SendBuffer.ToArray());
            }
        }

        /// <summary>
        /// Server side RPC for testing
        /// </summary>
        /// <param name="parameters">server rpc parameters</param>
        [ServerRpc]
        private void SendBufferServerRpc(byte[] buffer)
        {
            TestFailed = !NetworkManagerHelper.BuffersMatch(0, buffer.Length, buffer, m_SendBuffer.ToArray());
            if (!TestFailed)
            {
                Debug.Log($"Tested buffer size of {m_SendBuffer.Count} -- OK");
            }

            if (m_CurrentBufferSize == MaximumBufferSize)
            {
                m_CurrentBufferSize++;
            }
            else
            {
                //Increasse buffer size
                m_CurrentBufferSize = m_CurrentBufferSize << 1;
            }

            m_WaitForValidation = false;
        }
    }
}
