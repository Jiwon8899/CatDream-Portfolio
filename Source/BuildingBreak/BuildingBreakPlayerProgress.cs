using UnityEngine;

namespace CatPrototype.BuildingBreak
{
    /// <summary>
    /// One player's gold and upgrade levels. Nothing here is shared: every participant has
    /// their own instance and buying a level never touches anyone else's multipliers.
    ///
    /// Saving uses its own key prefix and only ever adds keys, so existing save data keeps
    /// its shape.
    /// </summary>
    public class BuildingBreakPlayerProgress
    {
        public const string KeyPrefix = "BuildingBreak.";

        private const string Axis = "Multi";
        private const string MigrationKey = KeyPrefix + "ClassUpgradeMigrationV1";
        private readonly int[][] classLevels = new int[3][];
        private PlayerClass currentClass;

        public long Gold { get; private set; }
        public long TotalGoldEarned { get; private set; }
        public long TotalHits { get; private set; }
        public int HighestStageReached { get; private set; }
        public const int MaxBombs = 5;
        /// <summary>Session-only consumable. Deliberately absent from Save/Load.</summary>
        public int BombCount { get; private set; }
        public int BombPurchaseCount { get; private set; }

        public event System.Action<BuildingBreakPlayerProgress> Changed;

        public BuildingBreakPlayerProgress()
        {
            for (int i = 0; i < classLevels.Length; i++)
                classLevels[i] = new int[BuildingBreakUpgrades.KindCount];
            currentClass = PlayerClassSelection.Current;
        }

        public PlayerClass CurrentClass => currentClass;
        private int[] Levels => classLevels[(int)currentClass];

        public int GetLevel(BuildingBreakUpgradeKind kind) => Levels[(int)kind];
        public int GetLevel(PlayerClass playerClass, BuildingBreakUpgradeKind kind) => classLevels[(int)playerClass][(int)kind];

        public float AttackMultiplier => BuildingBreakUpgrades.AttackMultiplier(GetLevel(BuildingBreakUpgradeKind.Attack), currentClass);
        public float CooldownScale => BuildingBreakUpgrades.CooldownScale(GetLevel(BuildingBreakUpgradeKind.AttackSpeed), currentClass);
        public float CritChance => BuildingBreakUpgrades.CritChance(GetLevel(BuildingBreakUpgradeKind.CritChance), currentClass);
        public float CritMultiplier => BuildingBreakUpgrades.GetCritMultiplier(currentClass);
        public float GoldMultiplier => BuildingBreakUpgrades.GoldMultiplier(GetLevel(BuildingBreakUpgradeKind.GoldGain), currentClass);
        public float UniqueValue => BuildingBreakUpgrades.UniqueValue(GetLevel(BuildingBreakUpgradeKind.Unique), currentClass);
        public float BasicAttackRadiusScale => currentClass == PlayerClass.Basic ? 1f + UniqueValue : 1f;
        public float MeleeThirdHitScale => currentClass == PlayerClass.Melee ? 1f + UniqueValue : 1f;
        [System.Obsolete("GUNCAT_AMMO_SYSTEM superseded probability-based object conversion.")]
        public float GunSpecialChanceBonus => 0f;
        public float GunReloadScale => currentClass == PlayerClass.Gun ? Mathf.Clamp01(1f - UniqueValue) : 1f;

        public void SwitchClass(PlayerClass playerClass)
        {
            currentClass = playerClass;
            Raise();
        }

        public void AddGold(long amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            TotalGoldEarned += amount;
            Raise();
        }

        public static long BombCost(long stageMaxHealth)
        {
            return System.Math.Max(1L, (long)System.Math.Round(stageMaxHealth * 0.03,
                System.MidpointRounding.AwayFromZero));
        }

        public static long BombDamage(long stageMaxHealth)
        {
            return System.Math.Max(1L, (long)System.Math.Round(stageMaxHealth * 0.05,
                System.MidpointRounding.AwayFromZero));
        }

        public bool TryPurchaseBomb(long stageMaxHealth)
        {
            if (BombCount >= MaxBombs) return false;
            long cost = BombCost(stageMaxHealth);
            if (Gold < cost) return false;
            Gold -= cost;
            BombCount++;
            BombPurchaseCount++;
            Raise();
            return true;
        }

        public bool TryConsumeBomb()
        {
            if (BombCount <= 0) return false;
            BombCount--;
            Raise();
            return true;
        }

        public void RegisterHit() { TotalHits++; }

        public void RegisterStage(int stageNumber)
        {
            if (stageNumber > HighestStageReached)
            {
                HighestStageReached = stageNumber;
                Raise();
            }
        }

        public bool CanAfford(BuildingBreakUpgradeKind kind)
        {
            long cost = BuildingBreakUpgrades.CostForNextLevel(kind, GetLevel(kind), currentClass);
            return cost >= 0 && Gold >= cost;
        }

        public bool IsMaxed(BuildingBreakUpgradeKind kind)
            => GetLevel(kind) >= BuildingBreakUpgrades.MaxLevel(kind, currentClass);

        /// <summary>Buys one level. Returns false and changes nothing when maxed or short.</summary>
        public bool TryPurchase(BuildingBreakUpgradeKind kind)
        {
            if (IsMaxed(kind)) return false;
            long cost = BuildingBreakUpgrades.CostForNextLevel(kind, GetLevel(kind), currentClass);
            if (cost < 0 || Gold < cost) return false;
            Gold -= cost;
            Levels[(int)kind]++;
            Raise();
            return true;
        }

