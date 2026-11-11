using System;
using System.Collections.Generic;
using UnityEngine;

public class TransferrableItemSlotTransformOverride : MonoBehaviour
{
	[Serializable]
	public struct SlotTransformOverride
	{
		public Transform overrideTransform;

		public TransferrableObject.PositionState positionState;
	}

	public List<SlotTransformOverride> transformOverridesList;

	public TransferrableObject.PositionState lastPosition;

	public TransferrableObject followingTransferrableObject;

	public SlotTransformOverride defaultPosition;

	public Transform defaultTransform;

	private void Awake()
	{
		defaultPosition = new SlotTransformOverride
		{
			overrideTransform = defaultTransform,
			positionState = TransferrableObject.PositionState.None
		};
		lastPosition = TransferrableObject.PositionState.None;
	}

	private void Update()
	{
		if (followingTransferrableObject == null)
		{
			return;
		}
		if (followingTransferrableObject.currentState != lastPosition)
		{
			SlotTransformOverride slotTransformOverride = transformOverridesList.Find((SlotTransformOverride x) => (x.positionState & followingTransferrableObject.currentState) != 0);
			if (slotTransformOverride.positionState == TransferrableObject.PositionState.None)
			{
				slotTransformOverride = defaultPosition;
			}
			if (slotTransformOverride.overrideTransform != null)
			{
				followingTransferrableObject.transform.position = slotTransformOverride.overrideTransform.position;
				followingTransferrableObject.transform.rotation = slotTransformOverride.overrideTransform.rotation;
			}
		}
		lastPosition = followingTransferrableObject.currentState;
	}
}
