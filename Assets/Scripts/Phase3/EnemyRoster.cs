using UnityEngine;

namespace ProjectLike.Phase3
{
    [CreateAssetMenu(menuName = "Project Like/Enemies/Enemy Roster", fileName = "EnemyRoster")]
    public class EnemyRoster : ScriptableObject
    {
        public EnemyData[] common;
        public EnemyData[] elite;
        public EnemyData[] bosses;
    }
}
