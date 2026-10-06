using System;
using System.Collections.Generic;
using ProjectLike.Phase2;
using UnityEngine;

namespace ProjectLike.Phase8
{
    public class PlayerInventory : MonoBehaviour
    {
        public int keys;
        public List<WeaponData> weapons = new List<WeaponData>();
        public List<LootData> specialItems = new List<LootData>();
        public event Action Changed;

        public void AddWeapon(WeaponData weapon) { if (weapon) weapons.Add(weapon); Changed?.Invoke(); }
        public void RemoveWeapon(WeaponData weapon) { if (weapon) weapons.Remove(weapon); Changed?.Invoke(); }
        public void AddKeys(int amount) { keys = Mathf.Max(0, keys + amount); Changed?.Invoke(); }
        public void AddSpecial(LootData item) { if (item) specialItems.Add(item); Changed?.Invoke(); }
        public void ResetForNewRun() { keys = 0; weapons.Clear(); specialItems.Clear(); Changed?.Invoke(); }
    }
}
