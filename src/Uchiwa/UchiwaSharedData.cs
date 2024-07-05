using System.Collections.Generic;
using GameNetcodeStuff;

namespace LethalCompanySeichiItems.Uchiwa;

public class UchiwaSharedData
{
    private static UchiwaSharedData _instance;
    public static UchiwaSharedData Instance => _instance ??= new UchiwaSharedData();

    public Dictionary<PlayerControllerB, int> PlayersMaxHealth { get; } = new();

    public static void FlushDictionaries()
    {
        Instance.PlayersMaxHealth.Clear();
    }
}