using Photon.Pun;
using UnityEngine;

public class SnowballThrowable : HoldableObject
{
	public GameObject projectilePrefab;

	public GorillaVelocityEstimator velocityEstimator;

	public SoundBankPlayer launchSoundBankPlayer;

	public VRRig myRig;

	public VRRig myOnlineRig;

	private VRRig targetRig;

	public float linSpeedMultiplier = 1f;

	public float maxLinSpeed = 12f;

	public float maxWristSpeed = 4f;

	public bool isLeftHanded;

	public bool IsMine()
	{
		if (myRig != null)
		{
			return myRig.isOfflineVRRig;
		}
		return false;
	}

	public override void OnEnable()
	{
		base.OnEnable();
		if (myRig == null && myOnlineRig != null && myOnlineRig.photonView != null && myOnlineRig.photonView.IsMine)
		{
			base.gameObject.SetActive(value: false);
			return;
		}
		if (myRig == null && myOnlineRig == null)
		{
			base.gameObject.SetActive(value: false);
			return;
		}
		targetRig = ((myRig != null) ? myRig : myOnlineRig);
		AnchorToHand();
	}

	public void EnableSnowball(bool enable)
	{
		if (isLeftHanded)
		{
			myRig.LeftHandState = (enable ? 1 : 0);
		}
		else
		{
			myRig.RightHandState = (enable ? 1 : 0);
		}
		base.gameObject.SetActive(enable);
		EquipmentInteractor.instance.UpdateHandEquipment(enable ? this : null, isLeftHanded);
	}

	protected void LateUpdateLocal()
	{
	}

	protected void LateUpdateReplicated()
	{
	}

	protected void LateUpdateShared()
	{
	}

	private Transform Anchor()
	{
		return base.transform.parent;
	}

	private void AnchorToHand()
	{
		BodyDockPositions component = targetRig.GetComponent<BodyDockPositions>();
		Transform transform = Anchor();
		if (isLeftHanded)
		{
			transform.parent = component.leftHandTransform;
		}
		else
		{
			transform.parent = component.rightHandTransform;
		}
		transform.localPosition = Vector3.zero;
		transform.localRotation = Quaternion.identity;
	}

	protected void LateUpdate()
	{
		if (IsMine())
		{
			LateUpdateLocal();
		}
		else
		{
			LateUpdateReplicated();
		}
		LateUpdateShared();
	}

	public override void OnRelease(DropZone zoneReleased, GameObject releasingHand)
	{
		base.OnRelease(zoneReleased, releasingHand);
		if ((!(releasingHand == EquipmentInteractor.instance.rightHand) || !isLeftHanded) && (!(releasingHand == EquipmentInteractor.instance.leftHand) || isLeftHanded))
		{
			LaunchSnowball();
		}
	}

	private void LaunchSnowball()
	{
		GameObject gameObject = ObjectPools.instance.Instantiate(projectilePrefab);
		SlingshotProjectile component = gameObject.GetComponent<SlingshotProjectile>();
		Transform obj = base.transform;
		Vector3 position = obj.position;
		float x = obj.lossyScale.x;
		Vector3 linearVelocity = velocityEstimator.linearVelocity;
		_ = velocityEstimator.angularVelocity;
		_ = velocityEstimator.handPos;
		Vector3 zero = Vector3.zero;
		float magnitude = linearVelocity.magnitude;
		float magnitude2 = zero.magnitude;
		if (magnitude > 0.001f)
		{
			float num = Mathf.Clamp(magnitude * linSpeedMultiplier, 0f, maxLinSpeed);
			linearVelocity *= num / magnitude;
		}
		if (magnitude2 > 0.001f)
		{
			float num2 = Mathf.Clamp(magnitude2, 0f, maxWristSpeed);
			zero *= num2 / magnitude2;
		}
		Vector3 vector = Vector3.zero;
		Rigidbody component2 = GorillaTagger.Instance.GetComponent<Rigidbody>();
		if (component2 != null)
		{
			vector = component2.velocity;
		}
		Vector3 vector2 = linearVelocity + zero + vector;
		if (GorillaGameManager.instance != null)
		{
			int num3 = GorillaGameManager.instance.IncrementLocalPlayerProjectileCount();
			component.Launch(base.transform.position, vector2, PhotonNetwork.LocalPlayer, blueTeam: false, orangeTeam: false, num3, x);
			GorillaGameManager.instance.photonView.RPC("LaunchSlingshotProjectile", RpcTarget.Others, position, vector2, PoolUtils.GameObjHashCode(gameObject), -1, isLeftHanded, num3);
		}
		else
		{
			component.Launch(position, vector2, PhotonNetwork.LocalPlayer, blueTeam: false, orangeTeam: false, 0, x);
		}
		launchSoundBankPlayer.Play();
		EnableSnowball(enable: false);
	}
}
