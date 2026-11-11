using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class GorillaParent : MonoBehaviourPunCallbacks
{
	public GameObject tagUI;

	public GameObject playerParent;

	public GameObject vrrigParent;

	public static volatile GorillaParent instance;

	public static bool hasInstance;

	public List<VRRig> vrrigs;

	public Dictionary<Player, VRRig> vrrigDict = new Dictionary<Player, VRRig>();

	private int i;

	private PhotonView[] childPhotonViews;

	private bool joinedRoom;

	public void Awake()
	{
		if (instance == null)
		{
			instance = this;
			hasInstance = true;
		}
		else if (instance != this)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		StartCoroutine(OccasionalUpdate());
	}

	protected void OnDestroy()
	{
		if (instance == this)
		{
			hasInstance = false;
			instance = null;
		}
	}

	public void LateUpdate()
	{
		for (i = vrrigs.Count - 1; i > -1; i--)
		{
			if (vrrigs[i] == null)
			{
				vrrigs.RemoveAt(i);
			}
		}
		if (PhotonNetwork.InRoom && joinedRoom)
		{
			if (!vrrigDict.ContainsKey(PhotonNetwork.LocalPlayer) || vrrigDict[PhotonNetwork.LocalPlayer] == null)
			{
				PhotonNetwork.Instantiate("GorillaPrefabs/Gorilla Player Networked", Vector3.zero, Quaternion.identity, 0);
			}
		}
		else if (!PhotonNetwork.InRoom && joinedRoom)
		{
			joinedRoom = false;
		}
	}

	private IEnumerator OccasionalUpdate()
	{
		while (true)
		{
			try
			{
				if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
				{
					childPhotonViews = vrrigParent.GetPhotonViewsInChildren();
					if (childPhotonViews.Length > 10)
					{
						for (int num = childPhotonViews.Length - 1; num > -1; num--)
						{
							if ((bool)childPhotonViews[num].GetComponent<VRRig>() && childPhotonViews[num].IsRoomView && childPhotonViews[num].IsMine)
							{
								PhotonNetwork.Destroy(childPhotonViews[num].gameObject);
							}
						}
					}
				}
			}
			catch
			{
			}
			yield return new WaitForSeconds(5f);
		}
	}

	public override void OnJoinedRoom()
	{
		joinedRoom = true;
	}

	public override void OnLeftRoom()
	{
		joinedRoom = false;
	}
}
