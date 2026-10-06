using System.Collections;
using System.Collections.Generic;
using ProjectLike.Phase1;
using ProjectLike.Phase2;
using ProjectLike.Phase3;
using ProjectLike.Phase5;
using UnityEngine;

namespace ProjectLike.Phase12
{
    [DisallowMultipleComponent]
    public class PowerUpRuntime : MonoBehaviour
    {
        PlayerPowerUps inventory;
        PlayerStats stats;
        PlayerController controller;
        float lastDamageTime = -99f, nextRegen, nextCorruptDrain, damageBuffUntil, speedBuffUntil, attackBuffUntil, hungerUntil;
        float temporaryDamage, temporarySpeed, temporaryAttack;
        int meleeHits, projectileHits, totalHits, currentRoomSerial;
        bool secondWindUsed, fleetingUsed, valkyrieUsed, fragmentedUsed, counterReady, bonusDashSpent;
        float bonusDashRecharge;
        float switchReady, dashStrikeUntil, recyclingReady, thermalReady, ruptureReady, echoReady, inversionReady;
        int paidAttacks, singularityHits;
        bool roomDamaged;
        readonly HashSet<EnemyBrain> openedTargets = new HashSet<EnemyBrain>();
        public float MeleeCriticalBonus(WeaponData weapon)
        {
            var value = 0f;
            if (weapon && !weapon.projectile && inventory) foreach (var item in inventory.acquired) if (item) value += item.meleeCriticalChance;
            return value;
        }
        public void OnWeaponSwitched()
        {
            if (Has("cambio_tactico") && TimeStopAbility.PlayerTime >= switchReady)
            { switchReady = TimeStopAbility.PlayerTime + 3f; stats.RestoreEnergy(4f); }
        }
        public void OnConsumableUsed()
        {
            if (Has("alquimia_residual")) { stats.AddShield(2f); BuffAttack(.3f, 4f); }
        }

        void Awake() { EnsureReferences(); }
        void EnsureReferences()
        {
            if (!inventory) inventory = GetComponent<PlayerPowerUps>();
            if (!stats) stats = GetComponent<PlayerStats>();
            if (!controller) controller = GetComponent<PlayerController>();
        }
        public bool Has(string id) => inventory && inventory.Has(id);
        public int Stacks(string id) => inventory ? inventory.Stacks(id) : 0;
        public void ResetForNewRun()
        {
            StopAllCoroutines(); temporaryDamage = temporarySpeed = temporaryAttack = 0f;
            damageBuffUntil = speedBuffUntil = attackBuffUntil = hungerUntil = 0f;
            meleeHits = projectileHits = totalHits = currentRoomSerial = paidAttacks = singularityHits = 0;
            secondWindUsed = fleetingUsed = valkyrieUsed = fragmentedUsed = counterReady = bonusDashSpent = false;
            openedTargets.Clear();
            foreach (var companion in GetComponentsInChildren<PowerUpCompanion>()) if (companion) Destroy(companion.gameObject);
        }

        public void OnAcquired(PowerUpData item)
        {
            if (!item) return;
            EnsureReferences();
            if (!stats) return;
            if (item.effectId == "tiempo_cero" && !GetComponent<TimeStopAbility>()) gameObject.AddComponent<TimeStopAbility>();
            if (item.maxShield != 0f) { stats.maxShield = Mathf.Max(0f, stats.maxShield + item.maxShield); stats.AddShield(item.maxShield); }
            if (item.maxEnergy != 0f) { stats.maxEnergy = Mathf.Max(1f, stats.maxEnergy + item.maxEnergy); stats.RestoreEnergy(item.maxEnergy); }
            if (item.effectId == "maldicion_del_rey") stats.luck += 2f;
            if (item.effectId == "corazon_corrupto") stats.ModifyMaxHealth(stats.maxHealth * .5f);
            if (item.effectId == "poder_inestable") ApplyUnstableRoomEffect();
            EnsureCompanions();
        }

