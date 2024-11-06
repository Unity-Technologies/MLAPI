using System;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Tests.Manual.NetworkAnimatorTests
{
    [RequireComponent(typeof(Animator))]
    public class AnimatedCubeController : NetworkBehaviour
    {
        private Animator m_Animator;
        private bool m_Rotate;
        private NetworkAnimator m_NetworkAnimator;
        private bool m_IsServerAuthoritative = true;

        private void DetermineNetworkAnimatorComponentType()
        {
            m_NetworkAnimator = GetComponent<NetworkAnimator>();
            if (m_NetworkAnimator != null)
            {
                m_IsServerAuthoritative = m_NetworkAnimator.GetType() != typeof(OwnerNetworkAnimator);
            }
            else
            {
                throw new Exception($"{nameof(AnimatedCubeController)} requires that it is paired with either a {nameof(NetworkAnimator)} or {nameof(OwnerNetworkAnimator)}.  Neither of the two components were found!");
            }
        }

        public override void OnNetworkSpawn()
        {
            DetermineNetworkAnimatorComponentType();

            m_Animator = GetComponent<Animator>();

            m_Rotate = m_Animator.GetBool("Rotate");
        }

        private bool IsServerAuthority()
        {
            if (IsServer && m_IsServerAuthoritative)
            {
                return true;
            }
            return false;
        }

        private bool IsOwnerAuthority()
        {
            if (IsOwner && !m_IsServerAuthoritative)
            {
                return true;
            }
            return false;
        }


        [ServerRpc(RequireOwnership = false)]
        private void ToggleRotateAnimationServerRpc(bool rotate)
        {
            m_Rotate = rotate;
            m_Animator.SetBool("Rotate", m_Rotate);
        }

        internal void ToggleRotateAnimation()
        {
            m_Rotate = !m_Rotate;
            if (m_IsServerAuthoritative)
            {
                if (!IsServer && IsOwner)
                {
                    ToggleRotateAnimationServerRpc(m_Rotate);
                }
                else if (IsServer && IsOwner)
                {
                    m_Animator.SetBool("Rotate", m_Rotate);
                }
            }
            else if (IsOwner)
            {
                m_Animator.SetBool("Rotate", m_Rotate);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void PlayPulseAnimationServerRpc(bool rotate)
        {
            m_NetworkAnimator.SetTrigger("Pulse");
        }

        internal void PlayPulseAnimation()
        {
            if (m_IsServerAuthoritative)
            {
                if (!IsServer && IsOwner)
                {
                    m_NetworkAnimator.SetTrigger("Pulse");
                }
                else if (IsServer && IsOwner)
                {
                    m_NetworkAnimator.SetTrigger("Pulse");
                }
            }
            else if (IsOwner)
            {
                m_NetworkAnimator.SetTrigger("Pulse");
            }
        }

        private Coroutine m_TestAnimatorRoutine;

        internal void TestAnimator(bool useNetworkAnimator = true)
        {
            if (IsServer)
            {
                if (m_TestAnimatorRoutine == null)
                {
                    m_TestAnimatorRoutine = StartCoroutine(TestAnimatorRoutine());
                }
            }
        }

        private IEnumerator TestAnimatorRoutine()
        {
            var waitForSeconds = new WaitForSeconds(0.016f);
            var counter = 1.0f;
            Debug.Log("Linearly increase test:");
            while (counter < 100)
            {
                m_Animator.SetFloat("TestFloat", counter);
                m_Animator.SetInteger("TestInt", (int)counter);
                counter++;
                yield return waitForSeconds;
            }
            Debug.Log("Random value assignment test:");
            counter = 0.0f;
            while (counter < 100)
            {
                m_Animator.SetFloat("TestFloat", UnityEngine.Random.Range(0.0f, 100.0f));
                m_Animator.SetInteger("TestInt", UnityEngine.Random.Range(0, 100));
                counter++;
                yield return waitForSeconds;
            }
            StopCoroutine(m_TestAnimatorRoutine);
            m_TestAnimatorRoutine = null;
        }

        private int m_TestIntValue;
        private float m_TestFloatValue;

        private void DisplayTestIntValueIfChanged()
        {
            var testIntValue = m_Animator.GetInteger("TestInt");
            if (m_TestIntValue != testIntValue)
            {
                m_TestIntValue = testIntValue;
                Debug.Log($"[{name}]TestInt value changed to = {m_TestIntValue}");
            }
            var testFloatValue = m_Animator.GetFloat("TestFloat");
            if (m_TestFloatValue != testFloatValue)
            {
                m_TestFloatValue = testFloatValue;
                Debug.Log($"[{name}]TestFloat value changed to = {m_TestIntValue}");
            }
        }

        private void BeginAttack(int weaponType)
        {
            m_Animator.SetInteger("WeaponType", weaponType);
            m_NetworkAnimator.SetTrigger("Attack");
        }

        private void SetLayerWeight(int layer, float weight)
        {
            m_Animator.SetLayerWeight(layer, weight);
        }

        private float GetLayerWeight(int layer)
        {
            return m_Animator.GetLayerWeight(layer);
        }

        [ServerRpc]
        private void TestCrossFadeServerRpc()
        {
            m_Animator.CrossFade("CrossFadeState", 0.25f, 0);
        }

        private void TestCrossFade()
        {
            if (!IsServer && m_IsServerAuthoritative)
            {
                TestCrossFadeServerRpc();
            }
            else
            {
                m_Animator.CrossFade("CrossFadeState", 0.25f, 0);
            }
        }

        private void LateUpdate()
        {

            if (!IsSpawned || !IsOwner)
            {
                if (!IsOwner && IsSpawned)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha4))
                    {
                        Debug.Log($"Layer 1 weight: {GetLayerWeight(1)}");
                    }
                    DisplayTestIntValueIfChanged();
                    return;
                }

                return;
            }

            DisplayTestIntValueIfChanged();

            if (Input.GetKeyDown(KeyCode.G))
            {
                TestCrossFade();
            }

            // Rotates the cube
            if (Input.GetKeyDown(KeyCode.C))
            {
                ToggleRotateAnimation();
            }

            // Pulse animation (scale down and up slowly)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                PlayPulseAnimation();
            }

            // Test changing Animator parameters over time
            if (Input.GetKeyDown(KeyCode.T))
            {
                TestAnimator();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                Debug.Log($"[{name}] TestInt value = {m_TestIntValue}");
                Debug.Log($"[{name}] TestInt value = {m_TestIntValue}");
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                BeginAttack(1);
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                BeginAttack(2);
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SetLayerWeight(1, 0.75f);
            }
        }
    }
}

