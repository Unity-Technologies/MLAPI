using UnityEngine;
using TestProject.ManualTests;

#if UNITY_EDITOR
using UnityEditor;
[CustomEditor(typeof(TestProject.RuntimeTests.AutomatedPlayerMover))]
public class AutomatedPlayerMoverManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}
#endif

namespace TestProject.RuntimeTests
{
    public class AutomatedPlayerMover : IntegrationNetworkTransform
    {
        public static bool StopMovement;
        private bool m_LocalStopMovement;
        private float m_Speed = 15.0f;
        private float m_RotSpeed = 15.0f;

        private GameObject m_Destination;
        private Vector3 m_Target;

        private void UpdateDestination()
        {
            if (Navigationpoints.Instance != null)
            {
                var targetNavPointIndex = Random.Range(0, Navigationpoints.Instance.NavPoints.Count - 1);
                m_Destination = Navigationpoints.Instance.NavPoints[targetNavPointIndex];

                m_Target = m_Destination.transform.position;
            }
        }

        protected override void OnNetworkPostSpawn()
        {
            if (CanCommitToTransform)
            {
                UpdateDestination();
            }
            base.OnNetworkPostSpawn();
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (CanCommitToTransform)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    m_LocalStopMovement = !m_LocalStopMovement;
                }

                if (!StopMovement && !m_LocalStopMovement)
                {
                    m_Target.y = transform.position.y;
                    var distance = Vector3.Distance(transform.position, m_Target);
                    if (distance < 0.25f)
                    {
                        var currentDestination = m_Destination;
                        while (m_Destination == currentDestination)
                        {
                            UpdateDestination();
                        }
                    }

                    transform.position = Vector3.MoveTowards(transform.position, m_Destination.transform.position, m_Speed * Time.deltaTime);
                    var normalizedDirection = (m_Target - transform.position).normalized;
                    if (normalizedDirection.magnitude != 0.0f)
                    {
                        var lookRotation = Quaternion.LookRotation(normalizedDirection, transform.up).eulerAngles;
                        var currentEuler = transform.eulerAngles;
                        currentEuler.y = Mathf.LerpAngle(currentEuler.y, lookRotation.y, Time.deltaTime * m_RotSpeed);
                        transform.eulerAngles = currentEuler;
                    }
                }
            }
            else
            {
                base.OnUpdate();
            }
        }
    }
}
