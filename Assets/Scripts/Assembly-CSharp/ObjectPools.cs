using System.Collections.Generic;
using UnityEngine;

public class ObjectPools : MonoBehaviour
{
	public static ObjectPools instance;

	[SerializeField]
	private List<SinglePool> pools;

	private Dictionary<int, SinglePool> lookUp;

	public bool initialized { get; private set; }

	protected void Awake()
	{
		instance = this;
	}

	protected void Start()
	{
		lookUp = new Dictionary<int, SinglePool>();
		foreach (SinglePool pool in pools)
		{
			pool.Initialize(base.gameObject);
			lookUp.Add(pool.PoolGUID(), pool);
		}
		initialized = true;
	}

	public SinglePool GetPoolByHash(int hash)
	{
		return lookUp[hash];
	}

	public SinglePool GetPoolByObjectType(GameObject obj)
	{
		int hash = PoolUtils.GameObjHashCode(obj);
		return GetPoolByHash(hash);
	}

	public GameObject Instantiate(GameObject obj)
	{
		return GetPoolByObjectType(obj).Instantiate();
	}

	public GameObject Instantiate(int hash)
	{
		return GetPoolByHash(hash).Instantiate();
	}

	public GameObject Instantiate(GameObject obj, Vector3 position)
	{
		GameObject obj2 = Instantiate(obj);
		obj2.transform.position = position;
		return obj2;
	}

	public GameObject Instantiate(int hash, Vector3 position)
	{
		GameObject obj = Instantiate(hash);
		obj.transform.position = position;
		return obj;
	}

	public GameObject Instantiate(GameObject obj, Vector3 position, Quaternion rotation)
	{
		GameObject obj2 = Instantiate(obj);
		obj2.transform.SetPositionAndRotation(position, rotation);
		return obj2;
	}

	public void Destroy(GameObject obj)
	{
		GetPoolByObjectType(obj).Destroy(obj);
	}
}