        void Update()
        {
            if (Has("regeneracion") && Time.time - lastDamageTime > 5f && Time.time >= nextRegen) { nextRegen = Time.time + 1.25f; stats.Heal(.5f * Mathf.Max(1, Stacks("regeneracion"))); }
            if (Has("pacto_de_sangre") && Time.time - lastDamageTime > 8f && Time.time>=nextCorruptDrain && stats.CurrentHealth > stats.maxHealth * .35f) {nextCorruptDrain=Time.time+1.5f;stats.SpendHealth(.5f);}
            if (Time.time >= damageBuffUntil) temporaryDamage = 0f;
            if (Time.time >= speedBuffUntil) temporarySpeed = 0f;
            if (Time.time >= attackBuffUntil) temporaryAttack = 0f;
            if (Time.time >= hungerUntil && Has("hambre_del_vacio")) temporaryDamage = Mathf.MoveTowards(temporaryDamage, 0f, Time.deltaTime * 2f);
            if (bonusDashSpent && Time.time >= bonusDashRecharge) bonusDashSpent = false;
        }

        public float ModifyHealing(float amount)
        {
            if (Has("corazon_corrupto")) amount *= .45f;
            if (Has("moneda_sangrienta")) amount *= .65f;
            return amount;
        }

        public float EnergyRegenMultiplier
        {
            get
            {
                var multiplier = 1f;
                foreach (var item in inventory ? inventory.acquired : new System.Collections.Generic.List<PowerUpData>()) if (item) multiplier += item.energyRegenerationPercent * .01f;
                if (Has("manantial_eterno")) multiplier += 1.25f;
                return multiplier;
            }
        }

        public bool TrySpendEnergy(float baseCost, WeaponData weapon)
        {
            var cost = baseCost;
            if (baseCost > 0f && Has("ritmo_arcano") && paidAttacks % 5 == 4) { paidAttacks++; return true; }
            if (Has("reactor_inestable")) cost *= .5f;
            var rangedPhysical = weapon && (weapon.type == WeaponType.Bow || weapon.type == WeaponType.Crossbow);
            var magic = IsMagic(weapon);
            if (rangedPhysical && Has("carcaj_ampliado")) cost *= .8f;
            if (rangedPhysical && Has("carcaj_infinito")) cost *= .25f;
            if (magic && Has("concentracion")) cost *= .92f;
            if (magic && Has("archimago")) cost *= .85f;
            if (magic && Has("magia_prohibida")) cost *= 1.35f;
            if (stats.TrySpendEnergy(cost))
            {
                if (baseCost > 0f) { paidAttacks++; if (Has("reactor_inestable") && paidAttacks % 10 == 0) stats.SpendHealth(1f); }
                return true;
            }
            if (magic && Has("mago_de_sangre") && stats.CurrentHealth > cost * .3f + 1f) { stats.SpendHealth(cost * .3f); paidAttacks++; return true; }
            return false;
        }

        public bool DashCostsEnergy => !Has("dash_sin_energia");

        public float AttackCooldownMultiplier(WeaponData weapon)
        {
            var value = 1f / Mathf.Max(.35f, stats.attackSpeed / 4f);
            if (weapon && !weapon.projectile && inventory)
            {
                var bonus = 0f; foreach (var item in inventory.acquired) if (item) bonus += item.meleeAttackSpeedPercent * .01f;
                value /= 1f + bonus;
            }
            if (Time.time < attackBuffUntil) value /= 1f + temporaryAttack;
            if (Has("velocidad_imposible")) value *= .78f;
            return value;
        }

        public int ExtraProjectileCount(WeaponData weapon)
        {
            var extra = 0;
            if (Has("disparo_doble") && Random.value < .42f) extra++;
            if (IsMagic(weapon) && Has("duplicacion_arcana") && Random.value < .3f) extra++;
            if (IsMagic(weapon) && Has("eco_magico") && Random.value < .22f) extra++;
            return extra;
        }

        public bool ProjectilePierces(WeaponData weapon) => Has("flecha_espectral") || Has("flecha_perforante");
        public int ProjectilePierceCount => Has("flecha_espectral") ? 99 : Has("flecha_perforante") ? 1 : 0;
        public int ProjectileBounces => Has("rebote") ? 1 : 0;
        public bool ProjectileHoming => Has("ojo_del_grifo");

