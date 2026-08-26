using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class MeleeCatCombatRuntime : MonoBehaviour
{
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int GroundedId = Animator.StringToHash("Grounded");
    private static readonly int JumpId = Animator.StringToHash("Jump");
    private static readonly int AttackId = Animator.StringToHash("Attack");
    private static readonly int ComboStepId = Animator.StringToHash("ComboStep");
    private static readonly int LockedId = Animator.StringToHash("Locked");
    private static readonly int[] SkillIds =
    {
        Animator.StringToHash("Skill1"), Animator.StringToHash("Skill2"),
        Animator.StringToHash("Skill3"), Animator.StringToHash("Skill4")
    };
    private static readonly float[] SkillCooldowns = { 6f, 12f, 15f, 10f };
    private static readonly float[] SkillManaCosts = { 15f, 25f, 30f, 45f };

    private const float BaseDamage = 28f;
    private const float AttackRadius = 1.8f;
    private const float AttackCooldown = 0.35f;
    private CatController controller;
    private Rigidbody body;
    private Animator animator;
    private Transform visualRoot;
    private Vector3 visualBaseScale = Vector3.one;
    private bool classActive;
    private bool lastGrounded = true;
    private int comboStep;
    private bool comboQueued;
    private bool restartQueued;
    private bool hitApplied;
    private float actionStartedAt;
    private float nextAttackAt;
    private float comboWindowOpen;
    private float comboWindowClose;
    private float actionEnd;
    private string networkState = "Idle";
    private bool stageSelectIdleApplied;
    private readonly float[] skillReadyAt = new float[4];
    private Coroutine skillRoutine;
    private Coroutine enlargedWeaponRoutine;
    private float enlargedAttackScale = 1f;
    private Coroutine shieldDashRoutine;
    private float nextShieldDashAt;
    private int activeSkill = -1;
    private int bufferedSkill = -1;
    private float bufferedSkillUntil;
    private float attackAnimationSpeed = 1f;
    private int lastBladeWaveTargets;
    private int empoweredSlashCharges;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public int LastBladeWaveCountForValidation { get; private set; }
#endif

    public bool IsClassActive => classActive;
    public string NetworkState => networkState;
    public int ComboStep => comboStep;
    public bool IsActionLocked => comboStep > 0 || skillRoutine != null;
    public bool LocksMovement => false;
    public float MovementScale => skillRoutine != null ? 0.6f : 1f;
    public float LastHitElapsed { get; private set; } = -1f;
    public float LastHitDamage { get; private set; }
    public int LastHitTargetCount { get; private set; }
    public int LastHitSequence { get; private set; }
    public int LastSkillTickCount { get; private set; }
    public float ThirdHitMultiplierForValidation
    {
        get
        {
            CatPrototype.BuildingBreak.BuildingBreakBootstrap bootstrap = CatPrototype.BuildingBreak.BuildingBreakBootstrap.Instance;
            float scale = bootstrap != null && bootstrap.Progress != null
                ? bootstrap.Progress.MeleeThirdHitScale : 1f;
            return 1.4f * scale;
        }
    }
    public float LastSkillFirstHitElapsed { get; private set; } = -1f;
    public float LastDashDistance { get; private set; }
    public static float ShieldGrabRangeForValidation => 2.5f;
    public static int ShieldDurabilityForValidation => 5;
    public static float ShieldDashDistanceForValidation => 15f;
    public static float ShieldDashSpeedForValidation => 10f;
    public static float ShieldDashRadiusForValidation => 1.4f;
    public static float ShieldDashDamageMultiplierForValidation => 0.5f;
    public static float ShieldDashCooldownForValidation => 1.2f;
    public static float ShieldDashAnimationSpeedForValidation => 2.4f;
    public static float EnlargedWeaponScaleForValidation => 1.6f;
    public static float EnlargedWeaponHoldForValidation => 3f;
    public static float EnlargedWeaponRecoveryForValidation => 0.3f;
    public static float EnlargedAttackDamageMultiplierForValidation => 1.35f;
    public static float EnlargedAttackRadiusForValidation => 3.24f;
    public static float EmpoweredSlashWindowForValidation => 3f;
    public bool EmpoweredSlashReadyForValidation => empoweredSlashCharges > 0;
    public int EmpoweredSlashChargesForValidation => empoweredSlashCharges;
    public int EmpoweredSlashCountForValidation { get; private set; }
    public int LastEmpoweredSlashTargetsForValidation { get; private set; }
    public float GetSkillCooldown(int index) => index < 0 || index >= 4 ? 0f : Mathf.Max(0f, skillReadyAt[index] - Time.unscaledTime);
    public float GetSkillCooldownDuration(int index) => index < 0 || index >= 4 ? 0f : SkillCooldowns[index];

    private void Awake()
    {
        controller = GetComponent<CatController>();
        body = GetComponent<Rigidbody>();
    }

    public void Configure(GameObject visual)
    {
        controller = GetComponent<CatController>();
        body = GetComponent<Rigidbody>();
        animator = visual != null ? visual.GetComponentInChildren<Animator>(true) : null;
        visualRoot = visual != null ? visual.transform : null;
        visualBaseScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
        if (animator == null) Debug.LogError("[MeleeCat] Animator missing.", this);
    }

    public void SetClassActive(bool value)
    {
        classActive = value;
        enabled = value;
        if (!value)
        {
            comboStep = 0;
            comboQueued = false;
            if (skillRoutine != null) StopCoroutine(skillRoutine);
            skillRoutine = null;
            empoweredSlashCharges = 0;
            activeSkill = -1;
            networkState = "Idle";
            RestoreVisualScale();
        }
    }

    public void ResetActionStateForStageStart()
    {
        if (skillRoutine != null) StopCoroutine(skillRoutine);
        skillRoutine = null;
        empoweredSlashCharges = 0;
        comboStep = 0;
        comboQueued = false;
        restartQueued = false;
        hitApplied = false;
        activeSkill = -1;
        nextAttackAt = 0f;
        for (int i = 0; i < skillReadyAt.Length; i++) skillReadyAt[i] = 0f;
        networkState = "Idle";
        RestoreVisualScale();
        if (animator != null)
        {
            animator.speed = 1f;
            animator.SetBool(LockedId, false);
        }
    }

    /// <summary>
    /// Stage selection is a modal presentation state, not an airborne gameplay state.
    /// Force the authored locomotion blend to its zero-speed Blender idle and clear any
    /// jump/skill trigger that could otherwise leave the cat looping Jump after E is used.
    /// Update keeps Grounded true for as long as the modal remains open.
    /// </summary>
    public void ForceStageSelectIdlePose()
    {
        ResetActionStateForStageStart();
        networkState = "Melee_Idle";
        stageSelectIdleApplied = true;
        if (animator == null) return;
        animator.speed = 1f;
        animator.ResetTrigger(JumpId);
        animator.ResetTrigger(AttackId);
        for (int i = 0; i < SkillIds.Length; i++) animator.ResetTrigger(SkillIds[i]);
        animator.SetInteger(ComboStepId, 0);
        animator.SetBool(LockedId, false);
        animator.SetBool(GroundedId, true);
        animator.SetFloat(SpeedId, 0f);
        animator.CrossFade("Locomotion", 0.03f, 0, 0f);
    }

    private void Update()
    {
        if (!classActive || controller == null) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            CatCarryThrow carry = controller.CarryThrow;
            if (carry != null && carry.IsShieldMode) carry.DetachShield();
        }
        bool stageSelectIdle = GameManager.Instance != null
            && GameManager.Instance.CurrentState == GameState.StageSelect;
        if (!stageSelectIdle) stageSelectIdleApplied = false;
        else if (!stageSelectIdleApplied) ForceStageSelectIdlePose();
        bool grounded = stageSelectIdle || controller.IsGroundedForAnimation;
        float normalizedSpeed = stageSelectIdle
            ? 0f : Mathf.Clamp01(controller.CurrentMoveInput.magnitude);
        if (animator != null)
        {
            float locomotionSpeed = shieldDashRoutine != null ? 0.72f : normalizedSpeed;
            animator.SetFloat(SpeedId, locomotionSpeed, 0.15f, Time.unscaledDeltaTime);
            animator.SetBool(GroundedId, grounded);
            animator.SetBool(LockedId, IsActionLocked);
            if (shieldDashRoutine != null && comboStep == 0 && skillRoutine == null)
                animator.speed = ShieldDashAnimationSpeedForValidation;
            if (lastGrounded && !grounded) animator.SetTrigger(JumpId);
        }
        lastGrounded = grounded;

        if (comboStep > 0) TickCombo();
        else if (skillRoutine == null && shieldDashRoutine == null)
            networkState = grounded ? (normalizedSpeed >= 0.75f ? "Melee_Run" : normalizedSpeed > 0.1f ? "Melee_Walk" : "Melee_Idle") : "Melee_Jump";

        MeleeWeaponAttachmentProfile[] profiles = GetComponentsInChildren<MeleeWeaponAttachmentProfile>(true);
        for (int i = 0; i < profiles.Length; i++)
        {
            MeleeWeaponAttachmentProfile profile = profiles[i];
            if (profile == null) continue;
            if (profile.gripPoint == null) profile.gripPoint = profile.transform.Find("GripPoint");
            if (profile.gripPoint == null) continue;
            Transform hand = profile.transform.parent;
            if (hand != null) profile.ApplyDynamicCorrection(hand, transform.localScale);
        }
    }

    public void HandleShieldAction(Vector3 direction, float powerRating)
    {
        if (!classActive || controller == null) return;
        CatCarryThrow carry = controller.CarryThrow;
        if (carry == null) return;
        if (!carry.IsShieldMode)
        {
            carry.TryGrabNearestAsShield(direction, powerRating);
            return;
        }
        if (shieldDashRoutine == null && Time.unscaledTime >= nextShieldDashAt)
            shieldDashRoutine = StartCoroutine(RunShieldDash(direction));
    }

    private IEnumerator RunShieldDash(Vector3 direction)
    {
        nextShieldDashAt = Time.unscaledTime + 1.2f;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
        direction.Normalize();
        Vector3 start = transform.position;
        float duration = 1.5f;
        float began = Time.unscaledTime;
        float nextDamageAt = began;
        int hitCount = 0;
        Dictionary<int, float> targetReadyAt = new Dictionary<int, float>();
        networkState = "Melee_ShieldDash";
        if (animator != null)
        {
            animator.SetBool(LockedId, false);
            animator.SetFloat(SpeedId, 0.72f);
            animator.speed = ShieldDashAnimationSpeedForValidation;
        }
        while (Time.unscaledTime - began < duration)
        {
            Vector3 inputDirection = ResolveShieldInputDirection();
            if (inputDirection.sqrMagnitude > 0.001f) direction = inputDirection.normalized;
            Vector3 stepEnd = ResolveDashEnd(transform.position, direction,
                ShieldDashSpeedForValidation * Time.unscaledDeltaTime, 0.5f);
            if (Vector3.Distance(stepEnd, transform.position) < 0.001f) break;
            CommitDashPosition(stepEnd);
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            if (Time.unscaledTime >= nextDamageAt)
            {
                nextDamageAt += 0.2f;
                Collider[] hits = Physics.OverlapCapsule(transform.position + Vector3.up * 0.6f,
                    transform.position + direction * 2f + Vector3.up * 0.6f,
                    ShieldDashRadiusForValidation, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits.Length; i++)
                {
                    BreakableObject target = hits[i] != null ? hits[i].GetComponentInParent<BreakableObject>() : null;
                    if (target == null || target.transform.IsChildOf(transform) || target.IsDestroyed) continue;
                    int id = target.GetInstanceID();
                    if (targetReadyAt.TryGetValue(id, out float readyAt) && Time.unscaledTime < readyAt) continue;
                    targetReadyAt[id] = Time.unscaledTime + 0.4f;
                    if (target.ApplyDamage(BaseDamage * ShieldDashDamageMultiplierForValidation
                        * ClassFix28RoundBuffs.DamageMultiplier, this)) hitCount++;
                }
            }
            yield return null;
        }
        float distance = Vector3.Distance(start, transform.position);
        CatCarryThrow carry = controller.CarryThrow;
        carry?.ConsumeShieldDashDurability();
        Debug.Log("[ClassFix28][shield_dash] distance=" + distance.ToString("F2")
            + " hits=" + hitCount + " durability=" + (carry != null ? carry.HeldDurability : 0), this);
        if (animator != null)
        {
            animator.speed = 1f;
            animator.SetFloat(SpeedId, controller.CurrentMoveInput.magnitude);
        }
        networkState = "Melee_Idle";
        shieldDashRoutine = null;
    }

    private Vector3 ResolveShieldInputDirection()
    {
        Vector2 input = controller != null ? controller.CurrentMoveInput : Vector2.zero;
        if (input.sqrMagnitude < 0.001f) return transform.forward;
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        Camera camera = Camera.main;
        if (camera != null)
        {
            Vector3 forward = camera.transform.forward; forward.y = 0f; forward.Normalize();
            Vector3 right = camera.transform.right; right.y = 0f; right.Normalize();
            direction = right * input.x + forward * input.y;
        }
        return direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
    }

    public void HandleSkillInput(int index)
    {
        if (!classActive || index < 0 || index >= 4) return;
        string skillId = CatSkillUnlocks.SkillIdForIndex(index);
        if (!CatSkillUnlocks.IsSkillUnlocked(skillId))
        {
            CatSkillHudUI.NotifySkillInput(index, false);
            return;
        }
        CatSkillEffectRuntime mana = GetComponent<CatSkillEffectRuntime>();
        bool resourcesReady = Time.unscaledTime >= skillReadyAt[index]
            && mana != null && mana.CurrentMana >= SkillManaCosts[index];
        bool canOverrideBasicAttack = comboStep > 0 && skillRoutine == null;
        bool available = resourcesReady && (skillRoutine == null) && (!IsActionLocked || canOverrideBasicAttack);
        CatSkillHudUI.NotifySkillInput(index, available);
        if (!available) return;
        bufferedSkill = index;
        bufferedSkillUntil = Time.unscaledTime + 0.4f;
        StartBufferedSkillIfPossible();
    }

    private void StartBufferedSkillIfPossible()
    {
        if (bufferedSkill < 0 || Time.unscaledTime > bufferedSkillUntil || skillRoutine != null) { bufferedSkill = -1; return; }
        int index = bufferedSkill;
        CatSkillEffectRuntime mana = GetComponent<CatSkillEffectRuntime>();
        if (mana == null || Time.unscaledTime < skillReadyAt[index] || !mana.TrySpendClassMana(SkillManaCosts[index])) return;
        // Skills intentionally override the basic combo, but never another skill.
        comboStep = 0;
        comboQueued = false;
        restartQueued = false;
        hitApplied = false;
        if (animator != null) animator.speed = 1f;
        bufferedSkill = -1;
        skillReadyAt[index] = Time.unscaledTime + SkillCooldowns[index]
            * ClassFix28RoundBuffs.SkillCooldownMultiplier;
        skillRoutine = StartCoroutine(RunSkill(index));
    }

    public int TryAttack()
    {
        if (!classActive || skillRoutine != null) return 0;
        float now = Time.unscaledTime;
        if (comboStep > 0)
        {
            if (now >= comboWindowOpen && now <= comboWindowClose && comboStep < 3) comboQueued = true;
            else restartQueued = true;
            return 0;
        }
        if (now < nextAttackAt) return 0;
        StartAttack(1);
        return 0;
    }

    private void StartAttack(int step)
    {
        comboStep = Mathf.Clamp(step, 1, 3);
        comboQueued = false;
        restartQueued = false;
        hitApplied = false;
        actionStartedAt = Time.unscaledTime;
        attackAnimationSpeed = Mathf.Clamp(1f / Mathf.Max(0.01f, CurrentCooldownScale()), 1f, 3f);
        float length = comboStep == 3 ? 39f / 30f : 31f / 30f;
        comboWindowOpen = actionStartedAt + 16f / 30f / attackAnimationSpeed;
        comboWindowClose = actionStartedAt + 20f / 30f / attackAnimationSpeed;
        actionEnd = actionStartedAt + length / attackAnimationSpeed;
        networkState = "Melee_Attack" + comboStep;
        GameAudioManager.PlayMeleeAttack(transform);
        // Skill 1 primes the next three normal combo attacks. Consume here instead
        // of TryAttack so queued combo steps two and three each launch exactly one
        // wave as well. Waves are allowed to overlap; a fast attack-speed build must
        // never cancel the previous projectile.
        if (empoweredSlashCharges > 0)
        {
            empoweredSlashCharges--;
            StartCoroutine(RunEmpoweredSlashWave());
        }
        if (animator != null)
        {
            animator.speed = attackAnimationSpeed;
            animator.SetInteger(ComboStepId, comboStep);
            animator.SetTrigger(AttackId);
        }
    }

    private void TickCombo()
    {
        float now = Time.unscaledTime;
        float hitTime = actionStartedAt + (comboStep == 3 ? 18f : 11f) / 30f / attackAnimationSpeed;
        if (!hitApplied && now >= hitTime)
        {
            hitApplied = true;
            float finisherScale = 1f;
            CatPrototype.BuildingBreak.BuildingBreakBootstrap bootstrap = CatPrototype.BuildingBreak.BuildingBreakBootstrap.Instance;
            if (comboStep == 3 && bootstrap != null && bootstrap.Progress != null)
                finisherScale = bootstrap.Progress.MeleeThirdHitScale;
            float storyFinisherScale = 1f + ClassCombatUpgradeRuntime.Effect(PlayerClass.Melee, 1);
            float multiplier = comboStep == 1 ? 1f : comboStep == 2 ? 1.15f : 1.4f * finisherScale * storyFinisherScale;
            LastHitDamage = BaseDamage * multiplier * enlargedAttackScale;
            LastHitElapsed = now - actionStartedAt;
            float activeRadius = enlargedAttackScale > 1f ? AttackRadius * 1.8f : AttackRadius;
            LastHitTargetCount = DamageSphere(transform.position + FlattenForward() * 1.05f, activeRadius, LastHitDamage, 0f);
            if (LastHitTargetCount > 0) GameAudioManager.PlayMeleeHit(transform);
            LastHitSequence++;
            Debug.Log("[ClassSelect] combo_hit step=" + comboStep + " elapsed=" + LastHitElapsed.ToString("F3")
                + " damage=" + LastHitDamage.ToString("F2") + " targets=" + LastHitTargetCount, this);
        }
        if (comboQueued && comboStep < 3 && now >= comboWindowClose)
        {
            StartAttack(comboStep + 1);
            return;
        }
        if (now < actionEnd) return;
        bool startOver = restartQueued;
        comboStep = 0;
        comboQueued = false;
        restartQueued = false;
        if (animator != null) animator.speed = 1f;
        nextAttackAt = now + AttackCooldown * CurrentCooldownScale();
        if (startOver)
        {
            nextAttackAt = now;
            StartAttack(1);
        }
        else StartBufferedSkillIfPossible();
    }

    private IEnumerator RunSkill(int index)
    {
        activeSkill = index;
        actionStartedAt = Time.unscaledTime;
        LastSkillTickCount = 0;
        LastSkillFirstHitElapsed = -1f;
        networkState = "Melee_Skill" + (index + 1);
        if (animator != null) { animator.speed = 1f; animator.SetTrigger(SkillIds[index]); }
        GameAudioManager.PlayMeleeSkill(index, transform);
        Vector3 origin = transform.position;
        Vector3 forward = FlattenForward();
        switch (index)
        {
            case 0:
                yield return WaitRealtime(14f / 30f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LastBladeWaveCountForValidation = 0;
#endif
                lastBladeWaveTargets = 0;
                int enlargedBladeTargets = DamageArc(transform.position, forward, AttackRadius * 1.8f, 180f, BaseDamage * 1.35f);
                RecordSkillHit(0, Time.unscaledTime - actionStartedAt, BaseDamage * 1.35f,
                    enlargedBladeTargets);
                if (enlargedWeaponRoutine != null) StopCoroutine(enlargedWeaponRoutine);
                enlargedWeaponRoutine = StartCoroutine(AnimateMeleeWeaponScale(1.6f, 3f, 0.3f));
                empoweredSlashCharges = 3;
                FloatingFeedbackUI.Show("<color=#FF8A2A>검기 3회 준비! 왼쪽 클릭</color>",
                    transform.position + Vector3.up * 1.5f);
                break;
            case 1:
                Vector3 jumpGround = transform.position;
                Vector3 jumpTarget = jumpGround + forward * 1.5f;
                float jumpStarted = Time.unscaledTime;
                while (Time.unscaledTime - jumpStarted < 0.35f)
                {
                    float t = Mathf.Clamp01((Time.unscaledTime - jumpStarted) / 0.35f);
                    Vector3 horizontal = Vector3.Lerp(jumpGround, jumpTarget, t);
                    horizontal.y = jumpGround.y + Mathf.Sin(t * Mathf.PI) * 1.2f;
                    CommitDashPosition(horizontal);
                    yield return null;
                }
                CommitDashPosition(jumpTarget);
                // The impact occurs as the cat reaches the ground. Waiting for
                // frame 23 made the VFX visibly trail the landing by ~0.4 s.
                yield return WaitRealtime(Mathf.Max(0f, 12f / 30f - (Time.unscaledTime - actionStartedAt)));
                float knockback = 6f;
                Vector3 impact = transform.position;
                BlenderSkillVfxRuntime.Spawn(PlayerClass.Melee, 1, impact, forward, 0.8f, 0f, 1f);
                RecordSkillHit(1, Time.unscaledTime - actionStartedAt, BaseDamage * 3f,
                    DamageSphereFalloff(impact, 8f, BaseDamage * 3f, 0.5f, knockback));
                BroadcastVisual(2, impact, impact, 8f, 1f, 1.4f);
                yield return WaitRealtime(Mathf.Max(0f, 1.4f - (Time.unscaledTime - actionStartedAt)));
                break;
            case 2:
                yield return WaitRealtime(12f / 30f);
                yield return AnimateVisualScale(visualBaseScale, visualBaseScale * 1.35f, 0.12f);
                BlenderSkillVfxRuntime.Spawn(PlayerClass.Melee, 2, transform.position,
                    forward, 0.8f, 0f, 1f, transform);
                const int spinTicks = 5;
                for (int tick = 0; tick < spinTicks; tick++)
                {
                    RecordSkillHit(2, Time.unscaledTime - actionStartedAt, BaseDamage * 0.75f,
                        DamageSphere(transform.position, 5.4f, BaseDamage * 0.75f, 0f));
                    BroadcastVisual(3, transform.position, transform.position, 5.4f, 1.35f, 0.7f);
                    if (tick < spinTicks - 1) yield return WaitRealtime(0.125f);
                }
                yield return AnimateVisualScale(visualBaseScale * 1.35f, visualBaseScale, 0.2f);
                yield return WaitRealtime(Mathf.Max(0f, 41f / 30f - (Time.unscaledTime - actionStartedAt)));
                break;
            case 3:
                yield return WaitRealtime(15f / 30f);
                Vector3 start = transform.position;
                const float dashDistance = 6f;
                Vector3 end = ResolveDashEnd(start, forward, dashDistance, 0.8f);
                LastDashDistance = Vector3.Distance(start, end);
                BlenderSkillVfxRuntime.Spawn(PlayerClass.Melee, 3,
                    start + forward * 0.5f, forward, 0.65f, LastDashDistance, 1f);
                RecordSkillHit(3, Time.unscaledTime - actionStartedAt, BaseDamage * 2.2f,
                    DamageCapsuleWithEndpoint(start, end, 2.4f, BaseDamage * 2.2f, 4f, BaseDamage * 2.2f));
                CommitDashPosition(end);
                BroadcastVisual(4, start, end, 4.8f, 1f, 1f);
                float dashHoldUntil = Time.unscaledTime + Mathf.Max(0f, 33f / 30f - 15f / 30f);
                while (Time.unscaledTime < dashHoldUntil)
                {
                    CommitDashPosition(end);
                    yield return null;
                }
                break;
        }
        yield return null;
        skillRoutine = null;
        activeSkill = -1;
        RestoreVisualScale();
        if (animator != null) animator.SetBool(LockedId, false);
    }

    private void RecordSkillHit(int index, float elapsed, float damage, int targets)
    {
        if (LastSkillTickCount == 0) LastSkillFirstHitElapsed = elapsed;
        LastHitElapsed = elapsed;
        LastHitDamage = damage;
        LastHitTargetCount = targets;
        LastHitSequence++;
        LastSkillTickCount++;
        if (targets > 0) GameAudioManager.PlayMeleeHit(transform);
        Debug.Log("[ClassSelect] skill_hit index=" + (index + 1) + " tick=" + LastSkillTickCount
            + " elapsed=" + elapsed.ToString("F3") + " damage=" + damage.ToString("F2")
            + " targets=" + targets, this);
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        float until = Time.unscaledTime + Mathf.Max(0f, seconds);
        while (Time.unscaledTime < until) yield return null;
    }

    private static float CurrentCooldownScale()
    {
        CatPrototype.BuildingBreak.BuildingBreakBootstrap bootstrap = CatPrototype.BuildingBreak.BuildingBreakBootstrap.Instance;
        float buildingScale = bootstrap != null && bootstrap.Progress != null ? bootstrap.Progress.CooldownScale : 1f;
        return buildingScale * Mathf.Max(0.2f, 1f + ClassCombatUpgradeRuntime.Effect(PlayerClass.Melee, 0))
            * ClassFix28RoundBuffs.AttackCooldownMultiplier;
    }

    private Vector3 FlattenForward()
    {
        Vector3 value = transform.forward;
        value.y = 0f;
        return value.sqrMagnitude > 0.001f ? value.normalized : Vector3.forward;
    }

    private int DamageArc(Vector3 origin, Vector3 forward, float radius, float angle, float damage)
    {
        Collider[] hits = Physics.OverlapSphere(origin, radius, ~0, QueryTriggerInteraction.Ignore);
        HashSet<BreakableObject> unique = new HashSet<BreakableObject>();
        int damaged = 0;
        for (int i = 0; i < hits.Length; i++)
        {
            BreakableObject target = hits[i] != null ? hits[i].GetComponentInParent<BreakableObject>() : null;
            if (target == null || target.transform.IsChildOf(transform) || unique.Contains(target)) continue;
            Vector3 closest = hits[i].ClosestPoint(origin);
            Vector3 direction = closest - origin; direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f && Vector3.Angle(forward, direction) <= angle * 0.5f)
            {
                unique.Add(target);
                if (target.ApplyDamage(damage * ClassFix28RoundBuffs.DamageMultiplier, this)) damaged++;
            }
        }
        return damaged;
    }

    private int DamageSphere(Vector3 origin, float radius, float damage, float knockback)
    {
        return DamageUnique(Physics.OverlapSphere(origin, radius, ~0, QueryTriggerInteraction.Ignore), origin, damage, knockback);
    }

    private int DamageSphereFalloff(Vector3 origin, float radius, float centerDamage, float edgeScale, float knockback)
    {
        Collider[] hits = Physics.OverlapSphere(origin, radius, ~0, QueryTriggerInteraction.Ignore);
        HashSet<BreakableObject> unique = new HashSet<BreakableObject>();
        int damaged = 0;
        for (int i = 0; i < hits.Length; i++)
        {
            BreakableObject target = hits[i] != null ? hits[i].GetComponentInParent<BreakableObject>() : null;
            if (target == null || target.transform.IsChildOf(transform) || !unique.Add(target)) continue;
            Vector3 closest = hits[i].ClosestPoint(origin);
            float t = Mathf.Clamp01(Vector3.Distance(origin, closest) / radius);
            if (!target.ApplyDamage(centerDamage * Mathf.Lerp(1f, edgeScale, t)
                    * ClassFix28RoundBuffs.DamageMultiplier, this)) continue;
            Rigidbody targetBody = target.GetComponent<Rigidbody>();
            if (targetBody != null && !targetBody.isKinematic)
                targetBody.AddForce((target.transform.position - origin).normalized * knockback, ForceMode.VelocityChange);
            damaged++;
        }
        return damaged;
    }

    private IEnumerator RunBladeWaves(Vector3 start, Vector3 baseForward, int count, float damagePerWave)
    {
        const float speed = 22f;
        const float range = 14f;
        const float radius = 1.2f;
        Vector3[] directions = new Vector3[count];
        Vector3[] positions = new Vector3[count];
        HashSet<BreakableObject>[] damaged = new HashSet<BreakableObject>[count];
        for (int i = 0; i < count; i++)
        {
            float angle = count == 1 ? 0f : Mathf.Lerp(-12f, 12f, i / (float)(count - 1));
            directions[i] = Quaternion.AngleAxis(angle, Vector3.up) * baseForward;
            positions[i] = start;
            damaged[i] = new HashSet<BreakableObject>();
            BlenderSkillVfxRuntime.Spawn(PlayerClass.Melee, 0, start,
                directions[i], range / speed, range, 1f);
            BroadcastVisual(1, start, start + directions[i] * range, 2.4f, 1f, range / speed);
        }
        float travelled = 0f;
        while (travelled < range)
        {
            float step = Mathf.Min(range - travelled, speed * Time.unscaledDeltaTime);
            for (int i = 0; i < count; i++)
            {
                Vector3 next = positions[i] + directions[i] * step;
                Collider[] hits = Physics.OverlapCapsule(positions[i], next, radius, ~0, QueryTriggerInteraction.Ignore);
                for (int h = 0; h < hits.Length; h++)
                {
                    BreakableObject target = hits[h] != null ? hits[h].GetComponentInParent<BreakableObject>() : null;
                    if (target == null || target.transform.IsChildOf(transform) || !damaged[i].Add(target)) continue;
                    if (target.ApplyDamage(damagePerWave * ClassFix28RoundBuffs.DamageMultiplier, this))
                        lastBladeWaveTargets++;
                }
                positions[i] = next;
            }
            travelled += step;
            yield return null;
        }
    }

    private IEnumerator RunEmpoweredSlashWave()
    {
        Vector3 direction = FlattenForward();
        lastBladeWaveTargets = 0;
        EmpoweredSlashCountForValidation++;
        yield return RunBladeWaves(transform.position + Vector3.up * 0.65f,
            direction, 1, BaseDamage * 1.25f);
        LastEmpoweredSlashTargetsForValidation = lastBladeWaveTargets;
        if (lastBladeWaveTargets > 0) GameAudioManager.PlayMeleeHit(transform);
        Debug.Log("[BlenderSkillVFX] empowered_slash count=" + EmpoweredSlashCountForValidation
            + " targets=" + lastBladeWaveTargets, this);
    }

    private IEnumerator AnimateMeleeWeaponScale(float scale, float hold, float recover)
    {
        MeleeWeaponAttachmentProfile profile = GetComponentInChildren<MeleeWeaponAttachmentProfile>(true);
        enlargedAttackScale = 1.35f;
        if (profile == null)
        {
            yield return WaitRealtime(hold + recover);
            enlargedAttackScale = 1f;
            enlargedWeaponRoutine = null;
            yield break;
        }
        Transform weapon = profile.transform;
        Vector3 baseScale = weapon.localScale;
        weapon.localScale = baseScale * scale;
        yield return WaitRealtime(hold);
        float started = Time.unscaledTime;
        while (Time.unscaledTime - started < recover)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - started) / Mathf.Max(0.01f, recover));
            weapon.localScale = Vector3.Lerp(baseScale * scale, baseScale, t);
            yield return null;
        }
        weapon.localScale = baseScale;
        enlargedAttackScale = 1f;
        enlargedWeaponRoutine = null;
    }

    private int DamageCapsule(Vector3 start, Vector3 end, float radius, float damage)
    {
        return DamageUnique(Physics.OverlapCapsule(start + Vector3.up * 0.6f, end + Vector3.up * 0.6f, radius, ~0, QueryTriggerInteraction.Ignore), start, damage, 0f);
    }

    private int DamageCapsuleWithEndpoint(Vector3 start, Vector3 end, float capsuleRadius, float capsuleDamage,
        float endpointRadius, float endpointDamage)
    {
        HashSet<BreakableObject> unique = new HashSet<BreakableObject>();
        int damaged = ApplyUniqueDamage(
            Physics.OverlapCapsule(start + Vector3.up * 0.6f, end + Vector3.up * 0.6f,
                capsuleRadius, ~0, QueryTriggerInteraction.Ignore), unique, capsuleDamage);
        damaged += ApplyUniqueDamage(
            Physics.OverlapSphere(end, endpointRadius, ~0, QueryTriggerInteraction.Ignore),
            unique, endpointDamage);
        return damaged;
    }

    private int ApplyUniqueDamage(Collider[] hits, HashSet<BreakableObject> unique, float damage)
    {
        int damaged = 0;
        for (int i = 0; i < hits.Length; i++)
        {
            BreakableObject target = hits[i] != null ? hits[i].GetComponentInParent<BreakableObject>() : null;
            if (target == null || target.transform.IsChildOf(transform) || !unique.Add(target)) continue;
            if (target.ApplyDamage(damage * ClassFix28RoundBuffs.DamageMultiplier, this)) damaged++;
        }
        return damaged;
    }

    private int DamageUnique(Collider[] hits, Vector3 origin, float damage, float knockback)
    {
        HashSet<BreakableObject> unique = new HashSet<BreakableObject>();
        for (int i = 0; i < hits.Length; i++)
        {
            BreakableObject target = hits[i] != null ? hits[i].GetComponentInParent<BreakableObject>() : null;
            if (target == null || target.transform.IsChildOf(transform) || !unique.Add(target)) continue;
            bool applied = target.ApplyDamage(damage * ClassFix28RoundBuffs.DamageMultiplier, this);
            if (knockback > 0f)
            {
                Rigidbody targetBody = target.GetComponent<Rigidbody>();
                if (targetBody != null && !targetBody.isKinematic) targetBody.AddForce((target.transform.position - origin).normalized * knockback, ForceMode.VelocityChange);
            }
            if (!applied) unique.Remove(target);
        }
        return unique.Count;
    }

    private Vector3 ResolveDashEnd(Vector3 start, Vector3 direction, float distance, float halfWidth)
    {
        // The damage capsule remains 0.8 m wide, but using that same radius from
        // y=0.6 overlaps the floor and collapses every dash to zero distance.
        float movementRadius = Mathf.Min(0.5f, halfWidth);
        if (Physics.SphereCast(start + Vector3.up * 1f, movementRadius, direction, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && !hit.collider.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<BreakableObject>() == null)
                return start + direction * Mathf.Max(0f, hit.distance - movementRadius);
        }
        return start + direction * distance;
    }

    private void CommitDashPosition(Vector3 position)
    {
        transform.position = position;
        if (body == null) return;
        body.position = position;
        if (!body.isKinematic) body.linearVelocity = Vector3.zero;
    }

    private static void BroadcastVisual(int skill, Vector3 origin, Vector3 target, float radius, float scale, float duration)
    {
        Vector3 position = skill == 2 ? origin : target;
        if (skill == 3)
        {
            ClassSkillEffectPool.SpawnSkill(PlayerClass.Melee, skill - 1, position, false);
            StoryCoopRuntimeBridge.NotifySkillVisual(new StoryCoopSkillVisualEvent(
                ClassSkillEffectPool.MeleeEventBase + skill - 1, position, position, radius, scale, duration));
        }
        else ClassSkillEffectPool.SpawnSkill(PlayerClass.Melee, skill - 1, position, true);
    }

    private IEnumerator AnimateVisualScale(Vector3 from, Vector3 to, float duration)
    {
        if (visualRoot == null) yield break;
        float started = Time.unscaledTime;
        while (Time.unscaledTime - started < duration)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - started) / Mathf.Max(0.01f, duration));
            visualRoot.localScale = Vector3.Lerp(from, to, t);
            yield return null;
        }
        visualRoot.localScale = to;
    }

    private void RestoreVisualScale()
    {
        if (visualRoot != null) visualRoot.localScale = visualBaseScale;
    }

    public void ApplyNetworkState(string state)
    {
        if (!classActive || animator == null || string.IsNullOrEmpty(state) || state == networkState) return;
        networkState = state;
        if (state.StartsWith("Melee_Attack") && int.TryParse(state.Substring(state.Length - 1), out int attack))
        {
            animator.SetInteger(ComboStepId, Mathf.Clamp(attack, 1, 3));
            animator.SetTrigger(AttackId);
        }
        else if (state.StartsWith("Melee_Skill") && int.TryParse(state.Substring(state.Length - 1), out int skill)) animator.SetTrigger(SkillIds[Mathf.Clamp(skill - 1, 0, 3)]);
        else if (state == "Melee_Jump") animator.SetTrigger(JumpId);
        else animator.SetFloat(SpeedId, state == "Melee_Run" ? 1f : state == "Melee_Walk" ? 0.5f : 0f);
    }
}
