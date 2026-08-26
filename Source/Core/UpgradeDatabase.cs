using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class UpgradeDefinition
{
	public string id;
	public string displayName;
	public string koreanName;
	public string englishName;
	public string description;
	public string koreanDescription;
	public string englishDescription;
	public int level;
	public int maxLevel;
	public int baseCost;
	public float costMultiplier;
	public float effectValue;

	public int CurrentCost => level >= maxLevel ? int.MaxValue : (int)(baseCost * Math.Pow(costMultiplier, level));
	public bool IsMaxed => level >= maxLevel;
}

public static class UpgradeDatabase
{
	private static readonly List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>
	{
		Make("move_speed", "이동 속도", "Move Speed", 15, 180, 1.45f, 0.45f, "기본 이동 속도가 증가합니다.", "Increases base movement speed."),
		Make("jump_force", "점프력", "Jump Force", 5, 160, 1.42f, 0.45f, "점프 높이와 공중 제어가 좋아집니다.", "Improves jump height and air control."),
		Make("base_damage", "기본 파괴력", "Base Damage", 8, 210, 1.48f, 1.2f, "일반 공격과 스킬 피해가 증가합니다.", "Increases attack and skill damage."),
		Make("body_size", "몸집 성장", "Body Size", 50, 240, 1.28f, 0.08f, "고양이 몸집이 커지고 스킬이 단계적으로 해금됩니다.", "Grows the cat and unlocks skills by level."),
		Make("stability", "균형 감각", "Stability", 5, 160, 1.42f, 0.08f, "충돌 뒤 자세가 더 안정됩니다.", "Improves stability after impacts."),
		Make("dash_power", "대시 파괴력", "Dash Power", 8, 230, 1.48f, 1.7f, "대시 충돌 피해를 강화합니다.", "Improves dash impact damage."),
		Make("dash_cooldown", "대시 회복", "Dash Cooldown", 5, 220, 1.45f, -0.18f, "대시 회복 시간이 줄어듭니다.", "Reduces dash recovery time."),
		Make("dash_recharge", "대쉬 충전", "Dash Recharge", 8, 170, 1.38f, 0.075f, "대쉬 게이지가 100%까지 더 빠르게 차오릅니다.", "Recharges dash stamina faster."),
		Make("dash_distance", "관통 거리", "Dash Distance", 5, 190, 1.45f, 0.36f, "관통 이동 거리가 증가합니다.", "Increases phase dash distance."),
		Make("punch_power", "냥냥펀치", "Paw Punch", 8, 210, 1.48f, 1.3f, "왼쪽 클릭 공격의 파괴력이 증가합니다.", "Increases left-click attack power."),
		Make("punch_range", "펀치 범위", "Punch Range", 5, 170, 1.42f, 0.13f, "일반 공격 판정 범위가 증가합니다.", "Increases normal attack range."),
		Make("punch_cooldown", "펀치 회복", "Punch Cooldown", 5, 170, 1.42f, -0.06f, "일반 공격 쿨타임을 줄입니다.", "Reduces normal attack cooldown."),
		Make("liquid_duration", "액체냥 지속", "Liquid Duration", 6, 220, 1.45f, 0.8f, "액체냥 상태 지속 시간이 증가합니다.", "Increases liquid cat duration."),
		Make("liquid_cooldown", "액체냥 회복", "Liquid Cooldown", 5, 210, 1.45f, -0.45f, "액체냥 재사용 대기시간을 줄입니다.", "Reduces liquid cat cooldown."),
		Make("liquid_speed", "액체냥 이동", "Liquid Speed", 5, 180, 1.42f, 0.05f, "액체냥 상태 이동 속도가 증가합니다.", "Increases liquid cat movement speed."),
		Make("large_break_bonus", "대형 파괴 보너스", "Large Break Bonus", 5, 240, 1.45f, 0.1f, "대형 사물 파괴 보상이 증가합니다.", "Increases large object break rewards."),
		Make("auto_targeting", "자동 목표 탐색", "Auto Targeting", 5, 190, 1.45f, 0.15f, "동료와 자동 행동의 목표 탐색이 좋아집니다.", "Improves ally and auto target selection."),
		Make("auto_avoidance", "자동 회피", "Auto Avoidance", 5, 190, 1.45f, 0.15f, "동료와 자동 행동의 장애물 회피가 좋아집니다.", "Improves ally and auto obstacle avoidance."),
		Make("auto_dash_judgment", "자동 스킬 판단", "Auto Skill Judgment", 5, 190, 1.45f, 0.15f, "동료와 자동 행동의 스킬 사용 판단이 좋아집니다.", "Improves ally and auto skill decisions."),
		Make("radar_range", "레이더 범위", "Radar Range", 5, 180, 1.45f, 1.0f, "Q 레이더의 탐색 반경이 증가합니다.", "Increases Q radar radius."),
		Make("radar_cooldown", "레이더 회복", "Radar Cooldown", 5, 200, 1.45f, -0.55f, "Q 레이더 회복 시간이 줄어듭니다.", "Reduces Q radar cooldown."),
		Make("radar_marker_duration", "레이더 표시 시간", "Radar Marker Duration", 5, 160, 1.42f, 0.4f, "레이더로 표시된 사물이 더 오래 보입니다.", "Keeps radar markers visible longer."),
		Make("radar_new_bonus", "새 발견 보너스", "New Discovery Bonus", 5, 220, 1.45f, 6f, "새 사물 발견 보상이 증가합니다.", "Increases new discovery rewards."),
		Make("radar_speed_boost", "레이더 질주", "Radar Sprint", 5, 210, 1.45f, 0.3f, "레이더 사용 후 잠시 이동 속도가 증가합니다.", "Briefly increases speed after radar use."),
		Make("giant_duration", "거대냥 지속", "Giant Cat Duration", 5, 230, 1.45f, 0.22f, "4번 거대냥 지속 시간이 증가합니다.", "Increases skill 4 giant cat duration."),
		Make("giant_move_speed", "거대냥 질주", "Giant Cat Speed", 5, 220, 1.45f, 0.35f, "4번 거대냥 상태 이동 속도가 증가합니다.", "Increases giant cat movement speed."),
		Make("one_hand_weapon", "고양이 무기추가", "Cat Weapon Add", 8, 260, 1.38f, 1f, "고양이 주변에 떠다니는 사물 무기 개수가 늘어납니다. 최대 8개까지 동시에 출전합니다.", "Adds floating object weapons around the cat. Up to 8 weapons can attack together."),
		Make("two_hand_weapon", "무기 공격력", "Weapon Power", 8, 420, 1.42f, 0.18f, "공중 무기가 때리는 피해량과 약간의 범위가 증가합니다.", "Increases floating weapon damage and slightly improves range."),
		Make("cat_companion", "고양이 동료", "Cat Companion", 4, 360, 1.55f, 1f, "함께 사물을 부수는 동료 고양이가 늘어납니다.", "Adds companion cats that help break objects.")
	};

