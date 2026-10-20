using System.Collections;
using System.Collections.Generic;
using ProjectLike.Phase1;
using ProjectLike.Phase2;
using ProjectLike.Phase5;
using ProjectLike.Online;
using ProjectLike.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectLike.Phase3
{
    enum KharosParticleStyle { Arcane, Laser, Spore, Teleport, Shield, Thorn, Bloom, Dash, Pulse, Vine }

    /// <summary>Unique controller for Kharos. EnemyBrain remains responsible for health and room completion.</summary>
    [DisallowMultipleComponent]
    public sealed class KharosBossAI : MonoBehaviour
    {
        const float CloseRange = 4.7f;
        const float PulseRadius = 3.35f;
        static Material lineMaterial;
        static Sprite groundRingSprite;
        static readonly HashSet<KharosBossAI> activeBosses = new HashSet<KharosBossAI>();
        EnemyBrain brain;
        SpriteRenderer sprite;
        SpriteRenderer sourceSprite;
        SpriteRenderer spriteGlow;
        Collider2D bossCollider;
        RoomController room;
        Transform target;
        GameObject shieldVisual;
        SpriteRenderer auraRenderer;
        Color baseTint = Color.white;
        float nextPulse, nextGhost, nextBunker, nextLasso, nextSpore, nextVolley, nextRift, nextRush, closeTime, stunUntil;
        float displayedHealth = 1f, orbitSwapAt;
        int orbitDirection = 1;
        Vector2 attackMovement;
        GameObject bossHud;
        AudioSource bossMusicSource;
        CanvasGroup bossHudGroup;
        Image bossHealthFill;
        RectTransform bossHealthFillRect;
        const float BossHealthFillWidth = 514f;
        Text bossNameText, bossHealthText, bossPhaseText;
        float hudIntro, lastHealthRatio = 1f, healthHitFlash;
        float nextBloom, nextCross, nextSporeBurst, nextBarrage;
        bool busy, deathPlayed, enragePlayed, hudLeaving, ghosting, bossMusicStarted, bossMusicPaused;
        bool isHologram, cloneSpawned;
        bool introStarted, introFinished, outroStarted;
        int cinematicPhase;
        float cinematicProgress;
        Vector3 cinematicCameraPosition;
        readonly List<PlayerController> cinematicLockedPlayers = new List<PlayerController>();
        GameObject activeCross;
        KharosBossCinematicOverlay cinematicOverlay;
        KharosBossAI cloneOwner;
        KharosBossAI hologramClone;
        const float HologramOpacity = .58f;
        float AttackDamage => brain && brain.data ? brain.data.damage * (isHologram ? .7f : 1f) : 0f;
        float Damage(float multiplier, float minimum) => Mathf.Max(isHologram ? .8f : minimum, AttackDamage * multiplier);
        bool hudReady;
        static Font cachedBossFont;
        public bool IsImmune { get; private set; }
        public bool IsProtectedByHologram => !isHologram && hologramClone && hologramClone.brain && !hologramClone.brain.IsDead;
        public bool ShouldReplicateBossHud => !isHologram && brain && !brain.IsDead && cinematicPhase != 1 && cinematicPhase != 2;
        public string ReplicatedBossName => bossNameText ? bossNameText.text : "KHAROS";
        public string ReplicatedBossPhase => bossPhaseText ? bossPhaseText.text : "GUARDIAN DE LAS RAICES";
        public float ReplicatedHealth => brain ? brain.CurrentHealth : 0f;
        public float ReplicatedMaxHealth => brain ? brain.RuntimeMaxHealth : 0f;
        public string ReplicatedMusicName => bossMusicSource && bossMusicSource.isPlaying && bossMusicSource.clip ? bossMusicSource.clip.name : "";
        public float ReplicatedMusicVolume => bossMusicSource && bossMusicSource.isPlaying ? bossMusicSource.volume : 0f;
        public int ReplicatedCinematicPhase => cinematicPhase;
        public float ReplicatedCinematicProgress => cinematicProgress;
        public Vector3 ReplicatedCinematicCameraPosition => cinematicCameraPosition;
        public float ReplicatedCinematicCameraSize => Camera.main && Camera.main.orthographic ? Camera.main.orthographicSize : 5f;
        public float ReplicatedCinematicCameraRoll => Camera.main ? Camera.main.transform.eulerAngles.z : 0f;
        public bool ShouldReplicateEncounter => !isHologram && brain;

        public void ConfigureAsHologram(KharosBossAI owner)
        {
            isHologram = true;
            cloneOwner = owner;
        }

        public static void WarmupSpawnAssets()
        {
            _ = SoftGlowSprite.Sprite;
            _ = SoftGlowSprite.Material;
            MakeLineMaterial();
        }

        public static void SetEncounterMusicPaused(bool paused)
        {
            foreach (var boss in activeBosses)
            {
                if (!boss) continue;
                boss.bossMusicPaused = paused;
                if (!boss.bossMusicSource) continue;
                if (paused) boss.bossMusicSource.Pause();
                else boss.bossMusicSource.UnPause();
            }
        }

        public static void StopEncounterMusic()
        {
            foreach (var boss in activeBosses)
            {
                if (!boss) continue;
                if (boss.bossMusicSource)
                {
                    boss.bossMusicSource.Stop();
                    Destroy(boss.bossMusicSource);
                    boss.bossMusicSource = null;
                }
                boss.bossMusicStarted = true;
            }
        }

        void Start()
        {
            if (!isHologram) activeBosses.Add(this);
            brain = GetComponent<EnemyBrain>();
            sourceSprite = GetComponent<SpriteRenderer>();
            if (!sourceSprite) sourceSprite = GetComponentInChildren<SpriteRenderer>(true);
            if (sourceSprite && brain && brain.data && brain.data.sprite)
            {
                sourceSprite.sprite = brain.data.sprite;
                sourceSprite.enabled = false;
            }
            var walkVisual = GetComponent<EnemyWalkVisual>();
            if (!walkVisual) walkVisual = gameObject.AddComponent<EnemyWalkVisual>();
            if (walkVisual) walkVisual.visualScaleMultiplier = isHologram ? 1.08f : 1.3f;
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var candidate in renderers)
                if (candidate && candidate != sourceSprite && candidate.enabled) { sprite = candidate; break; }
            if (!sprite && sourceSprite) { sprite = sourceSprite; sprite.enabled = true; }
            if (sprite && brain && brain.data && brain.data.sprite) { sprite.sprite = brain.data.sprite; sprite.enabled = true; }
            if (sprite)
            {
                var visibleColor = sprite.color; visibleColor.a = isHologram ? HologramOpacity : 1f;
                if (isHologram) visibleColor = new Color(.35f, .91f, 1f, HologramOpacity);
                sprite.color = visibleColor;
                if (sourceSprite) sourceSprite.color = visibleColor;
                baseTint = visibleColor;
            }
            if (sourceSprite && sourceSprite != sprite) sourceSprite.enabled = false;
            var localBar = GetComponent<EnemyHealthBar>();
            if (localBar && !isHologram)
            {
                localBar.enabled = false;
                foreach (var renderer in localBar.GetComponentsInChildren<SpriteRenderer>(true)) renderer.enabled = false;
            }
            if (isHologram && !GetComponent<EnemyHealthBar>()) gameObject.AddComponent<EnemyHealthBar>();
            BuildAura();
            bossCollider = GetComponent<Collider2D>();
            room = brain ? brain.OwningRoom : null;
            nextPulse = Time.time + (isHologram ? Random.Range(1f, 2.4f) : 2f);
            nextGhost = Time.time + 7f;
            nextBunker = Time.time + 22f;
            nextLasso = Time.time + 8f;
            nextSpore = Time.time + 12f;
            nextVolley = Time.time + 4f;
            nextRift = Time.time + 9f;
            nextRush = Time.time + 5f;
            nextBloom = Time.time + 11f;
            nextCross = Time.time + 16f;
            nextSporeBurst = Time.time + 21f;
            nextBarrage = Time.time + 8f;
            if (isHologram)
            {
                nextGhost = Time.time + 4f; nextBunker = Time.time + 8f;
                nextLasso = Time.time + Random.Range(1.2f, 2.8f);
                nextSpore = Time.time + Random.Range(2f, 3.2f);
                nextVolley = Time.time + Random.Range(.9f, 2f);
                nextRift = Time.time + Random.Range(2.5f, 4f);
                nextRush = Time.time + Random.Range(1f, 2.5f);
                nextBloom = Time.time + Random.Range(3f, 4.5f);
                nextCross = Time.time + Random.Range(3.5f, 5f);
                nextSporeBurst = Time.time + Random.Range(4f, 5.5f);
            }
            orbitSwapAt = Time.time + 1.6f;
            MakeLineMaterial();
        }

        public void Tick(Transform playerTransform)
        {
            if (!brain || brain.IsDead) return;
            target = playerTransform;
            if (!isHologram && room && room.State == RoomState.Combat && !introFinished)
            {
                if (!introStarted) StartCoroutine(PlayBossIntro());
                brain.SetCustomMovement(Vector2.zero, EnemyState.Alert);
                return;
            }
            if (!isHologram && !bossMusicStarted && introFinished) StartBossMusic();
            if (!target) { brain.SetCustomMovement(Vector2.zero, EnemyState.Idle); return; }
            var delta = (Vector2)(target.position - transform.position);
            var distance = delta.magnitude;
            var direction = distance > .01f ? delta / distance : Vector2.right;
            brain.SetCustomFacing(direction);

            if (Time.time < stunUntil) { attackMovement = Vector2.zero; brain.SetCustomMovement(Vector2.zero, EnemyState.Stun); return; }
            if (busy) { brain.SetCustomMovement(attackMovement, IsImmune ? EnemyState.Recovery : EnemyState.Attack); return; }

            closeTime = distance < 3.8f ? closeTime + Time.deltaTime : Mathf.Max(0f, closeTime - Time.deltaTime * .8f);
            var healthRatio = brain.RuntimeMaxHealth > 0f ? brain.CurrentHealth / brain.RuntimeMaxHealth : 1f;
            if (!isHologram && !cloneSpawned && healthRatio <= .25f) StartCoroutine(SummonHologram());
            var enrage = isHologram || healthRatio <= .5f;
            if (Time.time >= nextGhost && (healthRatio < .7f || closeTime >= 2.5f))
            {
                nextGhost = Time.time + (enrage ? 5.8f : 8.5f); closeTime = 0f; StartCoroutine(GhostShift(direction)); return;
            }
            if (!IsImmune && Time.time >= nextBunker)
            {
                StartCoroutine(RootBunker()); return;
            }
            if (!isHologram && healthRatio <= .32f && Time.time >= nextBarrage)
            {
                nextBarrage = Time.time + 17f;
                StartCoroutine(ThornBarrage()); return;
            }
            if (Time.time >= nextRush && distance > 3.5f && distance < 10f)
            {
                nextRush = Time.time + (enrage ? 4.2f : 7.5f); StartCoroutine(ThornRush(direction)); return;
            }
            if (Time.time >= nextCross)
            {
                nextCross = Time.time + (enrage ? 6f : 11f); StartCoroutine(RootCross()); return;
            }
            if (Time.time >= nextBloom)
            {
                nextBloom = Time.time + (enrage ? 6f : 10f); StartCoroutine(ThornBloom(target.position)); return;
            }
            if (Time.time >= nextSporeBurst && distance > 3f)
            {
                nextSporeBurst = Time.time + (enrage ? 5.5f : 9.5f); StartCoroutine(SporeBurst()); return;
            }
            if (Time.time >= nextRift)
            {
                nextRift = Time.time + (enrage ? 6f : 10.5f); StartCoroutine(RootRift(target.position)); return;
            }
            if (Time.time >= nextVolley && distance > 3f)
            {
                nextVolley = Time.time + (enrage ? 4.2f : 7f); StartCoroutine(ThornVolley(direction)); return;
            }
            if (distance < CloseRange && Time.time >= nextPulse)
            {
                nextPulse = Time.time + (enrage ? 3.2f : 5.2f); StartCoroutine(DreadPulse()); return;
            }
            if (Time.time >= nextLasso && distance > 4f)
            {
                nextLasso = Time.time + (enrage ? 5.8f : 9.5f); StartCoroutine(ToxicLasso(target)); return;
            }
            if (Time.time >= nextSpore)
            {
                nextSpore = Time.time + (enrage ? 7.5f : 12.5f); StartCoroutine(SporeRain(target.position)); return;
            }

            if (Time.time >= orbitSwapAt) { orbitDirection *= -1; orbitSwapAt = Time.time + Random.Range(1.1f, 1.9f); }
            Vector2 movement;
            if (distance > 5.4f) movement = direction * (enrage ? 1f : .9f);
            else if (distance < 2.8f) movement = -direction;
            else movement = Vector2.Perpendicular(direction) * orbitDirection + direction * Mathf.Sin(Time.time * 2.1f) * .18f;
            brain.SetCustomMovement(movement, movement.sqrMagnitude > .01f ? EnemyState.Chase : EnemyState.Alert);
        }

        void Update()
        {
            if (!deathPlayed && brain && brain.IsDead)
            {
                deathPlayed = true;
                if (isHologram)
                {
                    if (cloneOwner && cloneOwner.bossPhaseText) cloneOwner.bossPhaseText.text = "FASE III · ECO DESTRUIDO";
                    if (cloneOwner && cloneOwner.hologramClone == this) cloneOwner.hologramClone = null;
                    SpawnEpicParticles(transform.position, new Color(.3f, .86f, 1f), 48, 2.7f, .65f, KharosParticleStyle.Teleport);
                    Burst(transform.position, EnemyAttackStyle.ArcaneBurst, new Color(.32f, .9f, 1f));
                    PlayBossSound(SfxCue.EnemyDeath, .42f);
                    return;
                }
                hudLeaving = true;
                if (bossHud) bossHud.GetComponent<KharosBossHudFade>()?.FadeAndDestroy(.42f);
                if (bossMusicSource) StartCoroutine(FadeBossMusicOut());
                if (bossNameText) bossNameText.text = "KHAROS DERROTADO";
                if (bossPhaseText) bossPhaseText.text = "EL BOSQUE VUELVE A RESPIRAR";
                if (bossHealthFillRect) bossHealthFillRect.sizeDelta = new Vector2(0f, 10f);
                Burst(transform.position, EnemyAttackStyle.ArcaneBurst, new Color(.42f, 1f, .52f));
                PlayBossSound(SfxCue.EnemyDeath, .9f);
                var collapse = CreateRing(transform.position, 4.5f, new Color(.65f, 1f, .65f, .8f), 48, .12f);
                if (collapse) StartCoroutine(FadeOutVisual(collapse, .65f));
                SpawnEpicParticles(transform.position, new Color(.62f, 1f, .55f), 160, 5.2f, 1.5f);
                ShakeCamera(.24f);
                if (activeCross) { activeCross.AddComponent<KharosExternalEffectFade>().Begin(.55f); activeCross = null; }
                if (!outroStarted) StartCoroutine(PlayBossOutro());
            }
            if (!isHologram && brain && bossHealthFill && !brain.IsDead)
            {
                var ratio = brain.RuntimeMaxHealth > 0f ? Mathf.Clamp01(brain.CurrentHealth / brain.RuntimeMaxHealth) : 0f;
                if (ratio < lastHealthRatio - .002f) healthHitFlash = .18f;
                lastHealthRatio = ratio;
                healthHitFlash = Mathf.MoveTowards(healthHitFlash, 0f, Time.unscaledDeltaTime);
                if (!enragePlayed && ratio <= .5f)
                {
                    enragePlayed = true;
                    if (bossNameText) bossNameText.text = "KHAROS - FASE DESATADA";
                    if (bossPhaseText) bossPhaseText.text = "FASE II - RAICES DESATADAS";
                    Burst(transform.position, EnemyAttackStyle.ArcaneBurst, new Color(1f, .3f, .16f));
                    PlayBossSound(SfxCue.EnemySpecial, .9f);
                    var phaseRing = CreateRing(transform.position, 2.8f, new Color(1f, .32f, .14f, .9f), 48, .1f);
                    if (phaseRing) StartCoroutine(FadeOutVisual(phaseRing, .5f));
                    SpawnEpicParticles(transform.position, new Color(1f, .25f, .12f), 90, 3.6f, 1.15f, KharosParticleStyle.Shield);
                    ShakeCamera(.2f);
                }
                if (ratio <= .25f && bossNameText)
                {
                    bossNameText.text = cloneSpawned ? "KHAROS · ECO DESATADO" : "KHAROS · FASE FINAL";
                    if (bossPhaseText) bossPhaseText.text = cloneSpawned ? "FASE III · DOBLE AMENAZA" : "FASE III · EL ECO SE MANIFIESTA";
                }
                displayedHealth = Mathf.MoveTowards(displayedHealth, ratio, Time.unscaledDeltaTime * 2.4f);
                if (bossHealthFillRect) bossHealthFillRect.sizeDelta = new Vector2(BossHealthFillWidth * displayedHealth, 10f);
                var baseBarColor = ratio <= .5f ? new Color(.86f, .18f, .08f) : new Color(.56f, .11f, .12f);
                bossHealthFill.color = Color.Lerp(baseBarColor, new Color(1f, .72f, .34f), healthHitFlash / .18f);
                if (bossHealthText) bossHealthText.text = Mathf.CeilToInt(brain.CurrentHealth) + " / " + Mathf.CeilToInt(brain.RuntimeMaxHealth);
            }
            if (hudReady && bossHudGroup)
            {
                hudIntro = Mathf.MoveTowards(hudIntro, hudLeaving ? 0f : 1f, Time.unscaledDeltaTime * 2.5f);
                bossHudGroup.alpha = Mathf.SmoothStep(0f, 1f, hudIntro);
                bossHud.transform.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, hudIntro);
                if (hudLeaving && hudIntro <= .001f && bossHud) Destroy(bossHud);
            }
            if (sprite && !isHologram && !ghosting && sprite.color.a < .999f)
            {
                var visibleColor = sprite.color; visibleColor.a = 1f; sprite.color = visibleColor;
            }
            if (!auraRenderer) return;
            var health = brain && brain.RuntimeMaxHealth > 0f ? brain.CurrentHealth / brain.RuntimeMaxHealth : 1f;
            var pulse = (Mathf.Sin(Time.time * (IsImmune ? 7f : 2.6f)) + 1f) * .5f;
            var danger = 1f - Mathf.Clamp01(health);
            var color = isHologram ? new Color(.22f, .82f, 1f) : IsImmune ? new Color(1f, .22f, .12f) : Color.Lerp(new Color(.34f, .9f, .36f), new Color(1f, .22f, .12f), danger * .75f);
            var visibility = sprite ? sprite.color.a : 1f;
            auraRenderer.color = SoftGlowSprite.Tint(color, (.2f + pulse * .12f) * visibility, 1.4f);
            auraRenderer.transform.localScale = Vector3.one * (2.4f + pulse * .22f + danger * .25f);
            if (spriteGlow && sprite)
            {
                spriteGlow.sprite = sprite.sprite;
                spriteGlow.flipX = sprite.flipX;
                var glowAlpha = (isHologram ? .25f : IsImmune ? .34f : .22f + danger * .08f) * visibility;
                spriteGlow.color = SoftGlowSprite.Tint(isHologram ? new Color(.25f, .85f, 1f) : IsImmune ? new Color(1f, .2f, .1f) : new Color(.5f, 1f, .26f), glowAlpha, 1.7f);
            }
        }

        IEnumerator SummonHologram()
        {
            if (isHologram || cloneSpawned || !brain || !brain.data) yield break;
            cloneSpawned = true;
            if (bossNameText) bossNameText.text = "KHAROS · FASE FINAL";
            if (bossPhaseText) bossPhaseText.text = "FASE III · EL ECO SE MANIFIESTA";
            var targetTransform = NearestPlayer();
            var towardPlayer = targetTransform ? ((Vector2)targetTransform.transform.position - (Vector2)transform.position).normalized : Vector2.right;
            var side = Random.value < .5f ? -1f : 1f;
            var spawnPosition = (Vector2)transform.position + (Vector2)(Quaternion.Euler(0f, 0f, side * 75f) * towardPlayer) * 4.3f;
            if (room) spawnPosition = room.ClampInside(spawnPosition, 2.1f);
            var markerObject = CreateRing(spawnPosition, 1.15f, new Color(.23f, .83f, 1f, .95f), 40, .075f);
            var marker = markerObject ? markerObject.GetComponent<EnemySpawnMarker>() : null;
            if (marker) marker.ConfigureTelegraph(1.8f);
            PlayBossSound(SfxCue.EnemySpecial, .8f);
            Burst(spawnPosition, EnemyAttackStyle.ArcaneBurst, new Color(.22f, .82f, 1f));
            SpawnEpicParticles(spawnPosition, new Color(.28f, .87f, 1f), 78, 2.8f, 1.1f, KharosParticleStyle.Teleport);
            ShakeCamera(.14f);
            yield return new WaitForSeconds(.72f);
            if (marker) marker.PlaySpawn();
            var cloneObject = new GameObject("Kharos_Hologram");
            cloneObject.transform.position = spawnPosition;
            cloneObject.AddComponent<SpriteRenderer>();
            var cloneBrain = cloneObject.AddComponent<EnemyBrain>();
            cloneBrain.Configure(brain.data, room, 1f, .55f, 1.15f, .68f, 1);
            cloneBrain.ConfigureKharosClone(brain.RuntimeMaxHealth * .58f, brain);
            cloneObject.transform.localScale *= .78f;
            cloneObject.AddComponent<EnemyWalkVisual>();
            var cloneAi = cloneObject.AddComponent<KharosBossAI>();
            cloneAi.ConfigureAsHologram(this);
            hologramClone = cloneAi;
            SpawnEpicParticles(spawnPosition, new Color(.28f, .87f, 1f), 92, 3.4f, 1.25f, KharosParticleStyle.Arcane);
            Burst(spawnPosition, EnemyAttackStyle.ArcaneBurst, new Color(.22f, .82f, 1f));
            PlayBossSound(SfxCue.EnemySpecial, .72f);
            ShakeCamera(.19f);
        }

        IEnumerator BuildBossHud()
        {
            bossHud = new GameObject("KharosBossHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = bossHud.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 4600;
            var scaler = bossHud.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(960f, 540f);
            bossHudGroup = bossHud.GetComponent<CanvasGroup>(); bossHudGroup.blocksRaycasts = false; bossHudGroup.interactable = false;
            bossHudGroup.alpha = 0f;
            bossHud.AddComponent<KharosBossHudFade>();
            // Floating boss HUD: keep the name and health readable without a solid panel or portrait.
            HudImage("TopFiligree", bossHud.transform, new Vector2(0f, -2f), new Vector2(520f, 2f), new Color(.64f, .57f, .29f, .82f));
            HudImage("BottomFiligree", bossHud.transform, new Vector2(0f, -77f), new Vector2(420f, 1.5f), new Color(.3f, .57f, .32f, .68f));
            yield return null;
            var panel = HudText("BossName", bossHud.transform, new Vector2(0f, -7f), new Vector2(570f, 26f), "KHAROS", 23, TextAnchor.MiddleCenter, new Color(.97f, .9f, .68f));
            bossNameText = panel.GetComponent<Text>();
            panel = HudText("BossPhase", bossHud.transform, new Vector2(0f, -29f), new Vector2(570f, 16f), "FASE 1 - GUARDIAN DE LAS RAICES", 12, TextAnchor.MiddleCenter, new Color(.57f, .78f, .54f));
            bossPhaseText = panel.GetComponent<Text>();
            yield return null;
            var barBackgroundObject = HudImage("BossHealthBackground", bossHud.transform, new Vector2(0f, -52f), new Vector2(520f, 16f), new Color(.025f, .035f, .027f, .72f));
            var fillObject = HudImage("BossHealthFill", barBackgroundObject.transform, new Vector2(3f, -3f), new Vector2(514f, 10f), new Color(.56f, .11f, .12f));
            bossHealthFillRect = fillObject.GetComponent<RectTransform>(); bossHealthFillRect.anchorMin = bossHealthFillRect.anchorMax = new Vector2(0f, 1f); bossHealthFillRect.pivot = new Vector2(0f, 1f);
            bossHealthFill = fillObject.GetComponent<Image>(); bossHealthFill.type = Image.Type.Simple;
            for (var i = 1; i < 11; i++)
            {
                HudImage("HealthSegment", barBackgroundObject.transform, new Vector2(-257f + i * (514f / 11f), -2f), new Vector2(2f, 13f), new Color(.02f, .03f, .025f, .45f));
                if (i % 3 == 0) yield return null;
            }
            panel = HudText("BossHealthValue", bossHud.transform, new Vector2(0f, -50f), new Vector2(180f, 19f), "", 13, TextAnchor.MiddleCenter, new Color(.98f, .94f, .8f));
            bossHealthText = panel.GetComponent<Text>();
            if (brain) bossHealthText.text = Mathf.CeilToInt(brain.CurrentHealth) + " / " + Mathf.CeilToInt(brain.RuntimeMaxHealth);
            hudReady = true;
        }

        IEnumerator PlayBossIntro()
        {
            introStarted = true;
            cinematicPhase = 1; cinematicProgress = 0f; IsImmune = true;
            LockEncounterPlayers(true);
            cinematicOverlay = KharosBossCinematicOverlay.Create();
            cinematicOverlay.SetSequence(1, 0f, "KHAROS", "EL GUARDIAN DE LAS RAICES", sprite ? sprite.sprite : null);
            yield return KharosCinematicHudFader.FadeOut(.42f);
            var cam = Camera.main;
            var follow = cam ? cam.GetComponent<CameraFollow>() : null;
            var player = NearestPlayer();
            var originalTarget = follow ? follow.target : null;
            var originalSize = cam && cam.orthographic ? cam.orthographicSize : 5f;
            var originalRotation = cam ? cam.transform.rotation : Quaternion.identity;
            var followWasEnabled = follow && follow.enabled;
            var startPosition = cam ? cam.transform.position : transform.position + Vector3.back * 10f;
            var destination = transform.position; destination.z = startPosition.z;
            if (follow) { follow.enabled = false; follow.Shake(.2f); }
            if (cam && cam.orthographic) cam.orthographicSize = originalSize * 1.12f;
            PlayBossSound(SfxCue.EnemySpecial, .8f);

            const float duration = 5.4f;
            const float travelEnd = .38f;
            const float holdEnd = .69f;
            var revealTriggered = false;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                cinematicProgress = Mathf.Clamp01(elapsed / duration);
                if (cinematicOverlay) cinematicOverlay.SetSequence(1, cinematicProgress, "KHAROS", "EL GUARDIAN DE LAS RAICES", sprite ? sprite.sprite : null);
                var position = destination;
                if (cinematicProgress < travelEnd)
                {
                    var t = Mathf.SmoothStep(0f, 1f, cinematicProgress / travelEnd);
                    var side = Vector3.Cross((destination - startPosition).normalized, Vector3.forward).normalized;
                    position = Vector3.Lerp(startPosition, destination, t) + side * Mathf.Sin(t * Mathf.PI) * 1.25f;
                }
                else if (cinematicProgress > holdEnd)
                {
                    var t = Mathf.SmoothStep(0f, 1f, (cinematicProgress - holdEnd) / (1f - holdEnd));
                    var playerPosition = player ? player.transform.position : startPosition;
                    playerPosition.z = startPosition.z;
                    position = Vector3.Lerp(destination, playerPosition, t);
                }
                else position += new Vector3(Mathf.Sin(elapsed * .75f) * .1f, Mathf.Sin(elapsed * .52f) * .08f, 0f);
                if (cam)
                {
                    var shakeAmount = cinematicProgress > travelEnd && cinematicProgress < holdEnd ? .075f : .026f;
                    if (cinematicProgress > .8f) shakeAmount *= 1f - Mathf.InverseLerp(.8f, 1f, cinematicProgress);
                    position += (Vector3)(Random.insideUnitCircle * shakeAmount);
                    cam.transform.position = position; cinematicCameraPosition = position;
                }
                if (cam && cam.orthographic)
                {
                    var closeSize = Mathf.Max(3.4f, originalSize * .72f);
                    var zoom = cinematicProgress < travelEnd
                        ? Mathf.Lerp(originalSize * 1.12f, closeSize, Mathf.SmoothStep(0f, 1f, cinematicProgress / travelEnd))
                        : cinematicProgress < holdEnd ? closeSize
                        : Mathf.Lerp(closeSize, originalSize, Mathf.SmoothStep(0f, 1f, (cinematicProgress - holdEnd) / (1f - holdEnd)));
                    cam.orthographicSize = zoom;
                    var distortion = cinematicProgress < travelEnd ? Mathf.Sin(elapsed * 3f) * .5f : Mathf.Sin(elapsed * 2.3f) * .15f;
                    cam.transform.rotation = Quaternion.Euler(0f, 0f, distortion);
                }
                if (!revealTriggered && cinematicProgress >= travelEnd)
                {
                    revealTriggered = true;
                    SpawnEpicParticles(transform.position, new Color(.4f, .95f, .57f), 100, 3.5f, 1.7f, KharosParticleStyle.Arcane);
                    var ring = CreateRing(transform.position, 3.1f, new Color(.42f, .94f, .58f, .85f), 56, .13f);
                    if (ring) StartCoroutine(FadeOutVisual(ring, 1.8f));
                    PlayBossSound(SfxCue.EnemySpecial, .95f);
                    if (follow) follow.Shake(.34f);
                }
                yield return null;
            }
            if (cam) cam.transform.rotation = originalRotation;
            if (cam && cam.orthographic) cam.orthographicSize = originalSize;
            if (cam && player) cam.transform.position = player.transform.position + (follow ? follow.offset : new Vector3(0f, 0f, startPosition.z));
            if (follow) { follow.enabled = followWasEnabled; follow.SetCinematicZoom(false); follow.SetTarget(player ? player.transform : originalTarget, false); }
            LockEncounterPlayers(false);
            IsImmune = false;
            if (cinematicOverlay) cinematicOverlay.FadeAndDestroy(.32f);
            cinematicPhase = 0; cinematicProgress = 0f; introFinished = true;
            cinematicCameraPosition = cam ? cam.transform.position : Vector3.zero;
            yield return KharosCinematicHudFader.FadeIn(.45f);
            StartCoroutine(BuildBossHud());
            StartBossMusic();
        }

        IEnumerator PlayBossOutro()
        {
            outroStarted = true;
            cinematicPhase = 2; cinematicProgress = 0f;
            cinematicOverlay = KharosBossCinematicOverlay.Create();
            cinematicOverlay.SetSequence(2, 0f, "KHAROS DERROTADO", "EL BOSQUE VUELVE A RESPIRAR", sprite ? sprite.sprite : null);
            yield return KharosCinematicHudFader.FadeOut(.4f);
            var cam = Camera.main;
            var follow = cam ? cam.GetComponent<CameraFollow>() : null;
            var player = NearestPlayer();
            var originalTarget = follow ? follow.target : null;
            var originalSize = cam && cam.orthographic ? cam.orthographicSize : 5f;
            var originalRotation = cam ? cam.transform.rotation : Quaternion.identity;
            var followWasEnabled = follow && follow.enabled;
            var startPosition = cam ? cam.transform.position : transform.position + Vector3.back * 10f;
            var focusPosition = transform.position; focusPosition.z = startPosition.z;
            LockEncounterPlayers(true);
            if (follow) { follow.enabled = false; follow.Shake(.55f); }
            if (cam) { cam.transform.position = focusPosition; cinematicCameraPosition = focusPosition; }
            if (cam && cam.orthographic) cam.orthographicSize = originalSize * .82f;
            Burst(transform.position, EnemyAttackStyle.ArcaneBurst, new Color(.48f, 1f, .58f));
            SpawnEpicParticles(transform.position, new Color(.6f, 1f, .64f), 190, 5.6f, 2.2f, KharosParticleStyle.Bloom);
            var collapse = CreateRing(transform.position, 5.6f, new Color(.64f, 1f, .7f, .85f), 64, .15f);
            if (collapse) StartCoroutine(FadeOutVisual(collapse, 2.2f));
            PlayBossSound(SfxCue.EnemyDeath, 1f);
            const float duration = 3.8f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                cinematicProgress = Mathf.Clamp01(elapsed / duration);
                if (cinematicOverlay) cinematicOverlay.SetSequence(2, cinematicProgress, "KHAROS DERROTADO", "EL BOSQUE VUELVE A RESPIRAR", sprite ? sprite.sprite : null);
                if (follow && elapsed < .8f) follow.Shake(Mathf.Lerp(.45f, .02f, elapsed / .8f));
                if (cam)
                {
                    var shake = Random.insideUnitCircle * Mathf.Lerp(.2f, .025f, cinematicProgress);
                    cam.transform.position = focusPosition + new Vector3(Mathf.Sin(elapsed * .7f) * .28f, Mathf.Cos(elapsed * .54f) * .16f, 0f) + (Vector3)shake;
                    cinematicCameraPosition = cam.transform.position;
                }
                if (cam && cam.orthographic)
                {
                    var overshoot = 1f + Mathf.Sin(cinematicProgress * Mathf.PI * 2f) * .035f;
                    cam.orthographicSize = Mathf.Lerp(originalSize * .82f, originalSize * 1.08f, Mathf.SmoothStep(0f, 1f, cinematicProgress)) * overshoot;
                    cam.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 4.2f) * Mathf.Lerp(.9f, 0f, cinematicProgress));
                }
                yield return null;
            }
            if (cam) cam.transform.rotation = originalRotation;
            if (cam && cam.orthographic) cam.orthographicSize = originalSize;
            if (cam && player) cam.transform.position = player.transform.position + (follow ? follow.offset : new Vector3(0f, 0f, startPosition.z));
            if (follow) { follow.enabled = followWasEnabled; follow.SetCinematicZoom(false); follow.SetTarget(player ? player.transform : originalTarget, false); }
            LockEncounterPlayers(false);
            if (cinematicOverlay) cinematicOverlay.FadeAndDestroy(.5f);
            cinematicPhase = 0; cinematicProgress = 0f;
            yield return KharosCinematicHudFader.FadeIn(.45f);
            if (brain) brain.CompleteBossDefeatCinematic();
        }

        void LockEncounterPlayers(bool locked)
        {
            if (locked)
            {
                cinematicLockedPlayers.Clear();
                foreach (var player in FindObjectsByType<PlayerController>())
                    if (player) { player.SetMovementLocked(true); cinematicLockedPlayers.Add(player); }
            }
            else
            {
                foreach (var player in cinematicLockedPlayers) if (player) player.SetMovementLocked(false);
                cinematicLockedPlayers.Clear();
            }
        }

        static GameObject HudImage(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var element = new GameObject(name, typeof(RectTransform), typeof(Image)); element.transform.SetParent(parent, false);
            var rect = element.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f); rect.pivot = new Vector2(.5f, 1f); rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = element.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            return element;
        }

        static GameObject HudText(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize, TextAnchor alignment, Color color)
        {
            var element = new GameObject(name, typeof(RectTransform), typeof(Text)); element.transform.SetParent(parent, false);
            var rect = element.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f); rect.pivot = new Vector2(.5f, 1f); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = element.GetComponent<Text>(); text.text = value; text.font = BossFont(); text.fontSize = fontSize; text.alignment = alignment; text.color = color; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            return element;
        }

        static Font BossFont()
        {
            if (cachedBossFont) return cachedBossFont;
            var hud = FindAnyObjectByType<PixelHUDCanvas>();
            if (hud && hud.pixelFont) return cachedBossFont = hud.pixelFont;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        void BuildAura()
        {
            if (!sprite) return;
            var spriteGlowObject = new GameObject("Kharos_SpriteRimGlow");
            spriteGlowObject.transform.SetParent(transform, false);
            spriteGlowObject.transform.localScale = Vector3.one * 1.18f;
            spriteGlow = spriteGlowObject.AddComponent<SpriteRenderer>();
            spriteGlow.sprite = sprite.sprite;
            spriteGlow.sharedMaterial = SoftGlowSprite.Material;
            spriteGlow.sortingLayerID = sprite.sortingLayerID;
            spriteGlow.sortingOrder = sprite.sortingOrder - 1;
            spriteGlow.color = SoftGlowSprite.Tint(new Color(.5f, 1f, .26f), .24f, 1.7f);
            var glow = new GameObject("Kharos_SoftShaderAura");
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = Vector3.zero;
            glow.transform.localScale = Vector3.one * 2.4f;
            auraRenderer = glow.AddComponent<SpriteRenderer>();
            auraRenderer.sprite = SoftGlowSprite.Sprite;
            auraRenderer.sharedMaterial = SoftGlowSprite.Material;
            auraRenderer.sortingLayerID = sprite.sortingLayerID;
            auraRenderer.sortingOrder = sprite.sortingOrder - 2;
            auraRenderer.color = SoftGlowSprite.Tint(new Color(.34f, .9f, .36f), .25f, 1.4f);
        }

        public void NotifyQteSuccess(Vector2 position)
        {
            Stun(1f);
            GameSfx.Play(SfxCue.Stun, position, .55f);
        }

        static void ShakeCamera(float trauma)
        {
            var camera = Object.FindAnyObjectByType<CameraFollow>();
            if (camera) camera.Shake(trauma);
        }

        Color AttackColor(Color original) => isHologram ? new Color(.2f, .78f, 1f, original.a) : original;

        void Burst(Vector2 position, EnemyAttackStyle style, Color color)
        {
            color = AttackColor(color);
            var vfx = CombatVfxPool.Instance;
            if (vfx) vfx.SpawnEnemyAbilityBurst(position, style, color);
        }

        void SpawnEpicParticles(Vector2 position, Color tint, int count = 52, float speed = 3f, float lifetime = .9f, KharosParticleStyle style = KharosParticleStyle.Arcane)
        {
            tint = AttackColor(tint);
            var root = new GameObject("Kharos_EpicParticles"); root.transform.position = position;
            var system = root.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main; main.playOnAwake = false; main.duration = .22f; main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = Mathf.Max(count, 16);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .6f, lifetime);
            var speedScale = style == KharosParticleStyle.Spore ? .32f : style == KharosParticleStyle.Thorn || style == KharosParticleStyle.Laser ? 1.25f : 1f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * speedScale * .45f, speed * speedScale);
            main.startSize = style == KharosParticleStyle.Spore ? new ParticleSystem.MinMaxCurve(.075f, .19f)
                : style == KharosParticleStyle.Laser || style == KharosParticleStyle.Thorn ? new ParticleSystem.MinMaxCurve(.025f, .075f)
                : new ParticleSystem.MinMaxCurve(.045f, .13f);
            main.startColor = new Color(tint.r, tint.g, tint.b, 1f);
            main.gravityModifier = style == KharosParticleStyle.Spore ? .08f : style == KharosParticleStyle.Dash ? -.28f : -.12f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = system.emission; emission.rateOverTime = 0f; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = system.shape; shape.enabled = true; shape.arc = 360f;
            shape.shapeType = style == KharosParticleStyle.Spore || style == KharosParticleStyle.Vine ? ParticleSystemShapeType.Cone
                : style == KharosParticleStyle.Shield ? ParticleSystemShapeType.Donut : ParticleSystemShapeType.Circle;
            shape.radius = style == KharosParticleStyle.Shield ? .72f : style == KharosParticleStyle.Spore ? .1f : style == KharosParticleStyle.Vine ? .14f : .34f;
            shape.radiusThickness = style == KharosParticleStyle.Shield ? .04f : .25f;
            if (style == KharosParticleStyle.Spore || style == KharosParticleStyle.Vine)
            {
                shape.angle = style == KharosParticleStyle.Spore ? 54f : 32f;
                shape.rotation = style == KharosParticleStyle.Spore ? new Vector3(-90f, 0f, 0f) : Vector3.zero;
            }
            var color = system.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(tint, 0f), new GradientColorKey(Color.white, .22f), new GradientColorKey(tint, 1f) },
                new[] { new GradientAlphaKey(.95f, 0f), new GradientAlphaKey(.9f, .25f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var size = system.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, style == KharosParticleStyle.Spore
                ? new AnimationCurve(new Keyframe(0f, .1f), new Keyframe(.25f, 1.3f), new Keyframe(1f, 0f))
                : style == KharosParticleStyle.Laser || style == KharosParticleStyle.Thorn
                    ? new AnimationCurve(new Keyframe(0f, .1f), new Keyframe(.1f, 1f), new Keyframe(1f, .02f))
                    : new AnimationCurve(new Keyframe(0f, .15f), new Keyframe(.18f, 1.15f), new Keyframe(.72f, .72f), new Keyframe(1f, 0f)));
            var rotation = system.rotationOverLifetime; rotation.enabled = style == KharosParticleStyle.Spore || style == KharosParticleStyle.Bloom || style == KharosParticleStyle.Teleport;
            if (rotation.enabled) rotation.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            var noise = system.noise; noise.enabled = style != KharosParticleStyle.Laser; noise.strength = style == KharosParticleStyle.Spore ? .9f : .3f; noise.frequency = style == KharosParticleStyle.Spore ? 1.1f : .7f; noise.scrollSpeed = .35f;
            var useTrails = style == KharosParticleStyle.Laser || style == KharosParticleStyle.Teleport || style == KharosParticleStyle.Thorn || style == KharosParticleStyle.Dash || style == KharosParticleStyle.Vine;
            var trails = system.trails; trails.enabled = useTrails; trails.lifetime = style == KharosParticleStyle.Teleport ? .3f : .2f; trails.ratio = style == KharosParticleStyle.Laser ? .85f : .55f; trails.widthOverTrail = new ParticleSystem.MinMaxCurve(.22f);
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            var stretched = style == KharosParticleStyle.Laser || style == KharosParticleStyle.Thorn || style == KharosParticleStyle.Dash || style == KharosParticleStyle.Vine;
            renderer.renderMode = stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = stretched ? 2.5f : 1f; renderer.velocityScale = stretched ? .24f : 0f;
            renderer.sharedMaterial = SoftGlowSprite.Material; renderer.trailMaterial = SoftGlowSprite.Material; renderer.sortingOrder = 32;
            system.Play(); Destroy(root, lifetime + .45f);
        }

        IEnumerator FadeOutVisual(GameObject effect, float duration)
        {
            if (!effect) yield break;
            var spawnMarker = effect.GetComponent<EnemySpawnMarker>();
            if (spawnMarker)
            {
                spawnMarker.PlaySpawn();
                yield return new WaitForSeconds(.32f);
                yield break;
            }
            var telegraph = effect.GetComponent<KharosGroundTelegraph>();
            if (telegraph) telegraph.enabled = false;
            var sprites = effect.GetComponentsInChildren<SpriteRenderer>(true);
            var lines = effect.GetComponentsInChildren<LineRenderer>(true);
            var spriteColors = new Color[sprites.Length];
            var lineStartColors = new Color[lines.Length];
            var lineEndColors = new Color[lines.Length];
            for (var i = 0; i < sprites.Length; i++) if (sprites[i]) spriteColors[i] = sprites[i].color;
            for (var i = 0; i < lines.Length; i++) if (lines[i]) { lineStartColors[i] = lines[i].startColor; lineEndColors[i] = lines[i].endColor; }
            var baseScale = effect.transform.localScale;
            for (var elapsed = 0f; elapsed < duration && effect; elapsed += Time.deltaTime)
            {
                var fade = 1f - Mathf.Clamp01(elapsed / duration);
                effect.transform.localScale = baseScale * Mathf.Lerp(.88f, 1f, fade);
                for (var i = 0; i < sprites.Length; i++)
                    if (sprites[i]) { var c = spriteColors[i]; c.a *= fade; sprites[i].color = c; }
                for (var i = 0; i < lines.Length; i++)
                    if (lines[i]) { var a = lineStartColors[i]; var b = lineEndColors[i]; a.a *= fade; b.a *= fade; lines[i].startColor = a; lines[i].endColor = b; }
                yield return null;
            }
            if (effect) Destroy(effect);
        }

        void PlayBossSound(SfxCue cue, float volume = .6f)
        {
            GameSfx.Play(cue, transform.position, volume);
        }

        void BeginRootQte(PlayerController player)
        {
            if (!player || player.GetComponent<KharosRootQte>()) return;
            player.gameObject.AddComponent<KharosRootQte>().Begin(this);
        }

        void StartBossMusic()
        {
            bossMusicStarted = true;
            if (!brain || !brain.data || !brain.data.bossMusic) return;
            bossMusicSource = gameObject.AddComponent<AudioSource>();
            bossMusicSource.clip = brain.data.bossMusic;
            bossMusicSource.loop = true;
            bossMusicSource.playOnAwake = false;
            bossMusicSource.spatialBlend = 0f;
            bossMusicSource.volume = 0f;
            bossMusicSource.ignoreListenerPause = true;
            bossMusicSource.Play();
            if (bossMusicPaused) bossMusicSource.Pause();
            StartCoroutine(FadeBossMusicIn());
        }

        IEnumerator FadeBossMusicIn()
        {
            const float targetVolume = .22f;
            for (var elapsed = 0f; elapsed < 2.5f && bossMusicSource; elapsed += Time.unscaledDeltaTime)
            {
                bossMusicSource.volume = Mathf.Lerp(0f, targetVolume, Mathf.SmoothStep(0f, 1f, elapsed / 2.5f));
                yield return null;
            }
            if (bossMusicSource) bossMusicSource.volume = targetVolume;
        }

        IEnumerator FadeBossMusicOut()
        {
            if (!bossMusicSource) yield break;
            var startVolume = bossMusicSource.volume;
            for (var elapsed = 0f; elapsed < 1.6f && bossMusicSource; elapsed += Time.unscaledDeltaTime)
            {
                bossMusicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / 1.6f);
                yield return null;
            }
            if (bossMusicSource) { bossMusicSource.Stop(); Destroy(bossMusicSource); bossMusicSource = null; }
        }

        IEnumerator RootCross()
        {
            busy = true; attackMovement = Vector2.zero;
            var center = (Vector2)transform.position;
            var angle = Random.Range(0f, 90f);
            var extent = room ? Mathf.Min(room.WorldBounds.extents.x, room.WorldBounds.extents.y) * .95f : 10f;
            var cross = new GameObject("Kharos_RotatingRootCross"); cross.transform.position = center;
            activeCross = cross;
            cross.transform.localScale = Vector3.one * .04f;
            var redBeam = CreateLocalCrossBeam(cross.transform, extent, AttackColor(new Color(1f, .24f, .32f, 1f)));
            var greenBeam = CreateLocalCrossBeam(cross.transform, extent, AttackColor(new Color(.22f, 1f, .58f, 1f)));
            redBeam.transform.localRotation = Quaternion.identity;
            greenBeam.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreateCrossFocus(cross.transform, AttackColor(new Color(.74f, 1f, .48f, 1f)));
            PlayBossSound(SfxCue.DarkMagicCast, .56f);
            Burst(center, EnemyAttackStyle.ArcaneBurst, new Color(.8f, .72f, .18f));
            SpawnEpicParticles(center, new Color(.76f, 1f, .22f), 48, 3.2f, 1.1f, KharosParticleStyle.Laser);
            var elapsed = 0f;
            var healthRatio = brain.CurrentHealth / Mathf.Max(1f, brain.RuntimeMaxHealth);
            var enraged = healthRatio < .5f;
            var spinSpeed = enraged ? 48f : 36f;
            var duration = enraged ? 6.4f : 7.2f;
            const float damage = 2f;
            var nextHit = new Dictionary<PlayerController, float>();
            var nextSparks = .45f;
            var qteTriggered = false;
            while (elapsed < duration && cross && !deathPlayed)
            {
                elapsed += Time.deltaTime;
                angle += spinSpeed * Time.deltaTime;
                cross.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                var reveal = Mathf.SmoothStep(.04f, 1f, Mathf.Clamp01(elapsed / .62f));
                cross.transform.localScale = Vector3.one * reveal;
                if (elapsed > .62f)
                {
                    var forward = (Vector2)cross.transform.right;
                    var side = Vector2.Perpendicular(forward);
                    foreach (var player in Players())
                    {
                        if (!player) continue;
                        var offset = (Vector2)player.transform.position - center;
                        var touching = DistanceToSegment(offset, -forward * extent, forward * extent) < .62f
                            || DistanceToSegment(offset, -side * extent, side * extent) < .62f;
                        if (!touching)
                        {
                            nextHit.Remove(player);
                            continue;
                        }
                        if (nextHit.TryGetValue(player, out var readyAt) && Time.time < readyAt) continue;
                        var confirmedHit = player.TryTakeContinuousDamage(damage, center);
                        if (!confirmedHit) continue;
                        nextHit[player] = Time.time + .2f;
                        if (enraged && !qteTriggered) { BeginRootQte(player); qteTriggered = true; }
                        ShakeCamera(.045f);
                        PlayBossSound(SfxCue.Wave, .26f);
                    }
                    if (elapsed >= nextSparks)
                    {
                        nextSparks = elapsed + .62f;
                        var lineDirection = Random.value < .5f ? forward : side;
                        var sparkPosition = center + lineDirection * Random.Range(-extent * .76f, extent * .76f);
                        SpawnEpicParticles(sparkPosition, Random.value < .5f ? new Color(1f, .36f, .16f) : new Color(.55f, 1f, .24f), 7, .65f, .32f, KharosParticleStyle.Laser);
                    }
                }
                yield return null;
            }
            if (deathPlayed || !cross)
            {
                if (activeCross == cross) activeCross = null;
                busy = false;
                yield break;
            }
            ShakeCamera(.14f); PlayBossSound(SfxCue.Wave, .62f);
            SpawnEpicParticles(center, new Color(1f, .52f, .17f), 58, 4f, 1.2f, KharosParticleStyle.Laser);
            Burst(center, EnemyAttackStyle.HeavySmash, new Color(1f, .48f, .17f));
            if (cross) yield return FadeOutVisual(cross, .3f);
            if (activeCross == cross) activeCross = null;
            yield return new WaitForSeconds(.2f); busy = false;
        }

        static GameObject CreateLocalCrossBeam(Transform parent, float extent, Color color)
        {
            var beamRoot = new GameObject("CrossBeam"); beamRoot.transform.SetParent(parent, false);
            var halo = CreateBeamLine(beamRoot.transform, "BeamHalo", extent, color, .72f, .075f, 13);
            var bloom = CreateBeamLine(beamRoot.transform, "BeamBloom", extent, color, .3f, .2f, 14);
            var core = CreateBeamLine(beamRoot.transform, "BeamCore", extent, color, .075f, .94f, 15);
            var heart = CreateBeamLine(beamRoot.transform, "BeamHotCore", extent, Color.Lerp(color, Color.white, .72f), .025f, .95f, 16);
            var electricity = beamRoot.AddComponent<KharosElectricBeam>();
            electricity.Initialize(extent, color, lineMaterial, halo, bloom, core);
            return beamRoot;
        }

        static LineRenderer CreateBeamLine(Transform parent, string name, float extent, Color color, float width, float alpha, int sortingOrder)
        {
            var child = new GameObject(name); child.transform.SetParent(parent, false);
            var line = child.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.positionCount = 2;
            line.sharedMaterial = lineMaterial; line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = new Color(color.r, color.g, color.b, alpha);
            line.sortingOrder = sortingOrder; line.numCapVertices = 3;
            line.SetPosition(0, new Vector3(-extent, 0f, 0f)); line.SetPosition(1, new Vector3(extent, 0f, 0f));
            return line;
        }

        static void CreateCrossFocus(Transform parent, Color tint)
        {
            var focus = new GameObject("RootCross_ElectricalFocus");
            focus.transform.SetParent(parent, false);
            var aura = focus.AddComponent<SpriteRenderer>();
            aura.sprite = SoftGlowSprite.Sprite; aura.sharedMaterial = SoftGlowSprite.Material;
            aura.color = SoftGlowSprite.Tint(tint, .42f, 1.75f); aura.sortingOrder = 20;
            var sparkObject = new GameObject("FocusSpark"); sparkObject.transform.SetParent(focus.transform, false);
            sparkObject.transform.localScale = Vector3.one * .17f;
            var spark = sparkObject.AddComponent<SpriteRenderer>();
            spark.sprite = CombatFeedback.WhiteSprite; spark.color = Color.Lerp(tint, Color.white, .35f); spark.sortingOrder = 21;
            focus.AddComponent<KharosCrossFocus>().Initialize(aura.transform);
        }

        TrailRenderer CreateTrail(Transform parent, Color headColor, Color tailColor, float duration, float width, int sortingOrder)
        {
            headColor = AttackColor(headColor);
            tailColor = AttackColor(tailColor);
            MakeProjectileTrailMaterial();
            var trail = parent.GetComponent<TrailRenderer>();
            if (!trail) trail = parent.gameObject.AddComponent<TrailRenderer>();
            trail.Clear();
            trail.time = duration; trail.minVertexDistance = .025f; trail.autodestruct = false;
            trail.textureMode = LineTextureMode.Stretch;
            trail.sharedMaterial = projectileTrailMaterial; trail.alignment = LineAlignment.View;
            trail.widthMultiplier = width;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, .15f), new Keyframe(.28f, .12f), new Keyframe(1f, 0f));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.Lerp(headColor, Color.white, .42f), 0f), new GradientColorKey(tailColor, 1f) },
                new[] { new GradientAlphaKey(.86f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient; trail.sortingOrder = sortingOrder;
            return trail;
        }

        static Material projectileTrailMaterial;
        static void MakeProjectileTrailMaterial()
        {
            if (projectileTrailMaterial) return;
            var shader = Shader.Find("Sprites/Default");
            if (shader) projectileTrailMaterial = new Material(shader) { name = "Kharos Projectile Style Trail" };
        }

        IEnumerator ThornBloom(Vector2 aimedAt)
        {
            busy = true; attackMovement = Vector2.zero;
            var count = brain.CurrentHealth / Mathf.Max(1f, brain.RuntimeMaxHealth) < .5f ? 7 : 5;
            var positions = new Vector2[count]; var warnings = new GameObject[count];
            var player = NearestPlayer();
            var targetPoint = player ? (Vector2)player.transform.position : aimedAt;
            var playerBody = player ? player.GetComponent<Rigidbody2D>() : null;
            if (playerBody) targetPoint += playerBody.linearVelocity * .62f;
            var radius = brain.CurrentHealth / Mathf.Max(1f, brain.RuntimeMaxHealth) < .5f ? 1.75f : 2.15f;
            PlayBossSound(SfxCue.DarkMagicCast, .48f);
            SpawnEpicParticles(targetPoint, new Color(.96f, .48f, .14f), 58, 2.8f, .95f, KharosParticleStyle.Bloom);
            for (var i = 0; i < count; i++)
            {
                if (i == 0) positions[i] = targetPoint;
                else
                {
                    var angle = (360f * (i - 1) / (count - 1) + Random.Range(-9f, 9f)) * Mathf.Deg2Rad;
                    positions[i] = targetPoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
                if (room) positions[i] = room.ClampInside(positions[i], 1.25f);
                warnings[i] = CreateRing(positions[i], .85f, new Color(1f, .42f, .17f, .9f), 32, .06f);
            }
            yield return new WaitForSeconds(.5f);
            for (var i = 0; i < count; i++)
            {
                if (warnings[i]) StartCoroutine(FadeOutVisual(warnings[i], .2f));
                SpawnSpine(positions[i], Damage(1.2f, 6.5f));
            }
            ShakeCamera(.12f);
            yield return new WaitForSeconds(.38f); busy = false;
        }

        IEnumerator SporeBurst()
        {
            busy = true; attackMovement = Vector2.zero;
            var enraged = brain.CurrentHealth / Mathf.Max(1f, brain.RuntimeMaxHealth) < .5f;
            var waves = enraged ? 4 : 3;
            PlayBossSound(SfxCue.DarkMagicCast, .52f);
            SetTint(new Color(.72f, 1f, .34f, 1f));
            SpawnEpicParticles(transform.position, new Color(.55f, 1f, .2f), 64, 3.2f, .95f, KharosParticleStyle.Spore);
            yield return new WaitForSeconds(.35f);
            SetTint(baseTint);
            for (var wave = 0; wave < waves; wave++)
            {
                var count = wave % 2 == 0 ? 10 : 8;
                var offset = Time.time * 61f + wave * (360f / count) * .47f;
                for (var i = 0; i < count; i++)
                {
                    var direction = Quaternion.Euler(0f, 0f, offset + i * 360f / count) * Vector2.right;
                    LaunchThorn(transform.position, direction, Damage(.64f, 3.5f));
                }
                Burst(transform.position, EnemyAttackStyle.ToxicVolley, new Color(.53f, 1f, .21f));
                PlayBossSound(SfxCue.MagicCast, .22f);
                if (wave + 1 < waves) yield return new WaitForSeconds(.36f);
            }
            yield return new WaitForSeconds(.35f); busy = false;
        }

        IEnumerator ThornBarrage()
        {
            busy = true; attackMovement = Vector2.zero; IsImmune = true;
            SetTint(new Color(.72f, .16f, .14f, 1f));
            PlayBossSound(SfxCue.Shield, .72f);
            var shieldColor = AttackColor(new Color(1f, .22f, .16f, .85f));
            shieldVisual = CreateRing(transform.position, 2.15f, shieldColor, 48, .1f);
            if (shieldVisual) shieldVisual.transform.SetParent(transform, true);
            Burst(transform.position, EnemyAttackStyle.HeavySmash, new Color(1f, .2f, .13f));
            SpawnEpicParticles(transform.position, new Color(1f, .23f, .14f), 58, 2.7f, .9f, KharosParticleStyle.Shield);
            yield return new WaitForSeconds(.42f);

            const int salvos = 7;
            const int projectilesPerSalvo = 16;
            var rotation = Random.Range(0f, 20f);
            for (var salvo = 0; salvo < salvos; salvo++)
            {
                var offset = rotation + salvo * 10f;
                for (var shot = 0; shot < projectilesPerSalvo; shot++)
                {
                    var angle = (offset + shot * (360f / projectilesPerSalvo)) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    LaunchThorn(transform.position, direction, Damage(.72f, 4.5f), new Color(1f, .2f, .17f));
                }
                SpawnEpicParticles(transform.position, new Color(1f, .25f, .18f), 12, 1.5f, .38f, KharosParticleStyle.Thorn);
                PlayBossSound(SfxCue.MagicCast, .3f);
                yield return new WaitForSeconds(.27f);
            }
            yield return new WaitForSeconds(.35f);
            IsImmune = false;
            if (shieldVisual) yield return FadeOutVisual(shieldVisual, .38f);
            shieldVisual = null; SetTint(baseTint); busy = false;
        }

        static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            var delta = end - start;
            var t = delta.sqrMagnitude < .001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - start, delta) / delta.sqrMagnitude);
            return Vector2.Distance(point, start + delta * t);
        }

        IEnumerator DreadPulse()
        {
            busy = true;
            attackMovement = Vector2.zero;
            brain.SetCustomMovement(Vector2.zero, EnemyState.Attack);
            SetTint(new Color(.28f, 1f, .35f, 1f));
            PlayBossSound(SfxCue.DarkMagicCast, .62f);
            Burst(transform.position, EnemyAttackStyle.ArcaneBurst, new Color(.28f, 1f, .35f));
            SpawnEpicParticles(transform.position, new Color(.3f, 1f, .34f), 64, 2.7f, .85f, KharosParticleStyle.Pulse);
            yield return new WaitForSeconds(.68f);
            SetTint(baseTint);
            var center = transform.position;
            Burst(center, EnemyAttackStyle.ArcaneBurst, new Color(.3f, 1f, .38f));
            var ring = CreateRing(center, PulseRadius, new Color(.28f, 1f, .35f, .9f), 64, .075f);
            yield return new WaitForSeconds(.72f);
            if (ring) StartCoroutine(FadeOutVisual(ring, .22f));
            var pulse = CreateRing(center, PulseRadius, new Color(.45f, 1f, .38f, .9f), 64, .12f);
            ShakeCamera(.2f);
            PlayBossSound(SfxCue.Wave, .78f);
            DamagePlayersInRadius(center, PulseRadius, Damage(1.25f, 4f));
            SpawnEpicParticles(center, new Color(.42f, 1f, .42f), 72, 3.2f, .9f, KharosParticleStyle.Pulse);
            yield return new WaitForSeconds(.2f);
            if (pulse) yield return FadeOutVisual(pulse, .26f);
            attackMovement = Vector2.zero; busy = false;
        }

        IEnumerator ThornRush(Vector2 direction)
        {
            busy = true; attackMovement = Vector2.zero;
            var start = (Vector2)transform.position;
            direction = direction.sqrMagnitude > .001f ? direction.normalized : Vector2.right;
            var end = start + direction * 8f;
            if (room) end = room.ClampInside(end, 1.5f);
            var warning = CreateBeamTelegraph(start, end, new Color(1f, .42f, .18f, .85f), .07f);
            PlayBossSound(SfxCue.HeavySwing, .72f);
            Burst(start, EnemyAttackStyle.HeavySmash, new Color(1f, .42f, .18f));
            SpawnEpicParticles(start, new Color(1f, .42f, .16f), 44, 2.7f, .75f, KharosParticleStyle.Dash);
            for (var elapsed = 0f; elapsed < .36f; elapsed += Time.deltaTime)
            {
                var markedPlayer = NearestPlayer();
                if (markedPlayer)
                {
                    var aimed = ((Vector2)markedPlayer.transform.position - (Vector2)transform.position).normalized;
                    if (aimed.sqrMagnitude > .01f) direction = aimed;
                    end = start + direction * 8f;
                    if (room) end = room.ClampInside(end, 1.5f);
                    if (warning)
                        foreach (var line in warning.GetComponentsInChildren<LineRenderer>())
                            if (line && line.positionCount >= 2) { line.SetPosition(0, start); line.SetPosition(1, end); }
                }
                yield return null;
            }
            if (warning) StartCoroutine(FadeOutVisual(warning, .2f));
            SpawnEpicParticles(start, new Color(1f, .4f, .15f), 56, 3.4f, .75f, KharosParticleStyle.Dash);
            var oldMultiplier = brain.ExternalSpeedMultiplier;
            brain.ExternalSpeedMultiplier = oldMultiplier * 5.5f;
            var dashTrail = CreateTrail(transform, new Color(1f, .54f, .18f), new Color(.53f, .95f, .2f), .22f, .16f, 23);
            attackMovement = direction;
            var struckPlayers = new HashSet<PlayerController>();
            var previousPosition = (Vector2)transform.position;
            var dashEnd = Time.time + .62f;
            while (Time.time < dashEnd)
            {
                foreach (var player in Players())
                {
                    if (!player || struckPlayers.Contains(player)) continue;
                    var currentPosition = (Vector2)transform.position;
                    var distanceToSweep = DistanceToSegment(player.transform.position, previousPosition, currentPosition);
                    if (distanceToSweep <= 1.35f && player.TryTakeDamage(Damage(1.65f, 7f), currentPosition))
                    {
                        struckPlayers.Add(player);
                        Burst(player.transform.position, EnemyAttackStyle.HeavySmash, new Color(1f, .42f, .18f));
                        SpawnEpicParticles(player.transform.position, new Color(1f, .52f, .2f), 36, 2.5f, .7f, KharosParticleStyle.Dash);
                        ShakeCamera(.2f); PlayBossSound(SfxCue.HeavyImpact, .88f);
                    }
                }
                previousPosition = transform.position;
                yield return null;
            }
            attackMovement = Vector2.zero; brain.ExternalSpeedMultiplier = oldMultiplier;
            if (dashTrail) dashTrail.emitting = false;
            Burst(transform.position, EnemyAttackStyle.HeavySmash, new Color(.92f, .28f, .14f));
            if (struckPlayers.Count == 0) PlayBossSound(SfxCue.HeavyImpact, .42f);
            yield return new WaitForSeconds(.3f); busy = false;
        }

        IEnumerator ThornVolley(Vector2 facing)
        {
            busy = true; attackMovement = Vector2.zero;
            SetTint(new Color(.78f, 1f, .38f));
            PlayBossSound(SfxCue.DarkMagicCast, .52f);
            Burst(transform.position + (Vector3)(facing * .7f), EnemyAttackStyle.ToxicVolley, new Color(.56f, 1f, .24f));
            SpawnEpicParticles(transform.position, new Color(.58f, 1f, .2f), 48, 2.6f, .75f, KharosParticleStyle.Thorn);
            yield return new WaitForSeconds(.38f);
            SetTint(baseTint);
            var salvos = brain.CurrentHealth / Mathf.Max(1f, brain.RuntimeMaxHealth) < .5f ? 4 : 3;
            for (var salvo = 0; salvo < salvos; salvo++)
            {
                var player = NearestPlayer();
                var aim = player ? ((Vector2)player.transform.position - (Vector2)transform.position).normalized : facing;
                var spread = salvos > 3 ? 13f : 10f;
                LaunchThorn(transform.position, aim, Damage(.9f, 6f));
                LaunchThorn(transform.position, Quaternion.Euler(0f, 0f, spread) * aim, Damage(.9f, 6f));
                LaunchThorn(transform.position, Quaternion.Euler(0f, 0f, -spread) * aim, Damage(.9f, 6f));
                Burst(transform.position, EnemyAttackStyle.ToxicVolley, new Color(.62f, 1f, .28f));
                SpawnEpicParticles(transform.position, new Color(.52f, 1f, .2f), 28, 2.1f, .58f, KharosParticleStyle.Thorn);
                PlayBossSound(SfxCue.MagicCast, .3f);
                if (salvo + 1 < salvos) yield return new WaitForSeconds(.22f);
            }
            yield return new WaitForSeconds(.25f); busy = false;
        }

        IEnumerator RootRift(Vector2 around)
        {
            busy = true; attackMovement = Vector2.zero;
            var towardBoss = ((Vector2)transform.position - around).normalized;
            if (towardBoss.sqrMagnitude < .01f) towardBoss = Vector2.up;
            var side = Vector2.Perpendicular(towardBoss) * 1.35f;
            PlayBossSound(SfxCue.DarkMagicCast, .5f);
            var positions = new[] { around + towardBoss * .35f, around - side, around + side };
            for (var i = 0; i < positions.Length; i++)
            {
                if (room) positions[i] = room.ClampInside(positions[i], 1.2f);
                var warning = CreateRing(positions[i], .95f, new Color(1f, .42f, .19f, .94f), 32, .06f);
                Burst(positions[i], EnemyAttackStyle.ArcaneBurst, new Color(1f, .32f, .15f));
                SpawnEpicParticles(positions[i], new Color(1f, .38f, .15f), 24, 1.8f, .55f);
                yield return new WaitForSeconds(.42f);
                if (warning) StartCoroutine(FadeOutVisual(warning, .22f));
                SpawnSpine(positions[i], Damage(1.25f, 6.5f));
                SpawnEpicParticles(positions[i], new Color(1f, .4f, .16f), 30, 2.2f, .62f, KharosParticleStyle.Bloom);
                yield return new WaitForSeconds(.18f);
            }
            yield return new WaitForSeconds(.22f); busy = false;
        }

        void LaunchThorn(Vector2 origin, Vector2 direction, float damage)
        {
            LaunchThorn(origin, direction, damage, new Color(.63f, 1f, .27f));
        }

        void LaunchThorn(Vector2 origin, Vector2 direction, float damage, Color tint)
        {
            var thorn = new GameObject("Kharos_SeekingThorn"); thorn.transform.position = origin;
            thorn.transform.right = direction.sqrMagnitude > .001f ? (Vector3)direction.normalized : Vector3.right;
            tint = AttackColor(tint);
            var visual = thorn.AddComponent<SpriteRenderer>(); visual.sprite = CombatFeedback.WhiteSprite; visual.color = tint; visual.sortingOrder = 24;
            thorn.transform.localScale = new Vector3(.22f, .12f, 1f);
            CreateTrail(thorn.transform, tint, Color.Lerp(tint, Color.black, .55f), .28f, .2f, 22);
            var halo = new GameObject("SoftGlow"); halo.transform.SetParent(thorn.transform, false); halo.transform.localScale = Vector3.one * 4f;
            var haloRenderer = halo.AddComponent<SpriteRenderer>(); haloRenderer.sprite = SoftGlowSprite.Sprite; haloRenderer.sharedMaterial = SoftGlowSprite.Material;
            haloRenderer.color = SoftGlowSprite.Tint(tint, .22f, 1.5f); haloRenderer.sortingOrder = 23;
            var projectile = thorn.AddComponent<KharosThornProjectile>(); projectile.Initialize(direction, 8.5f, damage, tint);
        }

        IEnumerator GhostShift(Vector2 awayFromPlayer)
        {
            busy = true;
            ghosting = true;
            var oldPosition = transform.position;
            PlayBossSound(SfxCue.Stun, .35f);
            Burst(oldPosition, EnemyAttackStyle.ArcaneBurst, new Color(.28f, .8f, 1f));
            SpawnEpicParticles(oldPosition, new Color(.28f, .8f, 1f), 84, 3.8f, 1.1f, KharosParticleStyle.Teleport);
            EnemyVfx.Teleport(oldPosition);
            for (var elapsed = 0f; elapsed < .3f; elapsed += Time.deltaTime)
            {
                SetAlpha(1f - elapsed / .3f); yield return null;
            }
            SetAlpha(0f);
            if (bossCollider) bossCollider.enabled = false;
            var player = NearestPlayer();
            Vector2 destination;
            if (isHologram && cloneOwner && cloneOwner.brain && !cloneOwner.brain.IsDead)
            {
                var side = Random.value < .5f ? -1f : 1f;
                var towardPlayer = player ? ((Vector2)player.transform.position - (Vector2)cloneOwner.transform.position).normalized : Vector2.right;
                destination = (Vector2)cloneOwner.transform.position + Vector2.Perpendicular(towardPlayer) * side * Random.Range(4f, 4.8f) + Random.insideUnitCircle * .3f;
            }
            else
            {
                var away = player ? ((Vector2)transform.position - (Vector2)player.transform.position).normalized : -awayFromPlayer.normalized;
                if (away.sqrMagnitude < .01f) away = Vector2.left;
                var radius = Random.Range(4f, 5.2f);
                var direction = (Vector2)(Quaternion.Euler(0f, 0f, Random.Range(-28f, 28f)) * (Vector3)away);
                destination = player ? (Vector2)player.transform.position + direction * radius : (Vector2)transform.position + direction * radius;
            }
            if (room) destination = room.ClampInside(destination, 2f);
            transform.position = destination;
            var body = GetComponent<Rigidbody2D>(); if (body) { body.position = destination; body.linearVelocity = Vector2.zero; }
            if (!isHologram) ShiftHologramBeside(destination, player ? (Vector2)player.transform.position : destination + awayFromPlayer);
            EnemyVfx.Teleport(destination);
            Burst(destination, EnemyAttackStyle.ArcaneBurst, new Color(.28f, .8f, 1f));
            SpawnEpicParticles(destination, new Color(.28f, .8f, 1f), 100, 4.1f, 1.25f, KharosParticleStyle.Teleport);
            PlayBossSound(SfxCue.Stun, .32f);
            for (var elapsed = 0f; elapsed < .3f; elapsed += Time.deltaTime)
            {
                SetAlpha(elapsed / .3f); yield return null;
            }
            SetAlpha(isHologram ? HologramOpacity : 1f);
            if (bossCollider) bossCollider.enabled = true;
            ghosting = false;
            busy = false;
        }

        void ShiftHologramBeside(Vector2 ownerPosition, Vector2 playerPosition)
        {
            foreach (var hologram in FindObjectsByType<KharosBossAI>())
            {
                if (!hologram || !hologram.isHologram || hologram.cloneOwner != this || !hologram.brain || hologram.brain.IsDead) continue;
                var away = (ownerPosition - playerPosition).normalized;
                if (away.sqrMagnitude < .01f) away = Vector2.up;
                var side = Random.value < .5f ? -1f : 1f;
                var destination = ownerPosition + Vector2.Perpendicular(away) * side * 4.2f;
                if (room) destination = room.ClampInside(destination, 2f);
                var oldPosition = (Vector2)hologram.transform.position;
                EnemyVfx.Teleport(oldPosition);
                hologram.transform.position = destination;
                var body = hologram.GetComponent<Rigidbody2D>();
                if (body) { body.position = destination; body.linearVelocity = Vector2.zero; }
                EnemyVfx.Teleport(destination);
                hologram.SpawnEpicParticles(destination, new Color(.28f, .87f, 1f), 48, 3f, .85f, KharosParticleStyle.Teleport);
                hologram.Burst(destination, EnemyAttackStyle.ArcaneBurst, new Color(.22f, .82f, 1f));
                break;
            }
        }

        IEnumerator RootBunker()
        {
            busy = true; nextBunker = Time.time + 20f; IsImmune = true;
            SetTint(new Color(.64f, .17f, .12f, 1f));
            PlayBossSound(SfxCue.Shield, .7f);
            Burst(transform.position, EnemyAttackStyle.HeavySmash, new Color(.7f, .2f, .12f));
            SpawnEpicParticles(transform.position, new Color(.88f, .24f, .13f), 96, 3.6f, 1.2f, KharosParticleStyle.Shield);
            ShakeCamera(.12f);
            shieldVisual = CreateRing(transform.position, 2.3f, new Color(.83f, .25f, .14f, .8f), 48, .08f);
            if (shieldVisual) shieldVisual.transform.SetParent(transform, true);
            var started = Time.time;
            for (var spike = 0; spike < 3; spike++)
            {
                var player = NearestPlayer();
                if (player)
                {
                    var position = (Vector2)player.transform.position;
                    var warning = CreateRing(position, .8f, new Color(1f, .38f, .2f, .95f), 28, .055f);
                    SpawnEpicParticles(position, new Color(1f, .38f, .2f), 24, 1.7f, .58f);
                    yield return new WaitForSeconds(.6f);
                    if (warning) StartCoroutine(FadeOutVisual(warning, .22f));
                    SpawnSpine(position, 1.5f);
                }
                yield return new WaitForSeconds(.9f);
            }
            var remaining = 6f - (Time.time - started);
            if (remaining > 0f) yield return new WaitForSeconds(remaining);
            IsImmune = false;
            if (shieldVisual) yield return FadeOutVisual(shieldVisual, .45f);
            shieldVisual = null; SetTint(baseTint); busy = false;
        }

        IEnumerator ToxicLasso(Transform player)
        {
            if (!player) yield break;
            busy = true; brain.SetCustomMovement(Vector2.zero, EnemyState.Attack);
            var start = (Vector2)transform.position;
            var end = (Vector2)player.position;
            var delta = end - start;
            if (delta.sqrMagnitude < .01f) { busy = false; yield break; }
            var direction = delta.normalized;
            var vine = new GameObject("Kharos_ToxicLiana");
            PlayBossSound(SfxCue.DarkMagicCast, .48f);
            SpawnEpicParticles(start, new Color(.48f, 1f, .18f), 45, 2.2f, .8f, KharosParticleStyle.Vine);
            var line = vine.AddComponent<LineRenderer>();
            line.useWorldSpace = true; line.positionCount = 3; line.startWidth = .22f; line.endWidth = .09f;
            line.sharedMaterial = lineMaterial; line.startColor = AttackColor(new Color(.48f, .9f, .2f)); line.endColor = AttackColor(new Color(.13f, .42f, .12f));
            line.sortingOrder = 28; line.SetPosition(0, start);
            var haloObject = new GameObject("SoftGlow");
            haloObject.transform.SetParent(vine.transform, false);
            var halo = haloObject.AddComponent<LineRenderer>();
            halo.useWorldSpace = true; halo.positionCount = 3; halo.startWidth = .42f; halo.endWidth = .18f;
            halo.sharedMaterial = lineMaterial; halo.startColor = AttackColor(new Color(.35f, 1f, .16f, .24f)); halo.endColor = AttackColor(new Color(.25f, .8f, .12f, .04f));
            halo.sortingOrder = 27; halo.SetPosition(0, start);
            var vineTip = new GameObject("Kharos_LianaTip"); vineTip.transform.position = start;
            var vineTrail = CreateTrail(vineTip.transform, new Color(.64f, 1f, .3f), new Color(.14f, .54f, .12f), .36f, .36f, 29);
            var position = start;
            var duration = 1.15f;
            Burst(start, EnemyAttackStyle.ToxicVolley, new Color(.48f, .9f, .2f));
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                position += direction * 13f * Time.deltaTime;
                if (vineTip) vineTip.transform.position = position;
                var curve = (Vector2)Vector3.Lerp(start, position, .52f) + Vector2.Perpendicular(direction) * Mathf.Sin(elapsed * 22f) * .16f;
                line.SetPosition(1, curve); line.SetPosition(2, position);
                halo.SetPosition(1, curve); halo.SetPosition(2, position);
                if (player && Vector2.Distance(position, player.position) < .55f)
                {
                    var controller = player.GetComponent<PlayerController>();
                    if (controller)
                    {
                        BeginRootQte(controller);
                        PlayBossSound(SfxCue.DarkMagicImpact, .68f);
                        Burst(position, EnemyAttackStyle.ToxicVolley, new Color(.72f, 1f, .2f));
                        ShakeCamera(.1f);
                    }
                    break;
                }
                if (Vector2.Distance(position, start) > delta.magnitude + 1.5f) break;
                yield return null;
            }
            if (vine) StartCoroutine(FadeOutVisual(vine, .22f));
            if (vineTip) Destroy(vineTip, vineTrail ? .42f : .1f);
            yield return new WaitForSeconds(.25f); busy = false;
        }

        IEnumerator SporeRain(Vector2 around)
        {
            busy = true; brain.SetCustomMovement(Vector2.zero, EnemyState.Attack);
            var player = NearestPlayer();
            var targetPoint = player ? (Vector2)player.transform.position : around;
            var playerBody = player ? player.GetComponent<Rigidbody2D>() : null;
            if (playerBody) targetPoint += playerBody.linearVelocity * .95f;
            PlayBossSound(SfxCue.DarkMagicCast, .56f);
            Burst(transform.position + Vector3.up, EnemyAttackStyle.ToxicVolley, new Color(.32f, .9f, .2f));
            SpawnEpicParticles(transform.position + Vector3.up, new Color(.43f, 1f, .2f), 72, 2.9f, 1f, KharosParticleStyle.Spore);
            var positions = new Vector2[4]; var warnings = new GameObject[4];
            var angleOffset = Random.Range(0f, 90f);
            for (var i = 0; i < positions.Length; i++)
            {
                if (i == 0) positions[i] = targetPoint;
                else
                {
                    var angle = (angleOffset + (i - 1) * 120f) * Mathf.Deg2Rad;
                    positions[i] = targetPoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.8f;
                }
                if (room) positions[i] = room.ClampInside(positions[i], 1.2f);
                warnings[i] = CreateRing(positions[i], 1.35f, new Color(.55f, 1f, .18f, .88f), 36, .06f);
            }
            yield return new WaitForSeconds(.82f);
            for (var i = 0; i < positions.Length; i++)
            {
                if (warnings[i]) StartCoroutine(FadeOutVisual(warnings[i], .24f));
                Burst(positions[i], EnemyAttackStyle.ToxicVolley, new Color(.48f, 1f, .2f));
                SpawnEpicParticles(positions[i], new Color(.44f, 1f, .18f), 42, 2.8f, .8f, KharosParticleStyle.Spore);
                PlayBossSound(SfxCue.DarkMagicImpact, .22f);
                var cloud = new GameObject("Kharos_SporeCloud"); cloud.transform.position = positions[i];
                cloud.AddComponent<KharosPoisonCloud>().Configure(3f, 1.2f, .8f, AttackColor(new Color(.35f, .92f, .18f, .38f)));
            }
            yield return new WaitForSeconds(.3f); busy = false;
        }

        void SpawnSpine(Vector2 position, float damage)
        {
            var spike = new GameObject("Kharos_RootSpine"); spike.transform.position = position + Vector2.down * .7f;
            var visual = spike.AddComponent<SpriteRenderer>(); visual.sprite = CombatFeedback.WhiteSprite;
            visual.color = AttackColor(new Color(.8f, .25f, .12f)); visual.sortingOrder = 25;
            spike.transform.localScale = new Vector3(.3f, .08f, 1f);
            Burst(position, EnemyAttackStyle.HeavySmash, new Color(1f, .3f, .16f));
            SpawnEpicParticles(position, new Color(1f, .32f, .13f), 48, 3.1f, .78f, KharosParticleStyle.Bloom);
            PlayBossSound(SfxCue.HeavyImpact, .54f);
            ShakeCamera(.13f);
            StartCoroutine(SpineStrike(spike, position, damage));
        }

        IEnumerator SpineStrike(GameObject spike, Vector2 position, float damage)
        {
            const float riseTime = .14f;
            for (var elapsed = 0f; elapsed < riseTime; elapsed += Time.deltaTime)
            {
                if (!spike) yield break;
                var t = Mathf.Clamp01(elapsed / riseTime);
                spike.transform.localScale = new Vector3(.3f + t * .12f, .08f + t * 1.37f, 1f);
                yield return null;
            }
            foreach (var player in Players())
                if (player && Vector2.Distance(position, player.transform.position) < .9f) player.TakeDamage(damage, position + Vector2.down);
            if (spike) Destroy(spike, .16f);
        }

        void DamagePlayersInRadius(Vector2 center, float radius, float damage)
        {
            foreach (var player in Players())
                if (player && Vector2.Distance(center, player.transform.position) <= radius) player.TakeDamage(damage, center);
        }

        static PlayerController[] Players() => Object.FindObjectsByType<PlayerController>();
        PlayerController NearestPlayer()
        {
            PlayerController closest = null; var nearest = float.PositiveInfinity;
            foreach (var candidate in Players())
            {
                if (!candidate || candidate.IsDead) continue;
                var distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance < nearest) { nearest = distance; closest = candidate; }
            }
            return closest;
        }

        void SetTint(Color value)
        {
            value = AttackColor(value);
            if (sourceSprite) sourceSprite.color = new Color(value.r, value.g, value.b, sourceSprite.color.a);
            if (sprite) sprite.color = new Color(value.r, value.g, value.b, sprite.color.a);
        }
        void SetAlpha(float alpha)
        {
            if (sourceSprite) { var color = sourceSprite.color; color.a = Mathf.Clamp01(alpha); sourceSprite.color = color; }
            if (sprite) { var color = sprite.color; color.a = Mathf.Clamp01(alpha); sprite.color = color; }
            if (spriteGlow) spriteGlow.enabled = alpha > .08f;
            if (auraRenderer) auraRenderer.enabled = alpha > .08f;
        }
        public void Stun(float duration) { stunUntil = Mathf.Max(stunUntil, Time.time + duration); }

        GameObject CreateRing(Vector2 center, float radius, Color color, int segments, float width)
        {
            color = AttackColor(color);
            var marker = EnemySpawnVfxPool.Instance.Show(center, color);
            marker.ConfigureTelegraph(Mathf.Max(.35f, radius * 1.8f));
            return marker.gameObject;
        }

        static void EnsureGroundRingSprite()
        {
            if (groundRingSprite) return;
            var size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "KharosSoftGroundRing",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var px = (x + .5f) / size * 2f - 1f;
                var py = (y + .5f) / size * 2f - 1f;
                var delta = Mathf.Abs(new Vector2(px, py).magnitude - .78f);
                var core = Mathf.Exp(-Mathf.Pow(delta / .025f, 2f)) * .9f;
                var feather = Mathf.Exp(-Mathf.Pow(delta / .105f, 2f)) * .22f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(core + feather));
            }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            groundRingSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            groundRingSprite.name = "KharosSoftGroundRingSprite";
        }

        GameObject CreateBeamTelegraph(Vector2 start, Vector2 end, Color color, float width)
        {
            color = AttackColor(color);
            MakeLineMaterial();
            var root = new GameObject("Kharos_RushTelegraph");
            var haloObject = new GameObject("SoftGlow"); haloObject.transform.SetParent(root.transform, false);
            var halo = haloObject.AddComponent<LineRenderer>(); halo.useWorldSpace = true; halo.positionCount = 2;
            halo.sharedMaterial = lineMaterial; halo.startWidth = halo.endWidth = width * 4f;
            halo.startColor = halo.endColor = new Color(color.r, color.g, color.b, .18f); halo.sortingOrder = 13;
            halo.SetPosition(0, start); halo.SetPosition(1, end);
            var core = root.AddComponent<LineRenderer>(); core.useWorldSpace = true; core.positionCount = 2;
            core.sharedMaterial = lineMaterial; core.startWidth = core.endWidth = width;
            core.startColor = core.endColor = color; core.sortingOrder = 14;
            core.SetPosition(0, start); core.SetPosition(1, end);
            return root;
        }

        static void MakeLineMaterial()
        {
            if (lineMaterial) return;
            var shader = Shader.Find("Sprites/Default");
            if (shader) lineMaterial = new Material(shader) { name = "Kharos VFX Line" };
        }

        static void EnsureLineMaterial() => MakeLineMaterial();

        void OnDestroy()
        {
            activeBosses.Remove(this);
            IsImmune = false;
            if (shieldVisual) Destroy(shieldVisual);
            if (!deathPlayed && bossHud) Destroy(bossHud);
        }
    }

    sealed class KharosBossHudFade : MonoBehaviour
    {
        CanvasGroup group;
        bool fading;
        float duration, elapsed;
        public void FadeAndDestroy(float seconds)
        {
            if (fading) return;
            group = GetComponent<CanvasGroup>();
            if (!group) { Destroy(gameObject); return; }
            fading = true; duration = Mathf.Max(.05f, seconds); elapsed = 0f;
        }
        void Update()
        {
            if (!fading || !group) return;
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(group.alpha, 0f, Mathf.Clamp01(elapsed / duration));
            if (elapsed >= duration) Destroy(gameObject);
        }
    }

    public sealed class KharosCrossFocus : MonoBehaviour
    {
        Transform aura;
        public void Initialize(Transform glow) { aura = glow; }
        void Update()
        {
            var pulse = .82f + (Mathf.Sin(Time.time * 16f) + 1f) * .16f;
            if (aura) aura.localScale = Vector3.one * (.68f * pulse);
            transform.Rotate(0f, 0f, 90f * Time.deltaTime);
        }
    }

    /// <summary>Layered beam glow, traveling surges, electric filaments, and sparks for the X laser.</summary>
    public sealed class KharosElectricBeam : MonoBehaviour
    {
        const int ArcPointCount = 17;
        const int BranchCount = 5;
        LineRenderer arc;
        LineRenderer[] branches;
        LineRenderer travelingSurge;
        ParticleSystem sparks;
        LineRenderer halo, bloom, core;
        float extent;
        float nextArc, nextSpark, nextBranch;
        Color tint;

        public void Initialize(float beamExtent, Color beamColor, Material beamMaterial, LineRenderer beamHalo, LineRenderer beamBloom, LineRenderer beamCore)
        {
            extent = beamExtent;
            tint = beamColor;
            halo = beamHalo; bloom = beamBloom; core = beamCore;
            arc = gameObject.AddComponent<LineRenderer>();
            arc.useWorldSpace = false;
            arc.positionCount = ArcPointCount;
            arc.sharedMaterial = beamMaterial;
            arc.startWidth = arc.endWidth = .035f;
            arc.startColor = arc.endColor = Color.Lerp(tint, Color.white, .7f);
            arc.sortingOrder = 17;

            branches = new LineRenderer[BranchCount];
            for (var i = 0; i < branches.Length; i++)
                branches[i] = CreateElectricLine("BranchingArc_" + i, beamMaterial, .035f, 18);
            travelingSurge = CreateElectricLine("TravelingEnergy", beamMaterial, .16f, 19);
            travelingSurge.positionCount = 4;
            travelingSurge.startColor = new Color(1f, 1f, 1f, .05f);
            travelingSurge.endColor = new Color(1f, 1f, 1f, .9f);

            sparks = gameObject.AddComponent<ParticleSystem>();
            var main = sparks.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.16f, .34f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.25f, .9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .065f);
            main.startColor = Color.Lerp(tint, Color.white, .72f);
            main.maxParticles = 48;
            var emission = sparks.emission; emission.enabled = false;
            var shape = sparks.shape; shape.enabled = false;
            var lifetime = sparks.colorOverLifetime; lifetime.enabled = true;
            var alpha = new Gradient();
            alpha.SetKeys(
                new[] { new GradientColorKey(Color.Lerp(tint, Color.white, .72f), 0f), new GradientColorKey(Color.Lerp(tint, Color.white, .25f), 1f) },
                new[] { new GradientAlphaKey(.95f, 0f), new GradientAlphaKey(0f, 1f) });
            lifetime.color = alpha;
            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ProjectLike.Phase1.SoftGlowSprite.Material;
            renderer.sortingOrder = 17;
            sparks.Play();
            RefreshArc();
            RefreshBranches();
        }

        void Update()
        {
            var pulse = .82f + (Mathf.Sin(Time.time * 13f) + 1f) * .09f;
            if (halo) { halo.startWidth = halo.endWidth = .62f * pulse; SetAlpha(halo, .045f * pulse); }
            if (bloom) { bloom.startWidth = bloom.endWidth = .26f * pulse; SetAlpha(bloom, .16f * pulse); }
            if (core) { core.startWidth = core.endWidth = .068f * pulse; SetAlpha(core, .9f + pulse * .1f); }
            UpdateTravelingSurge();
            if (Time.time >= nextArc)
            {
                nextArc = Time.time + .045f;
                RefreshArc();
            }
            if (Time.time >= nextBranch)
            {
                nextBranch = Time.time + Random.Range(.09f, .16f);
                RefreshBranches();
            }
            if (Time.time < nextSpark) return;
            nextSpark = Time.time + .045f;
            for (var i = 0; i < 3; i++)
            {
                var distance = Random.Range(-extent * .94f, extent * .94f);
                var emit = new ParticleSystem.EmitParams
                {
                    position = transform.TransformPoint(new Vector3(distance, Random.Range(-.075f, .075f), 0f)),
                    velocity = new Vector3(Random.Range(-.5f, .5f), Random.Range(-1.15f, 1.15f), 0f),
                    startColor = Color.Lerp(tint, Color.white, Random.Range(.62f, .98f)),
                    startLifetime = Random.Range(.12f, .28f),
                    startSize = Random.Range(.025f, .075f)
                };
                sparks.Emit(emit, 1);
            }
        }

        LineRenderer CreateElectricLine(string name, Material material, float width, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.sharedMaterial = material; line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = Color.Lerp(tint, Color.white, .65f); line.sortingOrder = sortingOrder;
            line.numCapVertices = 2;
            return line;
        }

        void RefreshBranches()
        {
            if (branches == null) return;
            foreach (var branch in branches)
            {
                if (!branch) continue;
                var originX = Random.Range(-extent * .88f, extent * .88f);
                var direction = Random.value < .5f ? -1f : 1f;
                var length = Random.Range(.35f, 1.55f);
                var height = Random.Range(.3f, .9f) * direction;
                branch.positionCount = 5;
                branch.SetPosition(0, new Vector3(originX, 0f, 0f));
                branch.SetPosition(1, new Vector3(originX + length * .18f, height * .22f, 0f));
                branch.SetPosition(2, new Vector3(originX + length * .48f, height * .82f, 0f));
                branch.SetPosition(3, new Vector3(originX + length * .73f, height * .65f, 0f));
                branch.SetPosition(4, new Vector3(originX + length, height, 0f));
                var alpha = Random.Range(.3f, .9f);
                var branchColor = Color.Lerp(tint, Color.white, Random.Range(.5f, .9f)); branchColor.a = alpha;
                branch.startColor = branch.endColor = branchColor;
                branch.startWidth = Random.Range(.018f, .038f); branch.endWidth = .006f;
            }
        }

        void UpdateTravelingSurge()
        {
            if (!travelingSurge) return;
            var t = Mathf.Repeat(Time.time * 9f, extent * 2f + 2.4f) - extent - 1.2f;
            var end = t + 1.2f;
            travelingSurge.SetPosition(0, new Vector3(t - .24f, 0f, 0f));
            travelingSurge.SetPosition(1, new Vector3(t + .22f, .025f, 0f));
            travelingSurge.SetPosition(2, new Vector3(t + .72f, -.025f, 0f));
            travelingSurge.SetPosition(3, new Vector3(end, 0f, 0f));
        }

        static void SetAlpha(LineRenderer line, float alpha)
        {
            var color = line.startColor; color.a = alpha;
            line.startColor = line.endColor = color;
        }

        void RefreshArc()
        {
            if (!arc) return;
            for (var i = 0; i < ArcPointCount; i++)
            {
                var u = i / (float)(ArcPointCount - 1);
                var x = Mathf.Lerp(-extent, extent, u);
                var y = i == 0 || i == ArcPointCount - 1 ? 0f : Random.Range(-.075f, .075f);
                arc.SetPosition(i, new Vector3(x, y, 0f));
            }
            var flicker = Random.Range(.72f, 1f);
            var color = Color.Lerp(tint, Color.white, .72f);
            color.a = flicker;
            arc.startColor = arc.endColor = color;
        }
    }

    public sealed class KharosPoisonCloud : MonoBehaviour
    {
        float until, started, damage, radius, nextTick;
        SpriteRenderer visual;
        ParticleSystem motes;
        Color tint = new Color(.35f, .92f, .18f, .38f);
        void Awake()
        {
            visual = gameObject.AddComponent<SpriteRenderer>(); visual.sprite = ProjectLike.Phase1.SoftGlowSprite.Sprite;
            visual.sharedMaterial = ProjectLike.Phase1.SoftGlowSprite.Material; visual.color = new Color(.35f, .92f, .18f, .38f);
            visual.sortingOrder = 10; transform.localScale = Vector3.one * 3.4f;
            var motesObject = new GameObject("SwirlingSporeMotes"); motesObject.transform.SetParent(transform, false);
            motes = motesObject.AddComponent<ParticleSystem>();
            var main = motes.main; main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.7f, 1.5f); main.startSpeed = new ParticleSystem.MinMaxCurve(.08f, .32f);
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .065f); main.startColor = new Color(.62f, 1f, .24f, .62f); main.gravityModifier = -.04f;
            var emission = motes.emission; emission.rateOverTime = 18f;
            var shape = motes.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .34f; shape.radiusThickness = .65f;
            var rotation = motes.rotationOverLifetime; rotation.enabled = true; rotation.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var noise = motes.noise; noise.enabled = true; noise.strength = .22f; noise.frequency = .8f;
            var renderer = motesObject.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = ProjectLike.Phase1.SoftGlowSprite.Material; renderer.sortingOrder = 11;
            motes.Play();
        }
        public void Configure(float duration, float size, float tickDamage, Color effectTint)
        {
            started = Time.time; until = started + duration; radius = size; damage = tickDamage;
            tint = effectTint;
            if (visual) visual.color = tint;
            if (motes) { var main = motes.main; main.startColor = new Color(tint.r, tint.g, tint.b, .62f); }
        }
        void Update()
        {
            if (Time.time >= until) { Destroy(gameObject); return; }
            var phase = Mathf.Sin(Time.time * 4f) * .06f;
            transform.localScale = Vector3.one * (3.4f + phase);
            if (visual)
            {
                var fade = Mathf.Clamp01(Mathf.Min((until - Time.time) / .45f, (Time.time - started) / .35f));
                visual.color = new Color(tint.r, tint.g, tint.b, tint.a * .8f * fade * (.9f + phase));
            }
            if (Time.time < nextTick) return;
            nextTick = Time.time + .8f;
            foreach (var player in Object.FindObjectsByType<PlayerController>())
                if (player && Vector2.Distance(player.transform.position, transform.position) < radius) player.TakeDamage(damage, transform.position);
        }
    }

    public sealed class KharosGroundTelegraph : MonoBehaviour
    {
        SpriteRenderer[] sprites;
        LineRenderer[] lines;
        Color[] spriteColors, lineStartColors, lineEndColors;
        Transform ornaments;
        Vector3 baseScale;
        float started;

        public void Initialize(SpriteRenderer coreRenderer, SpriteRenderer glowRenderer, Transform rotatingMarks)
        {
            ornaments = rotatingMarks;
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            lines = GetComponentsInChildren<LineRenderer>(true);
            spriteColors = new Color[sprites.Length]; lineStartColors = new Color[lines.Length]; lineEndColors = new Color[lines.Length];
            for (var i = 0; i < sprites.Length; i++) if (sprites[i]) spriteColors[i] = sprites[i].color;
            for (var i = 0; i < lines.Length; i++) if (lines[i]) { lineStartColors[i] = lines[i].startColor; lineEndColors[i] = lines[i].endColor; }
        }

        void Start()
        {
            baseScale = transform.localScale; started = Time.time;
            transform.localScale = baseScale * .68f;
            SetAlpha(0f);
        }

        void Update()
        {
            var intro = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - started) / .2f));
            var pulse = (Mathf.Sin((Time.time - started) * 8f) + 1f) * .5f;
            transform.localScale = baseScale * Mathf.Lerp(.68f, 1f, intro) * (1f + pulse * .035f);
            if (ornaments) ornaments.Rotate(0f, 0f, 16f * Time.deltaTime);
            SetAlpha(intro * (.7f + pulse * .3f));
        }

        void SetAlpha(float multiplier)
        {
            if (sprites != null)
                for (var i = 0; i < sprites.Length; i++) if (sprites[i]) { var c = spriteColors[i]; c.a *= multiplier; sprites[i].color = c; }
            if (lines != null)
                for (var i = 0; i < lines.Length; i++) if (lines[i])
                {
                    var a = lineStartColors[i]; var b = lineEndColors[i]; a.a *= multiplier; b.a *= multiplier;
                    lines[i].startColor = a; lines[i].endColor = b;
                }
        }
    }

    public sealed class KharosThornProjectile : MonoBehaviour
    {
        Vector2 direction;
        float speed, damage, expiresAt;
        bool hit, fading;
        Color tint = Color.green;

        public void Initialize(Vector2 aim, float travelSpeed, float hitDamage, Color effectTint)
        {
            direction = aim.sqrMagnitude > .001f ? aim.normalized : Vector2.right;
            speed = travelSpeed; damage = hitDamage; tint = effectTint; expiresAt = Time.time + 3.5f;
            transform.right = direction;
        }

        void Update()
        {
            if (hit || Time.time >= expiresAt)
            {
                if (!fading) StartCoroutine(FadeOut());
                return;
            }
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
            foreach (var player in Object.FindObjectsByType<PlayerController>())
            {
                if (!player || Vector2.Distance(player.transform.position, transform.position) > .42f) continue;
                hit = true; player.TakeDamage(damage, transform.position);
                GameSfx.Play(SfxCue.DarkMagicImpact, transform.position, .34f);
                var vfx = CombatVfxPool.Instance;
                if (vfx) vfx.SpawnEnemyAbilityBurst(transform.position, EnemyAttackStyle.ToxicVolley, tint);
                break;
            }
        }

        IEnumerator FadeOut()
        {
            fading = true;
            var trail = GetComponent<TrailRenderer>();
            if (trail) trail.emitting = false;
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            var colors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++) if (renderers[i]) colors[i] = renderers[i].color;
            var originalScale = transform.localScale;
            for (var elapsed = 0f; elapsed < .16f && gameObject; elapsed += Time.deltaTime)
            {
                var fade = 1f - elapsed / .16f;
                transform.localScale = originalScale * fade;
                for (var i = 0; i < renderers.Length; i++)
                    if (renderers[i]) { var color = colors[i]; color.a *= fade; renderers[i].color = color; }
                yield return null;
            }
            if (trail) yield return new WaitForSeconds(trail.time);
            if (gameObject) Destroy(gameObject);
        }
    }

    public sealed class KharosExternalEffectFade : MonoBehaviour
    {
        public void Begin(float duration) { StopAllCoroutines(); StartCoroutine(Fade(duration)); }

        IEnumerator Fade(float duration)
        {
            var beams = GetComponentsInChildren<KharosElectricBeam>(true);
            foreach (var beam in beams) if (beam) beam.enabled = false;
            var sprites = GetComponentsInChildren<SpriteRenderer>(true);
            var lines = GetComponentsInChildren<LineRenderer>(true);
            var spriteColors = new Color[sprites.Length];
            var lineColors = new Color[lines.Length];
            for (var i = 0; i < sprites.Length; i++) if (sprites[i]) spriteColors[i] = sprites[i].color;
            for (var i = 0; i < lines.Length; i++) if (lines[i]) lineColors[i] = lines[i].startColor;
            var elapsed = 0f;
            while (elapsed < duration && gameObject)
            {
                elapsed += Time.unscaledDeltaTime;
                var alpha = 1f - Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration));
                for (var i = 0; i < sprites.Length; i++) if (sprites[i]) { var c = spriteColors[i]; c.a *= alpha; sprites[i].color = c; }
                for (var i = 0; i < lines.Length; i++) if (lines[i]) { var c = lineColors[i]; c.a *= alpha; lines[i].startColor = c; lines[i].endColor = c; }
                yield return null;
            }
            if (gameObject) Destroy(gameObject);
        }
    }

    public sealed class KharosBossCinematicOverlay : MonoBehaviour
    {
        CanvasGroup group;
        Image shade, portrait, echo, bloom;
        Text title, subtitle;
        RectTransform portraitRect, topBar, bottomBar;
        float phase = 1f;
        bool fading;
        static Font cinematicFont;

        public static KharosBossCinematicOverlay Create()
        {
            var root = new GameObject("KharosBossCinematic", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var overlay = root.AddComponent<KharosBossCinematicOverlay>();
            overlay.Build();
            return overlay;
        }

        void Build()
        {
            var canvas = GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 4700;
            var scaler = GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(960f, 540f);
            group = GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            shade = MakeImage("ShadowCurtain", transform, Vector2.zero, new Vector2(1200f, 800f), new Color(.008f, .016f, .02f, .1f));
            shade.enabled = false;
            topBar = MakeBar("TopLetterbox", transform, true);
            bottomBar = MakeBar("BottomLetterbox", transform, false);
            bloom = MakeImage("BossRevealGlow", transform, new Vector2(0f, -20f), new Vector2(320f, 320f), Color.clear);
            bloom.enabled = false;
            echo = MakeImage("ChromaticEcho", transform, new Vector2(-7f, 7f), new Vector2(160f, 190f), new Color(.1f, .95f, 1f, .46f)); echo.preserveAspect = true; echo.enabled = false;
            portrait = MakeImage("KharosPortrait", transform, new Vector2(0f, 0f), new Vector2(160f, 190f), Color.white); portrait.preserveAspect = true; portrait.enabled = false;
            portraitRect = portrait.rectTransform;
            title = MakeText("BossTitle", transform, new Vector2(0f, 239f), new Vector2(850f, 38f), 30, Color.white);
            subtitle = MakeText("BossSubtitle", transform, new Vector2(0f, 216f), new Vector2(850f, 22f), 13, new Color(.72f, 1f, .76f));
            group.alpha = 0f;
        }

        public void SetSequence(int sequence, float progress, string headline, string caption, Sprite bossSprite)
        {
            if (!group) return;
            phase = sequence;
            fading = false;
            group.alpha = 1f;
            title.text = headline;
            subtitle.text = caption;
            var bars = sequence == 2 ? 58f : 58f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / .1f));
            SetBarHeight(topBar, bars); SetBarHeight(bottomBar, bars);
            var titleAlpha = sequence == 2 ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - .28f) / .12f));
            title.color = new Color(1f, 1f, 1f, titleAlpha);
            subtitle.color = new Color(.72f, 1f, .76f, titleAlpha);
        }

        public void FadeAndDestroy(float duration) { if (!fading) StartCoroutine(Fade(duration)); }

        IEnumerator Fade(float duration)
        {
            fading = true;
            var start = group ? group.alpha : 0f;
            for (var elapsed = 0f; elapsed < duration && group; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                group.alpha = Mathf.Lerp(start, 0f, t);
                SetBarHeight(topBar, Mathf.Lerp(topBar ? topBar.sizeDelta.y : 0f, 0f, Time.unscaledDeltaTime * 4f));
                SetBarHeight(bottomBar, Mathf.Lerp(bottomBar ? bottomBar.sizeDelta.y : 0f, 0f, Time.unscaledDeltaTime * 4f));
                yield return null;
            }
            if (gameObject) Destroy(gameObject);
        }

        static RectTransform MakeBar(string name, Transform parent, bool top)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = top ? new Vector2(0f, 1f) : Vector2.zero;
            rect.anchorMax = top ? Vector2.one : new Vector2(1f, 0f);
            rect.pivot = top ? new Vector2(.5f, 1f) : new Vector2(.5f, 0f);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(0f, 0f);
            var image = go.GetComponent<Image>(); image.color = Color.black; image.raycastTarget = false;
            return rect;
        }

        static void SetBarHeight(RectTransform rect, float height)
        { if (rect) rect.sizeDelta = new Vector2(0f, height); }

        static Image MakeImage(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }

        static Text MakeText(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            var hud = Object.FindAnyObjectByType<PixelHUDCanvas>();
            if (hud && hud.pixelFont) cinematicFont = hud.pixelFont;
            if (!cinematicFont) cinematicFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = cinematicFont; text.fontSize = fontSize; text.fontStyle = FontStyle.Bold; text.alignment = TextAnchor.MiddleCenter; text.color = color; text.raycastTarget = false;
            var shadow = go.AddComponent<Shadow>(); shadow.effectColor = new Color(0f, .08f, .04f, .9f); shadow.effectDistance = new Vector2(2f, -3f);
            return text;
        }
    }

    public static class KharosCinematicHudFader
    {
        sealed class HudState
        {
            public CanvasGroup group;
            public float alpha;
            public bool interactable, blocksRaycasts;
        }

        static readonly List<HudState> hiddenHud = new List<HudState>();

        public static IEnumerator FadeOut(float duration)
        {
            if (hiddenHud.Count == 0)
            {
                var seen = new HashSet<Canvas>();
                foreach (var canvas in Object.FindObjectsByType<Canvas>())
                {
                    if (!canvas || canvas.rootCanvas != canvas || canvas.GetComponent<KharosBossCinematicOverlay>() || !seen.Add(canvas)) continue;
                    var group = canvas.GetComponent<CanvasGroup>();
                    if (!group) group = canvas.gameObject.AddComponent<CanvasGroup>();
                    hiddenHud.Add(new HudState { group = group, alpha = group.alpha, interactable = group.interactable, blocksRaycasts = group.blocksRaycasts });
                }
            }
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(.01f, duration));
                foreach (var state in hiddenHud)
                {
                    if (!state.group) continue;
                    state.group.alpha = Mathf.Lerp(state.alpha, 0f, t);
                    state.group.interactable = false; state.group.blocksRaycasts = false;
                }
                yield return null;
            }
            foreach (var state in hiddenHud) if (state.group) state.group.alpha = 0f;
        }

        public static IEnumerator FadeIn(float duration)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(.01f, duration));
                foreach (var state in hiddenHud)
                    if (state.group) state.group.alpha = Mathf.Lerp(0f, state.alpha, t);
                yield return null;
            }
            foreach (var state in hiddenHud)
            {
                if (!state.group) continue;
                state.group.alpha = state.alpha;
                state.group.interactable = state.interactable;
                state.group.blocksRaycasts = state.blocksRaycasts;
            }
            hiddenHud.Clear();
        }
    }

    public sealed class KharosRootQte : MonoBehaviour
    {
        const int RequiredPresses = 5;
        PlayerController player;
        KharosBossAI boss;
        GameObject overlay;
        Text label;
        Image keyIcon;
        Sprite keyIcon138, keyIcon136;
        float nextIconSwap;
        int presses;
        float deadline, nextDamage;
        public int Presses => presses;
        public bool IsActive => boss && player && isActiveAndEnabled;

        public void Begin(KharosBossAI source)
        {
            if (boss) return;
            player = GetComponent<PlayerController>(); boss = source;
            if (!player || !boss) { Destroy(this); return; }
            player.SetMovementLocked(true);
            deadline = Time.unscaledTime + 3f; nextDamage = Time.time + .65f;
            BuildOverlay(); RefreshLabel();
        }

        void Update()
        {
            if (!player || !boss) { Release(false); return; }
            if (Time.time >= nextDamage) { nextDamage = Time.time + .8f; player.TakeDamage(.55f, boss.transform.position); }
            var actor = player.GetComponent<CoopPlayer>();
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            var pressed = actor
                ? actor.controls.Down(CoopButtons.Qte)
                : keyboard != null && keyboard.rKey.wasPressedThisFrame || gamepad != null && gamepad.buttonWest.wasPressedThisFrame;
            if (pressed)
            {
                presses++; RefreshLabel();
                if (presses >= RequiredPresses) { Release(true); return; }
            }
            if (keyIcon && Time.unscaledTime >= nextIconSwap)
            {
                keyIcon.sprite = keyIcon.sprite == keyIcon138 ? keyIcon136 : keyIcon138;
                nextIconSwap = Time.unscaledTime + .13f;
            }
            if (Time.unscaledTime >= deadline) Release(false);
        }

        void BuildOverlay()
        {
            overlay = new GameObject("KharosRootQTE", typeof(Canvas), typeof(CanvasScaler));
            var canvas = overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 4500;
            var scaler = overlay.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(960, 540);
            var panel = new GameObject("QTELabel", typeof(RectTransform), typeof(Text)); panel.transform.SetParent(overlay.transform, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = new Vector2(0f, -101f); rect.sizeDelta = new Vector2(760f, 54f);
            label = panel.GetComponent<Text>();
            var hud = FindAnyObjectByType<ProjectLike.Phase1.PixelHUDCanvas>();
            label.font = hud && hud.pixelFont ? hud.pixelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 23; label.alignment = TextAnchor.MiddleCenter; label.color = Color.white; label.raycastTarget = false;
            var shadow = panel.AddComponent<Shadow>(); shadow.effectColor = Color.black; shadow.effectDistance = new Vector2(2f, -2f);
            keyIcon138 = hud ? hud.kharosQteIcon138 : null;
            keyIcon136 = hud ? hud.kharosQteIcon136 : null;
            if (keyIcon138 || keyIcon136)
            {
                var icon = new GameObject("AlternatingRKeyIcon", typeof(RectTransform), typeof(Image)); icon.transform.SetParent(overlay.transform, false);
                var iconRect = icon.GetComponent<RectTransform>(); iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(.5f, .5f); iconRect.anchoredPosition = new Vector2(0f, -55f); iconRect.sizeDelta = new Vector2(48f, 48f);
                keyIcon = icon.GetComponent<Image>(); keyIcon.sprite = keyIcon138 ? keyIcon138 : keyIcon136; keyIcon.preserveAspect = true; keyIcon.raycastTarget = false;
                nextIconSwap = Time.unscaledTime + .13f;
            }
        }

        void RefreshLabel() { if (label) label.text = "ATRAPADO - PULSA RAPIDO R " + presses + "/" + RequiredPresses; }

        void Release(bool success)
        {
            if (player) player.SetMovementLocked(false);
            if (success && boss) boss.NotifyQteSuccess(player ? (Vector2)player.transform.position : (Vector2)boss.transform.position);
            if (success && player)
            {
                var vfx = CombatVfxPool.Instance;
                if (vfx) vfx.SpawnEnemyAbilityBurst(player.transform.position, EnemyAttackStyle.ArcaneBurst, new Color(.35f, .9f, 1f));
                var camera = Object.FindAnyObjectByType<CameraFollow>();
                if (camera) camera.Shake(.16f);
            }
            if (overlay) Destroy(overlay);
            Destroy(this);
        }

        void OnDestroy()
        {
            if (player) player.SetMovementLocked(false);
            if (overlay) Destroy(overlay);
        }
    }
}
