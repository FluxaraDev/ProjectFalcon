using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class SlingshotProjectile : MonoBehaviour
{
	public Player projectileOwner;

	[Tooltip("Rotates to point along the Y axis after spawn.")]
	public GameObject surfaceImpactEffectPrefab;

	[Tooltip("Distance from the surface that the particle should spawn.")]
	private float impactEffectOffset;

	public float lifeTime = 20f;

	public Color defaultColor = Color.white;

	public Color orangeColor = new Color(1f, 0.5f, 0f, 1f);

	public Color blueColor = new Color(0f, 0.72f, 1f, 1f);

	[Tooltip("Already team colored ball meshes to get better looking paintballs")]
	public Renderer defaultBall;

	[Tooltip("Already team colored ball meshes to get better looking paintballs")]
	public Renderer orangeBall;

	[Tooltip("Already team colored ball meshes to get better looking paintballs")]
	public Renderer blueBall;

	public bool colorizeBalls;

	private bool particleLaunched;

	private float timeCreated;

	private Rigidbody projectileRigidbody;

	private Color teamColor = Color.white;

	public int myProjectileCount;

	private float initialScale;

	private Vector3 previousPosition;

	private ConstantForce forceComponent;

	private MaterialPropertyBlock matPropBlock;

	public void Launch(Vector3 position, Vector3 velocity, Player player, bool blueTeam, bool orangeTeam, int projectileCount, float scale)
	{
		particleLaunched = true;
		timeCreated = Time.time;
		Transform obj = base.transform;
		obj.position = position;
		obj.localScale = Vector3.one * scale;
		GetComponent<Collider>().contactOffset = 0.01f * scale;
		if (scale != 1f)
		{
			projectileRigidbody.useGravity = false;
			forceComponent.force = Physics.gravity * scale;
		}
		projectileRigidbody.velocity = velocity;
		projectileOwner = player;
		myProjectileCount = projectileCount;
		ColorizeProjectile(blueTeam, orangeTeam);
	}

	protected void Awake()
	{
		projectileRigidbody = GetComponent<Rigidbody>();
		forceComponent = GetComponent<ConstantForce>();
		initialScale = base.transform.localScale.x;
		ColorizeBalls();
	}

	private void Deactivate()
	{
		base.transform.localScale = Vector3.one * initialScale;
		ObjectPools.instance.Destroy(base.gameObject);
	}

	private void SpawnImpactEffect(GameObject prefab, Vector3 position, Vector3 normal)
	{
		Vector3 position2 = position + normal * impactEffectOffset;
		GameObject obj = ObjectPools.instance.Instantiate(prefab, position2);
		obj.transform.localScale = base.transform.localScale;
		obj.transform.up = normal;
		obj.GetComponent<GorillaColorizableBase>().SetColor(teamColor);
		obj.GetComponent<SurfaceImpactFX>()?.SetScale(base.transform.localScale.x);
	}

	public void ColorizeProjectile(bool blueTeam, bool orangeTeam)
	{
		teamColor = (blueTeam ? blueColor : (orangeTeam ? orangeColor : defaultColor));
		blueBall.enabled = blueTeam;
		orangeBall.enabled = orangeTeam;
		defaultBall.enabled = !blueTeam && !orangeTeam;
	}

	protected void OnEnable()
	{
		timeCreated = 0f;
		particleLaunched = false;
	}

	protected void OnDisable()
	{
		particleLaunched = false;
	}

	protected void Update()
	{
		if (particleLaunched)
		{
			if (Time.time > timeCreated + lifeTime)
			{
				Deactivate();
			}
			Transform transform = base.transform;
			Vector3 position = transform.position;
			Vector3 forward = position - previousPosition;
			transform.rotation = ((forward.sqrMagnitude > 0f) ? Quaternion.LookRotation(forward) : transform.rotation);
			previousPosition = position;
		}
	}

	protected void OnCollisionEnter(Collision collision)
	{
		if (particleLaunched)
		{
			if (collision.gameObject.TryGetComponent<HitTargetWithScoreCounter>(out var component))
			{
				component.TargetHit();
			}
			ContactPoint contact = collision.GetContact(0);
			SpawnImpactEffect(surfaceImpactEffectPrefab, contact.point, contact.normal);
			Deactivate();
		}
	}

	protected void OnCollisionStay(Collision collision)
	{
		if (particleLaunched)
		{
			if (collision.gameObject.TryGetComponent<HitTargetWithScoreCounter>(out var component))
			{
				component.TargetHit();
			}
			ContactPoint contact = collision.GetContact(0);
			SpawnImpactEffect(surfaceImpactEffectPrefab, contact.point, contact.normal);
			Deactivate();
		}
	}

	protected void OnTriggerEnter(Collider other)
	{
		if (!particleLaunched || projectileOwner != PhotonNetwork.LocalPlayer || !PhotonNetwork.InRoom || GorillaGameManager.instance == null)
		{
			return;
		}
		GorillaBattleManager component = GorillaGameManager.instance.gameObject.GetComponent<GorillaBattleManager>();
		if (other.gameObject.layer != LayerMask.NameToLayer("Gorilla Tag Collider") && other.gameObject.layer != LayerMask.NameToLayer("GorillaSlingshotCollider"))
		{
			return;
		}
		PhotonView componentInParent = other.GetComponentInParent<PhotonView>();
		if (!(componentInParent == null) && PhotonNetwork.LocalPlayer != componentInParent.Owner && (!component || component.LocalCanHit(PhotonNetwork.LocalPlayer, componentInParent.Owner)))
		{
			if ((bool)component)
			{
				PhotonView.Get(component).RPC("ReportSlingshotHit", RpcTarget.MasterClient, componentInParent.Owner, base.transform.position, myProjectileCount);
			}
			PhotonView.Get(GorillaGameManager.instance).RPC("SpawnSlingshotPlayerImpactEffect", RpcTarget.All, base.transform.position, teamColor.r, teamColor.g, teamColor.b, teamColor.a, myProjectileCount);
			Deactivate();
		}
	}

	private void ColorizeBalls()
	{
		if (colorizeBalls)
		{
			if (matPropBlock == null)
			{
				matPropBlock = new MaterialPropertyBlock();
			}
			matPropBlock.SetColor("_Color", defaultColor);
			defaultBall.SetPropertyBlock(matPropBlock);
			matPropBlock.SetColor("_Color", orangeColor);
			orangeBall.SetPropertyBlock(matPropBlock);
			matPropBlock.SetColor("_Color", blueColor);
			blueBall.SetPropertyBlock(matPropBlock);
		}
	}
}
