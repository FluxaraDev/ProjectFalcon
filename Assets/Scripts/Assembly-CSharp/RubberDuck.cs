using GorillaTag;
using Photon.Pun;
using UnityEngine;

public class RubberDuck : TransferrableObject
{
	[DebugOption]
	public bool disableActivation;

	[DebugOption]
	public bool disableDeactivation;

	private SkinnedMeshRenderer skinRenderer;

	public float duckieLerp;

	private int tempHandPos;

	[GorillaSoundLookup]
	public int squeezeSound = 75;

	[GorillaSoundLookup]
	public int squeezeReleaseSound = 76;

	public float squeezeStrength = 0.05f;

	public float releaseStrength = 0.03f;

	public ParticleSystem particleFX;

	[Tooltip("The emission rate of the particle effect when not squeezed.")]
	public float particleFXEmissionIdle = 0.8f;

	[Tooltip("The emission rate of the particle effect when squeezed.")]
	public float particleFXEmissionSqueeze = 10f;

	[Tooltip("The animation of the particle effect returning to the idle emission rate. X axis is time, Y axis is the emission lerp value where 0 is idle, 1 is squeezed.")]
	public AnimationCurve particleFXEmissionCooldownCurve;

	private ParticleSystem.EmissionModule pFXEmissionModule;

	private bool hasParticleFX;

	private float squeezeTimeElapsed;

	protected override void Awake()
	{
		base.Awake();
		skinRenderer = GetComponent<SkinnedMeshRenderer>();
		myThreshold = 0.7f;
		hysterisis = 0.3f;
		hasParticleFX = particleFX != null;
		if (hasParticleFX)
		{
			pFXEmissionModule = particleFX.emission;
			pFXEmissionModule.rateOverTime = particleFXEmissionIdle;
		}
	}

	public override void LateUpdate()
	{
		base.LateUpdate();
		float num = 0f;
		if (InHand())
		{
			tempHandPos = ((myOnlineRig != null) ? myOnlineRig.ReturnHandPosition() : myRig.ReturnHandPosition());
			num = ((currentState != PositionState.InLeftHand) ? ((float)Mathf.FloorToInt((float)(tempHandPos % 10) / 1f)) : ((float)Mathf.FloorToInt((float)(tempHandPos % 10000) / 1000f)));
		}
		skinRenderer.SetBlendShapeWeight(0, Mathf.Lerp(skinRenderer.GetBlendShapeWeight(0), num * 11.1f, duckieLerp));
		if (hasParticleFX)
		{
			squeezeTimeElapsed += Time.deltaTime;
			pFXEmissionModule.rateOverTime = Mathf.Lerp(particleFXEmissionIdle, particleFXEmissionSqueeze, particleFXEmissionCooldownCurve.Evaluate(squeezeTimeElapsed));
		}
	}

	public override void OnActivate()
	{
		base.OnActivate();
		if (IsMyItem())
		{
			bool flag = currentState == PositionState.InLeftHand;
			if (PhotonNetwork.InRoom && GorillaGameManager.instance != null)
			{
				GorillaGameManager.instance.FindVRRigForPlayer(PhotonNetwork.LocalPlayer).RPC("PlayHandTap", RpcTarget.Others, squeezeSound, flag, 0.33f);
			}
			myRig.PlayHandTapLocal(squeezeSound, flag, 0.33f);
			GorillaTagger.Instance.StartVibration(flag, squeezeStrength, Time.deltaTime);
		}
		if (hasParticleFX)
		{
			squeezeTimeElapsed = 0f;
			pFXEmissionModule.rateOverTime = particleFXEmissionSqueeze;
		}
	}

	public override void OnDeactivate()
	{
		base.OnDeactivate();
		if (IsMyItem())
		{
			bool flag = currentState == PositionState.InLeftHand;
			if (PhotonNetwork.InRoom && GorillaGameManager.instance != null)
			{
				GorillaGameManager.instance.FindVRRigForPlayer(PhotonNetwork.LocalPlayer).RPC("PlayHandTap", RpcTarget.Others, squeezeReleaseSound, flag, 0.33f);
			}
			myRig.PlayHandTapLocal(squeezeReleaseSound, flag, 0.33f);
			GorillaTagger.Instance.StartVibration(flag, releaseStrength, Time.deltaTime);
		}
		if (hasParticleFX)
		{
			squeezeTimeElapsed = 0f;
			pFXEmissionModule.rateOverTime = particleFXEmissionIdle;
		}
	}

	public override bool CanActivate()
	{
		return !disableActivation;
	}

	public override bool CanDeactivate()
	{
		return !disableDeactivation;
	}
}
