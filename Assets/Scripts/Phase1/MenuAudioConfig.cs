using UnityEngine;

namespace ProjectLike.Phase1
{
    [CreateAssetMenu(menuName = "Project Like/Menu Audio Config")]
    public sealed class MenuAudioConfig : ScriptableObject
    {
        public AudioClip menuMusic;
        public AudioClip levelBoom;
        public Font menuFont;
    }
}
