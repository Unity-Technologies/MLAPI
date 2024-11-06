using System;
using System.Collections.Generic;

namespace Unity.Netcode.TestHelpers.Runtime
{
    /// <summary>
    /// Will automatically register for the NetworkVariable OnValueChanged
    /// delegate handler. It then will expose that single delegate invocation
    /// to anything that registers for this NetworkVariableHelper's instance's OnValueChanged event.
    /// This allows us to register any NetworkVariable type as well as there are basically two "types of types":
    /// <list type="bullet">
    /// <item>IEquatable&lt;T&gt;</item>
    /// <item>ValueType</item>
    /// </list>
    /// From both we can then at least determine if the value indeed changed
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class NetworkVariableHelper<T> : NetworkVariableBaseHelper
    {
        private readonly NetworkVariable<T> m_NetworkVariable;
        public delegate void OnMyValueChangedDelegateHandler(T previous, T next);
        public event OnMyValueChangedDelegateHandler OnValueChanged;

        /// <summary>
        /// IEquatable&lt;T&gt; Equals Check
        /// </summary>
        private void CheckVariableChanged(IEquatable<T> previous, IEquatable<T> next)
        {
            if (!previous.Equals(next))
            {
                ValueChanged();
            }
        }

        /// <summary>
        /// ValueType Equals Check
        /// </summary>
        private void CheckVariableChanged(ValueType previous, ValueType next)
        {
            if (!previous.Equals(next))
            {
                ValueChanged();
            }
        }

        /// <summary>
        /// INetworkVariable's OnVariableChanged delegate callback
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="next"></param>
        private void OnVariableChanged(T previous, T next)
        {
            if (previous is ValueType testValueType)
            {
                CheckVariableChanged(previous as ValueType, next as ValueType);
            }
            else
            {
                CheckVariableChanged(previous as IEquatable<T>, next as IEquatable<T>);
            }

            OnValueChanged?.Invoke(previous, next);
        }

        public NetworkVariableHelper(NetworkVariableBase networkVariable) : base(networkVariable)
        {
            m_NetworkVariable = networkVariable as NetworkVariable<T>;
            m_NetworkVariable.OnValueChanged = OnVariableChanged;
        }
    }

    /// <summary>
    /// The BaseNetworkVariableHelper keeps track of:
    /// The number of instances and associates the instance with the NetworkVariable
    /// The number of times a specific NetworkVariable instance had its value changed (i.e. !Equal)
    /// Note: This could be expanded for future tests focuses around NetworkVariables
    /// </summary>
    public class NetworkVariableBaseHelper
    {
        private static Dictionary<NetworkVariableBaseHelper, NetworkVariableBase> s_Instances;
        private static Dictionary<NetworkVariableBase, int> s_InstanceChangedCount;

        /// <summary>
        /// Returns the total number of registered INetworkVariables
        /// </summary>
        public static int InstanceCount
        {
            get
            {
                if (s_Instances != null)
                {
                    return s_Instances.Count;
                }
                return 0;
            }
        }

        /// <summary>
        /// Returns total number of changes that occurred for all registered INetworkVariables
        /// </summary>
        public static int VarChangedCount
        {
            get
            {
                if (s_InstanceChangedCount != null)
                {
                    var changeCount = 0;
                    foreach (var keyPair in s_InstanceChangedCount)
                    {
                        changeCount += keyPair.Value;
                    }
                    return changeCount;
                }
                return 0;
            }
        }

        /// <summary>
        /// Called by the child class NetworkVariableHelper when a value changed
        /// </summary>
        protected void ValueChanged()
        {
            if (s_Instances.ContainsKey(this))
            {
                if (s_InstanceChangedCount.ContainsKey(s_Instances[this]))
                {
                    s_InstanceChangedCount[s_Instances[this]]++;
                }
            }
        }

        public NetworkVariableBaseHelper(NetworkVariableBase networkVariable)
        {
            if (s_Instances == null)
            {
                s_Instances = new Dictionary<NetworkVariableBaseHelper, NetworkVariableBase>();
            }

            if (s_InstanceChangedCount == null)
            {
                s_InstanceChangedCount = new Dictionary<NetworkVariableBase, int>();
            }

            // Register new instance and associated INetworkVariable
            if (!s_Instances.ContainsKey(this))
            {
                s_Instances.Add(this, networkVariable);
                if (!s_InstanceChangedCount.ContainsKey(networkVariable))
                {
                    s_InstanceChangedCount.Add(networkVariable, 0);
                }
            }
        }
    }
}
