using ProjectLike.Phase1;
using ProjectLike.Phase2;
using ProjectLike.Phase5;
using ProjectLike.Phase6;
using ProjectLike.Phase12;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectLike.Audio;

namespace ProjectLike.Phase3
{
    public class EnemyBrain : MonoBehaviour, ICombatTarget
    {
        public EnemyData data;
        public EnemyState State { get; private set; } = EnemyState.Idle;
        public float CurrentHealth { get; private set; }
        public float CurrentShield { get; private set; }
        public RoomController OwningRoom { get; private set; }
        public bool IsDead => dead;
        public float RuntimeMaxHealth => runtimeMaxHealth;
        public bool IsMoving => !dead && (charging || movement.sqrMagnitude > .01f);
        public float MovementIntensity => dead ? 0f : charging ? 1f : Mathf.Clamp01(movement.magnitude);
        public float ExternalSpeedMultiplier { get; set; } = 1f;
        public float KnockbackSpeed => knockbackVelocity.magnitude;

        Rigidbody2D body; SpriteRenderer spriteRenderer; Animator animator; AudioSource audioSource; Transform player;
        Vector2 movement, chargeDirection, knockbackVelocity; float nextAttack, recoveryUntil, stunUntil, nextTeleport, nextSummon, nextSupport, chargeUntil, telegraphUntil, nextBossPattern, nextStrafeSwap;
        bool charging, telegraphing, chargeHitPlayer, dead;
        float healthScale = 1f, damageScale = 1f, moveScale = 1f, cooldownScale = 1f, runtimeMaxHealth;
        int patternBonus;
        GameObject lastAttacker;
        EnemyBrain summoner;
        bool miniBoss;
        int bossWorld = 1, bossPatternIndex, strafeSign = 1;
        readonly List<EnemyBrain> ownedSummons = new List<EnemyBrain>();

