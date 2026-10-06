using System.Collections.Generic;
using ProjectLike.Phase1;
using UnityEngine;

namespace ProjectLike.Phase3
{
    public class MobDropPool : MonoBehaviour
    {
        public static MobDropPool Instance
        {
            get
            {
                if (!instance)
                {
                    var root = new GameObject("MobDropPool");
                    instance = root.AddComponent<MobDropPool>();
                }
                return instance;
            }
        }

        static MobDropPool instance;
        public static void ClearActiveDrops()
        {
            if (!instance) return;
            foreach (var orb in Object.FindObjectsByType<MobDropOrb>())
                instance.Recycle(orb);
        }
        readonly Queue<MobDropOrb> available = new Queue<MobDropOrb>();
        [Min(4)] public int warmCount = 24;

        void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this;
            for (var i = 0; i < warmCount; i++) available.Enqueue(CreateOrb());
        }

        public void SpawnDrops(Vector2 origin, PlayerStats target, int coins, int energy, float healthChance = .035f)
        {
            if (!target) return;
            SpawnSplit(MobDropType.Coin, origin, target, Mathf.Max(1, coins), Mathf.Clamp(Mathf.CeilToInt(coins / 2f), 1, 3));
            SpawnSplit(MobDropType.Energy, origin, target, Mathf.Max(1, energy), Mathf.Clamp(Mathf.CeilToInt(energy / 7f), 1, 3));
            if (Random.value < Mathf.Clamp01(healthChance)) SpawnSplit(MobDropType.Health, origin, target, 5, 1);
        }

        public void SpawnHealth(Vector2 origin, PlayerStats target, int amount)
        {
            if (target) SpawnSplit(MobDropType.Health, origin, target, Mathf.Max(1, amount), 1);
        }

        void SpawnSplit(MobDropType type, Vector2 origin, PlayerStats target, int total, int orbCount)
        {
            var remaining = total;
            for (var i = 0; i < orbCount; i++)
            {
                var amount = Mathf.CeilToInt(remaining / (float)(orbCount - i));
                remaining -= amount;
                var orb = available.Count > 0 ? available.Dequeue() : CreateOrb();
                orb.gameObject.SetActive(true);
                orb.Setup(this, type, amount, origin, target);
            }
        }

        MobDropOrb CreateOrb()
        {
            var go = new GameObject("Pooled Mob Drop");
            go.transform.SetParent(transform);
            go.SetActive(false);
            return go.AddComponent<MobDropOrb>();
        }

        public void Recycle(MobDropOrb orb)
        {
            if (!orb) return;
            orb.gameObject.SetActive(false);
            orb.transform.SetParent(transform);
            available.Enqueue(orb);
        }
    }
}
