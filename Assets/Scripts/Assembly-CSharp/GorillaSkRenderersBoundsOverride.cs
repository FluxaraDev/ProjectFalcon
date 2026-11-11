using UnityEngine;

public class GorillaSkRenderersBoundsOverride : MonoBehaviour
{
	public Bounds localBoundsOverride = new Bounds(Vector3.zero, Vector3.one * 2f);

	public SkinnedMeshRenderer[] skinnedMeshRenderers;

	private Bounds[] originalSkinnedMeshBounds;

	private void OnEnable()
	{
		originalSkinnedMeshBounds = new Bounds[skinnedMeshRenderers.Length];
		for (int i = 0; i < skinnedMeshRenderers.Length; i++)
		{
			originalSkinnedMeshBounds[i] = skinnedMeshRenderers[i].bounds;
			skinnedMeshRenderers[i].localBounds = localBoundsOverride;
		}
	}

	private void OnDisable()
	{
		for (int i = 0; i < skinnedMeshRenderers.Length; i++)
		{
			skinnedMeshRenderers[i].localBounds = originalSkinnedMeshBounds[i];
		}
	}
}
