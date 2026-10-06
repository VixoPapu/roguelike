using ProjectLike.Phase1;
using ProjectLike.Phase2;
using ProjectLike.Phase12;
using UnityEngine;
using ProjectLike.Audio;

namespace ProjectLike.Phase6
{
    /// <summary>Portal seguro que inicia el siguiente nivel procedural.</summary>
    public class DungeonPortal : Interactable
    {
        Vector3 baseScale;
        bool opening;

        void Awake()
        {
            prompt = "Entrar al portal";
            baseScale = transform.localScale;
        }

        void Update()
        {
            var pulse = 1f + Mathf.Sin(Time.time * 4f) * .08f;
            transform.localScale = baseScale * pulse;
            transform.Rotate(0f, 0f, 35f * Time.deltaTime);
        }

        public override void Interact(GameObject player)
        {
            if (ProjectLike.Online.CoopSession.Active) { ProjectLike.Online.CoopSession.Instance.EnterPortal(player); return; }
            var generator = FindAnyObjectByType<DungeonGenerator>();
            if (!generator || !player || opening || PowerUpRewardMenu.IsOpen) return;
            opening = PowerUpRewardMenu.Open(player, () => Advance(generator, player));
            if (opening)
            {
                GameSfx.Play(SfxCue.Portal, transform.position, .82f);
                var stats = player.GetComponent<PlayerStats>();
                if (stats) stats.RestoreVitals();
            }
        }

        static void Advance(DungeonGenerator generator, GameObject player)
        {
            if (!generator || !player) return;
            generator.AdvanceRun(player.GetComponent<PlayerController>());
            var body = player.GetComponent<Rigidbody2D>();
            if (body)
            {
                body.position = Vector2.zero;
                body.linearVelocity = Vector2.zero;
            }
            player.transform.position = Vector3.zero;
            Physics2D.SyncTransforms();
            var follow = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow) follow.SetTarget(player.transform, true);
        }
    }
}
