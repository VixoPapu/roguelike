using System;
using ProjectLike.Online;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectLike.Phase1;
using ProjectLike.Phase12;
using ProjectLike.Phase3;
using ProjectLike.Audio;

namespace ProjectLike.Phase2
{
    public enum WeaponType { Sword, Axe, Hammer, Spear, Dagger, Bow, Crossbow, Staff, Grimoire, Shield, Legendary }
    public enum WeaponRarity { Common, Uncommon, Rare, Epic, Legendary }
    public enum WeaponSpecialEffectType { None, Bleed, Fire, Ice, Shock, Stun, Wave, ProjectileBreak, Reflect, Summon, Orbiting, AutoAttack, Poison, Curse, Gravity, EnergySiphon, Execution, ShieldShred, LifeSteal, Nova }
    public enum WeaponAttackPattern { Legacy, Sweep, Thrust, Spin, Fan, Radial }
    public enum WeaponProjectileMotion { Straight, Homing, Returning, Orbiting }
    public enum ImpactSurface { Flesh, Stone, Wood, Metal, Magic }

    [CreateAssetMenu(menuName="Project Like/Combat/Weapon Data", fileName="Weapon_")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")] public string weaponName="New Weapon"; [TextArea] public string description; public Sprite icon; public Sprite sprite; public WeaponType type; public WeaponRarity rarity;
        [Header("Tilesheet selection (0-based, 16x16)")] public Texture2D spriteSheet; public int spriteColumn; public int spriteRow;
        [Header("Combat")] public float damage=10; public float attackSpeed=4; public float range=1.5f; public float knockback=2; [Range(0,1)] public float criticalChance=.1f; public float criticalMultiplier=2; public float energyCost=0; public float recovery=.2f; public int comboAttacks=1; public float attackDuration=.15f; public Vector2 hitbox=new Vector2(1.5f,.8f);
        [Header("Projectile / special")] public bool projectile; public bool charged; public float chargeTime=1; public float projectileSpeed=10; public int projectileCount=1; public int bounces; public bool piercing; public bool elemental; [TextArea] public string specialEffect; public WeaponSpecialEffectType[] specialEffects; [Range(0f, 1f)] public float specialEffectChance=.25f; [Min(.1f)] public float specialEffectPower=1f; public Color effectColor=Color.white; public GameObject visualEffect; public AudioClip attackSound; public int price=10;
        internal WeaponSpecialEffectType[] SpecialEffects => specialEffects;
        [Header("Attack identity")]
        public WeaponAttackPattern attackPattern;
        public WeaponProjectileMotion projectileMotion;
        [Range(10f, 180f)] public float sweepAngle = 110f;
        [Range(0f, 180f)] public float spreadAngle = 14f;
        [Min(.2f)] public float projectileLifetime = 3f;
        public bool UsesEnergyPower => energyCost > 0f && (elemental || type == WeaponType.Staff || type == WeaponType.Grimoire || type == WeaponType.Legendary);
        public bool HasSpecial(WeaponSpecialEffectType effect) => specialEffects != null && Array.IndexOf(specialEffects, effect) >= 0;
    }

    public struct CombatHit { public float damage; public Vector2 direction; public float knockback; public bool critical; public bool secondary; public WeaponData weapon; public GameObject source; }
    public interface ICombatTarget { void ReceiveHit(CombatHit hit); }
    public class CombatTarget : MonoBehaviour, ICombatTarget
    {
        public float health=30; public bool destroyOnDeath=true; public ImpactSurface impactSurface=ImpactSurface.Flesh;
        public void ReceiveHit(CombatHit hit){health-=hit.damage; var rb=GetComponent<Rigidbody2D>();if(rb)rb.AddForce(hit.direction*hit.knockback,ForceMode2D.Impulse);CombatFeedback.ShowImpact(transform.position,hit,impactSurface);var flash=GetComponent<CombatHitFlash>();if(!flash)flash=gameObject.AddComponent<CombatHitFlash>();flash.Play();Debug.Log($"{name} received {(hit.critical?"CRITICAL ":"")}{hit.damage:0} damage");if(health<=0&&destroyOnDeath)Destroy(gameObject);}
    }
    public class WeaponController : MonoBehaviour
    {
        [Header("Two weapon loadout")]
        public WeaponData equippedWeapon;
        public WeaponData primaryWeapon;
        public WeaponData secondaryWeapon;
        [SerializeField, Range(1, 2)] int activeSlot = 1;
        public bool autoAim;
        public WeaponData EquippedWeapon => equippedWeapon;
        public WeaponData PrimaryWeapon => primaryWeapon;
        public WeaponData SecondaryWeapon => secondaryWeapon;
        public int ActiveSlot => activeSlot;
        public bool IsCharging => charging;
        public float Charge01 => charging && equippedWeapon ? Mathf.Clamp01(held / Mathf.Max(.01f, equippedWeapon.chargeTime)) : 0f;
        bool UsesCharge => equippedWeapon && (equippedWeapon.charged || equippedWeapon.type == WeaponType.Bow);

