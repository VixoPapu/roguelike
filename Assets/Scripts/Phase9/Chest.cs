using System.Collections;
using ProjectLike.Phase2;
using ProjectLike.Phase3;
using ProjectLike.Phase8;
using ProjectLike.Phase9;
using UnityEngine;
using ProjectLike.Audio;

namespace ProjectLike.Phase1
{
    public class Chest : Interactable
    {
        public ChestType chestType = ChestType.Common;
        public bool opened;
        public LootTable lootTable;
        [Min(0)] public int lootRolls = 1;

        SpriteRenderer spriteRenderer;
        ChestVisualCatalog visuals;
        Sprite[] frames;
        bool opening;

        void Awake()
        {
            visuals = Resources.Load<ChestVisualCatalog>("ChestVisualCatalog");
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (!spriteRenderer) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 7;
            ApplyType();
        }

        public void Configure(ChestType type, LootTable table = null, int rolls = 1)
        {
            chestType = type;
            lootTable = table;
            lootRolls = Mathf.Max(0, rolls);
            ApplyType();
        }

        void ApplyType()
        {
            frames = visuals ? visuals.Frames(chestType) : null;
            if (spriteRenderer && frames != null && frames.Length > 0) spriteRenderer.sprite = frames[0];
            if (spriteRenderer) spriteRenderer.color = Color.white;
            prompt = "Abrir cofre " + DisplayName(chestType);
        }

        public override void Interact(GameObject player)
        {
            if (opened || opening || !player) return;
            var stats = player.GetComponent<PlayerStats>();
            if (!stats) return;
            StartCoroutine(OpenRoutine(player, stats));
        }

        IEnumerator OpenRoutine(GameObject player, PlayerStats stats)
        {
            opening = true;
            GameSfx.Play(SfxCue.ChestOpen, transform.position, .8f);
            prompt = "Abriendo...";
            if (frames != null && frames.Length > 0)
            {
                for (var i = 1; i < frames.Length; i++)
                {
                    spriteRenderer.sprite = frames[i];
                    transform.localScale = Vector3.one * (1f + Mathf.Sin(i / (float)frames.Length * Mathf.PI) * .08f);
                    yield return new WaitForSeconds(.095f);
                }
            }
            transform.localScale = Vector3.one;
            opened = true;
            opening = false;
            prompt = "Cofre abierto";
            ReleaseRewards(player, stats);
        }

        void ReleaseRewards(GameObject player, PlayerStats stats)
        {
            GetDropValues(out var coins, out var energy, out var healthChance, out var weaponChance, out var rarity);
            MobDropPool.Instance.SpawnDrops(transform.position, stats, Random.Range(coins.x, coins.y + 1), Random.Range(energy.x, energy.y + 1), healthChance);
            if (lootTable && lootRolls > 0) LootSpawner.SpawnTableCapped(lootTable, transform.position, lootRolls, stats.luck, MaximumTableRarity());
            if (Random.value <= weaponChance)
            {
                var weapon = WeaponCatalog.Roll(ToWeaponRarity(MaximumTableRarity()), stats.luck);
                if (weapon) LootSpawner.SpawnWeapon(weapon, (LootRarity)weapon.rarity, (Vector2)transform.position + Vector2.up * .55f, true);
            }
            var color = LootRarityRules.Color(rarity);
            CombatVfxPool.Instance.SpawnEnemyAbilityBurst(transform.position, EnemyAttackStyle.ArcaneBurst, color);
            if (chestType == ChestType.Cursed)
            {
                stats.ModifyMaxHealth(-3f);
                CombatVfxPool.Instance.SpawnEnemyAbilityBurst(player.transform.position, EnemyAttackStyle.ArcaneBurst, new Color(.7f, .02f, .16f));
            }
        }

        void GetDropValues(out Vector2Int coins, out Vector2Int energy, out float healthChance, out float weaponChance, out LootRarity rarity)
        {
            if (chestType == ChestType.Rare) { coins = new Vector2Int(9, 17); energy = new Vector2Int(18, 34); healthChance = .1f; weaponChance = .85f; rarity = LootRarity.Rare; return; }
            if (chestType == ChestType.Cursed) { coins = new Vector2Int(12, 22); energy = new Vector2Int(20, 38); healthChance = 0f; weaponChance = 1f; rarity = LootRarity.Epic; return; }
            if (chestType == ChestType.Secret) { coins = new Vector2Int(16, 28); energy = new Vector2Int(28, 48); healthChance = .18f; weaponChance = 1f; rarity = LootRarity.Epic; return; }
            if (chestType == ChestType.Boss) { coins = new Vector2Int(28, 48); energy = new Vector2Int(42, 72); healthChance = .35f; weaponChance = 1f; rarity = LootRarity.Legendary; return; }
            coins = new Vector2Int(4, 10); energy = new Vector2Int(10, 22); healthChance = .04f; weaponChance = .38f; rarity = LootRarity.Common;
        }

        static string DisplayName(ChestType type) => type == ChestType.Rare ? "raro" : type == ChestType.Cursed ? "maldito" : type == ChestType.Secret ? "secreto" : type == ChestType.Boss ? "de boss" : "común";
        LootRarity MaximumTableRarity() => chestType == ChestType.Common ? LootRarity.Uncommon : chestType == ChestType.Rare ? LootRarity.Rare : chestType == ChestType.Boss ? LootRarity.Legendary : LootRarity.Epic;
        static WeaponRarity ToWeaponRarity(LootRarity rarity) => (WeaponRarity)Mathf.Clamp((int)rarity, 0, (int)WeaponRarity.Legendary);
    }
}
