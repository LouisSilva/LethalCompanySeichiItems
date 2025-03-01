using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LethalCompanySeichiItems.Kanabo;

public class KanaboItem : GrabbableObject
{
    private const string LOGPrefix = "Kanabo";
    
    [Tooltip("The amount of damage the Kanabo does on a single hit.")]
    [SerializeField] private int hitForce = 2;
    [Tooltip("The reel up time in seconds for the Kanabo.")]
    [SerializeField] private float reelUpTime = 0.7f;
    
#pragma warning disable 0649
    [Header("Audio")] [Space(5f)]
    [SerializeField] private AudioSource kanaboAudio;
    public AudioClip[] reelUpSfx;
    public AudioClip[] swingSfx;
    public AudioClip[] hitSfx;
#pragma warning restore 0649

    private const int ShovelMask = 11012424;
    private static readonly int ShovelHit = Animator.StringToHash("shovelHit");
    private static readonly int ReelingUp = Animator.StringToHash("reelingUp");

    private bool _reelingUp;
    private bool _isHoldingButton;
    private bool _animatorSpeedCurrentlyModified;

    private float _origionalPlayerAnimatorSpeed;

    private List<RaycastHit> _objectsHitByKanaboList = [];
    private RaycastHit[] _objectsHitByKanabo;

    private Coroutine _reelingUpCoroutine;

    private PlayerControllerB _previousPlayerHeldBy;

    private enum AudioClipTypes
    {
        ReelUp,
        Swing,
        Hit,
        HitSurface
    }
    
    private void OnDisable()
    {
        if (_animatorSpeedCurrentlyModified)
        {
            playerHeldBy.playerBodyAnimator.speed = _origionalPlayerAnimatorSpeed;
        }
    }

    public override void Start()
    {
        base.Start();
        
        hitForce = Mathf.Clamp(KanaboConfig.Instance.KanaboDamage.Value, 0, int.MaxValue);
        reelUpTime = Mathf.Clamp(KanaboConfig.Instance.KanaboReelUpTime.Value, 0.05f, int.MaxValue);
        
        _origionalPlayerAnimatorSpeed = StartOfRound.Instance.allPlayerScripts[0].playerBodyAnimator.speed;
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (playerHeldBy == null) return;
        
        _isHoldingButton = buttonDown;
        if (_reelingUp || !buttonDown) return;
        
        _reelingUp = true;
        _previousPlayerHeldBy = playerHeldBy;
        if (_reelingUpCoroutine != null)
        {
            StopCoroutine(_reelingUpCoroutine);
            _reelingUpCoroutine = null;
        }
        
        _reelingUpCoroutine = StartCoroutine(ReelUpKanabo());
    }

