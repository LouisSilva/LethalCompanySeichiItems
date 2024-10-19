using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using BepInEx;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using LethalCompanySeichiItems.Kanabo;
using LethalCompanySeichiItems.Uchiwa;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanySeichiItems;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("linkoid-DissonanceLagFix-1.0.0", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("mattymatty-AsyncLoggers-1.6.3", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("mattymatty-Matty_Fixes-1.0.21", BepInDependency.DependencyFlags.SoftDependency)]
public class SeichiItemsPlugin : BaseUnityPlugin
{
    public const string ModGuid = $"LCM_SeichiItems|{ModVersion}";
    private const string ModName = "Lethal Company Seichi Items Mod";
    private const string ModVersion = "3.1.4";

    private readonly Harmony _harmony = new(ModGuid);

    private static SeichiItemsPlugin _instance;

    private static readonly ManualLogSource Mls = new($"{ModGuid}");

    public static UchiwaConfig UchiwaConfigInstance { get; internal set; }
    public static KanaboConfig KanaboConfigInstance { get; internal set; }

    private void Awake()
    {
        if (_instance == null) _instance = this;

        _harmony.PatchAll();
        UchiwaConfigInstance = new UchiwaConfig(Config);
        KanaboConfigInstance = new KanaboConfig(Config);

        _harmony.PatchAll();
        _harmony.PatchAll(typeof(SeichiItemsPlugin));

        InitializeNetworkStuff();
    }

    internal enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
    }

    internal static void Log(string msg, string prefix = "", LogLevel logLevel = LogLevel.Debug)
    {
        msg = $"{prefix}|{msg}";
        switch (logLevel)
        {
            case LogLevel.Debug:
                Mls.LogDebug(msg);
                break;
            case LogLevel.Info:
                Mls.LogInfo(msg);
                break;
            case LogLevel.Warning:
                Mls.LogWarning(msg);
                break;
            case LogLevel.Error:
                Mls.LogError(msg);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null); // Added as a formality
        }
    }
    
    internal static void ChangeNetworkVar<T>(NetworkVariable<T> networkVariable, T newValue) where T : IEquatable<T>
    {
        if (!EqualityComparer<T>.Default.Equals(networkVariable.Value, newValue))
        {
            networkVariable.Value = newValue;
        }
    }

    private static void InitializeNetworkStuff()
    {
        IEnumerable<Type> types;
        try
        {
            types = Assembly.GetExecutingAssembly().GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types.Where(t => t != null);
        }

        foreach (Type type in types)
        {
            MethodInfo[] methods =
                type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (MethodInfo method in methods)
            {
                object[] attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (attributes.Length > 0)
                {
                    method.Invoke(null, null);
                }
            }
        }
    }
}

[Serializable]
public class SyncedInstance<T>
{
    internal static CustomMessagingManager MessageManager => NetworkManager.Singleton.CustomMessagingManager;
    internal static bool IsClient => NetworkManager.Singleton.IsClient;
    internal static bool IsHost => NetworkManager.Singleton.IsHost;

    [NonSerialized] protected static int IntSize = 4;

    public static T Default { get; private set; }
    public static T Instance { get; private set; }

    public static bool Synced { get; internal set; }

    protected void InitInstance(T instance)
    {
        Default = instance;
        Instance = instance;

        IntSize = sizeof(int);
    }

    internal static void SyncInstance(byte[] data)
    {
        Instance = DeserializeFromBytes(data);
        Synced = true;
    }

    internal static void RevertSync()
    {
        Instance = Default;
        Synced = false;
    }

    public static byte[] SerializeToBytes(T val)
    {
        BinaryFormatter bf = new();
        using MemoryStream stream = new();

        try
        {
            bf.Serialize(stream, val);
            return stream.ToArray();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error serializing instance: {e}");
            return null;
        }
    }

    public static T DeserializeFromBytes(byte[] data)
    {
        BinaryFormatter bf = new();
        using MemoryStream stream = new(data);

        try
        {
            return (T)bf.Deserialize(stream);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error deserializing instance: {e}");
            return default;
        }
    }

    private static void RequestSync()
    {
        if (!IsClient) return;

        using FastBufferWriter stream = new(IntSize, Allocator.Temp);
        MessageManager.SendNamedMessage($"{SeichiItemsPlugin.ModGuid}_OnRequestConfigSync", 0uL, stream);
    }

    private static void OnRequestSync(ulong clientId, FastBufferReader _)
    {
        if (!IsHost) return;

        Debug.Log($"Config sync request received from client: {clientId}");

        byte[] array = SerializeToBytes(Instance);
        int value = array.Length;

        using FastBufferWriter stream = new(value + IntSize, Allocator.Temp);

        try
        {
            stream.WriteValueSafe(in value);
            stream.WriteBytesSafe(array);

            MessageManager.SendNamedMessage($"{SeichiItemsPlugin.ModGuid}_OnReceiveConfigSync", clientId, stream);
        }
        catch (Exception e)
        {
            Debug.Log($"Error occurred syncing config with client: {clientId}\n{e}");
        }
    }

    private static void OnReceiveSync(ulong _, FastBufferReader reader)
    {
        if (!reader.TryBeginRead(IntSize))
        {
            Debug.LogError("Config sync error: Could not begin reading buffer.");
            return;
        }

        reader.ReadValueSafe(out int val);
        if (!reader.TryBeginRead(val))
        {
            Debug.LogError("Config sync error: Host could not sync.");
            return;
        }

        byte[] data = new byte[val];
        reader.ReadBytesSafe(ref data, val);

        SyncInstance(data);

        Debug.Log("Successfully synced config with host.");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
    public static void InitializeLocalPlayer()
    {
        if (IsHost)
        {
            MessageManager.RegisterNamedMessageHandler($"{SeichiItemsPlugin.ModGuid}_OnRequestConfigSync",
                OnRequestSync);
            Synced = true;

            return;
        }

        Synced = false;
        MessageManager.RegisterNamedMessageHandler($"{SeichiItemsPlugin.ModGuid}_OnReceiveConfigSync", OnReceiveSync);
        RequestSync();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameNetworkManager), "StartDisconnect")]
    public static void PlayerLeave()
    {
        RevertSync();
    }
}