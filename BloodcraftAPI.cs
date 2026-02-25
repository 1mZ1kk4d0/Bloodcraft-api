using Bloodcraft.Interfaces;
using Bloodcraft.Services;
using Bloodcraft.Systems.Expertise;
using Bloodcraft.Systems.Legacies;
using Stunlock.Core;
using static Bloodcraft.Services.DataService.FamiliarPersistence.FamiliarExperienceManager;
using static Bloodcraft.Services.DataService.FamiliarPersistence.FamiliarPrestigeManager;
using static Bloodcraft.Services.DataService.FamiliarPersistence.FamiliarTierManager;
using static Bloodcraft.Services.DataService.FamiliarPersistence.FamiliarUnlocksManager;
using static Bloodcraft.Services.PlayerService;

namespace Bloodcraft.API;

public enum PlayerClass
{
    BloodKnight,
    DemonHunter,
    VampireLord,
    ShadowBlade,
    ArcaneSorcerer,
    DeathMage,
}

public enum QuestType
{
    Daily,
    Weekly,
}

public enum TargetType
{
    Kill,
    Craft,
    Gather,
    Fish,
}

public readonly struct QuestData
{
    public TargetType Goal { get; init; }
    public int TargetPrefabHash { get; init; }
    public int RequiredAmount { get; init; }
    public int Progress { get; init; }
    public bool Complete { get; init; }
    public DateTime LastReset { get; init; }
}

public readonly struct FamiliarData
{
    public int PrefabHash { get; init; }
    public string Name { get; init; }
    public int Level { get; init; }
    public float Xp { get; init; }
    public int Prestige { get; init; }
    public int Tier { get; init; }
}

public static class BloodcraftAPI
{
    public static string GetVersion() => MyPluginInfo.PLUGIN_VERSION;

    public static bool IsLevelingEnabled() => ConfigService.LevelingSystem;

    public static bool IsExpertiseEnabled() => ConfigService.ExpertiseSystem;

    public static bool IsLegacyEnabled() => ConfigService.LegacySystem;

    public static bool IsFamiliarEnabled() => ConfigService.FamiliarSystem;

    public static bool IsProfessionEnabled() => ConfigService.ProfessionSystem;

    public static bool IsPrestigeEnabled() => ConfigService.PrestigeSystem;

    public static bool IsQuestEnabled() => ConfigService.QuestSystem;

    public static bool IsClassEnabled() => ConfigService.ClassSystem;

    public static int GetMaxLevel() => ConfigService.MaxLevel;

    public static int GetMaxExpertiseLevel() => ConfigService.MaxExpertiseLevel;

    public static int GetMaxBloodLevel() => ConfigService.MaxBloodLevel;

    public static int GetMaxFamiliarLevel() => ConfigService.MaxFamiliarLevel;

    public static ulong GetSteamIdByName(string characterName)
    {
        PlayerInfo info = GetPlayerInfo(characterName);
        return info.User.PlatformId;
    }

    public static string GetCharacterName(ulong steamId)
    {
        if (SteamIdPlayerInfoCache.TryGetValue(steamId, out PlayerInfo info))
            return info.User.CharacterName.Value;

        return null;
    }

    public static bool IsPlayerOnline(ulong steamId)
    {
        return SteamIdOnlinePlayerInfoCache.ContainsKey(steamId);
    }

    public static IReadOnlyCollection<ulong> GetAllPlayerSteamIds()
    {
        return (IReadOnlyCollection<ulong>)SteamIdPlayerInfoCache.Keys;
    }

    public static IReadOnlyCollection<ulong> GetOnlinePlayerSteamIds()
    {
        return (IReadOnlyCollection<ulong>)SteamIdOnlinePlayerInfoCache.Keys;
    }

    public static int GetLevel(ulong steamId)
    {
        return steamId.TryGetPlayerExperience(out var data) ? data.Key : 0;
    }

