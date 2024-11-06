using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
[CustomEditor(typeof(TestProject.ManualTests.ChildMover))]
public class ChildMoverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}
#endif
namespace TestProject.ManualTests
{
    public class ChildMover : IntegrationNetworkTransform
    {
        public static bool RandomizeScale = true;

        [Range(0.1f, 30.0f)]
        public float RotationSpeed = 5.0f;

        private Transform m_RootParentTransform;

        public bool RotateBasedOnDirection = false;

        /// <summary>
        /// For other components to determine if this instance has authority
        /// </summary>
        public bool IsAuthority()
        {
            return CanCommitToTransform;
        }

        public void PlayerIsMoving(float movementDirection)
        {
            if (IsSpawned && CanCommitToTransform)
            {
                var rotateDirection = RotateBasedOnDirection ? movementDirection * RotationSpeed : RotationSpeed;
                // Just make sure we are set to local space for this test
                if (InLocalSpace)
                {
                    transform.RotateAround(m_RootParentTransform.position, transform.TransformDirection(Vector3.up), RotationSpeed);
                }
            }
        }

        private Transform GetRootParentTransform(Transform transform)
        {
            if (transform.parent != null)
            {
                return GetRootParentTransform(transform.parent);
            }
            return transform;
        }

        protected override void OnNetworkPostSpawn()
        {
            if (CanCommitToTransform)
            {
                m_RootParentTransform = GetRootParentTransform(transform);
                if (RandomizeScale)
                {
                    transform.localScale = transform.localScale * Random.Range(0.5f, 1.5f);
                }
            }
            base.OnNetworkPostSpawn();
        }
    }
}
