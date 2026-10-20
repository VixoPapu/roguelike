using System;
using System.Collections.Generic;
using ProjectLike.Phase1;
using ProjectLike.Phase2;
using ProjectLike.Phase3;
using ProjectLike.Phase5;
using ProjectLike.Phase9;
using ProjectLike.Phase10;
using ProjectLike.Phase12;
using ProjectLike.Phase8;
using UnityEngine;

namespace ProjectLike.Phase6
{
    /// <summary>Genera un grafo conectado y lo convierte en habitaciones jugables conectadas.</summary>
    public class DungeonGenerator : MonoBehaviour
    {
        [Header("Seed")]
        public bool useRandomSeedOnStart = true;
        public string fixedSeed = "RogueLike-Seed";
        [SerializeField] string currentSeed;
        [Header("Layout")]
        [Tooltip("Salas con enemigos. No incluye la entrada ni la sala del portal.")]
        [Min(4)] public int minGameplayRooms = 4;
        [Tooltip("Salas con enemigos. No incluye la entrada ni la sala del portal.")]
        [Min(4)] public int maxGameplayRooms = 7;
        public Vector2 roomSize = new Vector2(18f, 14f);
        [Tooltip("Conservado para compatibilidad con escenas antiguas. La separación real usa roomSize para que no existan huecos entre puertas.")]
        [Min(10f)] public float roomSpacing = 12f;
        [Header("Bridges")]
        [Min(2f)] public float bridgeLength = 10f;
        [Min(2f)] public float bridgeWidth = 3.2f;
        [Min(.15f)] public float bridgeRailThickness = .32f;
        [Header("Enemy pools")]
        public EnemyData[] commonEnemies;
        public EnemyData[] eliteEnemies;
        public EnemyData[] bossEnemies;
        [Header("Runtime")]
        public bool generateOnAwake = true;
        public bool replaceTrainingRoom = true;

        public string CurrentSeed => currentSeed;
        public int CurrentWorld { get; private set; } = 1;
        public int CurrentLevel { get; private set; } = 1;
        public bool HasMiniBossThisLevel { get; private set; }
        public const int MaximumWorld = 5;
        public const int LevelsPerWorld = 6;
        public DungeonLayout Layout { get; private set; }

        readonly Dictionary<Vector2Int, DungeonRoomLayout> roomLookup = new Dictionary<Vector2Int, DungeonRoomLayout>();
        readonly List<RoomData> runtimeRoomData = new List<RoomData>();
        readonly Vector2Int[] cardinal = { Vector2Int.up, Vector2Int.down, Vector2Int.right, Vector2Int.left };
        System.Random random;
        GameObject generatedRoot;
        Sprite squareSprite;
        bool kharosTestRun;

        void Awake()
        {
            if (generateOnAwake) GenerateRun(useRandomSeedOnStart ? Guid.NewGuid().ToString("N") : fixedSeed);
        }

        public void GenerateNewRun()
        {
            CurrentLevel++;
            if (CurrentLevel > LevelsPerWorld)
            {
                CurrentLevel = 1;
                CurrentWorld++;
                if (CurrentWorld > MaximumWorld) CurrentWorld = 1;
            }
            GenerateRun(Guid.NewGuid().ToString("N"));
        }

        public void AdvanceRun(PlayerController player)
        {
            if (CurrentWorld == MaximumWorld && CurrentLevel == LevelsPerWorld) RestartRun(player);
            else GenerateNewRun();
        }

        public void RestartRun(PlayerController player)
        {
            CurrentWorld = 1; CurrentLevel = 1;
            if (player) player.RestartForNewRun();
            GenerateRun(Guid.NewGuid().ToString("N"));
        }

        public void StartKharosTest(PlayerController player)
        {
            var kharos = Resources.Load<EnemyData>("Enemy_Kharos");
            if (!kharos) { Debug.LogError("No se encontró Assets/Resources/Enemies/Kharos.asset"); return; }
            CurrentWorld = 1; CurrentLevel = 1; HasMiniBossThisLevel = true; kharosTestRun = true;
            currentSeed = "KHAROS-TEST"; random = new System.Random(StableHash(currentSeed));
            PopulateEnemyPoolsFromScene(); ClearGeneratedDungeon();
            if (replaceTrainingRoom) DisableTrainingRoom();
            var entrance = new DungeonRoomLayout { gridPosition = Vector2Int.zero, roomType = RoomType.Entrance, visualVariant = RoomVisualVariant.Dungeon, mainPath = true, hasCustomGeometry = true, worldPosition = Vector2.zero, customRoomSize = new Vector2(18f, 14f) };
            // Keep the entrance, short connecting bridge, arena, and exit on one axis.
            var arena = new DungeonRoomLayout { gridPosition = Vector2Int.right, roomType = RoomType.Boss, visualVariant = RoomVisualVariant.BossArena, mainPath = true, hasCustomGeometry = true, worldPosition = new Vector2(37f, 0f), customRoomSize = new Vector2(50f, 36f) };
            var exit = new DungeonRoomLayout { gridPosition = Vector2Int.right * 2, roomType = RoomType.Exit, visualVariant = RoomVisualVariant.Temple, mainPath = true, hasCustomGeometry = true, worldPosition = new Vector2(74f, 0f), customRoomSize = new Vector2(18f, 14f) };
            entrance.connections.Add(RoomDirection.East); arena.connections.Add(RoomDirection.West); arena.connections.Add(RoomDirection.East); exit.connections.Add(RoomDirection.West);
            Layout = new DungeonLayout { seed = currentSeed, rooms = new List<DungeonRoomLayout> { entrance, arena, exit } };
            if (!ValidateCurrentLayout(out var reason)) { Debug.LogError("Arena de Kharos inválida: " + reason); return; }
            kharosTestRun = true;
            BuildPlayableDungeon();
            if (player) { player.RestartForNewRun(); }
        }