    public static (int Level, float Xp) GetExperience(ulong steamId)
    {
        return steamId.TryGetPlayerExperience(out var data) ? (data.Key, data.Value) : (0, 0f);
    }

    public static void SetExperience(ulong steamId, int level, float xp)
    {
        steamId.SetPlayerExperience(new KeyValuePair<int, float>(level, xp));
    }

    // ─── CLASSES ───────────────────────────────────────────────────────

    public static PlayerClass? GetPlayerClass(ulong steamId)
    {
        if (steamId.TryGetPlayerClass(out var internalClass))
            return (PlayerClass)(int)internalClass;

        return null;
    }

    public static (int FirstUnarmed, int SecondUnarmed, int ClassSpell)? GetPlayerSpells(
        ulong steamId
    )
    {
        if (steamId.TryGetPlayerSpells(out var spells))
            return spells;

        return null;
    }

    public static void SetPlayerClass(ulong steamId, PlayerClass playerClass)
    {
        steamId.SetPlayerClass((Systems.Leveling.ClassManager.PlayerClass)(int)playerClass);
    }

    public static void SetPlayerSpells(
        ulong steamId,
        int firstUnarmed,
        int secondUnarmed,
        int classSpell
    )
    {
        steamId.SetPlayerSpells((firstUnarmed, secondUnarmed, classSpell));
    }

    // ─── WEAPON EXPERTISE ──────────────────────────────────────────────

    public static (int Level, float Xp) GetWeaponExpertise(ulong steamId, WeaponType weaponType)
    {
        var handler = WeaponExpertiseFactory.GetExpertise(weaponType);
        if (handler == null)
            return (0, 0f);

        var data = handler.GetExpertiseData(steamId);
        return (data.Key, data.Value);
    }

    public static Dictionary<WeaponType, (int Level, float Xp)> GetAllWeaponExpertise(ulong steamId)
    {
        var result = new Dictionary<WeaponType, (int Level, float Xp)>();

        foreach (WeaponType weaponType in Enum.GetValues<WeaponType>())
        {
            var handler = WeaponExpertiseFactory.GetExpertise(weaponType);
            if (handler == null)
                continue;

            var data = handler.GetExpertiseData(steamId);
            if (data.Key > 0 || data.Value > 0)
                result[weaponType] = (data.Key, data.Value);
        }

        return result;
    }

    public static void SetWeaponExpertise(ulong steamId, WeaponType weaponType, int level, float xp)
    {
        if (WeaponSystem.SetExtensionMap.TryGetValue(weaponType, out var setter))
            setter(steamId, new KeyValuePair<int, float>(level, xp));
    }

    // ─── BLOOD LEGACIES ────────────────────────────────────────────────

    static readonly BloodType[] _legacyBloodTypes =
    [
        BloodType.Worker,
        BloodType.Warrior,
        BloodType.Scholar,
        BloodType.Rogue,
        BloodType.Mutant,
        BloodType.Draculin,
        BloodType.Immortal,
        BloodType.Creature,
        BloodType.Brute,
        BloodType.Corruption,
    ];

    public static (int Level, float Xp) GetBloodLegacy(ulong steamId, BloodType bloodType)
    {
        var handler = BloodLegacyFactory.GetBloodHandler(bloodType);
        if (handler == null)
            return (0, 0f);

        var data = handler.GetLegacyData(steamId);
        return (data.Key, data.Value);
    }

    public static Dictionary<BloodType, (int Level, float Xp)> GetAllBloodLegacies(ulong steamId)
    {
        var result = new Dictionary<BloodType, (int Level, float Xp)>();

        foreach (BloodType bloodType in _legacyBloodTypes)
        {
            var handler = BloodLegacyFactory.GetBloodHandler(bloodType);
            if (handler == null)
                continue;

            var data = handler.GetLegacyData(steamId);
            if (data.Key > 0 || data.Value > 0)
                result[bloodType] = (data.Key, data.Value);
        }

        return result;
    }

