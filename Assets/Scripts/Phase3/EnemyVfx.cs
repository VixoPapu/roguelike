using ProjectLike.Phase2;
using UnityEngine;

namespace ProjectLike.Phase3
{
    public static class EnemyVfx
    {
        public static void Telegraph(Vector2 position, Vector2 direction, float duration)
        {
            CombatVfxPool.Instance.SpawnEnemyAbilityBurst(position, EnemyAttackStyle.Pounce, new Color(1f, .18f, .04f));
        }
        public static void Teleport(Vector2 position) { CombatVfxPool.Instance.SpawnImpact(position, new CombatHit { damage = 18, direction = UnityEngine.Random.insideUnitCircle.normalized }, ImpactSurface.Magic); }
        public static void Support(Vector2 position) { CombatVfxPool.Instance.SpawnImpact(position, new CombatHit { damage = 10, direction = Vector2.up }, ImpactSurface.Magic); }
    }
}
