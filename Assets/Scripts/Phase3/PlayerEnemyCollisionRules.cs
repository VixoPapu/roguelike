using ProjectLike.Phase1;
using UnityEngine;

namespace ProjectLike.Phase3
{
    /// <summary>
    /// El jugador y los enemigos pueden atravesarse. Los ataques melee siguen
    /// usando distancia y los proyectiles mantienen sus propios triggers.
    /// </summary>
    public static class PlayerEnemyCollisionRules
    {
        public const int PlayerLayer = 8;
        public const int EnemyLayer = 9;

        public static void IgnoreForPlayer(PlayerController player)
        {
            if (!player) return;
            Physics2D.IgnoreLayerCollision(PlayerLayer, EnemyLayer, true);
            player.gameObject.layer = PlayerLayer;
            foreach (var enemy in Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include))
                Ignore(player, enemy);
        }

        public static void IgnoreForEnemy(EnemyBrain enemy)
        {
            if (!enemy) return;
            Physics2D.IgnoreLayerCollision(PlayerLayer, EnemyLayer, true);
            enemy.gameObject.layer = EnemyLayer;
            foreach (var player in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include))
                Ignore(player, enemy);
        }

        static void Ignore(PlayerController player, EnemyBrain enemy)
        {
            if (!player || !enemy) return;
            var playerColliders = player.GetComponentsInChildren<Collider2D>(true);
            var enemyColliders = enemy.GetComponentsInChildren<Collider2D>(true);
            foreach (var playerCollider in playerColliders)
            foreach (var enemyCollider in enemyColliders)
                if (playerCollider && enemyCollider) Physics2D.IgnoreCollision(playerCollider, enemyCollider, true);
        }
    }
}
