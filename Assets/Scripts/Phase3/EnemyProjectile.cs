using System.Collections.Generic;
using ProjectLike.Phase1;
using ProjectLike.Phase2;
using UnityEngine;

namespace ProjectLike.Phase3
{
    public class EnemyProjectilePool : MonoBehaviour
    {
        static EnemyProjectilePool instance;
        readonly Stack<EnemyProjectile> available = new Stack<EnemyProjectile>();

        public static EnemyProjectilePool Instance
        {
            get
            {
                if (instance) return instance;
                var root = new GameObject("EnemyProjectilePool");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<EnemyProjectilePool>();
                instance.Prewarm(32);
                return instance;
            }
        }

        public void Spawn(Vector2 position, float damage, float speed, Vector2 direction, GameObject owner, EnemyAttackStyle style = EnemyAttackStyle.Basic, Color? effectColor = null)
        {
            var projectile = available.Count > 0 ? available.Pop() : CreateProjectile();
            projectile.gameObject.SetActive(true);
            projectile.Launch(this, position, damage, speed, direction, owner, style, effectColor ?? new Color(1f, .25f, .05f));
        }

        internal void Release(EnemyProjectile projectile)
        {
            if (!projectile || !projectile.gameObject.activeSelf) return;
            projectile.ResetForPool();
            projectile.gameObject.SetActive(false);
            projectile.transform.SetParent(transform, false);
            available.Push(projectile);
        }

        void Prewarm(int amount)
        {
            for (var i = 0; i < amount; i++)
            {
                var projectile = CreateProjectile();
                projectile.gameObject.SetActive(false);
                available.Push(projectile);
            }
        }

        EnemyProjectile CreateProjectile()
        {
            var go = new GameObject("PooledEnemyProjectile");
            go.transform.SetParent(transform, false);
            return go.AddComponent<EnemyProjectile>();
        }
    }

    public class EnemyProjectile : MonoBehaviour
    {
        static Material sharedTrailMaterial;
        EnemyProjectilePool pool;
        Rigidbody2D body;
        SpriteRenderer core;
        SpriteRenderer glow;
        TrailRenderer trail;
        GameObject owner;
        float damage;
        float life;
        float launchedAt;
        bool returning;
        EnemyAttackStyle attackStyle;
        Color effectColor;
        Sprite[] effectFrames;
        float nextEffectFrame;
        int effectFrame;