        void Awake()
        {
            body = GetComponent<Rigidbody2D>(); if (!body) body = gameObject.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.freezeRotation = true; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (!GetComponent<Collider2D>()) gameObject.AddComponent<CircleCollider2D>();
            PlayerEnemyCollisionRules.IgnoreForEnemy(this);
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(); if (!spriteRenderer) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            animator = GetComponentInChildren<Animator>(); audioSource = GetComponent<AudioSource>(); if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
            strafeSign = UnityEngine.Random.value < .5f ? -1 : 1;
            nextStrafeSwap = Time.time + UnityEngine.Random.Range(1.2f, 2.4f);
            ApplyData();
            if (!GetComponent<EnemyWalkVisual>()) gameObject.AddComponent<EnemyWalkVisual>();
            if (!GetComponent<EnemyHealthBar>()) gameObject.AddComponent<EnemyHealthBar>();
        }
        // Los enemigos creados desde spawners asignan el ScriptableObject justo
        // después de AddComponent; esta segunda inicialización evita que nazcan
        // con 0 de vida por el orden Awake -> asignación de datos.
        void Start()
        {
            if (data && CurrentHealth <= 0) ApplyData();
        }
        public void Configure(EnemyData template, RoomController room = null, float healthMultiplier = 1f, float damageMultiplier = 1f, float moveMultiplier = 1f, float cooldownMultiplier = 1f, int extraPatternProjectiles = 0)
        {
            data = template; OwningRoom = room; healthScale = Mathf.Max(.1f, healthMultiplier); damageScale = Mathf.Max(.1f, damageMultiplier); moveScale = Mathf.Max(.1f, moveMultiplier); cooldownScale = Mathf.Max(.1f, cooldownMultiplier); patternBonus = Mathf.Max(0, extraPatternProjectiles);
            miniBoss = room && room.data && room.data.roomType == RoomType.Boss;
            bossWorld = miniBoss ? Mathf.Clamp(room.data.worldNumber, 1, 5) : 1;
            nextBossPattern = Time.time + UnityEngine.Random.Range(1.1f, 1.55f);
            nextTeleport = Time.time + Mathf.Max(1.8f, template ? template.teleportCooldown * .7f : 3f);
            nextSummon = Time.time + Mathf.Max(2.5f, template ? template.summonCooldown * .65f : 4f);
            nextSupport = Time.time + 2.2f;
            if (room && room.data && template && template.damage > 0f)
            {
                var budget = 2f + (room.data.worldNumber - 1) * .3f + room.data.difficultyProgress * .2f;
                if (room.data.roomType == ProjectLike.Phase5.RoomType.Boss) budget += 1f;
                damageScale = Mathf.Min(damageScale, budget / template.damage);
            }
            ApplyData();
        }
        public void AssignRoom(RoomController room) { if (!OwningRoom) OwningRoom = room; }
        void ApplyData()
        {
            if (!data) return;
            // También se necesita en edición, después de una recarga de dominio:
            // los campos privados de Awake no existen hasta entrar en Play.
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (!spriteRenderer) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            runtimeMaxHealth = data.maxHealth * healthScale;
            CurrentHealth = runtimeMaxHealth;
            transform.localScale = Vector3.one * data.visualScale;
            if (spriteRenderer && data.sprite) spriteRenderer.sprite = data.sprite;
            if (animator && data.animations) animator.runtimeAnimatorController = data.animations;
            gameObject.name = data.enemyName;
        }
        void Update()
        {
            if (dead || !data) return;
            if (ProjectLike.Online.CoopSession.IsHost) player = ProjectLike.Online.CoopSession.Instance.NearestPlayer(transform.position);
            else if (!player) player = GameObject.FindWithTag("Player")?.transform ?? GameObject.Find("Player")?.transform;
            if (!player) return;
            var playerController = player.GetComponent<PlayerController>();
            if (playerController && playerController.IsInvisible) { SetState(EnemyState.Idle); movement = Vector2.zero; return; }
            var toPlayer = (Vector2)(player.position - transform.position); var distance = toPlayer.magnitude; var direction = distance > .01f ? toPlayer / distance : Vector2.right;
            if (spriteRenderer && direction.x != 0) spriteRenderer.flipX = direction.x < 0;
            if (Time.time < stunUntil) { SetState(EnemyState.Stun); movement = Vector2.zero; return; }
            if (Time.time < recoveryUntil) { SetState(EnemyState.Recovery); movement = Vector2.zero; return; }
            var roomCombatActive = OwningRoom && OwningRoom.State == RoomState.Combat;
            if (!roomCombatActive && distance > data.detectionRange) { SetState(EnemyState.Idle); movement = Vector2.zero; return; }
            if (State == EnemyState.Idle) { SetState(EnemyState.Alert); Play(data.alertSound); }
            if (telegraphing) { SetState(EnemyState.Attack); movement = Vector2.zero; if (Time.time >= telegraphUntil) StartCharge(); return; }
            if (charging) { SetState(EnemyState.Attack); if (Time.time >= chargeUntil) { charging = false; recoveryUntil = Time.time + data.recoveryTime; } return; }
            if (TryMiniBossPattern(direction)) return;
            if (HandleSpecialAbilities(direction)) { movement = Vector2.zero; recoveryUntil = Time.time + .22f; return; }
            if (Time.time >= nextAttack && ShouldAttack(distance)) { Attack(direction); return; }
            movement = ChooseMovement(direction, distance); SetState(movement.sqrMagnitude > .01f ? EnemyState.Chase : EnemyState.Alert);
        }
        void FixedUpdate()
        {
            if (dead || !data) return;
            var start = body.position;
            var chaseSpeed = data.moveSpeed * moveScale * ExternalSpeedMultiplier;
            if (miniBoss) chaseSpeed = Mathf.Max(chaseSpeed, 1.75f + bossWorld * .07f);
            var controlledVelocity = charging
                ? chargeDirection * Mathf.Max(data.chargeSpeed, miniBoss ? 8.5f : 0f)
                : movement * chaseSpeed;
            var destination = body.position + (controlledVelocity + knockbackVelocity) * Time.fixedDeltaTime;
            if (OwningRoom) destination = OwningRoom.ClampInside(destination);
            if (charging) CheckChargeHit(start, destination);
            body.linearVelocity = Vector2.zero;
            body.MovePosition(destination);
            // MovePosition controla por completo a la IA. El impulso debe formar
            // parte de ese movimiento o queda oculto hasta que el enemigo muere.
            knockbackVelocity = Vector2.MoveTowards(knockbackVelocity, Vector2.zero, 18f * Time.fixedDeltaTime);
            if (animator) animator.SetFloat("Speed", movement.magnitude);
        }
        bool Has(EnemyBehaviour behaviour) { if (data.behaviours == null) return false; foreach (var item in data.behaviours) if (item == behaviour) return true; return false; }
        bool UsesRangedAttack() => Has(EnemyBehaviour.Turret) || Has(EnemyBehaviour.Ranged) || Has(EnemyBehaviour.Evasive) || Has(EnemyBehaviour.Support);
        bool ShouldAttack(float distance) { if (UsesRangedAttack()) return distance <= data.preferredRange + .75f; if (Has(EnemyBehaviour.Charger)) return distance <= data.chargeDistance; return distance <= data.attackRange + .15f; }
        Vector2 ChooseMovement(Vector2 direction, float distance)
        {
            if (Time.time >= nextStrafeSwap) { strafeSign *= -1; nextStrafeSwap = Time.time + UnityEngine.Random.Range(1.15f, 2.35f); }
            if (miniBoss)
            {
                var radial = distance < 2.35f ? -direction : distance > 5.3f ? direction : Vector2.zero;
                var orbit = Vector2.Perpendicular(direction) * strafeSign;
                return Vector2.ClampMagnitude(orbit * .78f + radial * .72f, 1f);
            }
            if (Has(EnemyBehaviour.Turret)) return Vector2.zero;
            if (UsesRangedAttack())
            {
                if (distance < data.preferredRange * .8f) return -direction;
                if (distance > data.preferredRange * 1.15f) return direction;
                return Vector2.Perpendicular(direction) * strafeSign;
            }
            return Has(EnemyBehaviour.Chaser) || Has(EnemyBehaviour.Charger) || Has(EnemyBehaviour.Support) || Has(EnemyBehaviour.Summoner) ? direction : Vector2.zero;
        }
        void Attack(Vector2 direction)
        {
            if (Has(EnemyBehaviour.Charger)) { BeginChargeTelegraph(direction); return; }
            SetState(EnemyState.Attack); nextAttack = Time.time + data.attackCooldown * cooldownScale; recoveryUntil = Time.time + data.recoveryTime * cooldownScale; Play(data.attackSound, data.attackStyle <= EnemyAttackStyle.HeavySmash ? SfxCue.EnemyAttack : SfxCue.EnemySpecial); if (animator) animator.SetTrigger("Attack");
            if (UsesRangedAttack()) FireProjectiles(direction);
            else PerformMeleeAttack(direction);
        }
        void BeginChargeTelegraph(Vector2 direction) { nextAttack = Time.time + Mathf.Max(1.7f, data.attackCooldown * cooldownScale); telegraphing = true; chargeDirection = direction; telegraphUntil = Time.time + Mathf.Max(.32f, data.chargeTelegraph * Mathf.Lerp(1.1f, .88f, Mathf.Clamp01(patternBonus * .5f))); EnemyVfx.Telegraph(transform.position, direction, Mathf.Max(.35f, data.chargeTelegraph)); }
        void StartCharge() { telegraphing = false; charging = true; chargeHitPlayer = false; chargeUntil = Time.time + data.chargeDistance / Mathf.Max(1, data.chargeSpeed); CombatVfxPool.Instance.SpawnChargerSlash(transform.position, chargeDirection, data.effectColor); Play(data.attackSound, SfxCue.EnemySpecial); }
        void CheckChargeHit(Vector2 start, Vector2 end)
        {
            if (chargeHitPlayer || !player) return;
            var point = (Vector2)player.position;
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            var t = lengthSquared > .0001f ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared) : 0f;
            var closest = start + segment * t;
            var hitRadius = Mathf.Max(.48f, data.attackRange * .52f);
            if (Vector2.Distance(point, closest) > hitRadius) return;
            chargeHitPlayer = true;
            HitPlayer(chargeDirection);
            charging = false;
            movement = Vector2.zero;
            recoveryUntil = Time.time + Mathf.Max(.3f, data.recoveryTime);
        }
        void HitPlayer(Vector2 direction) { var controller = player.GetComponent<PlayerController>(); if (controller) { var dealt=data.damage*damageScale; controller.TakeDamage(dealt, (Vector2)transform.position - direction * .2f); ApplyAttackStatus(controller,dealt); var elite=GetComponent<EliteEnemy>();if(elite)elite.ApplyOnHit(controller,dealt); } }
        void ApplyAttackStatus(PlayerController controller,float dealt)
        {
            if(!controller)return;var status=controller.GetComponent<ElementalStatusEffects>();
            if(data.attackStyle==EnemyAttackStyle.Basic||data.attackStyle==EnemyAttackStyle.CrescentSlash||data.attackStyle==EnemyAttackStyle.HeavySmash)return;
            if(!status)status=controller.gameObject.AddComponent<ElementalStatusEffects>();
            if(data.attackStyle==EnemyAttackStyle.TripleClaw||data.attackStyle==EnemyAttackStyle.BoneLance||data.attackStyle==EnemyAttackStyle.Pounce)status.Bleed(gameObject,3.5f,Mathf.Max(.35f,dealt*.12f));
            else if(data.attackStyle==EnemyAttackStyle.FireFan)status.Burn(gameObject,3f,Mathf.Max(.5f,dealt*.2f));
            else if(data.attackStyle==EnemyAttackStyle.ToxicVolley)status.Poison(gameObject,4f,Mathf.Max(.4f,dealt*.15f));
            else if(data.attackStyle==EnemyAttackStyle.ArcaneBurst)status.Shock(gameObject,2.5f,0f);
            else if(data.attackStyle==EnemyAttackStyle.BlinkStrike)status.Curse(gameObject,3.5f);
        }
        void PerformMeleeAttack(Vector2 direction)
        {
            CombatVfxPool.Instance.SpawnEnemyAttack(transform.position, direction, data.attackStyle, data.effectColor, data.damage / 9f);
            var distance = Vector2.Distance(transform.position, player.position);
            var reach = data.attackRange + (data.attackStyle == EnemyAttackStyle.HeavySmash ? .65f : .2f);
            if (distance <= reach) HitPlayer(direction);
            if (data.attackStyle == EnemyAttackStyle.HeavySmash)
            {
                CombatVfxPool.Instance.SpawnEnemyAbilityBurst((Vector2)transform.position + direction * .5f, data.attackStyle, data.effectColor);
                var camera = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;
                if (camera) camera.Shake(.16f);
            }
        }
        void FireProjectiles(Vector2 direction)
        {
            var style = data.attackStyle;
            var radial = style == EnemyAttackStyle.ArcaneBurst;
            var count = (radial ? Mathf.Max(6, data.projectileCount) : style == EnemyAttackStyle.FireFan ? Mathf.Max(3, data.projectileCount) : Mathf.Max(1, data.projectileCount)) + patternBonus;
            CombatVfxPool.Instance.SpawnEnemyAbilityBurst(transform.position, style, data.effectColor);
            for (var i = 0; i < count; i++)
            {
                var offset = radial ? i * (360f / count) : count == 1 ? 0f : Mathf.Lerp(style == EnemyAttackStyle.FireFan ? -28f : -15f, style == EnemyAttackStyle.FireFan ? 28f : 15f, i / (float)(count - 1));
                var shotDirection = (Vector2)(Quaternion.Euler(0, 0, offset) * direction);
                var speed = data.projectileSpeed * (style == EnemyAttackStyle.BoneLance ? 1.35f : 1f);
                EnemyProjectilePool.Instance.Spawn(transform.position + (Vector3)shotDirection * .35f, data.damage * damageScale, speed, shotDirection, gameObject, style, data.effectColor);
            }
        }
        bool HandleSpecialAbilities(Vector2 direction)
        {
            // Cada habilidad tiene su propio cooldown: una IA híbrida puede
            // teletransportarse, invocar y apoyar en la misma pelea.
            if (Has(EnemyBehaviour.Teleport) && (!miniBoss || bossWorld >= 3) && Time.time >= nextTeleport) { Teleport(direction); nextTeleport = Time.time + data.teleportCooldown * cooldownScale; return true; }
            if (Has(EnemyBehaviour.Summoner) && (!miniBoss || bossWorld >= 4) && Time.time >= nextSummon) { Summon(); nextSummon = Time.time + data.summonCooldown * cooldownScale; return true; }
            if (Has(EnemyBehaviour.Support) && (!miniBoss || bossWorld >= 2) && Time.time >= nextSupport) { SupportAllies(); nextSupport = Time.time + 3f; return true; }
            return false;
        }

