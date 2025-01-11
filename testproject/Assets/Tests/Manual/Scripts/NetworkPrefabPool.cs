using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TestProject.ManualTests
{
    public class NetworkPrefabPool : NetworkBehaviour, INetworkPrefabInstanceHandler
    {
        [Header("General Settings")]
        public bool AutoSpawnEnable = true;
        public float InitialSpawnDelay;
        public int SpawnsPerSecond;
        public int PoolSize;
        public float ObjectSpeed = 10.0f;
        public bool DontDestroy;

        public bool LabelEnabled = true;

        public int MaximumSpawnCount = 1000;

        public Toggle HalfFloat;
        public Toggle QuatSynch;
        public Toggle QuatComp;


        [Header("Prefab Instance Handling")]
        [Tooltip("When enabled, this will utilize the NetworkPrefabHandler to register a custom INetworkPrefabInstanceHandler")]
        public bool EnableHandler;

        [Tooltip("When enabled, this will register a custom INetworkPrefabInstanceHandler using a NetworkObject reference")]
        public bool RegisterUsingNetworkObject;

        [Tooltip("What is going to be spawned on the server side from the pool.")]
        public GameObject ServerObjectToPool;
        [Tooltip("What is going to be spawned on the client side from the ")]
        public GameObject ClientObjectToPool;

        [Header("Component Connections")]
        public SwitchSceneHandler SwitchScene;
        public Slider SpawnSlider;
        public Text SpawnSliderValueText;

        private bool m_IsSpawningObjects;

        private float m_EntitiesPerFrame;
        private float m_DelaySpawning;

        private GameObject m_ObjectToSpawn;
        private List<GameObject> m_ObjectPool;

        private static GameObject s_Instance;

        /// <summary>
        /// Called when enabled, if already connected we register any custom prefab spawn handler here
        /// </summary>
        private void OnEnable()
        {
            m_IsExitingScene = false;
            if (DontDestroy)
            {
                if (s_Instance != null && s_Instance != this)
                {
                    var instancePool = s_Instance.GetComponent<NetworkPrefabPool>();
                    if (instancePool.m_ObjectPool != null)
                    {
                        instancePool.MoveBackToCurrentlyActiveScene();
                        instancePool.m_ObjectPool.Clear();
                        m_ObjectPool = new List<GameObject>(instancePool.m_ObjectPool);
                    }
                    Destroy(s_Instance);
                    s_Instance = null;
                }

                if (s_Instance == null)
                {
                    s_Instance = gameObject;
                    DontDestroyOnLoad(gameObject);
                }
            }

            SpawnSlider?.gameObject.SetActive(false);
        }

        private bool m_IsExitingScene;
        public void OnExitingScene(bool destroyObjects)
        {
            m_IsExitingScene = true;

            if (DontDestroy)
            {
                if (destroyObjects)
                {
                    OnDestroyObjectPool();
                    SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
                    s_Instance = null;
                }
                else
                {
                    MovePoolObjectsToDontDestroyOnLoad();
                }
            }
        }

        private void MovePoolObjectsToDontDestroyOnLoad()
        {
            foreach (var obj in m_ObjectPool)
            {
                obj.GetComponent<NetworkObject>().AutoObjectParentSync = false;
                obj.transform.parent = gameObject.transform;
            }
        }

        private void MoveBackToCurrentlyActiveScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var obj in m_ObjectPool)
            {
                obj.transform.parent = null;
                obj.GetComponent<NetworkObject>().AutoObjectParentSync = true;
                SceneManager.MoveGameObjectToScene(obj, activeScene);
                var genericBehaviour = obj.GetComponent<GenericNetworkObjectBehaviour>();
                genericBehaviour.Reset();
            }
        }

        private bool m_IsQuitting;
        private void OnApplicationQuit()
        {
            m_IsQuitting = true;
            if (DontDestroy)
            {
                OnDestroyObjectPool();
            }
        }

        private void OnDisable()
        {
            if (!m_IsQuitting)
            {
                DeregisterCustomPrefabHandler();
            }
        }

        /// <summary>
        /// Handles registering the custom prefab handler
        /// </summary>
        private void RegisterCustomPrefabHandler()
        {
            // Register the custom spawn handler?
            if (EnableHandler)
            {
                if (NetworkManager && NetworkManager.PrefabHandler != null)
                {
                    if (RegisterUsingNetworkObject)
                    {
                        NetworkManager.PrefabHandler.AddHandler(ServerObjectToPool.GetComponent<NetworkObject>(), this);
                    }
                    else
                    {
                        NetworkManager.PrefabHandler.AddHandler(ServerObjectToPool, this);
                    }
                }
                else if (!IsServer)
                {
                    Debug.LogWarning($"Failed to register custom spawn handler and {nameof(EnableHandler)} is set to true!");
                }
            }
        }

        private void DeregisterCustomPrefabHandler()
        {
            // Register the custom spawn handler?
            if (EnableHandler && NetworkManager && NetworkManager.PrefabHandler != null)
            {
                NetworkManager.PrefabHandler.RemoveHandler(ServerObjectToPool);
                if (IsClient && m_ObjectToSpawn != null)
                {
                    NetworkManager.PrefabHandler.RemoveHandler(m_ObjectToSpawn);
                }
            }
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.Unload:
                    {
                        if (sceneEvent.LoadSceneMode == LoadSceneMode.Single && (gameObject.scene.name == sceneEvent.SceneName))
                        {
                            OnDestroyObjectPool();
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// Destroys the NetworkObject pool
        /// </summary>
        private void OnDestroyObjectPool()
        {
            if (NetworkObject != null && NetworkManager != null)
            {
                if (IsServer)
                {
                    StopCoroutine(SpawnObjects());
                }

                // De-register the custom prefab handler
                DeregisterCustomPrefabHandler();

                CleanNetworkObjects();

                if (NetworkManager != null && NetworkManager.SceneManager != null)
                {
                    NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
                }

                if (DontDestroy)
                {
                    PostNetworkObjectCleanup();
                }
            }
        }


        private IEnumerator PostNetworkObjectCleanup()
        {
            yield return new WaitUntil(() => !NetworkManager.Singleton.IsListening);
            yield return new WaitForSeconds(0.5f);
            OnNetworkDespawn();
        }


        public override void OnNetworkDespawn()
        {
            DeregisterCustomPrefabHandler();
            if (NetworkManager != null)
            {
                NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
            }

            foreach (var obj in m_ObjectPool)
            {
                var networkObject = obj.GetComponent<NetworkObject>();
                var genericBehaviour = obj.GetComponent<GenericNetworkObjectBehaviour>();
                if (networkObject.IsSpawned)
                {

                    Debug.Log($"{networkObject.name} is still considered spawned!");
                }
            }
        }

        private void CleanNetworkObjects()
        {
            if (m_ObjectPool != null)
            {
                foreach (var obj in m_ObjectPool)
                {
                    if (obj != null)
                    {
                        var networkObject = obj.GetComponent<NetworkObject>();
                        var genericBehaviour = obj.GetComponent<GenericNetworkObjectBehaviour>();
                        if (genericBehaviour != null)
                        {
                            genericBehaviour.IsRegisteredPoolObject = false;
                            genericBehaviour.IsRemovedFromPool = true;
                            if (IsServer)
                            {
                                if (networkObject.IsSpawned)
                                {
                                    networkObject.Despawn();
                                }
                                else if (!DontDestroy)
                                {
                                    DestroyImmediate(obj);
                                }

                            }
                            else  //Client
                            {
                                if (!DontDestroy && !networkObject.IsSpawned)
                                {
                                    DestroyImmediate(obj);
                                }

                            }
                        }
                    }
                }
                if (!DontDestroy)
                {
                    m_ObjectPool.Clear();
                }
            }
        }

        private void SetGlobalTransformPropertiesUI(bool isVisible)
        {
            HalfFloat?.gameObject.SetActive(isVisible);
            QuatComp?.gameObject.SetActive(isVisible);
            QuatSynch?.gameObject.SetActive(isVisible);
        }

        private void Awake()
        {
            SetGlobalTransformPropertiesUI(false);
        }

        // Start is called before the first frame update
        private void Start()
        {
            if (SpawnSliderValueText != null)
            {
                SpawnSliderValueText.text = SpawnsPerSecond.ToString();
            }

            m_LabelEnabled = LabelEnabled;
            NetworkObjectLabel.GlobalVisibility = m_LabelEnabled;
            if (HalfFloat != null)
            {
                m_UseHalfFloatPrecision = HalfFloat.isOn;
            }

            if (QuatSynch != null)
            {
                m_QuaternionSynchronization = QuatSynch.isOn;
            }

            if (QuatComp != null)
            {
                m_CompressQuaternions = QuatComp.isOn;
            }
        }

        private bool m_LabelEnabled;
        private bool m_UseHalfFloatPrecision;
        private bool m_CompressQuaternions;
        private bool m_QuaternionSynchronization;

        [ClientRpc]
        private void ShowHideObjectIdLabelClientRpc(bool isVisible)
        {
            m_LabelEnabled = isVisible;
            NetworkObjectLabel.GlobalVisibility = m_LabelEnabled;
#if UNITY_2023_1_OR_NEWER
            var labels = FindObjectsByType<NetworkObjectLabel>(FindObjectsSortMode.InstanceID);
#else
            var labels = FindObjectsOfType<NetworkObjectLabel>();
#endif

            foreach (var label in labels)
            {
                label.SetLabelVisibility(isVisible);
            }
        }

        /// <summary>
        /// Override NetworkBehaviour.NetworkStart
        /// </summary>
        public override void OnNetworkSpawn()
        {
            NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
            InitializeObjectPool();
            if (IsServer)
            {
                if (isActiveAndEnabled)
                {
                    m_DelaySpawning = Time.realtimeSinceStartup + InitialSpawnDelay;
                    StartSpawningBoxes();

                    //Make sure our slider reflects the current spawn rate
                    UpdateSpawnsPerSecond();
                }
                SetGlobalTransformPropertiesUI(true);
            }
        }

        /// <summary>
        /// Determines which object is going to be spawned and then
        /// initializes the object pool based on
        /// </summary>
        public void InitializeObjectPool()
        {
            // Base construction and registration of the custom prefab handler.
            RegisterCustomPrefabHandler();

            // Default to the server side object
            m_ObjectToSpawn = ServerObjectToPool;

            // Host and Client need to do an extra step
            if (IsClient)
            {
                if (EnableHandler && ClientObjectToPool != null)
                {
                    m_ObjectToSpawn = NetworkManager.GetNetworkPrefabOverride(ClientObjectToPool);
                }
                else
                {
                    m_ObjectToSpawn = NetworkManager.GetNetworkPrefabOverride(m_ObjectToSpawn);
                }

                // Since the host should spawn the override, we need to register the host to link it to the originally registered ServerObjectToPool
                if (IsHost && EnableHandler && ServerObjectToPool != m_ObjectToSpawn)
                {
                    // While this seems redundant, we could theoretically have several objects that we could potentially be spawning
                    NetworkManager.PrefabHandler.RegisterHostGlobalObjectIdHashValues(ServerObjectToPool, new List<GameObject>() { m_ObjectToSpawn });
                }
            }

            if ((EnableHandler || IsServer) && m_ObjectPool == null)
            {
                m_ObjectPool = new List<GameObject>(PoolSize);

                for (int i = 0; i < PoolSize; i++)
                {
                    var gameObject = AddNewInstance();
                    gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Gets an object from the pool
        /// </summary>
        /// <returns></returns>
        public GameObject GetObject()
        {
            if (m_ObjectPool != null)
            {
                foreach (var obj in m_ObjectPool)
                {
                    if (obj != null && !obj.activeInHierarchy)
                    {
                        obj.SetActive(true);
                        return obj;
                    }
                }
                var newObj = AddNewInstance();
                return newObj;
            }
            return null;
        }

        /// <summary>
        /// Add a new instance to the object pool
        /// </summary>
        /// <returns>new instance of the m_ObjectToSpawn prefab</returns>
        private GameObject AddNewInstance()
        {
            var obj = Instantiate(m_ObjectToSpawn);
            var genericNetworkObjectBehaviour = obj.GetComponent<GenericNetworkObjectBehaviour>();
            genericNetworkObjectBehaviour.HasHandler = EnableHandler;
            genericNetworkObjectBehaviour.IsRegisteredPoolObject = true;
            var networkObjectLabel = obj.GetComponentInChildren<NetworkObjectLabel>();
            networkObjectLabel?.SetLabelVisibility(LabelEnabled);
            ApplyPrecisionAdjustments(obj);
            m_ObjectPool.Add(obj);
            return m_ObjectPool[m_ObjectPool.Count - 1];
        }

        /// <summary>
        /// Starts spawning
        /// </summary>
        private void StartSpawningBoxes()
        {
            if (NetworkManager.IsHost && SpawnSlider != null)
            {
                SpawnSlider.gameObject.SetActive(true);
            }

            if (SpawnsPerSecond > 0)
            {
                StartCoroutine(SpawnObjects());
            }
        }

        /// <summary>
        /// Checks to determine if we need to update our spawns per second calculations
        /// </summary>
        public void UpdateSpawnsPerSecond()
        {
            if (SpawnSlider != null)
            {
                SpawnsPerSecond = (int)SpawnSlider.value;
                SpawnSliderValueText.text = SpawnsPerSecond.ToString();

                // Handle case where the initial value is set to zero and so coroutine needs to be started
                if (SpawnsPerSecond > 0 && !m_IsSpawningObjects)
                {
                    StartSpawningBoxes();
                }
                else //Handle case where spawning coroutine is running but we set our spawn rate to zero
                if (SpawnsPerSecond == 0 && m_IsSpawningObjects)
                {
                    m_IsSpawningObjects = false;
                    StopCoroutine(SpawnObjects());
                }
            }
        }

        private void ApplyPrecisionAdjustments(GameObject gameObject)
        {
            var networkTransform = gameObject.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (networkTransform != null)
            {
                networkTransform.UseQuaternionSynchronization = m_QuaternionSynchronization;
                networkTransform.UseHalfFloatPrecision = m_UseHalfFloatPrecision;
                networkTransform.UseQuaternionCompression = m_CompressQuaternions;
            }
        }

        private void AdjustPrecision()
        {
            foreach (var spawnObject in m_ObjectPool)
            {
                ApplyPrecisionAdjustments(spawnObject);
            }
        }

        private void CheckPropertyChanges()
        {
            if (m_LabelEnabled != LabelEnabled)
            {
                m_LabelEnabled = LabelEnabled;
                NetworkObjectLabel.GlobalVisibility = m_LabelEnabled;
                ShowHideObjectIdLabelClientRpc(m_LabelEnabled);
            }

            if ((HalfFloat != null && m_UseHalfFloatPrecision != HalfFloat.isOn) || (QuatComp != null && m_CompressQuaternions != QuatComp.isOn)
                || (QuatSynch != null) && m_QuaternionSynchronization != QuatSynch.isOn)
            {
                m_UseHalfFloatPrecision = HalfFloat.isOn;
                m_CompressQuaternions = QuatComp.isOn;
                m_QuaternionSynchronization = QuatSynch.isOn;
                AdjustPrecision();
            }
        }

        /// <summary>
        /// Coroutine to spawn boxes
        /// </summary>
        /// <returns></returns>
        private IEnumerator SpawnObjects()
        {
            //Exit if we are a client or we happen to not have a NetworkManager
            if (NetworkManager == null || (NetworkManager.IsClient && !NetworkManager.IsHost && !NetworkManager.IsServer))
            {
                yield return null;
            }

            if (m_DelaySpawning > Time.realtimeSinceStartup)
            {
                yield return new WaitForSeconds(m_DelaySpawning - Time.realtimeSinceStartup);
            }

            m_IsSpawningObjects = true;


            while (m_IsSpawningObjects && !m_IsExitingScene)
            {
                //Start spawning if auto spawn is enabled
                if (AutoSpawnEnable)
                {
                    float entitySpawnUpdateRate = 1.0f;
                    CheckPropertyChanges();
                    if (SpawnsPerSecond > 0)
                    {
                        entitySpawnUpdateRate = 1.0f / Mathf.Min(SpawnsPerSecond, 60.0f);
                        //While not 100% accurate, this basically allows for higher entities per second generation
                        m_EntitiesPerFrame = SpawnsPerSecond * entitySpawnUpdateRate;
                        int entitityCountPerFrame = Mathf.RoundToInt(m_EntitiesPerFrame);
                        //Spawn (n) entities then wait for 1/60th of a second and repeat
                        for (int i = 0; i < entitityCountPerFrame; i++)
                        {
                            if (NetworkManager.SpawnManager.SpawnedObjectsList.Count >= MaximumSpawnCount)
                            {
                                break;
                            }
                            GameObject go = GetObject();
                            if (go != null)
                            {
                                go.transform.position = transform.position;

                                float ang = Random.Range(0.0f, 2 * Mathf.PI);
                                go.GetComponent<GenericNetworkObjectBehaviour>().SetDirectionAndVelocity(new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)), ObjectSpeed);

                                var no = go.GetComponent<NetworkObject>();
                                if (!no.IsSpawned)
                                {
                                    no.Spawn(true);
                                }
                            }
                        }
                    }
                    yield return new WaitForSeconds(entitySpawnUpdateRate);
                }
                else //Just hang out until it is enabled
                {
                    yield return new WaitForSeconds(1.0f);
                }
            }
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            var obj = GetObject();
            if (obj != null)
            {
                obj.transform.position = position;
                obj.transform.rotation = rotation;
                return obj.GetComponent<NetworkObject>();
            }
            return null;
        }
        public void Destroy(NetworkObject networkObject)
        {
            var genericBehaviour = networkObject.gameObject.GetComponent<GenericNetworkObjectBehaviour>();
            if (genericBehaviour.IsRegisteredPoolObject)
            {
                networkObject.transform.position = transform.position;
                networkObject.transform.rotation = transform.rotation;
                networkObject.gameObject.SetActive(false);
            }
            else
            {
                Destroy(networkObject.gameObject);
            }
        }
    }
}
