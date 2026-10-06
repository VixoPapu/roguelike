using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProjectLike.Audio;
using ProjectLike.Phase1;
using ProjectLike.Phase5;
using ProjectLike.Phase6;
using ProjectLike.Phase8;
using ProjectLike.Phase10;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ProjectLike.Online
{
    public sealed class CoopUI : MonoBehaviour
    {
        Font font;
        RectTransform canvas, lobby, hud, menu, exit;
        InputField address, port, code, nickname;
        Text status, roster, addresses, coins, equipment, notice, allies, roomTitle, floor, menuMessage;
        Text[] values = new Text[3]; Image[] fills = new Image[3];
        Text[] warnings = new Text[3];
        Button create, join, start;
        string menuKey, mapSeed, lastRoomTitle;
        bool escapeOpen;
        int lastRevision = -1;
        float nextHud, titleSince;
        RectTransform graph;
        RectTransform comparisonPanel;
        Text comparison;
        Image timeFlash;
        RectTransform powersPanel;
        string shownPowers;
        readonly Dictionary<Vector2Int, Image> map = new Dictionary<Vector2Int, Image>();
        readonly HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        readonly List<CanvasGroup> cardGroups = new List<CanvasGroup>();
        public bool BlocksControls => escapeOpen;
        CoopSession Session => CoopSession.Instance;
        void Awake()
        {
            var existing = FindAnyObjectByType<PixelHUDCanvas>(); font = existing && existing.pixelFont ? existing.pixelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvas = Rect("OnlineUI", transform, Vector2.one * .5f, Vector2.zero, Vector2.zero);
            var c = canvas.gameObject.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 3500;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(960, 540); scaler.matchWidthOrHeight = .5f;
            if (!EventSystem.current) new GameObject("CoopEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            BuildLobby(); BuildHUD(); hud.gameObject.SetActive(false);
        }
        void BuildLobby()
        {
            lobby = Full("Lobby", canvas, Color.black);
            var logo = Rect("Logo", lobby, new Vector2(.5f, 1f), new Vector2(0f, -60f), new Vector2(370, 120));
            var image = logo.gameObject.AddComponent<Image>(); image.sprite = Resources.LoadAll<Sprite>("Images/Menu/imagen_2026-09-08_200152682-div6 (1)").FirstOrDefault(); image.preserveAspect = true;
            Label(lobby, "COOPERATIVO · HASTA 3 JUGADORES", new Vector2(.5f, 1f), new Vector2(0, -129), new Vector2(700, 30), 22);
            nickname = Field(lobby, "Nombre", new Vector2(-210, 87), PlayerPrefs.GetString("Coop.Name", "Jugador"));
            port = Field(lobby, "Puerto TCP", new Vector2(210, 87), CoopSession.Port.ToString()); port.contentType = InputField.ContentType.IntegerNumber;
            address = Field(lobby, "IP del anfitrion", new Vector2(-210, 26), PlayerPrefs.GetString("Coop.IP", "127.0.0.1"));
            code = Field(lobby, "Codigo de sala", new Vector2(210, 26), ""); code.contentType = InputField.ContentType.IntegerNumber;
            create = Button(lobby, "CREAR SALA", new Vector2(-210, -25), new Vector2(290, 36), () => { Save(); Session.Host(nickname.text, ParsedPort()); });
            join = Button(lobby, "UNIRSE", new Vector2(210, -25), new Vector2(290, 36), () => { Save(); Session.Join(address.text, ParsedPort(), code.text, nickname.text); });
            roster = Label(lobby, "", new Vector2(.5f, .5f), new Vector2(-210, -100), new Vector2(340, 100), 15);
            status = Label(lobby, "", new Vector2(.5f, .5f), new Vector2(210, -83), new Vector2(380, 80), 13);
            addresses = Label(lobby, "", new Vector2(.5f, .5f), new Vector2(210, -139), new Vector2(380, 38), 12);
            start = Button(lobby, "INICIAR PARTIDA", new Vector2(0, -186), new Vector2(320, 38), () => Session.StartMatch());
            Button(lobby, "VOLVER", new Vector2(0, -234), new Vector2(180, 30), () => Session.Leave());
        }
        int ParsedPort() => int.TryParse(port.text, out var number) && number > 0 && number < 65536 ? number : CoopSession.Port;
        void Save() { PlayerPrefs.SetString("Coop.Name", nickname.text); PlayerPrefs.SetString("Coop.IP", address.text); PlayerPrefs.Save(); }
        InputField Field(Transform parent, string title, Vector2 position, string value)
        {
            Label(parent, title, Vector2.one * .5f, position + Vector2.up * 22, new Vector2(320, 20), 13);
            var rect = Rect(title, parent, Vector2.one * .5f, position - Vector2.up * 4, new Vector2(330, 30)); Frame(rect);
            var field = rect.gameObject.AddComponent<InputField>(); field.targetGraphic = rect.GetComponent<Image>(); field.characterLimit = title == "Nombre" ? 20 : 64;
            var label = Label(rect, value, Vector2.one * .5f, Vector2.zero, new Vector2(310, 28), 15); label.supportRichText = false;
            field.textComponent = label; field.text = value;
            return field;
        }
        void BuildHUD()
        {
            hud = Full("GuestHUD", canvas, Color.clear); hud.GetComponent<Image>().raycastTarget = false;
            var stats = Rect("Vitals", hud, new Vector2(0, 1), new Vector2(111, -64), new Vector2(206, 112)); PixelHUDCanvas.DarkFrame(stats);
            Color[] colors = { new Color(.84f,.19f,.18f), new Color(.60f,.63f,.63f), new Color(.035f,.37f,.76f) };
            for (int i = 0; i < 3; i++)
            {
                var bar = Rect("Bar", stats, Vector2.one * .5f, new Vector2(0, 31 - i * 28), new Vector2(178, 26));
                bar.gameObject.AddComponent<Image>().color = new Color(.34f, .34f, .34f);
                var track = Rect("Track",bar,Vector2.one*.5f,Vector2.up,new Vector2(174,22));track.gameObject.AddComponent<Image>().color=new Color(.12f,.12f,.12f);
                var fill = Rect("Fill", track, new Vector2(0, .5f), Vector2.zero, new Vector2(174, 22)); fill.pivot = new Vector2(0, .5f);
                fills[i] = fill.gameObject.AddComponent<Image>(); fills[i].color = colors[i];
                var highlight=Rect("Highlight",fill,new Vector2(.5f,1),Vector2.down*2,new Vector2(174,4));highlight.anchorMin=new Vector2(0,1);highlight.anchorMax=Vector2.one;highlight.sizeDelta=new Vector2(0,4);highlight.gameObject.AddComponent<Image>().color=new Color(1,1,1,.16f);
                values[i] = Label(bar, "", Vector2.one * .5f, Vector2.up, new Vector2(178,26), 24);
                warnings[i] = Label(stats,"!",Vector2.one*.5f,new Vector2(119,31-i*28),new Vector2(15,28),26); warnings[i].color=colors[i];
            }
            coins = Label(hud, "", new Vector2(1,1), new Vector2(-90,-23), new Vector2(170,30), 23); coins.color = Color.yellow;
            equipment = Label(hud, "", new Vector2(1,0), new Vector2(-205,76), new Vector2(390,138), 15); equipment.alignment = TextAnchor.LowerRight;
            powersPanel=Rect("Powers",hud,new Vector2(0,1),new Vector2(112,-164),new Vector2(206,76));
            allies = Label(hud, "", new Vector2(0,1), new Vector2(160,-240), new Vector2(300,75), 12); allies.alignment = TextAnchor.UpperLeft;
            notice = Label(hud, "", new Vector2(.5f,.69f), Vector2.zero, new Vector2(640,60), 17);
            roomTitle = Label(hud, "", new Vector2(.5f,.76f), Vector2.zero, new Vector2(600,40), 24);
            var mapPanel = Rect("Minimap", hud, Vector2.one, new Vector2(-105,-128), new Vector2(190,160)); PixelHUDCanvas.DarkFrame(mapPanel);
            graph = Rect("Graph", mapPanel, Vector2.one * .5f, new Vector2(0,15), new Vector2(166,112));
            floor = Label(mapPanel, "", new Vector2(.5f,0), new Vector2(0,20), new Vector2(160,30), 22);
            comparisonPanel = Rect("Comparison", hud, new Vector2(.5f,0), new Vector2(0,58), new Vector2(670,88)); PixelHUDCanvas.DarkFrame(comparisonPanel);
            comparison = Label(comparisonPanel,"",Vector2.one*.5f,Vector2.zero,new Vector2(650,80),13);
            comparisonPanel.gameObject.SetActive(false);
            var flash = Full("SharedTimeFlash",hud,Color.clear); timeFlash = flash.GetComponent<Image>(); timeFlash.raycastTarget = false;
        }
        void Update()
        {
            if (!Session) return;
            bool playing = CoopSession.Playing;
            lobby.gameObject.SetActive(!playing);
            hud.gameObject.SetActive(playing && CoopSession.IsClient);
            if (!playing)
            {
                status.text = Session.Status; roster.text = Session.Roster;
                addresses.text = CoopSession.IsHost ? "IP local: " + Session.LocalAddresses : "Misma version del juego en todos los PCs";
                create.interactable = join.interactable = !CoopSession.Active && !Session.Busy && !Session.RequiresReturn;
                start.interactable = Session.CanStart && !Session.Busy;
                if (menu) menu.gameObject.SetActive(false);
                return;
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (Session.View?.menu != null && !Session.View.menu.reward) Session.LocalCommand("close");
                else ToggleExit();
            }
            if (Session.View == null) return;
            UpdateMenu(Session.View);
            if (CoopSession.IsClient && Time.unscaledTime >= nextHud) { nextHud = Time.unscaledTime + .05f; UpdateHUD(Session.View); }
            if (roomTitle)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - titleSince) / .3f);
                roomTitle.rectTransform.anchoredPosition = Vector2.up * Mathf.Lerp(12f, 0f, Mathf.SmoothStep(0f,1f,t));
                var color = roomTitle.color; color.a = t; roomTitle.color = color;
            }
        }
        void UpdateHUD(CoopWorld world)
        {
            var me = world.players.FirstOrDefault(p => p.id == Session.LocalId); if (me == null) return;
            float[] current = { me.health, me.shield, me.energy }, maximum = { me.maxHealth, me.maxShield, me.maxEnergy };
            for (int i = 0; i < 3; i++) { values[i].text = current[i].ToString("0") + "/" + maximum[i].ToString("0"); fills[i].rectTransform.sizeDelta = new Vector2(174 * Mathf.Clamp01(current[i] / Mathf.Max(1,maximum[i])),22); warnings[i].gameObject.SetActive(maximum[i]>0&&current[i]/maximum[i]<=.25f); }
            UpdatePowers(me);
            coins.text = me.coins.ToString();
            equipment.text = (me.slot == 1 ? "> " : "") + "[1] " + me.primary + "\n" + (me.slot == 2 ? "> " : "") + "[2] " + me.secondary + "\n[Q] " + me.consumable + "\n" + me.ability;
            allies.text = string.Join("\n", world.players.Where(p => p.id != me.id).Select(p => p.name + " · " + (p.downed ? "Derribado" : p.health.ToString("0") + "/" + p.maxHealth.ToString("0"))));
            notice.text = me.downed ? (me.reviveProgress > 0 ? "REVIVIENDO " + Mathf.RoundToInt(me.reviveProgress * 100f) + "%" : "DERRIBADO · espera a un aliado") : world.timeStopped ? "TIEMPO DETENIDO" : me.notice;
            Session.GetComponent<CoopSceneMirror>()?.SetDownedView(me.downed);
            timeFlash.color = world.flashColor;
            comparisonPanel.gameObject.SetActive(!string.IsNullOrEmpty(me.comparison)); comparison.text = me.comparison;
            UpdateMap(world, me);
            var room = world.rooms.FirstOrDefault(r => new Bounds(r.center, r.size).Contains(me.position));
            string title = room == null ? "" : (RoomType)room.type == RoomType.Entrance ? "Entrada" : (RoomType)room.type == RoomType.Shop ? "Tienda" : (RoomType)room.type == RoomType.Boss ? "Mini-Boss" : (RoomType)room.type == RoomType.Exit ? "Portal" : (RoomState)room.state == RoomState.Cleared ? "Sala completada" : (RoomState)room.state == RoomState.Combat ? "Oleada " + room.wave + "/" + room.totalWaves : "Sala";
            if (title != lastRoomTitle)
            {
                lastRoomTitle = title; titleSince = Time.unscaledTime; roomTitle.text = title;
                roomTitle.color = room != null && (RoomType)room.type == RoomType.Entrance ? new Color(.5f,.8f,1f) : room != null && (RoomType)room.type == RoomType.Boss ? new Color(1,.3f,.3f) : room != null && (RoomType)room.type == RoomType.Shop ? new Color(1,.8f,.3f) : room != null && (RoomState)room.state == RoomState.Cleared ? new Color(.4f,1,.65f) : new Color(1,.86f,.45f);
            }
        }
        void UpdatePowers(CoopActor me)
        {
            var key = string.Join("|", me.powers ?? System.Array.Empty<string>());
            if (key == shownPowers) return;
            shownPowers = key; foreach (Transform child in powersPanel) Destroy(child.gameObject);
            var definitions = Resources.LoadAll<ProjectLike.Phase12.PowerUpData>("PowerUps");
            int index = 0;
            foreach (var group in (me.powers ?? System.Array.Empty<string>()).GroupBy(p => p).Take(21))
            {
                var power = definitions.FirstOrDefault(p => p && p.effectId == group.Key);
                var icon = Rect("Power", powersPanel, new Vector2(0,1), new Vector2(13+(index%7)*28,-13-(index/7)*28), new Vector2(24,24));
                var image = icon.gameObject.AddComponent<Image>(); image.sprite = power ? power.icon : null; image.preserveAspect = true;
                image.color = image.sprite ? Color.white : power ? LootRarityRules.Color(power.rarity) : Color.white;
                if (group.Count()>1) Label(icon,group.Count().ToString(),new Vector2(1,0),Vector2.zero,new Vector2(18,16),11);
                index++;
            }
        }
        void UpdateMap(CoopWorld world, CoopActor me)
        {
            if (mapSeed != world.seed)
            {
                mapSeed = world.seed; visited.Clear(); map.Clear();
                foreach (Transform child in graph) Destroy(child.gameObject);
                if (world.rooms.Length == 0) return;
                var min = new Vector2(world.rooms.Min(r => r.grid.x),world.rooms.Min(r => r.grid.y));
                var max = new Vector2(world.rooms.Max(r => r.grid.x),world.rooms.Max(r => r.grid.y));
                float step = Mathf.Min(25f,145f / Mathf.Max(1,max.x-min.x),91f / Mathf.Max(1,max.y-min.y)); var center = (min+max)*.5f;
                foreach (var room in world.rooms)
                {
                    var pos = ((Vector2)room.grid-center)*step;
                    foreach (var direction in room.connections)
                    {
                        if ((RoomDirection)direction != RoomDirection.North && (RoomDirection)direction != RoomDirection.East) continue;
                        bool vertical = (RoomDirection)direction == RoomDirection.North;
                        var link = Rect("Link",graph,Vector2.one*.5f,pos+(vertical?Vector2.up:Vector2.right)*step*.5f,vertical?new Vector2(4,step):new Vector2(step,4)); link.gameObject.AddComponent<Image>().color = new Color(.25f,.27f,.29f);
                    }
                }
                foreach (var room in world.rooms)
                {
                    var node = Rect("Room",graph,Vector2.one*.5f,((Vector2)room.grid-center)*step,Vector2.one*15); map[room.grid] = node.gameObject.AddComponent<Image>();
                    if ((RoomType)room.type == RoomType.Boss) Label(node,"!",Vector2.one*.5f,Vector2.zero,new Vector2(18,20),18).color=Color.yellow;
                    else if ((RoomType)room.type == RoomType.Shop)
                    {
                        Pixel(node,new Vector2(0,-2),new Vector2(11,7),new Color(1,.55f,.08f)); Pixel(node,new Vector2(0,3),new Vector2(11,4),new Color(1,.72f,.13f)); Pixel(node,new Vector2(0,-1),new Vector2(3,4),Color.yellow);
                    }
                    else if ((RoomType)room.type == RoomType.Exit) { Pixel(node,Vector2.up*5,new Vector2(5,4),Color.cyan); Pixel(node,Vector2.zero,new Vector2(9,4),Color.cyan); Pixel(node,Vector2.down*5,new Vector2(5,4),Color.cyan); }
                    else if ((RoomType)room.type == RoomType.Entrance) Label(node,"^",Vector2.one*.5f,Vector2.zero,new Vector2(18,20),18).color=Color.green;
                }
            }
            var current = world.rooms.FirstOrDefault(r => new Bounds(r.center,r.size).Contains(me.position));
            if (current != null) visited.Add(current.grid);
            foreach (var pair in map) pair.Value.color = current != null && pair.Key == current.grid ? Color.white : visited.Contains(pair.Key) ? new Color(.43f,.45f,.47f) : new Color(.11f,.12f,.13f);
            floor.text = world.world + " - " + world.level;
        }
        void UpdateMenu(CoopWorld world)
        {
            var offer = world.menu;
            string key = world.gameOver ? "death" : offer == null ? "" : offer.reward ? offer.waiting ? "waiting" : "reward" : "shop";
            if (key == menuKey && (offer == null || offer.revision == lastRevision)) { if (menuMessage && offer != null) menuMessage.text = offer.message; return; }
            StopAllCoroutines(); cardGroups.Clear();
            menuKey = key; lastRevision = offer?.revision ?? -1;
            if (menu) Destroy(menu.gameObject);
            menuMessage = null;
            if (key == "") return;
            menu = Full("PersonalMenu",canvas,new Color(0,0,0,.9f));
            if (key == "death")
            {
                Label(menu,"LA RUN HA TERMINADO",Vector2.one*.5f,Vector2.up*65,new Vector2(700,60),28).color = new Color(1,.3f,.3f);
                if (CoopSession.IsHost) Button(menu,"REINICIAR RUN",Vector2.zero,new Vector2(330,42),()=>Session.LocalCommand("restart"));
                else Label(menu,"Esperando al anfitrion",Vector2.one*.5f,Vector2.zero,new Vector2(600,40),18);
                Button(menu,"SALIR DE LA SALA",Vector2.down*65,new Vector2(330,42),()=>Session.Leave()); return;
            }
            Label(menu,key == "waiting" ? "ESPERANDO A TUS ALIADOS" : offer.title,Vector2.one*.5f,new Vector2(0,215),new Vector2(800,50),27);
            menuMessage = Label(menu,offer.message,Vector2.one*.5f,new Vector2(0,174),new Vector2(820,40),13);
            if (key == "waiting") return;
            for (int i=0;i<offer.offers.Length;i++)
            {
                int index=i; var item=offer.offers[i];
                var card=Rect("Card",menu,Vector2.one*.5f,new Vector2((i-1)*265,0),new Vector2(250,285)); PixelHUDCanvas.DarkFrame(card);
                var group=card.gameObject.AddComponent<CanvasGroup>(); group.alpha=0; group.interactable=false; cardGroups.Add(group);
                var color=LootRarityRules.Color((LootRarity)item.rarity);
                Label(card,item.name,Vector2.one*.5f,new Vector2(0,100),new Vector2(222,56),18).color=color;
                Label(card,ShopMenu.RarityName((LootRarity)item.rarity),Vector2.one*.5f,new Vector2(0,57),new Vector2(222,25),11).color=color;
                var power = Resources.LoadAll<ProjectLike.Phase12.PowerUpData>("PowerUps").FirstOrDefault(p=>p&&p.displayName==item.name);
                var weapon = ProjectLike.Phase2.WeaponCatalog.All.FirstOrDefault(w=>w&&w.weaponName==item.name);
                var artwork = power ? power.icon : weapon ? weapon.icon ? weapon.icon : weapon.sprite : null;
                if(artwork) { var icon=Rect("Icon",card,Vector2.one*.5f,new Vector2(0,17),new Vector2(58,58));var image=icon.gameObject.AddComponent<Image>();image.sprite=artwork;image.preserveAspect=true; }
                Label(card,ShopMenu.ColorizeSignedValues(item.description),Vector2.one*.5f,new Vector2(0,artwork?-46:-10),new Vector2(220,artwork?76:106),13);
                var action=Button(card,item.sold?"AGOTADO":offer.reward?"ELEGIR":item.price+" MONEDAS",new Vector2(0,-104),new Vector2(218,36),()=>Session.LocalCommand(offer.reward?"choose":"buy",index)); action.interactable=!item.sold;
            }
            if (!offer.reward)
            {
                var reroll=Button(menu,"REROLL "+offer.rerolls+"/5 · "+offer.rerollPrice+" MONEDAS",new Vector2(-195,-196),new Vector2(355,38),()=>Session.LocalCommand("reroll")); reroll.interactable=offer.rerolls<5;
                Button(menu,"CERRAR · COMPRAS "+offer.purchases+"/3",new Vector2(195,-196),new Vector2(355,38),()=>Session.LocalCommand("close"));
            }
            StartCoroutine(Reveal());
        }
        IEnumerator Reveal()
        {
            foreach(var group in cardGroups)
            {
                GameSfx.Play(SfxCue.CardReveal,Vector2.zero,.65f,true);
                for(float t=0;t<.24f;t+=Time.unscaledDeltaTime) { if(!group)yield break; var a=Mathf.SmoothStep(0,1,t/.24f); group.alpha=a; group.transform.localScale=Vector3.one*Mathf.Lerp(.88f,1,a); yield return null; }
                group.alpha=1;group.transform.localScale=Vector3.one;group.interactable=true;
            }
        }
        void ToggleExit()
        {
            escapeOpen=!escapeOpen;
            if(exit)Destroy(exit.gameObject);
            if(!escapeOpen)return;
            exit=Full("LeaveSession",canvas,new Color(0,0,0,.9f));
            Label(exit,"PARTIDA ONLINE",Vector2.one*.5f,Vector2.up*90,new Vector2(600,50),26);
            Label(exit,"La partida sigue mientras este menu esta abierto",Vector2.one*.5f,Vector2.up*45,new Vector2(720,40),14);
            Button(exit,"CONTINUAR",Vector2.zero,new Vector2(330,40),ToggleExit);
            Button(exit,"SALIR DE LA SALA",Vector2.down*65,new Vector2(330,40),()=>Session.Leave());
        }
        void Pixel(Transform parent,Vector2 pos,Vector2 size,Color color) { Rect("Pixel",parent,Vector2.one*.5f,pos,size).gameObject.AddComponent<Image>().color=color; }
        static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var root=new GameObject(name,typeof(RectTransform));root.transform.SetParent(parent,false);var rect=(RectTransform)root.transform;rect.anchorMin=rect.anchorMax=anchor;rect.pivot=Vector2.one*.5f;rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
        }
        static RectTransform Full(string name,Transform parent,Color color)
        {
            var rect=Rect(name,parent,Vector2.one*.5f,Vector2.zero,Vector2.zero);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.gameObject.AddComponent<Image>().color=color;return rect;
        }
        static void Frame(RectTransform rect)
        {
            rect.gameObject.AddComponent<Image>().color=new Color(.105f,.105f,.11f);
            var outline=rect.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.31f,.31f,.32f);outline.effectDistance=new Vector2(3,-3);
            var shadow=rect.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.025f,.025f,.025f);shadow.effectDistance=new Vector2(6,-6);
        }
        Text Label(Transform parent,string value,Vector2 anchor,Vector2 position,Vector2 size,int fontSize)
        {
            var rect=Rect("Text",parent,anchor,position,size);var text=rect.gameObject.AddComponent<Text>();text.font=font;text.text=value;text.fontSize=fontSize;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.color=Color.white;
            var shadow=rect.gameObject.AddComponent<Shadow>();shadow.effectColor=Color.black;shadow.effectDistance=new Vector2(2,-2);return text;
        }
        Button Button(Transform parent,string title,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action)
        {
            var rect=Rect(title,parent,Vector2.one*.5f,position,size);Frame(rect);var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
            var colors=button.colors;colors.highlightedColor=new Color(.8f,.55f,1f);colors.selectedColor=colors.highlightedColor;button.colors=colors;
            Label(rect,title,Vector2.one*.5f,Vector2.zero,size-new Vector2(12,4),14);
            button.onClick.AddListener(()=>{GameSfx.Play(SfxCue.UiConfirm,Vector2.zero,.5f,true);action();});return button;
        }
    }
}