    private IEnumerator ReelUpKanabo()
    {
        try
        {
            // The null checks are needed because people install loads of badly coded mods which break things.
            if (playerHeldBy == null)
            {
                SeichiItemsPlugin.Log("PlayerHeldBy is null at the start of ReelUpKanabo. Aborting.", LOGPrefix, SeichiItemsPlugin.LogLevel.Warning);
                yield break;
            }
            
            ulong playerHeldByClientId = playerHeldBy.actualClientId;

            // A reference is created here for the same reason as above.
            Animator animator = playerHeldBy.playerBodyAnimator;
            if (animator == null)
            {
                SeichiItemsPlugin.Log("PlayerBodyAnimator is null. Aborting.", LOGPrefix, SeichiItemsPlugin.LogLevel.Warning);
                yield break;
            }
            
            playerHeldBy.activatingItem = true;
            playerHeldBy.twoHanded = true;
            animator.ResetTrigger(ShovelHit);
            animator.SetBool(ReelingUp, true);
            yield return null;
            
            // Get the reel up animation clip.
            AnimationClip reelingUpClip = animator.runtimeAnimatorController?.animationClips
                ?.FirstOrDefault(clip => clip.name == "ShovelReelUp");

            // Check if we found the clip successfully.
            if (reelingUpClip != null)
            {
                // Get the current player body animator speed.
                _origionalPlayerAnimatorSpeed = animator.speed;
                
                // Get the length of the reel up animation.
                float animationOrigionalLength = reelingUpClip.length;
                
                // Calculate the new speed of the reel up.
                float newSpeed = animationOrigionalLength / reelUpTime;
                
                // Set the player body animator to use the new speed.
                _animatorSpeedCurrentlyModified = true;
                animator.speed = newSpeed;
                
                PlayAudioClipTypeServerRpc(AudioClipTypes.ReelUp);
                
                // After the animation is done, change the player body animator speed back to normal.
                yield return new WaitForSeconds(reelUpTime);
                
                // Check AGAIN that the animator reference is still valid
                if (animator == null) animator = StartOfRound.Instance.allPlayerScripts[playerHeldByClientId].playerBodyAnimator;

                animator.speed = _origionalPlayerAnimatorSpeed;
                _animatorSpeedCurrentlyModified = false;
            }
            else
            {
                // If the reel up animation clip was not found, report it as a warning.
                SeichiItemsPlugin.Log("No animation clips found in the runtimeAnimatorController.", LOGPrefix, SeichiItemsPlugin.LogLevel.Warning);
            }
            
            yield return new WaitUntil(() => !_isHoldingButton || !isHeld);
            SwingKanabo(!isHeld);
            yield return new WaitForSeconds(0.13f);
            yield return new WaitForEndOfFrame();
            HitKanabo(!isHeld);
            yield return new WaitForSeconds(0.3f);
        }
        finally
        {
            _reelingUp = false;
            _reelingUpCoroutine = null;
        }
    }

    public void SwingKanabo(bool cancel = false)
    {
        _previousPlayerHeldBy.playerBodyAnimator.SetBool(ReelingUp, false);
        if (cancel) return;
        if (IsServer) PlayAudioClipTypeServerRpc(AudioClipTypes.Swing);
        _previousPlayerHeldBy.UpdateSpecialAnimationValue(true, 
            (short)_previousPlayerHeldBy.transform.localEulerAngles.y, 0.4f);
    }