        public void ModifyOutgoingHit(ref CombatHit hit, EnemyBrain target)
        {
            if (hit.secondary || !hit.weapon) return;
            var melee = hit.weapon && !hit.weapon.projectile;
            var magic = IsMagic(hit.weapon);
            var projectile = hit.weapon.projectile;
            var multiplier = 1f;
            if (melee) foreach (var item in inventory.acquired) if (item) multiplier += item.meleeDamagePercent * .01f;
            if (magic) foreach (var item in inventory.acquired) if (item) multiplier += item.magicDamagePercent * .01f;
            if (magic && Has("magia_prohibida")) multiplier += .6f;
            if (Has("cristal_desnudo") && stats.CurrentShield <= 0f) multiplier += .6f;
            if (target && Has("primera_sangre") && openedTargets.Add(target)) multiplier += .25f;
            if (target && Has("rompeguardias") && target.CurrentShield > 0f) multiplier += .5f;
            if (Has("punta_lanzada") && TimeStopAbility.PlayerTime < dashStrikeUntil) { multiplier += .4f; dashStrikeUntil = 0f; }
            if (Time.time < damageBuffUntil) multiplier += temporaryDamage;
            if (Has("berserker")) multiplier += Mathf.InverseLerp(1f, .15f, stats.CurrentHealth / stats.maxHealth) * .55f;
            if (Has("fortaleza") && stats.CurrentHealth >= stats.maxHealth * .8f) hit.knockback *= 1.35f;
            if (target && Has("ejecutor") && target.CurrentHealth <= target.RuntimeMaxHealth * .3f) multiplier += .25f;
            if (target && projectile && Has("cazador") && Vector2.Distance(transform.position, target.transform.position) > 5f) multiplier += .25f;
            if (magic && Has("sobrecarga_arcana") && stats.CurrentEnergy >= stats.maxEnergy * .8f) multiplier += .25f;
            var status = target ? target.GetComponent<ElementalStatusEffects>() : null;
            if (status && status.IsFrozen && Has("hielo_quebradizo")) multiplier += .35f;
            var wasCritical=hit.critical;
            if (Has("cazador_perfecto") && Time.time - lastDamageTime > 6f) hit.critical = hit.critical || Random.value < Mathf.Clamp01((Time.time - lastDamageTime - 6f) * .025f);
            totalHits++; if (Has("destino_marcado") && totalHits % 7 == 0) hit.critical = true;
            if(hit.critical&&!wasCritical)hit.damage*=Mathf.Max(1f,hit.weapon.criticalMultiplier);
            if (hit.critical)
            {
                var criticalBonus = 0f; foreach (var item in inventory.acquired) if (item) criticalBonus += item.criticalDamagePercent * .01f;
                hit.damage *= 1f + criticalBonus;
                if (Has("sangre_del_cazador")) BuffAttack(.18f, 3f);
            }
            if (melee) { meleeHits++; if (Has("filo_del_rey") && meleeHits % 3 == 0) SpawnWave(target ? target.transform.position : transform.position, hit.direction, hit.damage * .45f); }
            if (projectile) { projectileHits++; if (Has("tormenta_de_flechas") && projectileHits % 6 == 0 && target) StartCoroutine(ArrowStorm(target.transform.position, hit.damage * .35f)); }
            if (counterReady) { multiplier += .45f; counterReady = false; }
            hit.damage *= multiplier;
            var knock = 1f; foreach (var item in inventory.acquired) if (item) knock += item.knockbackPercent * .01f; hit.knockback *= knock;
            if(melee&&Has("brazales_de_fuerza"))hit.knockback*=1.1f;
            if (melee && Has("fuerza_del_titan") && (hit.weapon.type == WeaponType.Hammer || hit.weapon.type == WeaponType.Axe)) hit.knockback *= 1.65f;
        }