        bool TryMiniBossPattern(Vector2 direction)
        {
            if (!miniBoss || Time.time < nextBossPattern || telegraphing || charging) return false;
            var healthRatio = runtimeMaxHealth > 0f ? CurrentHealth / runtimeMaxHealth : 1f;
            var patternCount = Mathf.Clamp(bossWorld + 1, 2, 5);
            var pattern = bossPatternIndex++ % patternCount;
            var interval = Mathf.Lerp(3.9f, 2.8f, (bossWorld - 1) / 4f) + (healthRatio < .45f ? -.3f : 0f);
            nextBossPattern = Time.time + interval;
            nextAttack = Mathf.Max(nextAttack, Time.time + 1.05f);
            movement = Vector2.zero;
            SetState(EnemyState.Attack);

            if (pattern == 1)
            {
                BeginChargeTelegraph(direction);
                return true;
            }

            var style = pattern == 0 ? EnemyAttackStyle.FireFan : pattern == 2 ? EnemyAttackStyle.ArcaneBurst : pattern == 3 ? EnemyAttackStyle.BoneLance : EnemyAttackStyle.ToxicVolley;
            var count = pattern == 0 ? 3 + Mathf.FloorToInt(bossWorld * .5f) : pattern == 2 ? 7 + bossWorld : 3;
            var arc = pattern == 0 ? 54f : pattern == 3 ? 34f : 24f;
            var radial = pattern == 2;
            var volleys = pattern == 4 ? 3 : 1;
            var damageMultiplier = radial ? .7f : pattern == 4 ? .62f : .82f;
            if (pattern == 3) Teleport(direction);
            EnemyVfx.Telegraph(transform.position, direction, pattern == 4 ? .42f : .32f);
            recoveryUntil = Time.time + (pattern == 4 ? 1.25f : .72f);
            StartCoroutine(MiniBossVolley(style, count, arc, radial, volleys, damageMultiplier, pattern == 4 ? .4f : .3f));
            return true;
        }

