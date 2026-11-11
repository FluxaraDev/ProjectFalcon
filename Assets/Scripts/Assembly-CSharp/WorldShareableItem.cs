using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class WorldShareableItem : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback, IOnPhotonViewOwnerChange, IPhotonViewCallback, IPunObservable
{
	public delegate void Delegate();

	public delegate void OnOwnerChangeDelegate(Player newOwner, Player prevOwner);

	private bool validShareable = true;

	private PhotonView view;

	private Transform targetXf;

	private WorldTargetItem prevTarget = new WorldTargetItem();

	private WorldTargetItem target = new WorldTargetItem();

	public Delegate rpcCallBack;

	public OnOwnerChangeDelegate onOwnerChangeCb;

	private bool enableRemoteSync = true;

	public WorldTargetItem Target => target;

	public bool EnableRemoteSync
	{
		get
		{
			return enableRemoteSync;
		}
		set
		{
			enableRemoteSync = value;
		}
	}

	private void Start()
	{
		view = GetComponent<PhotonView>();
	}

	public override void OnEnable()
	{
		base.OnEnable();
		target.Invalidate();
	}

	public override void OnDisable()
	{
		base.OnDisable();
		target.Invalidate();
	}

	public override void OnLeftRoom()
	{
		base.OnLeftRoom();
		rpcCallBack = null;
		onOwnerChangeCb = null;
		target.Invalidate();
	}

	public void OnPhotonInstantiate(PhotonMessageInfo info)
	{
		object[] instantiationData = info.photonView.InstantiationData;
		if (instantiationData != null && instantiationData.Length >= 2)
		{
			target.itemIdx = (int)instantiationData[0];
			target.owner = (Player)instantiationData[1];
		}
		SyncToTarget();
		if (!validShareable && info.Sender != null)
		{
			GorillaNot.instance.SendReport("invalid world shareable", info.Sender.UserId, info.Sender.NickName);
		}
		PhotonView.Get(this).AddCallbackTarget(this);
	}

	void IOnPhotonViewOwnerChange.OnOwnerChange(Player newOwner, Player previousOwner)
	{
		if (onOwnerChangeCb != null)
		{
			onOwnerChangeCb(newOwner, previousOwner);
		}
	}

	private void Update()
	{
		if (target.IsValid())
		{
			if (view.IsMine)
			{
				base.transform.position = targetXf.transform.position;
				base.transform.rotation = targetXf.transform.rotation;
			}
			else if (targetXf != null && EnableRemoteSync)
			{
				targetXf.position = base.transform.position;
				targetXf.rotation = base.transform.rotation;
			}
		}
	}

	private bool ReadTargetFromStream(PhotonStream stream)
	{
		prevTarget.owner = target.owner;
		prevTarget.itemIdx = target.itemIdx;
		target.owner = (Player)stream.ReceiveNext();
		target.itemIdx = (int)stream.ReceiveNext();
		if (target.owner == prevTarget.owner)
		{
			return target.itemIdx == prevTarget.itemIdx;
		}
		return false;
	}

	private void SyncToTarget()
	{
		VRRig vRRig;
		if (target.owner == PhotonNetwork.LocalPlayer)
		{
			vRRig = GorillaTagger.Instance.offlineVRRig;
		}
		else
		{
			PhotonView photonView = GorillaGameManager.instance.FindVRRigForPlayer(target.owner);
			if (photonView == null)
			{
				target.Invalidate();
				return;
			}
			vRRig = photonView.gameObject.GetComponent<VRRig>();
		}
		if (vRRig == null)
		{
			target.Invalidate();
			return;
		}
		TransferrableObject transferrableObject = vRRig.myBodyDockPositions.TransferrableItem(target.itemIdx);
		targetXf = transferrableObject.gameObject.GetComponent<Transform>();
		validShareable = transferrableObject.canDrop || transferrableObject.shareable;
		if (!validShareable)
		{
			base.gameObject.SetActive(value: false);
		}
		else
		{
			transferrableObject.SetWorldShareableItem(base.gameObject);
		}
	}

	void IPunObservable.OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
	{
	}

	[PunRPC]
	private void RPCWorldShareable(PhotonMessageInfo info)
	{
		GorillaNot.IncrementRPCCall(info, "RPCWorldShareable");
		rpcCallBack();
	}
}
