using System.Collections.Generic;
using GorillaLocomotion;
using GorillaNetworking;
using UnityEngine;

public class SnowballMaker : MonoBehaviour
{
	public bool isLeftHand;

	public List<int> matDataIndexes = new List<int>();

	public SnowballThrowable snowball;

	public GorillaVelocityEstimator velocityEstimator;

	protected void Awake()
	{
	}

	protected void LateUpdate()
	{
		if (snowball.isActiveAndEnabled || !Player.hasInstance || !EquipmentInteractor.hasInstance || !Player.hasInstance || !GorillaTagger.hasInstance || GorillaTagger.Instance.offlineVRRig == null)
		{
			return;
		}
		Player instance = Player.Instance;
		EquipmentInteractor instance2 = EquipmentInteractor.instance;
		_ = GorillaTagger.Instance.offlineVRRig;
		_ = CosmeticsController.instance;
		Transform transform = base.transform;
		Transform transform2 = snowball.transform;
		_ = snowball.gameObject;
		bool num = (isLeftHand ? instance2.isLeftGrabbing : instance2.isRightGrabbing);
		bool flag = (isLeftHand ? instance2.leftHandHeldEquipment : instance2.rightHandHeldEquipment) != null;
		if (num && !flag)
		{
			int item = (isLeftHand ? instance.leftHandMaterialTouchIndex : instance.rightHandMaterialTouchIndex);
			if (matDataIndexes.Contains(item))
			{
				snowball.EnableSnowball(enable: true);
				snowball.velocityEstimator = velocityEstimator;
				transform2.position = transform.position;
				transform2.rotation = transform.rotation;
			}
		}
	}
}
