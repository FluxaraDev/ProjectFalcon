using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

internal class RequestableOwnershipGaurdHandler : IPunOwnershipCallbacks
{
	private static HashSet<PhotonView> gaurdedViews;

	private static readonly RequestableOwnershipGaurdHandler callbackInstance;

	private static Dictionary<PhotonView, RequestableOwnershipGuard> guardingLookup;

	static RequestableOwnershipGaurdHandler()
	{
		gaurdedViews = new HashSet<PhotonView>();
		callbackInstance = new RequestableOwnershipGaurdHandler();
		guardingLookup = new Dictionary<PhotonView, RequestableOwnershipGuard>();
		PhotonNetwork.AddCallbackTarget(callbackInstance);
	}

	internal static void RegisterView(PhotonView view, RequestableOwnershipGuard guard)
	{
		if (!(view == null) && !gaurdedViews.Contains(view))
		{
			gaurdedViews.Add(view);
			guardingLookup.Add(view, guard);
		}
	}

	internal static void RemoveView(PhotonView view)
	{
		if (!(view == null))
		{
			gaurdedViews.Remove(view);
			guardingLookup.Remove(view);
		}
	}

	void IPunOwnershipCallbacks.OnOwnershipTransfered(PhotonView targetView, Player previousOwner)
	{
		if (!gaurdedViews.Contains(targetView))
		{
			return;
		}
		RequestableOwnershipGuard value;
		if (!targetView.IsRoomView)
		{
			Debug.LogError("Requestable Ownership Guard does not work for non room views");
		}
		else if (guardingLookup.TryGetValue(targetView, out value))
		{
			if (previousOwner != value.currentOwner)
			{
				Debug.LogError("Ownership transferred but the previous owner didn't initiate the request, Switching back");
				targetView.OwnerActorNr = value.currentOwner.ActorNumber;
				targetView.ControllerActorNr = value.currentOwner.ActorNumber;
			}
			value.currentOwner = targetView.Owner;
		}
	}

	void IPunOwnershipCallbacks.OnOwnershipRequest(PhotonView targetView, Player requestingPlayer)
	{
	}

	void IPunOwnershipCallbacks.OnOwnershipTransferFailed(PhotonView targetView, Player senderOfFailedRequest)
	{
	}
}
