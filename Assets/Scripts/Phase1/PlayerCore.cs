using System;
using ProjectLike.Online;
using ProjectLike.Phase2;
using ProjectLike.Phase3;
using ProjectLike.Phase8;
using ProjectLike.Phase11;
using ProjectLike.Phase12;
using ProjectLike.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectLike.Phase1
{
    [Serializable]
    public class PlayerStats : MonoBehaviour
    {
        [Min(1)] public float maxHealth = 15f;
        public float moveSpeed = 5f;
        public float damage = 10f;
        public float attackSpeed = 4f;
        [Range(0, 1)] public float criticalChance = .1f;
        public float defense = 0f;
        public float projectileSpeed = 8f;
        public float range = 6f;
        public float cooldown = .25f;
        public float luck = 0f;
        public int coins = 0;
        public int experience;
        [Header("Defensa y energía")]
        public float maxShield = 5f;
        public float maxEnergy = 200f;
        [Min(0)] public float energyRegenDelay = 5f;
        [Min(0.05f)] public float energyRegenInterval = 1f;
        [Min(1)] public float energyRegenAmount = 10f;
        public float CurrentHealth { get; private set; }
        public float CurrentShield { get; private set; }
        public float CurrentEnergy { get; private set; }
        float nextEnergyRegenTime;
        public event Action<float, float> HealthChanged;
        public event Action Died;
        void Awake()
        {
            // Cada run comienza siempre con estos valores aunque una escena antigua
            // conserve overrides serializados de versiones anteriores.
            maxHealth = 15f;
            maxShield = 5f;
            maxEnergy = 200f;
            coins = 0;
            CurrentHealth = maxHealth;
            CurrentShield = maxShield;
            CurrentEnergy = maxEnergy;
        }
        void Update()
        {
            if (CurrentEnergy >= maxEnergy || Time.time < nextEnergyRegenTime) return;
            var runtime = GetComponent<PowerUpRuntime>();
            CurrentEnergy = Mathf.Min(maxEnergy, CurrentEnergy + energyRegenAmount * (runtime ? runtime.EnergyRegenMultiplier : 1f));
            nextEnergyRegenTime = Time.time + energyRegenInterval;
        }
        public void ResetHealth() { CurrentHealth = maxHealth; CurrentShield = maxShield; CurrentEnergy = maxEnergy; nextEnergyRegenTime = 0; HealthChanged?.Invoke(CurrentHealth, maxHealth); }
        public void ResetForNewRun()
        {
            maxHealth = 15f; maxShield = 5f; maxEnergy = 200f;
            moveSpeed = 5f; damage = 10f; attackSpeed = 4f; criticalChance = .1f;
            defense = 0f; projectileSpeed = 8f; range = 6f; cooldown = .25f; luck = 0f;
            coins = 0; experience = 0; ResetHealth();
        }
        public void RestoreVitals() { ResetHealth(); }
        // Used by cooperative revives.  It deliberately does not refill the
        // player: being picked up is a second chance, not a free heal.
        public void Revive(float healthFraction)
        {
            CurrentHealth = Mathf.Clamp(maxHealth * Mathf.Clamp01(healthFraction), 1f, maxHealth);
            CurrentShield = 0f;
            nextEnergyRegenTime = Time.time + energyRegenDelay;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }
        public void Heal(float amount) { if (amount <= 0 || CurrentHealth <= 0) return; var runtime=GetComponent<PowerUpRuntime>();if(runtime)amount=runtime.ModifyHealing(amount);CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount); HealthChanged?.Invoke(CurrentHealth, maxHealth); }
        public void RestoreEnergy(float amount) { if (amount > 0) CurrentEnergy = Mathf.Min(maxEnergy, CurrentEnergy + amount); }
        // Health payments bypass shields/armour and cannot kill the player.
        public void SpendHealth(float amount) { if (CurrentHealth <= 0f || amount <= 0f) return; CurrentHealth = Mathf.Max(1f, CurrentHealth - amount); HealthChanged?.Invoke(CurrentHealth, maxHealth); }
        public void AddShield(float amount) { if (amount > 0) CurrentShield = Mathf.Min(maxShield, CurrentShield + amount); }
        public void RemoveShield(float amount) { if (amount > 0) CurrentShield = Mathf.Max(0f, CurrentShield - amount); }
        public void ModifyMaxHealth(float amount) { maxHealth = Mathf.Max(1, maxHealth + amount); CurrentHealth = Mathf.Min(CurrentHealth, maxHealth); if (amount > 0) CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount); HealthChanged?.Invoke(CurrentHealth, maxHealth); }
        public bool TrySpendEnergy(float amount)
        {
            if (amount <= 0) return true;
            if (CurrentEnergy < amount) return false;
            CurrentEnergy -= amount;
            nextEnergyRegenTime = Time.time + energyRegenDelay;
            return true;
        }
        public bool ReceiveDamage(float amount)
        {
            if (amount <= 0 || CurrentHealth <= 0) return false;
            var runtime = GetComponent<PowerUpRuntime>(); if (runtime) amount = runtime.ModifyIncomingDamage(amount);
            if (amount <= 0f) return false;
            var reduced = Mathf.Max(1, amount - defense);
            var absorbed = Mathf.Min(CurrentShield, reduced); CurrentShield -= absorbed; reduced -= absorbed;
            CurrentHealth = Mathf.Max(0, CurrentHealth - reduced);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            if (CurrentHealth <= 0)
            {
                if (runtime && runtime.TryPreventDeath(out var restored)) { CurrentHealth = Mathf.Max(1f, restored); HealthChanged?.Invoke(CurrentHealth, maxHealth); }
                else Died?.Invoke();
            }
            return true;
        }
    }

    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")][Min(0)] public float acceleration = 45f;
        [Min(0)] public float deceleration = 55f;
        [Header("Dash")][Min(0)] public float dashSpeed = 15f;
        [Min(0)] public float dashEnergyCost = 25f;
        [Min(0)] public float dashDuration = .12f;
        [Min(0)] public float dashCooldown = .65f;
        [Min(0)] public float dashInvulnerability = .16f;
        [Header("Damage")][Min(0)] public float hitInvulnerability = .55f;
        [Min(0)] public float knockback = 4f;
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        public bool IsInvulnerable => TimeStopAbility.PlayerTime < invulnerableUntil;
        public float MovementAmount => moveInput.magnitude;
        public Vector2 MovementDirection => moveInput.sqrMagnitude > .01f ? moveInput.normalized : Vector2.zero;
        public bool IsDashing => dashing;
        public bool IsDead => dead;
        public bool IsInvisible { get; private set; }
        Rigidbody2D body; PlayerStats stats; SpriteRenderer sprite; Animator animator; PlayerVisuals visuals;
        Vector2 moveInput, velocity, dashDirection; float invulnerableUntil, nextDash, nextAttack, nextDashTrail, nextFootstep; float dashEnd;
        bool dashing, dead;
        void Awake()
        {
            body = GetComponent<Rigidbody2D>(); stats = GetComponent<PlayerStats>(); sprite = GetComponentInChildren<SpriteRenderer>(); animator = GetComponentInChildren<Animator>(); visuals = GetComponent<PlayerVisuals>();
            if (!body) body = gameObject.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.freezeRotation = true; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (!stats) stats = gameObject.AddComponent<PlayerStats>(); stats.Died += Die;
            if (!GetComponent<PlayerConsumables>()) gameObject.AddComponent<PlayerConsumables>();
            if (!GetComponent<Collider2D>()) { var c = gameObject.AddComponent<CapsuleCollider2D>(); c.size = new Vector2(.65f, .65f); }
            PlayerEnemyCollisionRules.IgnoreForPlayer(this);
        }
        void Update()
        {
            if (dead || CoopInput.Blocked(gameObject) || Time.timeScale <= 0f && !TimeStopAbility.IsActive) { moveInput = Vector2.zero; return; }
            var controls = CoopInput.For(gameObject);
            moveInput = controls.move;
            AimDirection = controls.aim;
            if (controls.Down(CoopButtons.Attack)) Attack();
            if (controls.Down(CoopButtons.Dash)) TryDash();
            if (dashing && TimeStopAbility.PlayerTime >= dashEnd) { dashing = false; velocity = Vector2.zero; }
            if (sprite && AimDirection.x != 0) sprite.flipX = AimDirection.x < 0;
            if (TimeStopAbility.IsActive) MoveInStoppedTime();
            if (!dashing && moveInput.sqrMagnitude > .15f && TimeStopAbility.PlayerTime >= nextFootstep) { nextFootstep = TimeStopAbility.PlayerTime + .3f; GameSfx.Play(SfxCue.Footstep, transform.position, .22f); }
            if (animator) { animator.SetFloat("Speed", velocity.magnitude); animator.SetBool("Dashing", dashing); }
        }
        void FixedUpdate()
        {
            if (dead || CoopInput.Blocked(gameObject)) return;
            if (dashing) { body.MovePosition(body.position + dashDirection * dashSpeed * Time.fixedDeltaTime); if(TimeStopAbility.PlayerTime>=nextDashTrail){if(visuals)visuals.SpawnDashGhost(dashDirection);nextDashTrail=TimeStopAbility.PlayerTime+.035f;} }
            else { var powers=GetComponent<PowerUpRuntime>();var status=GetComponent<ElementalStatusEffects>();var target = moveInput * stats.moveSpeed*(powers?powers.CurrentMoveMultiplier:1f)*(status?status.SpeedMultiplier:1f); velocity = Vector2.MoveTowards(velocity, target, (target.sqrMagnitude > velocity.sqrMagnitude ? acceleration : deceleration) * Time.fixedDeltaTime); body.MovePosition(body.position + velocity * Time.fixedDeltaTime); }
        }
        readonly RaycastHit2D[] frozenMoveHits = new RaycastHit2D[32];
        void MoveInStoppedTime()
        {
            var powers = GetComponent<PowerUpRuntime>();
            var motion = dashing ? dashDirection * dashSpeed : moveInput * stats.moveSpeed * (powers ? powers.CurrentMoveMultiplier : 1f);
            var delta = motion * Time.unscaledDeltaTime;
            if (delta.sqrMagnitude < .000001f) return;
            var distance = delta.magnitude;
            var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = Physics2D.GetLayerCollisionMask(gameObject.layer) };
            var count = body.Cast(delta.normalized, filter, frozenMoveHits, distance + .025f);
            for (var i = 0; i < count; i++)
            {
                var hit = frozenMoveHits[i];
                if (!hit.collider || hit.collider.GetComponentInParent<EnemyBrain>() || hit.collider.transform.IsChildOf(transform)
                    || CoopSession.Active && hit.collider.GetComponentInParent<PlayerController>()) continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - .025f));
            }
            body.position += delta.normalized * distance;
            transform.position = new Vector3(body.position.x, body.position.y, transform.position.z);
            Physics2D.SyncTransforms();
        }

        Vector2 ReadMove() { var k = Keyboard.current; var g = Gamepad.current; Vector2 v = g != null ? g.leftStick.ReadValue() : Vector2.zero; if (k != null) v += new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0), (k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0)); return Vector2.ClampMagnitude(v, 1); }
        Vector2 ReadAim()
        {
            var g = Gamepad.current; if (g != null && g.rightStick.ReadValue().sqrMagnitude > .08f) return g.rightStick.ReadValue().normalized;
            var mouse = Mouse.current; if (mouse != null && Camera.main) { var p = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue()); var d = (Vector2)(p - transform.position); if (d.sqrMagnitude > .04f) return d.normalized; }
            return moveInput.sqrMagnitude > .04f ? moveInput : AimDirection;
        }
        public void TryDash() { var powers=GetComponent<PowerUpRuntime>();var mustPay=!powers||powers.DashCostsEnergy;if ((TimeStopAbility.PlayerTime < nextDash && !(powers&&powers.TryUseBonusDash())) || dashing || (mustPay&&stats&&!stats.TrySpendEnergy(dashEnergyCost))) return; dashDirection = moveInput.sqrMagnitude > .04f ? moveInput.normalized : AimDirection; dashing=true;var distance=powers?powers.DashDistanceMultiplier:1f; dashEnd=TimeStopAbility.PlayerTime+dashDuration*distance; nextDash=TimeStopAbility.PlayerTime+dashCooldown*(powers?powers.DashCooldownMultiplier:1f); nextDashTrail=TimeStopAbility.PlayerTime; invulnerableUntil=Mathf.Max(invulnerableUntil, TimeStopAbility.PlayerTime+dashInvulnerability); GameSfx.Play(SfxCue.Dash, transform.position, .65f); if(visuals)visuals.Dash();if(powers)powers.OnDash(); }
        public void GrantInvulnerability(float duration) { invulnerableUntil = Mathf.Max(invulnerableUntil, TimeStopAbility.PlayerTime + Mathf.Max(0f, duration)); }
        public void SetInvisible(bool value)
        {
            IsInvisible = value;
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                var color = renderer.color;
                color.a = value ? .28f : 1f;
                renderer.color = color;
            }
        }
        public void ReviveForCoop(bool restoreVitals = true)
        {
            dead = false; dashing = false; velocity = Vector2.zero; moveInput = Vector2.zero;
            body.simulated = true; body.linearVelocity = Vector2.zero;
            if (restoreVitals) stats.RestoreVitals(); SetInvisible(false);
            if (animator) { animator.Rebind(); animator.Update(0f); }
            if (visuals) visuals.RestoreForNewRun();
        }
        public void RestartForNewRun()
        {
            StopAllCoroutines(); dead = false; dashing = false; velocity = Vector2.zero; moveInput = Vector2.zero;
            invulnerableUntil = nextDash = nextAttack = nextDashTrail = dashEnd = 0f;
            body.simulated = true; body.linearVelocity = Vector2.zero; body.angularVelocity = 0f;
            SetInvisible(false); stats.ResetForNewRun();
            GetComponent<PlayerConsumables>()?.ResetForNewRun();
            GetComponent<ProjectLike.Phase8.PlayerInventory>()?.ResetForNewRun();
            GetComponent<PlayerPowerUps>()?.ResetForNewRun();
            GetComponent<WeaponController>()?.ResetForNewRun();
            if (animator) { animator.Rebind(); animator.Update(0f); }
            if (visuals) visuals.RestoreForNewRun();
        }
        void Attack() { if(TimeStopAbility.PlayerTime<nextAttack)return; nextAttack=TimeStopAbility.PlayerTime+1f/Mathf.Max(.01f,stats.attackSpeed); if(animator)animator.SetTrigger("Attack"); if(visuals)visuals.Attack(); Debug.DrawRay(transform.position,AimDirection*stats.range,Color.yellow, .15f); }
        public void TakeDamage(float amount, Vector2 sourcePosition) { if (dead || IsInvulnerable) return; var status=GetComponent<ElementalStatusEffects>();if(status)amount*=status.DamageTakenMultiplier; var before=stats.CurrentHealth+stats.CurrentShield;var shieldBefore=stats.CurrentShield; if (!stats.ReceiveDamage(amount)) return; var received=Mathf.Max(0f,before-stats.CurrentHealth-stats.CurrentShield); var away=((Vector2)transform.position-sourcePosition).normalized; if(away.sqrMagnitude<.01f)away=Vector2.up; GameSfx.Play(shieldBefore>0f?SfxCue.Shield:SfxCue.PlayerHurt,transform.position,.75f); CombatVfxPool.Instance.SpawnPlayerDamage(transform.position,received,away); invulnerableUntil=TimeStopAbility.PlayerTime+hitInvulnerability;var powers=GetComponent<PowerUpRuntime>();var force=(powers&&powers.Has("corazon_del_gigante")) ? .55f : 1f; body.AddForce(away*knockback*force, ForceMode2D.Impulse);if(powers)powers.OnDamaged(received,sourcePosition,shieldBefore>0f&&stats.CurrentShield<=0f); if(animator) animator.SetTrigger("Hit"); if(visuals)visuals.Hit(); }
        void Die()
        {
            dead = true; velocity = Vector2.zero; dashing = false; body.simulated = false;
            GameSfx.Play(SfxCue.PlayerDeath, transform.position, .9f);
            if (animator) animator.SetTrigger("Die");
            if (visuals) visuals.Die();
            if (CoopSession.IsHost) CoopSession.Instance.PlayerDowned(this);
            else if (!CoopSession.Active) PlayerDeathSequence.Begin(this);
        }
    }

    public class Phase1HUD : MonoBehaviour
    {
        PlayerStats stats; PlayerController controller; GUIStyle style;
        void Start(){stats=FindAnyObjectByType<PlayerStats>(); controller=FindAnyObjectByType<PlayerController>(); style=new GUIStyle(GUI.skin.label){fontSize=16,normal={textColor=Color.white}};}
        void OnGUI(){if(!stats||!controller)return; GUI.Box(new Rect(12,12,290,40),""); GUI.Label(new Rect(24,20,260,22),$"HP {stats.CurrentHealth:0}/{stats.maxHealth:0}",style);}
    }

    public interface IInteractable { string Prompt { get; } void Interact(GameObject player); }
    public abstract class Interactable : MonoBehaviour, IInteractable
    {
        public string prompt = "Interact"; public string Prompt => prompt; public abstract void Interact(GameObject player);
        void OnTriggerEnter2D(Collider2D other) { if (other.GetComponent<PlayerController>()) Debug.Log("[Interact] " + prompt); }
    }
    public class Pickup : Interactable { public float health = 20; public override void Interact(GameObject player) { var s=player.GetComponent<PlayerStats>(); if(s) { s.ResetHealth(); Destroy(gameObject); } } }
    public class Door : Interactable { public override void Interact(GameObject player) { Debug.Log("Door used"); } }
    public class NPC : Interactable { [TextArea] public string dialogue="Hello, adventurer!"; public override void Interact(GameObject player) { Debug.Log(dialogue); } }
    public class ShopItem : Interactable { public int price=10; public override void Interact(GameObject player) { var s=player.GetComponent<PlayerStats>(); if(s&&s.coins>=price){s.coins-=price;Debug.Log("Purchased item for "+price);}else Debug.Log("Not enough coins"); } }
    public class PlayerInteraction : MonoBehaviour
    {
        public float radius=1.15f; public LayerMask interactionLayers=~0;
        public IInteractable FindTarget()
        {
            IInteractable closest = null;
            var distance = float.PositiveInfinity;
            foreach (var hit in Physics2D.OverlapCircleAll(transform.position, radius, interactionLayers))
            {
                var candidate = hit.GetComponentInParent<IInteractable>();
                if (candidate == null) continue;
                if (candidate is LootPickup loot && (!loot.CanCollectBy(gameObject) || loot.data.autoPickup)) continue;
                var next = ((Vector2)hit.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (next >= distance) continue;
                distance = next;
                closest = candidate;
            }
            return closest;
        }
        void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (CoopSession.IsHost && CoopSession.Instance.IsReviveInteraction(gameObject)) return;
            bool pressed = CoopInput.For(gameObject).Down(CoopButtons.Interact);
            if (pressed) FindTarget()?.Interact(gameObject);
        }
        void OnDrawGizmosSelected(){Gizmos.color=Color.cyan;Gizmos.DrawWireSphere(transform.position,radius);}
    }

    public class PlayerHUDCanvas : MonoBehaviour
    {
        PlayerStats stats; PlayerInteraction interaction; Text healthText, shieldText, energyText, promptText; Image healthFill, shieldFill, energyFill;
        Canvas canvas;
        void Awake()
        {
            stats=FindAnyObjectByType<PlayerStats>(); interaction=FindAnyObjectByType<PlayerInteraction>();
            canvas=GetComponent<Canvas>(); if(!canvas)canvas=gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=100;
            var scaler=gameObject.GetComponent<CanvasScaler>()??gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(960,540);
            healthFill=MakeBar("HealthBar",new Vector2(48,-18),new Color(.82f,.12f,.16f),out healthText,"♥");
            shieldFill=MakeBar("ShieldBar",new Vector2(48,-48),new Color(.62f,.66f,.72f),out shieldText,"⬟");
            energyFill=MakeBar("EnergyBar",new Vector2(48,-78),new Color(.08f,.38f,.82f),out energyText,"✦");
            promptText=MakeText("InteractionPrompt",new Vector2(0,70),new Vector2(500,42),22,Color.white); promptText.alignment=TextAnchor.MiddleCenter;
        }
        Text MakeText(string name,Vector2 pos,Vector2 size,int fontSize,Color color){var go=new GameObject(name);go.transform.SetParent(transform,false);var t=go.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=fontSize;t.color=color; t.rectTransform.anchorMin=new Vector2(.5f,0);t.rectTransform.anchorMax=new Vector2(.5f,0);t.rectTransform.pivot=new Vector2(.5f,0);t.rectTransform.anchoredPosition=pos;t.rectTransform.sizeDelta=size;return t;}
        Image MakeBar(string name,Vector2 pos,Color color,out Text value,string icon){var iconText=MakeText(name+"Icon",new Vector2(24,pos.y-2),new Vector2(28,26),22,color);iconText.text=icon;var go=new GameObject(name);go.transform.SetParent(transform,false);var bg=go.AddComponent<Image>();bg.color=new Color(.10f,.10f,.12f,.92f);bg.rectTransform.anchorMin=new Vector2(0,1);bg.rectTransform.anchorMax=new Vector2(0,1);bg.rectTransform.pivot=new Vector2(0,1);bg.rectTransform.anchoredPosition=pos;bg.rectTransform.sizeDelta=new Vector2(184,24);var fillGo=new GameObject("Fill");fillGo.transform.SetParent(go.transform,false);var fill=fillGo.AddComponent<Image>();fill.color=color;fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;fill.rectTransform.sizeDelta=Vector2.zero;value=MakeText(name+"Value",new Vector2(140,pos.y-2),new Vector2(180,26),18,Color.white);value.alignment=TextAnchor.MiddleCenter;return fill;}
        void Update(){if(stats){healthText.text=$"{stats.CurrentHealth:0}/{stats.maxHealth:0}";shieldText.text=$"{stats.CurrentShield:0}/{stats.maxShield:0}";energyText.text=$"{stats.CurrentEnergy:0}/{stats.maxEnergy:0}";healthFill.fillAmount=stats.maxHealth>0?stats.CurrentHealth/stats.maxHealth:0;shieldFill.fillAmount=stats.maxShield>0?stats.CurrentShield/stats.maxShield:0;energyFill.fillAmount=stats.maxEnergy>0?stats.CurrentEnergy/stats.maxEnergy:0;}if(interaction){var hits=Physics2D.OverlapCircleAll(interaction.transform.position,interaction.radius,interaction.interactionLayers);string prompt="";foreach(var h in hits){var i=h.GetComponentInParent<IInteractable>();if(i!=null){prompt="[ E / A ]  "+i.Prompt;break;}}promptText.text=prompt;}}
    }

    public class PlayerVisuals : MonoBehaviour
    {
        [Header("Walk squash")]
        [Min(1f)] public float walkCycleSpeed = 11f;
        [Range(0f, .3f)] public float verticalSquash = .11f;
        [Range(0f, .25f)] public float horizontalStretch = .055f;
        [Range(0f, 15f)] public float walkTilt = 5f;
        [Range(0f, .2f)] public float walkBob = .045f;

        SpriteRenderer sourceRenderer;
        SpriteRenderer animatedRenderer;
        Transform visualTransform;
        PlayerController controller;
        Vector3 baseScale;
        Vector3 basePosition;
        Color baseColor;
        float effectUntil;
        float dashUntil;
        float attackUntil;
        float walkPhase;
        float nextWalkDust;
        int dustFoot;
        bool dying;
        bool clonedRenderer;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            sourceRenderer = GetComponent<SpriteRenderer>();
            if (!sourceRenderer) sourceRenderer = GetComponentInChildren<SpriteRenderer>();
            if (!sourceRenderer) return;

            if (sourceRenderer.transform == transform)
            {
                var existingVisual = transform.Find("PlayerAnimatedVisual");
                var visual = existingVisual ? existingVisual.gameObject : new GameObject("PlayerAnimatedVisual");
                visual.transform.SetParent(transform, false);
                animatedRenderer = visual.GetComponent<SpriteRenderer>() ?? visual.AddComponent<SpriteRenderer>();
                animatedRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
                animatedRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                animatedRenderer.sortingOrder = sourceRenderer.sortingOrder;
                animatedRenderer.maskInteraction = sourceRenderer.maskInteraction;
                animatedRenderer.sprite = sourceRenderer.sprite;
                animatedRenderer.color = sourceRenderer.color;
                animatedRenderer.flipX = sourceRenderer.flipX;
                animatedRenderer.flipY = sourceRenderer.flipY;
                sourceRenderer.enabled = false;
                clonedRenderer = true;
            }
            else animatedRenderer = sourceRenderer;

            visualTransform = animatedRenderer.transform;
            baseScale = visualTransform.localScale;
            basePosition = visualTransform.localPosition;
            baseColor = animatedRenderer.color;
        }

        void Update()
        {
            if (!animatedRenderer || !visualTransform || dying) return;
            if (clonedRenderer)
            {
                animatedRenderer.sprite = sourceRenderer.sprite;
                animatedRenderer.flipX = sourceRenderer.flipX;
                animatedRenderer.flipY = sourceRenderer.flipY;
            }

            var movement = controller ? controller.MovementAmount : 0f;
            var walking = movement > .08f && (!controller || !controller.IsDashing);
            var targetScale = baseScale;
            var targetPosition = basePosition;
            var targetRotation = 0f;

            if (walking)
            {
                walkPhase += TimeStopAbility.PlayerDelta * walkCycleSpeed * Mathf.Lerp(.75f, 1.15f, movement);
                var side = Mathf.Sin(walkPhase);
                var contact = Mathf.Abs(side);
                targetScale.x *= 1f + horizontalStretch * contact;
                targetScale.y *= 1f - verticalSquash * contact;
                targetPosition.y += walkBob * contact;
                targetRotation = side * walkTilt;
                if (controller && TimeStopAbility.PlayerTime >= nextWalkDust)
                {
                    nextWalkDust = TimeStopAbility.PlayerTime + Mathf.Lerp(.16f, .105f, movement);
                    dustFoot++;
                    var direction = controller.MovementDirection;
                    var perpendicular = new Vector2(-direction.y, direction.x);
                    var footOffset = perpendicular * (dustFoot % 2 == 0 ? .11f : -.11f);
                    WalkDustPool.Instance.Spawn((Vector2)transform.position + Vector2.down * .32f + footOffset, -direction);
                }
            }
            else
            {
                walkPhase = Mathf.MoveTowards(walkPhase, 0f, TimeStopAbility.PlayerDelta * walkCycleSpeed);
                var breathe = Mathf.Sin(TimeStopAbility.PlayerTime * 3f) * .012f;
                targetScale.x *= 1f - breathe * .5f;
                targetScale.y *= 1f + breathe;
            }

            if (TimeStopAbility.PlayerTime < attackUntil)
            {
                targetScale.x *= 1.14f;
                targetScale.y *= .92f;
            }

            var response = 1f - Mathf.Exp(-22f * TimeStopAbility.PlayerDelta);
            visualTransform.localScale = Vector3.Lerp(visualTransform.localScale, targetScale, response);
            visualTransform.localPosition = Vector3.Lerp(visualTransform.localPosition, targetPosition, response);
            visualTransform.localRotation = Quaternion.Lerp(visualTransform.localRotation, Quaternion.Euler(0f, 0f, targetRotation), response);

            if (TimeStopAbility.PlayerTime < dashUntil) animatedRenderer.color = new Color(.15f, 1f, .25f);
            else if (TimeStopAbility.PlayerTime < effectUntil) animatedRenderer.color = Color.Lerp(Color.white, Color.red, Mathf.PingPong(TimeStopAbility.PlayerTime * 14f, 1f));
            else animatedRenderer.color = baseColor;
        }

        public void Attack() { attackUntil = TimeStopAbility.PlayerTime + .11f; }
        public void Hit() { effectUntil = TimeStopAbility.PlayerTime + .35f; }
        public void Dash() { dashUntil = TimeStopAbility.PlayerTime + .12f; if (animatedRenderer) animatedRenderer.color = new Color(.15f, 1f, .25f); }
        public void SpawnDashGhost(Vector2 direction, bool persistent = false, Color? tint = null) { if (animatedRenderer) DashVfxPool.Instance.Spawn(animatedRenderer, direction, persistent, tint); }
        public void Die() { dying = true; if (!animatedRenderer) return; animatedRenderer.color = new Color(1f, 1f, 1f, .25f); visualTransform.localRotation = Quaternion.Euler(0f, 0f, 90f); }
        public void RestoreForNewRun()
        {
            dying = false;
            effectUntil = dashUntil = attackUntil = 0f;
            walkPhase = 0f;
            if (animatedRenderer) animatedRenderer.color = baseColor;
            if (!visualTransform) return;
            visualTransform.localScale = baseScale;
            visualTransform.localPosition = basePosition;
            visualTransform.localRotation = Quaternion.identity;
        }
    }
}
