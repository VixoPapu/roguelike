using ProjectLike.Phase1;
using ProjectLike.Phase2;
using ProjectLike.Phase12;
using UnityEngine;

namespace ProjectLike.Phase3
{
    public enum EliteModifier { Fire, Ice, Electric, Poison, Explosive, Vampiric, Teleportation, Duplication }

    [DisallowMultipleComponent]
    public sealed class EliteEnemy : MonoBehaviour
    {
        static Material auraMaterial;
        EnemyBrain brain;
        LineRenderer aura;
        float nextSpecial;
        bool duplicated, dying;
        int world;
        public EliteModifier Modifier { get; private set; }
        public bool IsElite => brain && !brain.IsDead;

        public static EliteModifier RandomModifier() => (EliteModifier)Random.Range(0, System.Enum.GetValues(typeof(EliteModifier)).Length);

        public void Configure(EnemyBrain owner, EliteModifier modifier, int worldNumber)
        {
            brain = owner; Modifier = modifier; world = Mathf.Max(1, worldNumber); nextSpecial = Time.time + Random.Range(3.8f, 5.8f);
            transform.localScale *= 1.16f;
            BuildAura();
        }

        void BuildAura()
        {
            if (!auraMaterial) auraMaterial = new Material(Shader.Find("Sprites/Default"));
            var ring = new GameObject("EliteAura_Purple"); ring.transform.SetParent(transform, false); ring.transform.localPosition = new Vector3(0f, -.03f, 0f);
            aura = ring.AddComponent<LineRenderer>(); aura.useWorldSpace = false; aura.loop = true; aura.positionCount = 25; aura.sharedMaterial = auraMaterial; aura.sortingOrder = 3; aura.widthMultiplier = .045f;
            aura.startColor = new Color(.72f, .16f, 1f, .82f); aura.endColor = new Color(.42f, .05f, .85f, .3f);
            for (var i = 0; i < aura.positionCount; i++) { var angle = i / 24f * Mathf.PI * 2f; aura.SetPosition(i, new Vector3(Mathf.Cos(angle) * .58f, Mathf.Sin(angle) * .34f, 0f)); }

            var particlesObject = new GameObject("EliteAuraParticles"); particlesObject.transform.SetParent(transform, false);
            var particles = particlesObject.AddComponent<ParticleSystem>();
            var main = particles.main; main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .7f); main.startSpeed = new ParticleSystem.MinMaxCurve(.05f, .18f); main.startSize = new ParticleSystem.MinMaxCurve(.025f, .06f); main.startColor = new ParticleSystem.MinMaxGradient(new Color(.78f, .25f, 1f), ModifierColor()); main.maxParticles = 26;
            var emission = particles.emission; emission.rateOverTime = 7f;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .48f;
            var renderer = particlesObject.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = auraMaterial; renderer.sortingOrder = 4;
        }

        Color ModifierColor() => Modifier == EliteModifier.Fire || Modifier == EliteModifier.Explosive ? new Color(1f, .2f, .05f) : Modifier == EliteModifier.Ice ? new Color(.2f, .75f, 1f) : Modifier == EliteModifier.Electric ? new Color(.9f, .85f, .15f) : Modifier == EliteModifier.Poison ? new Color(.2f, 1f, .35f) : Modifier == EliteModifier.Vampiric ? new Color(1f, .05f, .25f) : new Color(.65f, .15f, 1f);

        void Update()
        {
            if (!brain || brain.IsDead) return;
            if (aura) { var pulse = 1f + Mathf.Sin(Time.time * 5.5f) * .08f; aura.transform.localScale = Vector3.one * pulse; }
            if (Modifier == EliteModifier.Duplication && !duplicated && brain.CurrentHealth <= brain.RuntimeMaxHealth * .5f) Duplicate();
            if (Modifier == EliteModifier.Teleportation && Time.time >= nextSpecial) { nextSpecial = Time.time + Mathf.Max(3.4f, 5.6f - world * .12f); Teleport(); }
        }

        public void ApplyOnHit(PlayerController player, float dealtDamage)
        {
            if (!player) return;
            var status = player.GetComponent<ElementalStatusEffects>(); if (!status) status = player.gameObject.AddComponent<ElementalStatusEffects>();
            if (Modifier == EliteModifier.Fire) status.Burn(gameObject, 3f, Mathf.Max(.6f, dealtDamage * .3f));
            else if (Modifier == EliteModifier.Ice) status.Ice(1, 3);
            else if (Modifier == EliteModifier.Electric) status.Shock(gameObject, 2.5f, 0f);
            else if (Modifier == EliteModifier.Poison) status.Poison(gameObject, 4.5f, Mathf.Max(.45f, dealtDamage * .18f));
            else if (Modifier == EliteModifier.Vampiric) brain.Heal(Mathf.Max(.5f, dealtDamage * .25f));
        }

        public void OnOwnerDeath()
        {
            if (dying) return; dying = true;
            if (Modifier != EliteModifier.Explosive) return;
            CombatVfxPool.Instance.SpawnEnemyAbilityBurst(transform.position, EnemyAttackStyle.HeavySmash, new Color(.75f, .1f, 1f));
            foreach (var player in Object.FindObjectsByType<PlayerController>())
                if (player && Vector2.Distance(transform.position, player.transform.position) <= 2.2f) player.TakeDamage(2.2f + world * .35f, transform.position);
        }

        void Teleport()
        {
            var target = ProjectLike.Online.CoopSession.IsHost ? ProjectLike.Online.CoopSession.Instance.NearestPlayer(transform.position) : null;
            var player = ProjectLike.Online.CoopSession.IsHost ? target ? target.GetComponent<PlayerController>() : null : Object.FindAnyObjectByType<PlayerController>(); if (!player) return;
            var destination = (Vector2)player.transform.position + Random.insideUnitCircle.normalized * Random.Range(2.2f, 3.6f);
            if (brain.OwningRoom) destination = brain.OwningRoom.ClampInside(destination, 1f);
            EnemyVfx.Teleport(transform.position); transform.position = destination; EnemyVfx.Teleport(destination);
        }

        void Duplicate()
        {
            duplicated = true;
            var clone = new GameObject(brain.data.enemyName + " Duplicado"); clone.transform.position = transform.position + (Vector3)Random.insideUnitCircle.normalized * .8f; clone.AddComponent<SpriteRenderer>();
            var cloneBrain = clone.AddComponent<EnemyBrain>(); cloneBrain.Configure(brain.data, brain.OwningRoom, .55f + world * .03f, .62f + world * .025f, 1f, 1.08f, 0); clone.transform.localScale *= .88f; clone.AddComponent<EnemySpriteAnimator>();
            if (brain.OwningRoom) brain.OwningRoom.RegisterEnemy(cloneBrain);
            CombatVfxPool.Instance.SpawnEnemyAbilityBurst(clone.transform.position, EnemyAttackStyle.ArcaneBurst, new Color(.7f, .18f, 1f));
        }
    }
}
