using System;
using UnityEngine;

namespace ProjectLike.Online
{
    [Serializable] public class CoopMessage
    {
        public string kind;
        public int version = 1, id, index, revision;
        public string text, code;
        public CoopControls input;
        public CoopWorld world;
        public CoopTexture texture;
        public CoopGeometry geometry;
        public CoopSound sound;
    }
    [Serializable] public class CoopWorld
    {
        public string seed;
        public int world, level;
        public bool timeStopped, gameOver, hasMenu;
        public Color background;
        public Color flashColor;
        public float flash;
        public CoopActor[] players;
        public CoopRoom[] rooms;
        public CoopDraw[] draws;
        public CoopMenu menu;
    }
    [Serializable] public class CoopActor
    {
        public int id, coins, slot;
        public string name, primary, secondary, consumable, ability, notice, comparison;
        public Vector3 position;
        public float health, maxHealth, shield, maxShield, energy, maxEnergy;
        public bool downed, reviving;
        public float reviveProgress;
        public int reviverId;
        public string[] powers;
    }
    [Serializable] public class CoopRoom
    {
        public Vector2Int grid;
        public Vector3 center, size;
        public int type, state, wave, totalWaves;
        public int[] connections;
    }
    [Serializable] public class CoopMenu
    {
        public string title, message;
        public bool reward, waiting;
        public int revision, purchases, rerolls, rerollPrice;
        public CoopOffer[] offers;
    }
    [Serializable] public class CoopOffer
    {
        public string name, description;
        public int rarity, price;
        public bool sold;
    }
    [Serializable] public class CoopTexture { public int id; public string png; }
    [Serializable] public class CoopGeometry
    {
        public int id;
        public Vector3[] vertices;
        public Vector2[] uv;
        public int[] triangles;
        public Color[] colors;
    }
    [Serializable] public class CoopDraw
    {
        public int id, geometry, texture, order, layer, type;
        public Vector3 position, scale;
        public Quaternion rotation;
        public Color color;
        public bool gray;
        public CoopGeometry dynamicMesh;
        public string text;
        public int fontSize;
        public float characterSize;
        public Vector2 textSize;
        public int alignment;
    }
    [Serializable] public class CoopSound
    {
        public string clip;
        public Vector3 position;
        public float volume, pitch, spatial;
    }
}