    public static void SetBloodLegacy(ulong steamId, BloodType bloodType, int level, float xp)
    {
        if (BloodSystem.SetExtensions.TryGetValue(bloodType, out var setter))
            setter(steamId, new KeyValuePair<int, float>(level, xp));
    }

    // ─── PROFESSIONS ───────────────────────────────────────────────────

    public static (int Level, float Xp) GetProfession(ulong steamId, Profession profession)
    {
        var handler = ProfessionFactory.GetProfession(profession);
        if (handler == null)
            return (0, 0f);

        var data = handler.GetProfessionData(steamId);
        return (data.Key, data.Value);
    }

    public static Dictionary<Profession, (int Level, float Xp)> GetAllProfessions(ulong steamId)
    {
        var result = new Dictionary<Profession, (int Level, float Xp)>();

        foreach (Profession profession in Enum.GetValues<Profession>())
        {
            if (profession == Profession.None)
                continue;

            var handler = ProfessionFactory.GetProfession(profession);
            if (handler == null)
                continue;

            var data = handler.GetProfessionData(steamId);
            if (data.Key > 0 || data.Value > 0)
                result[profession] = (data.Key, data.Value);
        }

        return result;
    }

    public static void SetProfession(ulong steamId, Profession profession, int level, float xp)
    {
        var handler = ProfessionFactory.GetProfession(profession);
        handler?.SetProfessionData(steamId, new KeyValuePair<int, float>(level, xp));
    }

    // ─── PRESTIGE ──────────────────────────────────────────────────────

    public static int GetPrestigeLevel(ulong steamId, PrestigeType prestigeType)
    {
        if (
            steamId.TryGetPlayerPrestiges(out var prestiges)
            && prestiges.TryGetValue(prestigeType, out int level)
        )
            return level;

        return 0;
    }

    public static Dictionary<PrestigeType, int> GetAllPrestiges(ulong steamId)
    {
        if (steamId.TryGetPlayerPrestiges(out var prestiges))
            return new Dictionary<PrestigeType, int>(prestiges);

        return [];
    }

    public static void SetPrestigeLevel(ulong steamId, PrestigeType prestigeType, int level)
    {
        steamId.TryGetPlayerPrestiges(out var prestiges);
        prestiges ??= [];
        prestiges[prestigeType] = level;
        steamId.SetPlayerPrestiges(prestiges);
    }

    // ─── FAMILIARS ─────────────────────────────────────────────────────

    public static string GetActiveFamiliarBox(ulong steamId)
    {
        return steamId.TryGetFamiliarBox(out string box) ? box : null;
    }

    public static (int Level, float Xp) GetFamiliarExperience(ulong steamId, int familiarPrefabHash)
    {
        var data = LoadFamiliarExperienceData(steamId);
        if (data.FamiliarExperience.TryGetValue(familiarPrefabHash, out var xp))
            return (xp.Key, xp.Value);

        return (0, 0f);
    }

    public static int GetFamiliarPrestige(ulong steamId, int familiarPrefabHash)
    {
        var data = LoadFamiliarPrestigeData(steamId);
        return data.FamiliarPrestige.TryGetValue(familiarPrefabHash, out int level) ? level : 0;
    }

    public static int GetFamiliarTier(ulong steamId, int familiarPrefabHash)
    {
        var data = LoadFamiliarTierData(steamId);
        return data.FamiliarTier.TryGetValue(familiarPrefabHash, out int tier) ? tier : 0;
    }

    public static Dictionary<string, List<int>> GetUnlockedFamiliars(ulong steamId)
    {
        var data = LoadFamiliarUnlocksData(steamId);
        return new Dictionary<string, List<int>>(data.FamiliarUnlocks);
    }