        public void AfterHit(EnemyBrain target, CombatHit hit, float dealt)
        {
            if (!target || hit.secondary) return;
            var status = target.GetComponent<ElementalStatusEffects>(); if (!status) status = target.gameObject.AddComponent<EnemyStatusEffects>();
            var elementalPower = Has("maestro_elemental") ? 1.45f : 1f;
            if (Has("reciclaje_critico") && hit.critical && hit.weapon && hit.weapon.energyCost > 0f && TimeStopAbility.PlayerTime >= recyclingReady)
            { recyclingReady = TimeStopAbility.PlayerTime + .6f; stats.RestoreEnergy(2f); }
            if (Has("reaccion_termica") && status.Burning && (status.IsFrozen || status.Chilled) && Time.time >= thermalReady)
            { thermalReady = Time.time + 1.5f; DamageArea(target.transform.position, 2f, dealt * .6f, hit.direction); }
            if (Has("eco_de_impacto") && Time.time >= echoReady)
            { echoReady = Time.time + 1f; StartCoroutine(EchoHit(target, dealt * .5f)); }
            if (Has("singularidad") && ++singularityHits % 10 == 0)
            {
                var seen = new HashSet<EnemyBrain>();
                foreach (var collider in Physics2D.OverlapCircleAll(target.transform.position, 3.5f))
                {
                    var other = collider.GetComponentInParent<EnemyBrain>();
                    if (!other || other == target || other.IsDead || !seen.Add(other)) continue;
                    SecondaryHit(other, dealt * .8f, ((Vector2)target.transform.position - (Vector2)other.transform.position).normalized, 9f);
                }
                CombatVfxPool.Instance.SpawnEnemyAbilityBurst(target.transform.position, EnemyAttackStyle.ArcaneBurst, new Color(.8f, .15f, 1f));
            }
            if (Has("cero_absoluto") && Random.value < .25f) status.Ice(2, 4);
            if (Has("ascua") && Random.value < .18f) status.Burn(gameObject, 4f * elementalPower, 1.1f * elementalPower);
            if (Has("escarcha") && Random.value < .2f) status.Ice(1, Has("cero_absoluto") ? 4 : 6);
            if (Has("toxina") && Random.value < .2f) status.Poison(gameObject, 5f * elementalPower, .8f * elementalPower);
            if ((Has("chispa") || Has("cadena_electrica")) && Random.value < (Has("cadena_electrica") ? .28f : .18f)) { status.Electrified=true; ChainLightning(target, dealt * .3f, Has("cadena_electrica") ? 2 : 1); }
            if (Has("hemorragia") && hit.weapon && !hit.weapon.projectile && Random.value < .22f) status.Bleed(gameObject, 4f, 1f);
            if (Has("caos_elemental") && Random.value < .22f) { var roll = Random.Range(0, 3); if (roll == 0) status.Burn(gameObject, 4f, 1.3f); else if (roll == 1) status.Poison(gameObject, 5f, 1f); else status.Ice(2, 5); }
            if (Has("resonancia") && IsMagic(hit.weapon) && Random.value < .24f) DamageArea(target.transform.position, 1.6f, dealt * .35f, hit.direction);
            if (Has("explosion_critica") && hit.critical) DamageArea(target.transform.position, 1.45f, dealt * .3f, hit.direction);
            if (Has("tormenta_viviente") && hit.critical && status.Electrified) ChainLightning(target, dealt * .4f, 2);
            if (Has("artillero_arcano") && hit.weapon && hit.weapon.projectile && !IsMagic(hit.weapon) && Random.value < .25f) SpawnWave(target.transform.position, hit.direction, dealt * .28f);
            if (Has("impacto_sismico") && hit.weapon && !hit.weapon.projectile && (hit.weapon.type == WeaponType.Hammer || hit.weapon.type == WeaponType.Axe)) DamageArea(target.transform.position, 1.55f, dealt * .32f, hit.direction);
            if (Has("fuerza_del_titan") && hit.weapon && !hit.weapon.projectile && (hit.weapon.type == WeaponType.Hammer || hit.weapon.type == WeaponType.Axe)) DamageArea(target.transform.position, 2f, dealt * .45f, hit.direction);
            if (Has("maestro_de_espadas") && hit.weapon && !hit.weapon.projectile && Random.value < .25f) SpawnWave(target.transform.position, hit.direction, dealt * .4f);
            if (Has("pacto_de_sangre")) stats.Heal(dealt * .035f);
        }

        public float ModifyIncomingDamage(float amount)
        {
            if (Has("egida_divina") && Time.time >= aegisReady) { aegisReady = Time.time + 12f; return 0f; }
            if (Has("inversion_cinetica") && TimeStopAbility.PlayerTime >= inversionReady && stats.TrySpendEnergy(amount * 4f))
            { inversionReady = TimeStopAbility.PlayerTime + .8f; amount *= .35f; CombatVfxPool.Instance.SpawnEnemyAbilityBurst(transform.position, EnemyAttackStyle.ArcaneBurst, Color.cyan); }
            if (Has("cristal_desnudo")) amount *= 1.3f;
            var healthRatio = stats.CurrentHealth / Mathf.Max(1f, stats.maxHealth);
            if (Has("armadura_de_cuero")) amount *= .95f;
            if (Has("placas_encantadas")) amount *= .9f;
            if (Has("ultimo_bastion") && healthRatio < .25f) amount *= .8f;
            if (Has("piel_de_dragon")) amount *= .86f;
            if (Has("fortaleza") && healthRatio > .8f) amount *= .82f;
            if (Has("filo_corrupto")) amount *= 1.22f;
            if (Has("velocidad_imposible")) amount *= 1.12f;
            return amount;
        }
        float aegisReady;

