using UnityEngine;

public enum PlayerClass
{
    Basic = 0,
    Melee = 1,
    Gun = 2
}

public static class PlayerClassSelection
{
    public const string PreferenceKey = "player_class_v1";

    public static PlayerClass Current
    {
        get => (PlayerClass)Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, (int)PlayerClass.Basic), 0, 2);
        set
        {
            PlayerPrefs.SetInt(PreferenceKey, (int)value);
            PlayerPrefs.Save();
        }
    }

    public static string ToKorean(PlayerClass value) => value == PlayerClass.Melee
        ? "근접 고양이"
        : value == PlayerClass.Gun ? "권총 고양이" : "기본 고양이";
}

[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class PlayerClassRuntime : MonoBehaviour
{
    private const string MeleeRootName = "MeleeClassVisual";
    private const string GunRootName = "GunClassVisual";
    [SerializeField] private PlayerClass selectedClass;
    private GameObject meleeVisual;
    private MeleeCatCombatRuntime meleeCombat;
    private GameObject gunVisual;
    private GunCatCombatRuntime gunCombat;
    private GunCatAmmoRuntime gunAmmo;
    private GunCatAimRuntime gunAim;
    private ClassRightClickAction rightClickAction;
    private bool meleeFeetAligned;
    private bool gunFeetAligned;
    private bool basicVisualRepaired;

    public PlayerClass SelectedClass => selectedClass;
    public bool IsMelee => selectedClass == PlayerClass.Melee;
    public bool IsGun => selectedClass == PlayerClass.Gun;
    public MeleeCatCombatRuntime MeleeCombat => meleeCombat;
    public GunCatCombatRuntime GunCombat => gunCombat;
    public GunCatAimRuntime GunAim => gunAim;
    public ClassRightClickAction RightClickAction => rightClickAction;
    public bool AllowsObjectPickup => rightClickAction == ClassRightClickAction.GrabOrDrop;
    public bool UsesAimInput => rightClickAction == ClassRightClickAction.Aim;

    private void Awake()
    {
        if (gameObject.name == "CatPlayer") ApplyClass(PlayerClassSelection.Current);
    }

    private void LateUpdate()
    {
        if (selectedClass == PlayerClass.Basic)
        {
            Transform basicRoot = transform.Find("CatVisualRoot");
            if (basicRoot != null && basicRoot.gameObject.activeInHierarchy)
            {
                SetBasicVisualVisible(basicRoot);
                if (!basicVisualRepaired)
                {
                    CatAvatarVisualIntegrity.Repair(gameObject);
                    basicVisualRepaired = true;
                }
            }
            return;
        }
        GameObject activeVisual = IsMelee ? meleeVisual : IsGun ? gunVisual : null;
        if (activeVisual == null || !activeVisual.activeInHierarchy) return;
        SetClassVisualVisible(activeVisual);
        if (IsMelee && !meleeFeetAligned) AlignVisualToBodyFeet(meleeVisual, ref meleeFeetAligned, "melee");
        if (IsGun && !gunFeetAligned) AlignVisualToBodyFeet(gunVisual, ref gunFeetAligned, "gun");
    }

    public static PlayerClassRuntime Ensure(GameObject target, PlayerClass value)
    {
        if (target == null) return null;
        PlayerClassRuntime runtime = target.GetComponent<PlayerClassRuntime>();
        if (runtime == null) runtime = target.AddComponent<PlayerClassRuntime>();
        runtime.ApplyClass(value);
        return runtime;
    }

    public void ApplyClass(PlayerClass value)
    {
        selectedClass = value;
        basicVisualRepaired = false;
        ClassAssetCatalog catalog = ClassAssetCatalog.Load();
        PlayerClassDefinition definition = catalog != null ? catalog.GetClass(value) : null;
        // Runtime class input ownership must remain correct even when an old
        // catalog asset still contains the former GrabOrDrop value.
        rightClickAction = value == PlayerClass.Gun
            ? ClassRightClickAction.Aim
            : value == PlayerClass.Melee
                ? ClassRightClickAction.Disabled
                : ClassRightClickAction.GrabOrDrop;
        CatController ownerController = GetComponent<CatController>();
        Transform basicRoot = transform.Find("CatVisualRoot");
        Transform existing = transform.Find(MeleeRootName);
        Transform existingGun = transform.Find(GunRootName);
        if (existing != null) meleeVisual = existing.gameObject;
        if (existingGun != null) gunVisual = existingGun.gameObject;

        meleeCombat = GetComponent<MeleeCatCombatRuntime>();
        gunCombat = GetComponent<GunCatCombatRuntime>();
        gunAmmo = GetComponent<GunCatAmmoRuntime>();
        gunAim = GetComponent<GunCatAimRuntime>();
        if (meleeCombat != null) meleeCombat.SetClassActive(false);
        if (gunCombat != null) gunCombat.SetClassActive(false);
        if (gunAmmo != null) gunAmmo.Configure(false);
        if (gunAim != null) gunAim.Configure(false, null, null, null);
        if (ownerController != null) ownerController.RegisterClassInputRuntime(this, null);
        if (meleeVisual != null) meleeVisual.SetActive(false);
        if (gunVisual != null) gunVisual.SetActive(false);

        if (value == PlayerClass.Basic)
        {
            if (basicRoot != null)
            {
                basicRoot.gameObject.SetActive(true);
                SetBasicVisualVisible(basicRoot);
                CatAvatarVisualIntegrity.Repair(gameObject);
                basicVisualRepaired = true;
            }
            SetLegacyAnimationEnabled(true);
            CatSkillHudUI.RefreshClassPresentationNow(value);
            return;
        }

        if (value == PlayerClass.Melee && meleeVisual == null)
        {
            if (catalog == null || catalog.meleeCatPrefab == null)
            {
                Debug.LogError("[ClassSelect] MeleeCat prefab/catalog missing.", this);
                return;
            }
            meleeVisual = Instantiate(catalog.meleeCatPrefab, transform);
            meleeVisual.name = MeleeRootName;
            meleeVisual.transform.localPosition = Vector3.zero;
            meleeVisual.transform.localRotation = Quaternion.identity;
            meleeVisual.transform.localScale = Vector3.one;
            meleeFeetAligned = false;
        }

        if (value == PlayerClass.Gun && gunVisual == null)
        {
            if (catalog == null || catalog.gunCatPrefab == null)
            {
                Debug.LogError("[ClassSelect] GunCat prefab/catalog missing.", this);
                return;
            }
            gunVisual = Instantiate(catalog.gunCatPrefab, transform);
            gunVisual.name = GunRootName;
            gunVisual.transform.localPosition = Vector3.zero;
            gunVisual.transform.localRotation = Quaternion.identity;
            gunVisual.transform.localScale = Vector3.one;
            gunFeetAligned = false;
        }

        if (basicRoot != null) basicRoot.gameObject.SetActive(false);
        SetLegacyAnimationEnabled(false);
        if (value == PlayerClass.Melee)
        {
            meleeVisual.SetActive(true);
            DisableClassVisualPhysics(meleeVisual);
            SetClassVisualVisible(meleeVisual);
            AlignVisualToBodyFeet(meleeVisual, ref meleeFeetAligned, "melee");
            if (meleeCombat == null) meleeCombat = gameObject.AddComponent<MeleeCatCombatRuntime>();
            meleeCombat.Configure(meleeVisual);
            meleeCombat.SetClassActive(true);
            StartCoroutine(RealignVisualAfterAnimator(meleeVisual, true));
        }
        else
        {
            gunVisual.SetActive(true);
            DisableClassVisualPhysics(gunVisual);
            SetClassVisualVisible(gunVisual);
            AlignVisualToBodyFeet(gunVisual, ref gunFeetAligned, "gun");
            if (gunCombat == null) gunCombat = gameObject.AddComponent<GunCatCombatRuntime>();
            if (gunAmmo == null) gunAmmo = gameObject.AddComponent<GunCatAmmoRuntime>();
            gunCombat.Configure(gunVisual, catalog != null ? catalog.gunCatBulletPrefab : null);
            gunCombat.ConfigureAmmo(gunAmmo);
            gunAmmo.Configure(true);
            if (gunAim == null) gunAim = gameObject.AddComponent<GunCatAimRuntime>();
            gunAim.Configure(gameObject.name == "CatPlayer", gunCombat, gunAmmo,
                catalog != null ? catalog.gunCatCrosshair : null);
            gunCombat.ConfigureAim(gunAim);
            if (ownerController != null) ownerController.RegisterClassInputRuntime(this, gunAim);
            gunCombat.SetClassActive(true);
            StartCoroutine(RealignVisualAfterAnimator(gunVisual, false));
        }
        Debug.Log("[ClassSelect] applied class=" + value + " object=" + gameObject.name, this);
        CatSkillHudUI.RefreshClassPresentationNow(value);
    }

    private static void SetClassVisualVisible(GameObject visual)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = true;
        Animator animator = visual.GetComponentInChildren<Animator>(true);
        if (animator != null) animator.enabled = true;
    }

    private static void SetBasicVisualVisible(Transform basicRoot)
    {
        if (basicRoot == null) return;
        Transform selected = basicRoot.Find("VarcoCatModel");
        Renderer[] renderers = basicRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            // Old scenes still contain primitive Body/Head/Paw renderers beside
            // the authored FBX.  They are socket markers/fallbacks, not a second
            // playable skin, and must never be re-enabled with the selected model.
            renderer.enabled = selected != null && renderer.transform.IsChildOf(selected);
        }
        Animator animator = selected != null ? selected.GetComponentInChildren<Animator>(true) : null;
        if (animator != null) animator.enabled = true;
    }

    private static void DisableClassVisualPhysics(GameObject visual)
    {
        if (visual == null) return;
        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = false;
        Rigidbody[] bodies = visual.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null) continue;
            bodies[i].isKinematic = true;
            bodies[i].detectCollisions = false;
        }
    }

    private void AlignVisualToBodyFeet(GameObject visual, ref bool aligned, string label)
    {
        Collider body = GetComponent<Collider>();
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (body == null || renderers.Length == 0) return;
        float visualBottom = float.PositiveInfinity;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) visualBottom = Mathf.Min(visualBottom, renderers[i].bounds.min.y);
        }
        if (float.IsInfinity(visualBottom) || float.IsNaN(visualBottom)) return;
        float parentScaleY = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));
        float delta = body.bounds.min.y - visualBottom;
        if (Mathf.Abs(delta) > 0.002f)
        {
            Vector3 local = visual.transform.localPosition;
            local.y += delta / parentScaleY;
            visual.transform.localPosition = local;
        }
        aligned = true;
        Debug.Log("[ClassSelect] " + label + "_feet_aligned delta=" + delta.ToString("F3")
            + " visualLocal=" + visual.transform.localPosition.ToString("F3"), this);
    }

    public void RealignActiveVisualToBodyFeet()
    {
        if (IsMelee && meleeVisual != null)
        {
            meleeFeetAligned = false;
            AlignVisualToBodyFeet(meleeVisual, ref meleeFeetAligned, "melee");
        }
        else if (IsGun && gunVisual != null)
        {
            gunFeetAligned = false;
            AlignVisualToBodyFeet(gunVisual, ref gunFeetAligned, "gun");
        }
    }

    public void ResetActionStateForStageStart()
    {
        if (meleeCombat != null) meleeCombat.ResetActionStateForStageStart();
        if (gunCombat != null) gunCombat.ResetActionStateForStageStart();
    }

    public void ForceStageSelectIdlePose()
    {
        if (selectedClass == PlayerClass.Melee && meleeCombat != null)
            meleeCombat.ForceStageSelectIdlePose();
    }

    private System.Collections.IEnumerator RealignVisualAfterAnimator(GameObject expectedVisual, bool melee)
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        if (this == null || expectedVisual == null || !expectedVisual.activeInHierarchy) yield break;
        if (melee)
        {
            meleeFeetAligned = false;
            AlignVisualToBodyFeet(expectedVisual, ref meleeFeetAligned, "melee_deferred");
        }
        else
        {
            gunFeetAligned = false;
            AlignVisualToBodyFeet(expectedVisual, ref gunFeetAligned, "gun_deferred");
        }
    }

    private void SetLegacyAnimationEnabled(bool enabledValue)
    {
        CatAnimationStateDriver driver = GetComponent<CatAnimationStateDriver>();
        if (driver != null) driver.enabled = enabledValue;
        CatPawAnimator paws = GetComponent<CatPawAnimator>();
        if (paws != null) paws.enabled = enabledValue;
    }
}
