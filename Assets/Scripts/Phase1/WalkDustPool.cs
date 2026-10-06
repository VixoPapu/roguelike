using System.Collections.Generic;
using ProjectLike.Phase2;
using UnityEngine;

namespace ProjectLike.Phase1
{
    /// <summary>Polvo de pasos multicapa reutilizado mediante object pooling.</summary>
    public class WalkDustPool : MonoBehaviour
    {
        static WalkDustPool instance;
        readonly Stack<PooledWalkDust> available = new Stack<PooledWalkDust>();

        public static WalkDustPool Instance
        {
            get
            {
                if (instance) return instance;
                var root = new GameObject("WalkDustPool");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<WalkDustPool>();
                instance.Prewarm(20);
                return instance;
            }
        }

        public void Spawn(Vector2 position, Vector2 movement)
        {
            var dust = available.Count > 0 ? available.Pop() : CreateDust();
            dust.gameObject.SetActive(true);
            dust.Play(this, position, movement);
        }

        internal void Release(PooledWalkDust dust)
        {
            if (!dust || !dust.gameObject.activeSelf) return;
            dust.gameObject.SetActive(false);
            dust.transform.SetParent(transform, false);
            available.Push(dust);
        }

        void Prewarm(int amount)
        {
            for (var i = 0; i < amount; i++)
            {
                var dust = CreateDust();
                dust.gameObject.SetActive(false);
                available.Push(dust);
            }
        }

        PooledWalkDust CreateDust()
        {
            var go = new GameObject("PooledWalkDust");
            go.transform.SetParent(transform, false);
            return go.AddComponent<PooledWalkDust>();
        }
    }

    public class PooledWalkDust : MonoBehaviour
    {
        readonly SpriteRenderer[] motes = new SpriteRenderer[4];
        readonly Vector2[] velocities = new Vector2[4];
        readonly Vector3[] initialScales = new Vector3[4];
        WalkDustPool owner;
        float elapsed;
        const float Duration = .38f;

        void Awake()
        {
            for (var i = 0; i < motes.Length; i++)
            {
                var mote = new GameObject("DustMote_" + i);
                mote.transform.SetParent(transform, false);
                motes[i] = mote.AddComponent<SpriteRenderer>();
                motes[i].sprite = CombatFeedback.WhiteSprite;
                motes[i].sortingOrder = 7;
            }
        }

        internal void Play(WalkDustPool pool, Vector2 position, Vector2 movement)
        {
            owner = pool;
            transform.position = position;
            transform.rotation = Quaternion.identity;
            elapsed = 0f;
            var backward = movement.sqrMagnitude > .01f ? movement.normalized : Vector2.down;
            for (var i = 0; i < motes.Length; i++)
            {
                var renderer = motes[i];
                var side = new Vector2(-backward.y, backward.x) * Random.Range(-.55f, .55f);
                velocities[i] = backward * Random.Range(.15f, .42f) + side + Vector2.up * Random.Range(.04f, .18f);
                renderer.transform.localPosition = Random.insideUnitCircle * .055f;
                renderer.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                var size = Random.Range(.045f, .085f);
                initialScales[i] = new Vector3(size * Random.Range(.85f, 1.35f), size, 1f);
                renderer.transform.localScale = initialScales[i] * .45f;
                var shade = Random.Range(.58f, .78f);
                renderer.color = new Color(shade, shade * .94f, shade * .82f, Random.Range(.32f, .52f));
            }
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / Duration);
            for (var i = 0; i < motes.Length; i++)
            {
                var renderer = motes[i];
                renderer.transform.localPosition += (Vector3)velocities[i] * Time.deltaTime;
                renderer.transform.localScale = initialScales[i] * Mathf.Lerp(.45f, 1.65f, t);
                renderer.transform.Rotate(0f, 0f, (i % 2 == 0 ? 110f : -110f) * Time.deltaTime);
                var color = renderer.color;
                renderer.color = new Color(color.r, color.g, color.b, (1f - t) * .45f);
            }
            if (t >= 1f) owner.Release(this);
        }
    }
}