        public bool TryPreventDeath(out float restoredHealth)
        {
            restoredHealth = 0f;
            if (Has("inmortalidad_fugaz") && !fleetingUsed) { fleetingUsed = true; restoredHealth = 1f; controller.GrantInvulnerability(2f); return true; }
            if (Has("favor_de_la_valquiria") && !valkyrieUsed) { valkyrieUsed = true; restoredHealth = stats.maxHealth * .5f; controller.GrantInvulnerability(2.5f); return true; }
            if (Has("alma_fragmentada") && !fragmentedUsed) { fragmentedUsed = true; stats.maxHealth = Mathf.Max(1f, stats.maxHealth * .7f); restoredHealth = stats.maxHealth; temporaryDamage += .75f; damageBuffUntil = Time.time + 12f; controller.GrantInvulnerability(2f); return true; }
            return false;
        }

        public void OnDamaged(float damage, Vector2 source, bool shieldBroken)
        {
            lastDamageTime = Time.time;
            roomDamaged = true;
            if (shieldBroken && Has("ruta_segura") && Time.time >= ruptureReady)
            {
                ruptureReady = Time.time + 10f;
                foreach (var projectile in FindObjectsByType<EnemyProjectile>())
                    if (Vector2.Distance(transform.position, projectile.transform.position) <= 3.5f) projectile.Dispel();
                CombatVfxPool.Instance.SpawnEnemyAbilityBurst(transform.position, EnemyAttackStyle.ArcaneBurst, Color.cyan);
            }
            if (Has("segundo_aliento") && !secondWindUsed && stats.CurrentHealth < stats.maxHealth * .3f) { secondWindUsed = true; StartCoroutine(TemporaryDefense(1.5f, 5f)); }
            if (Has("escudo_reactivo")) StartCoroutine(TemporaryDefense(1f, 4f));
            if (Has("contraataque")) counterReady = true;
            if (Has("repulsion")) DamageArea(transform.position, 2.2f, 0f, Vector2.zero, 8f);
            if (Has("espinas")) { var enemy = ClosestEnemy(source, 1.8f); if (enemy) SecondaryHit(enemy, Mathf.Max(1f, damage * .25f), ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized, 2f); }
            if (shieldBroken && Has("ira_del_guardian")) DamageArea(transform.position, 2f, 8f, Vector2.zero, 5f);
        }

        public void OnEnemyKilled(EnemyBrain enemy)
        {
            if (!enemy) return;
            var status = enemy ? enemy.GetComponent<ElementalStatusEffects>() : null;
            if (Has("moneda_sangrienta")) stats.coins += 3;
            if (Has("tormenta_orbital"))
            {
                var origin = (Vector2)enemy.transform.position;
                var seen = new HashSet<EnemyBrain>();
                for (var i = 0; i < 3; i++)
                {
                    EnemyBrain next = null; var distance = 5f;
                    foreach (var candidate in FindObjectsByType<EnemyBrain>())
                    { if (!candidate || candidate.IsDead || seen.Contains(candidate)) continue; var d = Vector2.Distance(origin, candidate.transform.position); if (d < distance) { distance = d; next = candidate; } }
                    if (!next) break; seen.Add(next); StartCoroutine(EchoHit(next, stats.damage * .8f));
                    CombatVfxPool.Instance.SpawnChainLightning(origin, next.transform.position);
                }
            }
            if (Has("cosecha") && Random.value < .3f) stats.RestoreEnergy(8f);
            if (Has("alma_menor") && Random.value < .16f) MobDropPool.Instance.SpawnHealth(enemy.transform.position, stats, 3);
            if (Has("segador")) { /* El siguiente ataque queda disponible antes mediante buff de ataque. */ BuffAttack(.15f, 2.5f); }
            if (Has("sed_de_batalla")) BuffDamage(.08f, 5f, .4f);
            if (Has("cadena_de_muerte")) BuffDamage(.1f, 5f, .65f);
            if (Has("viento_favorable")) BuffSpeed(.18f, 4f);
            if (Has("hambre_del_vacio")) { BuffDamage(.12f, 7f, .8f); hungerUntil = Time.time + 7f; }
            if (Has("explosion_final") && Random.value < .3f) DamageArea(enemy.transform.position, 1.8f, 8f, Vector2.zero);
            if (status && status.Burning && Has("combustion")) DamageArea(enemy.transform.position, 1.7f, 7f, Vector2.zero);
            if (status && status.Poisoned && Has("veneno_contagioso")) SpreadPoison(enemy.transform.position);
            if (Has("senor_de_las_almas")) SpawnSoul(enemy.transform.position);
            if (Has("nigromante") && Random.value < .28f) SpawnSoul(enemy.transform.position);
        }

