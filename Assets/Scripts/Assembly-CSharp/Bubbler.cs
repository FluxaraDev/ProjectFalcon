using System.Collections.Generic;
using UnityEngine;

public class Bubbler : TransferrableObject
{
	private enum BubblerState
	{
		None = 1,
		Bubbling = 2
	}

	public ParticleSystem bubbleParticleSystem;

	private ParticleSystem.Particle[] bubbleParticleArray;

	public AudioSource bubblerAudio;

	public AudioSource popBubbleAudio;

	private List<uint> currentParticles = new List<uint>();

	private Dictionary<uint, Vector3> particleInfoDict = new Dictionary<uint, Vector3>();

	private Vector3 outPosition;

	private bool allBubblesPopped;

	public bool disableActivation;

	public bool disableDeactivation;

	public float rotationSpeed = 5f;

	public GameObject fan;

	public float ongoingStrength = 0.005f;

	public float triggerStrength = 0.2f;

	private float initialTriggerPull;

	private float initialTriggerDuration;

	private bool hasBubblerAudio;

	private bool hasPopBubbleAudio;

	protected override void Awake()
	{
		base.Awake();
		bubbleParticleArray = new ParticleSystem.Particle[bubbleParticleSystem.main.maxParticles];
		bubbleParticleSystem.trigger.SetCollider(0, GorillaTagger.Instance.leftHandTriggerCollider.GetComponent<SphereCollider>());
		bubbleParticleSystem.trigger.SetCollider(1, GorillaTagger.Instance.rightHandTriggerCollider.GetComponent<SphereCollider>());
		initialTriggerDuration = 0.05f;
		triggerStrength = 0.8f;
		itemState = ItemStates.State0;
	}

	public override void OnEnable()
	{
		base.OnEnable();
		itemState = ItemStates.State0;
		hasBubblerAudio = bubblerAudio != null && bubblerAudio.clip != null;
		hasPopBubbleAudio = popBubbleAudio != null && popBubbleAudio.clip != null;
	}

	private void InitToDefault()
	{
		itemState = ItemStates.State0;
		if (bubbleParticleSystem.isPlaying)
		{
			bubbleParticleSystem.Stop();
		}
		if (hasBubblerAudio && bubblerAudio.isPlaying)
		{
			bubblerAudio.Stop();
		}
	}

	public override void OnDisable()
	{
		base.OnDisable();
		itemState = ItemStates.State0;
		if (bubbleParticleSystem.isPlaying)
		{
			bubbleParticleSystem.Stop();
		}
		if (hasBubblerAudio && bubblerAudio.isPlaying)
		{
			bubblerAudio.Stop();
		}
		currentParticles.Clear();
		particleInfoDict.Clear();
	}

	public override void ResetToDefaultState()
	{
		base.ResetToDefaultState();
		InitToDefault();
	}

	protected override void LateUpdateShared()
	{
		base.LateUpdateShared();
		if (!IsMyItem() && myOnlineRig != null && myOnlineRig.muted)
		{
			itemState = ItemStates.State0;
		}
		bool forLeftController = currentState == PositionState.InLeftHand;
		if (itemState == ItemStates.State0)
		{
			if (bubbleParticleSystem.isPlaying)
			{
				bubbleParticleSystem.Stop();
			}
			if (hasBubblerAudio && bubblerAudio.isPlaying)
			{
				bubblerAudio.Stop();
			}
		}
		else
		{
			if (!bubbleParticleSystem.isEmitting)
			{
				bubbleParticleSystem.Play();
			}
			if (hasBubblerAudio && !bubblerAudio.isPlaying)
			{
				bubblerAudio.Play();
			}
			if (IsMyItem())
			{
				initialTriggerPull = Time.time;
				GorillaTagger.Instance.StartVibration(forLeftController, triggerStrength, initialTriggerDuration);
				if (Time.time > initialTriggerPull + initialTriggerDuration)
				{
					GorillaTagger.Instance.StartVibration(forLeftController, ongoingStrength, Time.deltaTime);
				}
			}
			float z = fan.transform.localEulerAngles.z + rotationSpeed * Time.fixedDeltaTime;
			fan.transform.localEulerAngles = new Vector3(0f, 0f, z);
		}
		if (allBubblesPopped && itemState != ItemStates.State1)
		{
			return;
		}
		int particles = bubbleParticleSystem.GetParticles(bubbleParticleArray);
		allBubblesPopped = particles <= 0;
		if (allBubblesPopped)
		{
			return;
		}
		for (int i = 0; i < particles; i++)
		{
			if (currentParticles.Contains(bubbleParticleArray[i].randomSeed))
			{
				currentParticles.Remove(bubbleParticleArray[i].randomSeed);
			}
		}
		foreach (uint currentParticle in currentParticles)
		{
			if (particleInfoDict.TryGetValue(currentParticle, out outPosition))
			{
				if (hasPopBubbleAudio)
				{
					AudioSource.PlayClipAtPoint(popBubbleAudio.clip, outPosition);
				}
				particleInfoDict.Remove(currentParticle);
			}
		}
		currentParticles.Clear();
		for (int j = 0; j < particles; j++)
		{
			if (particleInfoDict.TryGetValue(bubbleParticleArray[j].randomSeed, out outPosition))
			{
				particleInfoDict[bubbleParticleArray[j].randomSeed] = bubbleParticleArray[j].position;
			}
			else
			{
				particleInfoDict.Add(bubbleParticleArray[j].randomSeed, bubbleParticleArray[j].position);
			}
			currentParticles.Add(bubbleParticleArray[j].randomSeed);
		}
	}

	public override void OnActivate()
	{
		base.OnActivate();
		itemState = ItemStates.State1;
	}

	public override void OnDeactivate()
	{
		base.OnDeactivate();
		itemState = ItemStates.State0;
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