        public void GenerateRun(string seed)
        {
            kharosTestRun = false;
            currentSeed = string.IsNullOrWhiteSpace(seed) ? "RogueLike-Seed" : seed;
            random = new System.Random(StableHash(currentSeed));
            HasMiniBossThisLevel = CurrentLevel == LevelsPerWorld;
            PopulateEnemyPoolsFromScene();
            ClearGeneratedDungeon();
            if (IsFinalBossLevel) KharosBossAI.WarmupSpawnAssets();
            if (replaceTrainingRoom) DisableTrainingRoom();
            Layout = BuildLayout(currentSeed);
            if (!ValidateCurrentLayout(out var reason)) { Debug.LogError("Dungeon inválido: " + reason); return; }
            BuildPlayableDungeon();
            Debug.Log("[Dungeon] Mundo " + CurrentWorld + "-" + CurrentLevel + " · Seed " + currentSeed + " · "
                + (IsFinalBossLevel ? "entrada + arena de jefe gigante + portal desbloqueable." : (Layout.rooms.Count - 2) + " salas + entrada + portal."));
        }

        void PopulateEnemyPoolsFromScene()
        {
            var roster = Resources.Load<EnemyRoster>("EnemyRoster");
            if (roster && roster.common != null && roster.common.Length > 0)
            {
                commonEnemies = roster.common;
                eliteEnemies = roster.elite != null && roster.elite.Length > 0 ? roster.elite : roster.common;
                bossEnemies = roster.bosses != null && roster.bosses.Length > 0 ? roster.bosses : eliteEnemies;
                return;
            }
            if (commonEnemies != null && commonEnemies.Length > 0) return;
            var found = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include);
            var common = new List<EnemyData>();
            var elite = new List<EnemyData>();
            foreach (var enemy in found)
            {
                if (!enemy || !enemy.data || common.Contains(enemy.data)) continue;
                common.Add(enemy.data);
                if (enemy.data.maxHealth >= 100 || enemy.data.behaviours != null && Array.Exists(enemy.data.behaviours, item => item == EnemyBehaviour.Charger)) elite.Add(enemy.data);
            }
            commonEnemies = common.ToArray();
            eliteEnemies = elite.Count > 0 ? elite.ToArray() : commonEnemies;
            bossEnemies = eliteEnemies;
        }

        DungeonLayout BuildLayout(string seed)
        {
            var layout = new DungeonLayout { seed = seed };
            roomLookup.Clear();
            if (IsFinalBossLevel) return BuildFinalBossLayout(layout);
            var start = AddRoom(layout, Vector2Int.zero, RoomType.Entrance, true);
            var minimum = Mathf.Max(4, minGameplayRooms);
            var maximum = Mathf.Max(minimum, maxGameplayRooms);
            // El primer mundo es mas corto para que los cinco niveles iniciales
            // sean rapidos de aprender y no se alargue el tutorial.
            var gameplayRooms = CurrentWorld == 1 ? random.Next(3, 5) : random.Next(minimum, maximum + 1);
            if (!ExtendMainPath(layout, start, gameplayRooms, gameplayRooms))
                throw new InvalidOperationException("No fue posible crear el camino principal.");
            // Una tienda garantizada por piso, sin sumar habitaciones ni reemplazar
            // la primera pelea ni el boss.
            if (layout.rooms.Count > 3)
            {
                var shopIndex = random.Next(2, layout.rooms.Count - 1);
                layout.rooms[shopIndex].roomType = RoomType.Shop;
                layout.rooms[shopIndex].visualVariant = RoomVisualVariant.Shop;
            }
            var finalRoom = layout.rooms[layout.rooms.Count - 1];
            if (!AttachExit(layout, finalRoom))
                throw new InvalidOperationException("No fue posible conectar la sala del portal.");
            return layout;
        }

        bool IsFinalBossLevel => CurrentLevel == LevelsPerWorld && !kharosTestRun;

        DungeonLayout BuildFinalBossLayout(DungeonLayout layout)
        {
            const float arenaWidth = 72f;
            const float arenaHeight = 50f;
            var entrance = AddRoom(layout, Vector2Int.zero, RoomType.Entrance, true);
            entrance.hasCustomGeometry = true;
            entrance.customRoomSize = new Vector2(18f, 14f);
            entrance.worldPosition = Vector2.zero;

            var arena = AddRoom(layout, Vector2Int.right, RoomType.Boss, true);
            arena.visualVariant = RoomVisualVariant.BossArena;
            arena.hasCustomGeometry = true;
            arena.customRoomSize = new Vector2(arenaWidth, arenaHeight);
            // Keep a full bridge between spawn and the arena while placing Kharos
            // in the center of a much larger room.
            arena.worldPosition = new Vector2(9f + bridgeLength + arenaWidth * .5f, 0f);
            Connect(entrance, arena, RoomDirection.East);
            return layout;
        }

