using ProjectLike.Phase1;
using ProjectLike.Phase2;
using UnityEngine;

namespace ProjectLike.Online
{
    // World-space ally UI. Sprite renderers are also mirrored to guests by
    // CoopSceneMirror, so every player sees the same green health bars.
    public sealed class CoopAllyStatus : MonoBehaviour
    {
        SpriteRenderer healthFill, reviveFill;
        TextMesh reviveText;
        PlayerStats stats;
        CoopPlayer player;
        public bool Downed { get; private set; }
        public float ReviveProgress { get; private set; }

        void Awake()
        {
            stats = GetComponent<PlayerStats>(); player = GetComponent<CoopPlayer>();
            healthFill = Bar("AllyHealth", .86f, new Color(.22f, .95f, .38f));
            reviveFill = Bar("ReviveProgress", .58f, new Color(1f, .79f, .16f));
            reviveText = new GameObject("ReviveText").AddComponent<TextMesh>();
            reviveText.transform.SetParent(transform, false); reviveText.transform.localPosition = Vector3.up * .72f;
            reviveText.anchor = TextAnchor.MiddleCenter; reviveText.alignment = TextAlignment.Center; reviveText.fontSize = 34; reviveText.characterSize = .045f;
            reviveText.color = new Color(1f, .85f, .28f); reviveText.gameObject.SetActive(false);
        }
        SpriteRenderer Bar(string name, float y, Color color)
        {
            var outline = new GameObject(name + "Outline").AddComponent<SpriteRenderer>();
            outline.transform.SetParent(transform, false); outline.transform.localPosition = Vector3.up * y; outline.sprite = CombatFeedback.WhiteSprite;
            outline.color = new Color(.03f, .04f, .05f, .95f); outline.sortingOrder = 161; outline.transform.localScale = new Vector3(.92f, .13f, 1f);
            var fill = new GameObject(name + "Fill").AddComponent<SpriteRenderer>();
            fill.transform.SetParent(transform, false); fill.transform.localPosition = Vector3.up * y; fill.sprite = CombatFeedback.WhiteSprite;
            fill.color = color; fill.sortingOrder = 162; return fill;
        }
        public void SetDowned(bool value) { Downed = value; if (!value) SetRevive(0f, 0); }
        public void SetRevive(float progress, int reviverId)
        {
            ReviveProgress = Mathf.Clamp01(progress);
            bool active = Downed;
            reviveFill.gameObject.SetActive(active); reviveText.gameObject.SetActive(active);
            reviveFill.transform.localScale = new Vector3(.88f * (active ? Mathf.Max(.02f, ReviveProgress) : 0f), .08f, 1f);
            reviveFill.transform.localPosition = new Vector3(-.44f + .44f * ReviveProgress, .58f, 0f);
            reviveText.text = active ? (ReviveProgress > 0f ? "REVIVIENDO " + Mathf.RoundToInt(ReviveProgress * 100f) + "%" : "DERRIBADO") : "";
        }
        void LateUpdate()
        {
            if (!stats) return;
            float amount = Downed ? 0f : Mathf.Clamp01(stats.CurrentHealth / Mathf.Max(1f, stats.maxHealth));
            healthFill.transform.localScale = new Vector3(.88f * Mathf.Max(.02f, amount), .08f, 1f);
            healthFill.transform.localPosition = new Vector3(-.44f + .44f * amount, .86f, 0f);
        }
    }
}
