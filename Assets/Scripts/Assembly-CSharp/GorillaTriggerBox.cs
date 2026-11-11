using System.Collections;
using UnityEngine;

public class GorillaTriggerBox : MonoBehaviour
{
	public bool triggerBoxOnce;

	private void OnEnable()
	{
		if (Application.isEditor)
		{
			StartCoroutine(TestTrigger());
		}
	}

	private void OnDisable()
	{
		if (Application.isEditor)
		{
			StopAllCoroutines();
		}
	}

	private IEnumerator TestTrigger()
	{
		while (true)
		{
			if (triggerBoxOnce)
			{
				triggerBoxOnce = false;
				OnBoxTriggered();
			}
			yield return new WaitForSeconds(0.1f);
		}
	}

	public virtual void OnBoxTriggered()
	{
	}
}
