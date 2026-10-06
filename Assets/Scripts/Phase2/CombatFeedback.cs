using UnityEngine;
using ProjectLike.Phase1;
using ProjectLike.Audio;

namespace ProjectLike.Phase2
{
    public static class CombatFeedback
    {
        static Sprite whiteSprite;
        public static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite) return whiteSprite;
                var texture = new Texture2D(1, 1);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                whiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
                return whiteSprite;
            }
        }

        public static Sprite GetWeaponSprite(WeaponData data)
        {
            if (data.sprite) return data.sprite;
            if (!data.spriteSheet) return null;
            var texture = data.spriteSheet;
            var x = Mathf.Clamp(data.spriteColumn, 0, texture.width / 16 - 1) * 16;
            var y = texture.height - (Mathf.Clamp(data.spriteRow, 0, texture.height / 16 - 1) + 1) * 16;
            return Sprite.Create(texture, new Rect(x, y, 16, 16), new Vector2(.5f, .5f), 16);
        }

        public static void ShowAttack(Vector2 origin, Vector2 direction, WeaponData data, int comboIndex)
        {
            CombatVfxPool.Instance.SpawnAttackSlash(origin, direction, data, comboIndex);
        }

        public static void ShowImpact(Vector2 position, CombatHit hit, ImpactSurface surface)
        {
            GameSfx.Impact(position, hit, surface);
            CombatVfxPool.Instance.SpawnImpact(position, hit, surface);
        }

        public static Color GetImpactColor(WeaponData data, ImpactSurface surface)
        {
            if (data && data.rarity >= WeaponRarity.Rare && data.SpecialEffects != null && data.SpecialEffects.Length > 0) return data.effectColor;
            if (surface == ImpactSurface.Stone) return new Color(.78f, .74f, .66f);
            if (surface == ImpactSurface.Wood) return new Color(.76f, .46f, .22f);
            if (surface == ImpactSurface.Metal) return new Color(.75f, .88f, 1f);
            if (surface == ImpactSurface.Magic) return new Color(.76f, .28f, 1f);
            if (!data) return new Color(1f, .2f, .2f);
            if (data.elemental) return Color.cyan;
            if (data.SpecialEffects != null)
                foreach (var effect in data.SpecialEffects)
                {
                    if (effect == WeaponSpecialEffectType.Fire) return new Color(1f, .25f, .04f);
                    if (effect == WeaponSpecialEffectType.Ice) return new Color(.35f, .85f, 1f);
                    if (effect == WeaponSpecialEffectType.Shock) return new Color(.8f, .35f, 1f);
                    if (effect == WeaponSpecialEffectType.Bleed) return new Color(.8f, .05f, .12f);
                }
            return data.type == WeaponType.Hammer || data.type == WeaponType.Axe ? new Color(1f, .55f, .12f) : new Color(.85f, .92f, 1f);
        }
    }

    public class CombatHitFlash : MonoBehaviour
    {
        SpriteRenderer spriteRenderer;
        Color baseColor;
        float until;
        void Awake() { spriteRenderer = GetComponentInChildren<SpriteRenderer>(); if (spriteRenderer) baseColor = spriteRenderer.color; baseScale = transform.localScale; }
        Vector3 baseScale;
        public void Play() { until = ProjectLike.Phase12.TimeStopAbility.PlayerTime + .12f; if (spriteRenderer) spriteRenderer.color = Color.white; transform.localScale = baseScale * 1.13f; }
        void Update() { if (spriteRenderer && until > 0 && ProjectLike.Phase12.TimeStopAbility.PlayerTime >= until) { spriteRenderer.color = baseColor; transform.localScale = baseScale; until = 0; } }
    }
}
