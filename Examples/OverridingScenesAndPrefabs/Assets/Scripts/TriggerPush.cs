using UnityEngine;

/// <summary>
/// This helper class is used to push a player away from a rotating body.
/// <see cref="CharacterController"/>s without a <see cref="Rigidbody"/> don't
/// handle collision with rotating bodies. This simulates a "collision".
/// </summary>
public class TriggerPush : MonoBehaviour
{    
    public enum RightOrLeft
    {
        Right,
        Left
    }

    [Tooltip("Determines if this trigger will push the player to the left or right of the root transform")]
    public RightOrLeft PushDirection;

    private TagHandle m_TagHandle;

    private void Awake()
    {
        m_TagHandle = TagHandle.GetExistingTag("Player");
    }

    private void PushObject(Collider other, bool isInside = false)
    {
        var nonRigidPlayerMover = other.GetComponent<MoverScriptNoRigidbody>();
        if (nonRigidPlayerMover != null && nonRigidPlayerMover.CanCommitToTransform)
        {
            // We determine the direction to push and if within a trigger we push a little more to prevent from
            // completely clipping through the object.
            var direction = (PushDirection == RightOrLeft.Right ? 1.0f : -1.0f) * (isInside ? 1.75f : 1.0f);
            nonRigidPlayerMover.PushAwayFrom(transform.parent.right * direction);
        }
    }

    /// <summary>
    /// Pushes the player away from the object
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(m_TagHandle))
        {
            return;
        }
        PushObject(other);
    }

    /// <summary>
    /// When the trigger is in a "stay" state, we need to signal that
    /// the amount to "push away" should be increased.
    /// </summary>
    /// <param name="other"></param>
    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(m_TagHandle))
        {
            return;
        }
        PushObject(other, true);
    }
}
