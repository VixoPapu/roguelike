using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using ProjectLike.Phase1;
using ProjectLike.Phase2;
using ProjectLike.Phase5;
using ProjectLike.Phase6;
using ProjectLike.Phase10;
using ProjectLike.Phase11;
using ProjectLike.Phase12;
using ProjectLike.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectLike.Online
{
    [DefaultExecutionOrder(-1000)]
    public sealed class CoopSession : MonoBehaviour
    {
        public static CoopSession Instance { get; private set; }
        public static bool Active => Instance && Instance.connected;
        public static bool IsHost => Active && Instance.host;
        public static bool IsClient => Active && !Instance.host;
        public static bool Playing => Active && Instance.playing;
        public const int Port = 7777;
        public int LocalId { get; private set; } = 1;
        public string Status { get; private set; } = "Crea una sala o introduce la IP del anfitrion.";
        public string RoomCode { get; private set; }
        public string Roster { get; private set; } = "";
        public string LocalAddresses { get; private set; } = "";
        public bool Busy { get; private set; }
        public bool RequiresReturn { get; private set; }
        public CoopWorld View { get; private set; }
        public bool CanStart => IsHost && !playing && peers.Exists(p => p.accepted);
        bool host, connected, playing, portal, gameOver, quitting;
        int nextId = 2, revision;
        float nextSnapshot, nextInput, heartbeat;
        TcpListener listener;
        DirectConnection server;
        readonly List<Peer> peers = new List<Peer>();
        readonly Dictionary<int, Member> members = new Dictionary<int, Member>();
        CoopSceneMirror mirror;
        CoopUI ui;
        CoopControls pendingInput;
        string nickname;
        bool previousRunInBackground;
        void Awake() { previousRunInBackground = Application.runInBackground; Application.runInBackground = true; }

        sealed class Peer
        {
            public DirectConnection connection;
            public int id;
            public bool accepted;
            public float lastHeard, lastInput;
            public int budget;
        }
        sealed class Member
        {
            public int id;
            public string name;
            public CoopPlayer actor;
            public ShopController shop;
            public List<PowerUpData> rewards;
            public bool selected;
            public int revision;
            public string message;
            public Member reviveTarget;
            public float reviveProgress;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;
        public static void OpenLobby()
        {
            if (Instance) return;
            var root = new GameObject("CoopSession");
            Instance = root.AddComponent<CoopSession>();
            DontDestroyOnLoad(root);
            Instance.mirror = root.AddComponent<CoopSceneMirror>();
            Instance.ui = root.AddComponent<CoopUI>();
            MainMenuController.DismissForOnline();
        }
        public void Host(string playerName, int port)
        {
            if (connected || Busy) return;
            try
            {
                listener = new TcpListener(IPAddress.Any, port); listener.Start(2);
                host = connected = true; LocalId = 1; nickname = CleanName(playerName);
                RoomCode = UnityEngine.Random.Range(100000, 1000000).ToString();
                members[1] = new Member { id = 1, name = nickname };
                LocalAddresses = string.Join(" / ", Dns.GetHostAddresses(Dns.GetHostName()).Where(a => a.AddressFamily == AddressFamily.InterNetwork).Select(a => a.ToString()));
                Status = "Sala abierta · TCP " + port + " · Codigo " + RoomCode;
                UpdateRoster();
            }
            catch (Exception e) { listener?.Stop(); listener = null; connected = false; Status = "No se pudo crear la sala: " + e.Message; }
        }
        public async void Join(string address, int port, string code, string playerName)
        {
            if (connected || Busy) return;
            Busy = true; Status = "Conectando..."; nickname = CleanName(playerName);
            var socket = new TcpClient();
            try
            {
                var connect = socket.ConnectAsync(address.Trim(), port);
                if (await Task.WhenAny(connect, Task.Delay(8000)) != connect) { socket.Close(); throw new TimeoutException("No responde el anfitrion. Revisa IP, puerto y firewall."); }
                await connect;
                if (!this || quitting) { socket.Close(); return; }
                server = new DirectConnection(socket); host = false; connected = true; heartbeat = Time.unscaledTime;
                Send(server, new CoopMessage { kind = "hello", text = nickname, code = code.Trim() });
                Status = "Esperando la aceptacion del anfitrion...";
            }
            catch (Exception e) { socket.Close(); if (this) Status = "No se pudo conectar: " + e.Message; }
            finally { if (this) Busy = false; }
        }
        static string CleanName(string value)
        {
            value = new string((value ?? "Jugador").Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '_').Take(20).ToArray()).Trim();
            return value.Length == 0 ? "Jugador" : value;
        }
        public static void Send(DirectConnection connection, CoopMessage message, bool snapshot = false) => connection?.Send(JsonUtility.ToJson(message), snapshot);
        void Update()
        {
            if (!connected) return;
            if (host)
            {
                while (listener != null && listener.Pending())
                {
                    var client = listener.AcceptTcpClient();
                    if (playing || peers.Count >= 2) { client.Close(); continue; }
                    peers.Add(new Peer { connection = new DirectConnection(client), id = nextId++, lastHeard = Time.unscaledTime });
                }
                foreach (var peer in peers.ToArray())
                {
                    peer.budget = 0;
                    while (peer.budget++ < 128 && peer.connection.TryReceive(out var json))
                    {
                        try { HandlePeer(peer, JsonUtility.FromJson<CoopMessage>(json)); }
                        catch (Exception e) { Debug.LogWarning("Paquete coop rechazado: " + e.Message); peer.connection.Dispose(); break; }
                    }
                    if (!peer.connection.Connected || Time.unscaledTime - peer.lastHeard > (peer.accepted ? 15f : 8f)) RemovePeer(peer);
                    else if (playing && Time.unscaledTime - peer.lastInput > .4f && members.TryGetValue(peer.id, out var stalled) && stalled.actor) stalled.actor.controls = default;
                }
                if (playing && members.TryGetValue(1, out var local) && local.actor) local.actor.controls = ui.BlocksControls ? default : CoopInput.Capture(local.actor.transform.position);
                if (playing && !portal)
                {
                    UpdateRevives();
                    gameOver = members.Values.All(m => !m.actor || m.actor.GetComponent<PlayerStats>().CurrentHealth <= 0f);
                    if (gameOver) Time.timeScale = 0f;
                }
            }
            else
            {
                int budget = 0;
                while (server != null && budget++ < 256 && server.TryReceive(out var json))
                {
                    try { HandleServer(JsonUtility.FromJson<CoopMessage>(json)); heartbeat = Time.unscaledTime; }
                    catch (Exception e) { Debug.LogWarning("Estado coop rechazado: " + e.Message); Status = "La partida recibida es incompatible"; server.Dispose(); }
                }
                if (server == null || !server.Connected || Time.unscaledTime - heartbeat > 20f)
                { Status = server?.Error ?? "Se perdio la conexion con el anfitrion"; RequiresReturn = playing; server?.Dispose(); connected = playing = false; Time.timeScale = 0f; return; }
                if (playing)
                {
                    var actor = View?.players?.FirstOrDefault(p => p.id == LocalId);
                    pendingInput.Merge(ui.BlocksControls ? default : CoopInput.Capture(actor != null ? actor.position : Vector3.zero));
                    if (Time.unscaledTime >= nextInput)
                    {
                        nextInput = Time.unscaledTime + 1f / 30f;
                        Send(server, new CoopMessage { kind = "input", input = pendingInput }); pendingInput.ClearEdges();
                    }
                }
                else if (Time.unscaledTime >= nextInput) { nextInput = Time.unscaledTime + 1f; Send(server, new CoopMessage { kind = "ping" }); }
            }
        }
        void LateUpdate()
        {
            if (!IsHost) return;
            if (!playing)
            {
                if (Time.unscaledTime >= nextSnapshot) { nextSnapshot = Time.unscaledTime + 1f; UpdateRoster(); }
                return;
            }
            if (Time.unscaledTime >= nextSnapshot)
            {
                nextSnapshot = Time.unscaledTime + .05f;
                var common = CaptureWorld();
                common.draws = mirror.Capture();
                foreach (var member in members.Values)
                {
                    common.menu = MenuFor(member);
                    common.hasMenu = common.menu != null;
                    if (member.id == 1) View = CopyView(common);
                    else
                    {
                        var peer = peers.Find(p => p.id == member.id && p.accepted);
                        if (peer != null) Send(peer.connection, new CoopMessage { kind = "state", world = common }, true);
                    }
                }
            }
            foreach (var member in members.Values) if (member.actor) member.actor.controls.ClearEdges();
        }
        // Host UI keeps its own menu reference; the shared render array is immutable this frame.
        static CoopWorld CopyView(CoopWorld world) => new CoopWorld { seed = world.seed, world = world.world, level = world.level, players = world.players, rooms = world.rooms, menu = world.menu, gameOver = world.gameOver, timeStopped = world.timeStopped };
        void HandlePeer(Peer peer, CoopMessage message)
        {
            if (message == null) throw new InvalidOperationException("Mensaje vacio");
            peer.lastHeard = Time.unscaledTime;
            if (!peer.accepted)
            {
                if (message.kind != "hello" || message.version != 1 || message.code != RoomCode || playing) { peer.connection.Dispose(); return; }
                peer.accepted = true;
                members[peer.id] = new Member { id = peer.id, name = CleanName(message.text) };
                Send(peer.connection, new CoopMessage { kind = "welcome", id = peer.id }); UpdateRoster(); return;
            }
            if (message.kind == "input" && playing && members.TryGetValue(peer.id, out var member) && member.actor)
            {
                var value = message.input;
                if (!Finite(value.move) || !Finite(value.aim)) throw new InvalidOperationException("Entrada invalida");
                value.move = Vector2.ClampMagnitude(value.move, 1f); value.aim = value.aim.sqrMagnitude > .001f ? value.aim.normalized : Vector2.right;
                value.pressed &= 127; value.held &= 127; value.released &= 127;
                member.actor.controls.Merge(value);
                peer.lastInput = Time.unscaledTime;
            }
            else if (message.kind == "command") Command(peer.id, message.text, message.index, message.revision);
        }
        static bool Finite(Vector2 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);
        void HandleServer(CoopMessage message)
        {
            if (message == null) return;
            switch (message.kind)
            {
                case "welcome": LocalId = message.id; Status = "Conectado. Esperando que el anfitrion inicie."; break;
                case "lobby": Roster = message.text; break;
                case "start": StartClient(); break;
                case "texture": mirror.ReceiveTexture(message.texture); break;
                case "geometry": mirror.ReceiveGeometry(message.geometry); break;
                case "sound": mirror.PlaySound(message.sound); break;
                case "uiSound": if (Enum.IsDefined(typeof(SfxCue), message.index)) GameSfx.Play((SfxCue)message.index, Vector2.zero, .7f, true); break;
                case "state": if (playing && message.world != null) { View = message.world; if (!View.hasMenu) View.menu = null; mirror.Apply(View); } break;
            }
        }
        void UpdateRoster()
        {
            Roster = string.Join("\n", members.Values.Select(m => (m.id == 1 ? "Anfitrion · " : "Jugador · ") + m.name)) + "\n" + members.Count + "/3 jugadores";
            Broadcast(new CoopMessage { kind = "lobby", text = Roster });
        }
        void RemovePeer(Peer peer)
        {
            peer.connection.Dispose(); peers.Remove(peer);
            if (members.TryGetValue(peer.id, out var member)) { if (member.actor) Destroy(member.actor.gameObject); members.Remove(peer.id); }
            UpdateRoster(); if (portal) TryAdvancePortal();
        }
        public void Broadcast(CoopMessage message)
        {
            foreach (var peer in peers) if (peer.accepted) Send(peer.connection, message);
        }
        public void StartMatch() { if (CanStart && !Busy) StartCoroutine(BeginMatch()); }
        IEnumerator BeginMatch()
        {
            Busy = true;
            var player = FindAnyObjectByType<PlayerController>();
            var generator = FindAnyObjectByType<DungeonGenerator>();
            if (!player || !generator) { Status = "No se encontro el jugador o el dungeon"; Busy = false; yield break; }
            var actor = player.GetComponent<CoopPlayer>() ?? player.gameObject.AddComponent<CoopPlayer>();
            actor.id = 1; actor.playerName = nickname; members[1].actor = actor;
            generator.RestartRun(player);
            yield return null; // Finish deferred destruction from ResetForNewRun before cloning.
            foreach (var member in members.Values)
            {
                if (member.id == 1) continue;
                var clone = Instantiate(player.gameObject, player.transform.position, player.transform.rotation);
                clone.name = "CoopPlayer_" + member.id;
                member.actor = clone.GetComponent<CoopPlayer>(); member.actor.id = member.id; member.actor.playerName = member.name;
                foreach (var camera in clone.GetComponentsInChildren<Camera>()) Destroy(camera);
                foreach (var audio in clone.GetComponentsInChildren<AudioListener>()) Destroy(audio);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var test = clone.GetComponent<TimeStopTestGrant>(); if (test) Destroy(test);
#endif
            }
            PlaceParty(true); IgnorePartyCollisions();
            foreach (var member in members.Values) AddNameplate(member);
            playing = true; Busy = false; Time.timeScale = 1f;
            Broadcast(new CoopMessage { kind = "start" });
        }
        void IgnorePartyCollisions()
        {
            foreach (var a in members.Values) foreach (var b in members.Values)
                if (a.id < b.id && a.actor && b.actor)
                    foreach (var ca in a.actor.GetComponentsInChildren<Collider2D>()) foreach (var cb in b.actor.GetComponentsInChildren<Collider2D>()) Physics2D.IgnoreCollision(ca, cb);
        }
        void AddNameplate(Member member)
        {
            if (!member.actor.GetComponent<CoopAllyStatus>()) member.actor.gameObject.AddComponent<CoopAllyStatus>();
            if (member.id == 1 && !member.actor.GetComponent<CoopDownedView>()) member.actor.gameObject.AddComponent<CoopDownedView>();
            var root = new GameObject("CoopName", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(member.actor.transform, false); root.transform.localPosition = Vector3.up * 1.05f; root.transform.localScale = Vector3.one * .02f;
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 150;
            var label = new GameObject("Name", typeof(RectTransform), typeof(UnityEngine.UI.Text)); label.transform.SetParent(root.transform, false);
            var text = label.GetComponent<UnityEngine.UI.Text>(); var hud = FindAnyObjectByType<PixelHUDCanvas>(); text.font = hud && hud.pixelFont ? hud.pixelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18; text.text = member.name; text.alignment = TextAnchor.MiddleCenter; text.rectTransform.sizeDelta = new Vector2(220, 26); text.raycastTarget = false;
            text.color = member.id == 1 ? new Color(.8f,.55f,1f) : member.id == 2 ? new Color(.3f,.9f,1f) : new Color(1f,.75f,.25f);
        }
        void StartClient()
        {
            if (playing) return;
            // The guest displays authoritative render state; it never simulates a second dungeon.
            foreach (var script in FindObjectsByType<MonoBehaviour>())
                if (script != this && script != ui && script != mirror && script.GetType().Namespace != null && script.GetType().Namespace.StartsWith("ProjectLike") && !(script is GameSfx)) script.enabled = false;
            foreach (var renderer in FindObjectsByType<Renderer>()) renderer.enabled = false;
            foreach (var canvas in FindObjectsByType<Canvas>()) if (!canvas.transform.IsChildOf(transform)) canvas.enabled = false;
            foreach (var body in FindObjectsByType<Rigidbody2D>()) body.simulated = false;
            foreach (var animator in FindObjectsByType<Animator>()) animator.enabled = false;
            playing = true; Time.timeScale = 0f;
            mirror.InitializeClient();
        }
        public Transform NearestPlayer(Vector3 position)
        {
            Transform nearest = null; float distance = float.PositiveInfinity;
            foreach (var member in members.Values)
            {
                if (!member.actor || member.actor.GetComponent<PlayerController>().IsDead || member.actor.GetComponent<PlayerController>().IsInvisible) continue;
                float next = (member.actor.transform.position - position).sqrMagnitude;
                if (next < distance) { distance = next; nearest = member.actor.transform; }
            }
            return nearest;
        }
        public PlayerController HostPlayer => members.TryGetValue(1, out var member) && member.actor ? member.actor.GetComponent<PlayerController>() : null;
        public void PlayerDowned(PlayerController controller)
        {
            if (!IsHost || !controller) return;
            var status = controller.GetComponent<CoopAllyStatus>();
            if (status) status.SetDowned(true);
            var member = controller.GetComponent<CoopPlayer>();
            if (member != null && members.TryGetValue(member.id, out var entry)) entry.message = "DERRIBADO";
        }
        Member NearestDowned(Member source)
        {
            if (source == null || !source.actor) return null;
            Member closest = null; float best = 1.5f * 1.5f;
            foreach (var candidate in members.Values)
            {
                if (candidate == source || !candidate.actor || candidate.actor.GetComponent<PlayerStats>().CurrentHealth > 0f) continue;
                float distance = (candidate.actor.transform.position - source.actor.transform.position).sqrMagnitude;
                if (distance < best) { best = distance; closest = candidate; }
            }
            return closest;
        }
        public bool IsReviveInteraction(GameObject actor)
        {
            if (!IsHost || !actor) return false;
            var player = actor.GetComponent<CoopPlayer>();
            return player != null && members.TryGetValue(player.id, out var member) && member.actor && member.actor.controls.Hold(CoopButtons.Interact) && NearestDowned(member) != null;
        }
        void UpdateRevives()
        {
            foreach (var reviver in members.Values)
            {
                if (!reviver.actor || reviver.actor.GetComponent<PlayerStats>().CurrentHealth <= 0f) { reviver.reviveTarget = null; reviver.reviveProgress = 0f; continue; }
                var target = reviver.actor.controls.Hold(CoopButtons.Interact) ? NearestDowned(reviver) : null;
                if (target == null) { if (reviver.reviveTarget != null) reviver.reviveTarget.actor.GetComponent<CoopAllyStatus>()?.SetRevive(0f, 0); reviver.reviveTarget = null; reviver.reviveProgress = 0f; continue; }
                if (reviver.reviveTarget != target) { reviver.reviveTarget = target; reviver.reviveProgress = 0f; }
                reviver.reviveProgress = Mathf.Min(1f, reviver.reviveProgress + Time.deltaTime / 2.25f);
                target.actor.GetComponent<CoopAllyStatus>()?.SetRevive(reviver.reviveProgress, reviver.id);
                if (reviver.reviveProgress < 1f) continue;
                var stats = target.actor.GetComponent<PlayerStats>(); stats.Revive(.35f);
                target.actor.GetComponent<PlayerController>().ReviveForCoop(false);
                target.actor.GetComponent<CoopAllyStatus>()?.SetDowned(false);
                target.message = "REVIVIDO"; reviver.message = "ALIADO REVIVIDO";
                GameSfx.Play(SfxCue.PowerUp, target.actor.transform.position, .72f);
                reviver.reviveTarget = null; reviver.reviveProgress = 0f;
            }
        }
        public void PartyEntered(RoomController room, GameObject first)
        {
            if (!IsHost) return;
            int offset = 0;
            foreach (var member in members.Values)
            {
                if (!member.actor) continue;
                var player = member.actor.gameObject;
                if (room.data != null && room.data.RequiresCombat && player != first && !room.WorldBounds.Contains(player.transform.position))
                {
                    var destination = room.ClampInside((Vector2)first.transform.position + Vector2.right * (++offset * .8f), 1.5f);
                    player.transform.position = destination; var body = player.GetComponent<Rigidbody2D>(); body.position = destination; body.linearVelocity = Vector2.zero;
                    member.shop = null; member.actor.menuOpen = false;
                }
                player.GetComponent<PowerUpRuntime>()?.OnRoomEntered(room);
            }
            Physics2D.SyncTransforms();
        }
        public void PartyCompleted(RoomController room)
        {
            foreach (var member in members.Values) if (member.actor) member.actor.GetComponent<PowerUpRuntime>()?.OnRoomCompleted(room);
        }
        public void EnterPortal(GameObject player)
        {
            if (!IsHost || !playing || portal || TimeStopAbility.IsActive || gameOver || !player.GetComponent<CoopPlayer>()) return;
            portal = true; Time.timeScale = 0f;
            var pool = Resources.LoadAll<PowerUpData>("PowerUps");
            var used = new HashSet<PowerUpData>();
            foreach (var member in members.Values)
            {
                member.shop = null; member.selected = false; member.rewards = new List<PowerUpData>(); member.revision = ++revision;
                member.actor.menuOpen = true;
                var powers = member.actor.GetComponent<PlayerPowerUps>() ?? member.actor.gameObject.AddComponent<PlayerPowerUps>();
                var eligible = pool.Where(p => p && powers.CanAcquire(p)).ToList();
                var unique = eligible.Where(p => !used.Contains(p)).ToList();
                if (unique.Count >= 3) eligible = unique;
                while (member.rewards.Count < 3 && eligible.Count > 0)
                {
                    int Weight(PowerUpData p) => (int)p.rarity == 0 ? 10 : (int)p.rarity == 1 ? 7 : (int)p.rarity == 2 ? 4 : (int)p.rarity == 3 ? 2 : 1;
                    int roll = UnityEngine.Random.Range(0, eligible.Sum(Weight));
                    var chosen = eligible[0]; foreach (var power in eligible) { roll -= Weight(power); if (roll < 0) { chosen = power; break; } }
                    member.rewards.Add(chosen); used.Add(chosen); eligible.Remove(chosen);
                }
                if (member.rewards.Count == 0) member.selected = true;
            }
            GameSfx.Play(SfxCue.Portal, player.transform.position, .82f);
            TryAdvancePortal();
        }
        void TryAdvancePortal()
        {
            if (!portal || members.Values.Any(m => !m.selected)) return;
            portal = false; Time.timeScale = 1f;
            var generator = FindAnyObjectByType<DungeonGenerator>();
            bool reset = generator.CurrentWorld == 5 && generator.CurrentLevel == 5;
            generator.AdvanceRun(members[1].actor.GetComponent<PlayerController>());
            PlaceParty(reset);
            foreach (var member in members.Values) { member.rewards = null; member.actor.menuOpen = false; member.message = ""; }
        }
        void PlaceParty(bool reset)
        {
            int offset = 0;
            foreach (var member in members.Values)
            {
                if (!member.actor) continue;
                var controller = member.actor.GetComponent<PlayerController>();
                if (reset) controller.RestartForNewRun();
                else controller.ReviveForCoop();
                var position = new Vector3((offset++ - 1) * .9f, 0f, 0f);
                member.actor.transform.position = position;
                var body = member.actor.GetComponent<Rigidbody2D>(); body.position = position; body.linearVelocity = Vector2.zero;
                member.actor.controls = default;
            }
            Physics2D.SyncTransforms();
            var follow = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow && HostPlayer) follow.SetTarget(HostPlayer.transform, true);
        }
        public void OpenShop(ShopController shop, GameObject player)
        {
            if (!IsHost || portal || gameOver || !shop || shop.IsLockedFor(player)) return;
            var actor = player.GetComponent<CoopPlayer>();
            if (!actor || !members.TryGetValue(actor.id, out var member)) return;
            shop.SelectCustomer(player); member.shop = shop; member.revision = ++revision; member.message = ""; actor.menuOpen = true;
        }
        public void LocalCommand(string command, int index = 0)
        {
            int current = View?.menu?.revision ?? 0;
            if (IsHost) Command(1, command, index, current);
            else Send(server, new CoopMessage { kind = "command", text = command, index = index, revision = current });
        }
        void Command(int id, string command, int index, int expectedRevision)
        {
            if (!playing || !members.TryGetValue(id, out var member)) return;
            if (command == "restart" && gameOver && id == 1)
            {
                foreach (var entry in members.Values) { entry.shop = null; entry.rewards = null; entry.actor.menuOpen = false; }
                FindAnyObjectByType<DungeonGenerator>().RestartRun(members[1].actor.GetComponent<PlayerController>());
                PlaceParty(true); gameOver = false; Time.timeScale = 1f; return;
            }
            if (member.revision != expectedRevision) return;
            if (command == "choose" && portal && !member.selected && member.rewards != null && index >= 0 && index < member.rewards.Count)
            {
                if (!member.actor.GetComponent<PlayerPowerUps>().Add(member.rewards[index])) return;
                PersonalSound(id, SfxCue.PowerUp);
                member.selected = true; member.revision = ++revision; TryAdvancePortal(); return;
            }
            if (!member.shop || portal) return;
            if (command == "close") { member.shop = null; member.actor.menuOpen = false; return; }
            var shop = member.shop; shop.SelectCustomer(member.actor.gameObject);
            bool success;
            if (command == "buy") success = shop.TryPurchase(index, member.actor.gameObject, out member.message);
            else if (command == "reroll") success = shop.TryReroll(member.actor.GetComponent<PlayerStats>(), out member.message);
            else return;
            PersonalSound(id, success ? command == "buy" ? SfxCue.ShopBuy : SfxCue.UiConfirm : SfxCue.UiDeny);
            member.revision = ++revision;
            if (shop.IsLockedFor(member.actor.gameObject)) { member.shop = null; member.actor.menuOpen = false; }
        }
        CoopMenu MenuFor(Member member)
        {
            if (portal) return new CoopMenu { title = "ELIGE UNA MEJORA", reward = true, waiting = member.selected, revision = member.revision, message = "Elegidos: " + members.Values.Count(m => m.selected) + "/" + members.Count,
                offers = member.rewards.Select(p => new CoopOffer { name = p.displayName, description = p.effectDescription, rarity = (int)p.rarity }).ToArray() };
            if (!member.shop) return null;
            var shop = member.shop; shop.SelectCustomer(member.actor.gameObject);
            return new CoopMenu { title = "TIENDA", revision = member.revision, message = member.message, purchases = shop.PurchasesFor(member.actor.gameObject), rerolls = shop.RerollsFor(member.actor.gameObject), rerollPrice = shop.GetRerollCost(member.actor.gameObject),
                offers = shop.CurrentItems.Select((p, i) => new CoopOffer { name = p.displayName, description = p.description, rarity = (int)p.rarity, price = shop.GetPrice(i, member.actor.gameObject), sold = shop.SoldItems[i] }).ToArray() };
        }
        void PersonalSound(int id, SfxCue cue)
        {
            if (id == 1) GameSfx.Play(cue, Vector2.zero, .7f, true);
            else { var peer = peers.Find(p => p.id == id); if (peer != null) Send(peer.connection, new CoopMessage { kind = "uiSound", index = (int)cue }); }
        }
        CoopWorld CaptureWorld()
        {
            var generator = FindAnyObjectByType<DungeonGenerator>();
            return new CoopWorld { seed = generator.CurrentSeed, world = generator.CurrentWorld, level = generator.CurrentLevel, gameOver = gameOver, timeStopped = TimeStopAbility.IsActive,
                background = Camera.main ? Camera.main.backgroundColor : Color.black,
                flashColor = FindObjectsByType<UnityEngine.UI.Image>().Where(i => i.name == "Flash" && i.GetComponentInParent<TimeStopAbility>()).Select(i => i.color).OrderByDescending(c => c.a).FirstOrDefault(),
                players = members.Values.Where(m => m.actor).Select(m =>
                {
                    var s = m.actor.GetComponent<PlayerStats>(); var w = m.actor.GetComponent<WeaponController>(); var c = m.actor.GetComponent<PlayerConsumables>(); var t = m.actor.GetComponent<TimeStopAbility>(); var powers = m.actor.GetComponent<PlayerPowerUps>();
                    var ally = m.actor.GetComponent<CoopAllyStatus>();
                    return new CoopActor { id = m.id, name = m.name, position = m.actor.transform.position, health = s.CurrentHealth, maxHealth = s.maxHealth, shield = s.CurrentShield, maxShield = s.maxShield, energy = s.CurrentEnergy, maxEnergy = s.maxEnergy, coins = s.coins, downed = ally && ally.Downed, reviveProgress = ally ? ally.ReviveProgress : 0f, reviving = ally && ally.ReviveProgress > 0f,
                        primary = w && w.PrimaryWeapon ? w.PrimaryWeapon.weaponName : "-", secondary = w && w.SecondaryWeapon ? w.SecondaryWeapon.weaponName : "-", slot = w ? w.ActiveSlot : 1,
                        consumable = c && c.Current ? c.Current.displayName + " x" + c.Count : "Sin consumibles", ability = t ? t.Remaining > 0 ? "Tiempo cero " + t.Remaining.ToString("0.0") : t.CooldownRemaining > 0 ? "Recarga " + t.CooldownRemaining.ToString("0") + "s" : "[F] Tiempo cero" : "", notice = m.message, comparison = Comparison(m.actor.gameObject, w ? w.EquippedWeapon : null), powers = powers ? powers.acquired.Where(p => p).Select(p => p.effectId).ToArray() : Array.Empty<string>() };
                }).ToArray(),
                rooms = FindObjectsByType<DungeonRoomNode>().Select(n => { var r = n.GetComponent<RoomController>(); return new CoopRoom { grid = n.gridPosition, type = (int)n.roomType, center = r ? r.WorldBounds.center : n.transform.position, size = r ? r.WorldBounds.size : Vector3.zero, state = r ? (int)r.State : 0, wave = r ? r.CurrentWave : 0, totalWaves = r ? r.TotalWaves : 0, connections = n.connections.Select(d => (int)d).ToArray() }; }).ToArray() };
        }
        static string Comparison(GameObject actor, WeaponData equipped)
        {
            var pickup = actor.GetComponent<PlayerInteraction>()?.FindTarget() as ProjectLike.Phase8.LootPickup;
            if (!pickup || !pickup.data || !pickup.data.weapon) return "";
            var weapon = pickup.data.weapon;
            string Stat(string label, float value, float old, bool lower = false)
            {
                bool equal = Mathf.Approximately(value, old);
                var color = equal ? "EEEEEE" : (lower ? value < old : value > old) ? "63EF8A" : "F65C5C";
                return label + " <color=#" + color + ">" + value.ToString("0.##") + "</color>";
            }
            var rarity = ColorUtility.ToHtmlStringRGB(ProjectLike.Phase8.LootRarityRules.Color(pickup.data.rarity));
            return "<color=#" + rarity + ">" + weapon.weaponName + "</color>\n" +
                Stat("Dano", weapon.damage, equipped ? equipped.damage : 0) + "    " + Stat("Cooldown", weapon.recovery, equipped ? equipped.recovery : 0, true) + "    " +
                Stat("Energia", weapon.energyCost, equipped ? equipped.energyCost : 0, true) + "    " + Stat("Empuje", weapon.knockback, equipped ? equipped.knockback : 0) + "\n" + weapon.description;
        }
        public static void EmitSound(AudioClip clip, Vector3 position, float volume, float pitch, float spatial)
        {
            if (!IsHost || !Playing || !clip) return;
            Instance.Broadcast(new CoopMessage { kind = "sound", sound = new CoopSound { clip = clip.name, position = position, volume = volume, pitch = pitch, spatial = spatial } });
        }
        public void Leave()
        {
            if (quitting) return; quitting = true;
            CloseConnections(); Time.timeScale = 1f; Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
            SceneManager.sceneLoaded += ReturnedToMenu;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            Destroy(gameObject);
        }
        static void ReturnedToMenu(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= ReturnedToMenu;
            if (!FindAnyObjectByType<DungeonGenerator>()) new GameObject("DungeonGenerator").AddComponent<DungeonGenerator>();
            MainMenuController.Show();
        }
        void CloseConnections() { connected = playing = false; listener?.Stop(); server?.Dispose(); foreach (var peer in peers) peer.connection.Dispose(); }
        void OnApplicationQuit() { quitting = true; CloseConnections(); }
        void OnDestroy() { CloseConnections(); Application.runInBackground = previousRunInBackground; if (Instance == this) Instance = null; }
    }
}
