using System;
using System.Collections;
using Core.Camera;
using Core.Utils;
using Inventory.Main;
using Inventory.Main.Item;
using Inventory.Main.Slot;
using Locomotion.Controllers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Weapon.Utils;

namespace Weapon.Main
{
    public class FirearmAdapter : RangedWeaponAdapter<Firearm, FirearmReference>
    {
        [SerializeField] private Transform pivotTransform;
        
        [SerializeField] private Transform dockTransform;

        [Space]
        
        [SerializeField] private ParticleSystem muzzleEffect;
        
        [Space]
        
        [SerializeField] private Grabber.GrabTarget[] grabTargets;
        
        private float _fireTime;
        
        private float _cooldownTime;
        
        private float _fireDuration;
        
        private float _cooldownDuration;

        private Transform _target;
        
        private Transform _dock;
        
        private Transform _cachedParent;

        private Beamer _beamer;

        private Grabber _grabber;
        
        private CameraManager _cameraManager;

        private int _recoilIndex;
        
        private Coroutine _resetRecoilCoroutine;

        public bool IsAimingDown => !CanUse[UsageType.Secondary];
        
        protected override void CharacterReady()
        {
            base.CharacterReady();

            TryGetCameraManager();

            TryGetTarget();

            TryGetGrabber();
            
            _beamer = Character.Beamer;
            
            AddUsage(Fire);
            
            AddUsage(_cameraManager.ZoomIn, UsageType.Secondary);

            AddStoppage(StopFiring);
            
            AddStoppage(_cameraManager.ZoomOut, UsageType.Secondary);
        }

        private void TryGetCameraManager()
        {
            if (GameManager.Instance.IsReady) GameManager.Instance.GetManager(out _cameraManager);

            else GameManager.Instance.OnReady += delegate { GameManager.Instance.GetManager(out _cameraManager); };
        }

        private void TryGetTarget()
        {
            if (Character.GetController(out MotionController motionController))
            {
                if (motionController.IsReady) _target = motionController.Target;

                else motionController.OnReady += delegate { _target = motionController.Target; };
            }
        }

        private void TryGetGrabber()
        {
            if (Character.GetController(out InventoryController inventoryController))
            {
                if (inventoryController.IsReady) _grabber = inventoryController.GetComponent<Grabber>();

                else inventoryController.OnReady += delegate { _grabber = inventoryController.GetComponent<Grabber>(); };
            }
        }
        
        public override void Equip()
        {
            base.Equip();

            _cachedParent = transform.parent;

            _grabber.Equip(item.SlotType);
        }

        public override void UnEquip()
        {
            base.UnEquip();

            _grabber.UnEquip(item.SlotType);

            muzzleEffect.gameObject.SetActive(false);
            
            _dock.LocalReset();

            _dock = null;

            transform.LocalReset(_cachedParent);
        }

        public override void EquippedCallback()
        {
            base.EquippedCallback();

            muzzleEffect.gameObject.SetActive(true);
            
            _dock = _grabber.Docks[item.SlotType];
            
            transform.SetParent(_dock);

            _dock.localPosition = dockTransform.localPosition;

            LookAtTarget();

            transform.SetLocalPositionAndRotation(pivotTransform.localPosition, pivotTransform.localRotation);

            _grabber.Equipped(item.SlotType, grabTargets);
        }

        public override void UnEquippedCallback()
        {
            
        }

        private void Update()
        {
            if (!IsEquipped) return;
            
            LookAtTarget();
            
            TryFireOnUpdate();
        }

        private void LookAtTarget()
        {
            if (_target != null && _dock != null)
            {
                _dock.rotation = Quaternion.LookRotation(_target.position - _dock.position);;
            }
        }

        private void TryFireOnUpdate()
        {
            //finished firing/hasn't started
            if (CanUse[UsageType.Primary]) return;
            
            Vector3 pivotLocalPosition = pivotTransform.localPosition;
            
            if (_fireTime < _fireDuration)
            {
                transform.localPosition = Vector3.Lerp(pivotLocalPosition, pivotLocalPosition + Reference.Recoil,
                    _fireTime / _fireDuration);
                
                _fireTime += Time.deltaTime;

                if (_fireTime >= _fireDuration)
                {
                    transform.localPosition = pivotLocalPosition + Reference.Recoil;
                    
                    _cooldownTime = 0;
                }
            }
            
            else if (_cooldownTime < _cooldownDuration)
            {
                transform.localPosition = Vector3.Lerp(pivotLocalPosition + Reference.Recoil, pivotLocalPosition,
                    _cooldownTime / _cooldownDuration);
                
                _cooldownTime += Time.deltaTime;

                if (_cooldownTime >= _cooldownDuration)
                {
                    transform.localPosition = pivotLocalPosition;
                }
            }
            
            else
            {
                CanUse[UsageType.Primary] = true;

                //automatic/continuous
                if (!Reference.SingleFire) Use();
            }
        }
        
        private void Fire()
        {
            CanUse[UsageType.Primary] = false;

            _fireDuration = Reference.FireDuration;
                
            _cooldownDuration = Reference.CooldownDuration;

            Vector3 shake = Reference.CameraShake;

            //aiming down sight
            if (IsAimingDown) shake *= Reference.DownSightFactor;
            
            _cameraManager.Shake(shake, _fireDuration, _cooldownDuration);
                
            _fireTime = 0;
            
            muzzleEffect.Play();

            _cameraManager.Gain(GetRecoilGain(), Reference.TotalDuration);

            RaycastHit[] hits = _beamer.Beam();

            if (hits == null || hits.Length == 0)
            {
                return;
            }

            RaycastHit hit = hits[0]; 

            GameObject hitObj = Instantiate(Reference.HitObjPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            
            hitObj.transform.parent = hit.transform;
            
            Vector3 hitDirection = (hit.point - _beamer.transform.position).normalized;

            if (hit.collider.TryGetComponent(out Rigidbody rBody))
            {
                //multiply by mass because bullets are fast af
                rBody.AddForceAtPosition(Reference.Power * hitDirection, hit.point);
            }
            
            Destroy(hitObj, 5f);
        }
        
        private void StopFiring()
        {
            transform.localPosition = pivotTransform.localPosition;

            if (_resetRecoilCoroutine != null) StopCoroutine(_resetRecoilCoroutine);

            _resetRecoilCoroutine = StartCoroutine(ResetRecoilIndex());
        }

        private IEnumerator ResetRecoilIndex()
        {
            yield return new WaitForSeconds(Reference.RecoilPatternTimeout);

            _recoilIndex = 0;
        }

        private Vector2 GetRecoilGain()
        {
            _recoilIndex++;

            if (_recoilIndex >= Reference.RecoilPattern.Length) _recoilIndex = 0;

            Vector2 recoilGain = Reference.RecoilPattern[_recoilIndex];
            
            //aiming down sight
            if (IsAimingDown) recoilGain *= Reference.DownSightFactor;
            
            return recoilGain;
        }
    }
}