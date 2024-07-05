using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanySeichiItems.Lantern;

public class LanternItem : GrabbableObject
{
    [Space(15f)]
    [SerializeField] private Light lanternBulb;
    [SerializeField] private Light lanternBulbGlow;
    
    [SerializeField] private AudioSource lanternAudioSource;
    [SerializeField] private AudioClip[] lanternAudioClips;
    
    [SerializeField] private Material bulbLight;
    [SerializeField] private Material bulbDark;
    
    [SerializeField] private MeshRenderer lanternMeshRenderer;
    
    [SerializeField] private bool changeMaterial = true;
    
    private PlayerControllerB _previousPlayerHeldBy;

    public override void Start()
    {
        base.Start();
        if (lanternMeshRenderer == null) lanternMeshRenderer = GetComponent<MeshRenderer>();
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (lanternAudioClips.Length == 0) return;
        lanternAudioSource.PlayOneShot(lanternAudioClips[Random.Range(0, lanternAudioClips.Length)]);
        RoundManager.Instance.PlayAudibleNoise(transform.position, 7f, 0.4f,
            noiseIsInsideClosedShip: isInElevator && StartOfRound.Instance.hangarDoorsClosed);
    }

    public override void PocketItem()
    {
        if (!IsOwner)
        {
            base.PocketItem();
        }
        else
        {
            if (_previousPlayerHeldBy != null)
            {
                lanternBulb.enabled = false;
                lanternBulbGlow.enabled = false;
                if (isBeingUsed && (_previousPlayerHeldBy.ItemSlots[_previousPlayerHeldBy.currentItemSlot] == null ||
                                    _previousPlayerHeldBy.ItemSlots[_previousPlayerHeldBy.currentItemSlot].itemProperties.itemId != 1 || 
                                    _previousPlayerHeldBy.ItemSlots[_previousPlayerHeldBy.currentItemSlot].itemProperties.itemId != 6))
                {
                    _previousPlayerHeldBy.pocketedFlashlight = this;
                    PocketLanternServerRpc(true);
                }
                else
                {
                    isBeingUsed = false;
                    lanternBulbGlow.enabled = false;
                    SwitchFlashlight(false);
                    PocketLanternServerRpc();
                }
            }

            base.PocketItem();
        }
    }

    [ServerRpc]
    private void PocketLanternServerRpc(bool stillUsingFlashlight = false)
    {
        PocketLanternClientRpc(stillUsingFlashlight);
    }

    [ClientRpc]
    private void PocketLanternClientRpc(bool stillUsingLantern)
    {
        lanternBulb.enabled = false;
        lanternBulbGlow.enabled = false;
        if (stillUsingLantern)
        {
            if (_previousPlayerHeldBy == null) return;
            _previousPlayerHeldBy.pocketedFlashlight = this;
        }
        else
        {
            isBeingUsed = false;
            lanternBulbGlow.enabled = false;
            SwitchFlashlight(false);
        }
    }

    public override void DiscardItem()
    {
        if (_previousPlayerHeldBy != null)
        {
            _previousPlayerHeldBy.helmetLight.enabled = false;
            lanternBulb.enabled = isBeingUsed;
            lanternBulbGlow.enabled = isBeingUsed;
        }

        base.DiscardItem();
    }

    public override void EquipItem()
    {
        _previousPlayerHeldBy = playerHeldBy;
        if (isBeingUsed) SwitchFlashlight(true);
        base.EquipItem();
    }

    private void SwitchFlashlight(bool on)
    {
        isBeingUsed = on;
        if (!IsOwner)
        {
            lanternBulb.enabled = false;
            lanternBulbGlow.enabled = false;
        }
        else
        {
            lanternBulb.enabled = on;
            lanternBulbGlow.enabled = on;
        }
        
        if (!changeMaterial) return;
        
        Material[] sharedMaterials = lanternMeshRenderer.sharedMaterials;
        sharedMaterials[0] = !on ? bulbDark : bulbLight;
        lanternMeshRenderer.sharedMaterials = sharedMaterials;
    }
}