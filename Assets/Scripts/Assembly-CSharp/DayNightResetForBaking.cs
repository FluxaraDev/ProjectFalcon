using UnityEngine;

public class DayNightResetForBaking : MonoBehaviour
{
	public BetterDayNightManager dayNightManager;

	public void SetMaterialsForBaking()
	{
		Material[] dayNightSupportedMaterials = dayNightManager.dayNightSupportedMaterials;
		for (int i = 0; i < dayNightSupportedMaterials.Length; i++)
		{
			dayNightSupportedMaterials[i].shader = dayNightManager.standard;
		}
		dayNightSupportedMaterials = dayNightManager.dayNightSupportedMaterialsCutout;
		for (int i = 0; i < dayNightSupportedMaterials.Length; i++)
		{
			dayNightSupportedMaterials[i].shader = dayNightManager.standardCutout;
		}
	}

	public void SetMaterialsForGame()
	{
		Material[] dayNightSupportedMaterials = dayNightManager.dayNightSupportedMaterials;
		for (int i = 0; i < dayNightSupportedMaterials.Length; i++)
		{
			dayNightSupportedMaterials[i].shader = dayNightManager.gorillaUnlit;
		}
		dayNightSupportedMaterials = dayNightManager.dayNightSupportedMaterialsCutout;
		for (int i = 0; i < dayNightSupportedMaterials.Length; i++)
		{
			dayNightSupportedMaterials[i].shader = dayNightManager.gorillaUnlitCutout;
		}
	}
}
