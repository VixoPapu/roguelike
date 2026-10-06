using System;
using UnityEngine;

namespace ProjectLike.Phase3
{
    [CreateAssetMenu(menuName = "Project Like/Enemies/Enemy Data", fileName = "Enemy_")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")] public string enemyName = "Enemy"; [TextArea] public string description; public Sprite sprite; public Sprite[] animationFrames; [Min(1)] public float animationFramesPerSecond = 7; public RuntimeAnimatorController animations; public EnemyBehaviour[] behaviours;
        [Header("Stats")] public float maxHealth = 35; public float damage = 3; public float moveSpeed = 2.5f; public float defense; [Range(0, 1)] public float resistance; public float knockbackResistance;
        [Header("Awareness / attack")] public float detectionRange = 7; public float attackRange = 1.1f; public float preferredRange = 4; public float attackCooldown = 1; public float recoveryTime = .25f;
        [Header("Abilities")] public EnemyAttackStyle attackStyle = EnemyAttackStyle.Basic; public Color effectColor = new Color(1f, .25f, .08f); public float projectileSpeed = 7; public int projectileCount = 1; public float chargeTelegraph = .55f; public float chargeSpeed = 9; public float chargeDistance = 5; public float teleportCooldown = 4; public float summonCooldown = 6; public EnemyData summonData;
        [Header("Presentation")] [Min(.1f)] public float visualScale = 1.2f; public AudioClip alertSound; public AudioClip attackSound; public AudioClip hurtSound; public AudioClip deathSound;
        [Header("Drops")] public EnemyLootEntry[] loot; public int experienceReward = 2; public int coinReward = 1; [Min(1)] public int minCoins = 1; [Min(1)] public int maxCoins = 3; [Min(1)] public int minEnergy = 6; [Min(1)] public int maxEnergy = 14;
    }

    public enum EnemyBehaviour { Chaser, Ranged, Charger, Turret, Teleport, Summoner, Support, Evasive }
    public enum EnemyAttackStyle { Basic, CrescentSlash, TripleClaw, HeavySmash, FireFan, ArcaneBurst, ToxicVolley, BoneLance, BlinkStrike, Pounce }
    public enum EnemyState { Idle, Alert, Chase, Attack, Recovery, Stun, Damage, Death }
    [Serializable]
    public struct EnemyLootEntry
    {
        public GameObject prefab;
        [Range(0, 1)] public float chance;
        public int minAmount;
        public int maxAmount;
    }
}
