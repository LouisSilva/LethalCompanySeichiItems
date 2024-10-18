using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LethalCompanySeichiItems.Lantern;

public class LanternItem : GrabbableObject
{
    private const string LOGPrefix = "Lantern";

#pragma warning disable 0649
    [Header("Lantern Stuff")] [Space(15f)]
    [SerializeField] private Light bulbLightSource;
    [SerializeField] private Light bulbGlowLightSource;
    
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] audioClips;
    
    [SerializeField] private Material bulbLightMaterial;
    [SerializeField] private Material bulbDarkMaterial;
    
    [SerializeField] private MeshRenderer meshRenderer;

    [SerializeField] private GameObject lanternHelmetLightPrefab;
    private Light lanternHelmetLightSource;
#pragma warning restore 0649

    private bool _isTurnedOn;
    private bool _isHelmetLightOn;

    public override void Start()
    {
        base.Start();
        
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) SeichiItemsPlugin.Log("The mesh renderer component on the lantern is null.", LOGPrefix, SeichiItemsPlugin.LogLevel.Error);
        
        if (bulbLightSource == null) SeichiItemsPlugin.Log("The bulbLightSource on the lantern is null.", LOGPrefix, SeichiItemsPlugin.LogLevel.Error);
        if (bulbGlowLightSource == null) SeichiItemsPlugin.Log("The bulbGlowLightSource on the lantern is null.", LOGPrefix, SeichiItemsPlugin.LogLevel.Error);
        if (lanternHelmetLightPrefab == null) SeichiItemsPlugin.Log("The lanternHelmetLightPrefab gameobject on this lantern is null.", LOGPrefix, SeichiItemsPlugin.LogLevel.Error);


        GameObject lanternHelmetLightObjInstance = Instantiate(lanternHelmetLightPrefab);
        lanternHelmetLightSource = lanternHelmetLightObjInstance.GetComponent<Light>();
        if (lanternHelmetLightSource == null)
        {
            lanternHelmetLightSource = lanternHelmetLightObjInstance.GetComponentInChildren<Light>();
            if (lanternHelmetLightSource == null) SeichiItemsPlugin.Log("The lanternHelmetLightSource light component on this lantern is null.", LOGPrefix, SeichiItemsPlugin.LogLevel.Error);
        }

        lanternHelmetLightSource.gameObject.transform.position = transform.position;
        
        lanternHelmetLightSource.enabled = false;
        bulbLightSource.enabled = false;
        bulbGlowLightSource.enabled = false;
    }

    public override void LateUpdate()
    {
        if (_isHelmetLightOn)
        {
            lanternHelmetLightSource.gameObject.transform.position = playerHeldBy.helmetLight.transform.position;
            lanternHelmetLightSource.gameObject.transform.rotation = playerHeldBy.helmetLight.transform.rotation;
        }
        
        base.LateUpdate();
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

    public override void DiscardItem()
    {
        SwitchHelmetLightStateServerRpc(false);
        base.DiscardItem();
    }

    public override void EquipItem()
    {
        SwitchHelmetLightStateServerRpc(true);
        base.EquipItem();
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void SwitchHelmetLightStateServerRpc(bool on)
    {
        if (_isHelmetLightOn == on) return;
        SwitchHelmetLightStateClientRpc(on);
    }

    [ClientRpc]
    private void SwitchHelmetLightStateClientRpc(bool on)
    {
        SeichiItemsPlugin.Log($"Helmet light on?: {on}", LOGPrefix);
        if (_isTurnedOn && on)
        {
            _isHelmetLightOn = true;
            lanternHelmetLightSource.enabled = true;
        }
        else
        {
            _isHelmetLightOn = false;
            lanternHelmetLightSource.enabled = false;
        }
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
        SeichiItemsPlugin.Log($"Turned on?: {on}", LOGPrefix);
        _isTurnedOn = on;
        _isHelmetLightOn = on;
        bulbLightSource.enabled = on;
        bulbGlowLightSource.enabled = on;
        lanternHelmetLightSource.enabled = on;

        Material[] sharedMaterials = meshRenderer.sharedMaterials;
        sharedMaterials[0] = on ? bulbLightMaterial : bulbDarkMaterial;
        meshRenderer.sharedMaterials = sharedMaterials;
    }
}