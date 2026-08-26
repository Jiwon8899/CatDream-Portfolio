using UnityEngine;

public class SaveSystem : MonoBehaviour
{
    private const string CurrencyKey = "CatChaos.Currency";
    private const string UnlockedStageKey = "CatChaos.UnlockedStage";
    private const string HasSaveKey = "CatChaos.HasSaveData";
    private const string IntroWatchedKey = "CatChaos.IntroWatched";
    private const string PendingStageClearStoryKey = "CatChaos.PendingStageClearStory";
    private const string PersistentGrowthLevelKey = "CatChaos.PersistentGrowthLevel";
    private const string UpgradePrefix = "CatChaos.Upgrade.";
    private const string StoryPrefix = "Story.";
    private const string StoryMigrationKey = StoryPrefix + "ClassUpgradeMigrationV1";
    private const string EquippedWeaponPrefix = "CatChaos.EquippedWeapon.";
    private const string SettingsPrefix = "CatChaos.Settings.";
    private const string StageBestScorePrefix = "CatChaos.Stage.BestScore.";
    private const string StageTimePrefix = "CatChaos.SecondDevelopment.Time.";
    private const string HiddenStoryPrefix = "CatChaos.SecondDevelopment.Hidden.";
    private const string DoctorUnlockedKey = "CatChaos.SecondDevelopment.DoctorUnlocked";
    private const string DoctorRewardClaimedKey = "CatChaos.SecondDevelopment.DoctorRewardClaimed";
    private const string ChuruBombQuestAcceptedKey = "CatChaos.SecondDevelopment.ChuruBombQuestAccepted";
    private const string ChuruBombUnlockedKey = "CatChaos.SecondDevelopment.ChuruBombUnlocked";
    private const string ChuruBombCountKey = "CatChaos.SecondDevelopment.ChuruBombCount";
    private const string TrueEndingSeenKey = "CatChaos.SecondDevelopment.TrueEndingSeen";

    public static event System.Action<int> CurrencyChanged;
    private static bool runtimeCurrencyDirty;
    private static string runtimeCurrencyKey;
    private static bool runtimeUpgradeLevelsLoaded;
    private static PlayerClass runtimeUpgradeClass;
    private static string runtimeUpgradeAxis;

    private static string StoryAxis => StoryCoopRuntimeBridge.IsInRoom ? "Multi" : "Single";
    private static string StoryGoldKey => StoryPrefix + "Gold." + StoryAxis;
    private static string StoryHighestStageKey => StoryPrefix + "HighestStage." + StoryAxis;
    private static string StoryUpgradeKey(PlayerClass playerClass, string item)
        => StoryPrefix + "Upgrade." + StoryAxis + "." + playerClass + "." + item;

    public static int Currency
    {
        get { EnsureStoryMigration(); return PlayerPrefs.GetInt(StoryGoldKey, 0); }
        set
        {
            SetCurrencyInternal(value, saveImmediately: true);
        }
    }

    public static void AddRuntimeCurrency(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        EnsureStoryMigration();
        SetCurrencyInternal(PlayerPrefs.GetInt(StoryGoldKey, 0) + amount, saveImmediately: false);
    }

    public static void FlushPendingRuntimeChanges()
    {
        if (!runtimeCurrencyDirty)
        {
            return;
        }

        runtimeCurrencyDirty = false;
        PlayerPrefs.Save();
    }

    private static void SetCurrencyInternal(int value, bool saveImmediately)
    {
        EnsureStoryMigration();
        string key = StoryGoldKey;
        int previous = PlayerPrefs.GetInt(key, 0);
        int next = Mathf.Max(0, value);
        PlayerPrefs.SetInt(key, next);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        if (saveImmediately)
        {
            runtimeCurrencyDirty = false;
            PlayerPrefs.Save();
        }
        else
        {
            runtimeCurrencyDirty = true;
            runtimeCurrencyKey = key;
        }

        if (previous != next)
        {
            CurrencyChanged?.Invoke(next);
        }
    }

    public static int UnlockedStage
    {
        get { EnsureStoryMigration(); return Mathf.Clamp(PlayerPrefs.GetInt(StoryHighestStageKey, 1), 1, SceneLoader.MaxStage); }
        set
        {
            EnsureStoryMigration();
            PlayerPrefs.SetInt(StoryHighestStageKey, Mathf.Clamp(value, 1, SceneLoader.MaxStage));
            PlayerPrefs.SetInt(HasSaveKey, 1);
            PlayerPrefs.Save();
        }
    }

