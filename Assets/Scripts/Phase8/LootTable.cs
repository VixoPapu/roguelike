using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectLike.Phase8
{
    [CreateAssetMenu(menuName = "Project Like/Loot/Loot Table", fileName = "LootTable_")]
    public class LootTable : ScriptableObject
    {
        public LootTableEntry[] entries;
        public bool allowDuplicates = true;

        public List<LootRoll> Roll(System.Random random, int rolls, float playerLuck, bool worldDropsOnly = false)
        {
            var results = new List<LootRoll>();
            if (entries == null || entries.Length == 0 || rolls <= 0) return results;
            var used = new HashSet<LootData>();
            for (var rollIndex = 0; rollIndex < rolls; rollIndex++)
            {
                var total = 0f;
                foreach (var entry in entries)
                    if (entry.enabled && entry.item && (!worldDropsOnly || entry.item.AllowedWorldDrop) && (allowDuplicates || !used.Contains(entry.item))) total += EffectiveWeight(entry, playerLuck);
                if (total <= 0) break;
                var choice = (float)random.NextDouble() * total;
                foreach (var entry in entries)
                {
                    if (!entry.enabled || !entry.item || worldDropsOnly && !entry.item.AllowedWorldDrop || !allowDuplicates && used.Contains(entry.item)) continue;
                    choice -= EffectiveWeight(entry, playerLuck);
                    if (choice > 0) continue;
                    var quantity = random.Next(Mathf.Max(1, entry.minQuantity), Mathf.Max(entry.minQuantity + 1, entry.maxQuantity + 1));
                    results.Add(new LootRoll { item = entry.item, quantity = quantity });
                    used.Add(entry.item);
                    break;
                }
            }
            return results;
        }

        static float EffectiveWeight(LootTableEntry entry, float luck)
        {
            var rarityLuck = 1f + Mathf.Max(0, luck) * .02f * (1f - entry.item.DropMultiplier);
            return Mathf.Max(0, entry.weight) * entry.item.DropMultiplier * rarityLuck;
        }
    }

    [Serializable]
    public struct LootTableEntry
    {
        public bool enabled;
        public LootData item;
        [Min(0)] public float weight;
        [Min(1)] public int minQuantity;
        [Min(1)] public int maxQuantity;
    }

    public struct LootRoll
    {
        public LootData item;
        public int quantity;
    }
}
