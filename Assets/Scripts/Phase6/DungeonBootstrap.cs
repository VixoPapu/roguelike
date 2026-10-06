using UnityEngine;

namespace ProjectLike.Phase6
{
    /// <summary>Inicia una run procedural incluso si la escena no tiene un objeto configurado a mano.</summary>
    internal static class DungeonBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureGenerator()
        {
            if (Object.FindAnyObjectByType<DungeonGenerator>()) return;
            var root = new GameObject("DungeonGenerator");
            root.AddComponent<DungeonGenerator>();
        }
    }
}
