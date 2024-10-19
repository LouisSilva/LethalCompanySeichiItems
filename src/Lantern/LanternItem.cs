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
#pragma warning restore 0649

    private GameObject lanternHelmetLightInstance;
    private Light lanternHelmetLightSource;
    
    private readonly NetworkVariable<bool> _isMainLightOn = new();
    private readonly NetworkVariable<bool> _isHelmetLightOn = new();
    private readonly NetworkVariable<bool> _isOn = new();

    private bool _subscribedToNetworkEvents;
    private bool _hasInstantiated;

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    public override void OnDestroy()
    {
        if (lanternHelmetLightInstance != null) Destroy(lanternHelmetLightInstance);
        base.OnDestroy();
    }

    public override void Start()
    {
        base.Start();
        SeichiItemsPlugin.Log("bob");
        
        SubscribeToNetworkEvents();
        if (_hasInstantiated) return;
        
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) SeichiItemsPlugin.Log("The mesh renderer component on the lantern is null.", LOGPrefix, SeichiItemsPlugin.LogLevel.Error);
        
        if (bulbLightSource == null) SeichiItemsPlugin.Log("The bulbLightSource on the lantern is null.", LOGPrefix, SeichiItemsPlugin.LogLevel.Error);
        if (bulbGlowLightSource == null) SeichiItemsPlugin.Log("The bulbGlowLightSource on the lantern is null.", LOGPrefix, SeichiItemsPlugin.LogLevel.Error);
        if (lanternHelmetLightPrefab == null) SeichiItemsPlugin.Log("The lanternHelmetLightPrefab gameobject on this lantern is null.", LOGPrefix, SeichiItemsPlugin.LogLevel.Error);
        
        lanternHelmetLightInstance = Instantiate(lanternHelmetLightPrefab);
        lanternHelmetLightSource = lanternHelmetLightInstance.GetComponent<Light>();
        if (lanternHelmetLightSource == null)
        {
            lanternHelmetLightSource = lanternHelmetLightInstance.GetComponentInChildren<Light>();
            if (lanternHelmetLightSource == null) SeichiItemsPlugin.Log("The lanternHelmetLightSource light component on this lantern is null.", LOGPrefix, SeichiItemsPlugin.LogLevel.Error);
        }

        lanternHelmetLightSource.gameObject.transform.position = transform.position;
        
        lanternHelmetLightSource.enabled = false;
        bulbLightSource.enabled = false;
        bulbGlowLightSource.enabled = false;

        _hasInstantiated = true;
    }

    public override void LateUpdate()
    {
        if (_isHelmetLightOn.Value)
        {
            lanternHelmetLightInstance.transform.position = playerHeldBy.helmetLight.transform.position;
            lanternHelmetLightInstance.transform.rotation = playerHeldBy.helmetLight.transform.rotation;
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
        
        if (IsOwner)
        {
            _isOn.Value = !_isOn.Value;
            _isMainLightOn.Value = !_isMainLightOn.Value;
            _isHelmetLightOn.Value = !_isHelmetLightOn.Value;
        }
    }

    public override void DiscardItem()
    {
        if (IsOwner)
        {
            SeichiItemsPlugin.ChangeNetworkVar(_isHelmetLightOn, false);
            SeichiItemsPlugin.ChangeNetworkVar(_isMainLightOn, _isOn.Value);
        }
        base.DiscardItem();
    }
    
    public override void EquipItem()
    {
        if (IsOwner)
        {
            SeichiItemsPlugin.ChangeNetworkVar(_isHelmetLightOn, _isOn.Value);
            SeichiItemsPlugin.ChangeNetworkVar(_isMainLightOn, _isOn.Value);
        }
        base.EquipItem();
    }

    public override void PocketItem()
    {
        if (IsOwner)
        {
            SeichiItemsPlugin.ChangeNetworkVar(_isHelmetLightOn, _isOn.Value);
            SeichiItemsPlugin.ChangeNetworkVar(_isMainLightOn, false);
        }
        base.PocketItem();
    }

    private void OnMainLightToggled(bool oldIsTurnedOn, bool newIsTurnedOn)
    {
        SeichiItemsPlugin.Log($"Main light on?: {newIsTurnedOn}", LOGPrefix);
        bulbLightSource.enabled = newIsTurnedOn;
        bulbGlowLightSource.enabled = newIsTurnedOn;

        Material[] sharedMaterials = meshRenderer.sharedMaterials;
        sharedMaterials[0] = newIsTurnedOn ? bulbLightMaterial : bulbDarkMaterial;
        meshRenderer.sharedMaterials = sharedMaterials;
    }

    private void OnHelmetLightToggled(bool oldIsTurnedOn, bool newIsTurnedOn)
    {
        SeichiItemsPlugin.Log($"Helmet light on?: {newIsTurnedOn}", LOGPrefix);
        lanternHelmetLightSource.enabled = newIsTurnedOn;
    }

    private void SubscribeToNetworkEvents()
    {
        if (_subscribedToNetworkEvents) return;
        _isMainLightOn.OnValueChanged += OnMainLightToggled;
        _isHelmetLightOn.OnValueChanged += OnHelmetLightToggled;
        _subscribedToNetworkEvents = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!_subscribedToNetworkEvents) return;
        _isMainLightOn.OnValueChanged -= OnMainLightToggled;
        _isHelmetLightOn.OnValueChanged -= OnHelmetLightToggled;
        _subscribedToNetworkEvents = false;
    }
}