        /// <summary>
        /// Damage for one swing, before the target clamps it to remaining HP.
        /// The 28 base and the 2.5x crit are contract values and are not altered here -
        /// upgrades only ever multiply.
        /// </summary>
        public long ComputeDamage(float baseDamage, bool crit)
        {
            double d = baseDamage * AttackMultiplier;
            if (crit) d *= CritMultiplier;
            long rounded = (long)System.Math.Round(d, System.MidpointRounding.AwayFromZero);
            return rounded < 1 ? 1 : rounded;
        }

        /// <summary>
        /// Gold per landed hit: round(appliedDamage * 0.46) * gold multiplier.
        ///
        /// Set so the gold a full run yields matches what maxing every upgrade costs.
        /// Calibrated on completed 1-55 runs rather than a model: 47.90 minutes at 1x and
        /// 46.52 at 2x with HP growth 1.152 and rate 0.0485, both landing a gold/cost ratio
        /// of 1.020. The current pair is solved from that measurement for a 40 minute ladder.
        /// </summary>
        public const double HitGoldRate = 0.0691;

        public long ComputeHitGold(long appliedDamage)
        {
            if (appliedDamage <= 0) return 0;
            double g = System.Math.Round(appliedDamage * HitGoldRate, System.MidpointRounding.AwayFromZero);
            return (long)System.Math.Round(g * GoldMultiplier, System.MidpointRounding.AwayFromZero);
        }

        /// <summary>Destruction bonus: round(stageHp * 0.05) * gold multiplier.</summary>
        public long ComputeDestroyBonus(long stageHealth)
        {
            double b = System.Math.Round(stageHealth * 0.05, System.MidpointRounding.AwayFromZero);
            return (long)System.Math.Round(b * GoldMultiplier, System.MidpointRounding.AwayFromZero);
        }

        public bool RollCrit() => Random.value < CritChance;

        private void Raise()
        {
            var h = Changed;
            if (h != null) h(this);
        }

        // ---------------------------------------------------------------- save

        public void Save()
        {
            try
            {
                PlayerPrefs.SetString(KeyPrefix + "TotalGold." + Axis, TotalGoldEarned.ToString());
                PlayerPrefs.SetInt(KeyPrefix + "HighestStage." + Axis, HighestStageReached);
                for (int c = 0; c < classLevels.Length; c++)
                    for (int i = 0; i < classLevels[c].Length; i++)
                        PlayerPrefs.SetInt(UpgradeKey((PlayerClass)c, (BuildingBreakUpgradeKind)i), classLevels[c][i]);
                PlayerPrefs.Save();
            }
            catch (System.Exception ex)
            {
                // A failed save must never stall the session; it is reported and dropped.
                Debug.LogWarning("[BuildingBreak] save failed: " + ex.Message);
            }
        }

        public void Load()
        {
            try
            {
                MigrateLegacyOnce();
                // Building-break currency is round-scoped party money.  Saved values from
                // older builds are deliberately ignored so every new session starts at 0.
                Gold = 0;
                TotalGoldEarned = ParseLong(PlayerPrefs.GetString(KeyPrefix + "TotalGold." + Axis, "0"));
                HighestStageReached = PlayerPrefs.GetInt(KeyPrefix + "HighestStage." + Axis, 0);
                for (int c = 0; c < classLevels.Length; c++)
                    for (int i = 0; i < classLevels[c].Length; i++)
                        classLevels[c][i] = PlayerPrefs.GetInt(UpgradeKey((PlayerClass)c, (BuildingBreakUpgradeKind)i), 0);
                Raise();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BuildingBreak] load failed: " + ex.Message);
            }
        }

        private static long ParseLong(string s)
        {
            long v;
            return long.TryParse(s, out v) ? v : 0L;
        }

        private static string UpgradeKey(PlayerClass playerClass, BuildingBreakUpgradeKind kind)
            => KeyPrefix + "Upgrade." + Axis + "." + playerClass + "." + kind;

        private static void MigrateLegacyOnce()
        {
            if (PlayerPrefs.GetInt(MigrationKey, 0) == 1) return;
            CopyStringIfMissing(KeyPrefix + "Gold", KeyPrefix + "Gold." + Axis);
            CopyStringIfMissing(KeyPrefix + "TotalGold", KeyPrefix + "TotalGold." + Axis);
            CopyIntIfMissing(KeyPrefix + "HighestStage", KeyPrefix + "HighestStage." + Axis);
            for (int i = 0; i < BuildingBreakUpgrades.CommonKindCount; i++)
                CopyIntIfMissing(KeyPrefix + "Level" + i, UpgradeKey(PlayerClass.Basic, (BuildingBreakUpgradeKind)i));
            PlayerPrefs.SetInt(MigrationKey, 1);
            PlayerPrefs.Save();
            Debug.Log("[ClassUpgrade] migrated legacy BuildingBreak keys to Multi.Basic; legacy keys retained.");
        }

        private static void CopyStringIfMissing(string oldKey, string newKey)
        {
            if (PlayerPrefs.HasKey(oldKey) && !PlayerPrefs.HasKey(newKey))
                PlayerPrefs.SetString(newKey, PlayerPrefs.GetString(oldKey));
        }

        private static void CopyIntIfMissing(string oldKey, string newKey)
        {
            if (PlayerPrefs.HasKey(oldKey) && !PlayerPrefs.HasKey(newKey))
                PlayerPrefs.SetInt(newKey, PlayerPrefs.GetInt(oldKey));
        }

        /// <summary>Test/harness hook: sets a level directly without spending gold.</summary>
        public void ForceLevel(BuildingBreakUpgradeKind kind, int level)
        {
            Levels[(int)kind] = Mathf.Clamp(level, 0, BuildingBreakUpgrades.MaxLevel(kind, currentClass));
            Raise();
        }

        public void ForceGold(long gold) { Gold = gold; Raise(); }
    }
}
