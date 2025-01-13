using UnityEngine;
using System;
using UnityEditor;

namespace Unity.Netcode
{
    public class MonobehaviourHelper : MonoBehaviour
    {
        public static event Action OnLateUpdate;

        private void LateUpdate()
        {
            OnLateUpdate?.Invoke();
        }

        private static MonobehaviourHelper s_MonobehaviourHelperInstance;

        public static void InitMonoBehaviourHelper()
        {
#if UNITY_EDITOR
            // Handling Edit Time behavior:
            void OnEditorApplicationOnplayModeStateChanged(PlayModeStateChange change)
            {
                Debug.Log("Instantiate RuntimeUpdaterBehaviour");

                switch (change)
                {
                    case PlayModeStateChange.EnteredPlayMode:
                    {
                        CreateInstance();
                        break;
                    }
                    case PlayModeStateChange.ExitingPlayMode:
                    {
                        UnityEngine.Object.Destroy(s_MonobehaviourHelperInstance);
                        break;
                    }
                }
            }

            EditorApplication.playModeStateChanged += OnEditorApplicationOnplayModeStateChanged;

            if (EditorApplication.isPlaying)
            {
                CreateInstance();
            }
#else
            CreateInstance();
#endif
        }

        private static void CreateInstance()
        {
            Debug.Assert(s_MonobehaviourHelperInstance == null, s_MonobehaviourHelperInstance);

            s_MonobehaviourHelperInstance = new GameObject($"[{nameof(MonobehaviourHelper)}]").AddComponent<MonobehaviourHelper>();
            s_MonobehaviourHelperInstance.gameObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(s_MonobehaviourHelperInstance.gameObject);
        }
    }
}
