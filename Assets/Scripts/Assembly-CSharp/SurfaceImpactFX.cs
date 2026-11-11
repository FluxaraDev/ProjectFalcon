using UnityEngine;

public class SurfaceImpactFX : MonoBehaviour
{
	public float startingGravityModifier;

	public Vector3 startingScale = Vector3.one;

	private void GatherDefaults()
	{
		ParticleSystem component = GetComponent<ParticleSystem>();
		startingGravityModifier = component.main.gravityModifierMultiplier;
		startingScale = base.transform.localScale;
	}

	public void SetScale(float scale)
	{
		ParticleSystem.MainModule main = GetComponent<ParticleSystem>().main;
		main.gravityModifierMultiplier = startingGravityModifier * scale;
		base.transform.localScale = startingScale * scale;
	}
}
