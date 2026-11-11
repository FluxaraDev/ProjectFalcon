using UnityEngine;
using UnityEngine.XR;

public class AutomaticAdjustIPD : MonoBehaviour
{
	public InputDevice headset;

	public float currentIPD;

	public float IPDDiffMin;

	public float overrideIPD;

	public Vector3 leftEyePosition;

	public Vector3 rightEyePosition;

	public bool testOverride;

	public Transform[] adjustXScaleObjects;

	public float sizeAt58mm = 1f;

	public float sizeAt63mm = 1.12f;

	private void Update()
	{
		_ = headset;
		if (!headset.isValid)
		{
			headset = InputDevices.GetDeviceAtXRNode(XRNode.Head);
		}
		if (headset.isValid && headset.TryGetFeatureValue(CommonUsages.leftEyePosition, out leftEyePosition) && headset.TryGetFeatureValue(CommonUsages.rightEyePosition, out rightEyePosition) && (rightEyePosition - leftEyePosition).magnitude > IPDDiffMin)
		{
			currentIPD = (rightEyePosition - leftEyePosition).magnitude;
			Transform[] array = adjustXScaleObjects;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].localScale = new Vector3(Mathf.LerpUnclamped(1f, 1.12f, (currentIPD - 0.058f) / 0.0050000027f), 1f, 1f);
			}
		}
	}
}
