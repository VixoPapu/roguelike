using UnityEngine;

namespace ProjectLike.Phase3
{
    /// <summary>
    /// Animacion procedural de caminata. Solo deforma una copia visual para que
    /// el Rigidbody2D y el collider del enemigo conserven siempre su tamano.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyWalkVisual : MonoBehaviour
    {
        [Min(1f)] public float walkCycleSpeed = 11f;
        [Range(0f, .3f)] public float verticalSquash = .11f;
        [Range(0f, .25f)] public float horizontalStretch = .055f;
        [Range(0f, 15f)] public float walkTilt = 5f;
        [Range(0f, .2f)] public float walkBob = .045f;

        EnemyBrain brain;
        SpriteRenderer sourceRenderer;
        SpriteRenderer animatedRenderer;
        Transform visualTransform;
        Vector3 baseScale;
        Vector3 basePosition;
        Color baseColor;
        float walkPhase;
        float spawnedAt;
        bool clonedRenderer;

        void Awake()
        {
            brain = GetComponent<EnemyBrain>();
            sourceRenderer = GetComponent<SpriteRenderer>();
            if (!sourceRenderer) sourceRenderer = GetComponentInChildren<SpriteRenderer>();
            if (!sourceRenderer) return;

            if (sourceRenderer.transform == transform)
            {
                var visual = new GameObject("EnemyAnimatedVisual");
                visual.transform.SetParent(transform, false);
                animatedRenderer = visual.AddComponent<SpriteRenderer>();
                CopyRenderer(sourceRenderer, animatedRenderer);
                sourceRenderer.enabled = false;
                clonedRenderer = true;
            }
            else
            {
                animatedRenderer = sourceRenderer;
            }

            visualTransform = animatedRenderer.transform;
            baseScale = visualTransform.localScale;
            basePosition = visualTransform.localPosition;
            baseColor = animatedRenderer.color;
            spawnedAt = Time.time;
        }

        void LateUpdate()
        {
            if (!brain || !animatedRenderer || !visualTransform) return;
            if (clonedRenderer) SyncRenderer();

            var movement = brain.MovementIntensity;
            var targetScale = baseScale;
            var targetPosition = basePosition;
            var targetRotation = 0f;

            if (brain.IsMoving)
            {
                walkPhase += Time.deltaTime * walkCycleSpeed * Mathf.Lerp(.75f, 1.15f, movement);
                var side = Mathf.Sin(walkPhase);
                var contact = Mathf.Abs(side);
                targetScale.x *= 1f + horizontalStretch * contact;
                targetScale.y *= 1f - verticalSquash * contact;
                targetPosition.y += walkBob * contact;
                targetRotation = side * walkTilt;
            }
            else
            {
                walkPhase = Mathf.MoveTowards(walkPhase, 0f, Time.deltaTime * walkCycleSpeed);
                var breathe = Mathf.Sin(Time.time * 3f) * .012f;
                targetScale.x *= 1f - breathe * .5f;
                targetScale.y *= 1f + breathe;
            }

            // Pequeno pop de aparicion que se integra con la marca del suelo.
            var reveal = Mathf.Clamp01((Time.time - spawnedAt) / .2f);
            targetScale *= Mathf.SmoothStep(.28f, 1f, reveal);
            var response = 1f - Mathf.Exp(-22f * Time.deltaTime);
            visualTransform.localScale = Vector3.Lerp(visualTransform.localScale, targetScale, response);
            visualTransform.localPosition = Vector3.Lerp(visualTransform.localPosition, targetPosition, response);
            visualTransform.localRotation = Quaternion.Lerp(
                visualTransform.localRotation,
                Quaternion.Euler(0f, 0f, brain.IsDead ? 90f : targetRotation),
                response);

            if (!brain.IsDead)
            {
                var current = clonedRenderer ? sourceRenderer.color : baseColor;
                animatedRenderer.color = new Color(current.r, current.g, current.b, current.a * reveal);
            }
        }

        void SyncRenderer()
        {
            animatedRenderer.sprite = sourceRenderer.sprite;
            animatedRenderer.flipX = sourceRenderer.flipX;
            animatedRenderer.flipY = sourceRenderer.flipY;
            animatedRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            animatedRenderer.sortingOrder = sourceRenderer.sortingOrder;
        }

        static void CopyRenderer(SpriteRenderer source, SpriteRenderer destination)
        {
            destination.sharedMaterial = source.sharedMaterial;
            destination.sortingLayerID = source.sortingLayerID;
            destination.sortingOrder = source.sortingOrder;
            destination.maskInteraction = source.maskInteraction;
            destination.sprite = source.sprite;
            destination.color = source.color;
            destination.flipX = source.flipX;
            destination.flipY = source.flipY;
        }
    }
}
