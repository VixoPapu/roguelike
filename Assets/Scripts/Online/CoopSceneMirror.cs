using System;
using System.Collections.Generic;
using System.Linq;
using ProjectLike.Phase1;
using ProjectLike.Phase2;
using ProjectLike.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLike.Online
{
    // Authoritative 2D presentation replication. Geometry/textures are cached once;
    // transforms, tint, live effect meshes and lifetimes follow host snapshots.
    // No client-side damage, loot rolls, physics or enemy AI can diverge.
    public sealed class CoopSceneMirror : MonoBehaviour
    {
        readonly HashSet<int> sentTextures = new HashSet<int>();
        readonly HashSet<int> sentGeometry = new HashSet<int>();
        readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();
        readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
        readonly Dictionary<int, RemoteDraw> draws = new Dictionary<int, RemoteDraw>();
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> voices = new List<AudioSource>();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        Mesh scratch;
        Shader shader;
        Font font;
        Vector3 cameraTarget;
        bool hasCameraTarget;
        bool downedView;
        float normalCameraSize;
        AudioLowPassFilter lowPass;
        readonly Dictionary<UnityEngine.Object, int> identities = new Dictionary<UnityEngine.Object, int>();
        int nextIdentity;
        int Identity(UnityEngine.Object value)
        {
            if (!identities.TryGetValue(value, out var id)) identities[value] = id = ++nextIdentity;
            return id;
        }
        sealed class RemoteDraw
        {
            public GameObject root;
            public Renderer renderer;
            public MeshFilter filter;
            public TextMesh text;
            public Text uiText;
            public Canvas canvas;
            public Mesh dynamic;
            public Material material;
            public Vector3 target, scale;
            public Quaternion rotation;
        }
        void Awake()
        {
            shader = Resources.Load<Shader>("CoopWorld");
            scratch = new Mesh();
            var hud = FindAnyObjectByType<PixelHUDCanvas>(); font = hud && hud.pixelFont ? hud.pixelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        public void InitializeClient()
        {
            Resources.Load<GameSfxBank>("Audio/GameSfxBank");
            Resources.LoadAll<ProjectLike.Phase12.PowerUpData>("PowerUps");
            Resources.LoadAll<ProjectLike.Phase2.WeaponData>("Weapons");
            RefreshClips();
            if (Camera.main) normalCameraSize = Camera.main.orthographicSize;
        }
        public void SetDownedView(bool active)
        {
            downedView = active;
            if (!Camera.main) return;
            if (normalCameraSize <= 0f) normalCameraSize = Camera.main.orthographicSize;
            lowPass = Camera.main.GetComponent<AudioLowPassFilter>() ?? Camera.main.gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.enabled = active; lowPass.cutoffFrequency = 650f; lowPass.lowpassResonanceQ = 1.15f;
        }
        void RefreshClips() { foreach (var clip in Resources.FindObjectsOfTypeAll<AudioClip>()) if (clip) clips[clip.name] = clip; }
        public CoopDraw[] Capture()
        {
            var result = new List<CoopDraw>();
            var players = FindObjectsByType<CoopPlayer>();
            foreach (var renderer in FindObjectsByType<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.GetComponentInParent<Canvas>()) continue;
                if (!players.Any(p => renderer.bounds.SqrDistance(p.transform.position) < 900f)) continue;
                var material = renderer.sharedMaterial;
                var entry = new CoopDraw { id = Identity(renderer), position = renderer.transform.position, scale = renderer.transform.lossyScale, rotation = renderer.transform.rotation, color = Color.white,
                    layer = renderer.sortingLayerID, order = renderer.sortingOrder, gray = material && material.shader && material.shader.name.Contains("TimeStop") };
                Texture texture = material && material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : Texture2D.whiteTexture;
                if (entry.gray && material.HasProperty("_TimeStopTexture")) texture = material.GetTexture("_TimeStopTexture");
                if (material && material.HasProperty("_Color")) entry.color = material.GetColor("_Color");
                var text = renderer.GetComponent<TextMesh>();
                if (text)
                {
                    entry.type = 2; entry.text = text.text; entry.color = text.color; entry.fontSize = text.fontSize; entry.characterSize = text.characterSize;
                    result.Add(entry); continue;
                }
                if (renderer is SpriteRenderer sprite)
                {
                    if (!sprite.sprite) continue;
                    var asset = sprite.sprite; entry.geometry = Identity(asset); texture = asset.texture;
                    entry.color *= sprite.color;
                    if (sprite.flipX) entry.scale.x *= -1f; if (sprite.flipY) entry.scale.y *= -1f;
                    if (sentGeometry.Add(entry.geometry))
                    {
                        var geometry = new CoopGeometry { id = entry.geometry, vertices = asset.vertices.Select(v => (Vector3)v).ToArray(), uv = asset.uv, triangles = asset.triangles.Select(i => (int)i).ToArray() };
                        CoopSession.Instance.Broadcast(new CoopMessage { kind = "geometry", geometry = geometry });
                    }
                }
                else
                {
                    entry.type = 1;
                    scratch.Clear();
                    if (renderer is LineRenderer line) line.BakeMesh(scratch, Camera.main, false);
                    else if (renderer is TrailRenderer trail) trail.BakeMesh(scratch, Camera.main, false);
                    else if (renderer is ParticleSystemRenderer particles)
                    {
                        particles.BakeMesh(scratch, Camera.main, ParticleSystemBakeMeshOptions.Default);
                        var system = particles.GetComponent<ParticleSystem>();
                        if (system && system.main.simulationSpace == ParticleSystemSimulationSpace.World)
                        { entry.position = Vector3.zero; entry.rotation = Quaternion.identity; entry.scale = Vector3.one; }
                    }
                    else
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable) continue;
                        entry.dynamicMesh = Geometry(filter.sharedMesh); // Includes changing hit flashes / procedural effects.
                    }
                    if (entry.dynamicMesh == null) entry.dynamicMesh = Geometry(scratch);
                    if (entry.dynamicMesh.vertices.Length == 0) continue;
                }
                entry.texture = RegisterTexture(texture ? texture : Texture2D.whiteTexture);
                result.Add(entry);
            }
            foreach (var text in FindObjectsByType<Text>())
            {
                var canvas = text.GetComponentInParent<Canvas>();
                if (!text.enabled || !canvas || canvas.renderMode != RenderMode.WorldSpace || !players.Any(p => (p.transform.position - text.transform.position).sqrMagnitude < 900f)) continue;
                var rect = text.rectTransform;
                var color = text.color;
                foreach (var group in text.GetComponentsInParent<CanvasGroup>()) color.a *= group.alpha;
                result.Add(new CoopDraw { type = 3, id = Identity(text), text = text.text, color = color, position = rect.TransformPoint(rect.rect.center), rotation = rect.rotation, scale = rect.lossyScale, textSize = rect.rect.size, fontSize = text.fontSize, alignment = (int)text.alignment, order = canvas.sortingOrder });
            }
            return result.ToArray();
        }
        static CoopGeometry Geometry(Mesh mesh) => new CoopGeometry { vertices = mesh.vertices, uv = mesh.uv, triangles = mesh.triangles, colors = mesh.colors };
        int RegisterTexture(Texture texture)
        {
            int id = Identity(texture);
            if (!sentTextures.Add(id)) return id;
            var temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D readable = null;
            try
            {
                Graphics.Blit(texture, temporary); RenderTexture.active = temporary;
                readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); readable.Apply();
                CoopSession.Instance.Broadcast(new CoopMessage { kind = "texture", texture = new CoopTexture { id = id, png = Convert.ToBase64String(readable.EncodeToPNG()) } });
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); if (readable) Destroy(readable); }
            return id;
        }
        public void ReceiveTexture(CoopTexture entry)
        {
            if (entry == null || textures.ContainsKey(entry.id)) return;
            var bytes = Convert.FromBase64String(entry.png);
            // Validate PNG dimensions before allocating GPU memory.
            if (bytes.Length < 24 || bytes[0] != 137 || bytes[1] != 80) throw new InvalidOperationException("Textura invalida");
            long width = (long)bytes[16] << 24 | (long)bytes[17] << 16 | (long)bytes[18] << 8 | bytes[19];
            long height = (long)bytes[20] << 24 | (long)bytes[21] << 16 | (long)bytes[22] << 8 | bytes[23];
            if (width < 1 || height < 1 || width * height > 16777216 || textures.Count > 4096) throw new InvalidOperationException("Textura fuera de limites");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            if (!texture.LoadImage(bytes)) { Destroy(texture); throw new InvalidOperationException("No se pudo leer la textura"); }
            textures[entry.id] = texture;
        }
        public void ReceiveGeometry(CoopGeometry geometry)
        {
            if (geometry == null || meshes.ContainsKey(geometry.id)) return;
            if (meshes.Count > 20000) throw new InvalidOperationException("Demasiadas mallas");
            var mesh = new Mesh(); Fill(mesh, geometry); meshes[geometry.id] = mesh;
        }
        static void Fill(Mesh mesh, CoopGeometry entry)
        {
            if (entry.vertices == null || entry.vertices.Length > 65535 || entry.triangles == null || entry.triangles.Length > 393210) throw new InvalidOperationException("Malla invalida");
            if (entry.triangles.Any(i => i < 0 || i >= entry.vertices.Length)) throw new InvalidOperationException("Indice de malla invalido");
            mesh.Clear(); mesh.vertices = entry.vertices;
            if (entry.uv != null && entry.uv.Length == entry.vertices.Length) mesh.uv = entry.uv;
            if (entry.colors != null && entry.colors.Length == entry.vertices.Length) mesh.colors = entry.colors;
            mesh.triangles = entry.triangles; mesh.RecalculateBounds();
        }
        public void Apply(CoopWorld world)
        {
            if (world.draws == null || world.draws.Length > 20000) throw new InvalidOperationException("Estado visual invalido");
            var alive = new HashSet<int>();
            foreach (var entry in world.draws)
            {
                alive.Add(entry.id);
                if (!draws.TryGetValue(entry.id, out var draw))
                {
                    draw = new RemoteDraw { root = new GameObject("Remote_" + entry.id) };
                    draw.root.transform.SetParent(transform, false); draw.root.transform.position = entry.position;
                    if (entry.type == 3)
                    {
                        draw.canvas = draw.root.AddComponent<Canvas>(); draw.canvas.renderMode = RenderMode.WorldSpace;
                        var label = new GameObject("Label", typeof(RectTransform)); label.transform.SetParent(draw.root.transform, false);
                        draw.uiText = label.AddComponent<Text>(); draw.uiText.font = font; draw.uiText.raycastTarget = false;
                    }
                    else if (entry.type == 2)
                    {
                        draw.text = draw.root.AddComponent<TextMesh>(); draw.text.font = font; draw.text.anchor = TextAnchor.MiddleCenter;
                        draw.renderer = draw.root.GetComponent<MeshRenderer>(); draw.renderer.sharedMaterial = font.material;
                    }
                    else
                    {
                        draw.filter = draw.root.AddComponent<MeshFilter>(); draw.renderer = draw.root.AddComponent<MeshRenderer>();
                        draw.material = new Material(shader); ownedMaterials.Add(draw.material); draw.renderer.sharedMaterial = draw.material;
                    }
                    draws[entry.id] = draw;
                }
                draw.target = entry.position; draw.rotation = entry.rotation; draw.scale = entry.scale;
                if (Vector3.Distance(draw.root.transform.position, draw.target) > 3f || world.timeStopped) draw.root.transform.position = draw.target;
                if (draw.renderer) { draw.renderer.sortingLayerID = entry.layer; draw.renderer.sortingOrder = entry.order; }
                if (draw.uiText)
                {
                    draw.canvas.sortingOrder = entry.order; draw.uiText.text = entry.text; draw.uiText.color = entry.color;
                    draw.uiText.fontSize = entry.fontSize; draw.uiText.alignment = (TextAnchor)entry.alignment; draw.uiText.rectTransform.sizeDelta = entry.textSize;
                }
                else if (draw.text)
                { draw.text.text = entry.text; draw.text.color = entry.gray ? Gray(entry.color) : entry.color; draw.text.fontSize = entry.fontSize; draw.text.characterSize = entry.characterSize; }
                else
                {
                    if (entry.type == 1 && entry.dynamicMesh != null)
                    {
                        if (!draw.dynamic) { draw.dynamic = new Mesh(); ownedMeshes.Add(draw.dynamic); }
                        Fill(draw.dynamic, entry.dynamicMesh); draw.filter.sharedMesh = draw.dynamic;
                    }
                    else if (meshes.TryGetValue(entry.geometry, out var geometry)) draw.filter.sharedMesh = geometry;
                    if (textures.TryGetValue(entry.texture, out var texture)) draw.material.mainTexture = texture;
                    draw.material.SetColor("_Color", entry.color); draw.material.SetFloat("_Gray", entry.gray ? 1f : 0f);
                }
            }
            foreach (var pair in draws.ToArray()) if (!alive.Contains(pair.Key))
            {
                if (pair.Value.material) { ownedMaterials.Remove(pair.Value.material); Destroy(pair.Value.material); }
                if (pair.Value.dynamic) { ownedMeshes.Remove(pair.Value.dynamic); Destroy(pair.Value.dynamic); }
                Destroy(pair.Value.root); draws.Remove(pair.Key);
            }
            var local = world.players?.FirstOrDefault(p => p.id == CoopSession.Instance.LocalId);
            if (local != null) { cameraTarget = new Vector3(local.position.x, local.position.y, Camera.main ? Camera.main.transform.position.z : -10f); hasCameraTarget = true; }
            if (Camera.main) Camera.main.backgroundColor = world.background;
        }
        static Color Gray(Color c) => new Color(c.grayscale, c.grayscale, c.grayscale, c.a);
        void LateUpdate()
        {
            if (!CoopSession.IsClient || !CoopSession.Playing) return;
            float alpha = 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime);
            foreach (var draw in draws.Values)
            {
                draw.root.transform.position = Vector3.Lerp(draw.root.transform.position, draw.target, alpha);
                draw.root.transform.rotation = Quaternion.Slerp(draw.root.transform.rotation, draw.rotation, alpha);
                draw.root.transform.localScale = draw.scale;
            }
            if (hasCameraTarget && Camera.main)
            {
                var camera = Camera.main; camera.orthographic = true;
                camera.transform.position = Vector3.Distance(camera.transform.position, cameraTarget) > 8f ? cameraTarget : Vector3.Lerp(camera.transform.position, cameraTarget, alpha);
                float size = (normalCameraSize <= 0f ? camera.orthographicSize : normalCameraSize) * (downedView ? .58f : 1f);
                camera.orthographicSize = Mathf.Lerp(camera.orthographicSize, size, alpha);
            }
        }
        public void PlaySound(CoopSound sound)
        {
            if (sound == null) return;
            if (!clips.TryGetValue(sound.clip, out var clip)) { RefreshClips(); if (!clips.TryGetValue(sound.clip, out clip)) return; }
            var source = voices.Find(s => !s.isPlaying);
            if (!source)
            {
                if (voices.Count >= 24) return;
                var root = new GameObject("RemoteSound"); root.transform.SetParent(transform); source = root.AddComponent<AudioSource>(); source.playOnAwake = false; voices.Add(source);
            }
            source.transform.position = sound.position; source.clip = clip; source.volume = Mathf.Clamp01(sound.volume); source.pitch = Mathf.Clamp(sound.pitch, .25f, 3f);
            source.spatialBlend = Mathf.Clamp01(sound.spatial); source.minDistance = 2.5f; source.maxDistance = 18f; source.rolloffMode = AudioRolloffMode.Linear; source.dopplerLevel = 0f; source.Play();
        }
        void OnDestroy()
        {
            if (scratch) Destroy(scratch);
            foreach (var texture in textures.Values) if (texture) Destroy(texture);
            foreach (var mesh in meshes.Values) if (mesh) Destroy(mesh);
            foreach (var mesh in ownedMeshes) if (mesh) Destroy(mesh);
            foreach (var material in ownedMaterials) if (material) Destroy(material);
        }
    }
}
