using GorillaNetworking;
using Photon.Pun;
using UnityEngine;

public class VRRigReliableState : MonoBehaviour, IPunObservable
{
	public int[] activeTransferrableObjectIndex;

	public TransferrableObject.PositionState[] transferrablePosStates;

	public TransferrableObject.ItemStates[] transferrableItemStates;

	public int extraSerializedState;

	public int lHandState;

	public int rHandState;

	private bool isOfflineVRRig;

	private BodyDockPositions bDock;

	public int sizeLayerMask = 1;

	public void SharedStart(bool isOfflineVRRig_, BodyDockPositions bDock_)
	{
		isOfflineVRRig = isOfflineVRRig_;
		bDock = bDock_;
		activeTransferrableObjectIndex = new int[CosmeticsController.maximumTransferrableItems];
		for (int i = 0; i < activeTransferrableObjectIndex.Length; i++)
		{
			activeTransferrableObjectIndex[i] = -1;
		}
		transferrablePosStates = new TransferrableObject.PositionState[CosmeticsController.maximumTransferrableItems];
		transferrableItemStates = new TransferrableObject.ItemStates[CosmeticsController.maximumTransferrableItems];
	}

	void IPunObservable.OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
	{
		if (isOfflineVRRig || info.Sender != info.photonView.Owner)
		{
			return;
		}
		if (stream.IsWriting)
		{
			for (int i = 0; i < activeTransferrableObjectIndex.Length; i++)
			{
				stream.SendNext(activeTransferrableObjectIndex[i]);
				stream.SendNext(transferrablePosStates[i]);
				stream.SendNext(transferrableItemStates[i]);
			}
			stream.SendNext(extraSerializedState);
			stream.SendNext(lHandState);
			stream.SendNext(rHandState);
			stream.SendNext(sizeLayerMask);
			return;
		}
		for (int j = 0; j < activeTransferrableObjectIndex.Length; j++)
		{
			activeTransferrableObjectIndex[j] = (int)stream.ReceiveNext();
			transferrablePosStates[j] = (TransferrableObject.PositionState)stream.ReceiveNext();
			transferrableItemStates[j] = (TransferrableObject.ItemStates)stream.ReceiveNext();
		}
		extraSerializedState = (int)stream.ReceiveNext();
		lHandState = (int)stream.ReceiveNext();
		rHandState = (int)stream.ReceiveNext();
		sizeLayerMask = (int)stream.ReceiveNext();
		bDock.RefreshTransferrableItems();
	}
}
