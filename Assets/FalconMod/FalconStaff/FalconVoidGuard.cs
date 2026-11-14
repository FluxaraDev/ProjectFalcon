using UnityEngine;
using Player = GorillaLocomotion.Player;

// FalconVoidGuard: the exit barrier for the whole map, for EVERY player
// (not staff-only). This Spring 2023 build ships the disconnect-trigger
// class but never places a single instance of it, so falling out of the
// map used to mean falling forever. Worse, the old design disconnected
// ("kicked") you. Both are replaced by one rule: cross below VoidY -
// from a fall, a fling, anything - and you are teleported back to the
// normal spawn in the stump room. There is no wall to get flung over:
// the check is a position test, not a volume.
// SpawnPosition is the Player object's own scene spawn, measured from
// the scene file ((-64, 12.53, -83) - the stump room).
public class FalconVoidGuard : MonoBehaviour
{
    public static readonly Vector3 SpawnPosition = new Vector3(-64.0f, 12.53f, -83.01f);

    // Scene Y of the stump/forest floor is ~12.5; every real floor in the
    // game (caves, canyon, beach) sits within ~35 m of that. -50 is below
    // all of them with margin. Tune this one constant if a future map
    // digs deeper.
    public const float VoidY = -50f;

    private float nextCheck;

    private void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.2f;
        Player p = Player.Instance;
        if (p == null) return;
        if (p.transform.position.y < VoidY) Respawn(p);
    }

    public static void Respawn(Player p)
    {
        if (p == null) return;
        p.transform.position = SpawnPosition;
        Rigidbody rb = p.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = SpawnPosition;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
