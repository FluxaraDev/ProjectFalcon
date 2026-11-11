using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
	public enum Level
	{
		City = 0,
		Forest = 1,
		Canyon = 2,
		Cave = 3,
		Mountain = 4,
		SkyJungle = 5
	}

	public Level level;

	public GorillaGeoHideShowTrigger geoTrigger;

	public float startSize = 1f;
}
