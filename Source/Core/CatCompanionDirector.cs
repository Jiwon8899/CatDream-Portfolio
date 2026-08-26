using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CatCompanionDirector : MonoBehaviour
{
    private const int MaxCompanionCount = 7;

    private readonly List<CatCompanionAlly> companions = new List<CatCompanionAlly>();
    private CatController owner;
    private bool active;
    private int forcedMinimumCount;
    private float nextSupportStrikeAt;

    public int ActiveCompanionCount => companions.Count;
    public bool CompanionsActive => active;
    public IReadOnlyList<CatCompanionAlly> Companions => companions;

    public static CatCompanionDirector EnsureFor(CatController owner)
    {
        if (owner == null)
        {
            return null;
        }

        CatCompanionDirector director = owner.GetComponent<CatCompanionDirector>();
        if (director == null)
        {
            director = owner.gameObject.AddComponent<CatCompanionDirector>();
        }

        director.owner = owner;
        return director;
    }

    public void SetCompanionsActive(bool enabled)
    {
        active = enabled;
        SyncCompanionCount();
        for (int i = 0; i < companions.Count; i++)
        {
            if (companions[i] != null)
            {
                companions[i].gameObject.SetActive(active);
            }
        }
        nextSupportStrikeAt = Time.time + 0.75f;
    }

    public void SyncCompanionCount()
    {
        if (!active)
        {
            return;
        }

        if (owner == null)
        {
            owner = GetComponent<CatController>();
        }

        int desired = Mathf.Clamp(Mathf.Max(UpgradeDatabase.GetLevel("cat_companion"), forcedMinimumCount),
            0, MaxCompanionCount);
        while (companions.Count < desired)
        {
            companions.Add(CreateCompanion(companions.Count));
        }

        for (int i = 0; i < companions.Count; i++)
        {
            bool shouldBeActive = i < desired && active;
            if (companions[i] != null)
            {
                companions[i].gameObject.SetActive(shouldBeActive);
            }
        }
    }

    public void EnsureStageSixFormation(int minimumCount)
    {
        active = true;
        forcedMinimumCount = Mathf.Clamp(minimumCount, 0, MaxCompanionCount);
        int desired = Mathf.Clamp(Mathf.Max(UpgradeDatabase.GetLevel("cat_companion"), forcedMinimumCount),
            0, MaxCompanionCount);
        while (companions.Count < desired) companions.Add(CreateCompanion(companions.Count));
        for (int i = 0; i < companions.Count; i++)
        {
            CatCompanionAlly ally = companions[i];
            if (ally == null) continue;
            ally.gameObject.SetActive(i < desired);
            if (i < desired)
            {
                CapsuleCollider capsule = ally.GetComponent<CapsuleCollider>();
                Vector3 requested = transform.position + GetSpawnOffset(i);
                ally.transform.position = ResolveGroundPosition(requested, capsule, gameObject.scene, transform);
            }
        }
        Debug.Log("[ClassFix29][stage6_companions] count=" + desired + " owner=" + name, this);
    }

    private CatCompanionAlly CreateCompanion(int index)
    {
        GameObject allyObject = new GameObject("CatCompanionAlly_" + (index + 1));
        allyObject.transform.position = transform.position + GetSpawnOffset(index);
        allyObject.transform.rotation = transform.rotation;

        Rigidbody rb = allyObject.AddComponent<Rigidbody>();
        rb.mass = 2.2f;
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.linearDamping = 0.35f;
        rb.angularDamping = 0.6f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        CapsuleCollider collider = allyObject.AddComponent<CapsuleCollider>();
        collider.height = 1.05f;
        collider.radius = 0.28f;
        collider.center = new Vector3(0f, 0.52f, 0f);
        collider.isTrigger = true;

        allyObject.transform.position = ResolveGroundPosition(allyObject.transform.position, collider, allyObject.scene, owner != null ? owner.transform : null);
        CatAnimationStateDriver animationDriver = allyObject.AddComponent<CatAnimationStateDriver>();
        animationDriver.EnsureRuntimeVisualReady();
        animationDriver.ApplyRuntimeVisualVisibilityNow();
        animationDriver.RealignRuntimeVisualToBodyFeet();

        CatCompanionAlly ally = allyObject.AddComponent<CatCompanionAlly>();
        ally.Initialize(owner, index);
        allyObject.SetActive(active);
        return ally;
    }

    private void Update()
    {
        if (!active || Time.time < nextSupportStrikeAt)
        {
            return;
        }

        nextSupportStrikeAt = Time.time + 0.75f;
        SyncCompanionCount();
    }

    private void ApplySupportStrike()
    {
        if (!CanSupportStrike())
        {
            return;
        }

        int companionLevel = Mathf.Clamp(UpgradeDatabase.GetLevel("cat_companion"), 0, MaxCompanionCount);
        if (companionLevel <= 0)
        {
            return;
        }

        if (owner == null)
        {
            owner = GetComponent<CatController>();
        }

        BreakableObject target = FindNearestSupportTarget();
        if (target == null)
        {
            return;
        }

        float ownerPower = owner != null ? owner.GetPunchPowerRating() : 1f;
        float damage = 7f + companionLevel * 3.5f + ownerPower * 0.35f;
        Vector3 direction = owner != null
            ? (target.transform.position - owner.transform.position).normalized
            : transform.forward;
        bool damaged = target.ApplyDamage(damage, true);
        if (damaged)
        {
            GameAudioManager.PlayPunchSwing();
        }
        if (damaged && target != null && !target.IsDestroyed)
        {
            target.ApplyInteractionImpulse(direction * 1.4f + Vector3.up * 0.15f, ForceMode.Impulse);
        }
    }

    private bool CanSupportStrike()
    {
        if (!active)
        {
            return false;
        }

        GameManager manager = GameManager.Instance;
        if (manager != null)
        {
            if (!manager.CompanionsEnabledForCurrentScene || manager.CurrentState != GameState.Playing || manager.IsStageOpeningActive)
            {
                return false;
            }
        }

        if (owner != null && owner.gameObject.scene.IsValid())
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && owner.gameObject.scene != activeScene)
            {
                return false;
            }
        }

        return true;
    }

    private BreakableObject FindNearestSupportTarget()
    {
        Vector3 origin = owner != null ? owner.transform.position : transform.position;
        float radius = 4.5f + Mathf.Clamp(UpgradeDatabase.GetLevel("cat_companion"), 0, MaxCompanionCount) * 0.65f;
        Scene searchScene = owner != null && owner.gameObject.scene.IsValid() ? owner.gameObject.scene : gameObject.scene;
        BreakableObject[] breakables = FindObjectsOfType<BreakableObject>();
        BreakableObject best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < breakables.Length; i++)
        {
            BreakableObject candidate = breakables[i];
            if (candidate == null || !candidate.isDestroyable || candidate.IsDestroyed || !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (searchScene.IsValid() && candidate.gameObject.scene != searchScene)
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(candidate.transform.position - origin);
            if (distance <= radius * radius && distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static Vector3 GetSpawnOffset(int index)
    {
        float angle = (index * 360f / MaxCompanionCount + 18f) * Mathf.Deg2Rad;
        float radius = index < 4 ? 1.15f : 1.62f;
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
    }

    private static Vector3 ResolveGroundPosition(Vector3 position, Collider collider, Scene scene, Transform ownerTransform)
    {
        RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 8f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
        float bestY = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || hit.normal.y < 0.45f)
            {
                continue;
            }
            if (scene.IsValid() && hit.collider.gameObject.scene != scene)
            {
                continue;
            }
            if (ownerTransform != null && hit.collider.transform.IsChildOf(ownerTransform))
            {
                continue;
            }
            if (hit.collider.GetComponentInParent<CatCompanionAlly>() != null || hit.collider.GetComponentInParent<BreakableObject>() != null)
            {
                continue;
            }
            if (hit.point.y > bestY)
            {
                bestY = hit.point.y;
                position.y = hit.point.y + GetColliderBottomOffset(collider) + 0.012f;
            }
        }

        return position;
    }

    private static float GetColliderBottomOffset(Collider collider)
    {
        CapsuleCollider capsule = collider as CapsuleCollider;
        if (capsule != null)
        {
            return Mathf.Max(0.005f, capsule.height * 0.5f - capsule.center.y);
        }

        return 0.012f;
    }

    private void CloneOwnerVisual(Transform companionRoot)
    {
        Transform visualRoot = owner != null ? owner.transform.Find("CatVisualRoot") : null;
        if (visualRoot != null && visualRoot.GetComponentInChildren<BreakableObject>(true) == null)
        {
            GameObject clone = Instantiate(visualRoot.gameObject, companionRoot);
            clone.name = "CatVisualRoot";
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one;
            StripPhysicsFromVisual(clone);
            return;
        }

        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fallback.name = "CatCompanionVisual";
        fallback.transform.SetParent(companionRoot, false);
        fallback.transform.localPosition = new Vector3(0f, 0.62f, 0f);
        fallback.transform.localScale = new Vector3(0.62f, 0.54f, 0.62f);
        Collider collider = fallback.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
        Renderer renderer = fallback.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader != null)
            {
                renderer.sharedMaterial = new Material(shader) { color = new Color(0.85f, 0.82f, 0.76f, 1f) };
            }
        }
    }

    public static void CloneOwnerVisual(Transform ownerTransform, Transform companionRoot, Color tint)
    {
        if (companionRoot == null)
        {
            return;
        }

        Transform visualRoot = ownerTransform != null ? ownerTransform.Find("CatVisualRoot") : null;
        if (visualRoot != null && visualRoot.GetComponentInChildren<BreakableObject>(true) == null)
        {
            GameObject clone = Instantiate(visualRoot.gameObject, companionRoot);
            clone.name = "CatVisualRoot";
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one;
            StripPhysicsFromVisual(clone);
            Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].material.color *= tint;
                }
            }
            return;
        }

        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        fallback.name = "CoopAiCatVisual";
        fallback.transform.SetParent(companionRoot, false);
        fallback.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        fallback.transform.localScale = new Vector3(0.62f, 0.54f, 0.62f);
        Collider fallbackCollider = fallback.GetComponent<Collider>();
        if (fallbackCollider != null)
        {
            Destroy(fallbackCollider);
        }
        Renderer fallbackRenderer = fallback.GetComponent<Renderer>();
        if (fallbackRenderer != null)
        {
            fallbackRenderer.material.color = tint;
        }
    }

    private static void StripPhysicsFromVisual(GameObject root)
    {
        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null)
            {
                Destroy(bodies[i]);
            }
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                Destroy(colliders[i]);
            }
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
            {
                behaviours[i].enabled = false;
            }
        }
    }
}
