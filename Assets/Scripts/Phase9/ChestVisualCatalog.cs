using UnityEngine;

namespace ProjectLike.Phase9
{
    public enum ChestType { Common, Rare, Cursed, Secret, Boss }

    [CreateAssetMenu(menuName = "Project Like/Chests/Visual Catalog", fileName = "ChestVisualCatalog")]
    public class ChestVisualCatalog : ScriptableObject
    {
        public Sprite[] commonFrames;
        public Sprite[] rareFrames;
        public Sprite[] cursedFrames;
        public Sprite[] secretFrames;
        public Sprite[] bossFrames;

        public Sprite[] Frames(ChestType type) => type == ChestType.Rare ? rareFrames : type == ChestType.Cursed ? cursedFrames : type == ChestType.Secret ? secretFrames : type == ChestType.Boss ? bossFrames : commonFrames;
    }
}
