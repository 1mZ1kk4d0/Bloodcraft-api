using Bloodcraft.Services;
using Bloodcraft.Utilities;
using ProjectM.Network;
using Unity.Entities;
using static Bloodcraft.Patches.DeathEventListenerSystemPatch;
using static Bloodcraft.Services.DataService.FamiliarPersistence.FamiliarSoulsManager;
using static Bloodcraft.Utilities.Familiars;
using static Bloodcraft.Utilities.Familiars.ActiveFamiliarManager;

namespace Bloodcraft.Systems.Familiars;
internal static class FamiliarSoulSystem
{
    static EntityManager EntityManager => Core.EntityManager;

    static readonly float _soulDropChance = ConfigService.SoulDropChance;

    public static void OnUpdate(object sender, DeathEventArgs deathEvent)
    {
        ProcessSoulDrop(deathEvent.Source, deathEvent.Target);
    }
    static void ProcessSoulDrop(Entity source, Entity target)
    {
        if (!source.IsPlayer()) return;

        User user = source.GetUser();
        ulong steamId = user.PlatformId;

        if (!steamId.HasActiveFamiliar()) return;

        ActiveFamiliarData activeFamiliar = GetActiveFamiliarData(steamId);
        int activeFamKey = activeFamiliar.FamiliarId;

        int targetFamKey = target.GetGuidHash();

        if (targetFamKey != activeFamKey) return;

        if (Misc.RollForChance(_soulDropChance))
        {
            FamiliarSoulsData soulsData = LoadFamiliarSoulsData(steamId);

            if (!soulsData.FamiliarSouls.ContainsKey(activeFamKey))
            {
                soulsData.FamiliarSouls[activeFamKey] = 0;
            }

            soulsData.FamiliarSouls[activeFamKey]++;
            SaveFamiliarSoulsData(steamId, soulsData);

            string familiarName = new Stunlock.Core.PrefabGUID(activeFamKey).GetLocalizedName();
            int totalSouls = soulsData.FamiliarSouls[activeFamKey];

            LocalizationService.HandleServerReply(EntityManager, user, $"Soul acquired: <color=#FFD700>{familiarName}</color> (<color=white>{totalSouls}</color> total)");
        }
    }
}