	static UpgradeDatabase()
	{
		ConfigureUpgrade("one_hand_weapon", "\uACE0\uC591\uC774 \uBB34\uAE30\uCD94\uAC00", "Cat Weapon Add", 8, 260, 1.38f, 1f, "\uACE0\uC591\uC774 \uC8FC\uBCC0\uC5D0 \uB5A0\uB2E4\uB2C8\uB294 \uC0AC\uBB3C \uBB34\uAE30 \uAC1C\uC218\uAC00 \uB298\uC5B4\uB0A9\uB2C8\uB2E4. \uCD5C\uB300 8\uAC1C\uAE4C\uC9C0 \uB3D9\uC2DC \uCD9C\uC804\uD569\uB2C8\uB2E4.", "Adds floating object weapons around the cat. Up to 8 weapons can attack together.");
		ConfigureUpgrade("two_hand_weapon", "\uBB34\uAE30 \uACF5\uACA9\uB825", "Weapon Power", 8, 420, 1.42f, 0.18f, "\uACF5\uC911 \uBB34\uAE30\uAC00 \uB54C\uB9AC\uB294 \uD53C\uD574\uB7C9\uACFC \uC57D\uAC04\uC758 \uBC94\uC704\uAC00 \uC99D\uAC00\uD569\uB2C8\uB2E4.", "Increases floating weapon damage and slightly improves range.");
		ConfigureUpgrade("cat_companion", "\uACE0\uC591\uC774 \uB3D9\uB8CC", "Cat Companion", 7, 360, 1.55f, 1f, "\uD568\uAED8 \uC0AC\uBB3C\uC744 \uBD80\uC218\uB294 \uB3D9\uB8CC \uACE0\uC591\uC774\uAC00 \uB298\uC5B4\uB0A9\uB2C8\uB2E4. \uCD5C\uB300 7\uBA85\uAE4C\uC9C0 \uCD9C\uC804\uD569\uB2C8\uB2E4.", "Adds companion cats that help break objects. Up to 7 companions can join.");
		EnsureUpgrade("skill1_range", "\uB0B4\uB824\uCC0D\uAE30 \uBC94\uC704", "Ground Slam Range", 5, 190, 1.42f, 0.25f, "1\uBC88 \uB0B4\uB824\uCC0D\uAE30 \uC2A4\uD0AC\uC758 \uBC94\uC704\uAC00 \uB113\uC5B4\uC9D1\uB2C8\uB2E4.", "Increases skill 1 ground slam radius.");
		EnsureUpgrade("skill2_distance", "\uAD00\uD1B5 \uAC70\uB9AC", "Phase Dash Distance", 5, 200, 1.42f, 0.42f, "2\uBC88 \uAD00\uD1B5 \uC2A4\uD0AC\uC758 \uC774\uB3D9 \uAC70\uB9AC\uAC00 \uAE38\uC5B4\uC9D1\uB2C8\uB2E4.", "Increases skill 2 phase dash travel distance.");
		EnsureUpgrade("skill3_range", "\uC808\uB2E8 \uBC94\uC704", "Spatial Cut Range", 5, 210, 1.42f, 0.24f, "3\uBC88 \uC808\uB2E8 \uC2A4\uD0AC\uC758 \uBC94\uC704\uAC00 \uB113\uC5B4\uC9D1\uB2C8\uB2E4.", "Increases skill 3 spatial cut radius.");
		EnsureUpgrade("attack_speed", "\uACF5\uACA9 \uC18D\uB3C4", "Attack Speed", 8, 230, 1.42f, -0.018f, "\uC67C\uCABD \uD074\uB9AD \uC77C\uBC18 \uACF5\uACA9\uC744 \uB354 \uBE60\uB974\uAC8C \uD718\uB450\uB974\uAC8C \uD569\uB2C8\uB2E4.", "Reduces normal attack cooldown.");
		EnsureUpgrade("gold_gain", "\uC7AC\uD654 \uD68D\uB4DD\uB7C9", "Currency Gain", 10, 260, 1.45f, 0.08f, "\uC2A4\uD14C\uC774\uC9C0\uC640 \uCF64\uBCF4\uC5D0\uC11C \uD68D\uB4DD\uD558\uB294 \uC7AC\uD654\uAC00 \uC99D\uAC00\uD569\uB2C8\uB2E4.", "Increases currency gained from breaks, combos, and stage rewards.");
		EnsureUpgrade("floating_weapon_speed", "\uBB34\uAE30 \uACF5\uACA9 \uC8FC\uAE30", "Weapon Attack Period", 8, 240, 1.42f, 0.035f, "\uACF5\uC911 \uBB34\uAE30\uAC00 \uB2E4\uC2DC \uACF5\uACA9\uD560 \uC218 \uC788\uB294 \uC8FC\uAE30\uAC00 \uBE68\uB77C\uC9D1\uB2C8\uB2E4.", "Lets floating weapons attack again sooner.");
		EnsureUpgrade("churu_drop_interval", "츄르 보급 주기", "Churu Drop Interval", 10, 180, 1.42f, 2f, "츄르폭탄 보급 간격이 레벨당 2초 줄어듭니다. 최소 10초입니다.", "Reduces Churu-bomb airdrop interval by 2 seconds per level, to a 10-second minimum.");
		EnsureUpgrade("churu_capacity", "츄르 소지 한도", "Churu Capacity", 7, 200, 1.42f, 1f, "츄르폭탄 소지 한도가 레벨당 1개 늘어납니다. 기본 한도는 3개입니다.", "Adds one Churu-bomb inventory slot per level from a base capacity of 3.");
		ConfigureUpgrade("stage_time", "\uB0A8\uC740 \uC2DC\uAC04 \uC99D\uAC00", "Extra Stage Time", 12, 230, 1.42f, 15f, "\uD604\uC7AC \uB0A8\uC740 \uC2DC\uAC04\uACFC \uC2A4\uD14C\uC774\uC9C0 \uC2DC\uC791 \uC2DC\uAC04\uC774 \uB808\uBCA8\uB2F9 15\uCD08\uC529 \uB298\uC5B4\uB0A9\uB2C8\uB2E4. \uCD5C\uB300 3\uBD84\uAE4C\uC9C0 \uCD94\uAC00\uB429\uB2C8\uB2E4.", "Adds 15 seconds to the current and starting stage timer per level, up to 3 minutes.");
	}

