using UnityEngine;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// This provides coverage for all of the predefined NetworkVariable types
    /// The initial goal is for generalized full coverage of NetworkVariables:
    /// Covers all of the various constructor calls (i.e. various parameters or no parameters)
    /// Covers the local NetworkVariable's OnValueChanged functionality (i.e. when a specific type changes do we get a notification?)
    /// This was built as a NetworkBehaviour for further client-server unit testing patterns when this capability is available.
    /// </summary>
    internal class NetworkVariableTestComponent : NetworkBehaviour
    {
        private NetworkVariableBool m_NetworkVariableBool;
        private NetworkVariableByte m_NetworkVariableByte;
        private NetworkVariableColor m_NetworkVariableColor;
        private NetworkVariableColor32 m_NetworkVariableColor32;
        private NetworkVariableDouble m_NetworkVariableDouble;
        private NetworkVariableFloat m_NetworkVariableFloat;
        private NetworkVariableInt m_NetworkVariableInt;
        private NetworkVariableLong m_NetworkVariableLong;
        private NetworkVariableSByte m_NetworkVariableSByte;
        private NetworkVariableQuaternion m_NetworkVariableQuaternion;
        private NetworkVariableShort m_NetworkVariableShort;
        private NetworkVariableString m_NetworkVariableString;
        private NetworkVariableVector4 m_NetworkVariableVector4;
        private NetworkVariableVector3 m_NetworkVariableVector3;
        private NetworkVariableVector2 m_NetworkVariableVector2;
        private NetworkVariableRay m_NetworkVariableRay;
        private NetworkVariableULong m_NetworkVariableULong;
        private NetworkVariableUInt m_NetworkVariableUInt;
        private NetworkVariableUShort m_NetworkVariableUShort;


        public NetworkVariableHelper<bool> Bool_Var;
        public NetworkVariableHelper<byte> Byte_Var;
        public NetworkVariableHelper<Color> Color_Var;
        public NetworkVariableHelper<Color32> Color32_Var;
        public NetworkVariableHelper<double> Double_Var;
        public NetworkVariableHelper<float> Float_Var;
        public NetworkVariableHelper<int> Int_Var;
        public NetworkVariableHelper<long> Long_Var;
        public NetworkVariableHelper<sbyte> Sbyte_Var;
        public NetworkVariableHelper<Quaternion> Quaternion_Var;
        public NetworkVariableHelper<short> Short_Var;
        public NetworkVariableHelper<string> String_Var;
        public NetworkVariableHelper<Vector4> Vector4_Var;
        public NetworkVariableHelper<Vector3> Vector3_Var;
        public NetworkVariableHelper<Vector2> Vector2_Var;
        public NetworkVariableHelper<Ray> Ray_Var;
        public NetworkVariableHelper<ulong> Ulong_Var;
        public NetworkVariableHelper<uint> Uint_Var;
        public NetworkVariableHelper<ushort> Ushort_Var;


        public bool EnableTesting;
        private bool m_Initialized;
        private bool m_FinishedTests;
        private bool m_ChangesAppliedToNetworkVariables;

        private float m_WaitForChangesTimeout;

        // Start is called before the first frame update
        private void InitializeTest()
        {
            // Generic Constructor Test Coverage
            m_NetworkVariableBool = new NetworkVariableBool();
            m_NetworkVariableByte = new NetworkVariableByte();
            m_NetworkVariableColor = new NetworkVariableColor();
            m_NetworkVariableColor32 = new NetworkVariableColor32();
            m_NetworkVariableDouble = new NetworkVariableDouble();
            m_NetworkVariableFloat = new NetworkVariableFloat();
            m_NetworkVariableInt = new NetworkVariableInt();
            m_NetworkVariableLong = new NetworkVariableLong();
            m_NetworkVariableSByte = new NetworkVariableSByte();
            m_NetworkVariableQuaternion = new NetworkVariableQuaternion();
            m_NetworkVariableShort = new NetworkVariableShort();
            m_NetworkVariableString = new NetworkVariableString();
            m_NetworkVariableVector4 = new NetworkVariableVector4();
            m_NetworkVariableVector3 = new NetworkVariableVector3();
            m_NetworkVariableVector2 = new NetworkVariableVector2();
            m_NetworkVariableRay = new NetworkVariableRay();
            m_NetworkVariableULong = new NetworkVariableULong();
            m_NetworkVariableUInt = new NetworkVariableUInt();
            m_NetworkVariableUShort = new NetworkVariableUShort();


            // NetworkVariable Value Type Constructor Test Coverage
            m_NetworkVariableBool = new NetworkVariableBool(true);
            m_NetworkVariableByte = new NetworkVariableByte(0);
            m_NetworkVariableColor = new NetworkVariableColor(new Color(1, 1, 1, 1));
            m_NetworkVariableColor32 = new NetworkVariableColor32(new Color32(1, 1, 1, 1));
            m_NetworkVariableDouble = new NetworkVariableDouble(1.0);
            m_NetworkVariableFloat = new NetworkVariableFloat(1.0f);
            m_NetworkVariableInt = new NetworkVariableInt(1);
            m_NetworkVariableLong = new NetworkVariableLong(1);
            m_NetworkVariableSByte = new NetworkVariableSByte(0);
            m_NetworkVariableQuaternion = new NetworkVariableQuaternion(Quaternion.identity);
            m_NetworkVariableShort = new NetworkVariableShort(256);
            m_NetworkVariableString = new NetworkVariableString("My String Value");
            m_NetworkVariableVector4 = new NetworkVariableVector4(new Vector4(1, 1, 1, 1));
            m_NetworkVariableVector3 = new NetworkVariableVector3(new Vector3(1, 1, 1));
            m_NetworkVariableVector2 = new NetworkVariableVector2(new Vector2(1, 1));
            m_NetworkVariableRay = new NetworkVariableRay(new Ray());
            m_NetworkVariableULong = new NetworkVariableULong(1);
            m_NetworkVariableUInt = new NetworkVariableUInt(1);
            m_NetworkVariableUShort = new NetworkVariableUShort(1);


            // NetworkVariable NetworkVariableSettings Constructor Test Coverage
            var settings = new NetworkVariableSettings();
            settings.ReadPermission = NetworkVariableReadPermission.OwnerOnly;
            settings.WritePermission = NetworkVariableWritePermission.ServerOnly;
            m_NetworkVariableBool = new NetworkVariableBool(settings);
            m_NetworkVariableByte = new NetworkVariableByte(settings);
            m_NetworkVariableColor = new NetworkVariableColor(settings);
            m_NetworkVariableColor32 = new NetworkVariableColor32(settings);
            m_NetworkVariableDouble = new NetworkVariableDouble(settings);
            m_NetworkVariableFloat = new NetworkVariableFloat(settings);
            m_NetworkVariableInt = new NetworkVariableInt(settings);
            m_NetworkVariableLong = new NetworkVariableLong(settings);
            m_NetworkVariableSByte = new NetworkVariableSByte(settings);
            m_NetworkVariableQuaternion = new NetworkVariableQuaternion(settings);
            m_NetworkVariableShort = new NetworkVariableShort(settings);
            m_NetworkVariableString = new NetworkVariableString(settings);
            m_NetworkVariableVector4 = new NetworkVariableVector4(settings);
            m_NetworkVariableVector3 = new NetworkVariableVector3(settings);
            m_NetworkVariableVector2 = new NetworkVariableVector2(settings);
            m_NetworkVariableRay = new NetworkVariableRay(settings);
            m_NetworkVariableULong = new NetworkVariableULong(settings);
            m_NetworkVariableUInt = new NetworkVariableUInt(settings);
            m_NetworkVariableUShort = new NetworkVariableUShort(settings);



            // NetworkVariable Value Type and NetworkVariableSettings Constructor Test Coverage
            m_NetworkVariableBool = new NetworkVariableBool(settings, true);
            m_NetworkVariableByte = new NetworkVariableByte(settings, 0);
            m_NetworkVariableColor = new NetworkVariableColor(settings, new Color(1, 1, 1, 1));
            m_NetworkVariableColor32 = new NetworkVariableColor32(settings, new Color32(1, 1, 1, 1));
            m_NetworkVariableDouble = new NetworkVariableDouble(settings, 1.0);
            m_NetworkVariableFloat = new NetworkVariableFloat(settings, 1.0f);
            m_NetworkVariableInt = new NetworkVariableInt(settings, 1);
            m_NetworkVariableLong = new NetworkVariableLong(settings, 1);
            m_NetworkVariableSByte = new NetworkVariableSByte(settings, 0);
            m_NetworkVariableQuaternion = new NetworkVariableQuaternion(settings, Quaternion.identity);
            m_NetworkVariableShort = new NetworkVariableShort(settings, 1);
            m_NetworkVariableString = new NetworkVariableString(settings, "My String Value");
            m_NetworkVariableVector4 = new NetworkVariableVector4(settings, new Vector4(1, 1, 1, 1));
            m_NetworkVariableVector3 = new NetworkVariableVector3(settings, new Vector3(1, 1, 1));
            m_NetworkVariableVector2 = new NetworkVariableVector2(settings, new Vector2(1, 1));
            m_NetworkVariableRay = new NetworkVariableRay(settings, new Ray());
            m_NetworkVariableULong = new NetworkVariableULong(settings, 1);
            m_NetworkVariableUInt = new NetworkVariableUInt(settings, 1);
            m_NetworkVariableUShort = new NetworkVariableUShort(settings, 1);



            // Use this nifty class: NetworkVariableHelper
            // Tracks if NetworkVariable changed invokes the OnValueChanged callback for the given instance type
            Bool_Var = new NetworkVariableHelper<bool>(m_NetworkVariableBool);
            Byte_Var = new NetworkVariableHelper<byte>(m_NetworkVariableByte);
            Color_Var = new NetworkVariableHelper<Color>(m_NetworkVariableColor);
            Color32_Var = new NetworkVariableHelper<Color32>(m_NetworkVariableColor32);
            Double_Var = new NetworkVariableHelper<double>(m_NetworkVariableDouble);
            Float_Var = new NetworkVariableHelper<float>(m_NetworkVariableFloat);
            Int_Var = new NetworkVariableHelper<int>(m_NetworkVariableInt);
            Long_Var = new NetworkVariableHelper<long>(m_NetworkVariableLong);
            Sbyte_Var = new NetworkVariableHelper<sbyte>(m_NetworkVariableSByte);
            Quaternion_Var = new NetworkVariableHelper<Quaternion>(m_NetworkVariableQuaternion);
            Short_Var = new NetworkVariableHelper<short>(m_NetworkVariableShort);
            String_Var = new NetworkVariableHelper<string>(m_NetworkVariableString);
            Vector4_Var = new NetworkVariableHelper<Vector4>(m_NetworkVariableVector4);
            Vector3_Var = new NetworkVariableHelper<Vector3>(m_NetworkVariableVector3);
            Vector2_Var = new NetworkVariableHelper<Vector2>(m_NetworkVariableVector2);
            Ray_Var = new NetworkVariableHelper<Ray>(m_NetworkVariableRay);
            Ulong_Var = new NetworkVariableHelper<ulong>(m_NetworkVariableULong);
            Uint_Var = new NetworkVariableHelper<uint>(m_NetworkVariableUInt);
            Ushort_Var = new NetworkVariableHelper<ushort>(m_NetworkVariableUShort);

        }

        /// <summary>
        /// Test result for all values changed the expected number of times (once per unique NetworkVariable type)
        /// </summary>
        public bool DidAllValuesChange()
        {
            if (BaseNetworkVariableHelper.VarChangedCount == BaseNetworkVariableHelper.InstanceCount)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns back whether the test has completed the total number of iterations
        /// </summary>
        public bool IsTestComplete()
        {
            return m_FinishedTests;
        }

        // Update is called once per frame
        private void Update()
        {
            if (EnableTesting)
            {
                //Added timeout functionality for near future changes to NetworkVariables and the Snapshot system
                if (!m_FinishedTests && m_ChangesAppliedToNetworkVariables)
                {
                    //We finish testing if all NetworkVariables changed their value or we timed out waiting for
                    //all NetworkVariables to change their value
                    m_FinishedTests = DidAllValuesChange() || (m_WaitForChangesTimeout < Time.realtimeSinceStartup);
                }
                else
                {
                    if (NetworkManager != null && NetworkManager.IsListening)
                    {
                        if (!m_Initialized)
                        {
                            InitializeTest();
                            m_Initialized = true;
                        }
                        else
                        {
                            //Now change all of the values to make sure we are at least testing the local callback
                            m_NetworkVariableBool.Value = false;
                            m_NetworkVariableByte.Value = 255;
                            m_NetworkVariableColor.Value = new Color(100, 100, 100);
                            m_NetworkVariableColor32.Value = new Color32(100, 100, 100, 100);
                            m_NetworkVariableDouble.Value = 1000;
                            m_NetworkVariableFloat.Value = 1000.0f;
                            m_NetworkVariableInt.Value = 1000;
                            m_NetworkVariableLong.Value = 100000;
                            m_NetworkVariableSByte.Value = -127;
                            m_NetworkVariableQuaternion.Value = new Quaternion(100, 100, 100, 100);
                            m_NetworkVariableShort.Value = short.MaxValue;
                            m_NetworkVariableString.Value = "My Changed String Value";
                            m_NetworkVariableVector4.Value = new Vector4(1000, 1000, 1000, 1000);
                            m_NetworkVariableVector3.Value = new Vector3(1000, 1000, 1000);
                            m_NetworkVariableVector2.Value = new Vector2(1000, 1000);
                            m_NetworkVariableRay.Value = new Ray(Vector3.one, Vector3.right);
                            m_NetworkVariableULong.Value = ulong.MaxValue;
                            m_NetworkVariableUInt.Value = uint.MaxValue;
                            m_NetworkVariableUShort.Value = ushort.MaxValue;

                            //Set the timeout (i.e. how long we will wait for all NetworkVariables to have registered their changes)
                            m_WaitForChangesTimeout = Time.realtimeSinceStartup + 0.50f;
                            m_ChangesAppliedToNetworkVariables = true;
                        }
                    }
                }
            }
        }
    }
}
