using ProjectLike.Phase2;
using UnityEngine;

namespace ProjectLike.Phase3
{
    /// <summary>Barra mundial pixel art que no depende de Canvas ni genera allocations.</summary>
    [DisallowMultipleComponent]
    public class EnemyHealthBar : MonoBehaviour
    {
        const float InnerWidth = .82f;
        EnemyBrain brain;
        Transform fillTransform;
        SpriteRenderer fillRenderer;
        float displayedRatio = 1f;

        void Awake()
        {
            brain = GetComponent<EnemyBrain>();
            var root = new GameObject("EnemyHealthBar");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, .72f, 0f);

            CreatePart("BlackOutline", root.transform, Vector2.zero, new Vector2(.94f, .16f), Color.black, 19);
            CreatePart("BlackBackground", root.transform, Vector2.zero, new Vector2(.87f, .105f), new Color(.025f, .025f, .025f, 1f), 20);
            fillRenderer = CreatePart("HealthFill", root.transform, Vector2.zero, new Vector2(InnerWidth, .075f), new Color(.88f, .06f, .08f, 1f), 21);
            fillTransform = fillRenderer.transform;
        }

        SpriteRenderer CreatePart(string partName, Transform parent, Vector2 position, Vector2 size, Color color, int order)
        {
            var part = new GameObject(partName);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = CombatFeedback.WhiteSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        void LateUpdate()
        {
            if (!brain || !brain.data || !fillTransform) return;
            var target = brain.RuntimeMaxHealth > 0f ? Mathf.Clamp01(brain.CurrentHealth / brain.RuntimeMaxHealth) : 0f;
            displayedRatio = Mathf.MoveTowards(displayedRatio, target, Time.deltaTime * 3.5f);
            fillTransform.localScale = new Vector3(InnerWidth * displayedRatio, .075f, 1f);
            fillTransform.localPosition = new Vector3(-InnerWidth * (1f - displayedRatio) * .5f, 0f, 0f);
            if (brain.IsDead) fillRenderer.enabled = false;
        }
    }
}
