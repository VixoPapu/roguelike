using System;
using ProjectLike.Phase2;
using ProjectLike.Phase11;
using ProjectLike.Phase12;
using UnityEngine;

namespace ProjectLike.Phase8
{
    [CreateAssetMenu(menuName = "Project Like/Loot/Loot Item", fileName = "Loot_")]
    public class LootData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Loot";
        [TextArea] public string description;
        public Sprite icon;
        public Sprite worldSprite;
        public LootType lootType;
        public LootRarity rarity;
        [Header("Value")]
        [Min(0)] public int basePrice = 10;
        [Min(0)] public float effectMagnitude = 1;
        public WeaponData weapon;
        public ConsumableData consumable;
        public PowerUpData powerUp;
        [Tooltip("Usado al soltar un arma equipada: evita volver a multiplicar sus estadísticas.")] public bool preserveWeaponStats;
        public LootStatModifiers modifiers;
        [TextArea] public string specialEffect;
        public bool autoPickup;

        public bool AllowedWorldDrop => !powerUp && lootType != LootType.Upgrade && lootType != LootType.Special;

        public float StatMultiplier => LootRarityRules.StatMultiplier(rarity);
        public float DropMultiplier => LootRarityRules.DropMultiplier(rarity);
        public int Price => Mathf.RoundToInt(basePrice * LootRarityRules.PriceMultiplier(rarity));
        public Color RarityColor => LootRarityRules.Color(rarity);
    }

    public enum LootType { Coins, Weapon, Consumable, Upgrade, Key, Special }
    public enum LootRarity { Common, Uncommon, Rare, Epic, Legendary, Corrupted }

    [Serializable]
    public struct LootStatModifiers
    {
        public float maxHealth;
        public float moveSpeed;
        public float damage;
        public float attackSpeed;
        public float criticalChance;
        public float defense;
        public float projectileSpeed;
        public float range;
        public float cooldownReduction;
        public float luck;
    }

    public static class LootRarityRules
    {
        public static float StatMultiplier(LootRarity rarity) => rarity == LootRarity.Common ? 1f : rarity == LootRarity.Uncommon ? 1.2f : rarity == LootRarity.Rare ? 1.5f : rarity == LootRarity.Epic ? 1.9f : rarity == LootRarity.Legendary ? 2.5f : 3f;
        public static float PriceMultiplier(LootRarity rarity) => rarity == LootRarity.Common ? 1f : rarity == LootRarity.Uncommon ? 1.6f : rarity == LootRarity.Rare ? 2.7f : rarity == LootRarity.Epic ? 5f : rarity == LootRarity.Legendary ? 10f : 4.5f;
        public static float DropMultiplier(LootRarity rarity) => rarity == LootRarity.Common ? 1f : rarity == LootRarity.Uncommon ? .55f : rarity == LootRarity.Rare ? .25f : rarity == LootRarity.Epic ? .09f : rarity == LootRarity.Legendary ? .025f : .04f;
        public static Color Color(LootRarity rarity) => rarity == LootRarity.Common ? new Color(.82f, .82f, .82f) : rarity == LootRarity.Uncommon ? new Color(.24f, .9f, .35f) : rarity == LootRarity.Rare ? new Color(.18f, .55f, 1f) : rarity == LootRarity.Epic ? new Color(.72f, .25f, 1f) : rarity == LootRarity.Legendary ? new Color(1f, .65f, .08f) : new Color(.82f, .05f, .28f);
    }
}
