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
    
    [Tooltip("The amount of healing the Uchiwa does per swing.")]
    [SerializeField] private int healAmount = 5;
    
    [Header("Audio")] [Space(5f)]
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
        if (!IsOwner) return;
        
        PlayAudioClipTypeServerRpc(_uchiwaId, AudioClipTypes.Swing);
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
                                }
                                catch (Exception ex)
                                {
                                    _mls.LogError($"Exception when hitting object with uchiwa from player #{_previousPlayerHeldBy.playerClientId}: {ex}");
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
            
            PlayAudioClipTypeServerRpc(_uchiwaId, AudioClipTypes.Hit);
            HitUchiwaServerRpc(hitSurfaceID);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void HealPlayerServerRpc(ulong playerId)
    {
        PlayerControllerB player;
        try
        {
            player = StartOfRound.Instance.allPlayerScripts[playerId];
        }
        catch (IndexOutOfRangeException)
        {
            _mls.LogError($"Tried to heal player with ID: {playerId}, but such player does not exist.");
            return;
        }
        
        if (player == null)
        {
            _mls.LogError($"Tried to heal player with ID: {playerId}, but the player object is null.");
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
            HUDManager.Instance.UpdateHealthUI(player.health, false);
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
        
        if (IsOwner) PlaySurfaceHitAudioClipServerRpc(_uchiwaId, hitSurfaceID);
    }

    /// <summary>
    /// Gets the max health of the given player
    /// This is needed because mods may increase the max health of a player
    /// </summary>
    /// <param name="player">The player to get the max health</param>
    /// <returns>The player's max health</returns>
    private int GetPlayerMaxHealth(PlayerControllerB player)
    {
        if (UchiwaSharedData.Instance.PlayersMaxHealth.ContainsKey(player))
        {
            return UchiwaSharedData.Instance.PlayersMaxHealth[player];
        }

        _mls.LogError($"Could not get the health of player {player.playerUsername}. This should not happen.");

        return -1;
    }

    private enum AudioClipTypes
    {
        Hit,
        Swing,
        HitSurface,
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlaySurfaceHitAudioClipServerRpc(string receivedUchiwaId, int hitSurfaceId)
    {
        if (_uchiwaId != receivedUchiwaId) return;
        
        PlayAudioClipTypeClientRpc(receivedUchiwaId, AudioClipTypes.HitSurface, hitSurfaceId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlayAudioClipTypeServerRpc(string receivedUchiwaId, AudioClipTypes audioClipType,
        bool interrupt = false)
    {
        if (_uchiwaId != receivedUchiwaId) return;
        
        int numberOfAudioClips = audioClipType switch
        {
            AudioClipTypes.Hit => hitSfx.Length,
            AudioClipTypes.Swing => swingSfx.Length,
            _ => -1
        };

        switch (numberOfAudioClips)
        {
            case 0:
                _mls.LogError($"There are no audio clips for audio clip type {audioClipType}.");
                return;
            
            case -1:
                _mls.LogError($"Audio Clip Type was not listed, cannot play audio clip. Number of audio clips: {numberOfAudioClips}.");
                return;

            default:
            {
                int clipIndex = Random.Range(0, numberOfAudioClips);
                PlayAudioClipTypeClientRpc(receivedUchiwaId, audioClipType, clipIndex, interrupt);
                break;
            }
        }
    }
    
    /// <summary>
    /// Plays an audio clip with the given type and index
    /// </summary>
    /// <param name="receivedUchiwaId">The Uchiwa ID.</param>
    /// <param name="audioClipType">The audio clip type to play.</param>
    /// <param name="clipIndex">The index of the clip in their respective AudioClip array to play.</param>
    /// <param name="interrupt">Whether to interrupt any previously playing sound before playing the new audio.</param>
    [ClientRpc]
    private void PlayAudioClipTypeClientRpc(string receivedUchiwaId, AudioClipTypes audioClipType, int clipIndex,
        bool interrupt = false)
    {
        if (_uchiwaId != receivedUchiwaId) return;

        AudioClip audioClipToPlay = audioClipType switch
        {
            AudioClipTypes.Hit => hitSfx[clipIndex],
            AudioClipTypes.Swing => swingSfx[clipIndex],
            AudioClipTypes.HitSurface => StartOfRound.Instance.footstepSurfaces[clipIndex].hitSurfaceSFX,
            _ => null
        };

        if (audioClipToPlay == null)
        {
            _mls.LogError($"Invalid audio clip with type: {audioClipType} and index: {clipIndex}");
            return;
        }
        
        if (interrupt) uchiwaAudio.Stop(true);
        uchiwaAudio.PlayOneShot(audioClipToPlay);
        WalkieTalkie.TransmitOneShotAudio(uchiwaAudio, audioClipToPlay, uchiwaAudio.volume);
    }
}