        void Awake()
        {
            body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var hitbox = gameObject.AddComponent<CircleCollider2D>();
            hitbox.isTrigger = true;
            hitbox.radius = .11f;

            var glowObject = new GameObject("ProjectileGlow");
            glowObject.transform.SetParent(transform, false);
            glowObject.transform.localScale = new Vector3(.34f, .19f, 1f);
            glow = glowObject.AddComponent<SpriteRenderer>();
            glow.sprite = SoftGlowSprite.Sprite;
            glow.sharedMaterial = SoftGlowSprite.Material;
            glow.color = new Color(1f, .16f, .035f, .32f);
            glow.sortingOrder = 24;

            var coreObject = new GameObject("ProjectileCore");
            coreObject.transform.SetParent(transform, false);
            coreObject.transform.localScale = new Vector3(.2f, .085f, 1f);
            core = coreObject.AddComponent<SpriteRenderer>();
            core.sprite = CombatFeedback.WhiteSprite;
            core.color = new Color(1f, .85f, .3f, 1f);
            core.sortingOrder = 25;

            if (!sharedTrailMaterial)
            {
                sharedTrailMaterial = new Material(Shader.Find("Sprites/Default"));
                DontDestroyOnLoad(sharedTrailMaterial);
            }
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = sharedTrailMaterial;
            trail.time = .22f;
            trail.minVertexDistance = .025f;
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.sortingOrder = 23;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, .15f), new Keyframe(.28f, .12f), new Keyframe(1f, 0f));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, .78f, .2f), 0f), new GradientColorKey(new Color(1f, .08f, .02f), 1f) },
                new[] { new GradientAlphaKey(.82f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
        }

        internal void Launch(EnemyProjectilePool sourcePool, Vector2 position, float sourceDamage, float speed, Vector2 direction, GameObject source, EnemyAttackStyle style, Color color)
        {
            pool = sourcePool;
            owner = source;
            damage = sourceDamage;
            life = 4f;
            launchedAt = Time.time;
            returning = false;
            attackStyle = style;
            effectColor = color;
            transform.position = position;
            transform.right = direction;
            body.position = position;
            body.linearVelocity = direction.normalized * speed;
            trail.Clear();
            trail.emitting = true;
            ConfigureVisuals();
        }

        void ConfigureVisuals()
        {
            var lance = attackStyle == EnemyAttackStyle.BoneLance;
            var toxic = attackStyle == EnemyAttackStyle.ToxicVolley;
            var arcane = attackStyle == EnemyAttackStyle.ArcaneBurst || attackStyle == EnemyAttackStyle.BlinkStrike;
            var yellowLightning = effectColor.r > .8f && effectColor.g > .55f && effectColor.b < .5f;
            var effectName = attackStyle == EnemyAttackStyle.FireFan ? "proj_fireball" : arcane ? (yellowLightning ? "proj_lightning_4_Y" : "proj_lightning 4_B") : null;
            effectFrames = string.IsNullOrEmpty(effectName) ? null : CombatVfxPool.LoadEffectFrames(effectName);
            effectFrame = 0; nextEffectFrame = Time.time + .075f;
            if (effectFrames != null && effectFrames.Length > 0) core.sprite = effectFrames[0];
            else core.sprite = CombatFeedback.WhiteSprite;
            core.transform.localScale = lance ? new Vector3(.42f, .065f, 1f) : arcane ? new Vector3(.16f, .16f, 1f) : new Vector3(.2f, .085f, 1f);
            if (effectFrames != null && effectFrames.Length > 0)
            {
                core.transform.localScale = Vector3.one * (attackStyle == EnemyAttackStyle.FireFan ? .2f : .24f);
                core.color = Color.white;
            }
            glow.transform.localScale = lance ? new Vector3(.9f, .3f, 1f) : arcane ? new Vector3(.72f, .72f, 1f) : new Vector3(.75f, .75f, 1f);
            core.color = Color.Lerp(effectColor, Color.white, lance ? .65f : .35f);
            glow.color = new Color(effectColor.r, effectColor.g, effectColor.b, toxic ? .5f : .34f);
            core.color = BloomColor(core.color);
            glow.color = BloomColor(glow.color);
            trail.time = lance ? .16f : toxic ? .34f : .23f;
            trail.widthMultiplier = lance ? .72f : arcane ? 1.15f : 1f;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.Lerp(effectColor, Color.white, .42f), 0f), new GradientColorKey(effectColor, 1f) },
                new[] { new GradientAlphaKey(.86f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
        }

        static Color BloomColor(Color color) => new Color(color.r * 1.55f, color.g * 1.55f, color.b * 1.55f, color.a);

        void Update()
        {
            if (ProjectLike.Phase12.TimeStopAbility.IsActive || returning) return;
            life -= Time.deltaTime;
            var pulse = 1f + Mathf.Sin((Time.time - launchedAt) * 22f) * .12f;
            var baseGlow = attackStyle == EnemyAttackStyle.BoneLance ? new Vector3(.9f, .3f, 1f) : attackStyle == EnemyAttackStyle.ArcaneBurst || attackStyle == EnemyAttackStyle.BlinkStrike ? new Vector3(.72f, .72f, 1f) : new Vector3(.75f, .75f, 1f);
            glow.transform.localScale = baseGlow * pulse;
            if (attackStyle != EnemyAttackStyle.BoneLance) glow.transform.Rotate(0f, 0f, 210f * Time.deltaTime);
            if (effectFrames != null && effectFrames.Length > 0 && Time.time >= nextEffectFrame)
            {
                effectFrame = (effectFrame + 1) % effectFrames.Length;
                core.sprite = effectFrames[effectFrame];
                nextEffectFrame = Time.time + .075f;
            }
            if (body.linearVelocity.sqrMagnitude > .01f) transform.right = body.linearVelocity.normalized;
            if (life <= 0f) Release(false);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (returning || !other) return;
            if (owner && (other.gameObject == owner || other.transform.IsChildOf(owner.transform))) return;
            if (other.GetComponentInParent<EnemyBrain>()) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player)
            {
                player.TakeDamage(damage, transform.position);
                ApplyAttackStatus(player);
                var elite = owner ? owner.GetComponent<EliteEnemy>() : null; if (elite) elite.ApplyOnHit(player, damage);
                Release(true);
                return;
            }
            if (!other.isTrigger) Release(true);
        }

        void ApplyAttackStatus(PlayerController player)
        {
            if (!player || attackStyle == EnemyAttackStyle.Basic || attackStyle == EnemyAttackStyle.CrescentSlash || attackStyle == EnemyAttackStyle.HeavySmash) return;
            var status = player.GetComponent<ProjectLike.Phase12.ElementalStatusEffects>(); if (!status) status = player.gameObject.AddComponent<ProjectLike.Phase12.ElementalStatusEffects>();
            if (attackStyle == EnemyAttackStyle.FireFan) status.Burn(owner, 3f, Mathf.Max(.5f, damage * .2f));
            else if (attackStyle == EnemyAttackStyle.ToxicVolley) status.Poison(owner, 4f, Mathf.Max(.4f, damage * .15f));
            else if (attackStyle == EnemyAttackStyle.BoneLance || attackStyle == EnemyAttackStyle.TripleClaw || attackStyle == EnemyAttackStyle.Pounce) status.Bleed(owner, 3.5f, Mathf.Max(.35f, damage * .12f));
            else if (attackStyle == EnemyAttackStyle.ArcaneBurst) status.Shock(owner, 2.5f, 0f);
            else if (attackStyle == EnemyAttackStyle.BlinkStrike) status.Curse(owner, 3.5f);
        }

        public void Dispel() => Release(false);

        void Release(bool impact)
        {
            if (returning) return;
            returning = true;
            if (impact)
            {
                CombatVfxPool.Instance.SpawnEnemyProjectileImpact(transform.position, body.linearVelocity.normalized, attackStyle);
                CombatVfxPool.Instance.SpawnEnemyAbilityBurst(transform.position, attackStyle, effectColor);
            }
            if (pool) pool.Release(this);
            else Destroy(gameObject);
        }

        internal void ResetForPool()
        {
            body.linearVelocity = Vector2.zero;
            trail.emitting = false;
            trail.Clear();
            owner = null;
            effectFrames = null;
            core.sprite = CombatFeedback.WhiteSprite;
        }
    }
}