	private static void ConfigureUpgrade(
		string id,
		string koreanName,
		string englishName,
		int maxLevel,
		int baseCost,
		float costMultiplier,
		float effectValue,
		string koreanDescription,
		string englishDescription)
	{
		UpgradeDefinition upgrade = upgrades.FirstOrDefault(item => item.id == id);
		if (upgrade == null)
		{
			upgrades.Add(Make(id, koreanName, englishName, maxLevel, baseCost, costMultiplier, effectValue, koreanDescription, englishDescription));
			return;
		}

		upgrade.displayName = koreanName;
		upgrade.koreanName = koreanName;
		upgrade.englishName = englishName;
		upgrade.description = koreanDescription;
		upgrade.koreanDescription = koreanDescription;
		upgrade.englishDescription = englishDescription;
		upgrade.maxLevel = maxLevel;
		upgrade.baseCost = baseCost;
		upgrade.costMultiplier = costMultiplier;
		upgrade.effectValue = effectValue;
	}

	private static void EnsureUpgrade(
		string id,
		string koreanName,
		string englishName,
		int maxLevel,
		int baseCost,
		float costMultiplier,
		float effectValue,
		string koreanDescription,
		string englishDescription)
	{
		if (upgrades.Any(item => item.id == id))
		{
			return;
		}

		upgrades.Add(Make(id, koreanName, englishName, maxLevel, baseCost, costMultiplier, effectValue, koreanDescription, englishDescription));
	}

