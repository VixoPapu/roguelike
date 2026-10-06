using System.Collections;
using System.Collections.Generic;
using ProjectLike.Phase1;
using ProjectLike.Phase2;
using ProjectLike.Phase3;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectLike.Audio;

namespace ProjectLike.Phase11
{
    public class PlayerConsumables : MonoBehaviour
    {
        [Min(1)] public int capacity = 8;
        public List<ConsumableData> items = new List<ConsumableData>();
        public int Count => items.Count;
        public ConsumableData Current => items.Count > 0 ? items[0] : null;

        PlayerStats stats;
        PlayerController controller;
        int invisibilityLayers;

        void Awake()
        {
            stats = GetComponent<PlayerStats>();
            controller = GetComponent<PlayerController>();
        }

        void Update()
        {
            var use = ProjectLike.Online.CoopInput.For(gameObject).Down(ProjectLike.Online.CoopButtons.Consumable);
            if (use && Time.timeScale > 0f) UseNext();
        }

        public bool CanAdd(ConsumableData item, int amount = 1) => item && amount > 0 && items.Count + amount <= capacity && (!Current || Current == item);

        public bool Add(ConsumableData item)
        {
            if (!CanAdd(item)) return false;
            items.Add(item);
            return true;
        }
        public void ResetForNewRun() { StopAllCoroutines(); items.Clear(); invisibilityLayers = 0; if (controller) controller.SetInvisible(false); }

        public bool UseNext()
        {
            if (items.Count == 0 || !items[0]) return false;
            var item = items[0];
            items.RemoveAt(0);
            Apply(item);
            GameSfx.Play(item.effectType == ConsumableEffectType.Bomb ? SfxCue.Explosion : SfxCue.Consumable, transform.position, .78f);
            GetComponent<ProjectLike.Phase12.PowerUpRuntime>()?.OnConsumableUsed();
            var hud = FindAnyObjectByType<PixelHUDCanvas>();
            if (hud) hud.ShowFeedback("Usado: " + item.displayName + "  (" + Count + " restantes)", item.effectColor);
            return true;
        }

        void Apply(ConsumableData item)
        {
            var power = Mathf.Max(0f, item.magnitude);
            switch (item.effectType)
            {
                case ConsumableEffectType.HealthPotion: stats.Heal(power); break;
                case ConsumableEffectType.EnergyPotion: stats.RestoreEnergy(power); break;
                case ConsumableEffectType.SpeedPotion: StartCoroutine(TemporaryStat(item.duration, () => stats.moveSpeed += power, () => stats.moveSpeed -= power)); break;
                case ConsumableEffectType.DamagePotion: StartCoroutine(TemporaryStat(item.duration, () => stats.damage += power, () => stats.damage -= power)); break;
                case ConsumableEffectType.CriticalBoost: StartCoroutine(TemporaryStat(item.duration, () => stats.criticalChance = Mathf.Clamp01(stats.criticalChance + power), () => stats.criticalChance = Mathf.Clamp01(stats.criticalChance - power))); break;
                case ConsumableEffectType.TemporaryShield: StartCoroutine(TemporaryShield(power, item.duration)); break;
                case ConsumableEffectType.Invisibility: StartCoroutine(Invisibility(item.duration)); break;
                case ConsumableEffectType.Invulnerability: controller.GrantInvulnerability(item.duration); break;
                case ConsumableEffectType.Bomb: Explode(item); break;
                case ConsumableEffectType.ThrowingKnives: FirePattern(item, Mathf.Max(1, item.projectileCount), 18f); break;
                case ConsumableEffectType.MagicScroll: FirePattern(item, Mathf.Max(6, item.projectileCount), 360f); break;
            }
            CombatVfxPool.Instance.SpawnEnemyAbilityBurst(transform.position, EnemyAttackStyle.ArcaneBurst, item.effectColor);
        }

        IEnumerator TemporaryStat(float duration, System.Action begin, System.Action end)
        {
            begin();
            yield return new WaitForSeconds(Mathf.Max(.1f, duration));
            end();
        }

        IEnumerator TemporaryShield(float amount, float duration)
        {
            var baseline = stats.CurrentShield;
            stats.maxShield += amount;
            stats.AddShield(amount);
            yield return new WaitForSeconds(Mathf.Max(.1f, duration));
            stats.RemoveShield(Mathf.Max(0f, stats.CurrentShield - baseline));
            stats.maxShield = Mathf.Max(0f, stats.maxShield - amount);
        }

        IEnumerator Invisibility(float duration)
        {
            invisibilityLayers++;
            controller.SetInvisible(true);
            yield return new WaitForSeconds(Mathf.Max(.1f, duration));
            invisibilityLayers--;
            if (invisibilityLayers <= 0) controller.SetInvisible(false);
        }

        void Explode(ConsumableData item)
        {
            foreach (var hit in Physics2D.OverlapCircleAll(transform.position, item.radius))
            {
                var enemy = hit.GetComponentInParent<EnemyBrain>();
                if (!enemy || enemy.IsDead) continue;
                var direction = ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized;
                enemy.ReceiveHit(new CombatHit { damage = item.magnitude * ProjectLike.Phase12.TimeStopAbility.DamageMultiplierFor(gameObject), direction = direction, knockback = 7f, source = gameObject });
            }
        }

        void FirePattern(ConsumableData item, int count, float arc)
        {
            var aim = controller ? controller.AimDirection : Vector2.right;
            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.weaponName = item.displayName;
            weapon.type = WeaponType.Staff;
            weapon.damage = item.magnitude;
            weapon.projectile = true;
            weapon.projectileSpeed = 12f;
            weapon.range = 8f;
            weapon.knockback = 2f;
            weapon.sprite = item.icon;
            weapon.elemental = item.effectType == ConsumableEffectType.MagicScroll;
            for (var i = 0; i < count; i++)
            {
                var angle = arc >= 360f ? i * 360f / count : count == 1 ? 0f : Mathf.Lerp(-arc * .5f, arc * .5f, i / (float)(count - 1));
                var direction = (Vector2)(Quaternion.Euler(0f, 0f, angle) * aim);
                var projectile = new GameObject(item.displayName + " Projectile");
                projectile.transform.position = transform.position + (Vector3)direction * .4f;
                projectile.AddComponent<CombatProjectile>().Setup(weapon, direction, Mathf.Max(.1f, stats.damage / 10f) * ProjectLike.Phase12.TimeStopAbility.DamageMultiplierFor(gameObject), gameObject, stats.criticalChance);
            }
        }
    }
}
