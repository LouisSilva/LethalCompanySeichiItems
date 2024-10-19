using BepInEx.Configuration;

namespace LethalCompanySeichiItems.Uchiwa;

public class UchiwaConfig : SyncedInstance<UchiwaConfig>
{
    internal readonly ConfigEntry<int> UchiwaHealAmount;
    
    public UchiwaConfig(ConfigFile cfg)
    {
        InitInstance(this);

        UchiwaHealAmount = cfg.Bind(
            "Uchiwa",
            "Player Heal Amount",
            5,
            "The amount of health a player receives when hit with the Uchiwa."
            );
    }
}