    public static Dictionary<string, List<FamiliarData>> GetBoxes(ulong steamId)
    {
        var unlocksData = LoadFamiliarUnlocksData(steamId);
        var experienceData = LoadFamiliarExperienceData(steamId);
        var prestigeData = LoadFamiliarPrestigeData(steamId);
        var tierData = LoadFamiliarTierData(steamId);

        var result = new Dictionary<string, List<FamiliarData>>();

        foreach (var box in unlocksData.FamiliarUnlocks)
        {
            var familiars = new List<FamiliarData>();

            foreach (int prefabHash in box.Value)
            {
                PrefabGUID prefabGuid = new(prefabHash);

                int level = 0;
                float xp = 0f;

                if (experienceData.FamiliarExperience.TryGetValue(prefabHash, out var expData))
                {
                    level = expData.Key;
                    xp = expData.Value;
                }

                int prestige = prestigeData.FamiliarPrestige.TryGetValue(prefabHash, out int p)
                    ? p
                    : 0;

                int tier = tierData.FamiliarTier.TryGetValue(prefabHash, out int t) ? t : 0;

                familiars.Add(
                    new FamiliarData
                    {
                        PrefabHash = prefabHash,
                        Name = prefabGuid.GetLocalizedName(),
                        Level = level,
                        Xp = xp,
                        Prestige = prestige,
                        Tier = tier,
                    }
                );
            }

            result[box.Key] = familiars;
        }

        return result;
    }

    public static void SetFamiliarExperience(
        ulong steamId,
        int familiarPrefabHash,
        int level,
        float xp
    )
    {
        var data = LoadFamiliarExperienceData(steamId);
        data.FamiliarExperience[familiarPrefabHash] = new KeyValuePair<int, float>(level, xp);
        SaveFamiliarExperienceData(steamId, data);
    }

    public static void SetFamiliarPrestige(ulong steamId, int familiarPrefabHash, int level)
    {
        var data = LoadFamiliarPrestigeData(steamId);
        data.FamiliarPrestige[familiarPrefabHash] = level;
        SaveFamiliarPrestigeData(steamId, data);
    }

    public static void SetFamiliarTier(ulong steamId, int familiarPrefabHash, int tier)
    {
        var data = LoadFamiliarTierData(steamId);
        data.FamiliarTier[familiarPrefabHash] = tier;
        SaveFamiliarTierData(steamId, data);
    }

    // ─── FAMILIAR BOX MANAGEMENT ───────────────────────────────────────

    const int BOX_SIZE = 10;
    const int BOX_CAP = 50;

    public static List<string> GetBoxNames(ulong steamId)
    {
        var data = LoadFamiliarUnlocksData(steamId);
        return [.. data.FamiliarUnlocks.Keys];
    }

    public static void SetActiveFamiliarBox(ulong steamId, string boxName)
    {
        var data = LoadFamiliarUnlocksData(steamId);

        if (data.FamiliarUnlocks.ContainsKey(boxName))
            steamId.SetFamiliarBox(boxName);
    }

    public static bool AddBox(ulong steamId, string boxName)
    {
        var data = LoadFamiliarUnlocksData(steamId);

        if (data.FamiliarUnlocks.Count == 0 || data.FamiliarUnlocks.Count >= BOX_CAP)
            return false;

        if (data.FamiliarUnlocks.ContainsKey(boxName))
            return false;

        data.FamiliarUnlocks.Add(boxName, []);
        SaveFamiliarUnlocksData(steamId, data);
        return true;
    }

    public static bool DeleteBox(ulong steamId, string boxName)
    {
        var data = LoadFamiliarUnlocksData(steamId);

        if (
            !data.FamiliarUnlocks.TryGetValue(boxName, out var familiarSet)
            || familiarSet.Count != 0
        )
            return false;

        data.FamiliarUnlocks.Remove(boxName);
        SaveFamiliarUnlocksData(steamId, data);
        return true;
    }