        public void ModifyDrops(EnemyBrain enemy, ref int coins, ref int energy, ref float healthChance)
        {
            if (Has("bolsa_de_monedas")) coins = Mathf.CeilToInt(coins * 1.15f);
            if (Has("toque_dorado") && enemy && enemy.GetComponent<EliteEnemy>()) coins = Mathf.CeilToInt(coins * 1.75f);
            if (Has("alma_menor")) healthChance += .08f;
        }

        public float ShopDiscount
        {
            get
            {
                var value = 0f;
                if (inventory) foreach (var item in inventory.acquired) if (item) value += item.shopDiscountPercent * .01f;
                if (Mathf.Approximately(value, 0f) && Has("regateador")) value = .1f;
                return Mathf.Clamp(value, 0f, .55f);
            }
        }
        public int RerollReduction => Has("mercader_astuto") ? 3 : 0;
        public float ExtraEliteChance => Has("maldicion_del_rey") ? .18f : 0f;
        public float RoomRewardChanceBonus => Has("buscatesoros") ? .2f : 0f;

        public float DashCooldownMultiplier { get { var value = 1f; foreach (var item in inventory.acquired) if (item) value *= 1f - item.dashCooldownPercent * .01f; return Mathf.Max(.35f, value); } }
        public float DashDistanceMultiplier { get { var value = 1f;foreach (var item in inventory.acquired) if (item) value += item.dashDistancePercent * .01f;if(Mathf.Approximately(value,1f)&&Has("pluma_espectral"))value=1.22f;return value; } }
        public bool TryUseBonusDash()
        {
            if (!Has("caminante_dimensional") || bonusDashSpent) return false;
            bonusDashSpent = true; bonusDashRecharge = Time.time + 1.2f; return true;
        }
        public void OnDash()
        {
            if (Has("punta_lanzada")) dashStrikeUntil = TimeStopAbility.PlayerTime + 3f;
            if (Has("estela_glacial"))
            {
                var seen = new HashSet<EnemyBrain>();
                foreach (var collider in Physics2D.OverlapCircleAll(transform.position, 2.5f))
                {
                    var enemy = collider.GetComponentInParent<EnemyBrain>();
                    if (!enemy || enemy.IsDead || !seen.Add(enemy)) continue;
                    var status = enemy.GetComponent<ElementalStatusEffects>(); if (!status) status = enemy.gameObject.AddComponent<EnemyStatusEffects>(); status.Ice(2, 4);
                }
                CombatVfxPool.Instance.SpawnEnemyAbilityBurst(transform.position, EnemyAttackStyle.ArcaneBurst, Color.cyan);
            }
            if (Has("impulso")) BuffSpeed(.25f, 2f);
            if (Has("paso_fantasmal")) controller.GrantInvulnerability(.35f);
        }

        public void OnRoomEntered(RoomController room)
        {
            currentRoomSerial++; secondWindUsed = false; roomDamaged = false; openedTargets.Clear();
            if (Has("barrera_espiritual")) { stats.maxShield = Mathf.Max(stats.maxShield, 5f); stats.AddShield(5f); }
            if(Has("paladin")&&stats.CurrentShield>0f)BuffDamage(.2f,6f,.4f);
            if (Has("poder_inestable")) ApplyUnstableRoomEffect();
        }
        public void OnRoomCompleted(RoomController room)
        {
            if (Has("bendicion_menor")) stats.Heal(2f);
            if (Has("sala_impecable") && !roomDamaged) stats.coins += 6;
            if (Has("caballero_caido") && currentRoomSerial % 3 == 0) EnsureCompanions(true);
        }

        public float CurrentMoveMultiplier => 1f + (Time.time < speedBuffUntil ? temporarySpeed : 0f);

