using UnityEngine;
using Player = GorillaLocomotion.Player;

// FalconFlight (round 90, Sans): the casual-only flight kit
// behind the staff hand menu - hand platforms and noclip
// (velocity fly lives in FalconFly and reads the same
// gate). CASUAL rooms only; offline counts as casual. The
// moment the room's mode is anything else - infection
// above all - everything snaps off the same frame:
// platforms hidden, colliders restored. Nothing else is
// in the kit (Sans: "just fly and noclip for now").
public class FalconFlight : MonoBehaviour
{
    public static bool PlatformsOn;
    public static bool NoclipOn;

    private GameObject platL, platR;
    private bool noclipApplied;

    public static bool CasualAllowed
    {
        get
        {
            try
            {
                var room = Photon.Pun.PhotonNetwork.CurrentRoom;
                if (room == null) return true; // offline
                object gm;
                if (room.CustomProperties != null && room.CustomProperties.TryGetValue("gameMode", out gm) && gm != null)
                    return gm.ToString().IndexOf("CASUAL", System.StringComparison.OrdinalIgnoreCase) >= 0;
                return false;
            }
            catch { return false; }
        }
    }

    private void Update()
    {
        bool allowed = CasualAllowed;
        HandlePlatforms(allowed);
        HandleNoclip(allowed);
    }

    // Platforms: hold grip and a platform sits under that
    // hand; release and it is gone. Classic GT platforms.
    private void HandlePlatforms(bool allowed)
    {
        Player p = Player.Instance;
        bool want = allowed && PlatformsOn && p != null;
        SetPlat(ref platL, want && Grip(true), p != null ? p.leftHandTransform : null);
        SetPlat(ref platR, want && Grip(false), p != null ? p.rightHandTransform : null);
    }

    private static bool Grip(bool left)
    {
        var poller = ControllerInputPoller.instance;
        if (poller == null) return false;
        return (left ? poller.leftControllerGripFloat : poller.rightControllerGripFloat) > 0.5f;
    }

    private void SetPlat(ref GameObject plat, bool show, Transform hand)
    {
        if (show && hand != null)
        {
            if (plat == null) plat = MakePlat();
            plat.SetActive(true);
            plat.transform.position = hand.position + Vector3.down * 0.05f;
            plat.transform.rotation = Quaternion.identity;
        }
        else if (plat != null) plat.SetActive(false);
    }

    private static GameObject MakePlat()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "FalconPlatform";
        go.transform.localScale = new Vector3(0.28f, 0.025f, 0.28f);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.material.color = new Color(0.45f, 0.12f, 0.85f, 1f);
        Object.DontDestroyOnLoad(go);
        go.SetActive(false);
        return go;
    }

    // Noclip: while flying (FalconFly on), the head/body
    // colliders come off so geometry cannot block or push
    // the body; the position-write fly already carries the
    // movement through. Any exit - toggle off, fly off,
    // mode change - restores both colliders the same frame.
    private void HandleNoclip(bool allowed)
    {
        Player p = Player.Instance;
        bool want = allowed && NoclipOn && FalconFly.Enabled && p != null;
        if (want && !noclipApplied)
        {
            if (p.headCollider != null) p.headCollider.enabled = false;
            if (p.bodyCollider != null) p.bodyCollider.enabled = false;
            noclipApplied = true;
        }
        else if (!want && noclipApplied)
        {
            if (p != null)
            {
                if (p.headCollider != null) p.headCollider.enabled = true;
                if (p.bodyCollider != null) p.bodyCollider.enabled = true;
            }
            noclipApplied = false;
        }
    }
}
