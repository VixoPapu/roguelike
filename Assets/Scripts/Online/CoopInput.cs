using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectLike.Online
{
    [Flags] public enum CoopButtons { Attack = 1, Dash = 2, Interact = 4, Consumable = 8, Ability = 16, Primary = 32, Secondary = 64 }
    [Serializable] public struct CoopControls
    {
        public Vector2 move, aim;
        public int pressed, held, released;
        public bool Down(CoopButtons key) => (pressed & (int)key) != 0;
        public bool Hold(CoopButtons key) => (held & (int)key) != 0;
        public bool Up(CoopButtons key) => (released & (int)key) != 0;
        public void ClearEdges() { pressed = 0; released = 0; }
        public void Merge(CoopControls next) { move = next.move; aim = next.aim; held = next.held; pressed |= next.pressed; released |= next.released; }
    }
    public sealed class CoopPlayer : MonoBehaviour
    {
        public int id;
        public string playerName;
        public CoopControls controls;
        public bool menuOpen;
    }
    public static class CoopInput
    {
        public static bool IsRemote(GameObject owner) => CoopSession.IsHost && owner.GetComponent<CoopPlayer>() is CoopPlayer member && member.id != 1;
        public static bool Blocked(GameObject owner)
        {
            if (CoopSession.IsClient) return true;
            var member = owner.GetComponent<CoopPlayer>();
            if (member && (member.menuOpen || !CoopSession.Playing)) return true;
            var stats = owner.GetComponent<ProjectLike.Phase1.PlayerStats>();
            return stats && stats.CurrentHealth <= 0 || ProjectLike.Phase12.TimeStopAbility.IsActive && !ProjectLike.Phase12.TimeStopAbility.CanAct(owner);
        }
        public static CoopControls For(GameObject owner)
        {
            if (Blocked(owner)) return default;
            var member = owner.GetComponent<CoopPlayer>();
            return member ? member.controls : Capture(owner.transform.position);
        }
        public static CoopControls Capture(Vector3 origin)
        {
            var k = Keyboard.current; var g = Gamepad.current; var m = Mouse.current;
            var value = new CoopControls { aim = Vector2.right };
            value.move = g != null ? g.leftStick.ReadValue() : Vector2.zero;
            if (k != null) value.move += new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
            value.move = Vector2.ClampMagnitude(value.move, 1f);
            if (g != null && g.rightStick.ReadValue().sqrMagnitude > .08f) value.aim = g.rightStick.ReadValue().normalized;
            else if (m != null && Camera.main) value.aim = ((Vector2)(Camera.main.ScreenToWorldPoint(m.position.ReadValue()) - origin)).normalized;
            Set(ref value, CoopButtons.Attack, m?.leftButton, g?.rightTrigger);
            Set(ref value, CoopButtons.Dash, k?.spaceKey, g?.rightShoulder);
            Set(ref value, CoopButtons.Interact, k?.eKey, g?.buttonSouth);
            Set(ref value, CoopButtons.Consumable, k?.qKey, g?.leftShoulder);
            Set(ref value, CoopButtons.Ability, k?.fKey, g?.rightStickButton);
            Set(ref value, CoopButtons.Primary, k?.digit1Key, g?.dpad.left);
            Set(ref value, CoopButtons.Secondary, k?.digit2Key, g?.dpad.right);
            Set(ref value, CoopButtons.Primary, k?.numpad1Key, null);
            Set(ref value, CoopButtons.Secondary, k?.numpad2Key, null);
            return value;
        }
        static void Set(ref CoopControls value, CoopButtons key, UnityEngine.InputSystem.Controls.ButtonControl a, UnityEngine.InputSystem.Controls.ButtonControl b)
        {
            if (a != null && a.wasPressedThisFrame || b != null && b.wasPressedThisFrame) value.pressed |= (int)key;
            if (a != null && a.isPressed || b != null && b.isPressed) value.held |= (int)key;
            if (a != null && a.wasReleasedThisFrame || b != null && b.wasReleasedThisFrame) value.released |= (int)key;
        }
    }
}
