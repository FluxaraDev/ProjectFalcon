using UnityEngine;

public class CosmeticAnchors : MonoBehaviour
{
	[SerializeField]
	protected GameObject nameAnchor;

	[SerializeField]
	protected GameObject leftArmAnchor;

	[SerializeField]
	protected GameObject rightArmAnchor;

	[SerializeField]
	protected GameObject chestAnchor;

	private VRRig vrRig;

	private VRRigAnchorOverrides anchorOverrides;

	protected void Awake()
	{
		vrRig = GetComponentInParent<VRRig>();
		if (vrRig != null)
		{
			anchorOverrides = vrRig.gameObject.GetComponent<VRRigAnchorOverrides>();
		}
	}

	public void EnableAnchor(bool enable)
	{
		if (!(anchorOverrides == null))
		{
			if ((bool)leftArmAnchor)
			{
				anchorOverrides.OverrideAnchor(TransferrableObject.PositionState.OnLeftArm, enable ? leftArmAnchor.transform : null);
			}
			if ((bool)rightArmAnchor)
			{
				anchorOverrides.OverrideAnchor(TransferrableObject.PositionState.OnRightArm, enable ? rightArmAnchor.transform : null);
			}
			if ((bool)chestAnchor)
			{
				anchorOverrides.OverrideAnchor(TransferrableObject.PositionState.OnChest, enable ? chestAnchor.transform : null);
			}
			if ((bool)nameAnchor)
			{
				Transform nameTransform = anchorOverrides.NameTransform;
				nameTransform.parent = (enable ? nameAnchor.transform : anchorOverrides.NameDefaultAnchor);
				nameTransform.transform.localPosition = Vector3.zero;
				nameTransform.transform.localRotation = Quaternion.identity;
			}
		}
	}

	public Transform GetPositionAnchor(TransferrableObject.PositionState pos)
	{
		switch (pos)
		{
		case TransferrableObject.PositionState.OnLeftArm:
			if (!leftArmAnchor)
			{
				return null;
			}
			return leftArmAnchor.transform;
		case TransferrableObject.PositionState.OnRightArm:
			if (!rightArmAnchor)
			{
				return null;
			}
			return rightArmAnchor.transform;
		case TransferrableObject.PositionState.OnChest:
			if (!chestAnchor)
			{
				return null;
			}
			return chestAnchor.transform;
		default:
			return null;
		}
	}

	public Transform GetNameAnchor()
	{
		if (!nameAnchor)
		{
			return null;
		}
		return nameAnchor.transform;
	}
}
