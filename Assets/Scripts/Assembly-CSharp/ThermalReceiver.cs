using UnityEngine;

public class ThermalReceiver : MonoBehaviour
{
	public float radius = 0.2f;

	[Tooltip("How fast the temperature should change overtime. 1.0 would be instantly.")]
	public float conductivity = 0.1f;

	public float celsius;

	public float Farenheit => celsius * 1.8f + 32f;

	protected void OnEnable()
	{
		ThermalManager.Register(this);
	}

	protected void OnDisable()
	{
		ThermalManager.Unregister(this);
	}
}