        bool ExtendMainPath(DungeonLayout layout, DungeonRoomLayout current, int remaining, int totalGameplayRooms)
        {
            if (remaining <= 0) return true;
            foreach (var direction in ShuffledDirections())
            {
                var position = current.gridPosition + ToVector(direction);
                if (roomLookup.ContainsKey(position)) continue;
                var roomProgress = totalGameplayRooms <= 1 ? 1f : (totalGameplayRooms - remaining) / (float)(totalGameplayRooms - 1);
                var type = remaining == 1 ? (HasMiniBossThisLevel ? RoomType.Boss : RoomType.DifficultCombat) : RollCombatRoomType(roomProgress);
                var next = AddRoom(layout, position, type, true);
                Connect(current, next, direction);
                if (ExtendMainPath(layout, next, remaining - 1, totalGameplayRooms)) return true;
                Disconnect(current, next, direction);
                roomLookup.Remove(position); layout.rooms.Remove(next);
            }
            return false;
        }

        RoomType RollCombatRoomType(float progress)
        {
            // El primer mundo enseña el combate con encuentros normales. Los
            // elites y composiciones exigentes comienzan realmente en mundo 2.
            if (CurrentWorld <= 1) return RoomType.Combat;
            if (progress < .3f) return RoomType.Combat;
            var roll = random.Next(100);
            if (progress < .45f) return roll < 72 ? RoomType.Combat : roll < 94 ? RoomType.DifficultCombat : RoomType.Elite;
            if (roll < 45) return RoomType.Combat;
            if (roll < 78) return RoomType.DifficultCombat;
            return RoomType.Elite;
        }

        bool AttachExit(DungeonLayout layout, DungeonRoomLayout boss)
        {
            foreach (var direction in ShuffledDirections())
            {
                var position = boss.gridPosition + ToVector(direction);
                if (roomLookup.ContainsKey(position)) continue;
                var exit = AddRoom(layout, position, RoomType.Exit, true);
                Connect(boss, exit, direction);
                return true;
            }
            return false;
        }

        void AddBranch(DungeonLayout layout, RoomType type)
        {
            var candidates = new List<(DungeonRoomLayout room, RoomDirection direction)>();
            foreach (var room in layout.rooms)
            {
                if (room.roomType == RoomType.Boss) continue;
                foreach (var direction in cardinalDirections)
                    if (!roomLookup.ContainsKey(room.gridPosition + ToVector(direction))) candidates.Add((room, direction));
            }
            if (candidates.Count == 0) throw new InvalidOperationException("No hay espacio para una rama conectada.");
            var selected = candidates[random.Next(candidates.Count)];
            var node = AddRoom(layout, selected.room.gridPosition + ToVector(selected.direction), type, false);
            Connect(selected.room, node, selected.direction);
        }

        readonly RoomDirection[] cardinalDirections = { RoomDirection.North, RoomDirection.South, RoomDirection.East, RoomDirection.West };

        DungeonRoomLayout AddRoom(DungeonLayout layout, Vector2Int position, RoomType type, bool mainPath)
        {
            var room = new DungeonRoomLayout { gridPosition = position, roomType = type, visualVariant = VariantFor(type), mainPath = mainPath };
            layout.rooms.Add(room); roomLookup.Add(position, room); return room;
        }

        void Connect(DungeonRoomLayout first, DungeonRoomLayout second, RoomDirection direction)
        {
            if (!first.connections.Contains(direction)) first.connections.Add(direction);
            var opposite = Opposite(direction);
            if (!second.connections.Contains(opposite)) second.connections.Add(opposite);
        }

        void Disconnect(DungeonRoomLayout first, DungeonRoomLayout second, RoomDirection direction)
        {
            first.connections.Remove(direction); second.connections.Remove(Opposite(direction));
        }

        public bool ValidateCurrentLayout(out string reason)
        {
            reason = "";
            if (Layout == null || Layout.rooms.Count == 0) { reason = "layout vacío"; return false; }
            var entrance = Layout.rooms.Find(room => room.roomType == RoomType.Entrance);
            var exit = Layout.rooms.Find(room => room.roomType == RoomType.Exit);
            var boss = Layout.rooms.Find(room => room.roomType == RoomType.Boss);
            if (entrance == null || (!IsFinalBossLevel && exit == null)) { reason = "falta entrada o portal"; return false; }
            if (IsFinalBossLevel && (boss == null || Layout.rooms.Count != 2)) { reason = "el nivel de jefe debe tener solo entrada y arena"; return false; }
            var visited = new HashSet<Vector2Int> { entrance.gridPosition };
            var queue = new Queue<DungeonRoomLayout>(); queue.Enqueue(entrance);
            while (queue.Count > 0)
            {
                var room = queue.Dequeue();
                foreach (var direction in room.connections)
                {
                    var neighbour = Layout.FindRoom(room.gridPosition + ToVector(direction));
                    if (neighbour == null) { reason = "puerta hacia el vacío en " + room.gridPosition; return false; }
                    if (!neighbour.connections.Contains(Opposite(direction))) { reason = "conexión sin retorno"; return false; }
                    if (visited.Add(neighbour.gridPosition)) queue.Enqueue(neighbour);
                }
            }
            if (visited.Count != Layout.rooms.Count) { reason = "hay habitaciones aisladas"; return false; }
            if (HasMiniBossThisLevel && (boss == null || !visited.Contains(boss.gridPosition))) { reason = "mini-boss inaccesible"; return false; }
            if (exit != null && !visited.Contains(exit.gridPosition)) { reason = "portal inaccesible"; return false; }
            return true;
        }

