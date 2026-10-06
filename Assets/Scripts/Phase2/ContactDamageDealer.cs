using ProjectLike.Phase1;
using UnityEngine;

namespace ProjectLike.Phase2
{
    /// <summary>Simple test enemy damage so shield/health can be validated in the room.</summary>
    public class ContactDamageDealer : MonoBehaviour
    {
        public float damage = 8f;
        public float interval = .8f;
        float nextDamage;

        void OnCollisionStay2D(Collision2D collision)
        {
            if (Time.time < nextDamage) return;
            var player = collision.collider.GetComponent<PlayerController>();
            if (!player) return;
            player.TakeDamage(damage, transform.position);
            nextDamage = Time.time + interval;
        }
    }
}
