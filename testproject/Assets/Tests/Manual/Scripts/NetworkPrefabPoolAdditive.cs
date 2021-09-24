using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;

namespace TestProject.ManualTests
{
    public class NetworkPrefabPoolAdditive : NetworkBehaviour, INetworkPrefabInstanceHandler
    {
        [Header("General Settings")]
        public bool RandomMovement = true;
        public bool AutoSpawnEnable = true;
        [Tooltip("When enabled, this will spawn the objects in the source spawn generator's scene")]
        public bool SpawnInSourceScene = true;

        [Tooltip("When enabled, this will despawn or destroy all associated network prefab instances when the additive scene is unloaded.")]
        public bool DestroyOnUnload = false;

        public bool Interpolate = true;
        public float InitialSpawnDelay;
        public int SpawnsPerSecond = 3;
        public int PoolSize;
        public float ObjectSpeed = 10.0f;




        [Header("Prefab Instance Handling")]
        [Tooltip("When enabled, this will utilize the NetworkPrefabHandler to register a custom INetworkPrefabInstanceHandler")]
        public bool EnableHandler;

        [Tooltip("When enabled, this will register a custom INetworkPrefabInstanceHandler using a NetworkObject reference")]
        public bool RegisterUsingNetworkObject;

        [Tooltip("What is going to be spawned on the server side from the pool.")]
        public GameObject ServerObjectToPool;
        [Tooltip("What is going to be spawned on the client side from the ")]
        public GameObject ClientObjectToPool;

        private bool m_IsSpawningObjects;

        private float m_EntitiesPerFrame;
        private float m_DelaySpawning;

        private GameObject m_ObjectToSpawn;
        private List<GameObject> m_ObjectPool;

        private NetworkVariable<bool> m_CurrentInterpolateState = new NetworkVariable<bool>();

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

        /// <summary>
        /// General clean up
        /// The custom prefab handler is unregistered here
        /// </summary>
        private void OnDisable()
        {
            if (IsServer)
            {
                StopCoroutine(SpawnObjects());
            }
            DeregisterCustomPrefabHandler();



            if (NetworkManager != null && NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
            }
        }

        // Start is called before the first frame update
        private void Start()
        {
            NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
        }


        /// <summary>
        /// For additive scenes, we only clear out our pooled NetworkObjects if we are migrating them from the ActiveScene
        /// to the scene where this NetworkPrefabPoolAdditive component is instantiated.
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="sceneName"></param>
        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            // Only process events for our local client id (this includes server)
            if (sceneEvent.ClientId == NetworkManager.LocalClientId)
            {
                switch (sceneEvent.SceneEventType)
                {
                    case SceneEventData.SceneEventTypes.S2C_Unload:
                        {
                            if (sceneEvent.LoadSceneMode == LoadSceneMode.Additive && (gameObject.scene.name == sceneEvent.SceneName))
                            {
                                OnUnloadScene();
                            }
                            break;
                        }
                    case SceneEventData.SceneEventTypes.S2C_Load:
                        {
                            if (sceneEvent.LoadSceneMode == LoadSceneMode.Single && ((gameObject.scene.name == sceneEvent.SceneName) || !SpawnInSourceScene))
                            {
                                OnUnloadScene();
                            }
                            break;
                        }
                }
            }
        }

        private void CleanNetworkObjects()
        {
            if (m_ObjectPool != null)
            {
                foreach (var obj in m_ObjectPool)
                {
                    if (obj == null)
                    {
                        continue;
                    }
                    var networkObject = obj.GetComponent<NetworkObject>();
                    var genericBehaviour = obj.GetComponent<GenericNetworkObjectBehaviour>();
                    genericBehaviour.IsRegisteredPoolObject = false;
                    genericBehaviour.IsRemovedFromPool = true;
                    genericBehaviour.HasHandler = false;
                    if (IsServer)
                    {
                        if (networkObject.IsSpawned)
                        {
                            if (DestroyOnUnload)
                            {
                                networkObject.Despawn(true);
                            }
                            else if (SpawnInSourceScene)
                            {
                                // If we are spawning in the source scene and we are not supposed to destroy this object
                                // then move it to the currently active scene.
                                var activeScene = SceneManager.GetActiveScene();
                                if (gameObject.scene != SceneManager.GetActiveScene())
                                {
                                    SceneManager.MoveGameObjectToScene(obj, activeScene);
                                }
                            }
                        }
                        else
                        {
                            DestroyImmediate(obj);
                        }
                    }
                    else //Client
                    {
                        if (!networkObject.IsSpawned)
                        {
                            DestroyImmediate(obj);
                        }
                        else if (SpawnInSourceScene)
                        {
                            // If we are spawning in the source scene and we are not supposed to destroy this object
                            // then move it to the currently active scene.
                            var activeScene = SceneManager.GetActiveScene();
                            if (gameObject.scene != SceneManager.GetActiveScene())
                            {
                                SceneManager.MoveGameObjectToScene(obj, activeScene);
                            }
                        }
                    }
                }
                m_ObjectPool.Clear();
            }
        }

