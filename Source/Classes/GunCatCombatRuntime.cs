using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GunCatCombatRuntime : MonoBehaviour
{
    public static int SpinShotCountForValidation => 36;
    public static float SpinShotDegreesPerShotForValidation => 10f;
    public static float SpinShotDamageMultiplierForValidation => 0.18f;
    public static float SpinShotMovementScaleForValidation => 0.6f;
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int GroundedId = Animator.StringToHash("Grounded");
    private static readonly int JumpId = Animator.StringToHash("Jump");
    private static readonly int AttackId = Animator.StringToHash("Attack");
    private static readonly int LockedId = Animator.StringToHash("Locked");
    private static readonly int[] SkillIds =
    {
        Animator.StringToHash("Skill1"), Animator.StringToHash("Skill2"),
        Animator.StringToHash("Skill3"), Animator.StringToHash("Skill4")
    };
    private static readonly float[] SkillCooldowns = { 8f, 12f, 15f, 30f };
    private static readonly float[] SkillManaCosts = { 15f, 25f, 30f, 45f };

    private const int BasicPoolSize = 30;
    private const int ObjectPoolSize = 20;
    private const float BaseDamage = 28f * 0.85f;
    private const float AttackCooldown = 0.35f;
    private const float Range = 40f;
    private const float NormalSpeed = 40f;
    private const float SpecialSpeed = 35f;
    private const float SpecialChance = 0.12f;
    private const float SpecialCooldown = 3f;
    // GUNCAT_AMMO_SYSTEM supersedes the legacy probability conversion.  The
    // authored code remains below for migration/reference but can no longer run.
    private static readonly bool LegacyInstantConversionEnabled = false;

    private CatController controller;
    private Rigidbody body;
    private Animator animator;
    private Transform rightMuzzle;
    private Transform leftMuzzle;
    private GameObject rightGun;
    private GameObject leftGun;
    private Transform gunVisualRoot;
    private GameObject projectilePrefab;
    private Transform poolRoot;
    private readonly Queue<GunCatProjectile> availableBasic = new Queue<GunCatProjectile>(BasicPoolSize);
    private readonly Queue<GunCatProjectile> availableObject = new Queue<GunCatProjectile>(ObjectPoolSize);
    private readonly List<GunCatProjectile> allProjectiles = new List<GunCatProjectile>(BasicPoolSize + ObjectPoolSize);
    private readonly HashSet<GunCatProjectile> objectProjectiles = new HashSet<GunCatProjectile>();
    private GunCatAmmoRuntime ammoRuntime;
    private GunCatAimRuntime aimRuntime;
    private readonly float[] skillReadyAt = new float[4];
    private Coroutine actionRoutine;
    private int activeSkill = -1;
    private float actionStartedAt;
    private float nextAttackAt;
    private float nextSpecialAt;
    private bool classActive;
    private bool lastGrounded = true;
    private string networkState = "Idle";
    private bool networkAimPose;
    private bool observedDualWieldUnlocked;
    private Transform aimChest;
    private Transform aimRightArm;
    private Transform aimLeftArm;
    private readonly Quaternion chestAimOffset = Quaternion.Euler(-7f, 6f, 0f);
    private readonly Quaternion rightArmAimOffset = Quaternion.Euler(-10f, 0f, -9f);
    private readonly Quaternion leftArmAimOffset = Quaternion.Euler(-5f, 0f, 7f);

    public bool IsClassActive => classActive;
    public bool IsActionLocked => actionRoutine != null;
    public bool LocksMovement
    {
        get
        {
            if (actionRoutine == null) return false;
            float elapsed = Time.unscaledTime - actionStartedAt;
            if (activeSkill == 2 || activeSkill == 3) return false;
            return activeSkill == 1 ? elapsed >= 5f / 30f && elapsed <= 19f / 30f : activeSkill >= 0;
        }
    }
    public float MovementScale => activeSkill == 2 ? 0.6f : activeSkill == 3 ? 0.5f : 1f;
    public bool IsGroundAttackInvulnerable => activeSkill == 3
        && Time.unscaledTime - actionStartedAt >= 11f / 30f
        && Time.unscaledTime - actionStartedAt <= 11f / 30f + 2.40f;
    public string NetworkState => networkState;
    public bool NetworkAim => networkAimPose;
    public int PoolInstantiatedCount => allProjectiles.Count;
    public Transform RightMuzzle => rightMuzzle;
    public int ActiveProjectileCount => allProjectiles.Count - availableBasic.Count - availableObject.Count;
    public int TotalShotsFired { get; private set; }
    public int SpecialShotsFired { get; private set; }
    public float LastSpecialVisualMaxDimension { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool ValidationForceNextSpecial { get; set; }
#endif
    public float LastShotElapsed { get; private set; } = -1f;
    public Vector3 LastSkillStart { get; private set; }
    public float LastSkillTravel { get; private set; }
    public float LastSkillDuration { get; private set; }
    public float LastSkillMaxHeight { get; private set; }
    public int LastSkillShotCount { get; private set; }
    public float SpecialChanceForValidation
    {
        get
        {
            CatPrototype.BuildingBreak.BuildingBreakBootstrap bootstrap = CatPrototype.BuildingBreak.BuildingBreakBootstrap.Instance;
            return 0f;
        }
    }
    public float GetSkillCooldown(int index) => index < 0 || index >= 4 ? 0f : Mathf.Max(0f, skillReadyAt[index] - Time.unscaledTime);
    public float GetSkillCooldownDuration(int index) => index < 0 || index >= 4 ? 0f : SkillCooldowns[index];

    private void Awake()
    {
        controller = GetComponent<CatController>();
        body = GetComponent<Rigidbody>();
    }

    public void Configure(GameObject visual, GameObject bulletPrefab)
    {
        controller = GetComponent<CatController>();
        body = GetComponent<Rigidbody>();
        gunVisualRoot = visual != null ? visual.transform : null;
        animator = visual != null ? visual.GetComponentInChildren<Animator>(true) : null;
        rightMuzzle = FindByName(visual != null ? visual.transform : null, "MuzzleRight");
        leftMuzzle = FindByName(visual != null ? visual.transform : null, "MuzzleLeft");
        rightGun = FindByName(visual != null ? visual.transform : null, "GunRightExternal")?.gameObject;
        leftGun = FindByName(visual != null ? visual.transform : null, "GunLeftExternal")?.gameObject;
        ApplyAuthoredGunTransforms(visual != null ? visual.transform : null);
        projectilePrefab = bulletPrefab;
        if (animator == null || rightMuzzle == null || projectilePrefab == null)
        {
            Debug.LogError("[GunCat] authored Animator, right muzzle, or bullet prefab missing.", this);
            return;
        }
        EnsurePool();
        RefreshWeaponVisibility();
    }

    public void ConfigureAmmo(GunCatAmmoRuntime runtime) => ammoRuntime = runtime;
    public void ConfigureAim(GunCatAimRuntime runtime) => aimRuntime = runtime;

    public void SetClassActive(bool value)
    {
        classActive = value;
        enabled = value;
        RefreshWeaponVisibility();
        if (!value)
        {
            if (aimRuntime != null) aimRuntime.CancelAim("class_inactive");
            SetNetworkAimPose(false);
            if (actionRoutine != null) StopCoroutine(actionRoutine);
            actionRoutine = null;
            activeSkill = -1;
            networkState = "Idle";
        }
    }

    public void ResetActionStateForStageStart()
    {
        if (actionRoutine != null) StopCoroutine(actionRoutine);
        actionRoutine = null;
        activeSkill = -1;
        nextAttackAt = 0f;
        for (int i = 0; i < skillReadyAt.Length; i++) skillReadyAt[i] = 0f;
        networkState = "Gun_Idle";
        RefreshWeaponVisibility();
        if (animator != null) animator.SetBool(LockedId, false);
    }

    private void Update()
    {
        if (!classActive || controller == null) return;
        bool dualWieldUnlocked = IsDualWieldUnlocked();
        if (dualWieldUnlocked != observedDualWieldUnlocked)
        {
            observedDualWieldUnlocked = dualWieldUnlocked;
            RefreshWeaponVisibility();
        }
        bool grounded = controller.IsGroundedForAnimation;
        float normalizedSpeed = Mathf.Clamp01(controller.CurrentMoveInput.magnitude);
        if (animator != null)
        {
            animator.SetFloat(SpeedId, normalizedSpeed, 0.12f, Time.unscaledDeltaTime);
            animator.SetBool(GroundedId, grounded);
            animator.SetBool(LockedId, IsActionLocked || networkAimPose);
            if (lastGrounded && !grounded) animator.SetTrigger(JumpId);
        }
        lastGrounded = grounded;
        if (actionRoutine == null)
            networkState = grounded ? (normalizedSpeed >= 0.75f ? "Gun_Run" : normalizedSpeed > 0.1f ? "Gun_Walk" : "Gun_Idle") : "Gun_Jump";

        MeleeWeaponAttachmentProfile[] profiles = GetComponentsInChildren<MeleeWeaponAttachmentProfile>(true);
        for (int i = 0; i < profiles.Length; i++)
        {
            MeleeWeaponAttachmentProfile profile = profiles[i];
            if (profile == null || profile.gripPoint == null || profile.transform.parent == null) continue;
            profile.ApplyDynamicCorrection(profile.transform.parent, transform.localScale);
        }
    }

    public int TryAttack()
    {
        if (!classActive || actionRoutine != null || Time.unscaledTime < nextAttackAt
            || (ammoRuntime != null && (ammoRuntime.IsReloading || ammoRuntime.CurrentAmmo <= 0))) return 0;
        // Keep the authored 0.35 baseline and apply the requested gun cadence
        // multiplier without changing shared balance data.
        nextAttackAt = Time.unscaledTime + AttackCooldown * 0.35f * CurrentCooldownScale();
        actionRoutine = StartCoroutine(RunBasicAttack());
        return 0;
    }

    public bool BeginLockOnVolley(IReadOnlyList<BreakableObject> targets)
    {
        if (!classActive || actionRoutine != null || targets == null || targets.Count == 0
            || ammoRuntime == null || ammoRuntime.IsReloading || ammoRuntime.CurrentAmmo <= 0) return false;
        actionRoutine = StartCoroutine(RunLockOnVolley(new List<BreakableObject>(targets)));
        return true;
    }

    private IEnumerator RunLockOnVolley(List<BreakableObject> targets)
    {
        activeSkill = -3;
        actionStartedAt = Time.unscaledTime;
        networkState = "Gun_LockVolley";
        int fired = 0;
        for (int i = 0; i < targets.Count && i < 6; i++)
        {
            BreakableObject target = targets[i];
            if (target == null || target.IsDestroyed || Vector3.Distance(transform.position, target.transform.position) > 40f) continue;
            if (ammoRuntime.IsReloading || ammoRuntime.CurrentAmmo <= 0) break;
            Transform muzzle = (fired & 1) == 0 ? rightMuzzle : (leftMuzzle != null ? leftMuzzle : rightMuzzle);
            Vector3 point = target.GetComponent<Collider>() != null
                ? target.GetComponent<Collider>().bounds.center : target.transform.position;
            Fire(muzzle, point - muzzle.position, 1.2f, true, false, 0f, 1f, target.transform);
            fired++;
            yield return WaitUntilElapsed(fired * 0.06f);
        }
        Debug.Log("[ClassFix29][lock_volley] requested=" + targets.Count + " fired=" + fired, this);
        EndAction();
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
        bool availableNow = actionRoutine == null && Time.unscaledTime >= skillReadyAt[index]
            && mana != null && mana.CurrentMana >= SkillManaCosts[index];
        CatSkillHudUI.NotifySkillInput(index, availableNow);
        if (!availableNow) return;
        if (!mana.TrySpendClassMana(SkillManaCosts[index])) return;
        if (aimRuntime != null) aimRuntime.CancelAim("skill");
        skillReadyAt[index] = Time.unscaledTime + SkillCooldowns[index]
            * ClassFix28RoundBuffs.SkillCooldownMultiplier;
        actionRoutine = StartCoroutine(RunSkill(index));
    }

    private IEnumerator RunBasicAttack()
    {
        activeSkill = -2;
        actionStartedAt = Time.unscaledTime;
        networkState = "Gun_Attack";
        if (animator != null) animator.SetTrigger(AttackId);
        yield return WaitUntilElapsed(0.06f);
        bool dualWield = IsDualWieldUnlocked();
        RefreshWeaponVisibility();
        Fire(rightMuzzle, transform.forward, dualWield ? 0.6f : 1f, true);
        if (dualWield)
        {
            yield return WaitUntilElapsed(0.10f);
            Fire(leftMuzzle, transform.forward, 0.6f, true);
        }
        yield return WaitUntilElapsed(0.12f);
        EndAction();
    }

    private IEnumerator RunSkill(int index)
    {
        activeSkill = index;
        actionStartedAt = Time.unscaledTime;
        LastSkillStart = transform.position;
        LastSkillTravel = 0f;
        LastSkillDuration = 0f;
        LastSkillMaxHeight = 0f;
        LastSkillShotCount = 0;
        networkState = "Gun_Skill" + (index + 1);
        if (animator != null) animator.SetTrigger(SkillIds[index]);
        GameAudioManager.PlayGunSkill(index, transform);
        if (leftGun != null) leftGun.SetActive(IsDualWieldUnlocked() || index == 3);

        if (index == 0)
        {
            float[] frames = { 10f, 16f, 22f };
            Vector3 start = transform.position;
            Vector3 retreatDirection = -FlattenForward();
            for (int i = 0; i < frames.Length; i++)
            {
                yield return WaitUntilElapsed(frames[i] / 30f);
                BlenderSkillVfxRuntime.Spawn(PlayerClass.Gun, 0, rightMuzzle.position,
                    transform.forward, 0.22f, 0f, 1f);
                Fire(rightMuzzle, transform.forward, 1.10f, false);
                // Network transforms can reconcile between shots. Drive toward a
                // cumulative position authored from the skill start so each shot
                // still contributes to the full 0.65 m retreat.
                MoveWithWallStopTo(start + retreatDirection * (0.65f * (i + 1) / frames.Length));
            }
            LastSkillTravel = Vector3.Distance(start, transform.position);
            yield return WaitUntilElapsed(42f / 30f);
        }
        else if (index == 1)
        {
            yield return WaitUntilElapsed(24f / 30f);
            Vector3 aimedForward = FlattenForward();
            BlenderSkillVfxRuntime.Spawn(PlayerClass.Gun, 1,
                transform.position + aimedForward * 5f, aimedForward, 0.65f, 0f, 1.35f);
            Fire(rightMuzzle, transform.forward, 3.2f, false, true, 5f, 0.5f);
            yield return WaitUntilElapsed(46f / 30f);
        }
        else if (index == 2)
        {
            Vector3 baseForward = FlattenForward();
            GunCatAmmoSlot consumedAmmo = null;
            if (ammoRuntime == null || ammoRuntime.TryConsumeBasicShot(out consumedAmmo))
            {
                yield return WaitUntilElapsed(12f / 30f);
                BlenderSkillVfxRuntime.Spawn(PlayerClass.Gun, 2, transform.position,
                    baseForward, 0.9f, 0f, 1.4f, transform);
                const int shotCount = 36;
                const float shotWindow = 16f / 30f;
                for (int i = 0; i < shotCount; i++)
                {
                    float targetElapsed = 12f / 30f + shotWindow * i / (shotCount - 1);
                    yield return WaitUntilElapsed(targetElapsed);
                    float angle = i * 10f;
                    Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * baseForward;
                    transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                    if (body != null) body.rotation = transform.rotation;
                    Fire(rightMuzzle, direction, 0.18f, false, false);
                }
            }
            yield return WaitUntilElapsed(46f / 30f);
        }
        else
        {
            Vector3 ground = transform.position;
            yield return WaitUntilElapsed(11f / 30f);
            BlenderSkillVfxRuntime.Spawn(PlayerClass.Gun, 3, transform.position,
                FlattenForward(), 2.4f, 0f, 1.2f, transform);
            yield return RunDualWieldBarrage(ground);
        }
        EndAction();
    }

    private IEnumerator RunDualWieldBarrage(Vector3 ground)
    {
        const float duration = 2.40f;
        const float shotInterval = 0.08f;
        const float hoverHeight = 0.90f;
        const int maxShots = 30;
        float started = Time.unscaledTime;
        float nextShotAt = shotInterval;
        int shots = 0;
        while (Time.unscaledTime - started < duration)
        {
            float elapsed = Time.unscaledTime - started;
            Vector3 aimDirection = aimRuntime != null
                ? aimRuntime.ResolveShotDirection(rightMuzzle)
                : transform.forward;
            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(aimDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation,
                    360f * Time.unscaledDeltaTime);
                if (body != null) body.rotation = transform.rotation;
            }
            float height;
            if (elapsed < 0.45f) height = Mathf.Lerp(0f, hoverHeight, elapsed / 0.45f);
            else if (elapsed > 2.10f) height = Mathf.Lerp(hoverHeight, 0f, (elapsed - 2.10f) / 0.30f);
            else height = hoverHeight;
            LastSkillMaxHeight = Mathf.Max(LastSkillMaxHeight, height);
            CommitPosition(new Vector3(transform.position.x, ground.y + height, transform.position.z));

            while (shots < maxShots && elapsed + 0.001f >= nextShotAt)
            {
                // Fire resolves the current aim direction on every shot, so pointer
                // movement during the hover changes the remaining barrage direction.
                Fire((shots & 1) == 0 ? rightMuzzle : leftMuzzle,
                    transform.forward, 0.45f, true);
                shots++;
                LastSkillShotCount = shots;
                nextShotAt += shotInterval;
            }
            yield return null;
        }
        // A rendered frame can advance from just below 2.40 directly past the
        // duration boundary, which previously dropped the authored 16th round.
        // Emit that final alternating shot immediately before landing.
        if (shots < maxShots)
        {
            Fire((shots & 1) == 0 ? rightMuzzle : leftMuzzle,
                transform.forward, 0.45f, true);
            shots++;
            LastSkillShotCount = shots;
        }
        CommitPosition(new Vector3(transform.position.x, ground.y, transform.position.z));
        LastSkillDuration = Time.unscaledTime - started;
    }

    private IEnumerator MoveVertical(Vector3 ground, float height, float targetElapsed)
    {
        float startElapsed = Time.unscaledTime - actionStartedAt;
        float startHeight = transform.position.y - ground.y;
        while (Time.unscaledTime - actionStartedAt < targetElapsed)
        {
            float t = Mathf.InverseLerp(startElapsed, targetElapsed, Time.unscaledTime - actionStartedAt);
            CommitPosition(new Vector3(transform.position.x, ground.y + Mathf.Lerp(startHeight, height, t), transform.position.z));
            yield return null;
        }
        CommitPosition(new Vector3(transform.position.x, ground.y + height, transform.position.z));
    }

    private void Fire(Transform muzzle, Vector3 shotDirection, float multiplier, bool consumeAmmo,
        bool useAim = true, float explosionRadius = 0f, float explosionEdgeScale = 1f, Transform homingTarget = null)
    {
        if (muzzle == null) return;
        Vector3 direction = useAim && aimRuntime != null
            ? aimRuntime.ResolveShotDirection(muzzle)
            : shotDirection.sqrMagnitude > 0.001f ? shotDirection.normalized : FlattenForward();
        GunCatAmmoSlot objectAmmo = null;
        if (consumeAmmo && ammoRuntime != null && !ammoRuntime.TryConsumeBasicShot(out objectAmmo)) return;
        Queue<GunCatProjectile> pool = objectAmmo != null ? availableObject : availableBasic;
        if (pool.Count == 0) return;

        BreakableObject special = null;
        bool specialRoll = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        specialRoll = LegacyInstantConversionEnabled && ValidationForceNextSpecial;
        ValidationForceNextSpecial = false;
#endif
        if (LegacyInstantConversionEnabled)
        {
            bool hostCanConvert = !StoryCoopRuntimeBridge.IsInRoom || StoryCoopRuntimeBridge.IsHost;
            float specialChance = SpecialChance;
            CatPrototype.BuildingBreak.BuildingBreakBootstrap bootstrap = CatPrototype.BuildingBreak.BuildingBreakBootstrap.Instance;
            if (bootstrap != null && bootstrap.Progress != null)
                specialChance += bootstrap.Progress.GunSpecialChanceBonus;
            specialRoll |= Random.value < specialChance;
            if (consumeAmmo && hostCanConvert && Time.unscaledTime >= nextSpecialAt && specialRoll)
            {
                special = FindSpecialAmmo();
                if (special != null)
                {
                    nextSpecialAt = Time.unscaledTime + SpecialCooldown;
                    SpecialShotsFired++;
                    string message = "<color=#FFC93C>사물 장전!</color>";
                    FloatingFeedbackUI.Show(message, transform.position + Vector3.up * 1.35f);
                    StoryCoopRuntimeBridge.ShowMessage(message, 0.8f);
                }
            }
        }

        Vector3 origin = muzzle.position;
        float shotSpeed = (objectAmmo != null ? SpecialSpeed : NormalSpeed)
            * (1f + ClassCombatUpgradeRuntime.Effect(PlayerClass.Gun, 3));
        bool twinShot = objectAmmo != null && objectAmmo.effectType == "twin";
        float shotDamage = BaseDamage * multiplier * (objectAmmo != null ? objectAmmo.damageMultiplier : 1f)
            * (twinShot ? 0.6f : 1f) * ClassFix28RoundBuffs.DamageMultiplier;
        if (objectAmmo != null && objectAmmo.effectType == "explosive")
        {
            explosionRadius = Mathf.Max(3f, explosionRadius);
            explosionEdgeScale = Mathf.Min(explosionEdgeScale, 0.5f);
        }
        Vector3 firstDirection = twinShot
            ? Quaternion.AngleAxis(-2.5f, Vector3.up) * direction
            : direction;
        GunCatProjectile projectile = pool.Dequeue();
        projectile.Launch(this, origin, firstDirection, shotSpeed, Range, shotDamage, objectAmmo, ReleaseProjectile,
            false, activeSkill, explosionRadius, explosionEdgeScale);
        if (homingTarget != null) projectile.SetHomingTarget(homingTarget, 8f);
        if (twinShot && pool.Count > 0)
        {
            GunCatProjectile twin = pool.Dequeue();
            Vector3 twinDirection = Quaternion.AngleAxis(2.5f, Vector3.up) * direction;
            twin.Launch(this, origin, twinDirection, shotSpeed, Range, shotDamage, objectAmmo.Clone(), ReleaseProjectile,
                false, activeSkill, explosionRadius, explosionEdgeScale);
        }
        if (objectAmmo != null) { LastSpecialVisualMaxDimension = projectile.CurrentVisualMaxDimension; SpecialShotsFired++; }
        TotalShotsFired += twinShot ? 2 : 1;
        GameAudioManager.PlayGunShot(origin, transform);
        LastShotElapsed = Time.unscaledTime - actionStartedAt;
        StoryCoopRuntimeBridge.NotifySkillVisual(new StoryCoopSkillVisualEvent(
            objectAmmo != null ? 102 : 101, origin, origin + firstDirection * Range, 0.3f,
            objectAmmo != null ? objectAmmo.maxDimension : 1f, Range / shotSpeed,
            objectAmmo != null ? objectAmmo.type : string.Empty));
        if (twinShot)
        {
            Vector3 twinDirection = Quaternion.AngleAxis(2.5f, Vector3.up) * direction;
            StoryCoopRuntimeBridge.NotifySkillVisual(new StoryCoopSkillVisualEvent(
                102, origin, origin + twinDirection * Range, 0.3f,
                objectAmmo.maxDimension, Range / shotSpeed, objectAmmo.type));
        }
        Debug.Log("[GunCat] shot=" + TotalShotsFired + " elapsed=" + LastShotElapsed.ToString("F3")
            + " multiplier=" + multiplier.ToString("F2") + " legacyChance=0"
            + " objectAmmo=" + (objectAmmo != null), this);
    }

    private static float CurrentCooldownScale()
    {
        CatPrototype.BuildingBreak.BuildingBreakBootstrap bootstrap = CatPrototype.BuildingBreak.BuildingBreakBootstrap.Instance;
        float buildingScale = bootstrap != null && bootstrap.Progress != null ? bootstrap.Progress.CooldownScale : 1f;
        return buildingScale * Mathf.Max(0.2f, 1f + ClassCombatUpgradeRuntime.Effect(PlayerClass.Gun, 0))
            * ClassFix28RoundBuffs.AttackCooldownMultiplier;
    }

    private BreakableObject FindSpecialAmmo()
    {
        BreakableObject[] candidates = FindObjectsOfType<BreakableObject>();
        HashSet<BreakableObject> held = new HashSet<BreakableObject>();
        CatCarryThrow[] carries = FindObjectsOfType<CatCarryThrow>();
        for (int i = 0; i < carries.Length; i++) if (carries[i] != null && carries[i].HeldObject != null) held.Add(carries[i].HeldObject);
        float best = 64f;
        BreakableObject result = null;
        for (int i = 0; i < candidates.Length; i++)
        {
            BreakableObject item = candidates[i];
            if (item == null || !item.gameObject.activeInHierarchy || item.IsDestroyed || !item.isDestroyable
                || item.GetComponent<CatPrototype.BuildingBreak.BuildingBreakTarget>() != null || held.Contains(item)
                || item.GetComponentInParent<CatController>() != null) continue;
            CoopBreakableState state = item.GetComponent<CoopBreakableState>();
            if (state != null && state.IsHeld) continue;
            Renderer renderer = item.GetComponentInChildren<Renderer>(true);
            MeshFilter filter = item.GetComponentInChildren<MeshFilter>(true);
            if (renderer == null || filter == null || filter.sharedMesh == null) continue;
            float sqr = (renderer.bounds.center - transform.position).sqrMagnitude;
            if (sqr < best) { best = sqr; result = item; }
        }
        return result;
    }

    private void EnsurePool()
    {
        if (allProjectiles.Count > 0 || projectilePrefab == null) return;
        GameObject root = new GameObject(name + "_GunCatProjectilePool");
        poolRoot = root.transform;
        for (int i = 0; i < BasicPoolSize + ObjectPoolSize; i++)
        {
            GameObject item = Instantiate(projectilePrefab, poolRoot);
            item.name = "GunCatProjectile_" + i.ToString("00");
            GunCatProjectile projectile = item.GetComponent<GunCatProjectile>();
            item.SetActive(false);
            allProjectiles.Add(projectile);
            if (i < BasicPoolSize) availableBasic.Enqueue(projectile);
            else { availableObject.Enqueue(projectile); objectProjectiles.Add(projectile); }
        }
        Debug.Log("[GunCat] projectile_pool_ready count=" + allProjectiles.Count, this);
    }

    private void ReleaseProjectile(GunCatProjectile projectile)
    {
        if (projectile == null) return;
        Queue<GunCatProjectile> pool = objectProjectiles.Contains(projectile) ? availableObject : availableBasic;
        if (!pool.Contains(projectile)) pool.Enqueue(projectile);
    }

    private void EndAction()
    {
        RefreshWeaponVisibility();
        activeSkill = -1;
        actionRoutine = null;
        networkState = "Gun_Idle";
        if (animator != null) animator.SetBool(LockedId, false);
    }

    public bool IsDualWieldUnlocked()
    {
        return classActive && ClassCombatUpgradeRuntime.Level(PlayerClass.Gun, 4) > 0
            && leftMuzzle != null && leftGun != null;
    }

    public void RefreshWeaponVisibility()
    {
        observedDualWieldUnlocked = IsDualWieldUnlocked();
        if (leftGun != null) leftGun.SetActive(observedDualWieldUnlocked);
    }

    public static void ApplyAuthoredGunTransforms(Transform visualRoot)
    {
        if (visualRoot == null) return;
        Transform right = FindByName(visualRoot, "GunRightExternal");
        Transform left = FindByName(visualRoot, "GunLeftExternal");
        if (right != null)
        {
            right.localPosition = new Vector3(-0.057f, 0.031f, 0.033f);
            right.localRotation = Quaternion.Euler(179.935f, 89.981f, 73.764f);
            right.localScale = Vector3.one * 0.35f;
        }
        if (left != null)
        {
            left.localPosition = new Vector3(0f, 0.06306949f, -0.04200655f);
            left.localRotation = Quaternion.Euler(-184.064f, 271.414f, -70.80402f);
            left.localScale = Vector3.one * 0.35f;
        }
    }

    private IEnumerator WaitUntilElapsed(float seconds)
    {
        while (Time.unscaledTime - actionStartedAt < seconds) yield return null;
    }

    private void MoveWithWallStop(Vector3 delta)
    {
        Vector3 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.zero;
        float distance = delta.magnitude;
        Vector3 start = transform.position;
        RaycastHit[] hits = Physics.SphereCastAll(start + Vector3.up * 0.55f, 0.35f, direction,
            distance, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider candidate = hits[i].collider;
            if (candidate == null || candidate.transform.IsChildOf(transform)
                || candidate.GetComponentInParent<CatController>() != null
                || candidate.GetComponentInParent<PlayerClassRuntime>() != null)
                continue;
            distance = Mathf.Max(0f, hits[i].distance - 0.05f);
            break;
        }
        CommitPosition(start + direction * distance);
    }

    private void MoveWithWallStopTo(Vector3 destination)
    {
        MoveWithWallStop(destination - transform.position);
    }

    private void CommitPosition(Vector3 value)
    {
        transform.position = value;
        if (body != null)
        {
            body.position = value;
            if (!body.isKinematic) body.linearVelocity = Vector3.zero;
        }
    }

    private Vector3 FlattenForward()
    {
        Vector3 value = transform.forward;
        value.y = 0f;
        return value.sqrMagnitude > 0.001f ? value.normalized : Vector3.forward;
    }

    private static Transform FindByName(Transform root, string target)
    {
        if (root == null) return null;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++) if (all[i].name == target) return all[i];
        return null;
    }

    public void ApplyNetworkState(string state)
    {
        if (!classActive || animator == null || string.IsNullOrEmpty(state) || state == networkState) return;
        networkState = state;
        if (state == "Gun_Attack") animator.SetTrigger(AttackId);
        else if (state.StartsWith("Gun_Skill") && int.TryParse(state.Substring(state.Length - 1), out int skill))
            animator.SetTrigger(SkillIds[Mathf.Clamp(skill - 1, 0, 3)]);
        else if (state == "Gun_Jump") animator.SetTrigger(JumpId);
        else animator.SetFloat(SpeedId, state == "Gun_Run" ? 1f : state == "Gun_Walk" ? 0.5f : 0f);
    }

    public void SetNetworkAimPose(bool value)
    {
        networkAimPose = value;
        if (animator != null) animator.SetBool(LockedId, IsActionLocked || value);
        CacheAimBones();
    }

    private void LateUpdate()
    {
        // Animator evaluation may rewrite child weapon transforms every frame.
        // Reapply the authored sockets after animation for local and remote cats.
        ApplyAuthoredGunTransforms(gunVisualRoot);
        ApplyAimPoseLate();
    }

    public void ApplyAimPoseLate()
    {
        CacheAimBones();
        if (aimChest == null || aimRightArm == null || aimLeftArm == null) return;
        if (!networkAimPose || IsActionLocked) return;
        aimChest.localRotation *= chestAimOffset;
        aimRightArm.localRotation *= rightArmAimOffset;
        aimLeftArm.localRotation *= leftArmAimOffset;
    }

    private void CacheAimBones()
    {
        if (animator == null || aimChest != null) return;
        if (animator.isHuman)
        {
            aimChest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (aimChest == null) aimChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            aimRightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            aimLeftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            return;
        }

        // The shipped GunCat rig is Generic. GetBoneTransform throws for Generic
        // avatars, so resolve authored bones by hierarchy and use the muzzle chain
        // as a deterministic fallback rather than treating the rig as Humanoid.
        aimChest = FindByTokens(animator.transform, "chest", "spine2", "spine_02", "spine");
        aimRightArm = FindByTokens(animator.transform, "rightupperarm", "upperarm_r", "r_upperarm", "rightarm");
        aimLeftArm = FindByTokens(animator.transform, "leftupperarm", "upperarm_l", "l_upperarm", "leftarm");
        if (aimRightArm == null) aimRightArm = FindArmAncestor(rightMuzzle);
        if (aimLeftArm == null) aimLeftArm = FindArmAncestor(leftMuzzle);
        if (aimChest == null) aimChest = animator.transform;
        if (aimRightArm == null) aimRightArm = rightMuzzle != null ? rightMuzzle.parent : animator.transform;
        if (aimLeftArm == null) aimLeftArm = leftMuzzle != null ? leftMuzzle.parent : animator.transform;
    }

    private static Transform FindByTokens(Transform root, params string[] tokens)
    {
        if (root == null) return null;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int t = 0; t < tokens.Length; t++)
        {
            string token = tokens[t];
            for (int i = 0; i < all.Length; i++)
            {
                string normalized = all[i].name.Replace(" ", string.Empty).Replace(":", string.Empty).ToLowerInvariant();
                if (normalized.Contains(token)) return all[i];
            }
        }
        return null;
    }

    private static Transform FindArmAncestor(Transform muzzle)
    {
        Transform current = muzzle != null ? muzzle.parent : null;
        while (current != null)
        {
            string normalized = current.name.Replace(" ", string.Empty).Replace(":", string.Empty).ToLowerInvariant();
            if (normalized.Contains("arm")) return current;
            current = current.parent;
        }
        return null;
    }
}
