using Photon.Pun;
using UnityEngine;

// Test dummy (Sans, 2026-11-10): press the button and a brand-new player
// rig plops into the game in front of you, for gun target practice when
// nobody else is around.
//
// What it is, honestly: the game's own "Gorilla Player Networked" prefab
// (the same prefab PhotonNetworkController instantiates per player),
// spawned LOCALLY with plain Instantiate — no PhotonNetwork.Instantiate,
// no new PlayFab user, no fake room member. It stands where it lands.
// Its VRRig has myPlayer unset, so gun hits on it stop at the raycast:
// every gun still fires, aims, and dry-runs against a real rig shape, but
// effects that move a player (fling, creepify, spin, slip, fake lag) only
// exist on a real client — test those with Self test mode on yourself.
// Outfit and color systems need a live rig too. What the dummy proves:
// your aim, your raycast, and your armed gun, solo.
//
// It spawns through the Resources path the game itself uses:
// Resources/GorillaPrefabs/Gorilla Player Networked.
public static class FalconTestDummy
{
    private static GameObject dummy;

    public static bool IsSpawned => dummy != null;

    public static string Toggle()
    {
        if (dummy != null)
        {
            Object.Destroy(dummy);
            dummy = null;
            return "dummy removed";
        }
        var prefab = Resources.Load<GameObject>("GorillaPrefabs/Gorilla Player Networked");
        if (prefab == null)
        {
            Debug.LogError("[FalconStaff] dummy prefab not found at Resources/GorillaPrefabs/Gorilla Player Networked");
            return "dummy prefab missing";
        }
        // Round 18: spawn in front of the PLAYER. The old code used the
        // caller's transform - the menu component sits on a root object
        // at the world origin, so the dummy plopped at (0, 0, 2), deep
        // under the map, and "spawn dummy does nothing" (Sans).
        Vector3 pos = new Vector3(-64f, 12.4f, -83f);
        Quaternion rot = Quaternion.identity;
        var player = GorillaLocomotion.Player.Instance;
        if (player != null && player.headCollider != null)
        {
            Transform head = player.headCollider.transform;
            Vector3 f = head.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
            f.Normalize();
            pos = head.position + f * 2f;
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out hit, 6f))
                pos.y = hit.point.y;
            else
                pos.y = head.position.y - 1.3f;
            rot = Quaternion.LookRotation(-f); // face the player
        }
        dummy = Object.Instantiate(prefab, pos, rot);
        dummy.name = "Falcon Test Dummy";
        // A networked rig with no owner can spam errors in its update
        // loops; strip PhotonViews so nothing tries to sync or serialize.
        foreach (var view in dummy.GetComponentsInChildren<PhotonView>())
            Object.Destroy(view);
        return "dummy spawned";
    }
}
