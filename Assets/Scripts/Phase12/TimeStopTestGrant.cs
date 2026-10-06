#if UNITY_EDITOR || DEVELOPMENT_BUILD
using ProjectLike.Phase1;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectLike.Phase12
{
    // Script de pruebas independiente. Se puede borrar junto con su .meta.
    public sealed class TimeStopTestGrant : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var player = Object.FindAnyObjectByType<PlayerStats>();
            if (player && !player.GetComponent<TimeStopTestGrant>()) player.gameObject.AddComponent<TimeStopTestGrant>();
        }
        void Update() { if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame) GrantForTest(); }
        [ContextMenu("TEST / Dar Tiempo cero")]
        public void GrantForTest()
        {
            if (!Application.isPlaying) return;
            var power = Resources.Load<PowerUpData>("PowerUps/PowerUp_Tiempo_cero");
            var inventory = GetComponent<PlayerPowerUps>();
            if (!inventory) inventory = gameObject.AddComponent<PlayerPowerUps>();
            if (power && inventory.Add(power)) Debug.Log("Tiempo cero adquirido. F/R3: activar. Coste 150, duracion aleatoria 6-9s, dano +15%, recarga 25s.");
        }
    }
}
#endif
