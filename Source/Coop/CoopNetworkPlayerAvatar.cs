using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CoopNetworkPlayerAvatar : MonoBehaviourPun, IPunObservable, IPunInstantiateMagicCallback
{
    private CatController source;
    private CatAnimationStateDriver animationDriver;
    private CatCarryThrow sourceCarry;
    private Renderer[] renderers;
    private string receivedState = "Idle";
    private int receivedAttackVariant = -1;
    private int receivedWeaponVariant = -1;
    private bool receivedDash;
    private bool receivedAim;
    private string receivedHeldKey = string.Empty;
    private Vector3 receivedHeldPosition;
    private Quaternion receivedHeldRotation = Quaternion.identity;
    private Vector3 receivedHeldScale = Vector3.one;
    private string appliedState = string.Empty;
    private int appliedAttackVariant = -1;
    private int appliedWeaponVariant = -1;
    private string appliedHeldKey = string.Empty;
    private GameObject heldVisual;
    private readonly int[] remoteSkillVisualCounts = new int[4];
    private Coroutine remoteScaleRoutine;
    private readonly List<GameObject> remoteCompanions = new List<GameObject>(7);
    private readonly Vector3[] receivedCompanionPositions = new Vector3[7];
    private readonly Quaternion[] receivedCompanionRotations = new Quaternion[7];
    private int receivedCompanionCount;
    private readonly Vector3[] receivedWeaponLocalPositions = new Vector3[8];
    private readonly Quaternion[] receivedWeaponLocalRotations = new Quaternion[8];
    private int receivedWeaponCount;
    private Vector3 receivedPawnPosition;
    private Quaternion receivedPawnRotation = Quaternion.identity;
    private Vector3 receivedPawnScale = Vector3.one;
    private Vector3 receivedVisualRootLocalPosition;
    private bool receivedPawnPose;
    private bool ownerHeldEventsSubscribed;
    private PlayerClass receivedPlayerClass = PlayerClass.Basic;
    private PlayerClassRuntime classRuntime;

    public bool IsRemote => photonView != null && !photonView.IsMine;
    public int OwnerActorNumber => photonView != null && photonView.Owner != null ? photonView.Owner.ActorNumber : 0;
    public string NetworkState => photonView != null && photonView.IsMine && source != null
        ? source.GetComponent<CatAnimationStateDriver>()?.CurrentStateName ?? "Idle"
        : receivedState;
    public int NetworkAttackVariant => photonView != null && photonView.IsMine && source != null
        ? source.GetComponent<CatAnimationStateDriver>()?.LastAttackVariant ?? -1
        : receivedAttackVariant;
    public int NetworkWeaponVariant => IsWeaponState(NetworkState) ? (NetworkState == "SwordAttack1" ? 0 : 1) : -1;
    public bool NetworkDash => photonView != null && photonView.IsMine && source != null ? source.IsDashActive : receivedDash;
    public bool NetworkAim => photonView != null && photonView.IsMine && source != null
        ? source.GetComponent<PlayerClassRuntime>()?.GunCombat?.NetworkAim ?? false
        : receivedAim;
    public string NetworkHeldKey => photonView != null && photonView.IsMine ? GetLocalHeldKey() : receivedHeldKey;
    public string NetworkClipName => photonView != null && photonView.IsMine && source != null
        ? source.GetComponent<CatAnimationStateDriver>()?.CurrentClipName ?? string.Empty
        : animationDriver != null ? animationDriver.CurrentClipName : string.Empty;
    public int LastRemoteSkillIndex { get; private set; }
    public int RemoteGunProjectileVisualCount { get; private set; }
    public int RemoteClassEffectVisualCount { get; private set; }
    public int RemoteCompanionVisualCount => receivedCompanionCount;
    public int RemoteFloatingWeaponCount => receivedWeaponCount;
    public float MaxRemoteFloatingWeaponDistance
    {
        get
        {
            float maximum = 0f;
            for (int i = 0; i < receivedWeaponCount; i++)
                maximum = Mathf.Max(maximum, receivedWeaponLocalPositions[i].magnitude);
            return maximum;
        }
    }
    public int GetRemoteSkillVisualCount(int skillIndex) => skillIndex >= 1 && skillIndex <= 4
        ? remoteSkillVisualCounts[skillIndex - 1]
        : 0;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = photonView != null ? photonView.InstantiationData : null;
        if (data != null && data.Length > 0 && data[0] is int classValue)
        {
            receivedPlayerClass = (PlayerClass)Mathf.Clamp(classValue, (int)PlayerClass.Basic, (int)PlayerClass.Gun);
            classRuntime = PlayerClassRuntime.Ensure(gameObject, receivedPlayerClass);
        }
        Initialize();
    }

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        animationDriver = GetComponent<CatAnimationStateDriver>();
        classRuntime = GetComponent<PlayerClassRuntime>();
        renderers = GetComponentsInChildren<Renderer>(true);
        CatController controller = GetComponent<CatController>();
        Rigidbody body = GetComponent<Rigidbody>();
        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        // Both the remote avatars and the local transport proxy are CatPlayer
        // clones, so battle code searching for a CatController must be able to tell
        // them apart from the pawn the player actually drives.
        CoopRemotePawnRegistry.RegisterProxy(transform);

        if (photonView != null && photonView.IsMine)
        {
            gameObject.name = "LocalNetworkTransportProxy";
            GameObject local = GameObject.Find("CatPlayer");
            source = local != null && local != gameObject ? local.GetComponent<CatController>() : null;
            sourceCarry = source != null ? source.GetComponent<CatCarryThrow>() : null;
            SubscribeOwnerHeldEvents();
            SetRenderers(false);
            // The locally owned Photon object is a transport proxy only.  The
            // real, input-driven CatPlayer is already visible in this process.
            // Leaving this driver alive lets its LateUpdate re-enable the cloned
            // FBX renderer, which is the local-only "second/lying cat" seen while
            // attacking and after a retry.
            if (animationDriver != null) animationDriver.enabled = false;
            SetProxyAnimatorsEnabled(false);
            DisableOwnerProxyVisualHierarchy();
        }
        else
        {
            ConfigureRemoteVisual();
            CoopRemotePawnRegistry.Register(transform);
            StartCoroutine(StabilizeRemoteVisualAfterAnimator());
        }

        if (controller != null)
        {
            controller.SetManualInputEnabled(false);
            controller.enabled = false;
        }
        if (body != null)
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this || behaviour == animationDriver || behaviour is CoopNetworkNameplate
                || behaviour is PhotonView || behaviour is PhotonTransformView)
            {
                continue;
            }
            behaviour.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (photonView == null) return;
        if (photonView.IsMine)
        {
            if (source == null) Initialize();
            if (source != null)
            {
                transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            }
            SetRenderers(false);
            SetProxyAnimatorsEnabled(false);
            DisableOwnerProxyVisualHierarchy();
            return;
        }

        ApplyRemoteAnimation();
        if (receivedPawnPose)
        {
            transform.localScale = receivedPawnScale;
            Transform visualRoot = transform.Find("CatVisualRoot");
            if (visualRoot != null) visualRoot.localPosition = receivedVisualRootLocalPosition;
            bool buildingBreak = SceneManager.GetActiveScene().name == "BuildingBreakScene";
            if (buildingBreak || Vector3.Distance(transform.position, receivedPawnPosition) > 0.4f)
            {
                transform.SetPositionAndRotation(receivedPawnPosition, receivedPawnRotation);
            }
            else
            {
                float blend = 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime);
                transform.SetPositionAndRotation(
                    Vector3.Lerp(transform.position, receivedPawnPosition, blend),
                    Quaternion.Slerp(transform.rotation, receivedPawnRotation, blend));
            }
        }
        ApplyRemoteHeldVisual();
        ApplyRemoteCompanions();
        ApplyRemoteFloatingWeapons();
    }

    private void ApplyRemoteAnimation()
    {
        if (classRuntime != null && classRuntime.IsGun && classRuntime.GunAim != null)
            classRuntime.GunAim.ApplyNetworkAim(receivedAim);
        if (receivedState == appliedState)
        {
            if (classRuntime != null && classRuntime.IsGun && classRuntime.GunCombat != null)
                classRuntime.GunCombat.ApplyAimPoseLate();
            return;
        }
        if (classRuntime != null && classRuntime.IsMelee && classRuntime.MeleeCombat != null)
        {
            classRuntime.MeleeCombat.ApplyNetworkState(receivedState);
            appliedState = receivedState;
            return;
        }
        if (classRuntime != null && classRuntime.IsGun && classRuntime.GunCombat != null)
        {
            classRuntime.GunCombat.ApplyNetworkState(receivedState);
            classRuntime.GunCombat.ApplyAimPoseLate();
            appliedState = receivedState;
            return;
        }
        if (animationDriver == null) return;
        if (receivedState == "Attack_A" || receivedState == "Attack_B" || receivedState == "Attack_C")
        {
            if (receivedAttackVariant != appliedAttackVariant)
            {
                animationDriver.PlayAttackVariant(1, receivedAttackVariant);
                appliedAttackVariant = receivedAttackVariant;
                Debug.Log("[CoopSync] remote_attack actor=" + OwnerActorNumber + " variant=" + receivedAttackVariant + " state=" + receivedState);
            }
        }
        else if (IsWeaponState(receivedState))
        {
            if (receivedWeaponVariant != appliedWeaponVariant)
            {
                animationDriver.PlayWeaponAttackVariant(receivedWeaponVariant + 1, receivedWeaponVariant);
                appliedWeaponVariant = receivedWeaponVariant;
                Debug.Log("[CoopSync] remote_weapon_attack actor=" + OwnerActorNumber + " variant=" + receivedWeaponVariant + " state=" + receivedState);
            }
        }
        else
        {
            animationDriver.PlayNetworkMovementState(receivedState);
            Debug.Log("[StoryCoopFollowup] remote_movement actor=" + OwnerActorNumber
                + " state=" + animationDriver.CurrentStateName
                + " clip=" + animationDriver.CurrentClipName
                + " dash=" + receivedDash);
        }
        appliedState = receivedState;
    }

    public void PlayRemoteSkillVisual(StoryCoopSkillVisualEvent visualEvent)
    {
        if (!IsRemote) return;
        if (!string.IsNullOrEmpty(visualEvent.AudioId))
        {
            GameAudioManager.PlayRemoteClassAudio(visualEvent.AudioId, visualEvent.Target, transform);
            if (visualEvent.SkillIndex == 0) return;
        }
        if (visualEvent.SkillIndex == ClassSkillEffectPool.MeleeEventBase + 2 && visualEvent.Scale > 1f)
        {
            if (remoteScaleRoutine != null) StopCoroutine(remoteScaleRoutine);
            remoteScaleRoutine = StartCoroutine(ApplyRemoteGiantScale(visualEvent.Scale, visualEvent.Duration));
        }
        if (visualEvent.SkillIndex == 101 || visualEvent.SkillIndex == 102)
        {
            GunCatProjectile.PlayRemoteVisual(visualEvent);
            RemoteGunProjectileVisualCount++;
            LastRemoteSkillIndex = visualEvent.SkillIndex;
            Debug.Log("[GunCat] remote_projectile actor=" + OwnerActorNumber + " kind=" + visualEvent.SkillIndex);
            return;
        }
        if (ClassSkillEffectPool.TryPlayRemote(receivedPlayerClass, visualEvent))
        {
            RemoteClassEffectVisualCount++;
            int classSkillSlot = visualEvent.SkillIndex >= ClassSkillEffectPool.MeleeEventBase
                && visualEvent.SkillIndex <= ClassSkillEffectPool.MeleeEventBase + 3
                    ? visualEvent.SkillIndex - ClassSkillEffectPool.MeleeEventBase
                    : visualEvent.SkillIndex >= ClassSkillEffectPool.GunEventBase
                        && visualEvent.SkillIndex <= ClassSkillEffectPool.GunEventBase + 3
                            ? visualEvent.SkillIndex - ClassSkillEffectPool.GunEventBase
                            : -1;
            // Preserve the existing per-slot telemetry contract while routing the
            // class-specific payload through the pooled visual consumer.
            if (classSkillSlot >= 0 && classSkillSlot < remoteSkillVisualCounts.Length)
                remoteSkillVisualCounts[classSkillSlot]++;
            LastRemoteSkillIndex = visualEvent.SkillIndex;
            Debug.Log("[ClassUI] remote_class_effect actor=" + OwnerActorNumber
                + " class=" + receivedPlayerClass + " code=" + visualEvent.SkillIndex
                + " count=" + RemoteClassEffectVisualCount);
            return;
        }
        if (visualEvent.SkillIndex < 1 || visualEvent.SkillIndex > 4) return;
        CatSkillEffectRuntime.PlayNetworkSkillVisual(visualEvent);
        remoteSkillVisualCounts[visualEvent.SkillIndex - 1]++;
        LastRemoteSkillIndex = visualEvent.SkillIndex;
        if (visualEvent.SkillIndex == 4 && visualEvent.Scale > 1f)
        {
            if (remoteScaleRoutine != null) StopCoroutine(remoteScaleRoutine);
            remoteScaleRoutine = StartCoroutine(ApplyRemoteGiantScale(visualEvent.Scale, visualEvent.Duration));
        }
        Debug.Log("[StoryCoopFollowup] remote_skill actor=" + OwnerActorNumber
            + " skill=" + visualEvent.SkillIndex
            + " count=" + remoteSkillVisualCounts[visualEvent.SkillIndex - 1]);
    }

    private IEnumerator ApplyRemoteGiantScale(float scale, float duration)
    {
        Transform visual = transform.Find("CatVisualRoot/VarcoCatModel");
        if (visual == null) yield break;
        Vector3 original = visual.localScale;
        visual.localScale = original * Mathf.Max(1f, scale);
        float until = Time.time + Mathf.Max(0.1f, duration);
        while (Time.time < until)
        {
            yield return null;
        }
        if (this != null && visual != null) visual.localScale = original;
        remoteScaleRoutine = null;
    }

    private void AlignRemoteVisualFeet()
    {
        Transform floatingRoot = transform.Find("FloatingCatWeapons");
        Renderer[] visuals = GetComponentsInChildren<Renderer>(true);
        bool found = false;
        float bottom = float.MaxValue;
        for (int i = 0; i < visuals.Length; i++)
        {
            if (visuals[i] == null || !visuals[i].enabled
                || (floatingRoot != null && visuals[i].transform.IsChildOf(floatingRoot))) continue;
            bottom = Mathf.Min(bottom, visuals[i].bounds.min.y);
            found = true;
        }
        if (!found) return;
        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 12f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider candidate = hits[i].collider;
            if (candidate == null || candidate.transform.IsChildOf(transform) || candidate.isTrigger
                || candidate.GetComponentInParent<BreakableObject>() != null || hits[i].normal.y < 0.5f) continue;
            transform.position += Vector3.up * (hits[i].point.y + 0.01f - bottom);
            break;
        }
    }

    /// <summary>
    /// Resolves the hand socket this proxy attaches carried objects to.
    /// CoopNetworkPrefabPool replaces the proxy's whole runtime skin after the
    /// clone's Awake already cached a socket inside the old bone hierarchy, so
    /// the cached reference is destroyed by the time the first held-state event
    /// arrives.  Rebuilding here is what makes remote carrying work at all.
    /// </summary>
    /// <summary>Key this proxy last received, for validation diagnostics.</summary>
    public string LastReceivedHeldKey => receivedHeldKey;

    private static bool IsShieldHeldKey(string key) => !string.IsNullOrEmpty(key) && key.StartsWith("shield:", System.StringComparison.Ordinal);
    private static string StripHeldMode(string key) => IsShieldHeldKey(key) ? key.Substring(7) : key;

    private Transform ResolveAttachmentSocket(bool shield = false)
    {
        CatCarryThrow carry = GetComponent<CatCarryThrow>();
        return carry != null ? carry.EnsureAttachmentSocket(shield) : null;
    }

    private void ApplyRemoteHeldVisual()
    {
        if (receivedHeldKey != appliedHeldKey)
        {
            ReleaseRemoteHeldVisual();
            appliedHeldKey = receivedHeldKey;
            if (!string.IsNullOrEmpty(receivedHeldKey))
            {
                string objectKey = StripHeldMode(receivedHeldKey);
                bool shield = IsShieldHeldKey(receivedHeldKey);
                BreakableObject sourceObject = CoopNetworkSyncManager.FindBreakable(objectKey);
                Transform socket = ResolveAttachmentSocket(shield);
                if (sourceObject != null && socket != null)
                {
                    CoopNetworkSyncManager.SetRemoteHeld(objectKey, true);
                    heldVisual = Instantiate(sourceObject.gameObject, socket);
                    heldVisual.name = "NetworkHeldVisual";
                    CoopNetworkSyncManager.MakeVisualOnly(heldVisual);
                    Renderer[] heldRenderers = heldVisual.GetComponentsInChildren<Renderer>(true);
                    for (int i = 0; i < heldRenderers.Length; i++)
                    {
                        // The authored tier outline shell is deliberately never
                        // rendered; enabling it here produced a solid blob.
                        heldRenderers[i].enabled = heldRenderers[i].transform.name != "OutlineShell";
                    }
                }
                else
                {
                    Debug.LogWarning("[MultiFix16D] remote_held_attach_failed actor=" + OwnerActorNumber
                        + " key=" + receivedHeldKey + " source=" + (sourceObject != null) + " socket=" + (socket != null));
                }
            }
            Debug.Log("[CoopSync] remote_held actor=" + OwnerActorNumber
                + " active=" + (!string.IsNullOrEmpty(receivedHeldKey)) + " key=" + receivedHeldKey
                + " attached=" + (heldVisual != null));
        }
        if (heldVisual == null) return;
        // Re-parenting keeps the object following the carrier in real time even
        // when the skin is rebuilt mid-round; a position stream would freeze it.
        Transform currentSocket = ResolveAttachmentSocket(IsShieldHeldKey(receivedHeldKey));
        if (currentSocket != null && heldVisual.transform.parent != currentSocket)
            heldVisual.transform.SetParent(currentSocket, false);
        heldVisual.transform.localPosition = receivedHeldPosition;
        heldVisual.transform.localRotation = receivedHeldRotation;
        heldVisual.transform.localScale = receivedHeldScale;
    }

    /// <summary>
    /// Drops the remote visual and hands the authored object back. The original
    /// is placed where the carried visual was last seen, so a carrier leaving
    /// mid-round drops the object on the spot instead of deleting it.
    /// </summary>
    private void ReleaseRemoteHeldVisual()
    {
        if (!string.IsNullOrEmpty(appliedHeldKey))
        {
            string objectKey = StripHeldMode(appliedHeldKey);
            BreakableObject original = CoopNetworkSyncManager.FindBreakable(objectKey);
            if (original != null && heldVisual != null)
            {
                original.transform.SetPositionAndRotation(
                    heldVisual.transform.position, heldVisual.transform.rotation);
            }
            CoopNetworkSyncManager.SetRemoteHeld(objectKey, false);
        }
        if (heldVisual != null) Destroy(heldVisual);
        heldVisual = null;
    }

    private string GetLocalHeldKey()
    {
        if (sourceCarry == null || !sourceCarry.HasHeldObject) return string.Empty;
        string key = CoopNetworkSyncManager.GetObjectKey(sourceCarry.HeldObject);
        return sourceCarry.IsShieldMode ? "shield:" + key : key;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            CatAnimationStateDriver driver = source != null ? source.GetComponent<CatAnimationStateDriver>() : null;
            PlayerClassRuntime sourceClass = source != null ? source.GetComponent<PlayerClassRuntime>() : null;
            PlayerClass playerClass = sourceClass != null ? sourceClass.SelectedClass : PlayerClass.Basic;
            MeleeCatCombatRuntime melee = sourceClass != null ? sourceClass.MeleeCombat : null;
            GunCatCombatRuntime gun = sourceClass != null ? sourceClass.GunCombat : null;
            string state = playerClass == PlayerClass.Melee && melee != null
                ? melee.NetworkState
                : playerClass == PlayerClass.Gun && gun != null ? gun.NetworkState
                : driver != null ? driver.CurrentStateName : "Idle";
            int attack = driver != null ? driver.LastAttackVariant : -1;
            int weapon = IsWeaponState(state) ? (state == "SwordAttack1" ? 0 : 1) : -1;
            string heldKey = GetLocalHeldKey();
            Vector3 heldPosition = Vector3.zero;
            Quaternion heldRotation = Quaternion.identity;
            Vector3 heldScale = Vector3.one;
            if (!string.IsNullOrEmpty(heldKey) && sourceCarry != null && sourceCarry.HeldObject != null)
            {
                Transform held = sourceCarry.HeldObject.transform;
                heldPosition = held.localPosition;
                heldRotation = held.localRotation;
                heldScale = held.localScale;
            }
            stream.SendNext((int)playerClass);
            stream.SendNext(state);
            stream.SendNext(attack);
            stream.SendNext(weapon);
            stream.SendNext(source != null && source.IsDashActive);
            stream.SendNext(gun != null && gun.NetworkAim);
            stream.SendNext(heldKey);
            stream.SendNext(heldPosition);
            stream.SendNext(heldRotation);
            stream.SendNext(heldScale);
            Vector3 pawnPosition = source != null ? source.transform.position : transform.position;
            CoopSpawnOwnershipLock spawnLock = source != null
                ? source.GetComponent<CoopSpawnOwnershipLock>()
                : null;
            if (spawnLock != null && spawnLock.TryGetAuthoritativePosition(out Vector3 lockedPosition))
                pawnPosition = lockedPosition;
            stream.SendNext(pawnPosition);
            stream.SendNext(source != null ? source.transform.rotation : transform.rotation);
            stream.SendNext(source != null ? source.transform.localScale : transform.localScale);
            Transform sourceVisualRoot = source != null ? source.transform.Find("CatVisualRoot") : null;
            stream.SendNext(sourceVisualRoot != null ? sourceVisualRoot.localPosition : Vector3.zero);

            CatCompanionDirector director = source != null ? source.GetComponent<CatCompanionDirector>() : null;
            int companionCount = StoryCoopRuntimeBridge.IsHost && director != null && director.CompanionsActive
                ? Mathf.Min(7, director.Companions.Count) : 0;
            stream.SendNext(companionCount);
            for (int i = 0; i < companionCount; i++)
            {
                CatCompanionAlly companion = director.Companions[i];
                stream.SendNext(companion != null ? companion.transform.position : source.transform.position);
                stream.SendNext(companion != null ? companion.transform.rotation : source.transform.rotation);
            }

            Transform weaponRoot = source != null ? source.transform.Find("FloatingCatWeapons") : null;
            int weaponCount = weaponRoot != null && weaponRoot.gameObject.activeInHierarchy ? Mathf.Min(8, weaponRoot.childCount) : 0;
            stream.SendNext(weaponCount);
            for (int i = 0; i < weaponCount; i++)
            {
                Transform socket = weaponRoot.GetChild(i);
                stream.SendNext(Vector3.ClampMagnitude(socket.localPosition, 2f));
                stream.SendNext(socket.localRotation);
            }
        }
        else
        {
            PlayerClass incomingClass = (PlayerClass)Mathf.Clamp((int)stream.ReceiveNext(), (int)PlayerClass.Basic, (int)PlayerClass.Gun);
            if (classRuntime == null || classRuntime.SelectedClass != incomingClass)
            {
                receivedPlayerClass = incomingClass;
                classRuntime = PlayerClassRuntime.Ensure(gameObject, incomingClass);
                animationDriver = GetComponent<CatAnimationStateDriver>();
                ConfigureRemoteVisual();
                appliedState = string.Empty;
            }
            receivedState = (string)stream.ReceiveNext();
            receivedAttackVariant = (int)stream.ReceiveNext();
            receivedWeaponVariant = (int)stream.ReceiveNext();
            receivedDash = (bool)stream.ReceiveNext();
            receivedAim = (bool)stream.ReceiveNext();
            receivedHeldKey = (string)stream.ReceiveNext();
            receivedHeldPosition = (Vector3)stream.ReceiveNext();
            receivedHeldRotation = (Quaternion)stream.ReceiveNext();
            receivedHeldScale = (Vector3)stream.ReceiveNext();
            bool hadPawnPose = receivedPawnPose;
            receivedPawnPosition = (Vector3)stream.ReceiveNext();
            receivedPawnRotation = (Quaternion)stream.ReceiveNext();
            receivedPawnScale = (Vector3)stream.ReceiveNext();
            receivedVisualRootLocalPosition = (Vector3)stream.ReceiveNext();
            receivedPawnPose = true;
            // A proxy can be instantiated before the owner's post-load ground
            // alignment has settled.  Interpolating a scene-transition-sized
            // correction leaves the remote avatar visibly above/below the
            // owner for several snapshots.  First pose and discontinuities are
            // authoritative teleports; ordinary gameplay deltas remain smooth.
            if (!hadPawnPose || Vector3.Distance(transform.position, receivedPawnPosition) > 0.4f)
                transform.SetPositionAndRotation(receivedPawnPosition, receivedPawnRotation);
            receivedCompanionCount = Mathf.Clamp((int)stream.ReceiveNext(), 0, 7);
            for (int i = 0; i < receivedCompanionCount; i++)
            {
                receivedCompanionPositions[i] = (Vector3)stream.ReceiveNext();
                receivedCompanionRotations[i] = (Quaternion)stream.ReceiveNext();
            }
            receivedWeaponCount = Mathf.Clamp((int)stream.ReceiveNext(), 0, 8);
            for (int i = 0; i < receivedWeaponCount; i++)
            {
                receivedWeaponLocalPositions[i] = Vector3.ClampMagnitude((Vector3)stream.ReceiveNext(), 2f);
                receivedWeaponLocalRotations[i] = (Quaternion)stream.ReceiveNext();
            }
        }
    }

    private void ApplyRemoteCompanions()
    {
        while (remoteCompanions.Count < receivedCompanionCount)
        {
            GameObject proxy = new GameObject("NetworkCompanionVisual_" + (remoteCompanions.Count + 1));
            CatCompanionDirector.CloneOwnerVisual(transform, proxy.transform, Color.white);
            CoopNetworkSyncManager.MakeVisualOnly(proxy);
            remoteCompanions.Add(proxy);
        }
        for (int i = 0; i < remoteCompanions.Count; i++)
        {
            GameObject proxy = remoteCompanions[i];
            bool visible = i < receivedCompanionCount;
            if (proxy == null) continue;
            proxy.SetActive(visible);
            if (!visible) continue;
            proxy.transform.SetPositionAndRotation(
                Vector3.Lerp(proxy.transform.position, receivedCompanionPositions[i], 1f - Mathf.Exp(-18f * Time.deltaTime)),
                Quaternion.Slerp(proxy.transform.rotation, receivedCompanionRotations[i], 1f - Mathf.Exp(-18f * Time.deltaTime)));
        }
    }

    private void ApplyRemoteFloatingWeapons()
    {
        Transform root = transform.Find("FloatingCatWeapons");
        if (root == null) return;
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;
        root.gameObject.SetActive(receivedWeaponCount > 0);
        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform socket = root.GetChild(i);
            bool visible = i < receivedWeaponCount;
            socket.gameObject.SetActive(visible);
            if (!visible) continue;
            socket.localPosition = Vector3.Lerp(socket.localPosition, receivedWeaponLocalPositions[i], 1f - Mathf.Exp(-20f * Time.deltaTime));
            socket.localRotation = Quaternion.Slerp(socket.localRotation, receivedWeaponLocalRotations[i], 1f - Mathf.Exp(-20f * Time.deltaTime));
        }
    }

    private void SetRenderers(bool visible)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = visible;
    }

    private void SetProxyAnimatorsEnabled(bool visible)
    {
        Animator[] proxyAnimators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < proxyAnimators.Length; i++)
            if (proxyAnimators[i] != null) proxyAnimators[i].enabled = visible;
    }

    private void DisableOwnerProxyVisualHierarchy()
    {
        string[] roots = { "CatVisualRoot", "MeleeClassVisual", "GunClassVisual", "FirstPersonPaws", "FloatingCatWeapons" };
        for (int i = 0; i < roots.Length; i++)
        {
            Transform root = transform.Find(roots[i]);
            if (root != null && root.gameObject.activeSelf) root.gameObject.SetActive(false);
        }
    }

    private void SubscribeOwnerHeldEvents()
    {
        if (ownerHeldEventsSubscribed || sourceCarry == null) return;
        sourceCarry.HeldWeaponStateChanged += BroadcastReliableHeldState;
        ownerHeldEventsSubscribed = true;
        BroadcastReliableHeldState();
    }

    private void BroadcastReliableHeldState()
    {
        if (photonView == null || !photonView.IsMine) return;
        string key = GetLocalHeldKey();
        Vector3 localPosition = Vector3.zero;
        Quaternion localRotation = Quaternion.identity;
        Vector3 localScale = Vector3.one;
        if (!string.IsNullOrEmpty(key) && sourceCarry != null && sourceCarry.HeldObject != null)
        {
            Transform held = sourceCarry.HeldObject.transform;
            localPosition = held.localPosition;
            localRotation = held.localRotation;
            localScale = held.localScale;
        }
        CoopNetworkSyncManager.BroadcastReliableHeldState(
            photonView.OwnerActorNr, key, localPosition, localRotation, localScale);
    }

    public void ReceiveReliableHeldState(string key, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        if (!IsRemote) return;
        receivedHeldKey = key ?? string.Empty;
        receivedHeldPosition = localPosition;
        receivedHeldRotation = localRotation;
        receivedHeldScale = localScale;
        ApplyRemoteHeldVisual();
    }

    private void ConfigureRemoteVisual()
    {
        bool authoredClassVisualActive = classRuntime != null && (classRuntime.IsMelee || classRuntime.IsGun);
        if (!authoredClassVisualActive && animationDriver != null)
        {
            animationDriver.EnsureRuntimeVisualReady();
            animationDriver.ApplyRuntimeVisualVisibilityNow();
        }

        Transform visualRoot = classRuntime != null && classRuntime.IsMelee
            ? transform.Find("MeleeClassVisual")
            : classRuntime != null && classRuntime.IsGun ? transform.Find("GunClassVisual")
            : transform.Find("CatVisualRoot/VarcoCatModel");
        Transform pawsRoot = transform.Find("FirstPersonPaws");
        Transform floatingWeapons = transform.Find("FloatingCatWeapons");
        renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            bool authoredCatVisual = visualRoot != null && renderer.transform.IsChildOf(visualRoot);
            bool hiddenLocalVisual = pawsRoot != null && renderer.transform.IsChildOf(pawsRoot);
            bool runtimeWeapon = floatingWeapons != null && renderer.transform.IsChildOf(floatingWeapons);
            renderer.enabled = authoredCatVisual && !hiddenLocalVisual && !runtimeWeapon;
        }

        if (!authoredClassVisualActive)
        {
            CatAvatarVisualIntegrity.Repair(gameObject);
            if (animationDriver != null) animationDriver.ApplyRuntimeVisualVisibilityNow();
        }
    }

    private IEnumerator StabilizeRemoteVisualAfterAnimator()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        if (this == null || animationDriver == null || !IsRemote) yield break;
        bool classVisualActive = (receivedPlayerClass == PlayerClass.Melee && transform.Find("MeleeClassVisual") != null)
            || (receivedPlayerClass == PlayerClass.Gun && transform.Find("GunClassVisual") != null);
        if (classVisualActive)
        {
            Transform classRoot = transform.Find(receivedPlayerClass == PlayerClass.Gun ? "GunClassVisual" : "MeleeClassVisual");
            Renderer[] meleeRenderers = classRoot.GetComponentsInChildren<Renderer>(true);
            int enabledRendererCount = 0;
            for (int i = 0; i < meleeRenderers.Length; i++)
            {
                if (meleeRenderers[i] == null) continue;
                meleeRenderers[i].enabled = true;
                enabledRendererCount++;
            }
            // Remote transport proxies disable every collider during Initialize so they
            // cannot participate in gameplay physics.  PlayerClassRuntime aligns the
            // authored class mesh to the body's bounds, however a disabled Collider has
            // an empty bounds value.  Temporarily expose the authored body only while the
            // visual offset is calculated, then restore the visual-only proxy state.
            Collider classBody = GetComponent<Collider>();
            bool classBodyWasEnabled = classBody != null && classBody.enabled;
            if (classBody != null) classBody.enabled = true;
            Physics.SyncTransforms();
            classRuntime?.RealignActiveVisualToBodyFeet();
            if (classBody != null) classBody.enabled = classBodyWasEnabled;
            Physics.SyncTransforms();
            Debug.Log("[ClassSelect] remote_class_visual_stabilized class=" + receivedPlayerClass + " actor=" + OwnerActorNumber
                + " root=" + transform.position.ToString("F3")
                + " renderers=" + enabledRendererCount);
            yield break;
        }
        Collider authoredBody = GetComponent<Collider>();
        bool bodyWasEnabled = authoredBody != null && authoredBody.enabled;
        if (authoredBody != null) authoredBody.enabled = true;
        Physics.SyncTransforms();
        animationDriver.RealignRuntimeVisualToBodyFeet();
        if (authoredBody != null) authoredBody.enabled = bodyWasEnabled;
        Physics.SyncTransforms();
        CatAvatarVisualIntegrity.Repair(gameObject);
        Debug.Log("[MultiFix9A] remote_visual_stabilized actor=" + OwnerActorNumber
            + " root=" + transform.position.ToString("F3")
            + " visualLocal=" + animationDriver.RuntimeVisualLocalPosition.ToString("F3"));
    }

    private void RepairExternalSkinnedBoneReferences(Transform visualRoot)
    {
        if (visualRoot == null) return;
        Transform[] localBones = visualRoot.GetComponentsInChildren<Transform>(true);
        Dictionary<string, Transform> byName = new Dictionary<string, Transform>(StringComparer.Ordinal);
        for (int i = 0; i < localBones.Length; i++)
        {
            if (localBones[i] != null && !byName.ContainsKey(localBones[i].name)) byName.Add(localBones[i].name, localBones[i]);
        }

        SkinnedMeshRenderer[] skins = visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skins.Length; i++)
        {
            SkinnedMeshRenderer skin = skins[i];
            if (skin == null) continue;
            if (skin.rootBone != null && !skin.rootBone.IsChildOf(transform)
                && byName.TryGetValue(skin.rootBone.name, out Transform localRoot)) skin.rootBone = localRoot;
            Transform[] bones = skin.bones;
            bool changed = false;
            for (int b = 0; b < bones.Length; b++)
            {
                Transform bone = bones[b];
                if (bone == null || bone.IsChildOf(transform)) continue;
                if (byName.TryGetValue(bone.name, out Transform localBone))
                {
                    bones[b] = localBone;
                    changed = true;
                }
            }
            if (changed) skin.bones = bones;
        }
    }

    private static bool IsWeaponState(string state)
    {
        return state == "SwordAttack1" || state == "SwordAttack2Reverse";
    }

    private void OnDestroy()
    {
        if (ownerHeldEventsSubscribed && sourceCarry != null)
            sourceCarry.HeldWeaponStateChanged -= BroadcastReliableHeldState;
        ownerHeldEventsSubscribed = false;
        CoopRemotePawnRegistry.Unregister(transform);
        ReleaseRemoteHeldVisual();
        appliedHeldKey = string.Empty;
        for (int i = 0; i < remoteCompanions.Count; i++) if (remoteCompanions[i] != null) Destroy(remoteCompanions[i]);
        remoteCompanions.Clear();
    }
}
