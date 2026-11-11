using System;
using GorillaNetworking;
using UnityEngine;
using emotitron.Compression;

namespace GorillaTag
{
	public class DrinkableHoldable : TransferrableObject
	{
		[AssignInCorePrefab]
		public ContainerLiquid containerLiquid;

		[AssignInCorePrefab]
		[SoundBankInfo]
		public SoundBankPlayer sipSoundBankPlayer;

		[AssignInCorePrefab]
		public float sipRate = 0.1f;

		[AssignInCorePrefab]
		public float sipSoundCooldown = 0.5f;

		[AssignInCorePrefab]
		public Vector3 headToMouthOffset = new Vector3(0f, 0.0208f, 0.171f);

		[AssignInCorePrefab]
		public float sipRadius = 0.15f;

		private float lastTimeSipSoundPlayed;

		private bool wasSipping;

		private bool coolingDown;

		private bool wasCoolingDown;

		public override void OnEnable()
		{
			base.OnEnable();
			base.enabled = containerLiquid != null;
			itemState = (ItemStates)PackValues(sipSoundCooldown, containerLiquid.fillAmount, coolingDown);
		}

		protected override void LateUpdateLocal()
		{
			if (!containerLiquid.isActiveAndEnabled || !GorillaParent.hasInstance || !GorillaComputer.hasInstance)
			{
				base.LateUpdateLocal();
				return;
			}
			float num = (float)((GorillaComputer.instance.startupMillis + (long)Time.realtimeSinceStartup * 1000) % 259200000) / 1000f;
			if (Mathf.Abs(num - lastTimeSipSoundPlayed) > 129600f)
			{
				lastTimeSipSoundPlayed = num;
			}
			float num2 = sipRadius * sipRadius;
			bool flag = (GorillaTagger.Instance.offlineVRRig.head.rigTarget.transform.TransformPoint(headToMouthOffset) - containerLiquid.cupTopWorldPos).sqrMagnitude < num2;
			if (!flag)
			{
				foreach (VRRig vrrig in GorillaParent.instance.vrrigs)
				{
					if (flag || vrrig.head == null || vrrig.head.rigTarget == null)
					{
						break;
					}
					flag = (vrrig.head.rigTarget.transform.TransformPoint(headToMouthOffset) - containerLiquid.cupTopWorldPos).sqrMagnitude < num2;
				}
			}
			if (flag)
			{
				containerLiquid.fillAmount = Mathf.Clamp01(containerLiquid.fillAmount - sipRate * Time.deltaTime);
				if (num > lastTimeSipSoundPlayed + sipSoundCooldown)
				{
					if (!wasSipping)
					{
						lastTimeSipSoundPlayed = num;
						coolingDown = true;
					}
				}
				else
				{
					coolingDown = false;
				}
			}
			wasSipping = flag;
			itemState = (ItemStates)PackValues(lastTimeSipSoundPlayed, containerLiquid.fillAmount, coolingDown);
			base.LateUpdateLocal();
		}

		protected override void LateUpdateReplicated()
		{
			base.LateUpdateReplicated();
			int packed = (int)itemState;
			UnpackValues(in packed, out lastTimeSipSoundPlayed, out containerLiquid.fillAmount, out coolingDown);
		}

		protected override void LateUpdateShared()
		{
			base.LateUpdateShared();
			if (coolingDown && !wasCoolingDown)
			{
				sipSoundBankPlayer.Play();
			}
			wasCoolingDown = coolingDown;
		}

		private static int PackValues(float cooldownStartTime, float fillAmount, bool coolingDown)
		{
			byte[] array = new byte[32];
			int bitposition = 0;
			array.WriteBool(coolingDown, ref bitposition);
			array.Write((ulong)((double)cooldownStartTime * 100.0), ref bitposition, 25);
			array.Write((ulong)((double)fillAmount * 63.0), ref bitposition, 6);
			return BitConverter.ToInt32(array, 0);
		}

		private static void UnpackValues(in int packed, out float cooldownStartTime, out float fillAmount, out bool coolingDown)
		{
			byte[] bytes = BitConverter.GetBytes(packed);
			int bitposition = 0;
			coolingDown = bytes.ReadBool(ref bitposition);
			cooldownStartTime = (float)((double)bytes.Read(ref bitposition, 25) / 100.0);
			fillAmount = (float)bytes.Read(ref bitposition, 6) / 63f;
		}
	}
}
