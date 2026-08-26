using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CatPrototype.BuildingBreak
{
    /// <summary>
    /// Owns the ladder: which stage is current, spawning its target, and advancing when the
    /// host says the target died. It holds no networking of its own - the net bridge calls
    /// into it - so the same code drives a solo editor run and a four-player room.
    /// </summary>
    public class BuildingBreakDirector : MonoBehaviour
    {
        public const float DestroyAnimationSeconds = 0.6f;
        public const float NextStageDelaySeconds = 0.4f;
        public const float ResultDelaySeconds = 1.2f;

        /// <summary>Clearance kept around a spawning target before players are pushed out.</summary>
        public const float SpawnClearanceMetres = 1.0f;

        public static readonly Vector3 TargetSpawnPosition = new Vector3(0f, 0f, 20f);

        public static BuildingBreakDirector Instance { get; private set; }

        public BuildingBreakStageList StageList { get; private set; }
        public BuildingBreakTarget CurrentTarget { get; private set; }
        public int CurrentStage { get; private set; }
        public bool IsFinished { get; private set; }
        public bool IsHost { get; set; } = true;

        /// <summary>Highest stage whose transition has already been applied. Guards against
        /// two players landing the killing blow together and advancing the ladder twice.</summary>
        private int lastAppliedTransition;

        public event System.Action<int, BuildingBreakTarget> StageSpawned;
        public event System.Action<int, long> StageDestroyed;
        public event System.Action<long, long> TargetHealthChanged;   // current, max
        public event System.Action AllStagesCleared;

        private readonly List<Transform> trackedPlayers = new List<Transform>();
        private Coroutine transitionRoutine;
        private BuildingBreakBackdropQueue backdropQueue;

        public BuildingBreakBackdropQueue BackdropQueue => backdropQueue;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("[BuildingBreak] a second director exists; the scene should hold exactly one");
            Instance = this;
            StageList = Resources.Load<BuildingBreakStageList>("BuildingBreak/StageList");
            if (StageList == null)
                Debug.LogError("[BuildingBreak] StageList not found at Resources/BuildingBreak/StageList");
            else
            {
                backdropQueue = GetComponent<BuildingBreakBackdropQueue>();
                if (backdropQueue == null) backdropQueue = gameObject.AddComponent<BuildingBreakBackdropQueue>();
                backdropQueue.Initialize(StageList);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RegisterPlayer(Transform t)
        {
            if (t != null && !trackedPlayers.Contains(t)) trackedPlayers.Add(t);
        }

        public void UnregisterPlayer(Transform t) { trackedPlayers.Remove(t); }

        public int StageCount => StageList == null ? 0 : StageList.Count;

        // ------------------------------------------------------------- spawning

        public void BeginAtStage(int stageNumber)
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }
            lastAppliedTransition = stageNumber - 1;
            IsFinished = false;
            if (backdropQueue != null) backdropQueue.PrepareForStage(stageNumber);
            SpawnStage(stageNumber);
        }

        public void SpawnStage(int stageNumber)
        {
            if (StageList == null) return;
            var entry = StageList.Get(stageNumber);
            if (entry == null || entry.prefab == null)
            {
                Debug.LogError("[BuildingBreak] stage " + stageNumber + " has no prefab");
                return;
            }

            ClearExistingTargets();

            GameObject go;
            if (backdropQueue == null || !backdropQueue.TryPromoteInstant(entry, out go))
                go = Instantiate(entry.prefab, TargetSpawnPosition, Quaternion.identity);
            CompleteSpawn(entry, go);
        }

        private void CompleteSpawn(BuildingBreakStage entry, GameObject go)
        {
            if (entry == null || go == null) return;
            int stageNumber = entry.stageNumber;
            go.name = "BB_Target_" + stageNumber.ToString("D3");
            var target = go.GetComponent<BuildingBreakTarget>();
            if (target == null) target = go.AddComponent<BuildingBreakTarget>();
            target.ResetForStage(stageNumber, entry.displayName, entry.health);
            target.SetCollidersEnabled(false);

            // Push anyone standing where the target is about to appear. A stage-55 building
            // spans 60m and would otherwise swallow whoever was near the pad.
            DisplaceOverlappingPlayers(target);

            // Colliders come on only once the target is placed and players are clear, so a
            // swing still in flight cannot register against a half-spawned object.
            if (backdropQueue != null) backdropQueue.EnableLivePhysics(go);
            target.SetCollidersEnabled(true);

            // Nothing was listening for the kill. HandleStageDestroyed was only ever called
            // by the network bridge's own hit paths, so a target punched down to zero simply
            // sat there: the ladder never advanced and the result screen never appeared.
            target.ReachedZero -= OnTargetReachedZero;
            target.ReachedZero += OnTargetReachedZero;

            CurrentTarget = target;
            CurrentStage = stageNumber;

            // Logged every stage: when a run reports "no target", this line is what
            // separates "never asked to spawn" from "spawned somewhere unexpected".
            Debug.Log("[BuildingBreak] spawned stage=" + stageNumber + " name=" + entry.displayName
                + " hp=" + entry.health + " at=" + go.transform.position
                + " directorId=" + GetInstanceID());

            var h = StageSpawned;
            if (h != null) h(stageNumber, target);
            RaiseHealth();
        }

        private void OnTargetReachedZero(BuildingBreakTarget target)
        {
            if (target == null) return;
            // The host owns the transition. A client waits for the host's message, which
            // arrives keyed on the same stage number and is idempotent either way.
            if (!IsHost) return;
            HandleStageDestroyed(target.stageNumber);
        }

        private void ClearExistingTargets()
        {
            // Removing by component rather than by name means a leftover from a previous
            // stage cannot linger on a client under a different instance id.
            var existing = FindObjectsByType<BuildingBreakTarget>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] == null) continue;
                // Backdrop prefabs may already carry a disabled target component. They are
                // renderer-only queue entries, not stale live targets.
                if (!existing[i].isActiveAndEnabled) continue;
                RetireTarget(existing[i].gameObject);
            }
            CurrentTarget = null;
        }

        /// <summary>
        /// Destroy() only takes effect at the end of the frame, so anything counting targets
        /// in the same frame as a stage change would see two. Deactivating first makes the
        /// count correct immediately; the destroy then reclaims it.
        /// </summary>
        private void RetireTarget(GameObject go)
        {
            if (go == null) return;
            if (backdropQueue != null && backdropQueue.TryRetire(go)) return;
            go.SetActive(false);
            go.name = "BB_Retired";
            var stale = go.GetComponent<BuildingBreakTarget>();
            if (stale != null) stale.enabled = false;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        /// <summary>
        /// Live targets in the scene. Retired ones are deactivated the instant they are
        /// replaced, so this is the number the leftover-target check should read.
        /// </summary>
        public static int CountActiveTargets()
        {
            var all = FindObjectsByType<BuildingBreakTarget>(FindObjectsSortMode.None);
            int n = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].isActiveAndEnabled) n++;
            return n;
        }

        internal void DisplaceOverlappingPlayers(BuildingBreakTarget target)
        {
            Bounds b = target.GetWorldBounds();
            b.Expand(SpawnClearanceMetres * 2f);

            for (int i = 0; i < trackedPlayers.Count; i++)
            {
                var t = trackedPlayers[i];
                if (t == null) continue;
                if (!b.Contains(t.position)) continue;

                // Push straight out along the shortest horizontal axis, then re-snap to the
                // ground so the move never leaves anyone hovering or buried.
                Vector3 p = t.position;
                Vector3 c = b.center;
                float dx = p.x < c.x ? b.min.x - p.x : b.max.x - p.x;
                float dz = p.z < c.z ? b.min.z - p.z : b.max.z - p.z;
                Vector3 moved = Mathf.Abs(dx) <= Mathf.Abs(dz)
                    ? new Vector3(p.x + dx, p.y, p.z)
                    : new Vector3(p.x, p.y, p.z + dz);
                float pivotToFeet = 0f;
                Renderer[] renderers = t.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    Renderer renderer = renderers[r];
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                        continue;
                    pivotToFeet = Mathf.Max(pivotToFeet, t.position.y - renderer.bounds.min.y);
                }
                Vector3 grounded = SnapToGround(moved);
                grounded.y += pivotToFeet;
                t.position = grounded;
            }
        }

        public static Vector3 SnapToGround(Vector3 position)
        {
            RaycastHit hit;
            var from = position + Vector3.up * 3f;
            if (Physics.Raycast(from, Vector3.down, out hit, 12f))
                return new Vector3(position.x, hit.point.y + 0.02f, position.z);
            return position;
        }

        // ------------------------------------------------------------- damage

        /// <summary>
        /// Applies damage to the live target and reports what actually landed. Returns 0
        /// while the target is locked, which is what makes hits during the destroy window
        /// pay no gold and leak nothing into the next stage.
        /// </summary>
        public long ApplyDamage(long amount)
        {
            if (IsFinished || CurrentTarget == null || !CurrentTarget.IsAlive) return 0;

            long applied = CurrentTarget.ApplyDamage(amount);
            if (applied > 0) RaiseHealth();
            return applied;
        }

        public void ApplyAuthoritativeHealth(long health)
        {
            if (CurrentTarget == null) return;
            CurrentTarget.ApplyAuthoritativeHealth(health);
            RaiseHealth();
        }

        private void RaiseHealth()
        {
            var h = TargetHealthChanged;
            if (h != null && CurrentTarget != null)
                h(CurrentTarget.CurrentHealth, CurrentTarget.maxHealth);
        }

        // ------------------------------------------------------------- transition

        /// <summary>
        /// Advances past <paramref name="destroyedStage"/>. Idempotent by stage number: a
        /// duplicate message for a stage already handled is dropped rather than skipping
        /// the next one.
        /// </summary>
        public void HandleStageDestroyed(int destroyedStage)
        {
            if (destroyedStage <= lastAppliedTransition) return;
            lastAppliedTransition = destroyedStage;

            long hp = CurrentTarget != null ? CurrentTarget.maxHealth
                                            : BuildingBreakStageList.HealthForStage(destroyedStage);
            if (CurrentTarget != null) CurrentTarget.BeginDestruction();

            var h = StageDestroyed;
            if (h != null) h(destroyedStage, hp);

            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(TransitionRoutine(destroyedStage));
        }

        private IEnumerator TransitionRoutine(int destroyedStage)
        {
            yield return new WaitForSeconds(DestroyAnimationSeconds);

            if (CurrentTarget != null)
            {
                RetireTarget(CurrentTarget.gameObject);
                CurrentTarget = null;
            }

            yield return new WaitForSeconds(NextStageDelaySeconds);

            int next = destroyedStage + 1;
            if (next > StageCount)
            {
                IsFinished = true;
                yield return new WaitForSeconds(ResultDelaySeconds);
                var done = AllStagesCleared;
                if (done != null) done();
                yield break;
            }

            var entry = StageList != null ? StageList.Get(next) : null;
            if (entry == null || entry.prefab == null)
            {
                Debug.LogError("[BuildingBreak] stage " + next + " has no prefab");
                yield break;
            }

            GameObject landed = null;
            if (backdropQueue != null)
                yield return backdropQueue.PromoteWithFlight(entry, go => landed = go);
            if (landed == null)
                landed = Instantiate(entry.prefab, TargetSpawnPosition, Quaternion.identity);
            CompleteSpawn(entry, landed);
        }

        public string CurrentDisplayName
            => CurrentTarget != null ? CurrentTarget.displayName : string.Empty;
    }
}
