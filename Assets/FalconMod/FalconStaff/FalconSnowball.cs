using Photon.Pun;
using UnityEngine;

// Snowball gun backend (round 18). Sans was right: snowballs exist -
// this replicates SnowballThrowable.LaunchSnowball exactly: pooled
// projectile from ObjectPools, SlingshotProjectile.Launch, and the
// game manager's LaunchSlingshotProjectile RPC so the whole room
// sees them. The projectile prefab is borrowed from the scene's own
// SnowballThrowable the first time it is needed.
public static class FalconSnowball
{
    private static GameObject projectilePrefab;

    public static bool Launch(Vector3 position, Vector3 velocity)
    {
        if (projectilePrefab == null)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<SnowballThrowable>())
            {
                if (t != null && t.projectilePrefab != null && t.gameObject.scene.IsValid())
                {
                    projectilePrefab = t.projectilePrefab;
                    break;
                }
            }
        }
        if (projectilePrefab == null || ObjectPools.instance == null) return false;
        GameObject go = ObjectPools.instance.Instantiate(projectilePrefab);
        if (go == null) return false;
        SlingshotProjectile proj = go.GetComponent<SlingshotProjectile>();
        if (proj == null) return false;
        if (GorillaGameManager.instance != null && PhotonNetwork.InRoom)
        {
            int count = GorillaGameManager.instance.IncrementLocalPlayerProjectileCount();
            proj.Launch(position, velocity, PhotonNetwork.LocalPlayer, false, false, count, 1f);
            GorillaGameManager.instance.photonView.RPC("LaunchSlingshotProjectile", RpcTarget.Others,
                position, velocity, PoolUtils.GameObjHashCode(go), -1, false, count);
        }
        else
        {
            proj.Launch(position, velocity, PhotonNetwork.LocalPlayer, false, false, 0, 1f);
        }
        return true;
    }
}
