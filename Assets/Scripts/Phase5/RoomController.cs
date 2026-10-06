using System.Collections;
using System.Collections.Generic;
using ProjectLike.Phase1;
using ProjectLike.Phase3;
using ProjectLike.Phase8;
using ProjectLike.Phase12;
using UnityEngine;
using UnityEngine.Events;
using ProjectLike.Audio;

namespace ProjectLike.Phase5
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class RoomController : MonoBehaviour
    {
        public RoomData data;
        public RoomDoor[] doors;
        public EnemyBrain[] sceneEnemies;
        [Tooltip("Objeto de recompensa ya presente en la sala, por ejemplo un cofre.")]
        public GameObject roomReward;
        public bool disableSceneEnemiesUntilEntered = true;
        [Tooltip("Distancia que el centro del jugador debe cruzar dentro de la sala antes de cerrar puertas.")]
        [Min(.25f)] public float combatEntryInset = 1.15f;
        public RoomState State { get; private set; } = RoomState.Dormant;
        public bool IsCompleted => State == RoomState.Cleared;
        public int ExistingEnemies => enemies.Count;
        public int LivingEnemies => CountLivingEnemies();
        public int CurrentWave { get; private set; }
        public int TotalWaves { get; private set; }
        public int TotalEnemiesSpawned { get; private set; }
        public bool IsRunningWaves => wavesRunning;
        public Bounds WorldBounds => roomBounds ? roomBounds.bounds : new Bounds(transform.position, Vector3.zero);
        public UnityEvent onEntered;
        public UnityEvent onCombatStarted;
        public UnityEvent onCompleted;

        readonly List<EnemyBrain> enemies = new List<EnemyBrain>();
        BoxCollider2D roomBounds;
        bool rewardSpawned;
        bool wavesRunning;
        GameObject activePlayer;

        void Awake()
        {
            roomBounds = GetComponent<BoxCollider2D>();
            roomBounds.isTrigger = true;
            if (doors == null || doors.Length == 0) doors = GetComponentsInChildren<RoomDoor>(true);
            CacheSceneEnemies();
            if (roomReward) roomReward.SetActive(false);
            if (data != null && data.RequiresCombat && disableSceneEnemiesUntilEntered)
                foreach (var enemy in enemies) if (enemy) enemy.gameObject.SetActive(false);
            SetDoorsLocked(false);
        }

        void Start()
        {
            if (IsSafeRoom()) ForceSafeOpen();
            var player = FindAnyObjectByType<PlayerController>();
            if (player && roomBounds.bounds.Contains(player.transform.position)) Enter(player.gameObject);
        }

        void Update()
        {
            if (IsSafeRoom())
            {
                SetDoorsLocked(false);
                return;
            }
            EvaluateCombat();
        }

        /// <summary>Puede llamarse desde oleadas, spawners o pruebas tras matar enemigos.</summary>
        public void EvaluateCombat()
        {
            if (State != RoomState.Combat) return;
            DiscoverNewEnemies();
            if (!wavesRunning && CountLivingEnemies() == 0) CompleteRoom();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player) TryEnterFromTrigger(player);
        }

        // Respaldo para colliders creados/movidos en el mismo frame que la sala.
        // Así no se pierde la entrada aunque Unity no emita OnTriggerEnter2D.
        void OnTriggerStay2D(Collider2D other)
        {
            if (State != RoomState.Dormant) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player) TryEnterFromTrigger(player);
        }

        void TryEnterFromTrigger(PlayerController player)
        {
            if (!player || State != RoomState.Dormant) return;
            if (data != null && data.RequiresCombat && !IsInsideCombatThreshold(player.transform.position)) return;
            Enter(player.gameObject);
        }

        bool IsInsideCombatThreshold(Vector3 worldPosition)
        {
            var bounds = roomBounds.bounds;
            var insetX = Mathf.Min(combatEntryInset, bounds.extents.x * .4f);
            var insetY = Mathf.Min(combatEntryInset, bounds.extents.y * .4f);
            return worldPosition.x >= bounds.min.x + insetX && worldPosition.x <= bounds.max.x - insetX
                && worldPosition.y >= bounds.min.y + insetY && worldPosition.y <= bounds.max.y - insetY;
        }

        public void Enter(GameObject player)
        {
            if (State != RoomState.Dormant) return;
            activePlayer = player;
            State = RoomState.Entered;
            if (ProjectLike.Online.CoopSession.IsHost) ProjectLike.Online.CoopSession.Instance.PartyEntered(this, player);
            else { var powers=player.GetComponent<PowerUpRuntime>();if(powers)powers.OnRoomEntered(this); }
            onEntered?.Invoke();
            if (data != null && data.RequiresCombat) BeginCombat();
            else CompleteRoom();
        }

        public void BeginCombat()
        {
            if (State == RoomState.Combat || State == RoomState.Cleared) return;
            State = RoomState.Combat;
            if (data == null || data.closeDoorsOnCombat) SetDoorsLocked(true);
            foreach (var enemy in enemies) if (enemy) enemy.gameObject.SetActive(true);
            wavesRunning = true;
            StartCoroutine(RunWaves());
            onCombatStarted?.Invoke();
        }

        /// <summary>Registra tanto enemigos iniciales como invocaciones en esta sala.</summary>
        public void RegisterEnemy(EnemyBrain enemy)
        {
            AddEnemy(enemy);
        }

        /// <summary>Permite que la muerte abra las puertas en el mismo frame.</summary>
        public void NotifyEnemyDefeated(EnemyBrain enemy)
        {
            if (State != RoomState.Combat) return;
            if (!wavesRunning && CountLivingEnemies() == 0) CompleteRoom();
        }

        public Vector2 ClampInside(Vector2 worldPosition, float margin = .7f)
        {
            var bounds = WorldBounds;
            var safeMarginX = Mathf.Min(margin, bounds.extents.x * .45f);
            var safeMarginY = Mathf.Min(margin, bounds.extents.y * .45f);
            return new Vector2(
                Mathf.Clamp(worldPosition.x, bounds.min.x + safeMarginX, bounds.max.x - safeMarginX),
                Mathf.Clamp(worldPosition.y, bounds.min.y + safeMarginY, bounds.max.y - safeMarginY));
        }

        void CompleteRoom()
        {
            if (State == RoomState.Cleared) return;
            State = RoomState.Cleared;
            GameSfx.Play(SfxCue.RoomClear, transform.position, .7f);
            SetDoorsLocked(false);
            SpawnReward();
            if (ProjectLike.Online.CoopSession.IsHost) ProjectLike.Online.CoopSession.Instance.PartyCompleted(this);
            else if(activePlayer){var powers=activePlayer.GetComponent<PowerUpRuntime>();if(powers)powers.OnRoomCompleted(this);}
            onCompleted?.Invoke();
        }

        void CacheSceneEnemies()
        {
            if (sceneEnemies == null || sceneEnemies.Length == 0)
                sceneEnemies = FindObjectsByType<EnemyBrain>();
            foreach (var enemy in sceneEnemies) AddEnemy(enemy);
        }

        void DiscoverNewEnemies()
        {
            foreach (var enemy in FindObjectsByType<EnemyBrain>())
                if (enemy && roomBounds.bounds.Contains(enemy.transform.position)) AddEnemy(enemy);
        }

        void AddEnemy(EnemyBrain enemy)
        {
            if (!enemy || enemies.Contains(enemy) || !roomBounds.bounds.Contains(enemy.transform.position)) return;
            enemies.Add(enemy);
            enemy.AssignRoom(this);
        }

        int CountLivingEnemies()
        {
            var count = 0;
            for (var i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                if (!enemy) { enemies.RemoveAt(i); continue; }
                if (enemy.CurrentHealth > 0) count++;
            }
            return count;
        }

        IEnumerator RunWaves()
        {
            if (data == null || data.enemySpawns == null || data.enemySpawns.Length == 0)
            {
                wavesRunning = false;
                CompleteRoom();
                yield break;
            }

            var minimumWaves = Mathf.Clamp(data.minWaves, 1, 3);
            var maximumWaves = Mathf.Clamp(data.maxWaves, minimumWaves, 3);
            TotalWaves = Random.Range(minimumWaves, maximumWaves + 1);
            TotalEnemiesSpawned = 0;

            for (var wave = 1; wave <= TotalWaves; wave++)
            {
                CurrentWave = wave;
                if (wave > 1 && data.timeBetweenWaves > 0f)
                    yield return new WaitForSeconds(data.timeBetweenWaves);

                var minimumEnemies = Mathf.Max(1, data.minEnemiesPerWave);
                var maximumEnemies = Mathf.Max(minimumEnemies, data.maxEnemiesPerWave);
                var enemyCount = Random.Range(minimumEnemies, maximumEnemies + 1);
                var positions = BuildSpawnPositions(enemyCount);
                var markers = new EnemySpawnMarker[enemyCount];
                var templates = new EnemyData[enemyCount];
                var eliteFlags = new bool[enemyCount];

                for (var i = 0; i < enemyCount; i++)
                {
                    templates[i] = RandomEnemyTemplate();
                    eliteFlags[i] = data.roomType == RoomType.Elite || Random.value < data.eliteMixChance;
                    markers[i] = EnemySpawnVfxPool.Instance.Show(positions[i], eliteFlags[i] ? new Color(.72f, .18f, 1f) : SpawnColor(templates[i]));
                }

                yield return new WaitForSeconds(Mathf.Max(.1f, data.spawnTelegraphTime));

                for (var i = 0; i < enemyCount; i++)
                {
                    if (markers[i]) markers[i].PlaySpawn();
                    SpawnEnemy(templates[i], positions[i], eliteFlags[i]);
                    TotalEnemiesSpawned++;
                    if (i >= enemyCount - 1) continue;
                    var minimumDelay = Mathf.Clamp(data.minimumSpawnInterval, .1f, .7f);
                    var maximumDelay = Mathf.Clamp(data.maximumSpawnInterval, minimumDelay, .7f);
                    yield return new WaitForSeconds(Random.Range(minimumDelay, maximumDelay));
                }

                yield return new WaitUntil(() => CountLivingEnemies() == 0);
            }

            wavesRunning = false;
            CompleteRoom();
        }

        List<Vector2> BuildSpawnPositions(int count)
        {
            var positions = new List<Vector2>(count);
            var bounds = WorldBounds;
            var playerPosition = activePlayer ? (Vector2)activePlayer.transform.position : (Vector2)bounds.center;
            for (var i = 0; i < count; i++)
            {
                var candidate = (Vector2)bounds.center;
                for (var attempt = 0; attempt < 24; attempt++)
                {
                    candidate = new Vector2(
                        Random.Range(bounds.min.x + 1.15f, bounds.max.x - 1.15f),
                        Random.Range(bounds.min.y + 1.05f, bounds.max.y - 1.05f));
                    if (Vector2.Distance(candidate, playerPosition) < 2.1f) continue;
                    var separated = true;
                    foreach (var other in positions)
                        if (Vector2.Distance(candidate, other) < .72f) { separated = false; break; }
                    if (separated) break;
                }
                positions.Add(ClampInside(candidate, 1f));
            }
            return positions;
        }

        EnemyData RandomEnemyTemplate()
        {
            for (var attempt = 0; attempt < data.enemySpawns.Length * 2; attempt++)
            {
                var selected = data.enemySpawns[Random.Range(0, data.enemySpawns.Length)].enemy;
                if (selected) return selected;
            }
            foreach (var spawn in data.enemySpawns) if (spawn.enemy) return spawn.enemy;
            return null;
        }

        void SpawnEnemy(EnemyData template, Vector2 position, bool elite)
        {
            if (!template) return;
            var instance = new GameObject(template.enemyName);
            instance.transform.position = position;
            instance.AddComponent<SpriteRenderer>();
            var brain = instance.AddComponent<EnemyBrain>();
            var eliteHealth = elite ? 2.3f : 1f; var eliteDamage = elite ? 1.05f : 1f; var eliteSpeed = elite ? 1.08f : 1f; var eliteCooldown = elite ? .92f : 1f;
            brain.Configure(template, this, data.enemyHealthMultiplier * eliteHealth, data.enemyDamageMultiplier * eliteDamage, data.enemySpeedMultiplier * eliteSpeed, data.enemyCooldownMultiplier * eliteCooldown, data.patternBonus);
            if (elite) instance.AddComponent<EliteEnemy>().Configure(brain, EliteEnemy.RandomModifier(), data.worldNumber);
            instance.AddComponent<EnemySpriteAnimator>();
            RegisterEnemy(brain);
        }

        static Color SpawnColor(EnemyData template)
        {
            if (!template || template.behaviours == null) return new Color(.72f, .22f, 1f);
            foreach (var behaviour in template.behaviours)
            {
                if (behaviour == EnemyBehaviour.Summoner || behaviour == EnemyBehaviour.Teleport) return new Color(.68f, .25f, 1f);
                if (behaviour == EnemyBehaviour.Ranged || behaviour == EnemyBehaviour.Turret) return new Color(.18f, .7f, 1f);
                if (behaviour == EnemyBehaviour.Charger) return new Color(1f, .32f, .12f);
            }
            return new Color(.95f, .18f, .32f);
        }

        void SpawnReward()
        {
            if (rewardSpawned || data == null) return;
            rewardSpawned = true;
            StartCoroutine(RevealRewards());
        }

        IEnumerator RevealRewards()
        {
            var powers = activePlayer ? activePlayer.GetComponent<PowerUpRuntime>() : null;
            var bonus = powers ? powers.RoomRewardChanceBonus : 0f;
            var table = data.lootTable ? data.lootTable : LootRuntimeDefaults.SharedTable;
            var dropLoot = Random.value < Mathf.Clamp01(data.looseLootChance + bonus);
            var dropChest = Random.value < Mathf.Clamp01(data.rewardChance + bonus);
            yield return new WaitForSeconds(.45f);
            if (dropChest)
            {
                if (!roomReward)
                {
                    if (data.rewardPrefab) roomReward = Instantiate(data.rewardPrefab, transform.position, Quaternion.identity, transform);
                    else
                    {
                        roomReward = new GameObject("RoomReward");
                        roomReward.transform.SetParent(transform, false);
                        roomReward.AddComponent<CircleCollider2D>().isTrigger = true;
                        roomReward.AddComponent<Chest>();
                    }
                }
                var chest = roomReward.GetComponent<Chest>();
                if (chest) chest.Configure(chest.chestType, table, Mathf.Max(1, data.lootRolls));
                var scale = roomReward.transform.localScale;
                var colliders = roomReward.GetComponentsInChildren<Collider2D>();
                var enabledColliders = new List<Collider2D>();
                foreach (var collider in colliders) if (collider.enabled) { enabledColliders.Add(collider); collider.enabled = false; }
                roomReward.transform.localScale = Vector3.zero;
                roomReward.SetActive(true);
                for (var elapsed = 0f; elapsed < .3f; elapsed += Time.deltaTime)
                {
                    roomReward.transform.localScale = scale * Mathf.SmoothStep(0f, 1f, elapsed / .3f);
                    yield return null;
                }
                roomReward.transform.localScale = scale;
                foreach (var collider in enabledColliders) if (collider) collider.enabled = true;
                yield return new WaitForSeconds(.3f);
            }
            if (dropLoot)
                yield return LootSpawner.RevealTable(table, ClampInside((Vector2)transform.position + Vector2.down * 1.4f), Mathf.Max(1, data.lootRolls), LootSpawner.PlayerLuck());
        }

        void SetDoorsLocked(bool locked)
        {
            if (IsSafeRoom()) locked = false;
            if (doors == null) return;
            foreach (var door in doors) if (door) door.SetLocked(locked);
        }

        public void ForceSafeOpen()
        {
            if (!IsSafeRoom()) return;
            State = RoomState.Cleared;
            if (doors != null)
                foreach (var door in doors) if (door) door.SetPermanentOpen(true);
            SetDoorsLocked(false);
        }

        bool IsSafeRoom() => data && (data.roomType == RoomType.Entrance || data.roomType == RoomType.Exit || data.roomType == RoomType.Shop || data.roomType == RoomType.NPC || data.roomType == RoomType.Rest);
    }
}
