using UnityEngine;

namespace ProjectLike.Phase3
{
    /// <summary>Reproduce los cuatro frames del spritesheet sin necesitar Animator Controllers.</summary>
    [RequireComponent(typeof(EnemyBrain))]
    public class EnemySpriteAnimator : MonoBehaviour
    {
        EnemyBrain brain;
        SpriteRenderer spriteRenderer;

        void Awake()
        {
            brain = GetComponent<EnemyBrain>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        void LateUpdate()
        {
            if (!brain || !brain.data || !spriteRenderer) return;
            var frames = brain.data.animationFrames;
            if (frames == null || frames.Length == 0) return;

            var moving = brain.State == EnemyState.Chase || brain.State == EnemyState.Attack;
            var index = moving ? Mathf.FloorToInt(Time.time * brain.data.animationFramesPerSecond) % frames.Length : 0;
            spriteRenderer.sprite = frames[index] ? frames[index] : brain.data.sprite;
        }
    }
}
