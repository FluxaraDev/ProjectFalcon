using GorillaLocomotion;
using UnityEngine;

public class GorillaVelocityEstimator : MonoBehaviour
{
	public struct VelocityHistorySample
	{
		public Vector3 linear;

		public Vector3 angular;
	}

	private int numFrames = 8;

	private VelocityHistorySample[] history;

	private int currentFrame;

	private Vector3 lastPos;

	public Vector3 linearVelocity { get; private set; }

	public Vector3 angularVelocity { get; private set; }

	public Vector3 handPos { get; private set; }

	private void Awake()
	{
		history = new VelocityHistorySample[numFrames];
	}

	private void OnEnable()
	{
		currentFrame = 0;
		for (int i = 0; i < history.Length; i++)
		{
			history[i] = default(VelocityHistorySample);
		}
		lastPos = base.transform.position;
	}

	protected void LateUpdate()
	{
		Vector3 position = base.transform.position;
		Vector3 currentVelocity = Player.Instance.currentVelocity;
		Vector3 linear = (position - lastPos) / Time.deltaTime - currentVelocity;
		Vector3 zero = Vector3.zero;
		history[currentFrame % numFrames] = new VelocityHistorySample
		{
			linear = linear,
			angular = zero
		};
		linearVelocity = history[0].linear;
		angularVelocity = history[0].angular;
		for (int i = 0; i < numFrames; i++)
		{
			linearVelocity += history[i].linear;
			angularVelocity += history[i].angular;
		}
		linearVelocity /= (float)numFrames;
		angularVelocity /= (float)numFrames;
		handPos = position;
		currentFrame = (currentFrame + 1) % numFrames;
		lastPos = position;
	}
}
