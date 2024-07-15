using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace LethalCompanySeichiItems.Uchiwa;

public class UchiwaItem : GrabbableObject
{
    private ManualLogSource _mls;
    private string _uchiwaId;
    
    [Tooltip("The amount of healing it does per swing.")]
    [SerializeField] private int healAmount = 5;
    
    [Header("Audio")][Space(5f)]
    [SerializeField] private AudioSource uchiwaAudio;
    public AudioClip[] hitSfx;
    public AudioClip[] swingSfx;
    
    private PlayerControllerB _previousPlayerHeldBy;
    
    private List<RaycastHit> _objectsHitByUchiwaList = [];
    private RaycastHit[] _objectsHitByUchiwa;
    
    private static readonly int UseHeldItem1 = Animator.StringToHash("UseHeldItem1");
    private const int KnifeMask = 11012424;
    
    private float _timeAtLastDamageDealt;
    
    public override void Start()
    {
        base.Start();
        _uchiwaId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource($"{SeichiItemsPlugin.ModGuid} | Uchiwa {_uchiwaId}");
        healAmount = Mathf.Clamp(UchiwaConfig.Instance.UchiwaHealAmount.Value, 0, int.MaxValue);
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (playerHeldBy == null) return;
        _previousPlayerHeldBy = playerHeldBy;
        if (playerHeldBy.IsOwner) playerHeldBy.playerBodyAnimator.SetTrigger(UseHeldItem1);
        uchiwaAudio.PlayOneShot(swingSfx[Random.Range(0, swingSfx.Length)]);

        if (!IsOwner) return;
        HitUchiwa();
    }

