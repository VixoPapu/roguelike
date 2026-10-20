using System;
using System.Collections.Generic;
using ProjectLike.Online;
using ProjectLike.Phase2;
using ProjectLike.Phase3;
using ProjectLike.Phase5;
using ProjectLike.Phase6;
using ProjectLike.Phase8;
using ProjectLike.Phase12;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectLike.Phase1
{
    /// <summary>Panel local de pruebas y administracion, accesible con F10.</summary>
    public sealed class AdminPanel : MonoBehaviour
    {
        enum Page { Inicio, Jugador, Poderes, Enemigos, Mundo }
        static AdminPanel instance;
        readonly string[] pageNames = { "INICIO", "JUGADOR", "PODERES", "ENEMIGOS", "MUNDO" };
        PowerUpData[] powers = Array.Empty<PowerUpData>();
        EnemyData[] enemyTypes = Array.Empty<EnemyData>();
        WeaponData[] weapons = Array.Empty<WeaponData>();
        Vector2 scroll;
        Rect window = new Rect(0, 0, 900, 620);
        Page page;
        bool isOpen, pausedByPanel, godMode;
        float previousTimeScale = 1f;
        string notice = "F10 abre o cierra este panel";
        float noticeUntil;
        GUIStyle titleStyle, subtitleStyle, buttonStyle, tabStyle, smallStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateRuntimePanel()
        {
            if (instance) return;
            var root = new GameObject("AdminPanel_Runtime");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<AdminPanel>();
        }

        void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this;
            powers = Resources.LoadAll<PowerUpData>("PowerUps");
            enemyTypes = Resources.LoadAll<EnemyData>("");
            weapons = Resources.LoadAll<WeaponData>("Weapons");
            Array.Sort(powers, (a, b) => string.Compare(a ? a.displayName : "", b ? b.displayName : "", StringComparison.OrdinalIgnoreCase));
            Array.Sort(enemyTypes, (a, b) => string.Compare(a ? a.enemyName : "", b ? b.enemyName : "", StringComparison.OrdinalIgnoreCase));
            Array.Sort(weapons, (a, b) => string.Compare(a ? a.weaponName : "", b ? b.weaponName : "", StringComparison.OrdinalIgnoreCase));
        }

        void Update()
        {
            if (noticeUntil > 0f && Time.unscaledTime >= noticeUntil) { notice = "Panel local · acciones administrativas"; noticeUntil = 0f; }
            if (Keyboard.current == null) return;
            if (isOpen && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (!Keyboard.current.f10Key.wasPressedThisFrame) return;
            if (isOpen) Close(); else Open();
        }

        void Open()
        {
            isOpen = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            pausedByPanel = true;
            window.x = Mathf.Max(12f, (Screen.width - window.width) * .5f);
            window.y = Mathf.Max(12f, (Screen.height - window.height) * .5f);
            RefreshNotice("PANEL DE ADMINISTRADOR · juego pausado");
        }

        void Close()
        {
            isOpen = false;
            if (pausedByPanel)
            {
                Time.timeScale = previousTimeScale;
                pausedByPanel = false;
            }
        }

        void OnGUI()
        {
            if (!isOpen) return;
            EnsureStyles();
            window = GUI.Window(71406321, window, DrawWindow, GUIContent.none);
        }

        void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(.88f, .75f, 1f) } };
            subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, normal = { textColor = new Color(.72f, .76f, .83f) } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fixedHeight = 34, wordWrap = true };
            tabStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fixedHeight = 38, fontStyle = FontStyle.Bold };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, normal = { textColor = new Color(.68f, .75f, .82f) } };
        }

        void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("ADMINISTRADOR", titleStyle, GUILayout.Height(38));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("REANUDAR  [F10]", buttonStyle, GUILayout.Width(174))) Close();
            GUILayout.EndHorizontal();
            DrawStatusLine();
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            for (var i = 0; i < pageNames.Length; i++)
                if (GUILayout.Toggle(page == (Page)i, pageNames[i], tabStyle)) page = (Page)i;
            GUILayout.EndHorizontal();
            GUILayout.Space(7);
            scroll = GUILayout.BeginScrollView(scroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            var canAct = !CoopSession.Active || CoopSession.IsHost;
            GUI.enabled = canAct;
            switch (page)
            {
                case Page.Inicio: DrawHome(); break;
                case Page.Jugador: DrawPlayer(); break;
                case Page.Poderes: DrawPowers(); break;
                case Page.Enemigos: DrawEnemies(); break;
                case Page.Mundo: DrawWorld(); break;
            }
            GUI.enabled = true;
            GUILayout.EndScrollView();
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUILayout.Label(notice, subtitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("F10 · ESC cierra", smallStyle, GUILayout.Width(140));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, window.width, 36));
        }

        void DrawStatusLine()
        {
            var player = FindPlayer();
            var stats = player ? player.GetComponent<PlayerStats>() : null;
            var generator = FindAnyObjectByType<DungeonGenerator>();
            var room = FindCurrentRoom(player);
            var line = player && stats
                ? $"HP {stats.CurrentHealth:0}/{stats.maxHealth:0}   Escudo {stats.CurrentShield:0}   Energia {stats.CurrentEnergy:0}/{stats.maxEnergy:0}   Monedas {stats.coins}"
                : "No hay jugador activo en la escena";
            if (generator) line += $"     Mundo {generator.CurrentWorld}-{generator.CurrentLevel}";
            if (room && room.data) line += $"     Sala {room.data.displayName} · {room.State}";
            GUILayout.Label(line, subtitleStyle, GUILayout.Height(23));
            if (CoopSession.Active && !CoopSession.IsHost)
                GUILayout.Label("Conectado como cliente: las acciones administrativas solo las ejecuta el anfitrion.", smallStyle);
        }

        void DrawHome()
        {
            GUILayout.Label("Control rapido de la partida", titleStyle);
            TwoButtons("Reanudar", "Cerrar el panel y continuar", Close, "Ralentizar x0.5", "Reduce la velocidad del juego", () => SetTimeScale(.5f));
            TwoButtons("Velocidad normal x1", "Restaura la velocidad normal", () => SetTimeScale(1f), "Acelerar x2", "Acelera combate y movimiento", () => SetTimeScale(2f));
            TwoButtons("Curar y rellenar todo", "Salud, escudo y energia al maximo", RefillPlayer, "+500 monedas / +500 XP", "Entrega recursos para probar tiendas", () => { var s = Stats(); if (s) { s.coins += 500; s.experience += 500; } });
            TwoButtons("Limpiar enemigos de sala", "Derrota a los enemigos de la sala actual", KillCurrentRoom, "Teletransportar al centro", "Mueve al jugador al centro de la sala actual", TeleportToCurrentRoom);
            GUILayout.Space(10);
            GUILayout.Label("Teclas: F10 abre/cierra · ESC cierra · el panel pausa el juego mientras esta abierto.", subtitleStyle);
        }

        void DrawPlayer()
        {
            GUILayout.Label("Jugador", titleStyle);
            var player = FindPlayer();
            var stats = player ? player.GetComponent<PlayerStats>() : null;
            if (!player || !stats) { GUILayout.Label("Inicia una partida para acceder a las herramientas del jugador.", subtitleStyle); return; }
            GUILayout.Label($"Daño {stats.damage:0.0} · Velocidad {stats.moveSpeed:0.0} · Defensa {stats.defense:0.0} · Critico {stats.criticalChance:P0}", subtitleStyle);
            TwoButtons("Curar y rellenar", "Restaura salud, escudo y energia", RefillPlayer, "+10 vida maxima", "Aumenta la vida maxima y cura esa cantidad", () => stats.ModifyMaxHealth(10));
            TwoButtons("+100 escudo", "Aumenta el escudo hasta el maximo actual", () => stats.AddShield(100), "+100 energia", "Rellena energia hasta el maximo actual", () => stats.RestoreEnergy(100));
            TwoButtons("+1000 monedas", "Añade monedas para probar compras", () => stats.coins += 1000, "+1000 experiencia", "Añade experiencia", () => stats.experience += 1000);
            TwoButtons("+5 daño", "Mejora el daño base durante esta run", () => stats.damage += 5, "+1 velocidad", "Mejora la velocidad durante esta run", () => stats.moveSpeed += 1);
            var nextGodMode = GUILayout.Toggle(godMode, " Modo invulnerable", GUI.skin.button, GUILayout.Height(34));
            if (nextGodMode != godMode) { godMode = nextGodMode; var controller = player.GetComponent<PlayerController>(); if (controller) controller.AdminGodMode = godMode; }
            TwoButtons("Ir al centro de sala", "Reposiciona al jugador en la sala actual", TeleportToCurrentRoom, "Matar jugador", "Prueba la pantalla de muerte", KillPlayerForTest);
        }

        void DrawPowers()
        {
            GUILayout.Label("Power-ups y equipo", titleStyle);
            var player = FindPlayer();
            if (!player) { GUILayout.Label("No hay jugador activo.", subtitleStyle); return; }
            TwoButtons("Añadir power-up aleatorio", "Elige uno que el jugador pueda adquirir", () => GrantRandomPower(player.gameObject), "Añadir todos los unicos", "Concede los power-ups unicos disponibles", () => GrantAllUnique(player.gameObject));
            TwoButtons("Añadir todas las armas", "Añade cada arma al inventario", () => { var inv = GetInventory(player.gameObject); foreach (var weapon in weapons) if (weapon && !inv.weapons.Contains(weapon)) inv.AddWeapon(weapon); }, "Dar 5 llaves", "Añade llaves al inventario", () => GetInventory(player.gameObject).AddKeys(5));
            GUILayout.Space(8);
            GUILayout.Label($"Power-ups disponibles: {powers.Length} · Armas: {weapons.Length}", subtitleStyle);
            foreach (var power in powers)
            {
                if (!power) continue;
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.BeginVertical();
                GUILayout.Label($"{power.displayName}  ·  {power.category} / {power.rarity}", GUI.skin.label);
                GUILayout.Label(power.effectDescription, smallStyle);
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUILayout.Label($"x{GetPowerCount(player.gameObject, power.effectId)}", smallStyle, GUILayout.Width(38));
                GUI.enabled = (!CoopSession.Active || CoopSession.IsHost) && GetPowerUps(player.gameObject).CanAcquire(power);
                if (GUILayout.Button("AÑADIR", buttonStyle, GUILayout.Width(104))) { GetPowerUps(player.gameObject).Add(power); RefreshNotice("Añadido: " + power.displayName); }
                GUI.enabled = !CoopSession.Active || CoopSession.IsHost;
                GUILayout.EndHorizontal();
            }
        }

        void DrawEnemies()
        {
            GUILayout.Label("Enemigos y jefe", titleStyle);
            TwoButtons("Derrotar enemigos de esta sala", "Abre la sala si no hay oleadas pendientes", KillCurrentRoom, "Derrotar a todos", "Elimina todos los enemigos encontrados", KillAllEnemies);
            TwoButtons("Curar todos los enemigos", "Restaura la vida de los enemigos activos", HealAllEnemies, "Teletransportar a sala jefe", "Busca y entra en la sala de jefe generada", EnterCurrentBossRoom);
            if (GUILayout.Button("GENERAR ARENA DE PRUEBA DE KHAROS (reinicia la run)", buttonStyle)) StartKharosTest();
            GUILayout.Space(10);
            GUILayout.Label("Generar enemigo cerca del jugador", subtitleStyle);
            foreach (var enemy in enemyTypes)
            {
                if (!enemy || enemy.kharosBoss) continue;
                if (GUILayout.Button("+  " + enemy.enemyName, buttonStyle)) SpawnEnemy(enemy);
            }
        }

        void DrawWorld()
        {
            GUILayout.Label("Mundo, salas y tiempo", titleStyle);
            var generator = FindAnyObjectByType<DungeonGenerator>();
            var player = FindPlayer();
            if (generator)
            {
                GUILayout.Label($"Mundo {generator.CurrentWorld} / {DungeonGenerator.MaximumWorld} · Nivel {generator.CurrentLevel} / {DungeonGenerator.LevelsPerWorld} · Seed {generator.CurrentSeed}", subtitleStyle);
                TwoButtons("Siguiente nivel", "Genera la siguiente etapa de la run", () => generator.AdvanceRun(player), "Reiniciar run", "Vuelve al mundo 1, nivel 1", () => generator.RestartRun(player));
                TwoButtons("Nueva semilla", "Regenera el nivel actual con otra semilla", () => generator.GenerateRun(Guid.NewGuid().ToString("N")), "Prueba de Kharos", "Genera el pasillo y arena de pruebas del jefe", () => generator.StartKharosTest(player));
            }
            else GUILayout.Label("No se encontro DungeonGenerator en la escena.", subtitleStyle);
            TwoButtons("Entrar a sala jefe", "Teletransporta e inicia su combate", EnterCurrentBossRoom, "Abrir todas las puertas", "Desbloquea las puertas de la escena", UnlockAllDoors);
            GUILayout.Space(8);
            GUILayout.Label("Control de tiempo", subtitleStyle);
            TwoButtons("x0.25", "Camara lenta", () => SetTimeScale(.25f), "x0.5", "Mitad de velocidad", () => SetTimeScale(.5f));
            TwoButtons("x1 normal", "Velocidad normal", () => SetTimeScale(1f), "x2", "Doble velocidad", () => SetTimeScale(2f));
        }

        void TwoButtons(string left, string leftHint, Action leftAction, string right, string rightHint, Action rightAction)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(left + "\n" + leftHint, buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(52))) leftAction?.Invoke();
            if (GUILayout.Button(right + "\n" + rightHint, buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(52))) rightAction?.Invoke();
            GUILayout.EndHorizontal();
        }

        void GrantRandomPower(GameObject player)
        {
            var candidates = new List<PowerUpData>(); var inventory = GetPowerUps(player);
            foreach (var power in powers) if (power && inventory.CanAcquire(power)) candidates.Add(power);
            if (candidates.Count == 0) { RefreshNotice("No hay power-ups disponibles para este jugador."); return; }
            var choice = candidates[UnityEngine.Random.Range(0, candidates.Count)]; inventory.Add(choice); RefreshNotice("Añadido: " + choice.displayName);
        }

        void GrantAllUnique(GameObject player)
        {
            var inventory = GetPowerUps(player); var added = 0;
            foreach (var power in powers) if (power && !power.IsStackable && inventory.CanAcquire(power)) { inventory.Add(power); added++; }
            RefreshNotice("Power-ups unicos añadidos: " + added);
        }

        void SpawnEnemy(EnemyData template)
        {
            var player = FindPlayer(); if (!player || !template) return;
            var room = FindCurrentRoom(player); var position = (Vector2)player.transform.position + UnityEngine.Random.insideUnitCircle.normalized * 3f;
            if (room) position = room.ClampInside(position, 1.2f);
            var instanceObject = new GameObject(template.enemyName); instanceObject.transform.position = position; instanceObject.AddComponent<SpriteRenderer>();
            var brain = instanceObject.AddComponent<EnemyBrain>(); brain.Configure(template, room);
            instanceObject.AddComponent<EnemySpriteAnimator>();
            if (room) room.RegisterEnemy(brain);
            RefreshNotice("Generado: " + template.enemyName);
        }

        void KillCurrentRoom()
        {
            var player = FindPlayer(); var room = FindCurrentRoom(player); if (!room) { RefreshNotice("No se encontro la sala actual."); return; }
            var enemies = FindObjectsByType<EnemyBrain>();
            foreach (var enemy in enemies) if (enemy && !enemy.IsDead && room.WorldBounds.Contains(enemy.transform.position)) DamageEnemy(enemy);
            room.EvaluateCombat(); RefreshNotice("Enemigos de la sala derrotados.");
        }

        void KillAllEnemies()
        {
            foreach (var enemy in FindObjectsByType<EnemyBrain>()) if (enemy && !enemy.IsDead) DamageEnemy(enemy);
            RefreshNotice("Enemigos derrotados.");
        }

        static void DamageEnemy(EnemyBrain enemy) => enemy.ReceiveHit(new CombatHit { damage = 100000f, direction = Vector2.zero, source = FindAnyObjectByType<PlayerController>()?.gameObject });
        static void HealAllEnemies() { foreach (var enemy in FindObjectsByType<EnemyBrain>()) if (enemy && !enemy.IsDead) enemy.Heal(enemy.RuntimeMaxHealth); }
        void RefillPlayer() { var stats = Stats(); if (stats) { stats.ResetHealth(); RefreshNotice("Salud, escudo y energia restaurados."); } }
        void KillPlayerForTest()
        {
            var player = FindPlayer(); if (!player) return;
            godMode = false; player.AdminGodMode = false;
            player.TakeDamage(100000f, player.transform.position - Vector3.up);
            RefreshNotice("Prueba de muerte activada.");
        }
        static PlayerController FindPlayer() => FindAnyObjectByType<PlayerController>();
        static PlayerStats Stats() { var player = FindPlayer(); return player ? player.GetComponent<PlayerStats>() : null; }
        static PlayerInventory GetInventory(GameObject player) { var inventory = player.GetComponent<PlayerInventory>(); return inventory ? inventory : player.AddComponent<PlayerInventory>(); }
        static PlayerPowerUps GetPowerUps(GameObject player) { var inventory = player.GetComponent<PlayerPowerUps>(); return inventory ? inventory : player.AddComponent<PlayerPowerUps>(); }
        static int GetPowerCount(GameObject player, string effectId) => GetPowerUps(player).Stacks(effectId);

        static RoomController FindCurrentRoom(PlayerController player)
        {
            if (!player) return null;
            RoomController best = null; var bestDistance = float.MaxValue;
            foreach (var room in FindObjectsByType<RoomController>())
            {
                if (!room || !room.WorldBounds.Contains(player.transform.position)) continue;
                var distance = ((Vector2)room.WorldBounds.center - (Vector2)player.transform.position).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = room; }
            }
            return best;
        }

        void TeleportToCurrentRoom()
        {
            var player = FindPlayer(); var room = FindCurrentRoom(player); if (!player || !room) return;
            Teleport(player, room.WorldBounds.center); RefreshNotice("Jugador movido al centro de la sala.");
        }

        void EnterCurrentBossRoom()
        {
            var player = FindPlayer(); if (!player) return;
            foreach (var room in FindObjectsByType<RoomController>())
                if (room && room.data && room.data.roomType == RoomType.Boss)
                {
                    Teleport(player, room.WorldBounds.center); room.Enter(player.gameObject); RefreshNotice("Teletransportado a la sala de jefe."); return;
                }
            RefreshNotice("Esta generacion no tiene sala de jefe.");
        }

        void StartKharosTest()
        {
            var generator = FindAnyObjectByType<DungeonGenerator>(); var player = FindPlayer();
            if (!generator || !player) { RefreshNotice("Se necesita una partida activa para iniciar la prueba."); return; }
            generator.StartKharosTest(player); RefreshNotice("Arena de pruebas de Kharos generada.");
        }

        static void Teleport(PlayerController player, Vector2 position)
        {
            if (!player) return;
            player.transform.position = position;
            var body = player.GetComponent<Rigidbody2D>(); if (body) { body.position = position; body.linearVelocity = Vector2.zero; }
        }

        static void UnlockAllDoors()
        {
            foreach (var door in FindObjectsByType<RoomDoor>()) if (door) door.SetLocked(false);
        }

        void SetTimeScale(float value)
        {
            Time.timeScale = Mathf.Clamp(value, .1f, 2f);
            if (isOpen) previousTimeScale = Time.timeScale;
            RefreshNotice("Velocidad del juego: x" + Time.timeScale.ToString("0.##"));
        }

        void RefreshNotice(string value) { notice = value; noticeUntil = Time.unscaledTime + 4f; }
        void OnDestroy() { if (instance == this) instance = null; if (pausedByPanel) Time.timeScale = previousTimeScale; }
    }
}
