using System.Collections.Generic;
using UnityEngine;

namespace ProjectLike.Phase1
{
    /// <summary>Lightweight pooled afterimages for a fast readable roguelike dash.</summary>
    public class DashVfxPool : MonoBehaviour
    {
        static DashVfxPool instance;
        int colorIndex;
        static readonly Color[] DashColors = { new Color(.65f, 1f, .06f), new Color(.15f, 1f, .25f), new Color(.02f, 1f, .65f), new Color(.02f, .85f, 1f) };
        readonly List<DashAfterimage> persistentImages = new List<DashAfterimage>();
        readonly Queue<DashAfterimage> available = new Queue<DashAfterimage>();

        public static DashVfxPool Instance
        {
            get
            {
                if (instance) return instance;
                var root = new GameObject("DashVfxPool");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<DashVfxPool>();
                return instance;
            }
        }

        public void Spawn(SpriteRenderer source, Vector2 direction, bool persistent = false, Color? tint = null)
        {
            if (!source || !source.sprite) return;
            DashAfterimage image;
            if (available.Count > 0) { image = available.Dequeue(); image.gameObject.SetActive(true); }
            else
            {
                var go = new GameObject("DashAfterimage"); go.transform.SetParent(transform);
                image = go.AddComponent<DashAfterimage>(); image.pool = this;
            }
            image.Play(source, direction, tint ?? DashColors[colorIndex++ % DashColors.Length], persistent);
            if (persistent) persistentImages.Add(image);
        }

        public static void ClearTimeStopTrail()
        {
            if (!instance) return;
            foreach (var image in instance.persistentImages)
                if (image && image.gameObject.activeSelf) instance.Return(image);
            instance.persistentImages.Clear();
        }

        internal void Return(DashAfterimage image) { image.gameObject.SetActive(false); available.Enqueue(image); }
    }

    public class DashAfterimage : MonoBehaviour
    {
        internal DashVfxPool pool;
        SpriteRenderer renderer;
        Color initialColor;
        Vector3 velocity;
        float elapsed;
        bool persistent;
        const float Duration = .22f;

        void Awake() { renderer = gameObject.AddComponent<SpriteRenderer>(); }
        public void Play(SpriteRenderer source, Vector2 direction, Color tint, bool holdUntilTimeResumes)
        {
            persistent = holdUntilTimeResumes;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.flipY = source.flipY;
            renderer.sprite = source.sprite; renderer.flipX = source.flipX; renderer.sortingOrder = source.sortingOrder - 1;
            initialColor = new Color(tint.r, tint.g, tint.b, persistent ? .6f : .48f);
            renderer.color = initialColor;
            transform.position = source.transform.position - (Vector3)direction * .04f;
            transform.rotation = source.transform.rotation; transform.localScale = source.transform.lossyScale;
            velocity = -(Vector3)direction * Random.Range(.2f, .55f);
            elapsed = 0;
        }
        void Update()
        {
            if (persistent) return;
            elapsed += Time.unscaledDeltaTime;
            var t = elapsed / Duration;
            transform.position += velocity * Time.unscaledDeltaTime;
            transform.localScale *= 1 + Time.unscaledDeltaTime * .65f;
            renderer.color = new Color(initialColor.r, initialColor.g, initialColor.b, initialColor.a * (1 - t));
            if (t >= 1) pool.Return(this);
        }
    }
}
