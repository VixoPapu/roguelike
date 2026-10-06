using UnityEngine;
using ProjectLike.Audio;

namespace ProjectLike.Phase5
{
    /// <summary>Puerta direccional que funciona como barrera durante un combate.</summary>
    public class RoomDoor : MonoBehaviour
    {
        public RoomDirection direction;
        public Color unlockedColor = new Color(.32f, .88f, .48f, 1);
        public Color lockedColor = new Color(.92f, .22f, .18f, 1);
        public bool IsLocked { get; private set; }
        public bool IsPermanentOpen { get; private set; }

        Collider2D[] blockers;
        SpriteRenderer spriteRenderer;
        bool initialized;

        void Awake()
        {
            blockers = GetComponents<Collider2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            SetLocked(false);
            initialized = true;
        }

        public void SetLocked(bool locked)
        {
            if (IsPermanentOpen) locked = false;
            var changed = initialized && IsLocked != locked;
            IsLocked = locked;
            if (blockers == null || blockers.Length == 0) blockers = GetComponents<Collider2D>();
            foreach (var blocker in blockers) blocker.isTrigger = !locked;
            if (spriteRenderer) spriteRenderer.color = locked ? lockedColor : unlockedColor;
            if (changed) GameSfx.Play(locked ? SfxCue.DoorClose : SfxCue.DoorOpen, transform.position, .55f);
        }

        public void SetPermanentOpen(bool permanentOpen)
        {
            IsPermanentOpen = permanentOpen;
            if (permanentOpen) SetLocked(false);
        }
    }
}
