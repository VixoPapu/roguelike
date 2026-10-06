using System.Collections.Generic;
using ProjectLike.Phase1;
using ProjectLike.Phase3;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLike.Phase2
{
    /// <summary>Shared pool for all short-lived combat visuals. It grows only when an effect type is exhausted.</summary>
    public class CombatVfxPool : MonoBehaviour
    {
        static CombatVfxPool instance;
        readonly Queue<PooledCombatVfx> visuals = new Queue<PooledCombatVfx>();
        readonly Queue<PooledDamageNumber> numbers = new Queue<PooledDamageNumber>();
        Sprite ringSprite;
        Sprite slashArcSprite;

        public static CombatVfxPool Instance
        {
            get
            {
                if (instance) return instance;
                var root = new GameObject("CombatVfxPool");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<CombatVfxPool>();
                return instance;
            }
        }

        public void SpawnAttackSlash(Vector2 origin, Vector2 direction, WeaponData weapon, int comboIndex)
        {
            if (weapon.rarity >= WeaponRarity.Rare && weapon.SpecialEffects != null && weapon.SpecialEffects.Length > 0)
            {
                var castColor = weapon.effectColor;
                GetVisual().Play(RingSprite, origin + direction * .35f, new Color(castColor.r, castColor.g, castColor.b, .6f), Vector2.zero,
                    Vector3.one * .2f, .24f, 0f, Vector3.one * .65f, 0f, 29);
                for (var i = 0; i < 3 + (int)weapon.rarity; i++)
                    SpawnColoredSpark(origin + direction * .45f, direction + Random.insideUnitCircle, castColor, .7f, 2f);
            }
            if (weapon.projectile) return;
            if (weapon.attackPattern == WeaponAttackPattern.Spin)
            {
                GetVisual().Play(RingSprite, origin, weapon.effectColor, Vector2.zero, Vector3.one * .3f, .28f, 0f,
                    Vector3.one * (weapon.range * 2.22f - .3f), 180f, 30);
                for (var i = 0; i < 3; i++) GetVisual().Play(SlashArcSprite, origin, weapon.effectColor, Vector2.zero,
                    Vector3.one * weapon.range * .55f, .25f, i * 120f, Vector3.zero, 320f, 31);
                return;
            }
            if (weapon.attackPattern == WeaponAttackPattern.Thrust)
            {
                var thrustAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                GetVisual().Play(CombatFeedback.WhiteSprite, origin + direction * weapon.range * .5f, weapon.effectColor, direction * .4f,
                    new Vector3(weapon.range, .035f, 1f), .13f, thrustAngle, Vector3.zero, 0f, 31);
                GetVisual().Play(SlashArcSprite, origin + direction * weapon.range * .7f, Color.Lerp(weapon.effectColor, Color.white, .5f), Vector2.zero,
                    new Vector3(.35f, weapon.hitbox.y * .6f, 1f), .16f, thrustAngle, Vector3.zero, 0f, 32);
                return;
            }
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var color = CombatFeedback.GetImpactColor(weapon, ImpactSurface.Metal);
            var mirror = comboIndex % 2 == 0 ? 1f : -1f;
            var scale = Mathf.Max(.82f, .72f + weapon.range * .34f);
            var position = origin + direction * Mathf.Max(.12f, weapon.range * .16f);
            var duration = Mathf.Clamp(weapon.attackDuration * 1.18f, .15f, .28f);

            var outerArc = GetVisual();
            outerArc.Play(SlashArcSprite, position, new Color(color.r, color.g, color.b, .78f), Vector2.zero,
                new Vector3(scale, scale * mirror, 1f), duration, angle,
                new Vector3(.16f, .16f * mirror, 0f), mirror * 52f, 30);

            var innerArc = GetVisual();
            innerArc.Play(SlashArcSprite, position + direction * .035f, Color.Lerp(color, Color.white, .82f), Vector2.zero,
                new Vector3(scale * .86f, scale * .86f * mirror, 1f), duration * .72f, angle,
                new Vector3(.08f, .08f * mirror, 0f), mirror * 68f, 31);

            var tip = origin + direction * Mathf.Max(.65f, weapon.range * .92f);
            for (var i = 0; i < 4; i++)
            {
                var sparkAngle = angle + Random.Range(-34f, 34f);
                var sparkDirection = (Vector2)(Quaternion.Euler(0f, 0f, sparkAngle) * Vector2.right);
                GetVisual().Play(CombatFeedback.WhiteSprite, tip + Random.insideUnitCircle * .035f, Color.Lerp(color, Color.white, .55f),
                    sparkDirection * Random.Range(.8f, 1.8f), new Vector3(Random.Range(.035f, .065f), .018f, 1f),
                    Random.Range(.08f, .15f), sparkAngle, Vector3.one * .018f, Random.Range(-480f, 480f), 32);
            }
        }

        public void SpawnEnemyAttack(Vector2 origin, Vector2 direction, EnemyAttackStyle style, Color color, float strength = 1f)
        {
            direction = direction.sqrMagnitude > .001f ? direction.normalized : Vector2.right;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var heavy = style == EnemyAttackStyle.HeavySmash;
            var claws = style == EnemyAttackStyle.TripleClaw;
            var blink = style == EnemyAttackStyle.BlinkStrike;
            var count = claws ? 3 : 1;
            for (var i = 0; i < count; i++)
            {
                var offset = claws ? (i - 1) * .16f : 0f;
                var perpendicular = new Vector2(-direction.y, direction.x);
                var arc = GetVisual();
                var scale = (heavy ? 1.42f : blink ? .92f : 1.02f) * Mathf.Lerp(.9f, 1.2f, Mathf.Clamp01(strength));
                arc.Play(SlashArcSprite, origin + direction * .38f + perpendicular * offset,
                    Color.Lerp(color, Color.white, blink ? .62f : .28f), Vector2.zero,
                    new Vector3(scale, scale * (i % 2 == 0 ? 1f : -1f), 1f), heavy ? .28f : .2f,
                    angle - (claws ? 8f : 0f), Vector3.one * (heavy ? .25f : .12f), (i % 2 == 0 ? 1f : -1f) * (heavy ? 34f : 62f), 29 + i);
            }

            if (heavy)
            {
                GetVisual().Play(RingSprite, origin + direction * .55f, new Color(color.r, color.g, color.b, .72f), Vector2.zero,
                    Vector3.one * .18f, .34f, 0f, Vector3.one * 1.35f, 0f, 28);
                for (var i = 0; i < 9; i++) SpawnColoredSpark(origin + direction * .5f, Random.insideUnitCircle.normalized, color, 1.2f, 2.8f);
            }
        }

        public void SpawnChargerSlash(Vector2 origin, Vector2 direction, Color color)
        {
            direction = direction.sqrMagnitude > .001f ? direction.normalized : Vector2.right;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GetVisual().Play(SlashArcSprite, origin + direction * .5f, new Color(color.r, color.g, color.b, .9f), direction * 1.1f,
                new Vector3(1.35f, .82f, 1f), .27f, angle, new Vector3(.46f, .25f, 0f), 38f, 32);
            GetVisual().Play(SlashArcSprite, origin + direction * .58f, new Color(1f, .92f, .72f, .82f), direction * 1.35f,
                new Vector3(1.02f, .58f, 1f), .18f, angle, new Vector3(.28f, .16f, 0f), 52f, 33);
            var perpendicular = new Vector2(-direction.y, direction.x);
            for (var i = 0; i < 8; i++)
            {
                var streakDirection = -direction * Random.Range(.8f, 1.8f) + perpendicular * Random.Range(-.7f, .7f);
                SpawnColoredSpark(origin - direction * Random.Range(.05f, .35f), streakDirection, color, 1f, 2.1f);
            }
        }

        public void SpawnEnemyAbilityBurst(Vector2 origin, EnemyAttackStyle style, Color color)
        {
            var count = style == EnemyAttackStyle.ArcaneBurst ? 12 : style == EnemyAttackStyle.FireFan ? 8 : 6;
            GetVisual().Play(RingSprite, origin, new Color(color.r, color.g, color.b, .7f), Vector2.zero,
                Vector3.one * .12f, .28f, Random.Range(0f, 360f), Vector3.one * .82f, style == EnemyAttackStyle.ArcaneBurst ? 95f : 25f, 30);
            for (var i = 0; i < count; i++)
            {
                var direction = (Vector2)(Quaternion.Euler(0f, 0f, i * (360f / count) + Random.Range(-8f, 8f)) * Vector2.right);
                SpawnColoredSpark(origin, direction, color, .8f, style == EnemyAttackStyle.FireFan ? 2.8f : 2f);
            }
        }

        public void SpawnWeaponProc(Vector2 origin, WeaponData weapon, WeaponSpecialEffectType effect)
        {
            var color = weapon.effectColor;
            switch (effect)
            {
                case WeaponSpecialEffectType.Fire: color = new Color(1f, .28f, .025f); break;
                case WeaponSpecialEffectType.Ice: color = new Color(.35f, .9f, 1f); break;
                case WeaponSpecialEffectType.Shock: color = new Color(.35f, .65f, 1f); break;
                case WeaponSpecialEffectType.Poison: color = new Color(.4f, 1f, .08f); break;
                case WeaponSpecialEffectType.Bleed: color = new Color(1f, .08f, .2f); break;
                case WeaponSpecialEffectType.Curse: color = new Color(.78f, .2f, 1f); break;
                case WeaponSpecialEffectType.Gravity: color = new Color(.6f, .12f, 1f); break;
                case WeaponSpecialEffectType.Nova: color = new Color(1f, .6f, .08f); break;
                case WeaponSpecialEffectType.EnergySiphon: color = Color.cyan; break;
                case WeaponSpecialEffectType.LifeSteal: color = new Color(1f, .1f, .3f); break;
            }
            var wave = effect == WeaponSpecialEffectType.Wave || effect == WeaponSpecialEffectType.ProjectileBreak || effect == WeaponSpecialEffectType.Gravity || effect == WeaponSpecialEffectType.Nova;
            // Ring diameter matches the two-unit damage radius (2.2 for projectile clearing).
            var diameter = wave ? (effect == WeaponSpecialEffectType.ProjectileBreak ? 4.9f : 4.45f) : .95f;
            GetVisual().Play(RingSprite, origin, new Color(color.r, color.g, color.b, .62f), Vector2.zero,
                Vector3.one * .15f, wave ? .4f : .26f, 0f, Vector3.one * (diameter - .15f), 0f, 30);
            var count = 5 + (int)weapon.rarity;
            for (var i = 0; i < count; i++)
            {
                var direction = (Vector2)(Quaternion.Euler(0f, 0f, i * 360f / count) * Vector2.right);
                SpawnColoredSpark(origin, direction, color, 1.2f, wave ? 4f : 2.6f);
            }
            if (effect == WeaponSpecialEffectType.Fire) SpawnStatusMotes(origin, color, true);
            if (effect == WeaponSpecialEffectType.Ice)
                for (var i = 0; i < 4; i++)
                    GetVisual().Play(CombatFeedback.WhiteSprite, origin + Random.insideUnitCircle * .25f, color, Vector2.up * .25f,
                        new Vector3(.045f, .22f, 1f), .4f, i * 45f, Vector3.zero, 0f, 32);
        }

        public void SpawnChainLightning(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            var normal = new Vector2(-delta.y, delta.x).normalized;
            var previous = from;
            const int segments = 6;
            for (var i = 1; i <= segments; i++)
            {
                var next = Vector2.Lerp(from, to, i / (float)segments) + (i == segments ? Vector2.zero : normal * Random.Range(-.16f, .16f));
                var segment = next - previous;
                var angle = Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg;
                var middle = (previous + next) * .5f;
                GetVisual().Play(CombatFeedback.WhiteSprite, middle, new Color(.15f, .45f, 1f, .45f), Vector2.zero,
                    new Vector3(segment.magnitude, .1f, 1f), .24f, angle, Vector3.zero, 0f, 32);
                GetVisual().Play(CombatFeedback.WhiteSprite, middle, new Color(.75f, .95f, 1f), Vector2.zero,
                    new Vector3(segment.magnitude, .025f, 1f), .18f, angle, Vector3.zero, 0f, 33);
                previous = next;
            }
        }

        public void SpawnStatusMotes(Vector2 origin, Color color, bool burning)
        {
            for (var i = 0; i < 3; i++)
                GetVisual().Play(CombatFeedback.WhiteSprite, origin + Random.insideUnitCircle * .28f,
                    Color.Lerp(color, burning ? Color.yellow : Color.white, Random.Range(0f, .4f)),
                    new Vector2(Random.Range(-.12f, .12f), burning ? .9f : .45f),
                    new Vector3(.035f, burning ? .14f : .06f, 1f), .38f, Random.Range(-20f, 20f), Vector3.zero, 0f, 32);
        }

        void SpawnColoredSpark(Vector2 origin, Vector2 direction, Color color, float minimumSpeed, float maximumSpeed)
        {
            var normalized = direction.sqrMagnitude > .001f ? direction.normalized : Random.insideUnitCircle.normalized;
            var angle = Mathf.Atan2(normalized.y, normalized.x) * Mathf.Rad2Deg;
            GetVisual().Play(CombatFeedback.WhiteSprite, origin + Random.insideUnitCircle * .04f, Color.Lerp(color, Color.white, Random.Range(.12f, .48f)),
                normalized * Random.Range(minimumSpeed, maximumSpeed), new Vector3(Random.Range(.035f, .075f), Random.Range(.012f, .028f), 1f),
                Random.Range(.12f, .24f), angle, Vector3.one * .025f, Random.Range(-420f, 420f), 31);
        }

        public void SpawnImpact(Vector2 position, CombatHit hit, ImpactSurface surface)
        {
            var damageFactor = Mathf.Clamp01(hit.damage / 36f);
            var heavy = damageFactor > .55f;
            var color = hit.critical ? new Color(1f, .83f, .08f) : CombatFeedback.GetImpactColor(hit.weapon, surface);
            var impactPosition = position + hit.direction.normalized * .06f;
            var primaryCount = 4 + Mathf.RoundToInt(damageFactor * 5) + (hit.critical ? 5 : 0) + Random.Range(-1, 2);
            var secondaryCount = 2 + Mathf.RoundToInt(damageFactor * 3) + (hit.critical ? 3 : 0);
            var size = .055f + damageFactor * .08f + (hit.critical ? .03f : 0);

            // Contact flash: very short, readable and never screen-filling.
            var flash = GetVisual();
            flash.Play(CombatFeedback.WhiteSprite, impactPosition, Color.Lerp(color, Color.white, .58f), Vector2.zero,
                Vector3.one * (.15f + damageFactor * .17f + (hit.critical ? .08f : 0)), hit.critical ? .12f : .075f,
                Random.Range(0f, 360f), Vector3.one * (.55f + damageFactor * .6f), 0, 34);

            var incomingAngle = Mathf.Atan2(hit.direction.y, hit.direction.x) * Mathf.Rad2Deg;
            for (var i = 0; i < primaryCount; i++)
            {
                var angle = incomingAngle + Random.Range(-58f, 58f);
                var direction = Quaternion.Euler(0, 0, angle) * Vector2.right;
                var shard = GetVisual();
                shard.Play(CombatFeedback.WhiteSprite, impactPosition + Random.insideUnitCircle * .035f, color,
                    direction * Random.Range(1.4f, 3f + damageFactor * 2.2f),
                    new Vector3(size * Random.Range(.75f, 1.35f), size * Random.Range(.22f, .45f), 1),
                    Random.Range(.11f, .21f), angle, Vector3.one * Random.Range(.02f, .08f), Random.Range(-720f, 720f), 32);
            }

            for (var i = 0; i < secondaryCount; i++)
            {
                var angle = incomingAngle + Random.Range(-125f, 125f);
                var direction = Quaternion.Euler(0, 0, angle) * Vector2.right;
                var particle = GetVisual();
                particle.Play(CombatFeedback.WhiteSprite, impactPosition, Color.Lerp(color, Color.white, .3f),
                    direction * Random.Range(.55f, 1.4f), Vector3.one * size * Random.Range(.22f, .5f),
                    Random.Range(.08f, .15f), Random.Range(0f, 360f), Vector3.zero, Random.Range(-900f, 900f), 31);
            }

            if (heavy || hit.critical)
            {
                var wave = GetVisual();
                wave.Play(RingSprite, impactPosition, new Color(color.r, color.g, color.b, hit.critical ? .8f : .55f), Vector2.zero,
                    Vector3.one * (.12f + damageFactor * .08f), hit.critical ? .24f : .16f, 0,
                    Vector3.one * (.8f + damageFactor * .95f), Random.Range(-35f, 35f), 31);
            }

            GetNumber().Play(impactPosition + Vector2.up * .3f + Random.insideUnitCircle * .04f, hit.damage, hit.critical);
            var camera = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;
            if (camera) camera.Shake(hit.critical ? .22f : .07f + damageFactor * .1f);
        }

        public void SpawnPlayerDamage(Vector2 position, float damage, Vector2 hitDirection)
        {
            if (damage <= 0f) return;
            GetNumber().Play(position + Vector2.up * .42f, damage, false, true);
            hitDirection = hitDirection.sqrMagnitude > .001f ? hitDirection.normalized : Vector2.up;
            var color = new Color(1f, .08f, .1f);
            GetVisual().Play(CombatFeedback.WhiteSprite, position, new Color(1f, .72f, .68f, .88f), Vector2.zero,
                Vector3.one * .24f, .11f, Random.Range(0f, 360f), Vector3.one * .5f, 0f, 37);
            GetVisual().Play(RingSprite, position, new Color(1f, .08f, .1f, .72f), Vector2.zero,
                Vector3.one * .12f, .24f, 0f, Vector3.one * .72f, 0f, 35);
            var impactAngle = Mathf.Atan2(hitDirection.y, hitDirection.x) * Mathf.Rad2Deg;
            var count = 8 + Mathf.RoundToInt(Mathf.Clamp01(damage / 12f) * 6f);
            for (var i = 0; i < count; i++)
            {
                var angle = impactAngle + Random.Range(-68f, 68f);
                var direction = (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector2.right);
                GetVisual().Play(CombatFeedback.WhiteSprite, position + Random.insideUnitCircle * .06f,
                    Color.Lerp(color, Color.white, Random.Range(.1f, .48f)), direction * Random.Range(1.4f, 3.8f),
                    new Vector3(Random.Range(.045f, .095f), Random.Range(.015f, .035f), 1f), Random.Range(.12f, .24f),
                    angle, Vector3.one * .025f, Random.Range(-620f, 620f), 36);
            }
            var camera = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;
            if (camera) camera.Shake(Mathf.Clamp(.16f + damage / 45f, .18f, .38f));
        }

        public void SpawnEnemyProjectileImpact(Vector2 position, Vector2 direction)
        {
            var color = new Color(1f, .2f, .04f);
            var flash = GetVisual();
            flash.Play(CombatFeedback.WhiteSprite, position, new Color(1f, .82f, .28f), Vector2.zero,
                Vector3.one * .16f, .09f, Random.Range(0f, 360f), Vector3.one * .24f, 0f, 34);
            for (var i = 0; i < 6; i++)
            {
                var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f + Random.Range(-72f, 72f);
                var sparkDirection = (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector2.right);
                GetVisual().Play(CombatFeedback.WhiteSprite, position, Color.Lerp(color, Color.yellow, Random.value * .45f),
                    sparkDirection * Random.Range(1.2f, 2.8f), new Vector3(Random.Range(.035f, .07f), .025f, 1f),
                    Random.Range(.1f, .2f), angle, Vector3.one * .025f, Random.Range(-500f, 500f), 32);
            }
            GetVisual().Play(RingSprite, position, new Color(1f, .16f, .03f, .6f), Vector2.zero,
                Vector3.one * .1f, .16f, 0f, Vector3.one * .42f, 0f, 31);
        }

        PooledCombatVfx GetVisual()
        {
            if (visuals.Count > 0)
            {
                var reused = visuals.Dequeue(); reused.gameObject.SetActive(true); return reused;
            }
            var go = new GameObject("PooledCombatVfx"); go.transform.SetParent(transform);
            var visual = go.AddComponent<PooledCombatVfx>(); visual.pool = this; return visual;
        }

        PooledDamageNumber GetNumber()
        {
            if (numbers.Count > 0)
            {
                var reused = numbers.Dequeue(); reused.gameObject.SetActive(true); return reused;
            }
            var go = new GameObject("PooledDamageNumber", typeof(RectTransform)); go.transform.SetParent(transform);
            var number = go.AddComponent<PooledDamageNumber>(); number.pool = this; return number;
        }

        internal void Return(PooledCombatVfx visual) { visual.gameObject.SetActive(false); visuals.Enqueue(visual); }
        internal void Return(PooledDamageNumber number) { number.gameObject.SetActive(false); numbers.Enqueue(number); }

        Sprite RingSprite
        {
            get
            {
                if (ringSprite) return ringSprite;
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                for (var y = 0; y < 32; y++) for (var x = 0; x < 32; x++)
                {
                    var radius = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                    texture.SetPixel(x, y, radius > 11.2f && radius < 14.5f ? Color.white : Color.clear);
                }
                texture.Apply();
                ringSprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 32);
                return ringSprite;
            }
        }

        Sprite SlashArcSprite
        {
            get
            {
                if (slashArcSprite) return slashArcSprite;
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "RuntimeSlashArc"
                };
                var center = new Vector2((size - 1) * .5f, (size - 1) * .5f);
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var delta = new Vector2(x, y) - center;
                    var radius = delta.magnitude;
                    var degrees = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                    var radialAlpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(37f, 42f, radius)) *
                                      (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(56f, 61f, radius)));
                    var angleAlpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(54f, 67f, Mathf.Abs(degrees)));
                    var alpha = radialAlpha * angleAlpha;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                texture.Apply();
                slashArcSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 64f);
                slashArcSprite.name = "RuntimeSlashArcSprite";
                return slashArcSprite;
            }
        }
    }

    public class PooledCombatVfx : MonoBehaviour
    {
        internal CombatVfxPool pool;
        SpriteRenderer renderer;
        float duration, elapsed, spin;
        Vector3 velocity, initialScale, scaleGrowth;
        Color initialColor;

        void Awake() { renderer = gameObject.AddComponent<SpriteRenderer>(); renderer.sortingOrder = 31; }
        public void Play(Sprite sprite, Vector2 position, Color color, Vector2 movement, Vector3 scale, float life, float angle, Vector3 growth, float rotationSpeed, int order)
        {
            transform.position = position; transform.rotation = Quaternion.Euler(0, 0, angle); transform.localScale = scale;
            renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order;
            velocity = movement; initialScale = scale; scaleGrowth = growth; initialColor = color; duration = life; elapsed = 0; spin = rotationSpeed;
        }
        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / Mathf.Max(.001f, duration));
            var eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.position += velocity * Time.unscaledDeltaTime;
            transform.localScale = initialScale + scaleGrowth * eased;
            transform.Rotate(0, 0, spin * Time.unscaledDeltaTime);
            var fade = 1f - Mathf.SmoothStep(.18f, 1f, t);
            renderer.color = new Color(initialColor.r, initialColor.g, initialColor.b, initialColor.a * fade);
            if (t >= 1) pool.Return(this);
        }
    }

    public class PooledDamageNumber : MonoBehaviour
    {
        internal CombatVfxPool pool;
        Canvas canvas;
        Text text;
        float duration, elapsed;
        Color initialColor;

        void Awake()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 40;
            transform.localScale = Vector3.one * .008f;
            var textObject = new GameObject("Value", typeof(RectTransform));
            textObject.transform.SetParent(transform, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220, 48);
            text = textObject.AddComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.fontSize = 38;
            var outline = textObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, -2);
        }
        public void Play(Vector2 position, float damage, bool critical, bool playerDamage = false)
        {
            var hud = Object.FindAnyObjectByType<PixelHUDCanvas>();
            text.font = hud && hud.pixelFont ? hud.pixelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = playerDamage ? $"-{damage:0}" : critical ? $"CRIT {damage:0}" : damage.ToString("0");
            text.color = playerDamage ? new Color(1f, .2f, .18f) : critical ? new Color(1f, .82f, .05f) : Color.white;
            text.fontSize = playerDamage ? 42 : critical ? 46 : 38;
            canvas.worldCamera = Camera.main;
            transform.position = position;
            transform.localScale = Vector3.one * .014f * (critical ? 1.25f : playerDamage ? 1.14f : Random.Range(.95f, 1.12f));
            initialColor = text.color; duration = critical ? .72f : playerDamage ? .68f : .56f; elapsed = 0;
        }
        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            transform.position += Vector3.up * Time.unscaledDeltaTime * (.72f + (1 - t) * .25f);
            text.color = new Color(initialColor.r, initialColor.g, initialColor.b, 1 - t);
            if (t >= 1) pool.Return(this);
        }
    }
}
