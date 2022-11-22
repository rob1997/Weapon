using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Utils;
using Inventory.Main;
using Inventory.Main.Slot;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Weapon.Utils
{
    [RequireComponent(typeof(InventoryController))]
    public class Grabber : MonoBehaviour
    {
        [Serializable]
        public class Hand
        {
            [HideInInspector] public Transform Target;

            [field: SerializeField] public TwoBoneIKConstraint Constraint { get; set; }

            [Tooltip("Which slots use this hand")] [HideInInspector]
            public GenericDictionary<UsableSlotType, bool> Dependencies =
                GenericDictionary<UsableSlotType, bool>.ToGenericDictionary(Core.Utils.Utils.GetEnumValues<UsableSlotType>()
                    .ToDictionary(s => s, s => false));
        }

        [Tooltip("This is where items are placed in (re-parented to be grabbed)")] [HideInInspector]
        public GenericDictionary<UsableSlotType, Transform> Docks =
            GenericDictionary<UsableSlotType, Transform>.ToGenericDictionary(Core.Utils.Utils.GetEnumValues<UsableSlotType>()
                .ToDictionary(s => s, s => default(Transform)));

        [HideInInspector] public Hand[] Hands = { };

        public void Equip(UsableSlotType slotType)
        {
            foreach (Hand hand in Hands.Where(h => h.Dependencies[slotType])) hand.Target = null;
        }

        public void EquippedLeft(Transform left)
        {
            Hand hand = Hands.FirstOrDefault(h => h.Dependencies[UsableSlotType.LeftHand]);

            if (hand != null)
                hand.Target = left;

            else
                Debug.LogError("Left Hand not found");
        }

        public void EquippedRight(Transform right)
        {
            Hand hand = Hands.FirstOrDefault(h => h.Dependencies[UsableSlotType.RightHand]);

            if (hand != null)
                hand.Target = right;

            else
                Debug.LogError("Right Hand not found");
        }

        public void UnEquip(UsableSlotType slotType)
        {
            foreach (Hand hand in Hands.Where(h => h.Dependencies[slotType])) hand.Target = null;
        }

        private void Update()
        {
            foreach (Hand hand in Hands)
            {
                if (hand.Target != null)
                {
                    hand.Constraint.weight = 1;

                    hand.Constraint.data.target.position = hand.Target.position;
                    hand.Constraint.data.target.rotation = hand.Target.rotation;
                }

                else
                    hand.Constraint.weight = 0;
            }
        }
    }
}