        IEnumerator MiniBossVolley(EnemyAttackStyle style, int count, float arc, bool radial, int volleys, float damageMultiplier, float delay)
        {
            yield return new WaitForSeconds(delay);
            for (var volley = 0; volley < volleys && !dead && player; volley++)
            {
                var direction = ((Vector2)(player.position - transform.position)).normalized;
                FireMiniBossProjectiles(style, direction, count, arc, radial, damageMultiplier, volley * 11f);
                if (volley + 1 < volleys) yield return new WaitForSeconds(.22f);
            }
        }

        void FireMiniBossProjectiles(EnemyAttackStyle style, Vector2 direction, int count, float arc, bool radial, float damageMultiplier, float rotationOffset)
        {
            CombatVfxPool.Instance.SpawnEnemyAbilityBurst(transform.position, style, data.effectColor);
            Play(data.attackSound, SfxCue.EnemySpecial);
            for (var i = 0; i < count; i++)
            {
                var angle = radial ? i * 360f / count + rotationOffset : count == 1 ? 0f : Mathf.Lerp(-arc * .5f, arc * .5f, i / (float)(count - 1));
                var shotDirection = (Vector2)(Quaternion.Euler(0f, 0f, angle) * direction);
                EnemyProjectilePool.Instance.Spawn(transform.position + (Vector3)shotDirection * .42f, data.damage * damageScale * damageMultiplier, data.projectileSpeed, shotDirection, gameObject, style, data.effectColor);
            }
        }
        void Teleport(Vector2 direction)
        {
            var destination = (Vector2)transform.position - direction * Mathf.Max(2f, data.preferredRange * .65f) + UnityEngine.Random.insideUnitCircle * 1.2f;
            if (OwningRoom) destination = OwningRoom.ClampInside(destination, 1f);
            EnemyVfx.Teleport(transform.position);
            body.position = destination;
            transform.position = destination;
            EnemyVfx.Teleport(destination);
        }
        void Summon()
        {
            if (!data.summonData) return;
            var summonTemplate = data.summonData;
            var generator = FindAnyObjectByType<DungeonGenerator>();
            if (generator) summonTemplate = generator.ResolveSummonForCurrentWorld(summonTemplate);
            if (!summonTemplate) return;
            var position = (Vector2)transform.position + UnityEngine.Random.insideUnitCircle.normalized * 1.1f;
            if (OwningRoom) position = OwningRoom.ClampInside(position);
            var summon = new GameObject(summonTemplate.enemyName);
            summon.transform.position = position;
            summon.AddComponent<SpriteRenderer>();
            var summonBrain = summon.AddComponent<EnemyBrain>();
            summonBrain.Configure(summonTemplate, OwningRoom, healthScale, damageScale, moveScale, cooldownScale, Mathf.Max(0, patternBonus - 1));
            summonBrain.summoner = this;
            ownedSummons.Add(summonBrain);
            summon.AddComponent<EnemySpriteAnimator>();
            if (OwningRoom) OwningRoom.RegisterEnemy(summonBrain);
        }
        void SupportAllies() { foreach (var ally in Physics2D.OverlapCircleAll(transform.position, 4f)) { var brain = ally.GetComponent<EnemyBrain>(); if (!brain || brain == this) continue; brain.Heal(data.damage * .5f); brain.AddShield(data.damage * .25f); } EnemyVfx.Support(transform.position); }
        public void ReceiveHit(CombatHit hit)
        {
            if (dead) return;
            if (hit.source && hit.source.GetComponentInParent<PlayerStats>()) lastAttacker = hit.source;
            var powerRuntime = hit.source ? hit.source.GetComponentInParent<PowerUpRuntime>() : null;
            if (powerRuntime) powerRuntime.ModifyOutgoingHit(ref hit, this);
            if (dead) return; var rawDamage = (hit.damage <= 0f ? 0f : Mathf.Max(1, hit.damage - data.defense)) * (1 - data.resistance); var status=GetComponent<ElementalStatusEffects>();if(status)rawDamage*=status.DamageTakenMultiplier; var absorbed = Mathf.Min(CurrentShield, rawDamage); CurrentShield -= absorbed; rawDamage -= absorbed; CurrentHealth -= rawDamage;
            ApplyKnockback(hit.direction, hit.knockback);
            CombatFeedback.ShowImpact(transform.position, new CombatHit { damage = rawDamage, direction = hit.direction, knockback = hit.knockback, critical = hit.critical, weapon = hit.weapon, source = hit.source }, ImpactSurface.Flesh);
            SetState(EnemyState.Damage); if (animator) animator.SetTrigger("Hit"); Play(data.hurtSound, SfxCue.EnemyHurt); if(powerRuntime)powerRuntime.AfterHit(this,hit,rawDamage); WeaponEffectRuntime.Apply(this, hit, rawDamage); if (CurrentHealth <= 0 && !dead) Die();
        }
        void ApplyKnockback(Vector2 direction, float strength)
        {
            if (strength <= 0f || direction.sqrMagnitude < .0001f) return;
            var resistance = Mathf.Clamp01(data.knockbackResistance);
            var impulse = direction.normalized * strength * (1f - resistance);
            knockbackVelocity = Vector2.ClampMagnitude(knockbackVelocity + impulse, 12f);
            if (impulse.sqrMagnitude > .04f)
                stunUntil = Mathf.Max(stunUntil, Time.time + Mathf.Clamp(.04f + impulse.magnitude * .025f, .05f, .18f));
        }
        public void Heal(float amount) { if (!dead) CurrentHealth = Mathf.Min(runtimeMaxHealth, CurrentHealth + amount); }
        public void AddShield(float amount) { if (!dead) CurrentShield += amount; }
        public void RemoveShield(float amount) { if (!dead) CurrentShield = Mathf.Max(0f, CurrentShield - Mathf.Max(0f, amount)); }
        public void Stun(float duration) { stunUntil = Mathf.Max(stunUntil, Time.time + duration); }
        void Die()
        {
            dead = true;
            CurrentHealth = 0f;
            knockbackVelocity = Vector2.zero;
            movement = Vector2.zero;
            if (body) { body.linearVelocity = Vector2.zero; body.angularVelocity = 0f; }
            SetState(EnemyState.Death);
            Play(data.deathSound, SfxCue.EnemyDeath);
            if (animator) animator.SetTrigger("Die");
            var elite = GetComponent<EliteEnemy>(); if (elite) elite.OnOwnerDeath();
            var killerRuntime=lastAttacker?lastAttacker.GetComponentInParent<PowerUpRuntime>():null;
            if(killerRuntime)killerRuntime.OnEnemyKilled(this);
            DropRewards();
            for (var i = ownedSummons.Count - 1; i >= 0; i--)
                if (ownedSummons[i]) ownedSummons[i].DespawnWithSummoner();
            ownedSummons.Clear();
            if (summoner) summoner.ownedSummons.Remove(this);
            if (OwningRoom) OwningRoom.NotifyEnemyDefeated(this);
            Destroy(gameObject, .25f);
        }

