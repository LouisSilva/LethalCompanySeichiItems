using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace LethalCompanySeichiItems.Kanabo;

public class KanaboItem : GrabbableObject
{
    private ManualLogSource _mls;
    private string _kanaboId;
    
    [Tooltip("The amount of damage the Kanabo does on a single hit.")]
    [SerializeField] private int hitForce = 2;
    [Tooltip("The reel up time in seconds for the Kanabo.")]
    [SerializeField] private float reelUpTime = 0.7f;
    
#pragma warning disable 0649
    [Header("Audio")] [Space(5f)]
    [SerializeField] private AudioSource kanaboAudio;
    public AudioClip reelUp;
    public AudioClip swing;
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

    public override void Start()
    {
        base.Start();
        _kanaboId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource($"{SeichiItemsPlugin.ModGuid} | Kanabo {_kanaboId}");
        hitForce = Mathf.Clamp(KanaboConfig.Instance.KanaboDamage.Value, 0, int.MaxValue);
        reelUpTime = Mathf.Clamp(KanaboConfig.Instance.KanaboReelUpTime.Value, 0.05f, int.MaxValue);
    }

    private void OnDisable()
    {
        if (_animatorSpeedCurrentlyModified)
        {
            playerHeldBy.playerBodyAnimator.speed = _origionalPlayerAnimatorSpeed;
        }
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (playerHeldBy == null) return;
        
        _isHoldingButton = buttonDown;
        if (_reelingUp || !buttonDown) return;
        
        _reelingUp = true;
        _previousPlayerHeldBy = playerHeldBy;
        if (_reelingUpCoroutine != null) StopCoroutine(_reelingUpCoroutine);
        
        _reelingUpCoroutine = StartCoroutine(ReelUpKanabo());
    }

    private IEnumerator ReelUpKanabo()
    {
        playerHeldBy.activatingItem = true;
        playerHeldBy.twoHanded = true;
        playerHeldBy.playerBodyAnimator.ResetTrigger(ShovelHit);
        playerHeldBy.playerBodyAnimator.SetBool(ReelingUp, true);
        yield return null;

        // replace state info stuff with finding the animation clip and getting its speed
        AnimationClip reelingUpClip = playerHeldBy.playerBodyAnimator.runtimeAnimatorController.animationClips
            .FirstOrDefault(clip => clip.name == "ShovelReelUp");

        if (reelingUpClip != null)
        {
            _origionalPlayerAnimatorSpeed = playerHeldBy.playerBodyAnimator.speed;
            float animationOrigionalLength = reelingUpClip.length;
            float newSpeed = animationOrigionalLength / reelUpTime;
            
            _animatorSpeedCurrentlyModified = true;
            playerHeldBy.playerBodyAnimator.speed = newSpeed;
            kanaboAudio.PlayOneShot(reelUp);
            ReelUpSfxServerRpc();
        
            yield return new WaitForSeconds(reelUpTime);

            playerHeldBy.playerBodyAnimator.speed = _origionalPlayerAnimatorSpeed;
            _animatorSpeedCurrentlyModified = false;
        }
        else
        {
            LogDebug("Reeling up clip was null");
        }
        
        yield return new WaitUntil(() => !_isHoldingButton || !isHeld);
        SwingKanabo(!isHeld);
        yield return new WaitForSeconds(0.13f);
        yield return new WaitForEndOfFrame();
        HitKanabo(!isHeld);
        yield return new WaitForSeconds(0.3f);
        _reelingUp = false;
        _reelingUpCoroutine = null;
    }

    public void SwingKanabo(bool cancel = false)
    {
        _previousPlayerHeldBy.playerBodyAnimator.SetBool(ReelingUp, false);
        if (cancel) return;
        kanaboAudio.PlayOneShot(swing);
        _previousPlayerHeldBy.UpdateSpecialAnimationValue(true, 
            (short)_previousPlayerHeldBy.transform.localEulerAngles.y, 0.4f);
    }

    public void HitKanabo(bool cancel = false)
    {
        if (_previousPlayerHeldBy == null)
        {
            _mls.LogError("Variable '_previousPlayerHeldBy' is null on this client when HitKanabo is called.");
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
                
                foreach (RaycastHit t in _objectsHitByKanaboList)
                {
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
                                    _mls.LogInfo($"Exception caught when hitting object with Kanabo from player #{_previousPlayerHeldBy.playerClientId}: {ex}");
                                }
                            }
                        }

                        continue;
                    }

                    flag1 = true;
                    objectsHitByKanabo = t;
                    for (int index2 = 0; index2 < StartOfRound.Instance.footstepSurfaces.Length; ++index2)
                    {
                        if (StartOfRound.Instance.footstepSurfaces[index2].surfaceTag != objectsHitByKanabo.collider.gameObject.tag) continue;
                        hitSurfaceID = index2;
                        break;
                    }
                }
            }

            if (!flag1) return;
            RoundManager.PlayRandomClip(kanaboAudio, hitSfx);
            FindObjectOfType<RoundManager>().PlayAudibleNoise(transform.position, 17f, 0.8f);
            if (!flag2 && hitSurfaceID != -1)
            {
                kanaboAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
                WalkieTalkie.TransmitOneShotAudio(kanaboAudio,
                    StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
            }

            playerHeldBy.playerBodyAnimator.SetTrigger(ShovelHit);
            HitKanaboServerRpc(hitSurfaceID);
        }
    }
    
    [ServerRpc]
    public void ReelUpSfxServerRpc()
    {
        ReelUpSfxClientRpc();
    }

    [ClientRpc]
    public void ReelUpSfxClientRpc()
    {
        kanaboAudio.PlayOneShot(reelUp);
    }

    public override void DiscardItem()
    {
        if (playerHeldBy != null) playerHeldBy.activatingItem = false;
        base.DiscardItem();
    }

    [ServerRpc]
    private void HitKanaboServerRpc(int hitSurfaceID)
    {
        HitKanaboClientRpc(hitSurfaceID);
    }

    [ClientRpc]
    private void HitKanaboClientRpc(int hitSurfaceID)
    {
        RoundManager.PlayRandomClip(kanaboAudio, hitSfx);
        if (hitSurfaceID == -1) return;
        HitSurfaceWithKanabo(hitSurfaceID);
    }

    private void HitSurfaceWithKanabo(int hitSurfaceID)
    {
        kanaboAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
        WalkieTalkie.TransmitOneShotAudio(kanaboAudio,
            StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
    }

    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo($"{msg}");
        #endif
    }
}