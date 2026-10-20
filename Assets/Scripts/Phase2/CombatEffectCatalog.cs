using System;
using UnityEngine;

namespace ProjectLike.Phase2
{
    [CreateAssetMenu(menuName = "Project Like/Combat Effect Catalog")]
    public class CombatEffectCatalog : ScriptableObject
    {
        [Serializable] public class Entry { public string name; public Sprite[] frames; }
        public Entry[] effects = Array.Empty<Entry>();
        public Sprite[] Get(string assetName)
        {
            foreach (var entry in effects) if (entry != null && entry.name == assetName) return entry.frames;
            return Array.Empty<Sprite>();
        }
    }
}