    public static bool RenameBox(ulong steamId, string currentName, string newName)
    {
        var data = LoadFamiliarUnlocksData(steamId);

        if (data.FamiliarUnlocks.ContainsKey(newName))
            return false;

        if (!data.FamiliarUnlocks.TryGetValue(currentName, out var familiarBox))
            return false;

        data.FamiliarUnlocks.Remove(currentName);
        data.FamiliarUnlocks[newName] = familiarBox;

        if (steamId.TryGetFamiliarBox(out var activeBox) && activeBox.Equals(currentName))
            steamId.SetFamiliarBox(newName);

        SaveFamiliarUnlocksData(steamId, data);
        return true;
    }

    public static bool MoveFamiliarToBox(
        ulong steamId,
        int familiarPrefabHash,
        string destinationBox
    )
    {
        var data = LoadFamiliarUnlocksData(steamId);

        if (
            !data.FamiliarUnlocks.TryGetValue(destinationBox, out var destSet)
            || destSet.Count >= BOX_SIZE
        )
            return false;

        foreach (var box in data.FamiliarUnlocks)
        {
            if (box.Value.Contains(familiarPrefabHash))
            {
                box.Value.Remove(familiarPrefabHash);
                destSet.Add(familiarPrefabHash);
                SaveFamiliarUnlocksData(steamId, data);
                return true;
            }
        }

        return false;
    }

    public static bool RemoveFamiliarToOverflow(
        ulong steamId,
        string boxName,
        int familiarPrefabHash
    )
    {
        var data = LoadFamiliarUnlocksData(steamId);

        if (!data.FamiliarUnlocks.TryGetValue(boxName, out var familiarSet))
            return false;

        if (!familiarSet.Contains(familiarPrefabHash))
            return false;

        familiarSet.Remove(familiarPrefabHash);
        data.OverflowFamiliars.Add(familiarPrefabHash);
        SaveFamiliarUnlocksData(steamId, data);
        return true;
    }

    public static bool RemoveFamiliarFromBox(ulong steamId, string boxName, int familiarPrefabHash)
    {
        var data = LoadFamiliarUnlocksData(steamId);

        if (!data.FamiliarUnlocks.TryGetValue(boxName, out var familiarSet))
            return false;

        if (!familiarSet.Remove(familiarPrefabHash))
            return false;

        SaveFamiliarUnlocksData(steamId, data);
        return true;
    }

    public static bool AddFamiliarToBox(ulong steamId, int familiarPrefabHash, string boxName)
    {
        var data = LoadFamiliarUnlocksData(steamId);

        if (
            !data.FamiliarUnlocks.TryGetValue(boxName, out var familiarSet)
            || familiarSet.Count >= BOX_SIZE
        )
            return false;

        if (familiarSet.Contains(familiarPrefabHash))
            return false;

        familiarSet.Add(familiarPrefabHash);
        SaveFamiliarUnlocksData(steamId, data);
        return true;
    }

    // ─── FAMILIAR OVERFLOW ─────────────────────────────────────────────

    public static List<FamiliarData> GetOverflow(ulong steamId)
    {
        var unlocksData = LoadFamiliarUnlocksData(steamId);
        var experienceData = LoadFamiliarExperienceData(steamId);
        var prestigeData = LoadFamiliarPrestigeData(steamId);

        var result = new List<FamiliarData>();

        foreach (int prefabHash in unlocksData.OverflowFamiliars)
        {
            PrefabGUID prefabGuid = new(prefabHash);

            int level = 0;
            float xp = 0f;

            if (experienceData.FamiliarExperience.TryGetValue(prefabHash, out var expData))
            {
                level = expData.Key;
                xp = expData.Value;
            }

            int prestige = prestigeData.FamiliarPrestige.TryGetValue(prefabHash, out int p) ? p : 0;

            result.Add(
                new FamiliarData
                {
                    PrefabHash = prefabHash,
                    Name = prefabGuid.GetLocalizedName(),
                    Level = level,
                    Xp = xp,
                    Prestige = prestige,
                }
            );
        }

        return result;
    }

