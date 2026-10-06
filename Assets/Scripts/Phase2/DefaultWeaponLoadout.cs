using UnityEngine;

namespace ProjectLike.Phase2
{
    [CreateAssetMenu(menuName = "Project Like/Combat/Default Weapon Loadout", fileName = "DefaultWeaponLoadout")]
    public class DefaultWeaponLoadout : ScriptableObject
    {
        public WeaponData primary;
        public WeaponData secondary;
        public WeaponData[] availableWeapons;
    }
}
