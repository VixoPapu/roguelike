using UnityEngine;

namespace ProjectLike.Audio
{
    [CreateAssetMenu(menuName = "Project Like/Audio/SFX Bank", fileName = "GameSfxBank")]
    public sealed class GameSfxBank : ScriptableObject
    {
        public AudioClip[] swordSwing, swordImpact, bowShot, weaponCharge, heavySwing, heavyImpact, magicCast, darkMagicCast, magicImpact, darkMagicImpact;
        public AudioClip[] electric, ice, explosion, wave, stun;
        public AudioClip[] playerHurt, playerDeath, dash, enemyAttack, enemySpecial, enemyHurt, enemyDeath;
        public AudioClip[] coin, energy, health, chestOpen, doorOpen, doorClose, shield;
        public AudioClip[] uiConfirm, uiDeny, shopBuy, pickup, powerUp, consumable, portal, roomClear, footstep, cardReveal;

        public AudioClip[] Clips(SfxCue cue)
        {
            switch (cue)
            {
                case SfxCue.SwordSwing: return swordSwing; case SfxCue.SwordImpact: return swordImpact;
                case SfxCue.BowShot: return bowShot; case SfxCue.WeaponCharge: return weaponCharge; case SfxCue.HeavySwing: return heavySwing;
                case SfxCue.HeavyImpact: return heavyImpact; case SfxCue.MagicCast: return magicCast;
                case SfxCue.DarkMagicCast: return darkMagicCast; case SfxCue.MagicImpact: return magicImpact;
                case SfxCue.DarkMagicImpact: return darkMagicImpact; case SfxCue.Electric: return electric;
                case SfxCue.Ice: return ice; case SfxCue.Explosion: return explosion; case SfxCue.Wave: return wave;
                case SfxCue.Stun: return stun; case SfxCue.PlayerHurt: return playerHurt;
                case SfxCue.PlayerDeath: return playerDeath; case SfxCue.Dash: return dash;
                case SfxCue.EnemyAttack: return enemyAttack; case SfxCue.EnemySpecial: return enemySpecial;
                case SfxCue.EnemyHurt: return enemyHurt; case SfxCue.EnemyDeath: return enemyDeath;
                case SfxCue.Coin: return coin; case SfxCue.Energy: return energy; case SfxCue.Health: return health;
                case SfxCue.ChestOpen: return chestOpen; case SfxCue.DoorOpen: return doorOpen;
                case SfxCue.DoorClose: return doorClose; case SfxCue.Shield: return shield;
                case SfxCue.UiConfirm: return uiConfirm; case SfxCue.UiDeny: return uiDeny;
                case SfxCue.ShopBuy: return shopBuy; case SfxCue.Pickup: return pickup;
                case SfxCue.PowerUp: return powerUp; case SfxCue.Consumable: return consumable;
                case SfxCue.Portal: return portal; case SfxCue.RoomClear: return roomClear;
                case SfxCue.CardReveal: return cardReveal;
                default: return footstep;
            }
        }
    }
}