    public static bool MoveOverflowToBox(ulong steamId, int familiarPrefabHash, string boxName)
    {
        var data = LoadFamiliarUnlocksData(steamId);

        if (!data.OverflowFamiliars.Contains(familiarPrefabHash))
            return false;

        if (
            !data.FamiliarUnlocks.TryGetValue(boxName, out var familiarSet)
            || familiarSet.Count >= BOX_SIZE
        )
            return false;

        data.OverflowFamiliars.Remove(familiarPrefabHash);
        familiarSet.Add(familiarPrefabHash);
        SaveFamiliarUnlocksData(steamId, data);
        return true;
    }

    // ─── FAMILIAR SEARCH ───────────────────────────────────────────────

    public static Dictionary<string, List<FamiliarData>> SearchFamiliar(ulong steamId, string name)
    {
        var unlocksData = LoadFamiliarUnlocksData(steamId);
        var experienceData = LoadFamiliarExperienceData(steamId);
        var prestigeData = LoadFamiliarPrestigeData(steamId);

        var result = new Dictionary<string, List<FamiliarData>>();

        foreach (var box in unlocksData.FamiliarUnlocks)
        {
            var matches = new List<FamiliarData>();

            foreach (int prefabHash in box.Value)
            {
                PrefabGUID prefabGuid = new(prefabHash);
                string familiarName = prefabGuid.GetLocalizedName();

                if (familiarName.Contains(name, StringComparison.CurrentCultureIgnoreCase))
                {
                    int level = 0;
                    float xp = 0f;

                    if (experienceData.FamiliarExperience.TryGetValue(prefabHash, out var expData))
                    {
                        level = expData.Key;
                        xp = expData.Value;
                    }

                    int prestige = prestigeData.FamiliarPrestige.TryGetValue(prefabHash, out int p)
                        ? p
                        : 0;

                    matches.Add(
                        new FamiliarData
                        {
                            PrefabHash = prefabHash,
                            Name = familiarName,
                            Level = level,
                            Xp = xp,
                            Prestige = prestige,
                        }
                    );
                }
            }

            if (matches.Count > 0)
                result[box.Key] = matches;
        }

        return result;
    }

    // ─── QUESTS ────────────────────────────────────────────────────────

    public static QuestData? GetQuest(ulong steamId, QuestType questType)
    {
        var internalType = (Systems.Quests.QuestSystem.QuestType)(int)questType;

        if (
            steamId.TryGetPlayerQuests(out var quests)
            && quests.TryGetValue(internalType, out var quest)
        )
        {
            return new QuestData
            {
                Goal = (TargetType)(int)quest.Objective.Goal,
                TargetPrefabHash = quest.Objective.Target.GuidHash,
                RequiredAmount = quest.Objective.RequiredAmount,
                Progress = quest.Progress,
                Complete = quest.Objective.Complete,
                LastReset = quest.LastReset,
            };
        }

        return null;
    }

    public static Dictionary<QuestType, QuestData> GetAllQuests(ulong steamId)
    {
        var result = new Dictionary<QuestType, QuestData>();

        if (steamId.TryGetPlayerQuests(out var quests))
        {
            foreach (var kvp in quests)
            {
                var apiType = (QuestType)(int)kvp.Key;
                result[apiType] = new QuestData
                {
                    Goal = (TargetType)(int)kvp.Value.Objective.Goal,
                    TargetPrefabHash = kvp.Value.Objective.Target.GuidHash,
                    RequiredAmount = kvp.Value.Objective.RequiredAmount,
                    Progress = kvp.Value.Progress,
                    Complete = kvp.Value.Objective.Complete,
                    LastReset = kvp.Value.LastReset,
                };
            }
        }

        return result;
    }

    // ─── SHAPESHIFT ────────────────────────────────────────────────────

    public static ShapeshiftType? GetPlayerShapeshift(ulong steamId)
    {
        if (steamId.TryGetPlayerShapeshift(out var shapeshift))
            return shapeshift;

        return null;
    }
}
