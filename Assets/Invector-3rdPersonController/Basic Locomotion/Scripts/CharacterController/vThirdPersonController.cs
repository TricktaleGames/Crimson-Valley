﻿using UnityEngine;
using System.Collections;
using Invector.vCamera;
using Prototype.NetworkLobby;
using UnityEngine.Networking;

namespace Invector.vCharacterController
{
    [vClassHeader("THIRD PERSON CONTROLLER", iconName = "controllerIcon")]
    public class vThirdPersonController : vThirdPersonAnimator
    {
        #region Variables

        //public static vThirdPersonController instance;

        [SyncVar]
        public LobbyData LobbyData;

        #endregion

        protected virtual void Awake()
        {
            StartCoroutine(UpdateRaycast()); // limit raycasts calls for better performance
        }

        public bool HasPartyFrame()
        {
            return m_frame != null;
        }

        public override void AssignPartyFrame(PartyFrame frame)
        {
            base.AssignPartyFrame(frame);

            if(m_frame && m_frame.Name)
            {
                m_frame.Name.text = LobbyData.Name;
            }
        }

        protected override void Start()
        {
            base.Start();

            //create hud frame for this player
            var hudCtrl = FindObjectOfType<HudController>();
            if(hudCtrl)
            {
                hudCtrl.AddPartyMember(this);
            }

            if (GameController.Instance.IsMultyPlayer())
            {
                if (isLocalPlayer)
                {
                    Camera.main.GetComponent<vThirdPersonCamera>().target = this.gameObject.transform;
                    Camera.main.GetComponent<vThirdPersonCamera>().Init();

                    //minimap
                    if (hudCtrl)
                    {
                        var mapCanvasCtrl = hudCtrl.GetComponentInChildren<MapCanvasController>(true);
                        mapCanvasCtrl.gameObject.SetActive(true);
                        mapCanvasCtrl.Load();
                    }

                    LobbyManager.s_Singleton.SendClientReadyToBegin();

                    // Show "Waiting for other players" text
                    MenuController.Instance.OnLocalLoaded();

                    // It is safe to init the chat controller now
                    ChatController.Instance.Init(this);
                }
                else
                {
                    this.gameObject.GetComponent<MapMarker>().enabled = true;

                    //Destroy(this);
                    //return;
                }
            }
        }

        #region Locomotion Actions

        [Command]
        public void CmdSetSprinting(bool value)
        {
            isSprinting = value;
        }

        public virtual void Sprint(bool value)
        {
            if (value)
            {
                if (currentStamina > 0 && input.sqrMagnitude > 0.1f)
                {
                    if (isGrounded && !isCrouching)
                    {
                        isSprinting = !isSprinting;
                        CmdSetSprinting(isSprinting);
                    }                        
                }
            }
            else if (currentStamina <= 0 || input.sqrMagnitude < 0.1f || isCrouching || !isGrounded || actions || isStrafing && !strafeSpeed.walkByDefault && (direction >= 0.5 || direction <= -0.5 || speed <= 0))
            {
                isSprinting = false;
                CmdSetSprinting(isSprinting);
            }
        }

        [Command]
        public void CmdSetCrouching(bool value)
        {
            isCrouching = value;
        }

        public virtual void Crouch()
        {
            if (isGrounded && !actions)
            {
                if (isCrouching && CanExitCrouch())
                    isCrouching = false;
                else
                    isCrouching = true;

                CmdSetCrouching(isCrouching);
            }
        }

        [Command]
        public void CmdSetStrafing(bool value)
        {
            isStrafing = value;
        }

        public virtual void Strafe()
        {
            isStrafing = !isStrafing;
            CmdSetStrafing(isStrafing);
        }

        public virtual void Jump(bool consumeStamina = false)
        {
            CmdJump(consumeStamina);

            if(!isServer)
            {
                JumpInternal(consumeStamina);
            }
        }

        [Command]
        public void CmdJump(bool consumeStamina)
        {
            if(JumpInternal(consumeStamina))
            {
                RpcJump(consumeStamina);
            }
        }

        [ClientRpc]
        public void RpcJump(bool consumeStamina)
        {
            if(!isLocalPlayer)
            {
                //Do not consume stamina on client, it is already synced
                JumpInternal(false);
            }
        }

