using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class MazePlayerCollection : MonoBehaviourPunCallbacks
{
	public List<VRRig> containedRigs = new List<VRRig>();

	public List<MonkeyeAI> monkeyeAis = new List<MonkeyeAI>();

	public void OnTriggerEnter(Collider other)
	{
		if ((bool)other.GetComponent<SphereCollider>())
		{
			VRRig component = other.attachedRigidbody.gameObject.GetComponent<VRRig>();
			if (!(component == null) && !containedRigs.Contains(component))
			{
				containedRigs.Add(component);
			}
		}
	}

	public void OnTriggerExit(Collider other)
	{
		if ((bool)other.GetComponent<SphereCollider>())
		{
			VRRig component = other.attachedRigidbody.gameObject.GetComponent<VRRig>();
			if (!(component == null) && containedRigs.Contains(component))
			{
				containedRigs.Remove(component);
			}
		}
	}

	public override void OnPlayerLeftRoom(Player otherPlayer)
	{
		for (int num = containedRigs.Count - 1; num >= 0; num--)
		{
			if (containedRigs[num] == null)
			{
				containedRigs.RemoveAt(num);
			}
		}
	}
}
