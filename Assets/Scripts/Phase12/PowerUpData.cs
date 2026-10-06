using ProjectLike.Phase8;
using UnityEngine;

namespace ProjectLike.Phase12
{
    public enum PowerUpCategory { Survival, Physical, Projectiles, Magic, Elemental, Mobility, Critical, OnKill, ReactiveDefense, Economy, Summons, Special, Corrupted }
    public enum PowerUpStackMode { Auto, Stackable, Unique }

    [CreateAssetMenu(menuName = "Project Like/Power Ups/Power Up", fileName = "PowerUp_")]
    public class PowerUpData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName;
        [TextArea(2, 5)] public string effectDescription;
        public PowerUpCategory category;
        public LootRarity rarity;
        [Min(0)] public int price = 10;
        [Header("Stacking")]
        [Tooltip("Auto acumula mejoras numéricas y trata los efectos binarios como únicos.")]
        public PowerUpStackMode stackMode = PowerUpStackMode.Auto;
        [Min(1)] public int maximumStacks = 99;

        [Header("Editable icon")]
        [Tooltip("Arrastra aquí cualquier sub-sprite de Assets/Textures/Items/PowerUpIcons.png.")]
        public Sprite icon;
        [Tooltip("Sheet original. Las coordenadas se cuentan desde arriba a la izquierda.")]
        public Texture2D iconSheet;
        [Range(0, 15)] public int iconColumn;
        [Range(0, 136)] public int iconRow;

        [Header("Ability audio")]
        public AudioClip abilityStartSound;
        public AudioClip abilityEndSound;

        [Header("Gameplay data")]
        [Tooltip("ID estable para conectar comportamientos especiales sin depender del nombre visible.")]
        public string effectId;
        public LootStatModifiers modifiers;
        public float maxEnergy;
        public float maxShield;
        public float energyRegenerationPercent;
        public float meleeDamagePercent;
        public float meleeAttackSpeedPercent;
        public float meleeCriticalChance;
        public float magicDamagePercent;
        public float criticalDamagePercent;
        public float knockbackPercent;
        public float dashCooldownPercent;
        public float dashDistancePercent;
        public float shopDiscountPercent;

        public bool IsStackable => stackMode == PowerUpStackMode.Stackable || stackMode == PowerUpStackMode.Auto && HasNumericStackValue;
        public bool IsUnique => !IsStackable;
        bool HasNumericStackValue => meleeAttackSpeedPercent != 0f || meleeCriticalChance != 0f || maxShield != 0f || maxEnergy != 0f || energyRegenerationPercent != 0f || meleeDamagePercent != 0f || magicDamagePercent != 0f || criticalDamagePercent != 0f || knockbackPercent != 0f || dashCooldownPercent != 0f || dashDistancePercent != 0f || shopDiscountPercent != 0f || modifiers.maxHealth != 0f || modifiers.moveSpeed != 0f || modifiers.damage != 0f || modifiers.attackSpeed != 0f || modifiers.criticalChance != 0f || modifiers.defense != 0f || modifiers.projectileSpeed != 0f || modifiers.range != 0f || modifiers.cooldownReduction != 0f || modifiers.luck != 0f;
    }
}