        void BuildPlayableDungeon()
        {
            generatedRoot = new GameObject("GeneratedDungeon_" + currentSeed);
            foreach (var layoutRoom in Layout.rooms) BuildRoom(layoutRoom);
            BuildBridges();
            PlacePlayerAtEntrance();
        }

        void BuildRoom(DungeonRoomLayout layoutRoom)
        {
            var room = new GameObject();
            room.transform.SetParent(generatedRoot.transform);
            // Cada eje usa su dimensión real. Usar 12 también en Y dejaba un vacío
            // de dos unidades entre habitaciones de diez unidades de alto.
            room.transform.position = RoomWorldPosition(layoutRoom);
            var node = room.AddComponent<DungeonRoomNode>(); node.Configure(layoutRoom);
            var actualSize = RoomSizeFor(layoutRoom);
            CreateFloor(room.transform, layoutRoom, actualSize);
            var roomDoors = CreateWallsAndDoors(room.transform, layoutRoom, actualSize);

            var bounds = room.AddComponent<BoxCollider2D>(); bounds.isTrigger = true; bounds.size = actualSize - Vector2.one * .65f;
            var controller = room.AddComponent<RoomController>();
            controller.data = CreateRuntimeRoomData(layoutRoom);
            controller.doors = roomDoors.ToArray();
            controller.disableSceneEnemiesUntilEntered = false;
            if (layoutRoom.roomType == RoomType.Entrance || layoutRoom.roomType == RoomType.Exit || layoutRoom.roomType == RoomType.Shop)
                controller.ForceSafeOpen();
            if (layoutRoom.roomType == RoomType.Treasure || layoutRoom.roomType == RoomType.Secret || controller.data.RequiresCombat)
                controller.roomReward = CreateReward(room.transform, ChestTypeFor(layoutRoom.roomType));
            if (layoutRoom.roomType == RoomType.Exit) CreatePortal(room.transform);
            else if (layoutRoom.roomType == RoomType.Boss && IsFinalBossLevel)
            {
                var bossPortal = CreatePortal(room.transform, new Vector2(actualSize.x * .36f, -actualSize.y * .2f));
                bossPortal.SetActive(false);
                controller.roomPortal = bossPortal;
            }
            if (layoutRoom.roomType == RoomType.Shop) room.AddComponent<ShopController>();
        }

        RoomData CreateRuntimeRoomData(DungeonRoomLayout layoutRoom)
        {
            var data = ScriptableObject.CreateInstance<RoomData>();
            runtimeRoomData.Add(data);
            var gameplayRooms = Mathf.Max(1, Layout.rooms.Count - 2);
            var gameplayIndex = Mathf.Clamp(Layout.rooms.IndexOf(layoutRoom) - 1, 0, gameplayRooms - 1);
            var progress = gameplayRooms <= 1 ? 1f : gameplayIndex / (float)(gameplayRooms - 1);
            data.displayName = layoutRoom.roomType + " Room";
            data.roomType = layoutRoom.roomType;
            data.visualVariant = layoutRoom.visualVariant;
            data.closeDoorsOnCombat = true;
            data.minimumSpawnInterval = .1f;
            data.maximumSpawnInterval = .7f;
            data.spawnTelegraphTime = .8f;
            data.timeBetweenWaves = 1.1f;
            data.difficultyProgress = progress;
            data.worldNumber = Mathf.Max(1, CurrentWorld);
            var worldStep = (CurrentWorld - 1) + (CurrentLevel - 1) * .18f;
            // El mundo marca el escalón principal; las salas sólo añaden una variación pequeña.
            var tutorial = worldStep == 0;
            data.enemyHealthMultiplier = .65f + worldStep * .16f + progress * .10f;
            data.enemyDamageMultiplier = .35f + worldStep * .04f + progress * .025f;
            data.enemySpeedMultiplier = Mathf.Min(1.15f, .76f + worldStep * .04f + progress * .025f);
            data.enemyCooldownMultiplier = Mathf.Max(.85f, 1.65f - worldStep * .10f - progress * .05f);
            data.spawnTelegraphTime = tutorial ? 1.2f : Mathf.Max(.8f, 1.1f - worldStep * .04f);
            data.minimumSpawnInterval = tutorial ? .45f : .25f;
            data.maximumSpawnInterval = tutorial ? .7f : .55f;
            data.timeBetweenWaves = tutorial ? 1.8f : 1.5f;
            // Introduce richer patterns from world 2, then add a projectile or
            // summon to those patterns as each world advances.
            data.patternBonus = CurrentWorld <= 1 ? 0 : Mathf.Clamp(1 + Mathf.FloorToInt((worldStep - 1f) / 1.5f), 1, 3);
            data.eliteMixChance = tutorial ? 0f : Mathf.Min(.22f, worldStep * .025f);
            var runPowers = FindAnyObjectByType<PowerUpRuntime>();
            if (!tutorial && runPowers) data.eliteMixChance = Mathf.Clamp01(data.eliteMixChance + runPowers.ExtraEliteChance);

            if (layoutRoom.roomType == RoomType.Boss)
            {
                data.minWaves = data.maxWaves = 1;
                data.minEnemiesPerWave = data.maxEnemiesPerWave = 1;
                data.enemyHealthMultiplier *= tutorial ? 1.15f : 1.65f;
            }
            else if (layoutRoom.roomType == RoomType.Elite)
            {
                data.minWaves = data.maxWaves = 1;
                data.minEnemiesPerWave = 1 + Mathf.Min(2, Mathf.FloorToInt(worldStep / 3f));
                data.maxEnemiesPerWave = data.minEnemiesPerWave + (worldStep >= 2 ? 1 : 0);
            }
            else
            {
                data.minWaves = 1;
                data.maxWaves = tutorial ? 1 : worldStep >= 2 ? 3 : 2;
                var roomBonus = progress >= .65f ? 1 : 0;
                var encounterStep = Mathf.FloorToInt(worldStep);
                data.minEnemiesPerWave = Mathf.Min(9, 2 + encounterStep + roomBonus);
                data.maxEnemiesPerWave = Mathf.Min(10, 3 + encounterStep + roomBonus);
                if (!tutorial && (layoutRoom.roomType == RoomType.DifficultCombat || layoutRoom.roomType == RoomType.Challenge))
                {
                    data.minEnemiesPerWave = Mathf.Min(10, data.minEnemiesPerWave + 1);
                    data.maxEnemiesPerWave = Mathf.Min(12, data.maxEnemiesPerWave + 1);
                }
            }
            data.rewardChance = layoutRoom.roomType == RoomType.Entrance || layoutRoom.roomType == RoomType.Exit || layoutRoom.roomType == RoomType.Shop
                ? 0f
                : layoutRoom.roomType == RoomType.Boss || layoutRoom.roomType == RoomType.Elite ? 1f : .65f;
            data.enemySpawns = CreateEnemySpawns(layoutRoom.roomType, progress);
            if (kharosTestRun && layoutRoom.roomType == RoomType.Boss)
                data.enemySpawns = new[] { new RoomEnemySpawn { enemy = Resources.Load<EnemyData>("Enemy_Kharos"), count = 1 } };
            return data;
        }