	public static IReadOnlyList<UpgradeDefinition> Upgrades => upgrades;

	public static void EnsureClassCombatDefinitions()
	{
		ClassAssetCatalog catalog = ClassAssetCatalog.Load();
		if (catalog == null || catalog.classes == null) return;
		foreach (PlayerClassDefinition playerClass in catalog.classes)
		{
			if (playerClass == null || playerClass.combatUpgrades == null) continue;
			foreach (ClassCombatUpgradeDefinition row in playerClass.combatUpgrades)
			{
				if (row == null || string.IsNullOrEmpty(row.id)) continue;
				UpgradeDefinition existing = Get(row.id);
				if (existing == null)
				{
					existing = Make(row.id, row.displayName, row.displayName, row.maxLevel,
						row.baseCost, row.costMultiplier, row.effectPerLevel, row.description, row.description);
					upgrades.Add(existing);
				}
				else
				{
					existing.displayName = existing.koreanName = existing.englishName = row.displayName;
					existing.description = existing.koreanDescription = existing.englishDescription = row.description;
					existing.maxLevel = row.maxLevel;
					existing.baseCost = row.baseCost;
					existing.costMultiplier = row.costMultiplier;
					existing.effectValue = row.effectPerLevel;
				}
			}
		}
	}

	public static UpgradeDefinition Get(string id)
	{
		return upgrades.FirstOrDefault(item => item.id == id);
	}

