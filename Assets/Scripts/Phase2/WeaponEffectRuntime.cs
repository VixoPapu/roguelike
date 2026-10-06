using ProjectLike.Phase3;
using ProjectLike.Phase12;
using ProjectLike.Phase1;
using ProjectLike.Audio;
using UnityEngine;
using System.Collections.Generic;

namespace ProjectLike.Phase2
{
    /// <summary>Ejecuta poderes definidos por datos sin crear una clase por arma.</summary>
    public static class WeaponEffectRuntime
    {
        public static void Apply(EnemyBrain target, CombatHit hit, float dealtDamage)
        {
            var weapon = hit.weapon;
            if (!target || hit.secondary || !weapon || weapon.SpecialEffects == null) return;
            var primary = true;
            foreach (var effect in weapon.SpecialEffects)
            {
                if (effect == WeaponSpecialEffectType.None) continue;
                var guaranteed = primary && weapon.energyCost > 0f && weapon.rarity >= WeaponRarity.Rare;
                primary = false;
                if (!guaranteed && Random.value > weapon.specialEffectChance) continue;
                var runtime = hit.source ? hit.source.GetComponentInParent<PowerUpRuntime>() : null;
                ApplyEffect(target, hit, dealtDamage, effect, Mathf.Max(.1f, weapon.specialEffectPower) * (runtime && runtime.Has("maestro_elemental") ? 1.45f : 1f));
            }
        }

        static void ApplyEffect(EnemyBrain target, CombatHit hit, float dealt, WeaponSpecialEffectType effect, float power)
        {
            var status = target.GetComponent<ElementalStatusEffects>();
            if (!status) status = target.gameObject.AddComponent<EnemyStatusEffects>();
            if (effect == WeaponSpecialEffectType.Fire) status.Burn(hit.source, 4f, Mathf.Max(2f, dealt * .20f * Mathf.Min(1.5f, power)), 1f);
            else if (effect == WeaponSpecialEffectType.Ice) status.Ice(Mathf.Max(1, Mathf.RoundToInt(power)), Mathf.Max(3, 6 - Mathf.RoundToInt(power)));
            else if (effect == WeaponSpecialEffectType.Shock) status.Shock(hit.source, 2.5f, dealt * .28f * power, Mathf.Clamp(Mathf.RoundToInt(power), 1, 3));
            else if (effect == WeaponSpecialEffectType.Poison) status.Poison(hit.source, 5f, Mathf.Max(.5f, dealt * .09f * power));
            else if (effect == WeaponSpecialEffectType.Curse) status.Curse(hit.source, 3f + power);
            else if (effect == WeaponSpecialEffectType.Bleed) status.Bleed(hit.source, 4f, Mathf.Max(.6f, dealt * .1f * power));
            else if (effect == WeaponSpecialEffectType.Stun) target.Stun(.22f + .18f * power);
            else if (effect == WeaponSpecialEffectType.Wave) Wave(target, hit, dealt * .34f * power);
            else if (effect == WeaponSpecialEffectType.Gravity || effect == WeaponSpecialEffectType.Nova)
            {
                var struck = new HashSet<EnemyBrain>();
                foreach (var collider in Physics2D.OverlapCircleAll(target.transform.position, effect == WeaponSpecialEffectType.Gravity ? 3f : 2.5f))
                {
                    var enemy = collider.GetComponentInParent<EnemyBrain>();
                    if (!enemy || enemy == target || enemy.IsDead || !struck.Add(enemy)) continue;
                    var toward = ((Vector2)target.transform.position - (Vector2)enemy.transform.position).normalized;
                    enemy.ReceiveHit(new CombatHit { damage = dealt * (effect == WeaponSpecialEffectType.Gravity ? .15f : .4f) * power,
                        direction = effect == WeaponSpecialEffectType.Gravity ? toward : -toward, knockback = 6f, source = hit.source, secondary = true });
                    if (effect == WeaponSpecialEffectType.Nova)
                    {
                        var burning = enemy.GetComponent<ElementalStatusEffects>();
                        if (!burning) burning = enemy.gameObject.AddComponent<EnemyStatusEffects>();
                        burning.Burn(hit.source, 3f, Mathf.Max(1f, dealt * .1f), 1f);
                    }
                }
            }
            else if (effect == WeaponSpecialEffectType.EnergySiphon)
            {
                var stats = hit.source ? hit.source.GetComponentInParent<PlayerStats>() : null;
                if (stats) stats.RestoreEnergy(Mathf.Min(2f, .65f * power));
            }
            else if (effect == WeaponSpecialEffectType.LifeSteal)
            {
                var stats = hit.source ? hit.source.GetComponentInParent<PlayerStats>() : null;
                if (stats && dealt > 0f) stats.Heal(Mathf.Min(.5f, dealt * .012f * power));
            }
            else if (effect == WeaponSpecialEffectType.ShieldShred) target.RemoveShield(8f * power);
            else if (effect == WeaponSpecialEffectType.Execution && target.CurrentHealth > 0f && target.CurrentHealth <= target.RuntimeMaxHealth * .3f)
                target.ReceiveHit(new CombatHit { damage = dealt * .65f * power, source = hit.source, secondary = true });
            else if (effect == WeaponSpecialEffectType.ProjectileBreak)
            {
                var projectiles = new HashSet<EnemyProjectile>();
                foreach (var collider in Physics2D.OverlapCircleAll(target.transform.position, 2.2f))
                {
                    var projectile = collider.GetComponentInParent<EnemyProjectile>();
                    if (projectile && projectiles.Add(projectile)) projectile.Dispel();
                }
            }
            CombatVfxPool.Instance.SpawnWeaponProc(target.transform.position, hit.weapon, effect);
            GameSfx.WeaponEffect(target.transform.position, effect);
        }

        static void Wave(EnemyBrain origin, CombatHit hit, float damage)
        {
            var struck = new HashSet<EnemyBrain>();
            foreach (var collider in Physics2D.OverlapCircleAll(origin.transform.position, 2f))
            {
                var enemy = collider.GetComponentInParent<EnemyBrain>();
                if (!enemy || enemy == origin || enemy.IsDead || !struck.Add(enemy)) continue;
                var direction = ((Vector2)enemy.transform.position - (Vector2)origin.transform.position).normalized;
                enemy.ReceiveHit(new CombatHit { damage = damage, direction = direction, knockback = hit.knockback * .45f, source = hit.source, secondary = true });
            }
        }
    }
}