        /// <summary>
        /// Detect when we are switching scenes in order
        /// to assure we stop spawning objects
        /// </summary>
        private void OnUnloadScene()
        {
            if (IsServer)
            {
                StopCoroutine(SpawnObjects());
            }

            // De-register the custom prefab handler
            DeregisterCustomPrefabHandler();

            CleanNetworkObjects();

            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
        }

        /// <summary>
        /// Override NetworkBehaviour.NetworkStart
        /// </summary>
        public override void OnNetworkSpawn()
        {
            InitializeObjectPool();
            if (IsServer)
            {
                if (isActiveAndEnabled)
                {
                    m_DelaySpawning = Time.realtimeSinceStartup + InitialSpawnDelay;
                    //Make sure our slider reflects the current spawn rate
                    UpdateSpawnsPerSecond();
                }

                // Server dictates interpolation
                m_CurrentInterpolateState.Value = Interpolate;
            }

            // Both client and server want to know when this changes
            m_CurrentInterpolateState.OnValueChanged = OnInterpolateStateChanged;
        }

        /// <summary>
        /// When we change interpolation state, we change this for all of our
        /// NetworkObjects in the pool
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        private void OnInterpolateStateChanged(bool previous, bool current)
        {
            if (IsSpawned)
            {
                Interpolate = current;
                foreach (var obj in m_ObjectPool)
                {
                    if (obj == null)
                    {
                        continue;
                    }
                    obj.GetComponent<NetworkTransform>().Interpolate = Interpolate;
                }
            }
        }

        /// <summary>
        /// Server uses the update to check and see if we want to change the interpolate
        /// status for the NetworkObject Pool
        /// </summary>
        private void Update()
        {
            if (IsSpawned && IsServer)
            {
                if (Interpolate != m_CurrentInterpolateState.Value)
                {
                    m_CurrentInterpolateState.Value = Interpolate;
                }
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

            if (EnableHandler || IsServer)
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
                    if (obj == null)
                    {
                        continue;
                    }

                    if (!obj.activeInHierarchy)
                    {
                        return obj;
                    }
                }
                var newObj = AddNewInstance();
                newObj.SetActive(true);
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
            if (genericNetworkObjectBehaviour)
            {
                genericNetworkObjectBehaviour.ShouldMoveRandomly(RandomMovement);
                genericNetworkObjectBehaviour.IsRegisteredPoolObject = true;
            }

            // Example of how to keep your pooled NetworkObjects in the same scene as your spawn generator (additive scenes only)
            if (SpawnInSourceScene && gameObject.scene != null)
            {
                // If you move your NetworkObject into the same scene as the spawn generator, then you do not need to worry
                // about setting the NetworkObject's scene dependency.
                SceneManager.MoveGameObjectToScene(obj, gameObject.scene);
            }
            obj.GetComponent<NetworkTransform>().Interpolate = Interpolate;
            obj.SetActive(false);


            m_ObjectPool.Add(obj);
            return obj;
        }

        /// <summary>
        /// Starts spawning
        /// </summary>
        private void StartSpawningBoxes()
        {
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
            while (m_IsSpawningObjects)
            {
                //Start spawning if auto spawn is enabled
                if (AutoSpawnEnable)
                {
                    float entitySpawnUpdateRate = 1.0f;
                    if (SpawnsPerSecond > 0)
                    {
                        entitySpawnUpdateRate = 1.0f / Mathf.Min(SpawnsPerSecond, 60.0f);
                        //While not 100% accurate, this basically allows for higher entities per second generation
                        m_EntitiesPerFrame = (float)SpawnsPerSecond * entitySpawnUpdateRate;
                        int entitityCountPerFrame = Mathf.RoundToInt(m_EntitiesPerFrame);
                        //Spawn (n) entities then wait for 1/60th of a second and repeat
                        for (int i = 0; i < entitityCountPerFrame; i++)
                        {
                            GameObject go = GetObject();
                            if (go != null)
                            {
                                go.transform.position = transform.position;
                                go.SetActive(true);
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
                obj.SetActive(true);


                return obj.GetComponent<NetworkObject>();
            }
            return null;
        }
        public void Destroy(NetworkObject networkObject)
        {
            var genericBehaviour = networkObject.gameObject.GetComponent<GenericNetworkObjectBehaviour>();
            if (genericBehaviour.IsRegisteredPoolObject)
            {
                networkObject.gameObject.SetActive(false);
            }
            else
            {
                Destroy(networkObject.gameObject);
            }
        }
    }
}
