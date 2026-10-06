using System.Collections.Generic;
using ProjectLike.Phase2;
using UnityEngine;

namespace ProjectLike.Phase5
{
    /// <summary>Pool compartido para marcas y partículas de aparición de enemigos.</summary>
    public class EnemySpawnVfxPool : MonoBehaviour
    {
        static EnemySpawnVfxPool instance;
        readonly Stack<EnemySpawnMarker> available = new Stack<EnemySpawnMarker>();

        public static EnemySpawnVfxPool Instance
        {
            get
            {
                if (instance) return instance;
                var root = new GameObject("EnemySpawnVfxPool");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<EnemySpawnVfxPool>();
                instance.Prewarm(18);
                return instance;
            }
        }

        public EnemySpawnMarker Show(Vector2 position, Color color)
        {
            var marker = available.Count > 0 ? available.Pop() : CreateMarker();
            marker.gameObject.SetActive(true);
            marker.Begin(this, position, color);
            return marker;
        }

        internal void Release(EnemySpawnMarker marker)
        {
            if (!marker || !marker.gameObject.activeSelf) return;
            marker.gameObject.SetActive(false);
            marker.transform.SetParent(transform, false);
            available.Push(marker);
        }

        void Prewarm(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var marker = CreateMarker();
                marker.gameObject.SetActive(false);
                available.Push(marker);
            }
        }

        EnemySpawnMarker CreateMarker()
        {
            var markerObject = new GameObject("PooledEnemySpawnMarker");
            markerObject.transform.SetParent(transform, false);
            return markerObject.AddComponent<EnemySpawnMarker>();
        }
    }

    public class EnemySpawnMarker : MonoBehaviour
    {
        static Sprite ringSprite;
        static Material particleMaterial;

        EnemySpawnVfxPool owner;
        SpriteRenderer ring;
        SpriteRenderer core;
        Transform runes;
        ParticleSystem particles;
        Color color;
        float startedAt;
        float releaseAt;
        bool spawning;
        bool built;

        void Awake() => Build();

        void Build()
        {
            if (built) return;
            built = true;

            var ringObject = new GameObject("SpawnRing");
            ringObject.transform.SetParent(transform, false);
            ring = ringObject.AddComponent<SpriteRenderer>();
            ring.sprite = GetRingSprite();
            ring.sortingOrder = 7;

            var coreObject = new GameObject("SpawnCore");
            coreObject.transform.SetParent(transform, false);
            coreObject.transform.localScale = new Vector3(.32f, .32f, 1f);
            core = coreObject.AddComponent<SpriteRenderer>();
            core.sprite = CombatFeedback.WhiteSprite;
            core.sortingOrder = 6;

            var runeRoot = new GameObject("Runes");
            runeRoot.transform.SetParent(transform, false);
            runes = runeRoot.transform;
            for (var i = 0; i < 4; i++)
            {
                var rune = new GameObject("Rune_" + i);
                rune.transform.SetParent(runes, false);
                var angle = i * Mathf.PI * .5f;
                rune.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * .52f;
                rune.transform.localScale = new Vector3(.12f, .035f, 1f);
                rune.transform.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                var renderer = rune.AddComponent<SpriteRenderer>();
                renderer.sprite = CombatFeedback.WhiteSprite;
                renderer.sortingOrder = 8;
            }

            if (!particleMaterial) particleMaterial = new Material(Shader.Find("Sprites/Default"));
            particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = .55f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.25f, .55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.15f, .5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .09f);
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 11f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = .5f;
            shape.radiusThickness = .12f;
            var rendererModule = particles.GetComponent<ParticleSystemRenderer>();
            rendererModule.material = particleMaterial;
            rendererModule.sortingOrder = 9;
        }

        internal void Begin(EnemySpawnVfxPool pool, Vector2 position, Color markerColor)
        {
            Build();
            owner = pool;
            transform.position = position;
            transform.localScale = Vector3.one * .15f;
            transform.localRotation = Quaternion.identity;
            color = markerColor;
            startedAt = Time.time;
            releaseAt = Time.time + 15f;
            spawning = false;
            ring.color = new Color(color.r, color.g, color.b, .78f);
            core.color = new Color(color.r, color.g, color.b, .18f);
            foreach (var rune in runes.GetComponentsInChildren<SpriteRenderer>())
                rune.color = new Color(color.r, color.g, color.b, .9f);
            var main = particles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
            particles.Clear(true);
            particles.Play(true);
        }

        public void PlaySpawn()
        {
            if (spawning) return;
            spawning = true;
            releaseAt = Time.time + .32f;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            particles.Emit(22);
            ring.color = Color.white;
            core.color = new Color(1f, 1f, 1f, .85f);
        }

        void Update()
        {
            if (!owner) return;
            if (Time.time >= releaseAt) { owner.Release(this); return; }

            if (spawning)
            {
                var remaining = Mathf.Clamp01((releaseAt - Time.time) / .32f);
                transform.localScale = Vector3.one * Mathf.Lerp(1.65f, .15f, 1f - remaining);
                ring.color = new Color(1f, 1f, 1f, remaining);
                core.color = new Color(color.r, color.g, color.b, remaining);
                return;
            }

            var reveal = Mathf.Clamp01((Time.time - startedAt) * 7f);
            var pulse = 1f + Mathf.Sin((Time.time - startedAt) * 8f) * .08f;
            transform.localScale = Vector3.one * reveal * pulse;
            ring.transform.localRotation = Quaternion.Euler(0f, 0f, (Time.time - startedAt) * 70f);
            runes.localRotation = Quaternion.Euler(0f, 0f, -(Time.time - startedAt) * 105f);
            var glow = .12f + Mathf.PingPong((Time.time - startedAt) * .55f, .25f);
            core.color = new Color(color.r, color.g, color.b, glow);
        }

        static Sprite GetRingSprite()
        {
            if (ringSprite) return ringSprite;
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "RuntimeSpawnRing"
            };
            var center = new Vector2((size - 1) * .5f, (size - 1) * .5f);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), center);
                var ring = distance >= 11.5f && distance <= 14.5f;
                var cross = (Mathf.Abs(x - center.x) < 1f || Mathf.Abs(y - center.y) < 1f) && distance > 6f && distance < 10f;
                texture.SetPixel(x, y, ring || cross ? Color.white : Color.clear);
            }
            texture.Apply();
            DontDestroyOnLoad(texture);
            ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 24f);
            ringSprite.name = "RuntimeSpawnRingSprite";
            DontDestroyOnLoad(ringSprite);
            return ringSprite;
        }
    }
}
