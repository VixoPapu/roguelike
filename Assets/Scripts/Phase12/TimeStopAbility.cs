using System.Collections.Generic;
using ProjectLike.Phase1;
using ProjectLike.Phase2;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectLike.Phase12
{
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class TimeStopAbility : MonoBehaviour
    {
        public const float MinDuration = 6f, MaxDuration = 9f, Cooldown = 25f, EnergyCost = 150f;
        float activeDuration;
        public float ActiveDuration => activeDuration;
        public static float DamageMultiplierFor(GameObject source) => active && source && source.transform.IsChildOf(active.transform) ? 1.15f : 1f;
        [Tooltip("Distancia recorrida entre siluetas de Tiempo cero.")]
        [Min(.2f)] public float ghostSpacing = .5f;
        static TimeStopAbility active;
        static float stoppedClock;
        public static bool IsActive => active;
        public static bool CanAct(GameObject actor) => !active || actor && actor.transform.IsChildOf(active.transform);
        public static float PlayerTime => Time.time + stoppedClock;
        public static float PlayerDelta => IsActive ? Time.unscaledDeltaTime : Time.deltaTime;
        public float Remaining => running ? Mathf.Max(0f, activeDuration - elapsed) : 0f;
        public float CooldownRemaining => Mathf.Max(0f, cooldownUntil - PlayerTime);
        bool running;
        float elapsed, cooldownUntil, previousScale, flashTime, startedAt, lastStoppedAt;
        PlayerStats stats;
        PlayerController player;
        PlayerVisuals visuals;
        Vector3 lastGhostPosition;
        readonly List<MonoBehaviour> frozen = new List<MonoBehaviour>();
        readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        readonly Dictionary<(Material source, Texture texture), Material> grayMaterials = new Dictionary<(Material, Texture), Material>();
        readonly Dictionary<Animator, AnimatorUpdateMode> playerAnimators = new Dictionary<Animator, AnimatorUpdateMode>();
        readonly List<ParticleSystem> pausedParticles = new List<ParticleSystem>();
        SimulationMode2D previousPhysicsMode;
        Shader grayShader;
        Camera sceneCamera;
        Color cameraColor;
        Image flash;
        LineRenderer ring, echoRing;
        AudioSource startAudio, endAudio;
        float pulseDuration;
        Color pulseColor;
        Vector3 pulseOrigin;
        static readonly Gradient TrailGradient = CreateTrailGradient();
        Material ringMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { active = null; stoppedClock = 0f; }

        void Awake()
        {
            stats = GetComponent<PlayerStats>(); player = GetComponent<PlayerController>(); visuals = GetComponent<PlayerVisuals>();
            grayShader = Resources.Load<Shader>("TimeStopGrayscale");
            var definition = Resources.Load<PowerUpData>("PowerUps/PowerUp_Tiempo_cero");
            startAudio = CreateAudio("TimeStopStartAudio", definition ? definition.abilityStartSound : null);
            endAudio = CreateAudio("TimeStopEndAudio", definition ? definition.abilityEndSound : null);
        }

        public bool TryActivate()
        {
            var inventory = GetComponent<PlayerPowerUps>();
            if (running || IsActive || Time.timeScale <= 0f || !stats || stats.CurrentHealth <= 0f || !inventory || !inventory.Has("tiempo_cero")) return false;
            if (CooldownRemaining > 0f) { Notice("Tiempo cero: recargando", true); return false; }
            if (!stats.TrySpendEnergy(EnergyCost)) { Notice("Tiempo cero: necesitas 150 de energia", true); return false; }
            activeDuration = Random.Range(MinDuration, MaxDuration);
            previousScale = Time.timeScale;
            previousPhysicsMode = Physics2D.simulationMode;
            running = true; active = this; elapsed = 0f; cooldownUntil = PlayerTime + Cooldown;
            startedAt = lastStoppedAt = Time.unscaledTime;
            DashVfxPool.ClearTimeStopTrail();
            lastGhostPosition = transform.position;
            // No se toca ninguna velocidad ni trayectoria. Sin pasos de fisica,
            // los cuerpos y sus temporizadores conservan exactamente su estado.
            foreach (var animator in GetComponentsInChildren<Animator>()) { playerAnimators[animator] = animator.updateMode; animator.updateMode = AnimatorUpdateMode.UnscaledTime; }
            Physics2D.simulationMode = SimulationMode2D.Script;
            Time.timeScale = 0f;
            sceneCamera = Camera.main;
            if (sceneCamera) { cameraColor = sceneCamera.backgroundColor; var g = cameraColor.grayscale; sceneCamera.backgroundColor = new Color(g, g, g, cameraColor.a); }
            FreezeWorld();
            if (visuals) visuals.SpawnDashGhost(Vector2.zero, true, TrailGradient.Evaluate(0f));
            PlaySound(startAudio); Pulse(true); Notice("TIEMPO DETENIDO");
            return true;
        }

        void Update()
        {
            if (ProjectLike.Online.CoopInput.For(gameObject).Down(ProjectLike.Online.CoopButtons.Ability)) TryActivate();
            if (running)
            {
                elapsed = Time.unscaledTime - startedAt;
                stoppedClock += Mathf.Max(0f, Time.unscaledTime - lastStoppedAt);
                lastStoppedAt = Time.unscaledTime;
                if (!stats || stats.CurrentHealth <= 0f || elapsed >= activeDuration) EndStop();
            }
            AnimatePulse();
        }

        void LateUpdate()
        {
            if (!running) return;
            FreezeWorld(); // Incluye proyectiles y efectos creados durante la habilidad.
            var delta = transform.position - lastGhostPosition;
            var spacing = Mathf.Max(.2f, ghostSpacing);
            if (visuals && delta.sqrMagnitude >= spacing * spacing)
            {
                visuals.SpawnDashGhost(delta.normalized, true, TrailGradient.Evaluate(Mathf.Clamp01(elapsed / activeDuration)));
                lastGhostPosition = transform.position;
            }
        }

        bool Exempt(Transform target) => target.IsChildOf(transform) || target.GetComponentInParent<Canvas>() || target.GetComponentInParent<Camera>() || target.GetComponentInParent<CameraFollow>() || target.GetComponentInParent<DashVfxPool>();

        void FreezeWorld()
        {
            foreach (var script in FindObjectsByType<MonoBehaviour>())
            {
                if (!script.enabled || script.GetType().Namespace == "ProjectLike.Online" || Exempt(script.transform) || script is PooledCombatVfx || script is CombatHitFlash) continue;
                if (script.GetType().Namespace == null || !script.GetType().Namespace.StartsWith("ProjectLike")) continue;
                script.enabled = false; frozen.Add(script);
            }
            foreach (var particles in FindObjectsByType<ParticleSystem>())
                if (!Exempt(particles.transform) && particles.isPlaying) { particles.Pause(true); pausedParticles.Add(particles); }
            if (!grayShader) return;
            foreach (var renderer in FindObjectsByType<Renderer>())
            {
                if (Exempt(renderer.transform)) continue;
                if (!originals.TryGetValue(renderer, out var materials))
                {
                    materials = renderer.sharedMaterials;
                    originals.Add(renderer, materials);
                }
                var spriteRenderer = renderer as SpriteRenderer;
                var replacements = new Material[materials.Length];
                for (var i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (!source) continue;
                    // Los sprites comparten material, pero no necesariamente atlas.
                    // El sampler propio evita perder la textura al cambiar de shader.
                    Texture texture = spriteRenderer && spriteRenderer.sprite ? spriteRenderer.sprite.texture
                        : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
                    if (!texture) texture = Texture2D.whiteTexture;
                    var key = (source, texture);
                    if (!grayMaterials.TryGetValue(key, out var replacement))
                    {
                        replacement = new Material(source) { shader = grayShader };
                        replacement.SetTexture("_TimeStopTexture", texture);
                        grayMaterials.Add(key, replacement);
                    }
                    replacements[i] = replacement;
                }
                renderer.sharedMaterials = replacements;
            }
        }

        void EndStop()
        {
            if (!running) return;
            running = false;
            DashVfxPool.ClearTimeStopTrail();
            if (active == this) { active = null; Physics2D.simulationMode = previousPhysicsMode; Time.timeScale = previousScale; }
            foreach (var script in frozen) if (script) script.enabled = true;
            frozen.Clear();
            foreach (var entry in playerAnimators) if (entry.Key) entry.Key.updateMode = entry.Value;
            playerAnimators.Clear();
            foreach (var entry in originals) if (entry.Key) entry.Key.sharedMaterials = entry.Value;
            originals.Clear();
            foreach (var material in grayMaterials.Values) if (material) Destroy(material);
            grayMaterials.Clear();
            foreach (var particles in pausedParticles) if (particles) particles.Play(true);
            pausedParticles.Clear();
            if (sceneCamera) sceneCamera.backgroundColor = cameraColor;
            PlaySound(endAudio); Pulse(false); Notice("EL TIEMPO CONTINUA");
        }

        void Notice(string message, bool warning = false) { var hud = FindAnyObjectByType<PixelHUDCanvas>(); if (hud) hud.ShowFeedback(message, warning ? new Color(1f, .52f, .25f) : new Color(.5f, 1f, .8f)); }

        static Gradient CreateTrailGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[]
            {
                new GradientColorKey(new Color(.30f, .85f, 1f), 0f),
                new GradientColorKey(new Color(.28f, .48f, 1f), .30f),
                new GradientColorKey(new Color(.80f, .25f, 1f), .55f),
                new GradientColorKey(new Color(1f, .18f, .45f), .75f),
                new GradientColorKey(new Color(1f, .06f, .04f), .92f),
                new GradientColorKey(new Color(1f, .06f, .04f), 1f)
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        AudioSource CreateAudio(string objectName, AudioClip clip)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(transform, false);
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0f;
            source.volume = .8f; source.clip = clip;
            return source;
        }

        static void PlaySound(AudioSource source)
        {
            if (!source || !source.clip) return;
            // Canales separados: el pitch de salida no modifica el sonido de entrada.
            source.pitch = Random.Range(.92f, 1.08f);
            source.Play();
            ProjectLike.Online.CoopSession.EmitSound(source.clip, source.transform.position, source.volume, source.pitch, source.spatialBlend);
        }

        void Pulse(bool entering)
        {
            if (!flash)
            {
                var root = new GameObject("TimePulse", typeof(RectTransform), typeof(Canvas));
                root.transform.SetParent(transform, false);
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 450;
                var image = new GameObject("Flash", typeof(RectTransform), typeof(Image)); image.transform.SetParent(root.transform, false);
                flash = image.GetComponent<Image>(); flash.raycastTarget = false;
                flash.rectTransform.anchorMin = Vector2.zero; flash.rectTransform.anchorMax = Vector2.one; flash.rectTransform.sizeDelta = Vector2.zero;
                var wave = new GameObject("TimeShockwave"); wave.transform.SetParent(transform, false);
                ring = wave.AddComponent<LineRenderer>(); ring.useWorldSpace = true; ring.loop = true; ring.positionCount = 65; ring.sortingOrder = 90;
                ringMaterial = new Material(Shader.Find("Sprites/Default")); ring.sharedMaterial = ringMaterial;
                var echo = new GameObject("TimeShockwaveEcho"); echo.transform.SetParent(transform, false);
                echoRing = echo.AddComponent<LineRenderer>(); echoRing.useWorldSpace = true;
                echoRing.loop = true; echoRing.positionCount = 65; echoRing.sortingOrder = 89; echoRing.sharedMaterial = ringMaterial;
            }
            pulseDuration = entering ? .65f : .4f;
            flashTime = pulseDuration;
            pulseOrigin = transform.position;
            pulseColor = entering ? TrailGradient.Evaluate(0f) : TrailGradient.Evaluate(1f);
            var follow = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow) follow.Shake(entering ? .30f : .18f);
        }

        void AnimatePulse()
        {
            if (!flash || !ring) return;
            flashTime = Mathf.Max(0f, flashTime - Time.unscaledDeltaTime);
            var t = Mathf.Clamp01(1f - flashTime / pulseDuration);
            var tint = Color.Lerp(pulseColor, Color.white, .8f);
            flash.color = new Color(tint.r, tint.g, tint.b, Mathf.Pow(1f - Mathf.Clamp01(t * 2f), 2f) * .4f);
            AnimateRing(ring, t, 7f, .22f, pulseColor);
            var echoProgress = Mathf.Clamp01((t - .12f) / .88f);
            AnimateRing(echoRing, echoProgress, 4.7f, .10f, Color.Lerp(pulseColor, Color.white, .5f));
            echoRing.enabled = flashTime > 0f && t >= .12f;
        }

        void AnimateRing(LineRenderer wave, float progress, float radius, float width, Color color)
        {
            wave.enabled = flashTime > 0f;
            wave.widthMultiplier = Mathf.Lerp(width, .005f, progress);
            wave.startColor = wave.endColor = new Color(color.r, color.g, color.b, 1f - progress);
            var size = Mathf.Lerp(.2f, radius, 1f - Mathf.Pow(1f - progress, 3f));
            for (var i = 0; i < wave.positionCount; i++)
            {
                var angle = i * Mathf.PI * 2f / 64f;
                wave.SetPosition(i, pulseOrigin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * size);
            }
        }

        void OnDisable() { EndStop(); }
        void OnDestroy() { EndStop(); if (ringMaterial) Destroy(ringMaterial); }
    }
}
