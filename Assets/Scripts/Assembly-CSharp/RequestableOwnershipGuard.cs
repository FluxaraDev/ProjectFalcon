using System;
using JetBrains.Annotations;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class RequestableOwnershipGuard : MonoBehaviourPunCallbacks
{
	[SerializeField]
	private new PhotonView photonView;

	[SerializeField]
	private bool autoRegister = true;

	[CanBeNull]
	public Player currentOwner;

	private void BindPhotonViews()
	{
		photonView = GetComponent<PhotonView>();
	}

	private void Start()
	{
		if (autoRegister)
		{
			BindPhotonViews();
		}
		if (!(photonView == null))
		{
			RequestableOwnershipGaurdHandler.RegisterView(photonView, this);
		}
	}

	public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
	{
		throw new NotImplementedException();
	}

	public override void OnPlayerEnteredRoom(Player newPlayer)
	{
		if (!PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
		{
			photonView.RPC("AdminOnlySetOwner", newPlayer, currentOwner);
		}
	}

	public override void OnJoinedRoom()
	{
		if (PhotonNetwork.IsMasterClient)
		{
			SetOwnership(PhotonNetwork.MasterClient);
		}
		else
		{
			SetOwnership(null);
		}
	}

	public override void OnPlayerLeftRoom(Player otherPlayer)
	{
		SetOwnership(PhotonNetwork.MasterClient);
	}

	[PunRPC]
	public void AdminOnlySetOwner(Player player, PhotonMessageInfo info)
	{
		if (!info.Sender.IsMasterClient)
		{
			Debug.LogError("Got an admin only ownership request, but the sender wasn't the master client for some reason");
		}
		else if (info.Sender.ActorNumber != PhotonNetwork.MasterClient.ActorNumber)
		{
			Debug.LogError("Got an admin only ownership request, The client sending has the masterClient flag but their actor number says otherwise");
		}
		else if (currentOwner != null)
		{
			Debug.LogError("Got an admin only ownership request, But the current owner is still set for some reason");
		}
		else
		{
			SetOwnership(player);
		}
	}

	public void TransferOwnership(Player player)
	{
		if (!photonView.IsMine)
		{
			Debug.LogError("Tried to transfer ownership when im not the owner");
			return;
		}
		SetOwnership(player);
		photonView.RPC("TransferOwnershipFromToRPC", RpcTarget.Others, player);
	}

	[PunRPC]
	public void TransferOwnershipFromToRPC(Player player, PhotonMessageInfo info)
	{
		if (photonView.OwnerActorNr != info.Sender.ActorNumber)
		{
			Debug.LogError("Got transfer request but the sender does not match the current owner");
		}
		else if (currentOwner == null)
		{
			Debug.LogError("Tried to transfer ownership when the currentOwner is null. This happens if we just loaded in and haven't been told the current owner by the master client yet.");
		}
		else if (currentOwner.ActorNumber != photonView.OwnerActorNr)
		{
			Debug.LogError("The stored actor number does not match the current actor number. Possible Attacker attempt or desync?");
		}
		else if (currentOwner.ActorNumber == player.ActorNumber)
		{
			Debug.LogError("Tried to set the new owner to ourselves? This shouldn't happen.");
		}
		else
		{
			SetOwnership(player);
		}
	}

	private void SetOwnership(Player player)
	{
		if (player == null)
		{
			currentOwner = null;
			photonView.OwnerActorNr = -1;
			photonView.ControllerActorNr = -1;
		}
		else
		{
			currentOwner = player;
			photonView.OwnerActorNr = player.ActorNumber;
			photonView.ControllerActorNr = player.ActorNumber;
		}
	}
}
