using UnityEngine;

public class FlagForLighting : MonoBehaviour
{
	public enum TimeOfDay
	{
		Sunrise = 0,
		TenAM = 1,
		Noon = 2,
		ThreePM = 3,
		Sunset = 4,
		Night = 5,
		RainingDay = 6,
		RainingNight = 7,
		None = 8
	}

	public TimeOfDay myTimeOfDay;
}