        void BuffDamage(float amount, float duration, float cap) { temporaryDamage = Mathf.Min(cap, temporaryDamage + amount); damageBuffUntil = Time.time + duration; }
        void BuffSpeed(float amount, float duration) { temporarySpeed = Mathf.Max(temporarySpeed, amount); speedBuffUntil = Time.time + duration; }
        void BuffAttack(float amount, float duration) { temporaryAttack = Mathf.Max(temporaryAttack, amount); attackBuffUntil = Time.time + duration; }
        IEnumerator TemporaryDefense(float amount, float duration) { stats.defense += amount; yield return new WaitForSeconds(duration); stats.defense -= amount; }
        void ApplyUnstableRoomEffect() { var good = Random.Range(0, 3); if (good == 0) BuffDamage(.35f, 30f, .8f); else if (good == 1) BuffSpeed(.3f, 30f); else stats.AddShield(8f); if (Random.value < .5f) StartCoroutine(TemporaryDefense(-.5f,30f)); else stats.TrySpendEnergy(15f); }

        IEnumerator EchoHit(EnemyBrain enemy, float damage)
        {
            yield return new WaitForSeconds(.4f);
            if (!enemy || enemy.IsDead) yield break;
            SecondaryHit(enemy, damage, Vector2.zero, 1f);
            CombatVfxPool.Instance.SpawnEnemyAbilityBurst(enemy.transform.position, EnemyAttackStyle.ArcaneBurst, new Color(.85f, .5f, 1f));
        }
        void DamageArea(Vector2 center, float radius, float damage, Vector2 direction, float knockback = 3f)
        {
            var seen = new HashSet<EnemyBrain>();
            foreach (var collider in Physics2D.OverlapCircleAll(center, radius)) { var enemy = collider.GetComponentInParent<EnemyBrain>(); if (!enemy || enemy.IsDead || !seen.Add(enemy)) continue; var away = ((Vector2)enemy.transform.position - center).normalized; SecondaryHit(enemy, damage, direction.sqrMagnitude > 0 ? direction : away, knockback); }
            CombatVfxPool.Instance.SpawnEnemyAbilityBurst(center, EnemyAttackStyle.ArcaneBurst, new Color(.7f, .3f, 1f));
        }
        void SecondaryHit(EnemyBrain enemy, float damage, Vector2 direction, float knockback) { enemy.ReceiveHit(new CombatHit { damage = damage, direction = direction, knockback = knockback, source = gameObject, secondary = true }); }
        EnemyBrain ClosestEnemy(Vector2 center, float radius) { EnemyBrain best = null; var distance = radius; foreach (var hit in Physics2D.OverlapCircleAll(center, radius)) { var enemy = hit.GetComponentInParent<EnemyBrain>(); if (!enemy || enemy.IsDead) continue; var d = Vector2.Distance(center, enemy.transform.position); if (d < distance) { distance = d; best = enemy; } } return best; }
        void ChainLightning(EnemyBrain origin, float damage, int jumps)
        {
            var status = origin.GetComponent<ElementalStatusEffects>(); if (!status) status = origin.gameObject.AddComponent<EnemyStatusEffects>();
            status.Shock(gameObject, 3f, damage, jumps);
        }
        void SpreadPoison(Vector2 center) { foreach (var hit in Physics2D.OverlapCircleAll(center, 2.5f)) { var enemy = hit.GetComponentInParent<EnemyBrain>(); if (!enemy || enemy.IsDead) continue; var status = enemy.GetComponent<ElementalStatusEffects>(); if (!status) status = enemy.gameObject.AddComponent<EnemyStatusEffects>(); status.Poison(gameObject, 4f, .8f); } }
        void SpawnWave(Vector2 origin, Vector2 direction, float damage) { var target = ClosestEnemy(origin + direction * 1.5f, 2.5f); if (target) SecondaryHit(target, damage, direction, 2f); CombatVfxPool.Instance.SpawnEnemyAbilityBurst(origin, EnemyAttackStyle.ArcaneBurst, Color.cyan); }
        IEnumerator ArrowStorm(Vector2 center, float damage) { for (var i = 0; i < 4; i++) { yield return new WaitForSeconds(.09f); var enemy = ClosestEnemy(center, 1.8f); if (enemy) SecondaryHit(enemy, damage, Vector2.down, 1f); } }
        void SpawnSoul(Vector2 origin) { var enemy = ClosestEnemy(origin, 6f); if (enemy) StartCoroutine(DelayedSoul(enemy, origin)); }
        IEnumerator DelayedSoul(EnemyBrain enemy, Vector2 origin) { yield return new WaitForSeconds(.2f); if (enemy) { SecondaryHit(enemy, 8f, ((Vector2)enemy.transform.position - origin).normalized, 1f); CombatVfxPool.Instance.SpawnEnemyAbilityBurst(enemy.transform.position, EnemyAttackStyle.ArcaneBurst, new Color(.5f, .2f, 1f)); } }
        void EnsureCompanions(bool temporary = false)
        {
            if (temporary) { CreateCompanion("caballero_caido", 12f); return; }
            foreach (var id in new[] { "espiritu_menor", "orbe_protector", "familiar_arcano", "lobo_espectral", "dragon_espiritual" })
            {
                if (!Has(id)) continue;
                var found = false; foreach (var companion in GetComponentsInChildren<PowerUpCompanion>()) if (companion.Identity == id) found = true;
                if (!found) CreateCompanion(id, -1f);
            }
        }
        void CreateCompanion(string id, float lifetime)
        {
            var go = new GameObject(id); go.transform.SetParent(transform, false);
            go.AddComponent<PowerUpCompanion>().Configure(this, lifetime, id);
        }
        // En la UI y descripciones se presenta como "ataque de energía": incluye
        // bastones, grimorios y cualquier arma elemental que realmente tenga coste.
        static bool IsMagic(WeaponData weapon) => weapon && weapon.UsesEnergyPower;
    }

