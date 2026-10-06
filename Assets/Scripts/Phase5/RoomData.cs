using System;
using ProjectLike.Phase3;
using ProjectLike.Phase8;
using UnityEngine;

namespace ProjectLike.Phase5
{
    public enum RoomType { Combat, DifficultCombat, Elite, Treasure, Shop, NPC, Event, Challenge, Secret, Rest, Boss, Entrance, Exit }
    public enum RoomVisualVariant { Dungeon, Crypt, Cave, Forest, Temple, Shop, BossArena, Secret }
    public enum RoomState { Dormant, Entered, Combat, Cleared }
    public enum RoomDirection { North, South, East, West }

    [Serializable]
    public struct RoomEnemySpawn
    {
        public EnemyData enemy;
        public Vector2 localPosition;
        [Min(1)] public int count;
        [Min(0)] public float spread;
    }

    [CreateAssetMenu(menuName = "Project Like/Rooms/Room Data", fileName = "Room_")]
    public class RoomData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Combat Room";
        [TextArea] public string description;
        public RoomType roomType = RoomType.Combat;
        public RoomVisualVariant visualVariant = RoomVisualVariant.Dungeon;
        [Header("Combat")]
        public bool closeDoorsOnCombat = true;
        public RoomEnemySpawn[] enemySpawns;
        [Header("Waves")]
        [Range(1, 3)] public int minWaves = 1;
        [Range(1, 3)] public int maxWaves = 3;
        [Min(1)] public int minEnemiesPerWave = 6;
        [Min(1)] public int maxEnemiesPerWave = 12;
        [Min(.1f)] public float minimumSpawnInterval = .1f;
        [Min(.1f)] public float maximumSpawnInterval = .7f;
        [Min(.1f)] public float spawnTelegraphTime = .8f;
        [Min(0f)] public float timeBetweenWaves = 1.1f;
        [Header("Dynamic difficulty")]
        [Min(1)] public int worldNumber = 1;
        [Range(0f, 1f)] public float difficultyProgress;
        [Min(.1f)] public float enemyHealthMultiplier = 1f;
        [Min(.1f)] public float enemyDamageMultiplier = 1f;
        [Min(.1f)] public float enemySpeedMultiplier = 1f;
        [Min(.1f)] public float enemyCooldownMultiplier = 1f;
        [Min(0)] public int patternBonus;
        [Range(0f, 1f)] public float eliteMixChance;
        [Header("Completion")]
        [Range(0, 1)] public float rewardChance = 1f;
        [Tooltip("Probabilidad independiente de loot suelto al completar la sala.")]
        [Range(0, 1)] public float looseLootChance = .65f;
        public GameObject rewardPrefab;
        public LootTable lootTable;
        [Min(1)] public int lootRolls = 1;

        public bool RequiresCombat => roomType == RoomType.Combat || roomType == RoomType.DifficultCombat || roomType == RoomType.Elite || roomType == RoomType.Challenge || roomType == RoomType.Boss;
    }
}
