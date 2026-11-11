using Photon.Pun;
using UnityEngine;

public class MonkeyeAI_ReplState : MonoBehaviour, IPunObservable
{
	public enum EStates
	{
		Sleeping = 0,
		Patrolling = 1,
		Chasing = 2,
		ReturnToSleepPt = 3,
		GoToSleep = 4,
		BeginAttack = 5,
		OpenFloor = 6,
		DropPlayer = 7,
		CloseFloor = 8
	}

	public EStates state;

	public string userId;

	public Vector3 attackPos;

	public float timer;

	public bool floorEnabled;

	public bool portalEnabled;

	public bool freezePlayer;

	public float alpha;

	void IPunObservable.OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
	{
		if (stream.IsWriting)
		{
			stream.SendNext(userId);
			stream.SendNext(attackPos);
			stream.SendNext(timer);
			stream.SendNext(floorEnabled);
			stream.SendNext(portalEnabled);
			stream.SendNext(freezePlayer);
			stream.SendNext(alpha);
			stream.SendNext(state);
		}
		else if (info.Sender.ActorNumber == info.photonView.Owner.ActorNumber)
		{
			userId = (string)stream.ReceiveNext();
			attackPos = (Vector3)stream.ReceiveNext();
			timer = (float)stream.ReceiveNext();
			floorEnabled = (bool)stream.ReceiveNext();
			portalEnabled = (bool)stream.ReceiveNext();
			freezePlayer = (bool)stream.ReceiveNext();
			alpha = (float)stream.ReceiveNext();
			state = (EStates)stream.ReceiveNext();
		}
	}
}
