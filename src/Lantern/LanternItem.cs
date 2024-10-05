using System;
using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace LethalCompanySeichiItems.Lantern;

public class LanternItem : GrabbableObject
{
    private ManualLogSource _mls;
    private string _lanternId;
    
    [Space(15f)]
    [SerializeField] private Light bulbLightSource;
    [SerializeField] private Light bulbGlowLightSource;
    
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] audioClips;
    
    [SerializeField] private Material bulbLightMaterial;
    [SerializeField] private Material bulbDarkMaterial;
    
    [SerializeField] private MeshRenderer meshRenderer;

    [SerializeField] private GameObject lanternHelmetLightObj;
    private Light lanternHelmetLightSource;

    private bool _isTurnedOn;

    public override void Start()
    {
        base.Start();

        _lanternId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource($"{SeichiItemsPlugin.ModGuid} | Lantern {_lanternId}");
        Random.InitState(StartOfRound.Instance.randomMapSeed + _lanternId.GetHashCode());
        
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) _mls.LogError("The mesh renderer component on the lantern is null.");
        
        if (bulbLightSource == null) _mls.LogError("The bulbLightSource on the lantern is null.");
        if (bulbGlowLightSource == null) _mls.LogError("The bulbGlowLightSource on the lantern is null.");
        if (lanternHelmetLightObj == null) _mls.LogError("The lanternHelmetLightObj gameobject on this lantern is null.");

        lanternHelmetLightSource = lanternHelmetLightObj.GetComponent<Light>();
        if (lanternHelmetLightSource == null)
        {
            lanternHelmetLightSource = lanternHelmetLightObj.GetComponentInChildren<Light>();
            if (lanternHelmetLightSource == null) _mls.LogError("The lanternHelmetLightSource light component on this lantern is null.");
        }

        lanternHelmetLightSource.gameObject.transform.position = transform.position;
        
        lanternHelmetLightSource.enabled = false;
        bulbLightSource.enabled = false;
        bulbGlowLightSource.enabled = false;
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        base.ItemActivate(used, buttonDown);
        isBeingUsed = used;
        
        // Play turn on/off noise
        if (audioClips.Length > 0)
        {
            audioSource.PlayOneShot(audioClips[Random.Range(0, audioClips.Length)]);
            RoundManager.Instance.PlayAudibleNoise(transform.position, 6f, 0.3f,
                noiseIsInsideClosedShip: isInElevator && StartOfRound.Instance.hangarDoorsClosed);
        }
        
        if (IsOwner) SwitchLanternStateServerRpc(!_isTurnedOn);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SwitchLanternStateServerRpc(bool on)
    {
        if (_isTurnedOn == on) return;
        SwitchLanternStateClientRpc(on);
    }

    [ClientRpc]
    private void SwitchLanternStateClientRpc(bool on)
    {
        LogDebug($"Turned on?: {on}");
        _isTurnedOn = on;
        bulbLightSource.enabled = on;
        bulbGlowLightSource.enabled = on;
        lanternHelmetLightSource.enabled = on;

        Material[] sharedMaterials = meshRenderer.sharedMaterials;
        sharedMaterials[0] = on ? bulbLightMaterial : bulbDarkMaterial;
        meshRenderer.sharedMaterials = sharedMaterials;

        lanternHelmetLightSource.gameObject.transform.position = on ? playerHeldBy.helmetLight.transform.position : transform.position;
        lanternHelmetLightSource.gameObject.transform.rotation = on ? playerHeldBy.helmetLight.transform.rotation : Quaternion.identity;
    }

    public override void PocketItem()
    {
        base.PocketItem();
        //if (IsOwner) SwitchLanternStateServerRpc(false);
    }
    public override void DiscardItem()
    {
        base.DiscardItem();
        if (IsOwner) SwitchLanternStateServerRpc(false);
    }
    
    private void LogDebug(string msg)
    {
#if DEBUG
        _mls?.LogInfo($"{msg}");
#endif
    }
}