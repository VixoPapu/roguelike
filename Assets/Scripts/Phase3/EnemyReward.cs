using ProjectLike.Phase1;
using UnityEngine;

namespace ProjectLike.Phase3
{
    public class EnemyReward : MonoBehaviour
    {
        public int coins; public int experience;
        void Awake() { Destroy(gameObject, 10); }
        void OnTriggerEnter2D(Collider2D other) { var stats = other.GetComponent<PlayerStats>(); if (!stats) return; stats.coins += coins; stats.experience += experience; Destroy(gameObject); }
    }
}