        void PlacePlayerAtEntrance()
        {
            var player = ProjectLike.Online.CoopSession.IsHost ? ProjectLike.Online.CoopSession.Instance.HostPlayer : FindAnyObjectByType<PlayerController>();
            if (!player) return;
            var body = player.GetComponent<Rigidbody2D>();
            if (body)
            {
                body.position = Vector2.zero;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            player.transform.position = Vector3.zero;
            Physics2D.SyncTransforms();
            var follow = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow) follow.SetTarget(player.transform, true);
        }

        RoomEnemySpawn[] CreateEnemySpawns(RoomType type, float progress)
        {
            if (type != RoomType.Combat && type != RoomType.DifficultCombat && type != RoomType.Elite && type != RoomType.Challenge && type != RoomType.Boss) return Array.Empty<RoomEnemySpawn>();
            if (type == RoomType.Boss)
            {
                if (CurrentWorld == 1 && CurrentLevel == LevelsPerWorld)
                {
                    var kharos = Resources.Load<EnemyData>("Enemy_Kharos");
                    if (kharos) return new[] { new RoomEnemySpawn { enemy = kharos, count = 1, spread = .35f } };
                }
                var bosses = bossEnemies != null && bossEnemies.Length > 0 ? bossEnemies : eliteEnemies;
                if (bosses == null || bosses.Length == 0) return Array.Empty<RoomEnemySpawn>();
                var worldBosses = BossesForCurrentWorld(bosses);
                var selectedBoss = worldBosses[random.Next(worldBosses.Count)];
                return new[] { new RoomEnemySpawn { enemy = selectedBoss, count = 1, spread = .35f } };
            }

            var candidates = BuildProgressivePool(progress, type == RoomType.Elite);
            if (candidates.Count == 0) return Array.Empty<RoomEnemySpawn>();
            var amount = type == RoomType.Elite ? (progress > .7f ? 2 : 1) : progress < .2f ? 1 : progress < .5f ? 2 : progress < .78f ? 3 : 4;
            if (type == RoomType.DifficultCombat || type == RoomType.Challenge) amount = Mathf.Min(4, amount + 1);
            var spawns = new RoomEnemySpawn[amount];
            for (var i = 0; i < amount; i++)
            {
                EnemyData selected;
                if (i == 0) selected = PickByRole(candidates, EnemyBehaviour.Chaser);
                else if (i == 1) selected = PickByRole(candidates, EnemyBehaviour.Ranged, EnemyBehaviour.Turret, EnemyBehaviour.Evasive);
                else if (i == 2) selected = PickByRole(candidates, EnemyBehaviour.Charger, EnemyBehaviour.Support, EnemyBehaviour.Summoner, EnemyBehaviour.Teleport);
                else selected = candidates[random.Next(candidates.Count)];
                if (!selected) selected = candidates[random.Next(candidates.Count)];
                spawns[i] = new RoomEnemySpawn { enemy = selected, localPosition = new Vector2(random.Next(-3, 4), random.Next(-2, 3)), count = 1, spread = .35f };
            }
            return spawns;
        }