    public static bool HasSaveData => PlayerPrefs.GetInt(HasSaveKey, 0) == 1;

    public static void Commit()
    {
        FlushPendingRuntimeChanges();
        SaveUpgradeLevels();
        PlayerPrefs.SetInt(HasSaveKey, 1);
        PlayerPrefs.Save();
    }

    public static int PersistentGrowthLevel
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(PersistentGrowthLevelKey, 0), 0, 6);
        set
        {
            PlayerPrefs.SetInt(PersistentGrowthLevelKey, Mathf.Clamp(value, 0, 6));
            PlayerPrefs.SetInt(HasSaveKey, 1);
            PlayerPrefs.Save();
        }
    }

    public static bool IntroWatched
    {
        get => PlayerPrefs.GetInt(IntroWatchedKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(IntroWatchedKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static void SetPendingStageClearStory(int clearedStage)
    {
        if (clearedStage < 1 || clearedStage > SceneLoader.MaxStage)
        {
            PlayerPrefs.DeleteKey(PendingStageClearStoryKey);
            PlayerPrefs.Save();
            return;
        }

        PlayerPrefs.SetInt(PendingStageClearStoryKey, clearedStage);
        PlayerPrefs.Save();
    }

    public static bool TryConsumePendingStageClearStory(int nextStage, out int clearedStage)
    {
        clearedStage = PlayerPrefs.GetInt(PendingStageClearStoryKey, 0);
        if (clearedStage < 1 || clearedStage > SceneLoader.MaxStage || nextStage != Mathf.Clamp(clearedStage + 1, 1, SceneLoader.MaxStage))
        {
            clearedStage = 0;
            return false;
        }

        PlayerPrefs.DeleteKey(PendingStageClearStoryKey);
        PlayerPrefs.Save();
        return true;
    }

    public static int GetBestScore(int stage)
    {
        return PlayerPrefs.GetInt(StageBestScorePrefix + Mathf.Clamp(stage, 1, SceneLoader.MaxStage), 0);
    }

    public static void SetBestScore(int stage, int score)
    {
        string key = StageBestScorePrefix + Mathf.Clamp(stage, 1, SceneLoader.MaxStage);
        PlayerPrefs.SetInt(key, Mathf.Max(PlayerPrefs.GetInt(key, 0), Mathf.Max(0, score)));
        PlayerPrefs.SetInt(HasSaveKey, 1);
        PlayerPrefs.Save();
    }

    public static string GetStageTimeJson(int stage)
    {
        return PlayerPrefs.GetString(StageTimePrefix + Mathf.Clamp(stage, 1, SceneLoader.MaxStage), string.Empty);
    }

    public static void SetStageTimeJson(int stage, string json)
    {
        PlayerPrefs.SetString(StageTimePrefix + Mathf.Clamp(stage, 1, SceneLoader.MaxStage), json ?? string.Empty);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        PlayerPrefs.Save();
    }

    public static bool IsHiddenStoryCollected(int stage)
    {
        return PlayerPrefs.GetInt(HiddenStoryPrefix + Mathf.Clamp(stage, 1, SceneLoader.MaxStage), 0) == 1;
    }

    public static void SetHiddenStoryCollected(int stage)
    {
        PlayerPrefs.SetInt(HiddenStoryPrefix + Mathf.Clamp(stage, 1, SceneLoader.MaxStage), 1);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        PlayerPrefs.Save();
    }

    public static int HiddenStoryCollectedCount
    {
        get
        {
            int count = 0;
            for (int stage = 1; stage <= SceneLoader.MaxStage; stage++)
            {
                if (IsHiddenStoryCollected(stage))
                {
                    count++;
                }
            }
            return count;
        }
    }

    public static bool DoctorUnlocked
    {
        get => PlayerPrefs.GetInt(DoctorUnlockedKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(DoctorUnlockedKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool DoctorRewardClaimed
    {
        get => PlayerPrefs.GetInt(DoctorRewardClaimedKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(DoctorRewardClaimedKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool ChuruBombQuestAccepted
    {
        get => PlayerPrefs.GetInt(ChuruBombQuestAcceptedKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(ChuruBombQuestAcceptedKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static int ChuruBombCount
    {
        get => Mathf.Max(0, PlayerPrefs.GetInt(ChuruBombCountKey, 0));
        set
        {
            PlayerPrefs.SetInt(ChuruBombCountKey, Mathf.Max(0, value));
            PlayerPrefs.Save();
        }
    }

    public static bool ChuruBombUnlocked
    {
        get => PlayerPrefs.GetInt(ChuruBombUnlockedKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(ChuruBombUnlockedKey, value ? 1 : 0);
            PlayerPrefs.SetInt(HasSaveKey, 1);
            PlayerPrefs.Save();
        }
    }

    public static bool TrueEndingSeen
    {
        get => PlayerPrefs.GetInt(TrueEndingSeenKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(TrueEndingSeenKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static void LoadUpgradeLevels()
    {
        LoadUpgradeLevelsForClass(PlayerClassSelection.Current, true);
    }

    public static void SaveUpgradeLevels()
    {
        SaveUpgradeLevelsForClass(PlayerClassSelection.Current);
    }

    public static void SaveUpgradeLevelsForClass(PlayerClass playerClass)
    {
        UpgradeDatabase.EnsureClassCombatDefinitions();
        EnsureStoryMigration();
        foreach (UpgradeDefinition upgrade in UpgradeDatabase.Upgrades)
        {
            if (upgrade != null)
            {
                PlayerPrefs.SetInt(StoryUpgradeKey(playerClass, upgrade.id), Mathf.Clamp(upgrade.level, 0, upgrade.maxLevel));
            }
        }

        runtimeUpgradeLevelsLoaded = true;
        runtimeUpgradeClass = playerClass;
        runtimeUpgradeAxis = StoryAxis;
        PlayerPrefs.SetInt(HasSaveKey, 1);
        PlayerPrefs.Save();
    }

    public static void EnsureUpgradeLevelsLoadedForClass(PlayerClass playerClass)
    {
        if (runtimeUpgradeLevelsLoaded && runtimeUpgradeClass == playerClass && runtimeUpgradeAxis == StoryAxis)
        {
            return;
        }

        LoadUpgradeLevelsForClass(playerClass, true);
    }

    public static bool HasUpgradeSaveForClass(PlayerClass playerClass)
    {
        UpgradeDatabase.EnsureClassCombatDefinitions();
        EnsureStoryMigration();
        foreach (UpgradeDefinition upgrade in UpgradeDatabase.Upgrades)
            if (upgrade != null && PlayerPrefs.HasKey(StoryUpgradeKey(playerClass, upgrade.id))) return true;
        return false;
    }

    public static void LoadUpgradeLevelsForClass(PlayerClass playerClass, bool continueSaved)
    {
        UpgradeDatabase.EnsureClassCombatDefinitions();
        EnsureStoryMigration();
        foreach (UpgradeDefinition upgrade in UpgradeDatabase.Upgrades)
        {
            if (upgrade == null) continue;
            int level = continueSaved
                ? PlayerPrefs.GetInt(StoryUpgradeKey(playerClass, upgrade.id), 0)
                : 0;
            upgrade.level = Mathf.Clamp(level, 0, upgrade.maxLevel);
            if (!continueSaved) PlayerPrefs.SetInt(StoryUpgradeKey(playerClass, upgrade.id), 0);
        }
        runtimeUpgradeLevelsLoaded = true;
        runtimeUpgradeClass = playerClass;
        runtimeUpgradeAxis = StoryAxis;
        PlayerPrefs.Save();
    }

    private static void EnsureStoryMigration()
    {
        UpgradeDatabase.EnsureClassCombatDefinitions();
        if (PlayerPrefs.GetInt(StoryMigrationKey, 0) == 1) return;
        CopyIntIfMissing(CurrencyKey, StoryPrefix + "Gold.Single");
        CopyIntIfMissing(UnlockedStageKey, StoryPrefix + "HighestStage.Single");
        foreach (UpgradeDefinition upgrade in UpgradeDatabase.Upgrades)
        {
            if (upgrade == null) continue;
            string oldKey = UpgradePrefix + upgrade.id;
            string newKey = StoryPrefix + "Upgrade.Single.Basic." + upgrade.id;
            CopyIntIfMissing(oldKey, newKey);
        }
        PlayerPrefs.SetInt(StoryMigrationKey, 1);
        PlayerPrefs.Save();
        Debug.Log("[ClassUpgrade] migrated legacy story keys to Single.Basic; legacy keys retained.");
    }

    private static void CopyIntIfMissing(string oldKey, string newKey)
    {
        if (PlayerPrefs.HasKey(oldKey) && !PlayerPrefs.HasKey(newKey))
            PlayerPrefs.SetInt(newKey, PlayerPrefs.GetInt(oldKey));
    }

    public static bool HasEquippedCodexWeapon => !string.IsNullOrEmpty(PlayerPrefs.GetString(EquippedWeaponPrefix + "ObjectId", string.Empty));

    public static bool EquippedCodexWeaponTwoHanded => PlayerPrefs.GetInt(EquippedWeaponPrefix + "TwoHanded", 0) == 1;

    public static CodexWeaponDefinition LoadEquippedCodexWeapon()
    {
        string objectId = PlayerPrefs.GetString(EquippedWeaponPrefix + "ObjectId", string.Empty);
        if (string.IsNullOrEmpty(objectId))
        {
            return null;
        }

        return new CodexWeaponDefinition
        {
            objectId = objectId,
            canonicalType = PlayerPrefs.GetString(EquippedWeaponPrefix + "CanonicalType", objectId),
            displayName = PlayerPrefs.GetString(EquippedWeaponPrefix + "DisplayName", objectId),
            stageIndex = Mathf.Clamp(PlayerPrefs.GetInt(EquippedWeaponPrefix + "StageIndex", 1), 1, SceneLoader.MaxStage),
            size = (BreakableObject.ObjectSize)Mathf.Clamp(PlayerPrefs.GetInt(EquippedWeaponPrefix + "Size", 1), 0, 2),
            damage = PlayerPrefs.GetFloat(EquippedWeaponPrefix + "Damage", 0f),
            range = PlayerPrefs.GetFloat(EquippedWeaponPrefix + "Range", 0f),
            prototype = null
        };
    }

    public static void SaveEquippedCodexWeapon(CodexWeaponDefinition definition, bool twoHanded)
    {
        if (definition == null)
        {
            ClearEquippedCodexWeapon();
            return;
        }

        PlayerPrefs.SetString(EquippedWeaponPrefix + "ObjectId", definition.objectId ?? string.Empty);
        PlayerPrefs.SetString(EquippedWeaponPrefix + "CanonicalType", definition.canonicalType ?? definition.objectId ?? string.Empty);
        PlayerPrefs.SetString(EquippedWeaponPrefix + "DisplayName", definition.displayName ?? definition.objectId ?? string.Empty);
        PlayerPrefs.SetInt(EquippedWeaponPrefix + "StageIndex", Mathf.Clamp(definition.stageIndex, 1, SceneLoader.MaxStage));
        PlayerPrefs.SetInt(EquippedWeaponPrefix + "Size", Mathf.Clamp((int)definition.size, 0, 2));
        PlayerPrefs.SetFloat(EquippedWeaponPrefix + "Damage", definition.damage);
        PlayerPrefs.SetFloat(EquippedWeaponPrefix + "Range", definition.range);
        PlayerPrefs.SetInt(EquippedWeaponPrefix + "TwoHanded", twoHanded ? 1 : 0);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        PlayerPrefs.Save();
    }

    public static void ClearEquippedCodexWeapon()
    {
        PlayerPrefs.DeleteKey(EquippedWeaponPrefix + "ObjectId");
        PlayerPrefs.DeleteKey(EquippedWeaponPrefix + "CanonicalType");
        PlayerPrefs.DeleteKey(EquippedWeaponPrefix + "DisplayName");
        PlayerPrefs.DeleteKey(EquippedWeaponPrefix + "StageIndex");
        PlayerPrefs.DeleteKey(EquippedWeaponPrefix + "Size");
        PlayerPrefs.DeleteKey(EquippedWeaponPrefix + "Damage");
        PlayerPrefs.DeleteKey(EquippedWeaponPrefix + "Range");
        PlayerPrefs.DeleteKey(EquippedWeaponPrefix + "TwoHanded");
        PlayerPrefs.Save();
    }

    public static void ResetProgressOnly()
    {
        PlayerPrefs.DeleteKey(CurrencyKey);
        PlayerPrefs.DeleteKey(UnlockedStageKey);
        PlayerPrefs.DeleteKey(HasSaveKey);
        PlayerPrefs.DeleteKey(PersistentGrowthLevelKey);
        PlayerPrefs.DeleteKey(PendingStageClearStoryKey);
        PlayerPrefs.DeleteKey(DoctorUnlockedKey);
        PlayerPrefs.DeleteKey(DoctorRewardClaimedKey);
        PlayerPrefs.DeleteKey(ChuruBombQuestAcceptedKey);
        PlayerPrefs.DeleteKey(ChuruBombUnlockedKey);
        PlayerPrefs.DeleteKey(ChuruBombCountKey);
        PlayerPrefs.DeleteKey(TrueEndingSeenKey);
        for (int stage = 1; stage <= SceneLoader.MaxStage; stage++)
        {
            PlayerPrefs.DeleteKey(StageBestScorePrefix + stage);
            PlayerPrefs.DeleteKey(StageTimePrefix + stage);
            PlayerPrefs.DeleteKey(HiddenStoryPrefix + stage);
        }
        ClearEquippedCodexWeapon();
        foreach (UpgradeDefinition upgrade in UpgradeDatabase.Upgrades)
        {
            if (upgrade != null)
            {
                PlayerPrefs.DeleteKey(UpgradePrefix + upgrade.id);
                for (int axis = 0; axis < 2; axis++)
                    for (int playerClass = 0; playerClass < 3; playerClass++)
                        PlayerPrefs.DeleteKey(StoryPrefix + "Upgrade." + (axis == 0 ? "Single" : "Multi")
                            + "." + (PlayerClass)playerClass + "." + upgrade.id);
                upgrade.level = 0;
            }
        }

        PlayerPrefs.DeleteKey(StoryPrefix + "Gold.Single");
        PlayerPrefs.DeleteKey(StoryPrefix + "Gold.Multi");
        PlayerPrefs.DeleteKey(StoryPrefix + "HighestStage.Single");
        PlayerPrefs.DeleteKey(StoryPrefix + "HighestStage.Multi");
        PlayerPrefs.DeleteKey(StoryMigrationKey);
        runtimeUpgradeLevelsLoaded = false;
        runtimeUpgradeAxis = null;
        PlayerPrefs.Save();
    }

    public static void ResetSettingsOnly()
    {
        PlayerPrefs.DeleteKey(SettingsPrefix + "MasterVolume");
        PlayerPrefs.DeleteKey(SettingsPrefix + "SfxVolume");
        PlayerPrefs.DeleteKey(SettingsPrefix + "BgmVolume");
        PlayerPrefs.DeleteKey(SettingsPrefix + "MouseSensitivity");
        PlayerPrefs.DeleteKey(SettingsPrefix + "CameraShake");
        PlayerPrefs.DeleteKey(SettingsPrefix + "DefaultAutoPlay");
        PlayerPrefs.DeleteKey(SettingsPrefix + "Fullscreen");
        PlayerPrefs.DeleteKey(SettingsPrefix + GraphicsSettingsRuntime.ScreenModeSetting);
        PlayerPrefs.DeleteKey(SettingsPrefix + GraphicsSettingsRuntime.ScreenWidthSetting);
        PlayerPrefs.DeleteKey(SettingsPrefix + GraphicsSettingsRuntime.ScreenHeightSetting);
        PlayerPrefs.DeleteKey(SettingsPrefix + GraphicsSettingsRuntime.QualitySetting);
        PlayerPrefs.DeleteKey(SettingsPrefix + "RadarMarkers");
        PlayerPrefs.DeleteKey(SettingsPrefix + "RadarWave");
        PlayerPrefs.DeleteKey(SettingsPrefix + "RadarAutoUse");
        PlayerPrefs.DeleteKey(SettingsPrefix + "RadarHud");
        PlayerPrefs.DeleteKey(SettingsPrefix + "FloatingWeaponsEnabled");
        PlayerPrefs.DeleteKey(SettingsPrefix + "CompanionsEnabled");
        PlayerPrefs.DeleteKey(SettingsPrefix + "Language");
        PlayerPrefs.Save();
    }

    public static void ResetAllSaveData()
    {
        ResetProgressOnly();
        ResetSettingsOnly();
        PlayerPrefs.DeleteKey(IntroWatchedKey);
        PlayerPrefs.Save();
    }

    public static float GetFloatSetting(string id, float defaultValue)
    {
        return PlayerPrefs.GetFloat(SettingsPrefix + id, defaultValue);
    }

    public static void SetFloatSetting(string id, float value)
    {
        PlayerPrefs.SetFloat(SettingsPrefix + id, value);
        PlayerPrefs.Save();
    }

    public static bool GetBoolSetting(string id, bool defaultValue)
    {
        return PlayerPrefs.GetInt(SettingsPrefix + id, defaultValue ? 1 : 0) == 1;
    }

    public static void SetBoolSetting(string id, bool value)
    {
        PlayerPrefs.SetInt(SettingsPrefix + id, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static int GetIntSetting(string id, int defaultValue)
    {
        return PlayerPrefs.GetInt(SettingsPrefix + id, defaultValue);
    }

    public static void SetIntSetting(string id, int value, bool saveImmediately = true)
    {
        PlayerPrefs.SetInt(SettingsPrefix + id, value);
        if (saveImmediately) PlayerPrefs.Save();
    }
}
