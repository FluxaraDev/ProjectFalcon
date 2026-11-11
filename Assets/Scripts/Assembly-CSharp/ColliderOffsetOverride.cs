using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ColliderOffsetOverride : MonoBehaviour
{
	public List<Collider> colliders;

	[HideInInspector]
	public bool autoSearch;

	public float targetScale = 1f;

	private void Awake()
	{
		if (autoSearch)
		{
			FindColliders();
		}
		foreach (Collider collider in colliders)
		{
			if (collider != null)
			{
				collider.contactOffset = 0.01f * targetScale;
			}
		}
	}

	public void FindColliders()
	{
		foreach (Collider item in Enumerable.ToList(base.gameObject.GetComponents<Collider>()))
		{
			if (!colliders.Contains(item))
			{
				colliders.Add(item);
			}
		}
	}

	public void FindCollidersRecursively()
	{
		foreach (Collider item in Enumerable.ToList(base.gameObject.GetComponentsInChildren<Collider>()))
		{
			if (!colliders.Contains(item))
			{
				colliders.Add(item);
			}
		}
	}

	private void AutoDisabled()
	{
		autoSearch = true;
	}

	private void AutoEnabled()
	{
		autoSearch = false;
	}
}