        List<EnemyData> BuildProgressivePool(float progress, bool forceElite)
        {
            var result = new List<EnemyData>();
            var source = forceElite && eliteEnemies != null && eliteEnemies.Length > 0 ? eliteEnemies : commonEnemies;
            var allowedTier = AllowedEnemyComplexity(progress, forceElite);
            var assignedWorld = new List<EnemyData>();
            if (source != null)
                foreach (var enemy in source)
                {
                    if (!enemy) continue;
                    if (IsBossTemplate(enemy)) continue;
                    if (EnemyHomeWorld(enemy) == CurrentWorld) assignedWorld.Add(enemy);
                }
            if (assignedWorld.Count > 0)
                foreach (var enemy in assignedWorld)
                {
                    if (EnemyComplexity(enemy) > allowedTier) continue;
                    if (progress < .5f && enemy.maxHealth > 145f) continue;
                    result.Add(enemy);
                }
            // Si faltan enemigos para la familia de este mundo, reutiliza el tier
            // adecuado. Así el sistema sigue funcionando mientras crece el roster.
            if (result.Count == 0 && source != null)
            {
                foreach (var enemy in source)
                    if (enemy && !IsBossTemplate(enemy) && EnemyComplexity(enemy) <= allowedTier) result.Add(enemy);
            }
            if (result.Count == 0 && source != null)
            {
                var minimumTier = int.MaxValue;
                foreach (var enemy in source) if (enemy && !IsBossTemplate(enemy)) minimumTier = Mathf.Min(minimumTier, EnemyComplexity(enemy));
                foreach (var enemy in source) if (enemy && !IsBossTemplate(enemy) && EnemyComplexity(enemy) == minimumTier) result.Add(enemy);
            }
            return result;
        }

        int AllowedEnemyComplexity(float progress, bool forceElite)
        {
            if (CurrentWorld <= 1) return 0;
            if (CurrentWorld == 2) return 1;
            if (CurrentWorld == 3) return 2;
            return 3;
        }

        int EnemyHomeWorld(EnemyData enemy)
        {
            var tier = EnemyComplexity(enemy);
            if (tier <= 0) return 1;
            if (tier == 1) return 2;
            if (tier == 2) return 3;
            return 4 + PositiveHash(enemy.enemyName) % 2;
        }

        public EnemyData ResolveSummonForCurrentWorld(EnemyData requested)
        {
            if (requested && !IsBossTemplate(requested) && EnemyHomeWorld(requested) == CurrentWorld) return requested;
            var pool = BuildProgressivePool(1f, false);
            var safe = new List<EnemyData>();
            foreach (var enemy in pool) if (enemy && !HasAny(enemy, EnemyBehaviour.Summoner)) safe.Add(enemy);
            if (safe.Count > 0) return safe[random.Next(safe.Count)];
            return pool.Count > 0 ? pool[random.Next(pool.Count)] : requested;
        }

        List<EnemyData> BossesForCurrentWorld(EnemyData[] source)
        {
            var ordered = new List<EnemyData>();
            foreach (var enemy in source) if (enemy && !ordered.Contains(enemy)) ordered.Add(enemy);
            ordered.Sort((a, b) =>
            {
                var complexity = EnemyComplexity(a).CompareTo(EnemyComplexity(b));
                if (complexity != 0) return complexity;
                var health = a.maxHealth.CompareTo(b.maxHealth);
                return health != 0 ? health : string.CompareOrdinal(a.enemyName, b.enemyName);
            });
            var result = new List<EnemyData>();
            for (var i = 0; i < ordered.Count; i++)
            {
                var homeWorld = Mathf.Min(MaximumWorld, Mathf.FloorToInt(i * MaximumWorld / (float)ordered.Count) + 1);
                if (homeWorld == CurrentWorld) result.Add(ordered[i]);
            }
            if (result.Count == 0 && ordered.Count > 0) result.Add(ordered[(CurrentWorld - 1) % ordered.Count]);
            return result;
        }

        bool IsBossTemplate(EnemyData enemy)
        {
            if (!enemy || bossEnemies == null) return false;
            foreach (var boss in bossEnemies) if (boss == enemy) return true;
            return false;
        }

        static int PositiveHash(string value)
        {
            unchecked
            {
                var hash = 17;
                if (!string.IsNullOrEmpty(value)) foreach (var character in value) hash = hash * 31 + character;
                return hash & int.MaxValue;
            }
        }

        static int EnemyComplexity(EnemyData enemy)
        {
            if (!enemy) return int.MaxValue;
            var roleCount = enemy.behaviours == null ? 0 : enemy.behaviours.Length;
            var behaviourTier = HasAny(enemy, EnemyBehaviour.Summoner, EnemyBehaviour.Teleport, EnemyBehaviour.Support) || roleCount >= 3 ? 3
                : HasAny(enemy, EnemyBehaviour.Charger, EnemyBehaviour.Turret) || roleCount >= 2 || enemy.projectileCount >= 3 ? 2
                : HasAny(enemy, EnemyBehaviour.Ranged, EnemyBehaviour.Evasive) ? 1 : 0;
            var attackTier = enemy.attackStyle == EnemyAttackStyle.ArcaneBurst || enemy.attackStyle == EnemyAttackStyle.BlinkStrike ? 3
                : enemy.attackStyle == EnemyAttackStyle.FireFan || enemy.attackStyle == EnemyAttackStyle.ToxicVolley || enemy.attackStyle == EnemyAttackStyle.BoneLance ? 2
                : enemy.attackStyle == EnemyAttackStyle.TripleClaw || enemy.attackStyle == EnemyAttackStyle.HeavySmash || enemy.attackStyle == EnemyAttackStyle.Pounce ? 1 : 0;
            return Mathf.Max(behaviourTier, attackTier);
        }

        EnemyData PickByRole(List<EnemyData> pool, params EnemyBehaviour[] roles)
        {
            var matches = new List<EnemyData>();
            foreach (var enemy in pool) if (HasAny(enemy, roles)) matches.Add(enemy);
            return matches.Count > 0 ? matches[random.Next(matches.Count)] : pool[random.Next(pool.Count)];
        }

