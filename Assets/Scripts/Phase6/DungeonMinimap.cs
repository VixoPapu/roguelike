using System.Collections.Generic;
using ProjectLike.Phase1;
using ProjectLike.Phase5;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLike.Phase6
{
    /// <summary>Mapa compacto del piso actual. Descubre salas cuando el jugador entra físicamente.</summary>
    public sealed class DungeonMinimap : MonoBehaviour
    {
        readonly Dictionary<Vector2Int, Image> rooms = new Dictionary<Vector2Int, Image>();
        readonly HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        PlayerStats player;
        DungeonGenerator generator;
        RectTransform graph;
        Text floorLabel;
        Font font;
        string displayedSeed;
        float nextRoomPoll;
        Vector2Int currentRoom;
        bool hasCurrentRoom;

        public void Initialize(PlayerStats target, Font pixelFont)
        {
            player = target;
            font = pixelFont ? pixelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            graph = Rect("Graph", transform, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -13f), new Vector2(166f, 112f));
            floorLabel = Label("Floor", transform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 9f), new Vector2(160f, 28f), 22, "1 - 1", Color.white);
        }

        void Update()
        {
            if (!generator) generator = FindAnyObjectByType<DungeonGenerator>();
            if (!player) player = FindAnyObjectByType<PlayerStats>();
            if (!generator || generator.Layout == null || !graph) return;
            if (displayedSeed != generator.CurrentSeed) Rebuild();
            floorLabel.text = generator.CurrentWorld + " - " + generator.CurrentLevel;
            if (Time.unscaledTime < nextRoomPoll) return;
            nextRoomPoll = Time.unscaledTime + .12f;
            LocatePlayer();
            RefreshColors();
        }

        void Rebuild()
        {
            displayedSeed = generator.CurrentSeed;
            visited.Clear(); rooms.Clear(); hasCurrentRoom = false;
            for (var i = graph.childCount - 1; i >= 0; i--) Destroy(graph.GetChild(i).gameObject);
            var layout = generator.Layout;
            if (layout.rooms.Count == 0) return;
            var min = layout.rooms[0].gridPosition;
            var max = min;
            foreach (var room in layout.rooms) { min = Vector2Int.Min(min, room.gridPosition); max = Vector2Int.Max(max, room.gridPosition); }
            var width = Mathf.Max(1, max.x - min.x);
            var height = Mathf.Max(1, max.y - min.y);
            var step = Mathf.Min(25f, 145f / width, 91f / height);
            var center = (Vector2)(min + max) * .5f;

            foreach (var room in layout.rooms)
            {
                var position = ((Vector2)room.gridPosition - center) * step;
                foreach (var direction in room.connections)
                {
                    if (direction != RoomDirection.North && direction != RoomDirection.East) continue;
                    var vertical = direction == RoomDirection.North;
                    var link = Rect("Link", graph, Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f,
                        position + (vertical ? Vector2.up : Vector2.right) * step * .5f,
                        vertical ? new Vector2(5f, Mathf.Max(5f, step - 12f)) : new Vector2(Mathf.Max(5f, step - 12f), 5f));
                    link.gameObject.AddComponent<Image>().color = new Color(.25f, .27f, .29f, 1f);
                }
            }
            foreach (var room in layout.rooms)
            {
                var position = ((Vector2)room.gridPosition - center) * step;
                var node = Rect(room.roomType + "_" + room.gridPosition, graph, Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, position, Vector2.one * 15f);
                var image = node.gameObject.AddComponent<Image>(); image.color = Unknown;
                rooms[room.gridPosition] = image;
                if (room.roomType == RoomType.Shop) BuildChest(node);
                else if (room.roomType == RoomType.Boss) Label("Boss", node, Vector2.zero, Vector2.one, Vector2.one * .5f, Vector2.zero, Vector2.one * 18f, 17, "!", new Color(1f, .88f, .12f));
                else if (room.roomType == RoomType.Exit) BuildPortal(node);
                else if (room.roomType == RoomType.Entrance) BuildEntrance(node);
            }
            LocatePlayer();
            RefreshColors();
        }

        void LocatePlayer()
        {
            if (!player) return;
            var nodes = FindObjectsByType<DungeonRoomNode>();
            foreach (var node in nodes)
            {
                var controller = node.GetComponent<RoomController>();
                if (!controller || !controller.WorldBounds.Contains(player.transform.position)) continue;
                currentRoom = node.gridPosition; hasCurrentRoom = true; visited.Add(currentRoom); return;
            }
        }

        void RefreshColors()
        {
            foreach (var pair in rooms)
                pair.Value.color = hasCurrentRoom && pair.Key == currentRoom ? Color.white : visited.Contains(pair.Key) ? Visited : Unknown;
        }

        void BuildChest(Transform parent)
        {
            Pixel(parent, "ChestBody", new Vector2(0f, -2f), new Vector2(11f, 7f), new Color(1f, .55f, .08f));
            Pixel(parent, "ChestLid", new Vector2(0f, 3.5f), new Vector2(11f, 4f), new Color(1f, .72f, .13f));
            Pixel(parent, "ChestLock", new Vector2(0f, -1f), new Vector2(3f, 4f), new Color(1f, .92f, .35f));
        }

        void BuildPortal(Transform parent)
        {
            var cyan = new Color(.04f, .9f, 1f);
            Pixel(parent, "PortalTop", new Vector2(0f, 5f), new Vector2(5f, 4f), cyan);
            Pixel(parent, "PortalMiddle", Vector2.zero, new Vector2(9f, 4f), cyan);
            Pixel(parent, "PortalBottom", new Vector2(0f, -5f), new Vector2(5f, 4f), cyan);
        }

        void BuildEntrance(Transform parent)
        {
            var green = new Color(.1f, 1f, .35f);
            Pixel(parent, "Roof", new Vector2(0f, 4f), new Vector2(11f, 5f), green);
            Pixel(parent, "Home", new Vector2(0f, -2f), new Vector2(8f, 8f), green);
        }

        static void Pixel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var rect = Rect(name, parent, Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, position, size);
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        }

        Text Label(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size, int sizePx, string value, Color color)
        {
            var rect = Rect(name, parent, min, max, pivot, pos, size);
            var label = rect.gameObject.AddComponent<Text>(); label.font = font; label.fontSize = sizePx; label.text = value; label.color = color; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            var shadow = rect.gameObject.AddComponent<Shadow>(); shadow.effectColor = Color.black; shadow.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.anchoredPosition = pos; rect.sizeDelta = size; return rect;
        }

        static readonly Color Unknown = new Color(.11f, .12f, .13f, 1f);
        static readonly Color Visited = new Color(.43f, .45f, .47f, 1f);
    }
}
