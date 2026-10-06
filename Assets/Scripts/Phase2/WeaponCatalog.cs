using System.Collections.Generic;
using UnityEngine;

namespace ProjectLike.Phase2
{
    /// <summary>Catálogo único para tienda, cofres y futuras recompensas.</summary>
    public static class WeaponCatalog
    {
        static readonly List<WeaponData> weapons = new List<WeaponData>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCatalog() => weapons.Clear();

        public static IReadOnlyList<WeaponData> All
        {
            get { EnsureLoaded(); return weapons; }
        }

        public static WeaponData Roll(WeaponRarity maximumRarity, float luck = 0f)
        {
            EnsureLoaded();
            var candidates = new List<WeaponData>();
            var weights = new List<float>();
            var total = 0f;
            foreach (var weapon in weapons)
            {
                if (!weapon || weapon.rarity > maximumRarity) continue;
                var distance = Mathf.Max(0, (int)maximumRarity - (int)weapon.rarity);
                var weight = 1f / (1f + distance * 1.15f);
                weight *= 1f + Mathf.Max(0f, luck) * .035f * ((int)weapon.rarity + 1);
                candidates.Add(weapon); weights.Add(weight); total += weight;
            }
            if (candidates.Count == 0) return null;
            var roll = Random.value * total;
            for (var i = 0; i < candidates.Count; i++) { roll -= weights[i]; if (roll <= 0f) return candidates[i]; }
            return candidates[candidates.Count - 1];
        }

        static void EnsureLoaded()
        {
            if (weapons.Count > 0 && weapons[0]) return;
            weapons.Clear();
            var loadout = Resources.Load<DefaultWeaponLoadout>("DefaultWeaponLoadout");
            if (loadout && loadout.availableWeapons != null)
                foreach (var weapon in loadout.availableWeapons) Add(weapon);
            foreach (var weapon in Resources.LoadAll<WeaponData>("Weapons")) Add(weapon);
        }

        static void Add(WeaponData weapon) { if (weapon && !weapons.Contains(weapon)) weapons.Add(weapon); }
    }
}