        void DespawnWithSummoner()
        {
            if (dead) return;
            dead = true;
            CurrentHealth = 0f;
            knockbackVelocity = Vector2.zero;
            movement = Vector2.zero;
            if (body) { body.linearVelocity = Vector2.zero; body.angularVelocity = 0f; }
            EnemyVfx.Teleport(transform.position);
            if (OwningRoom) OwningRoom.NotifyEnemyDefeated(this);
            Destroy(gameObject, .08f);
        }
        void DropRewards()
        {
            var killerStats = lastAttacker ? lastAttacker.GetComponentInParent<PlayerStats>() : null;
            if (!killerStats) killerStats = FindAnyObjectByType<PlayerStats>();
            if (killerStats)
            {
                killerStats.experience += data.experienceReward;
                // Un enemigo común puede no soltar moneda. Con 1-3 oleadas, una
                // sala entrega una fracción razonable de una compra de tienda.
                var coinMin = Mathf.Max(0, Mathf.FloorToInt(data.minCoins * .30f));
                var coinMax = Mathf.Max(coinMin, Mathf.CeilToInt(data.maxCoins * .30f));
                var energyMin = Mathf.Max(1, data.minEnergy); var energyMax = Mathf.Max(energyMin, data.maxEnergy);
                var coins=UnityEngine.Random.Range(coinMin, coinMax + 1);var energy=UnityEngine.Random.Range(energyMin, energyMax + 1);var healthChance=.035f;
                var runtime=killerStats.GetComponent<PowerUpRuntime>();if(runtime)runtime.ModifyDrops(this,ref coins,ref energy,ref healthChance);
                MobDropPool.Instance.SpawnDrops(transform.position, killerStats, coins, energy,healthChance);
            }
            if (data.loot == null) return; foreach (var entry in data.loot) { if (!entry.prefab || UnityEngine.Random.value > entry.chance) continue; var count = UnityEngine.Random.Range(Mathf.Max(1, entry.minAmount), Mathf.Max(entry.minAmount + 1, entry.maxAmount + 1)); for (var i = 0; i < count; i++) Instantiate(entry.prefab, transform.position + (Vector3)UnityEngine.Random.insideUnitCircle * .25f, Quaternion.identity); }
        }
        void SetState(EnemyState value) { if (State == value) return; State = value; if (animator) animator.SetInteger("State", (int)value); }
        void Play(AudioClip clip) { if (clip) { audioSource.PlayOneShot(clip); ProjectLike.Online.CoopSession.EmitSound(clip, transform.position, audioSource.volume, audioSource.pitch, audioSource.spatialBlend); } }
        void Play(AudioClip clip, SfxCue fallback) { if (clip) Play(clip); else GameSfx.Play(fallback, transform.position, fallback == SfxCue.EnemyHurt ? .5f : .72f); }
    }
}
