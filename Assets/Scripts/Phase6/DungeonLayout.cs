using System;
using System.Collections.Generic;
using ProjectLike.Phase5;
using UnityEngine;

namespace ProjectLike.Phase6
{
    [Serializable]
    public class DungeonRoomLayout
    {
        public Vector2Int gridPosition;
        public RoomType roomType;
        public RoomVisualVariant visualVariant;
        public bool mainPath;
        public List<RoomDirection> connections = new List<RoomDirection>();
    }

    [Serializable]
    public class DungeonLayout
    {
        public string seed;
        public List<DungeonRoomLayout> rooms = new List<DungeonRoomLayout>();

        public DungeonRoomLayout FindRoom(Vector2Int position)
        {
            return rooms.Find(room => room.gridPosition == position);
        }
    }
}