        static bool HasAny(EnemyData enemy, params EnemyBehaviour[] roles)
        {
            if (!enemy || enemy.behaviours == null) return false;
            foreach (var behaviour in enemy.behaviours) foreach (var role in roles) if (behaviour == role) return true;
            return false;
        }

        List<RoomDoor> CreateWallsAndDoors(Transform parent, DungeonRoomLayout room, Vector2 size)
        {
            var doors = new List<RoomDoor>();
            CreateHorizontalEdge(parent, room, RoomDirection.North, size.y * .5f, size, doors);
            CreateHorizontalEdge(parent, room, RoomDirection.South, -size.y * .5f, size, doors);
            CreateVerticalEdge(parent, room, RoomDirection.East, size.x * .5f, size, doors);
            CreateVerticalEdge(parent, room, RoomDirection.West, -size.x * .5f, size, doors);
            return doors;
        }

        void CreateHorizontalEdge(Transform parent, DungeonRoomLayout room, RoomDirection direction, float y, Vector2 size, List<RoomDoor> doors)
        {
            if (!room.connections.Contains(direction)) { CreateWall(parent, new Vector2(0, y), new Vector2(size.x, .38f)); return; }
            var opening = Mathf.Clamp(bridgeWidth, 1.8f, size.x - 2f);
            var half = (size.x - opening) * .5f;
            CreateWall(parent, new Vector2(-(half + opening) * .5f, y), new Vector2(half, .38f));
            CreateWall(parent, new Vector2((half + opening) * .5f, y), new Vector2(half, .38f));
            doors.Add(CreateDoor(parent, direction, new Vector2(0, y), new Vector2(opening, .5f)));
        }

        void CreateVerticalEdge(Transform parent, DungeonRoomLayout room, RoomDirection direction, float x, Vector2 size, List<RoomDoor> doors)
        {
            if (!room.connections.Contains(direction)) { CreateWall(parent, new Vector2(x, 0), new Vector2(.38f, size.y)); return; }
            var opening = Mathf.Clamp(bridgeWidth, 1.8f, size.y - 2f);
            var half = (size.y - opening) * .5f;
            CreateWall(parent, new Vector2(x, -(half + opening) * .5f), new Vector2(.38f, half));
            CreateWall(parent, new Vector2(x, (half + opening) * .5f), new Vector2(.38f, half));
            doors.Add(CreateDoor(parent, direction, new Vector2(x, 0), new Vector2(.5f, opening)));
        }

        void BuildBridges()
        {
            foreach (var room in Layout.rooms)
            {
                // Norte y este construyen cada conexion una unica vez.
                if (room.connections.Contains(RoomDirection.North)) CreateBridge(room, RoomDirection.North);
                if (room.connections.Contains(RoomDirection.East)) CreateBridge(room, RoomDirection.East);
            }
        }

        void CreateBridge(DungeonRoomLayout room, RoomDirection direction)
        {
            var neighbour = Layout.FindRoom(room.gridPosition + ToVector(direction));
            if (neighbour == null) return;
            var first = RoomWorldPosition(room);
            var second = RoomWorldPosition(neighbour);
            var bridge = new GameObject("Bridge_" + room.gridPosition + "_" + direction);
            bridge.transform.SetParent(generatedRoot.transform, false);
            bridge.transform.position = (first + second) * .5f;

            var horizontal = direction == RoomDirection.East || direction == RoomDirection.West;
            var gap = horizontal
                ? Mathf.Abs(second.x - first.x) - (RoomSizeFor(room).x + RoomSizeFor(neighbour).x) * .5f
                : Mathf.Abs(second.y - first.y) - (RoomSizeFor(room).y + RoomSizeFor(neighbour).y) * .5f;
            gap = Mathf.Max(.5f, gap);
            var floorSize = horizontal
                ? new Vector2(gap + .5f, bridgeWidth)
                : new Vector2(bridgeWidth, gap + .5f);
            CreateSpriteObject("BridgeFloor", bridge.transform, Vector2.zero, floorSize, new Color(.14f, .17f, .22f), -4);

            if (horizontal)
            {
                CreateBridgeRail(bridge.transform, new Vector2(0f, bridgeWidth * .5f), new Vector2(gap + .55f, bridgeRailThickness));
                CreateBridgeRail(bridge.transform, new Vector2(0f, -bridgeWidth * .5f), new Vector2(gap + .55f, bridgeRailThickness));
            }
            else
            {
                CreateBridgeRail(bridge.transform, new Vector2(bridgeWidth * .5f, 0f), new Vector2(bridgeRailThickness, gap + .55f));
                CreateBridgeRail(bridge.transform, new Vector2(-bridgeWidth * .5f, 0f), new Vector2(bridgeRailThickness, gap + .55f));
            }
        }

        void CreateBridgeRail(Transform parent, Vector2 position, Vector2 size)
        {
            var rail = CreateSpriteObject("BridgeRail", parent, position, size, new Color(.27f, .31f, .39f), 3);
            rail.AddComponent<BoxCollider2D>();
        }

        Vector3 RoomWorldPosition(DungeonRoomLayout room)
        {
            if (room.hasCustomGeometry) return new Vector3(room.worldPosition.x, room.worldPosition.y, 0f);
            return new Vector3(
                room.gridPosition.x * (roomSize.x + bridgeLength),
                room.gridPosition.y * (roomSize.y + bridgeLength),
                0f);
        }

        Vector2 RoomSizeFor(DungeonRoomLayout room) => room.hasCustomGeometry ? room.customRoomSize : roomSize;

