using ProjectLike.Phase1;
using UnityEngine;

namespace ProjectLike.Phase2
{
    /// <summary>Seguimiento 2D robusto. Se recupera aunque la referencia serializada se pierda.</summary>
    [DisallowMultipleComponent]
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        [Min(0f)] public float smoothTime = .08f;
        public Vector3 offset = new Vector3(0f, 0f, -10f);
        [Range(0f, 1f)] public float trauma;
        public float shakeDecay = 3.5f;
        [Header("Bow charge zoom")]
        [Min(0f)] public float maximumBowZoomOut = .5f;
        [Min(.01f)] public float bowZoomSmoothTime = .24f;
        [Min(.01f)] public float bowReturnSmoothTime = .1f;

        Vector3 velocity;
        Camera sceneCamera;
        float defaultOrthographicSize;
        float bowZoomAmount;
        float zoomVelocity;
        bool cinematicZoom;
        bool downedZoom;

        void OnEnable()
        {
            ResolveTarget();
            sceneCamera = GetComponent<Camera>();
            if (sceneCamera && sceneCamera.orthographic) defaultOrthographicSize = sceneCamera.orthographicSize;
        }

        public void SetBowChargeZoom(float normalizedCharge) { bowZoomAmount = Mathf.Clamp01(normalizedCharge); }
        public void SetCinematicZoom(bool active) { cinematicZoom = active; if (active) bowZoomAmount = 0f; }
        public void SetDownedZoom(bool active) { downedZoom = active; if (active) bowZoomAmount = 0f; }

        public void SetTarget(Transform newTarget, bool snapImmediately = true)
        {
            target = newTarget;
            velocity = Vector3.zero;
            if (snapImmediately) SnapToTarget();
        }

        public void SnapToTarget()
        {
            ResolveTarget();
            if (!target) return;
            transform.position = target.position + offset;
            velocity = Vector3.zero;
        }

        public void Shake(float intensity)
        {
            trauma = Mathf.Clamp01(Mathf.Max(trauma, intensity));
        }

        void LateUpdate()
        {
            ResolveTarget();
            if (!target) return;

            var desired = target.position + offset;
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desired,
                ref velocity,
                Mathf.Max(.001f, smoothTime),
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            if (sceneCamera && sceneCamera.orthographic && !cinematicZoom)
            {
                var easedCharge = Mathf.SmoothStep(0f, 1f, bowZoomAmount);
                var desiredSize = downedZoom ? defaultOrthographicSize * .58f : defaultOrthographicSize + maximumBowZoomOut * easedCharge;
                var smooth = bowZoomAmount > .001f ? bowZoomSmoothTime : bowReturnSmoothTime;
                sceneCamera.orthographicSize = Mathf.SmoothDamp(sceneCamera.orthographicSize, desiredSize, ref zoomVelocity, smooth, Mathf.Infinity, Time.unscaledDeltaTime);
            }

            if (trauma <= 0f) return;
            var shake = Random.insideUnitCircle * trauma * .18f;
            transform.position += (Vector3)shake;
            trauma = Mathf.MoveTowards(trauma, 0f, shakeDecay * Time.unscaledDeltaTime);
        }

        void ResolveTarget()
        {
            if (target && target.gameObject.activeInHierarchy) return;
            var player = FindAnyObjectByType<PlayerController>();
            if (player) target = player.transform;
            else
            {
                var taggedPlayer = GameObject.FindWithTag("Player");
                target = taggedPlayer ? taggedPlayer.transform : null;
            }
        }
    }

    /// <summary>
    /// Reinstala el componente si una escena antigua conserva una referencia Missing Script.
    /// </summary>
    internal static class CameraFollowBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureCameraFollow()
        {
            var camera = Camera.main;
            if (!camera) camera = Object.FindAnyObjectByType<Camera>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (!camera || !player) return;

            var follow = camera.GetComponent<CameraFollow>();
            if (!follow) follow = camera.gameObject.AddComponent<CameraFollow>();
            follow.SetTarget(player.transform, true);
        }
    }
}
