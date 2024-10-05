using GameNetcodeStuff;
using HarmonyLib;
using System.Diagnostics.CodeAnalysis;

namespace LethalCompanySeichiItems.Uchiwa.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(StartOfRound))]
internal class StartOfRoundPatches
{
    /// <summary>
    /// It makes sure the player's health dictionary is cleared when the round ends
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPatch(nameof(StartOfRound.ShipLeave))]
    [HarmonyPostfix]
    private static void ResetData(StartOfRound __instance)
    {
        UchiwaSharedData.FlushDictionaries();
    }
    
    /// <summary>
    /// Gets the max health of all the players
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPatch(nameof(StartOfRound.StartGame))]
    [HarmonyPostfix]
    private static void GetAllPlayersMaxHealth(StartOfRound __instance)
    {
        foreach (PlayerControllerB player in __instance.allPlayerScripts)
        {
            if (UchiwaSharedData.Instance.PlayersMaxHealth.ContainsKey(player))
                UchiwaSharedData.Instance.PlayersMaxHealth.Remove(player);
            
            UchiwaSharedData.Instance.PlayersMaxHealth.Add(player, player.health);
        }
    }
}