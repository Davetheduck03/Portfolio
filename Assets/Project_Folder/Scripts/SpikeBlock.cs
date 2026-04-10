using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Static hazard that instantly kills the active player on contact.
/// Works with both solid colliders (OnCollisionEnter) and trigger
/// colliders (OnTriggerEnter) — set the collider as a trigger in the
/// Inspector if you want the player to overlap the spike rather than
/// collide with it physically.
/// </summary>
public class SpikeBlock : MonoBehaviour
{
    [Tooltip("Fired once each time the active player first touches this spike.\n" +
             "Wire to your respawn / game-over method here.")]
    [SerializeField] private UnityEvent onPlayerKilled;

    // ----------------------------------------------------------------
    //  Solid collider contact
    // ----------------------------------------------------------------

    void OnCollisionEnter(Collision col) => TryKill(col.gameObject);

    // ----------------------------------------------------------------
    //  Trigger collider contact
    // ----------------------------------------------------------------

    void OnTriggerEnter(Collider other) => TryKill(other.gameObject);

    // ----------------------------------------------------------------
    //  Kill logic
    // ----------------------------------------------------------------

    private void TryKill(GameObject obj)
    {
        // Kill active player
        BaseCharacter character = obj.GetComponent<BaseCharacter>();
        if (character != null && character.IsActive)
        {
            onPlayerKilled.Invoke();
            return;
        }

        // Kill enemy
        Enemy enemy = obj.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.Die();
        }
    }
}
