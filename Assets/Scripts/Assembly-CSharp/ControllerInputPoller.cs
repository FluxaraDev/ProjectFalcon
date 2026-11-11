using UnityEngine;
using UnityEngine.XR;

public class ControllerInputPoller : MonoBehaviour
{
	public static volatile ControllerInputPoller instance;

	public float leftControllerIndexFloat;

	public float leftControllerGripFloat;

	public float rightControllerIndexFloat;

	public float rightControllerGripFloat;

	public float leftControllerIndexTouch;

	public float rightControllerIndexTouch;

	public float rightStickLRFloat;

	public Vector3 leftControllerPosition;

	public Vector3 rightControllerPosition;

	public Vector3 headPosition;

	public Quaternion leftControllerRotation;

	public Quaternion rightControllerRotation;

	public Quaternion headRotation;

	public InputDevice leftControllerDevice;

	public InputDevice rightControllerDevice;

	public InputDevice headDevice;

	public bool leftControllerPrimaryButton;

	public bool leftControllerSecondaryButton;

	public bool rightControllerPrimaryButton;

	public bool rightControllerSecondaryButton;

	public bool leftControllerPrimaryButtonTouch;

	public bool leftControllerSecondaryButtonTouch;

	public bool rightControllerPrimaryButtonTouch;

	public bool rightControllerSecondaryButtonTouch;

	private void Awake()
	{
		if (instance == null)
		{
			instance = this;
		}
		else if (instance != this)
		{
			Object.Destroy(base.gameObject);
		}
	}

	private void Update()
	{
		_ = leftControllerDevice;
		if (!leftControllerDevice.isValid)
		{
			leftControllerDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
		}
		_ = rightControllerDevice;
		if (!rightControllerDevice.isValid)
		{
			rightControllerDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
		}
		_ = headDevice;
		if (!headDevice.isValid)
		{
			headDevice = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
		}
		_ = leftControllerDevice;
		_ = rightControllerDevice;
		_ = headDevice;
		leftControllerDevice.TryGetFeatureValue(CommonUsages.primaryButton, out leftControllerPrimaryButton);
		leftControllerDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out leftControllerSecondaryButton);
		leftControllerDevice.TryGetFeatureValue(CommonUsages.primaryTouch, out leftControllerPrimaryButtonTouch);
		leftControllerDevice.TryGetFeatureValue(CommonUsages.secondaryTouch, out leftControllerSecondaryButtonTouch);
		leftControllerDevice.TryGetFeatureValue(CommonUsages.grip, out leftControllerGripFloat);
		leftControllerDevice.TryGetFeatureValue(CommonUsages.trigger, out leftControllerIndexFloat);
		leftControllerDevice.TryGetFeatureValue(CommonUsages.devicePosition, out leftControllerPosition);
		leftControllerDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out leftControllerRotation);
		rightControllerDevice.TryGetFeatureValue(CommonUsages.primaryButton, out rightControllerPrimaryButton);
		rightControllerDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out rightControllerSecondaryButton);
		rightControllerDevice.TryGetFeatureValue(CommonUsages.primaryTouch, out rightControllerPrimaryButtonTouch);
		rightControllerDevice.TryGetFeatureValue(CommonUsages.secondaryTouch, out rightControllerSecondaryButtonTouch);
		rightControllerDevice.TryGetFeatureValue(CommonUsages.grip, out rightControllerGripFloat);
		rightControllerDevice.TryGetFeatureValue(CommonUsages.trigger, out rightControllerIndexFloat);
		rightControllerDevice.TryGetFeatureValue(CommonUsages.devicePosition, out rightControllerPosition);
		rightControllerDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out rightControllerRotation);
		leftControllerDevice.TryGetFeatureValue(CommonUsages.indexTouch, out leftControllerIndexTouch);
		rightControllerDevice.TryGetFeatureValue(CommonUsages.indexTouch, out rightControllerIndexTouch);
		headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out headPosition);
		headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out headRotation);
	}

	public static bool PrimaryButtonPress(XRNode node)
	{
		switch (node)
		{
		case XRNode.LeftHand:
			return instance.leftControllerPrimaryButton;
		case XRNode.RightHand:
			return instance.rightControllerPrimaryButton;
		default:
			return false;
		}
	}

	public static bool SecondaryButtonPress(XRNode node)
	{
		switch (node)
		{
		case XRNode.LeftHand:
			return instance.leftControllerSecondaryButton;
		case XRNode.RightHand:
			return instance.rightControllerSecondaryButton;
		default:
			return false;
		}
	}

	public static bool PrimaryButtonTouch(XRNode node)
	{
		switch (node)
		{
		case XRNode.LeftHand:
			return instance.leftControllerPrimaryButtonTouch;
		case XRNode.RightHand:
			return instance.rightControllerPrimaryButtonTouch;
		default:
			return false;
		}
	}

	public static bool SecondaryButtonTouch(XRNode node)
	{
		switch (node)
		{
		case XRNode.LeftHand:
			return instance.leftControllerSecondaryButtonTouch;
		case XRNode.RightHand:
			return instance.rightControllerSecondaryButtonTouch;
		default:
			return false;
		}
	}

	public static float GripFloat(XRNode node)
	{
		switch (node)
		{
		case XRNode.LeftHand:
			return instance.leftControllerGripFloat;
		case XRNode.RightHand:
			return instance.rightControllerGripFloat;
		default:
			return 0f;
		}
	}

	public static float TriggerFloat(XRNode node)
	{
		switch (node)
		{
		case XRNode.LeftHand:
			return instance.leftControllerIndexFloat;
		case XRNode.RightHand:
			return instance.rightControllerIndexFloat;
		default:
			return 0f;
		}
	}

	public static float TriggerTouch(XRNode node)
	{
		switch (node)
		{
		case XRNode.LeftHand:
			return instance.leftControllerIndexTouch;
		case XRNode.RightHand:
			return instance.rightControllerIndexTouch;
		default:
			return 0f;
		}
	}

	public static Vector3 DevicePosition(XRNode node)
	{
		switch (node)
		{
		case XRNode.Head:
			return instance.headPosition;
		case XRNode.LeftHand:
			return instance.leftControllerPosition;
		case XRNode.RightHand:
			return instance.rightControllerPosition;
		default:
			return Vector3.zero;
		}
	}

	public static Quaternion DeviceRotation(XRNode node)
	{
		switch (node)
		{
		case XRNode.Head:
			return instance.headRotation;
		case XRNode.LeftHand:
			return instance.leftControllerRotation;
		case XRNode.RightHand:
			return instance.rightControllerRotation;
		default:
			return Quaternion.identity;
		}
	}

	public static bool PositionValid(XRNode node)
	{
		switch (node)
		{
		case XRNode.Head:
			_ = instance.headDevice;
			return instance.headDevice.isValid;
		case XRNode.LeftHand:
			_ = instance.leftControllerDevice;
			return instance.leftControllerDevice.isValid;
		case XRNode.RightHand:
			_ = instance.rightControllerDevice;
			return instance.rightControllerDevice.isValid;
		default:
			return false;
		}
	}
}
