using System.Collections.Generic;
using ProjectLike.Phase5;
using UnityEngine;

namespace ProjectLike.Phase6
{
    /// <summary>Representación física de un nodo del layout procedural.</summary>
    public class DungeonRoomNode : MonoBehaviour
    {
        public Vector2Int gridPosition;
        public RoomType roomType;
        public RoomVisualVariant visualVariant;
        public bool mainPath;
        public List<RoomDirection> connections = new List<RoomDirection>();

        public void Configure(DungeonRoomLayout layout)
        {
            gridPosition = layout.gridPosition;
            roomType = layout.roomType;
            visualVariant = layout.visualVariant;
            mainPath = layout.mainPath;
            connections = new List<RoomDirection>(layout.connections);
            gameObject.name = roomType + "_" + gridPosition.x + "_" + gridPosition.y;
        }
    }
}
