using System;
using UnityEngine;
using UnityEngine.XR;

[Serializable]
public class VRMap
{
	public XRNode vrTargetNode;

	public Transform overrideTarget;

	public Transform rigTarget;

	public Vector3 trackingPositionOffset;

	public Vector3 trackingRotationOffset;

	public Transform headTransform;

	public Vector3 syncPos;

	public Quaternion syncRotation;

	public float calcT;

	private InputDevice myInputDevice;

	private Vector3 tempPosition;

	private Quaternion tempRotation;

	public int tempInt;

	public void MapOther(float lerpValue)
	{
		rigTarget.localPosition = Vector3.Lerp(rigTarget.localPosition, syncPos, lerpValue);
		rigTarget.localRotation = Quaternion.Lerp(rigTarget.localRotation, syncRotation, lerpValue);
	}

	public void MapMine(float ratio, Transform playerOffsetTransform)
	{
		myInputDevice = InputDevices.GetDeviceAtXRNode(vrTargetNode);
		if ((ConnectedControllerHandler.Instance.GetValidForXRNode(vrTargetNode) && myInputDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out tempRotation) && myInputDevice.TryGetFeatureValue(CommonUsages.devicePosition, out tempPosition)) || overrideTarget == null)
		{
			rigTarget.rotation = tempRotation * Quaternion.Euler(trackingRotationOffset);
			if (overrideTarget != null)
			{
				rigTarget.RotateAround(overrideTarget.position, Vector3.up, playerOffsetTransform.eulerAngles.y);
				rigTarget.position = overrideTarget.position + rigTarget.rotation * trackingPositionOffset * ratio;
			}
			else
			{
				rigTarget.position = tempPosition + rigTarget.rotation * trackingPositionOffset * ratio + playerOffsetTransform.position;
				rigTarget.RotateAround(playerOffsetTransform.position, Vector3.up, playerOffsetTransform.eulerAngles.y);
			}
		}
		else
		{
			rigTarget.rotation = overrideTarget.rotation * Quaternion.Euler(trackingRotationOffset);
			if (overrideTarget != null)
			{
				rigTarget.position = overrideTarget.position + rigTarget.rotation * trackingPositionOffset * ratio;
				return;
			}
			rigTarget.position = tempPosition + overrideTarget.rotation * trackingPositionOffset * ratio + playerOffsetTransform.position;
			rigTarget.RotateAround(playerOffsetTransform.position, Vector3.up, playerOffsetTransform.eulerAngles.y);
		}
	}

	public virtual void MapOtherFinger(float handSync, float lerpValue)
	{
		calcT = handSync;
		LerpFinger(lerpValue, isOther: true);
	}

	public virtual void MapMyFinger(float lerpValue)
	{
	}

	public virtual void LerpFinger(float lerpValue, bool isOther)
	{
	}
}