    public void HitKanabo(bool cancel = false)
    {
        if (_previousPlayerHeldBy == null)
        {
            SeichiItemsPlugin.Log("Variable '_previousPlayerHeldBy' is null on this client when HitKanabo is called.", LOGPrefix, SeichiItemsPlugin.LogLevel.Error);
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
                _objectsHitByKanabo = Physics.SphereCastAll(
                    _previousPlayerHeldBy.gameplayCamera.transform.position +
                    _previousPlayerHeldBy.gameplayCamera.transform.right * -0.35f, 0.8f,
                    _previousPlayerHeldBy.gameplayCamera.transform.forward, 1.5f, ShovelMask,
                    QueryTriggerInteraction.Collide);
                _objectsHitByKanaboList = _objectsHitByKanabo.OrderBy(x => x.distance).ToList();

                for (int i = 0; i < _objectsHitByKanaboList.Count; i++)
                {
                    RaycastHit t = _objectsHitByKanaboList[i];
                    RaycastHit objectsHitByKanabo = t;
                    if (objectsHitByKanabo.transform.gameObject.layer != 8 &&
                        objectsHitByKanabo.transform.gameObject.layer != 11)
                    {
                        objectsHitByKanabo = t;
                        if (objectsHitByKanabo.transform.TryGetComponent(out IHittable component))
                        {
                            objectsHitByKanabo = t;
                            if (!(objectsHitByKanabo.transform == _previousPlayerHeldBy.transform))
                            {
                                objectsHitByKanabo = t;
                                if (!(objectsHitByKanabo.point == Vector3.zero))
                                {
                                    Vector3 position = _previousPlayerHeldBy.gameplayCamera.transform.position;
                                    objectsHitByKanabo = t;
                                    Vector3 point = objectsHitByKanabo.point;
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
                                    component.Hit(hitForce, forward, _previousPlayerHeldBy, true, 1);
                                    flag2 = true;
                                }
                                catch (Exception ex)
                                {
                                    SeichiItemsPlugin.Log(
                                        $"Exception caught when hitting object with Kanabo from player {_previousPlayerHeldBy.playerUsername}: {ex}",
                                        LOGPrefix, SeichiItemsPlugin.LogLevel.Error);
                                }
                            }
                        }

                        continue;
                    }

                    flag1 = true;
                    objectsHitByKanabo = t;
                    for (int index2 = 0; index2 < StartOfRound.Instance.footstepSurfaces.Length; ++index2)
                    {
                        if (StartOfRound.Instance.footstepSurfaces[index2].surfaceTag !=
                            objectsHitByKanabo.collider.gameObject.tag) continue;
                        hitSurfaceID = index2;
                        break;
                    }
                }
            }

            if (!flag1) return;
            if (!flag2 && hitSurfaceID != -1)
            {
                PlayAudioClipTypeClientRpc(AudioClipTypes.HitSurface, hitSurfaceID);
            }

            playerHeldBy.playerBodyAnimator.SetTrigger(ShovelHit);
            PlayAudioClipTypeServerRpc(AudioClipTypes.Hit);
        }
    }

    public override void DiscardItem()
    {
        if (playerHeldBy != null) playerHeldBy.activatingItem = false;
        base.DiscardItem();
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void PlayAudioClipTypeServerRpc(AudioClipTypes audioClipType, bool interrupt = false)
    {
        int numberOfAudioClips = audioClipType switch
        {
            AudioClipTypes.Swing => swingSfx.Length,
            AudioClipTypes.Hit => hitSfx.Length,
            AudioClipTypes.ReelUp => reelUpSfx.Length,
            _ => -1
        };

        switch (numberOfAudioClips)
        {
            case 0:
                SeichiItemsPlugin.Log($"There are no audio clips for audio clip type {audioClipType}.", LOGPrefix, SeichiItemsPlugin.LogLevel.Error);
                return;
            
            case -1:
                SeichiItemsPlugin.Log($"Audio Clip Type was not listed, cannot play audio clip. Number of audio clips: {numberOfAudioClips}.", LOGPrefix, SeichiItemsPlugin.LogLevel.Error);
                return;

            default:
            {
                int clipIndex = Random.Range(0, numberOfAudioClips);
                PlayAudioClipTypeClientRpc(audioClipType, clipIndex, interrupt);
                break;
            }
        }
    }
    
    /// <summary>
    /// Plays an audio clip with the given type and index
    /// </summary>
    /// <param name="audioClipType">The audio clip type to play.</param>
    /// <param name="clipIndex">The index of the clip in their respective AudioClip array to play.</param>
    /// <param name="interrupt">Whether to interrupt any previously playing sound before playing the new audio.</param>
    [ClientRpc]
    private void PlayAudioClipTypeClientRpc(AudioClipTypes audioClipType, int clipIndex, bool interrupt = false)
    {
        AudioClip audioClipToPlay = audioClipType switch
        {
            AudioClipTypes.Hit => hitSfx[clipIndex],
            AudioClipTypes.Swing => swingSfx[clipIndex],
            AudioClipTypes.ReelUp => reelUpSfx[clipIndex],
            AudioClipTypes.HitSurface => StartOfRound.Instance.footstepSurfaces[clipIndex].hitSurfaceSFX,
            _ => null
        };

        if (audioClipToPlay == null)
        {
            SeichiItemsPlugin.Log($"Invalid audio clip with type: {audioClipType} and index: {clipIndex}", LOGPrefix, SeichiItemsPlugin.LogLevel.Error);
            return;
        }
        
        if (interrupt) kanaboAudio.Stop(true);
        kanaboAudio.PlayOneShot(audioClipToPlay);
        WalkieTalkie.TransmitOneShotAudio(kanaboAudio, audioClipToPlay, kanaboAudio.volume);
        RoundManager.Instance.PlayAudibleNoise(transform.position, 8, 0.4f);
    }
}