    // Alias conservado para los power-ups ya creados.
    public class EnemyStatusEffects : ElementalStatusEffects { }

    public class PowerUpCompanion : MonoBehaviour
    {
        public string Identity { get; private set; }
        PowerUpRuntime owner; float angle, nextAttack, expires;
        public void Configure(PowerUpRuntime runtime, float lifetime, string id) { Identity = id; angle = Random.Range(0f, 360f); owner = runtime; expires = lifetime > 0f ? Time.time + lifetime : -1f; var renderer = gameObject.AddComponent<SpriteRenderer>(); renderer.sprite = CombatFeedback.WhiteSprite; renderer.color = new Color(.3f, .85f, 1f, .85f); renderer.sortingOrder = 12; transform.localScale = Vector3.one * .28f; }
        void Update() { if (!owner || expires > 0f && Time.time >= expires) { Destroy(gameObject); return; } angle += Time.deltaTime * 110f; transform.position = owner.transform.position + Quaternion.Euler(0, 0, angle) * Vector3.right * .85f;if(Identity == "orbe_protector"){foreach(var hit in Physics2D.OverlapCircleAll(transform.position,.3f)){var projectile=hit.GetComponentInParent<EnemyProjectile>();if(projectile){projectile.Dispel();CombatVfxPool.Instance.SpawnEnemyAbilityBurst(transform.position,EnemyAttackStyle.ArcaneBurst,Color.cyan);break;}}} if (Identity == "orbe_protector" || Time.time < nextAttack) return; nextAttack = Time.time + (Identity == "dragon_espiritual" ? .75f : Identity == "familiar_arcano" ? 1f : 1.5f); var enemy = FindClosest(5f); if (enemy)
            {
                CombatVfxPool.Instance.SpawnChainLightning(transform.position, enemy.transform.position);
                enemy.ReceiveHit(new CombatHit { damage = Identity == "dragon_espiritual" ? 7f : Identity == "lobo_espectral" ? 6f : 4f, direction = ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized, knockback = 1f, source = owner.gameObject, secondary = true });
                if (Identity == "dragon_espiritual" && !enemy.IsDead) { var status = enemy.GetComponent<ElementalStatusEffects>(); if (!status) status = enemy.gameObject.AddComponent<EnemyStatusEffects>(); status.Burn(owner.gameObject, 3f, 1.5f, 1f); }
            } }
        EnemyBrain FindClosest(float radius) { EnemyBrain best = null; var bestDistance = radius; foreach (var enemy in FindObjectsByType<EnemyBrain>()) { if (!enemy || enemy.IsDead) continue; var distance = Vector2.Distance(transform.position, enemy.transform.position); if (distance < bestDistance) { bestDistance = distance; best = enemy; } } return best; }
    }
}