        float nextAttack;
        int combo;
        float held;
        bool charging;
        SpriteRenderer weaponRenderer;
        Sprite runtimeSprite;
        PlayerStats stats;
        AudioSource audioSource;

        void Awake()
        {
            stats = GetComponent<PlayerStats>();
            audioSource = GetComponent<AudioSource>();
            if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
            var defaults = Resources.Load<DefaultWeaponLoadout>("DefaultWeaponLoadout");
            if (!primaryWeapon) primaryWeapon = equippedWeapon ? equippedWeapon : defaults ? defaults.primary : CreateFallbackSword();
            if (!secondaryWeapon) secondaryWeapon = defaults && defaults.secondary ? defaults.secondary : CreateFallbackBow(primaryWeapon ? primaryWeapon.spriteSheet : null);
            SwitchSlot(activeSlot, true);
        }

        void Update()
        {
            if (CoopInput.Blocked(gameObject) || Time.timeScale <= 0f && !TimeStopAbility.IsActive) return;
            var controls = CoopInput.For(gameObject);
            if (controls.Down(CoopButtons.Primary)) SwitchSlot(1);
            if (controls.Down(CoopButtons.Secondary)) SwitchSlot(2);
            if (!equippedWeapon) return;
            var down = controls.Down(CoopButtons.Attack);
            var hold = controls.Hold(CoopButtons.Attack);
            var up = controls.Up(CoopButtons.Attack);
            if (down && UsesCharge) { charging = true; held = 0f; GameSfx.Play(SfxCue.WeaponCharge, transform.position, .52f); }
            if (charging && hold) held = Mathf.Min(equippedWeapon.chargeTime, held + TimeStopAbility.PlayerDelta);
            if (down && !UsesCharge) Fire(1f);
            if (up && charging)
            {
                var charge = Mathf.Clamp01(held / Mathf.Max(.01f, equippedWeapon.chargeTime));
                charging = false;
                Fire(Mathf.Lerp(.65f, 1.8f, charge));
                held = 0f;
            }
            if (weaponRenderer)
            {
                var direction = GetAim();
                weaponRenderer.transform.right = direction;
                weaponRenderer.enabled = equippedWeapon.spriteSheet || equippedWeapon.sprite;
                var pulse = charging ? 1f + Charge01 * .22f + Mathf.Sin(TimeStopAbility.PlayerTime * 22f) * .025f : 1f;
                weaponRenderer.transform.localScale = Vector3.one * pulse;
                weaponRenderer.color = charging ? Color.Lerp(Color.white, new Color(.35f, .82f, 1f), Charge01) : Color.white;
            }
            SetCameraBowZoom(charging ? Charge01 : 0f);
        }
        void Fire(float charge)
        {
            var powers=GetComponent<PowerUpRuntime>();
            if(TimeStopAbility.PlayerTime<nextAttack || (stats && (powers ? !powers.TrySpendEnergy(equippedWeapon.energyCost,equippedWeapon) : !stats.TrySpendEnergy(equippedWeapon.energyCost)))) return;
            var recovery=Mathf.Max(.02f,equippedWeapon.recovery>0?equippedWeapon.recovery:1f/Mathf.Max(.01f,equippedWeapon.attackSpeed));
            nextAttack=TimeStopAbility.PlayerTime+recovery*(powers?powers.AttackCooldownMultiplier(equippedWeapon):1f);
            combo=(combo+1)%Mathf.Max(1,equippedWeapon.comboAttacks);
            var aim=GetAim();
            CombatFeedback.ShowAttack(transform.position,aim,equippedWeapon,combo);
            GameSfx.WeaponAttack(gameObject, equippedWeapon);
            if(equippedWeapon.attackSound) { audioSource.PlayOneShot(equippedWeapon.attackSound); CoopSession.EmitSound(equippedWeapon.attackSound, transform.position, audioSource.volume, audioSource.pitch, audioSource.spatialBlend); }
            if(equippedWeapon.visualEffect) Instantiate(equippedWeapon.visualEffect,transform.position,Quaternion.FromToRotation(Vector3.right,aim));
            if(equippedWeapon.projectile)FireProjectiles(aim,charge);else FireMelee(aim,charge);
        }
        Vector2 GetAim() => CoopInput.For(gameObject).aim;
        float PlayerDamageScale => (stats ? Mathf.Max(.1f, stats.damage / 10f) : 1f) * TimeStopAbility.DamageMultiplierFor(gameObject);
        float CriticalChance => Mathf.Clamp01(equippedWeapon.criticalChance + (stats ? stats.criticalChance : 0f) + (GetComponent<PowerUpRuntime>() ? GetComponent<PowerUpRuntime>().MeleeCriticalBonus(equippedWeapon) : 0f));
        void FireMelee(Vector2 aim, float charge)
        {
            var weapon = equippedWeapon;
            var origin = (Vector2)transform.position;
            var struck = new HashSet<ICombatTarget>();
            Collider2D[] hits;
            if (weapon.attackPattern == WeaponAttackPattern.Thrust)
                hits = Physics2D.OverlapBoxAll(origin + aim * weapon.range * .5f, new Vector2(weapon.range, weapon.hitbox.y), Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            else if (weapon.attackPattern == WeaponAttackPattern.Legacy)
                hits = Physics2D.OverlapCircleAll(origin + aim * weapon.range, weapon.hitbox.x * .5f);
            else hits = Physics2D.OverlapCircleAll(origin, weapon.range);
            foreach (var col in hits)
            {
                if (col.transform.IsChildOf(transform)) continue;
                var away = (Vector2)col.bounds.center - origin;
                if (weapon.attackPattern == WeaponAttackPattern.Sweep && Vector2.Angle(aim, away) > weapon.sweepAngle * .5f) continue;
                var target = col.GetComponentInParent<ICombatTarget>();
                if (target == null || !struck.Add(target)) continue;
                var crit = UnityEngine.Random.value < CriticalChance;
                target.ReceiveHit(new CombatHit { damage = weapon.damage * PlayerDamageScale * charge * (crit ? weapon.criticalMultiplier : 1f), direction = weapon.attackPattern == WeaponAttackPattern.Spin ? away.normalized : aim,
                    knockback = weapon.knockback, critical = crit, weapon = weapon, source = gameObject });
            }
        }
        void FireProjectiles(Vector2 aim, float charge)
        {
            var powers = GetComponent<PowerUpRuntime>();
            var weapon = equippedWeapon;
            var count = Mathf.Max(1, weapon.projectileCount) + (powers ? powers.ExtraProjectileCount(weapon) : 0);
            var spread = weapon.spreadAngle * (powers && powers.Has("punteria") ? .6f : 1f);
            for (var i = 0; i < count; i++)
            {
                var angle = weapon.attackPattern == WeaponAttackPattern.Radial ? i * 360f / count : count == 1 ? 0f : Mathf.Lerp(-spread * .5f, spread * .5f, i / (float)(count - 1));
                var direction = (Vector2)(Quaternion.Euler(0, 0, angle) * aim);
                var go = new GameObject(weapon.weaponName + "_Projectile");
                go.transform.position = transform.position + (Vector3)direction * .35f;
                go.AddComponent<CombatProjectile>().Setup(weapon, direction, charge * PlayerDamageScale * (count > weapon.projectileCount ? .82f : 1f), gameObject, CriticalChance);
            }
        }
        void BuildVisual(){var previous=transform.Find("WeaponVisual");if(previous)Destroy(previous.gameObject);var child=new GameObject("WeaponVisual");child.transform.SetParent(transform,false);child.transform.localPosition=new Vector3(.35f,0,0);weaponRenderer=child.AddComponent<SpriteRenderer>();weaponRenderer.sortingOrder=11;if(equippedWeapon.sprite)weaponRenderer.sprite=equippedWeapon.sprite;else if(equippedWeapon.spriteSheet){var t=equippedWeapon.spriteSheet;var x=Mathf.Clamp(equippedWeapon.spriteColumn,0,t.width/16-1)*16;var y=t.height-(Mathf.Clamp(equippedWeapon.spriteRow,0,t.height/16-1)+1)*16;runtimeSprite=Sprite.Create(t,new Rect(x,y,16,16),new Vector2(.15f,.5f),16);weaponRenderer.sprite=runtimeSprite;}}
        public void SwitchSlot(int slot, bool force = false)
        {
            slot = Mathf.Clamp(slot, 1, 2);
            if (!force && activeSlot == slot) return;
            activeSlot = slot;
            charging = false;
            held = 0f;
            SetCameraBowZoom(0f);
            equippedWeapon = activeSlot == 1 ? primaryWeapon : secondaryWeapon;
            BuildVisual();
            if (!force) GetComponent<PowerUpRuntime>()?.OnWeaponSwitched();
        }
        void OnDisable() { SetCameraBowZoom(0f); }
        void SetCameraBowZoom(float amount)
        {
            if (CoopInput.IsRemote(gameObject)) return;
            var follow = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow) follow.SetBowChargeZoom(amount);
        }
        public void Equip(WeaponData data) => EquipToSlot(activeSlot, data);
        public void ResetForNewRun()
        {
            var defaults = Resources.Load<DefaultWeaponLoadout>("DefaultWeaponLoadout");
            primaryWeapon = defaults && defaults.primary ? defaults.primary : CreateFallbackSword();
            secondaryWeapon = defaults && defaults.secondary ? defaults.secondary : CreateFallbackBow(primaryWeapon ? primaryWeapon.spriteSheet : null);
            activeSlot = 1; equippedWeapon = primaryWeapon; charging = false; held = 0f; nextAttack = 0f; combo = 0;
            BuildVisual();
        }
        public void EquipToSlot(int slot, WeaponData data)
        {
            if (!data) return;
            if (slot == 1) primaryWeapon = data; else secondaryWeapon = data;
            activeSlot = Mathf.Clamp(slot, 1, 2);
            equippedWeapon = data;
            charging = false;
            held = 0f;
            SetCameraBowZoom(0f);
            BuildVisual();
        }
        WeaponData CreateFallbackSword(){var d=ScriptableObject.CreateInstance<WeaponData>();d.weaponName="Starter Sword";d.type=WeaponType.Sword;d.damage=12;d.range=1.4f;d.hitbox=new Vector2(1.4f,.9f);return d;}
        WeaponData CreateFallbackBow(Texture2D sheet){var d=ScriptableObject.CreateInstance<WeaponData>();d.weaponName="Starter Bow";d.type=WeaponType.Bow;d.damage=14;d.range=7f;d.projectile=true;d.charged=true;d.chargeTime=1.15f;d.projectileSpeed=10f;d.attackSpeed=2f;d.recovery=.38f;d.spriteSheet=sheet;d.spriteColumn=15;d.spriteRow=0;return d;}
    }
    public class CombatProjectile : MonoBehaviour
    {
        readonly HashSet<ICombatTarget> struck = new HashSet<ICombatTarget>();
        float age; Vector2 orbitOrigin; bool spent;
        WeaponData data; Vector2 direction; float damageScale; float criticalChance; GameObject owner; float life=3;int remainingPierces,remainingBounces;bool homing,ignoreWalls;Rigidbody2D body;
        public void Setup(WeaponData d,Vector2 dir,float scale,GameObject source,float overrideCriticalChance=-1f){data=d;life=d.projectileLifetime;orbitOrigin=transform.position;direction=dir;damageScale=scale;criticalChance=overrideCriticalChance>=0f?overrideCriticalChance:d.criticalChance;owner=source;var powers=source?source.GetComponent<PowerUpRuntime>():null;remainingPierces=d.piercing?99:powers?powers.ProjectilePierceCount:0;remainingBounces=d.bounces+(powers?powers.ProjectileBounces:0);homing=d.projectileMotion==WeaponProjectileMotion.Homing || powers&&powers.ProjectileHoming;ignoreWalls=powers&&powers.Has("flecha_espectral");var sr=gameObject.AddComponent<SpriteRenderer>();var effect=d.effectColor.a>0f?d.effectColor:Color.cyan;sr.color=d.elemental?effect:Color.Lerp(Color.yellow,Color.white,Mathf.InverseLerp(.65f,1.8f,scale));sr.sprite=CombatFeedback.GetWeaponSprite(d);if(!sr.sprite)sr.sprite=CombatFeedback.WhiteSprite;transform.right=dir;transform.localScale=Vector3.one*Mathf.Lerp(.8f,1.28f,Mathf.InverseLerp(.65f,1.8f,scale));var col=gameObject.AddComponent<CircleCollider2D>();col.isTrigger=true;body=gameObject.AddComponent<Rigidbody2D>();body.gravityScale=0;var speedMultiplier=Mathf.Lerp(.72f,1.55f,Mathf.InverseLerp(.65f,1.8f,scale));var playerStats=source?source.GetComponent<PlayerStats>():null;speedMultiplier*=playerStats?Mathf.Max(.25f,playerStats.projectileSpeed/8f):1f;body.linearVelocity=dir*d.projectileSpeed*speedMultiplier;}
        void Update(){if(TimeStopAbility.IsActive)return;life-=Time.deltaTime;age+=Time.deltaTime;
            if (data.projectileMotion == WeaponProjectileMotion.Returning && age > .45f && owner && body)
            {
                var back = (Vector2)(owner.transform.position - transform.position);
                if (back.magnitude < .3f) { Destroy(gameObject); return; }
                direction = back.normalized; body.linearVelocity = direction * Mathf.Max(8f, data.projectileSpeed); transform.right = direction;
            }
            if (data.projectileMotion == WeaponProjectileMotion.Orbiting && body)
            {
                var center = owner ? (Vector2)owner.transform.position : orbitOrigin;
                var angle = age * 240f + Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                var desired = center + (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector2.right) * Mathf.Min(data.range, .35f + age * 5f);
                body.linearVelocity = (desired - (Vector2)transform.position) / Mathf.Max(.001f, Time.deltaTime);
            }
            if(life<=0){Destroy(gameObject);return;}if(!ignoreWalls&&CheckWall())return;if(homing&&body){EnemyBrain closest=null;var best=3.5f;foreach(var enemy in FindObjectsByType<EnemyBrain>()){if(!enemy||enemy.IsDead)continue;var distance=Vector2.Distance(transform.position,enemy.transform.position);if(distance<best){best=distance;closest=enemy;}}if(closest){var desired=((Vector2)closest.transform.position-(Vector2)transform.position).normalized;direction=Vector2.Lerp(direction,desired,Time.deltaTime*3.5f).normalized;body.linearVelocity=direction*body.linearVelocity.magnitude;transform.right=direction;}}}
        bool CheckWall(){foreach(var hit in Physics2D.RaycastAll(transform.position,direction,.12f)){if(!hit.collider||hit.collider.transform==transform||hit.collider.isTrigger||hit.collider.GetComponentInParent<ICombatTarget>()!=null||owner&&hit.collider.transform.IsChildOf(owner.transform))continue;if(remainingBounces<=0){Destroy(gameObject);return true;}remainingBounces--;direction=Vector2.Reflect(direction,hit.normal).normalized;transform.position+=(Vector3)direction*.08f;if(body)body.linearVelocity=direction*body.linearVelocity.magnitude;transform.right=direction;return false;}return false;}
        void OnTriggerEnter2D(Collider2D other)
        {
            if (spent || TimeStopAbility.IsActive || owner && other.transform.IsChildOf(owner.transform)) return;
            var target = other.GetComponentInParent<ICombatTarget>();
            if (target == null || !struck.Add(target)) return;
            if (remainingPierces > 0) remainingPierces--; else spent = true;
            var crit = UnityEngine.Random.value < criticalChance;
            target.ReceiveHit(new CombatHit { damage = data.damage * damageScale * (crit ? data.criticalMultiplier : 1f), direction = direction,
                knockback = data.knockback, critical = crit, weapon = data, source = owner });
            if (spent) Destroy(gameObject);
        }
        void OnCollisionEnter2D(Collision2D collision){if(remainingBounces<=0){Destroy(gameObject);return;}remainingBounces--;direction=Vector2.Reflect(direction,collision.GetContact(0).normal).normalized;if(body)body.linearVelocity=direction*body.linearVelocity.magnitude;transform.right=direction;}
    }
}