	public static int GetLevel(string id)
	{
		UpgradeDefinition upgrade = Get(id);
		return upgrade != null ? upgrade.level : 0;
	}

	public static float GetEffect(string id)
	{
		UpgradeDefinition upgrade = Get(id);
		return upgrade != null ? upgrade.level * upgrade.effectValue : 0f;
	}

	public static string GetDisplayName(string id)
	{
		UpgradeDefinition upgrade = Get(id);
		if (upgrade == null)
		{
			return id;
		}

		if (LocalizationManager.CurrentLanguage == GameLanguage.English)
		{
			return string.IsNullOrEmpty(upgrade.englishName) ? upgrade.displayName : upgrade.englishName;
		}

		return string.IsNullOrEmpty(upgrade.koreanName) ? upgrade.displayName : upgrade.koreanName;
	}

	public static string GetDescription(string id)
	{
		UpgradeDefinition upgrade = Get(id);
		if (upgrade == null)
		{
			return string.Empty;
		}

		if (LocalizationManager.CurrentLanguage == GameLanguage.English)
		{
			return string.IsNullOrEmpty(upgrade.englishDescription) ? upgrade.description : upgrade.englishDescription;
		}

		return string.IsNullOrEmpty(upgrade.koreanDescription) ? upgrade.description : upgrade.koreanDescription;
	}

	public static bool TryPurchase(string id, ref int currency)
	{
		UpgradeDefinition upgrade = Get(id);
		if (upgrade == null || upgrade.IsMaxed || currency < upgrade.CurrentCost)
		{
			return false;
		}

		currency -= upgrade.CurrentCost;
		upgrade.level++;
		return true;
	}

	public static UpgradeDefinition RecommendBestUpgrade(int currency, bool hasLiquidStage, bool scoreBehindTarget, int largeObjectCount, bool earlyRound)
	{
		List<string> priority = new List<string>();
		priority.Add("cat_companion");
		if (GetLevel("body_size") < 4)
		{
			priority.Add("body_size");
		}
		priority.Add("stage_time");
		priority.Add("gold_gain");
		priority.Add("punch_power");
		priority.Add("base_damage");
		priority.Add("punch_range");
		priority.Add("dash_recharge");
		priority.Add("move_speed");
		priority.Add("radar_range");
		priority.Add("one_hand_weapon");
		priority.Add("two_hand_weapon");
		priority.Add("floating_weapon_speed");
		if (hasLiquidStage)
		{
			priority.Add("liquid_duration");
			priority.Add("liquid_speed");
			priority.Add("liquid_cooldown");
		}
		if (largeObjectCount >= 6)
		{
			priority.Add("large_break_bonus");
		}
		if (scoreBehindTarget)
		{
			priority.Add("radar_new_bonus");
			priority.Add("radar_cooldown");
		}
		priority.Add("auto_targeting");
		priority.Add("auto_dash_judgment");
		priority.Add("auto_avoidance");

		foreach (string id in priority.Distinct())
		{
			UpgradeDefinition upgrade = Get(id);
			if (upgrade != null && !upgrade.IsMaxed && upgrade.CurrentCost <= currency)
			{
				return upgrade;
			}
		}

		return upgrades.Where(item => !item.IsMaxed && item.CurrentCost <= currency).OrderBy(item => item.CurrentCost).FirstOrDefault();
	}

	private static UpgradeDefinition Make(
		string id,
		string koreanName,
		string englishName,
		int maxLevel,
		int baseCost,
		float costMultiplier,
		float effectValue,
		string koreanDescription,
		string englishDescription)
	{
		return new UpgradeDefinition
		{
			id = id,
			displayName = koreanName,
			koreanName = koreanName,
			englishName = englishName,
			description = koreanDescription,
			koreanDescription = koreanDescription,
			englishDescription = englishDescription,
			maxLevel = maxLevel,
			baseCost = baseCost,
			costMultiplier = costMultiplier,
			effectValue = effectValue
		};
	}
}