        public bool JumpInternal(bool consumeStamina)
        {
            if (customAction) return false;

            // know if has enough stamina to make this action
            bool staminaConditions = currentStamina > jumpStamina;
            // conditions to do this action
            bool jumpConditions = !isCrouching && isGrounded && !actions && staminaConditions && !isJumping;
            // return if jumpCondigions is false
            if (!jumpConditions) return false;
            // trigger jump behaviour
            jumpCounter = jumpTimer;
            isJumping = true;
            // trigger jump animations
            if (input.sqrMagnitude < 0.1f)
                animator.CrossFadeInFixedTime("Jump", 0.1f);
            else
                animator.CrossFadeInFixedTime("JumpMove", .2f);
            // reduce stamina
            if (consumeStamina && isServer)
            {
                ReduceStamina(jumpStamina, false);
                currentStaminaRecoveryDelay = 1f;
            }

            return true;
        }

        public virtual void Roll()
        {
            CmdRoll();

            if(!isServer)
            {
                RollInternal();
            }
        }

        [Command]
        public void CmdRoll()
        {
            if (RollInternal())
            {
                RpcRoll();
            }
        }

        [ClientRpc]
        public void RpcRoll()
        {
            if (!isLocalPlayer)
            {
                RollInternal();
            }
        }

        public bool RollInternal()
        {
            bool staminaCondition = currentStamina > rollStamina;
            // can roll even if it's on a quickturn or quickstop animation
            bool actionsRoll = !actions || (actions && (quickStop));
            // general conditions to roll
            bool rollConditions = (input != Vector2.zero || speed > 0.25f) && actionsRoll && isGrounded && staminaCondition && !isJumping;

            if (!rollConditions || isRolling) return false;

            animator.CrossFadeInFixedTime("Roll", 0.1f);
            if(isServer)
            {
                ReduceStamina(rollStamina, false);
                currentStaminaRecoveryDelay = 2f;
            }

            return true;
        }

        /// <summary>
        /// Use another transform as  reference to rotate
        /// </summary>
        /// <param name="referenceTransform"></param>
        public virtual void RotateWithAnotherTransform(Transform referenceTransform)
        {
            var newRotation = new Vector3(transform.eulerAngles.x, referenceTransform.eulerAngles.y, transform.eulerAngles.z);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(newRotation), strafeSpeed.rotationSpeed * Time.deltaTime);
            targetRotation = transform.rotation;
        }

        #endregion

        #region Check Action Triggers 

        /// <summary>
        /// Call this in OnTriggerEnter or OnTriggerStay to check if enter in triggerActions     
        /// </summary>
        /// <param name="other">collider trigger</param>                         
        protected override void OnTriggerStay(Collider other)
        {
            try
            {
                CheckForAutoCrouch(other);
            }
            catch (UnityException e)
            {
                Debug.LogWarning(e.Message);
            }
            base.OnTriggerStay(other);
        }

        /// <summary>
        /// Call this in OnTriggerExit to check if exit of triggerActions 
        /// </summary>
        /// <param name="other"></param>
        protected override void OnTriggerExit(Collider other)
        {
            AutoCrouchExit(other);
            base.OnTriggerExit(other);
        }

        #region Update Raycasts  

        protected IEnumerator UpdateRaycast()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();

                AutoCrouch();
                //StopMove();
            }
        }

        #endregion

        #region Crouch Methods

        protected virtual void AutoCrouch()
        {
            if (autoCrouch)
                isCrouching = true;

            if (autoCrouch && !inCrouchArea && CanExitCrouch())
            {
                autoCrouch = false;
                isCrouching = false;
            }
        }

        protected virtual bool CanExitCrouch()
        {
            // radius of SphereCast
            float radius = _capsuleCollider.radius * 0.9f;
            // Position of SphereCast origin stating in base of capsule
            Vector3 pos = transform.position + Vector3.up * ((colliderHeight * 0.5f) - colliderRadius);
            // ray for SphereCast
            Ray ray2 = new Ray(pos, Vector3.up);

            // sphere cast around the base of capsule for check ground distance
            if (Physics.SphereCast(ray2, radius, out groundHit, headDetect - (colliderRadius * 0.1f), autoCrouchLayer))
                return false;
            else
                return true;
        }

        protected virtual void AutoCrouchExit(Collider other)
        {
            if (other.CompareTag("AutoCrouch"))
            {
                inCrouchArea = false;
            }
        }

        protected virtual void CheckForAutoCrouch(Collider other)
        {
            if (other.gameObject.CompareTag("AutoCrouch"))
            {
                autoCrouch = true;
                inCrouchArea = true;
            }
        }

        #endregion

        #endregion
    }
}