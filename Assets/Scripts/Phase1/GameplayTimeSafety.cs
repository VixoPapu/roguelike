using UnityEngine;

namespace ProjectLike.Phase1
{
    /// <summary>Evita que una recarga de scripts herede una pausa sin propietario.</summary>
    static class GameplayTimeSafety
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetForNewPlaySession()
        {
            Time.timeScale = 1f;
        }
    }
}
