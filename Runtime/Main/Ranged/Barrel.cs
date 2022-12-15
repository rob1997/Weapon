using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Utils;
using UnityEngine;

namespace Weapon.Main
{
    [RequireComponent(typeof(Beamer))]
    public class Barrel : MonoBehaviour
    {
        [SerializeField] private Vector2[] snapshots;

        private Transform _origin;

        private BarrelGroup _group;

        [field: SerializeField] public float Power { get; private set; }
        
        public Beamer Beamer { get; private set; }

        private void Awake()
        {
            Beamer = GetComponent<Beamer>();

            _group = GetComponentInParent<BarrelGroup>();
        }

        public void SetOrigin(Transform origin)
        {
            _origin = origin;
        }

        public void Fire(int index)
        {
            index = snapshots.WrapIndex(index);

            Vector2 snapshot = snapshots[index];

            Vector3 centre = _origin.position;

            //position the Beamer units from the snapshot
            Vector3 position = centre + _origin.forward + (snapshot.x * _group.Spread * _origin.right) +
                               (snapshot.y * _group.Spread * _origin.up);

            Quaternion rotation = Quaternion.LookRotation(position - centre);

            transform.SetPositionAndRotation(position, rotation);
            
            //
            RaycastHit[] hits = Beamer.Beam();

            if (hits == null || hits.Length == 0)
            {
                return;
            }

            RaycastHit hit = hits[0];

            GameObject hitObj = Instantiate(_group.HitObjPrefab, hit.point, Quaternion.LookRotation(hit.normal));

            hitObj.transform.parent = hit.transform;

            Vector3 hitDirection = (hit.point - transform.position).normalized;

            if (hit.collider.TryGetComponent(out Rigidbody rBody))
            {
                //multiply by mass because bullets are fast af
                rBody.AddForceAtPosition(Power * hitDirection, hit.point);
            }

            Destroy(hitObj, _group.HitObjDestroyTimeout);
        }

        public void TakeSnapshot()
        {
            snapshots = snapshots.Append((Vector2) transform.localPosition).ToArray();
        }
    }
}