    private void HitUchiwa(bool cancel = false)
    {
        if (_previousPlayerHeldBy == null)
        {
            _mls.LogError("Variable '_previousPlayerHeldBy' is null on this client when HitUchiwa is called.");
        }
        else
        {
            _previousPlayerHeldBy.activatingItem = false;
            bool flag1 = false;
            bool flag2 = false;
            int hitSurfaceID = -1;
            if (!cancel)
            {
                _previousPlayerHeldBy.twoHanded = false;
                _objectsHitByUchiwa = Physics.SphereCastAll(
                    _previousPlayerHeldBy.gameplayCamera.transform.position +
                    _previousPlayerHeldBy.gameplayCamera.transform.right * 0.1f, 0.3f,
                    _previousPlayerHeldBy.gameplayCamera.transform.forward, 0.75f, KnifeMask,
                    QueryTriggerInteraction.Collide);
                _objectsHitByUchiwaList = _objectsHitByUchiwa.OrderBy(x => x.distance).ToList();

                foreach (RaycastHit t in _objectsHitByUchiwaList)
                {
                    RaycastHit objectsHitByUchiwa = t;
                    if (objectsHitByUchiwa.transform.gameObject.layer != 8 &&
                        objectsHitByUchiwa.transform.gameObject.layer != 11)
                    {
                        objectsHitByUchiwa = t;
                        if (objectsHitByUchiwa.transform.TryGetComponent(out IHittable component))
                        {
                            objectsHitByUchiwa = t;
                            if (!(objectsHitByUchiwa.transform == _previousPlayerHeldBy.transform))
                            {
                                objectsHitByUchiwa = t;
                                if (!(objectsHitByUchiwa.point == Vector3.zero))
                                {
                                    Vector3 position = _previousPlayerHeldBy.gameplayCamera.transform.position;
                                    objectsHitByUchiwa = t;
                                    Vector3 point = objectsHitByUchiwa.point;
                                    RaycastHit raycastHit = default;
                                    ref RaycastHit local = ref raycastHit;
                                    int roomMaskAndDefault = StartOfRound.Instance.collidersAndRoomMaskAndDefault;
                                    if (Physics.Linecast(position, point, out local, roomMaskAndDefault))
                                        continue;
                                }

                                flag1 = true;
                                Vector3 forward = _previousPlayerHeldBy.gameplayCamera.transform.forward;
                                try
                                {
                                    if (Time.realtimeSinceStartup - (double)_timeAtLastDamageDealt > 0.4300000071525574)
                                    {
                                        _timeAtLastDamageDealt = Time.realtimeSinceStartup;

                                        if (component is PlayerControllerB player)
                                        {
                                            HealPlayerServerRpc(player.actualClientId);
                                        }
                                        else
                                        {
                                            component.Hit(0, forward, _previousPlayerHeldBy, true, 5);
                                        }
                                    }

                                    flag2 = true;
                                }
                                catch (Exception ex)
                                {
                                    _mls.LogInfo($"Exception caught when hitting object with uchiwa from player #{_previousPlayerHeldBy.playerClientId}: {ex}");
                                }
                            }
                        }

                        continue;
                    }

                    flag1 = true;
                    objectsHitByUchiwa = t;
                    for (int index2 = 0; index2 < StartOfRound.Instance.footstepSurfaces.Length; ++index2)
                    {
                        if (StartOfRound.Instance.footstepSurfaces[index2].surfaceTag != objectsHitByUchiwa.collider.gameObject.tag) continue;
                        hitSurfaceID = index2;
                        break;
                    }
                }
            }

            if (!flag1) return;
            RoundManager.PlayRandomClip(uchiwaAudio, hitSfx);
            FindObjectOfType<RoundManager>().PlayAudibleNoise(transform.position, 17f, 0.8f);
            if (!flag2 && hitSurfaceID != -1)
            {
                uchiwaAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
                WalkieTalkie.TransmitOneShotAudio(uchiwaAudio,
                    StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
            }

            HitUchiwaServerRpc(hitSurfaceID);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void HealPlayerServerRpc(ulong playerId)
    {
        if ((int)playerId >= StartOfRound.Instance.allPlayerScripts.Length) return;
        
        PlayerControllerB player;
        try
        {
            player = StartOfRound.Instance.allPlayerScripts[playerId];
        }
        catch (IndexOutOfRangeException)
        {
            return;
        }
        
        if (player == null)
        {
            return;
        }

        int playerMaxHealth = GetPlayerMaxHealth(player);
        // Debug.Log($"The max health of {player.playerUsername} is {playerMaxHealth}. Their current health is {player.health}");
        
        int playerNewHealth = playerMaxHealth != -1
            ? Mathf.Min(player.health + healAmount, playerMaxHealth)
            : player.health;
        // Debug.Log($"The new health is {player.health}");
        
        HealPlayerClientRpc(playerId, playerNewHealth);
    }

    [ClientRpc]
    private void HealPlayerClientRpc(ulong playerId, int playerNewHealth)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerId];
        player.health = playerNewHealth;
        
        if (HUDManager.Instance.localPlayer == player)
            HUDManager.Instance.UpdateHealthUI(GameNetworkManager.Instance.localPlayerController.health, false);
    }

    [ServerRpc]
    private void HitUchiwaServerRpc(int hitSurfaceID)
    {
        HitUchiwaClientRpc(hitSurfaceID);
    }

    [ClientRpc]
    private void HitUchiwaClientRpc(int hitSurfaceID)
    {
        HitSurfaceWithUchiwa(hitSurfaceID);
    }

    private void HitSurfaceWithUchiwa(int hitSurfaceID)
    {
        // Check if footstepSurfaces is not null
        if (StartOfRound.Instance.footstepSurfaces == null) return;

        // Check if hitSurfaceID is within the valid range of footstepSurfaces array
        if (hitSurfaceID < 0 || hitSurfaceID >= StartOfRound.Instance.footstepSurfaces.Length) return;

        // Check if the element at hitSurfaceID is not null and has a valid hitSurfaceSFX
        FootstepSurface surface = StartOfRound.Instance.footstepSurfaces[hitSurfaceID];
        if (surface == null || surface.hitSurfaceSFX == null) return;

        // Play the sound
        uchiwaAudio.PlayOneShot(surface.hitSurfaceSFX);
        WalkieTalkie.TransmitOneShotAudio(uchiwaAudio, surface.hitSurfaceSFX);
    }

    /// <summary>
    /// Gets the max health of the given player
    /// This is needed because mods may increase the max health of a player
    /// </summary>
    /// <param name="player">The player to get the max health</param>
    /// <returns>The player's max health</returns>
    private static int GetPlayerMaxHealth(PlayerControllerB player)
    {
        if (UchiwaSharedData.Instance.PlayersMaxHealth.ContainsKey(player))
        {
            return UchiwaSharedData.Instance.PlayersMaxHealth[player];
        }

        return -1;
    }
}