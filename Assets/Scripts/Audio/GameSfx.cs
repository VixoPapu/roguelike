using System.Collections.Generic;
using ProjectLike.Phase2;
using UnityEngine;

namespace ProjectLike.Audio
{
    public enum SfxCue
    {
        SwordSwing, SwordImpact, BowShot, WeaponCharge, HeavySwing, HeavyImpact, MagicCast, DarkMagicCast, MagicImpact, DarkMagicImpact,
        Electric, Ice, Explosion, Wave, Stun, PlayerHurt, PlayerDeath, Dash, EnemyAttack, EnemySpecial,
        EnemyHurt, EnemyDeath, Coin, Energy, Health, ChestOpen, DoorOpen, DoorClose, Shield,
        UiConfirm, UiDeny, ShopBuy, Pickup, PowerUp, Consumable, Portal, RoomClear, Footstep, CardReveal
    }

    /// <summary>Pool central de SFX. Usa el significado del nombre original del clip y evita saturar impactos simultaneos.</summary>
    public sealed class GameSfx : MonoBehaviour
    {
        static GameSfx instance;
        GameSfxBank bank;
        readonly List<AudioSource> sources = new List<AudioSource>();
        readonly Dictionary<SfxCue, float> lastPlayed = new Dictionary<SfxCue, float>();
        int variation;
        const int MaxSources = 18;

        static GameSfx Instance
        {
            get
            {
                if (instance) return instance;
                var go = new GameObject("Game SFX");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<GameSfx>();
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this;
            bank = Resources.Load<GameSfxBank>("Audio/GameSfxBank");
        }

        public static void Play(SfxCue cue, Vector2 position, float volume = 1f, bool ui = false)
        {
            var service = Instance;
            if (!service.bank) return;
            var clips = service.bank.Clips(cue);
            if (clips == null || clips.Length == 0) return;
            var now = Time.unscaledTime;
            var throttle = cue == SfxCue.DoorOpen || cue == SfxCue.DoorClose ? .08f
                : cue == SfxCue.EnemyHurt || cue == SfxCue.EnemyAttack || cue == SfxCue.SwordImpact ? .035f
                : cue == SfxCue.Electric || cue == SfxCue.Ice || cue == SfxCue.Explosion || cue == SfxCue.Wave ? .04f
                : cue == SfxCue.Coin ? .025f : 0f;
            if (service.lastPlayed.TryGetValue(cue, out var previous) && now - previous < throttle) return;
            service.lastPlayed[cue] = now;
            var clip = clips[service.variation++ % clips.Length];
            if (!clip) return;
            var source = service.FreeSource();
            if (!source) return;
            source.transform.position = position;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = Random.Range(.94f, 1.06f);
            source.spatialBlend = ui ? 0f : .72f;
            source.minDistance = 2.5f;
            source.maxDistance = 18f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            if (!ui) ProjectLike.Online.CoopSession.EmitSound(clip, position, source.volume, source.pitch, source.spatialBlend);
        }

        static void Play(SfxCue cue, GameObject source, float volume = 1f, bool ui = false) =>
            Play(cue, source ? (Vector2)source.transform.position : Vector2.zero, volume, ui);

        public static void WeaponAttack(GameObject owner, WeaponData weapon)
        {
            if (!weapon) return;
            if (weapon.attackSound) return; // Respeta clips elegidos manualmente en armas concretas.
            if (weapon.HasSpecial(WeaponSpecialEffectType.Curse) || weapon.HasSpecial(WeaponSpecialEffectType.Gravity)) Play(SfxCue.DarkMagicCast, owner, .7f);
            else if (weapon.UsesEnergyPower || weapon.type == WeaponType.Staff || weapon.type == WeaponType.Grimoire) Play(SfxCue.MagicCast, owner, .68f);
            else if (weapon.projectile) Play(SfxCue.BowShot, owner, .62f);
            else if (weapon.type == WeaponType.Hammer || weapon.type == WeaponType.Axe) Play(SfxCue.HeavySwing, owner, .75f);
            else Play(SfxCue.SwordSwing, owner, .62f);
        }

        public static void Impact(Vector2 position, CombatHit hit, ImpactSurface surface)
        {
            if (hit.weapon && (hit.weapon.UsesEnergyPower || hit.weapon.type == WeaponType.Staff || hit.weapon.type == WeaponType.Grimoire)) Play(SfxCue.MagicImpact, position, hit.critical ? .78f : .52f);
            else if (hit.weapon && (hit.weapon.type == WeaponType.Hammer || hit.weapon.type == WeaponType.Axe)) Play(SfxCue.HeavyImpact, position, .72f);
            else Play(SfxCue.SwordImpact, position, hit.critical ? .8f : .55f);
        }

        public static void WeaponEffect(Vector2 position, WeaponSpecialEffectType effect)
        {
            var cue = effect == WeaponSpecialEffectType.Shock ? SfxCue.Electric
                : effect == WeaponSpecialEffectType.Ice ? SfxCue.Ice
                : effect == WeaponSpecialEffectType.Fire || effect == WeaponSpecialEffectType.Nova ? SfxCue.Explosion
                : effect == WeaponSpecialEffectType.Wave ? SfxCue.Wave
                : effect == WeaponSpecialEffectType.Stun ? SfxCue.Stun
                : effect == WeaponSpecialEffectType.Curse || effect == WeaponSpecialEffectType.Gravity || effect == WeaponSpecialEffectType.Poison ? SfxCue.DarkMagicImpact
                : effect == WeaponSpecialEffectType.ProjectileBreak || effect == WeaponSpecialEffectType.ShieldShred ? SfxCue.Shield
                : SfxCue.MagicImpact;
            Play(cue, position, .56f);
        }

        AudioSource FreeSource()
        {
            foreach (var source in sources) if (!source.isPlaying) return source;
            if (sources.Count >= MaxSources) return null;
            var child = new GameObject("SFX Voice " + sources.Count);
            child.transform.SetParent(transform);
            var created = child.AddComponent<AudioSource>();
            created.playOnAwake = false;
            created.dopplerLevel = 0f;
            sources.Add(created);
            return created;
        }
    }
}
