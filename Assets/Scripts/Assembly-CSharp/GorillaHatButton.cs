using UnityEngine;
using UnityEngine.UI;

public class GorillaHatButton : MonoBehaviour
{
	public enum HatButtonType
	{
		Hat = 0,
		Face = 1,
		Badge = 2
	}

	public GorillaHatButtonParent buttonParent;

	public HatButtonType buttonType;

	public bool isOn;

	public Material offMaterial;

	public Material onMaterial;

	public string offText;

	public string onText;

	public Text myText;

	public float debounceTime = 0.25f;

	public float touchTime;

	public string cosmeticName;

	public bool testPress;

	public void Update()
	{
		if (testPress)
		{
			testPress = false;
			if (touchTime + debounceTime < Time.time)
			{
				touchTime = Time.time;
				isOn = !isOn;
				buttonParent.PressButton(isOn, buttonType, cosmeticName);
			}
		}
	}

	private void OnTriggerEnter(Collider collider)
	{
		if (!(touchTime + debounceTime < Time.time))
		{
			return;
		}
		touchTime = Time.time;
		Debug.Log("collision detected" + collider, collider);
		if (collider.GetComponentInParent<GorillaTriggerColliderHandIndicator>() != null)
		{
			GorillaTriggerColliderHandIndicator component = collider.GetComponent<GorillaTriggerColliderHandIndicator>();
			Debug.Log("buttan press");
			isOn = !isOn;
			buttonParent.PressButton(isOn, buttonType, cosmeticName);
			if (component != null)
			{
				GorillaTagger.Instance.StartVibration(component.isLeftHand, GorillaTagger.Instance.tapHapticStrength / 2f, GorillaTagger.Instance.tapHapticDuration);
			}
		}
	}

	public void UpdateColor()
	{
		if (isOn)
		{
			GetComponent<MeshRenderer>().material = onMaterial;
			myText.text = onText;
		}
		else
		{
			GetComponent<MeshRenderer>().material = offMaterial;
			myText.text = offText;
		}
	}
}