        RoomDoor CreateDoor(Transform parent, RoomDirection direction, Vector2 position, Vector2 size)
        {
            var door = CreateSpriteObject("Door_" + direction, parent, position, size, new Color(.34f, .85f, .48f, 1f), 4);
            door.AddComponent<BoxCollider2D>();
            var roomDoor = door.AddComponent<RoomDoor>(); roomDoor.direction = direction;
            return roomDoor;
        }

        void CreateFloor(Transform parent, DungeonRoomLayout room, Vector2 size)
        {
            var color = room.roomType == RoomType.Boss ? new Color(.22f, .08f, .10f) : room.roomType == RoomType.Treasure ? new Color(.17f, .14f, .06f) : room.roomType == RoomType.Shop ? new Color(.16f, .12f, .21f) : new Color(.11f, .13f, .17f);
            CreateSpriteObject("Floor", parent, Vector2.zero, size, color, -5);
        }

        void CreateWall(Transform parent, Vector2 position, Vector2 size) => CreateSpriteObject("Wall", parent, position, size, new Color(.22f, .25f, .32f), 3).AddComponent<BoxCollider2D>();

        GameObject CreateSpriteObject(string objectName, Transform parent, Vector2 position, Vector2 size, Color color, int order)
        {
            var go = new GameObject(objectName); go.transform.SetParent(parent); go.transform.localPosition = position; go.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = GetSquareSprite(); renderer.color = color; renderer.sortingOrder = order;
            return go;
        }

        GameObject CreateReward(Transform parent, ChestType type)
        {
            var reward = CreateSpriteObject("RoomReward", parent, Vector2.zero, new Vector2(.65f, .65f), new Color(1f, .75f, .12f), 6);
            var collider = reward.AddComponent<CircleCollider2D>(); collider.isTrigger = true;
            var chest = reward.AddComponent<Chest>(); chest.Configure(type);
            reward.SetActive(false);
            return reward;
        }

        static ChestType ChestTypeFor(RoomType type) => type == RoomType.Boss ? ChestType.Boss : type == RoomType.Secret ? ChestType.Secret : type == RoomType.Challenge ? ChestType.Cursed : type == RoomType.DifficultCombat || type == RoomType.Elite || type == RoomType.Treasure ? ChestType.Rare : ChestType.Common;

        GameObject CreatePortal(Transform parent, Vector2 localPosition = default)
        {
            var portal = CreateSpriteObject("ExitPortal", parent, Vector2.zero, new Vector2(1.25f, 1.25f), new Color(.18f, .72f, 1f, .82f), 6);
            portal.transform.localPosition = localPosition;
            var collider = portal.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = .65f;
            portal.AddComponent<DungeonPortal>();
            return portal;
        }

        Sprite GetSquareSprite()
        {
            if (squareSprite) return squareSprite;
            var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixel(0, 0, Color.white); texture.Apply();
            squareSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            return squareSprite;
        }

        void DisableTrainingRoom()
        {
            foreach (var name in new[] { "Wall_North", "Wall_South", "Wall_East", "Wall_West", "Door", "Chest", "NPC", "Pickup_Health", "Room_TrainingCombat" })
            {
                var objectToDisable = GameObject.Find(name);
                if (objectToDisable) objectToDisable.SetActive(false);
            }
            foreach (var enemy in FindObjectsByType<EnemyBrain>()) enemy.gameObject.SetActive(false);
        }

        void ClearGeneratedDungeon()
        {
            // Los pickups se crean en la raíz de la escena, fuera del dungeon.
            foreach (var pickup in FindObjectsByType<LootPickup>(FindObjectsInactive.Include))
            {
                pickup.gameObject.SetActive(false);
                Destroy(pickup.gameObject);
            }
            MobDropPool.ClearActiveDrops();
            if (generatedRoot) { generatedRoot.SetActive(false); Destroy(generatedRoot); }
            generatedRoot = null;
            foreach (var data in runtimeRoomData) if (data) Destroy(data);
            runtimeRoomData.Clear();
        }

        List<RoomDirection> ShuffledDirections()
        {
            var directions = new List<RoomDirection>(cardinalDirections);
            for (var i = directions.Count - 1; i > 0; i--) { var j = random.Next(i + 1); (directions[i], directions[j]) = (directions[j], directions[i]); }
            return directions;
        }

        static int StableHash(string value)
        {
            unchecked { var hash = (int)2166136261; foreach (var character in value) { hash ^= character; hash *= 16777619; } return hash; }
        }
        static RoomDirection Opposite(RoomDirection direction) => direction == RoomDirection.North ? RoomDirection.South : direction == RoomDirection.South ? RoomDirection.North : direction == RoomDirection.East ? RoomDirection.West : RoomDirection.East;
        static Vector2Int ToVector(RoomDirection direction) => direction == RoomDirection.North ? Vector2Int.up : direction == RoomDirection.South ? Vector2Int.down : direction == RoomDirection.East ? Vector2Int.right : Vector2Int.left;
        static RoomVisualVariant VariantFor(RoomType type) => type == RoomType.Boss ? RoomVisualVariant.BossArena : type == RoomType.Exit ? RoomVisualVariant.Temple : type == RoomType.Shop ? RoomVisualVariant.Shop : type == RoomType.Secret ? RoomVisualVariant.Secret : type == RoomType.Event ? RoomVisualVariant.Temple : RoomVisualVariant.Dungeon;
    }
}
