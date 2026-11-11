using System.Collections;
using System.Collections.Generic;
using GorillaLocomotion;
using GorillaNetworking;
using Photon.Pun;
using UnityEngine;

public class GorillaFriendCollider : MonoBehaviour
{
	public List<string> playerIDsCurrentlyTouching = new List<string>();

	public CapsuleCollider thisCapsule;

	public string[] myAllowedMapsToJoin;

	public Collider[] overlapColliders = new Collider[20];

	private int tagAndBodyLayerMask;

	public void Awake()
	{
		thisCapsule = GetComponent<CapsuleCollider>();
		StartCoroutine(UpdatePlayersInSphere());
		tagAndBodyLayerMask = LayerMask.GetMask("Gorilla Tag Collider") | LayerMask.GetMask("Gorilla Body Collider");
	}

	private IEnumerator UpdatePlayersInSphere()
	{
		yield return new WaitForSeconds(1f);
		while (true)
		{
			playerIDsCurrentlyTouching.Clear();
			if (Physics.OverlapSphereNonAlloc(base.transform.position, thisCapsule.radius, overlapColliders, tagAndBodyLayerMask) > 0)
			{
				for (int i = 0; i < overlapColliders.Length; i++)
				{
					if (overlapColliders[i] != null)
					{
						if (overlapColliders[i].GetComponentInParent<PhotonView>() != null && !playerIDsCurrentlyTouching.Contains(overlapColliders[i].GetComponentInParent<PhotonView>().Owner.UserId))
						{
							playerIDsCurrentlyTouching.Add(overlapColliders[i].GetComponentInParent<PhotonView>().Owner.UserId);
						}
						else if ((bool)overlapColliders[i].GetComponentInParent<Player>() && !playerIDsCurrentlyTouching.Contains(PhotonNetwork.LocalPlayer.UserId))
						{
							playerIDsCurrentlyTouching.Add(PhotonNetwork.LocalPlayer.UserId);
						}
					}
				}
				if (playerIDsCurrentlyTouching.Contains(PhotonNetwork.LocalPlayer.UserId) && GorillaComputer.instance.friendJoinCollider != this)
				{
					GorillaComputer.instance.allowedMapsToJoin = myAllowedMapsToJoin;
					GorillaComputer.instance.friendJoinCollider = this;
					GorillaComputer.instance.UpdateScreen();
				}
			}
			yield return new WaitForSeconds(1f);
		}
	}
}
