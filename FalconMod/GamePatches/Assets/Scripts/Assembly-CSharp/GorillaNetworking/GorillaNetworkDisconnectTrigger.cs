using Photon.Pun;
using UnityEngine;

namespace GorillaNetworking
{
	public class GorillaNetworkDisconnectTrigger : GorillaTriggerBox
	{
		public PhotonNetworkController photonNetworkController;

		public GameObject offlineVRRig;

		public GameObject makeSureThisIsEnabled;

		public GameObject[] makeSureTheseAreEnabled;

		public string componentTypeToRemove;

		public GameObject componentTarget;

		public override void OnBoxTriggered()
		{
			base.OnBoxTriggered();
			if (makeSureThisIsEnabled != null)
			{
				makeSureThisIsEnabled.SetActive(value: true);
			}
			GameObject[] array = makeSureTheseAreEnabled;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].SetActive(value: true);
			}
			// Project Falcon change (2026-11-12): falling out of the map
			// no longer disconnects ("kicks") the player. They are
			// teleported back to the normal spawn in the stump room
			// instead. The FalconVoidGuard covers the whole map as well;
			// this trigger now behaves the same way wherever it is placed.
			GorillaLocomotion.Player player = GorillaLocomotion.Player.Instance;
			if (player != null)
			{
				player.transform.position = FalconVoidGuard.SpawnPosition;
				Rigidbody rb = player.GetComponent<Rigidbody>();
				if (rb != null)
				{
					rb.position = FalconVoidGuard.SpawnPosition;
					rb.velocity = Vector3.zero;
					rb.angularVelocity = Vector3.zero;
				}
			}
		}
	}
}
