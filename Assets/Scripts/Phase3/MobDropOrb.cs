using ProjectLike.Phase1;
using ProjectLike.Phase2;
using UnityEngine;
using ProjectLike.Audio;

namespace ProjectLike.Phase3
{
    public enum MobDropType { Coin, Energy, Health }

    public class MobDropOrb : MonoBehaviour
    {
        MobDropPool pool;
        MobDropType type;
        PlayerStats target;
        SpriteRenderer core;
        SpriteRenderer glow;
        TrailRenderer trail;
        ParticleSystem collectParticles;
        Vector2 velocity;
        float value;
        float homingAt;
        float spawnedAt;
        float returnAt;
        bool homing;
        bool collected;
        static Material sharedMaterial;

        void Awake() { BuildVisuals(); }

        public void Setup(MobDropPool owner, MobDropType dropType, float amount, Vector2 origin, PlayerStats recipient)
        {
            if (!core) BuildVisuals();
            pool = owner; type = dropType; value = amount; target = recipient;
            transform.SetParent(null); transform.position = origin; transform.localScale = Vector3.one;
            var direction = Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < .1f) direction = Vector2.up;
            velocity = direction * Random.Range(2.4f, 4.2f);
            homingAt = Time.time + Random.Range(.4f, .75f);
            spawnedAt = Time.time;
            homing = false; collected = false;
            core.enabled = true; glow.enabled = true;
            var color = type == MobDropType.Coin ? new Color(1f, .72f, .08f) : type == MobDropType.Energy ? new Color(.08f, .65f, 1f) : new Color(1f, .08f, .18f);
            core.color = color; glow.color = new Color(color.r, color.g, color.b, .32f);
            core.transform.localScale = type == MobDropType.Coin ? new Vector3(.2f, .2f, 1) : type == MobDropType.Energy ? new Vector3(.17f, .25f, 1) : new Vector3(.24f, .2f, 1);
            trail.startColor = color; trail.endColor = new Color(color.r, color.g, color.b, 0);
            var particlesMain = collectParticles.main; particlesMain.startColor = color;
            trail.Clear(); trail.emitting = false;
            collectParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void Update()
        {
            if (collected)
            {
                if (Time.time >= returnAt) pool.Recycle(this);
                return;
            }
            if (!target) { if (Time.time - spawnedAt > 8f) pool.Recycle(this); return; }
            if (!homing && Time.time >= homingAt) { homing = true; trail.emitting = true; }
            if (!homing)
            {
                velocity = Vector2.Lerp(velocity, Vector2.zero, 4.5f * Time.deltaTime);
                transform.position += (Vector3)(velocity * Time.deltaTime);
            }
            else
            {
                var delta = (Vector2)(target.transform.position - transform.position);
                if (delta.sqrMagnitude < .1f) { Collect(); return; }
                var elapsed = Mathf.Clamp01((Time.time - homingAt) / .65f);
                var desired = delta.normalized * Mathf.Lerp(5f, 14f, elapsed);
                velocity = Vector2.MoveTowards(velocity, desired, 30f * Time.deltaTime);
                transform.position += (Vector3)(velocity * Time.deltaTime);
            }
            transform.Rotate(0, 0, (type == MobDropType.Coin ? 240f : type == MobDropType.Energy ? -180f : 120f) * Time.deltaTime);
            glow.transform.localScale = Vector3.one * (1.5f + Mathf.Sin(Time.time * 12f) * .18f);
        }

        void Collect()
        {
            if (collected || !target) return;
            if (type == MobDropType.Coin) target.coins += Mathf.RoundToInt(value);
            else if (type == MobDropType.Energy) target.RestoreEnergy(value);
            else target.Heal(value);
            GameSfx.Play(type == MobDropType.Coin ? SfxCue.Coin : type == MobDropType.Energy ? SfxCue.Energy : SfxCue.Health, transform.position, .62f);
            collected = true;
            core.enabled = false; glow.enabled = false; trail.emitting = false;
            collectParticles.Play();
            returnAt = Time.time + .28f;
        }

        void BuildVisuals()
        {
            if (!sharedMaterial) sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            core = GetComponent<SpriteRenderer>(); if (!core) core = gameObject.AddComponent<SpriteRenderer>();
            core.sprite = CombatFeedback.WhiteSprite; core.sortingOrder = 30;
            var glowObject = new GameObject("Glow"); glowObject.transform.SetParent(transform, false);
            glow = glowObject.AddComponent<SpriteRenderer>(); glow.sprite = CombatFeedback.WhiteSprite; glow.sortingOrder = 29;
            trail = gameObject.AddComponent<TrailRenderer>(); trail.material = sharedMaterial; trail.time = .22f; trail.startWidth = .13f; trail.endWidth = 0; trail.minVertexDistance = .035f; trail.sortingOrder = 28; trail.emitting = false;
            collectParticles = gameObject.AddComponent<ParticleSystem>();
            // Al añadirlo Unity comienza a reproducirlo automáticamente. Hay que
            // detenerlo antes de cambiar duration para evitar warnings por drop.
            collectParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = collectParticles.main; main.duration = .18f; main.loop = false; main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .24f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f); main.startSize = new ParticleSystem.MinMaxCurve(.05f, .12f); main.maxParticles = 12; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = collectParticles.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 8) });
            var shape = collectParticles.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .12f;
            var particleRenderer = collectParticles.GetComponent<ParticleSystemRenderer>(); particleRenderer.material = sharedMaterial; particleRenderer.sortingOrder = 31;
            collectParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
