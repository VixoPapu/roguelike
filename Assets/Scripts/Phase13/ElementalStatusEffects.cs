using System.Collections;
using System.Collections.Generic;
using ProjectLike.Phase1;
using ProjectLike.Phase2;
using ProjectLike.Phase3;
using UnityEngine;

namespace ProjectLike.Phase12
{
    public enum ElementalStatusType { Fire, Ice, Poison, Electric, Bleed, Curse }

    /// <summary>Estados universales para jugador y enemigos.</summary>
    [DisallowMultipleComponent]
    public class ElementalStatusEffects : MonoBehaviour
    {
        EnemyBrain enemy;
        PlayerController player;
        PlayerStats playerStats;
        GameObject source;
        float burnUntil, poisonUntil, bleedUntil, chillUntil, shockUntil, curseUntil, frozenUntil, nextTick;
        float burnDamage, poisonDamage, bleedDamage;
        int iceStacks, bleedStacks;
        float nextStatusVisual;

        public bool Burning => Time.time < burnUntil;
        public bool Poisoned => Time.time < poisonUntil;
        public bool Bleeding => Time.time < bleedUntil;
        public bool Cursed => Time.time < curseUntil;
        public bool IsFrozen => Time.time < frozenUntil;
        public bool Chilled => Time.time < chillUntil;
        public bool Electrified { get => Time.time < shockUntil; set => shockUntil = value ? Time.time + 3f : 0f; }
        public float SpeedMultiplier => IsFrozen ? 0f : (Time.time < chillUntil ? .68f : 1f) * (Cursed ? .82f : 1f);
        public float DamageTakenMultiplier => Cursed ? 1.2f : 1f;

        void Awake() { enemy = GetComponent<EnemyBrain>(); player = GetComponent<PlayerController>(); playerStats = GetComponent<PlayerStats>(); }

        public void Burn(GameObject owner, float duration, float damage, float tickDamageScale = .35f)
        {
            if (!Burning) burnDamage = 0f;
            source = owner;
            var eternal = owner && owner.GetComponent<PowerUpRuntime>()?.Has("fuego_eterno") == true;
            burnUntil = Mathf.Max(burnUntil, Time.time + duration * (eternal ? 1.7f : 1f));
            burnDamage = Mathf.Max(burnDamage, damage * tickDamageScale);
        }

        public void Poison(GameObject owner, float duration, float damage) { source = owner; poisonUntil = Mathf.Max(poisonUntil, Time.time + duration); poisonDamage = Mathf.Max(poisonDamage, damage); }
        public void Bleed(GameObject owner, float duration, float damage) { source = owner; bleedUntil = Mathf.Max(bleedUntil, Time.time + duration); bleedStacks = Mathf.Min(8, bleedStacks + 1); bleedDamage = Mathf.Max(bleedDamage, damage); }

        public void Ice(int stacks, int freezeAt)
        {
            if (Time.time >= chillUntil && !IsFrozen) iceStacks = 0;
            iceStacks += Mathf.Max(1, stacks); chillUntil = Mathf.Max(chillUntil, Time.time + 2.5f);
            if (enemy) enemy.ExternalSpeedMultiplier = .68f;
            if (iceStacks < Mathf.Max(1, freezeAt) || IsFrozen) return;
            frozenUntil = Time.time + (enemy ? 1.5f : 1f); iceStacks = 0;
            if (enemy) enemy.Stun(1.5f);
            StartCoroutine(Thaw());
        }

        public void Shock(GameObject owner, float duration, float damage, int jumps = 1, HashSet<EnemyBrain> struck = null)
        {
            source = owner; shockUntil = Mathf.Max(shockUntil, Time.time + duration);
            if (!enemy || jumps <= 0 || damage <= 0f) return;
            if (struck == null) struck = new HashSet<EnemyBrain>();
            struck.Add(enemy);
            EnemyBrain nearest = null; var nearestDistance = 3.2f;
            foreach (var candidate in FindObjectsByType<EnemyBrain>())
            {
                if (!candidate || candidate == enemy || candidate.IsDead || struck.Contains(candidate)) continue;
                var distance = Vector2.Distance(transform.position, candidate.transform.position);
                if (distance >= nearestDistance) continue; nearestDistance = distance; nearest = candidate;
            }
            if (!nearest) return;
            CombatVfxPool.Instance.SpawnChainLightning(transform.position, nearest.transform.position);
            nearest.ReceiveHit(new CombatHit { damage = damage, direction = ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized, source = owner, secondary = true });
            var chained = nearest.GetComponent<ElementalStatusEffects>(); if (!chained) chained = nearest.gameObject.AddComponent<EnemyStatusEffects>();
            chained.Shock(owner, duration * .75f, damage * .65f, jumps - 1, struck);
        }

        public void Curse(GameObject owner, float duration) { source = owner; curseUntil = Mathf.Max(curseUntil, Time.time + duration); }

        IEnumerator Thaw()
        {
            while (Time.time < frozenUntil) yield return null;
            if (enemy) enemy.ExternalSpeedMultiplier = Time.time < chillUntil ? .68f : 1f;
        }

        void Update()
        {
            if (enemy && enemy.IsDead) return;
            if (enemy && Time.time >= nextStatusVisual && (Burning || Poisoned || IsFrozen || Electrified || Bleeding || Cursed))
            {
                nextStatusVisual = Time.time + .22f;
                var color = Burning ? new Color(1f, .27f, .035f) : IsFrozen ? new Color(.35f, .85f, 1f) : Poisoned ? new Color(.35f, 1f, .14f) : Electrified ? new Color(.4f, .7f, 1f) : Bleeding ? new Color(1f, .08f, .18f) : new Color(.75f, .25f, 1f);
                CombatVfxPool.Instance.SpawnStatusMotes(transform.position, color, Burning);
            }
            if (enemy && Time.time >= chillUntil && !IsFrozen) enemy.ExternalSpeedMultiplier = 1f;
            if (Time.time >= bleedUntil) bleedStacks = 0;
            if (Time.time < nextTick) return;
            nextTick = Time.time + .75f;
            var damage = (Burning ? burnDamage : 0f) + (Poisoned ? poisonDamage : 0f) + (Bleeding ? bleedDamage * Mathf.Max(1, bleedStacks) : 0f);
            if (damage <= 0f) return;
            if (enemy) enemy.ReceiveHit(new CombatHit { damage = damage, direction = Vector2.zero, source = source, secondary = true });
            else if (player) player.TakeDamage(damage, source ? source.transform.position : transform.position + Vector3.down);
            else if (playerStats) playerStats.ReceiveDamage(damage);
        }
    }
}
