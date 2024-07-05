using BepInEx.Configuration;

namespace LethalCompanySeichiItems.Kanabo;

public class KanaboConfig : SyncedInstance<KanaboConfig>
{
    public readonly ConfigEntry<int> KanaboDamage;
    public readonly ConfigEntry<float> KanaboReelUpTime;
    
    public KanaboConfig(ConfigFile cfg)
    {
        InitInstance(this);

        KanaboDamage = cfg.Bind(
            "Kanabo",
            "Damage",
            2,
            "The amount of damage the Kanabo does per hit."
            );
        
        KanaboReelUpTime = cfg.Bind(
            "Kanabo",
            "Reel Up Time",
            0.7f,
            "The time it takes in seconds for the player to reel up the Kanabo."
        );
    }
}