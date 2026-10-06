using UnityEngine;

namespace ProjectLike.Phase11
{
    public enum ConsumableEffectType
    {
        HealthPotion, EnergyPotion, SpeedPotion, DamagePotion, TemporaryShield,
        Invisibility, CriticalBoost, Invulnerability, Bomb, ThrowingKnives, MagicScroll
    }

    [CreateAssetMenu(menuName = "Project Like/Consumables/Consumable", fileName = "Consumable_")]
    public class ConsumableData : ScriptableObject
    {
        public string displayName = "Consumible";
        [TextArea] public string description;
        public Sprite icon;
        public ConsumableEffectType effectType;
        [Min(0f)] public float magnitude = 5f;
        [Min(0f)] public float duration = 5f;
        [Min(.1f)] public float radius = 3f;
        [Min(1)] public int projectileCount = 3;
        [Min(0)] public int price = 10;
        public Color effectColor = Color.cyan;
    }
}
