using System.Collections.Generic;
using UnityEngine;

// Project Falcon name tags (Sans, 2026-11-13): ITEMS option 3 on the
// computer. Every other player gets a floating tag over their head
// with their name AND their PlayFab ID - the ID is the point (staff
// can read off exactly who to act on). Per-player like the other
// ITEMS options: your toggle only changes what YOU see. TextMesh with
// the font borrowed from the rig's own name text, billboarded to the
// camera like the game's built-in tags.
public class FalconNameTags : MonoBehaviour
{
    private class Tag { public GameObject go; public TextMesh tm; public bool local; }
    private readonly Dictionary<VRRig, Tag> tags = new Dictionary<VRRig, Tag>();
    private Font tagFont;
    private float nextScan;
    // Round 52 perf: cached rig list + reused scratch collections
    // (see SyncTags) - no per-tick scene walks or allocations.
    private readonly List<VRRig> rigCache = new List<VRRig>();
    private readonly HashSet<VRRig> seen = new HashSet<VRRig>();
    private readonly List<VRRig> stale = new List<VRRig>();
    private float nextRefresh;

    private void Update()
    {
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + 0.5f;
            SyncTags();
        }
        if (Time.time >= nextRefresh)
        {
            nextRefresh = Time.time + 2.5f;
            RefreshRigs();
        }
        Camera cam = Camera.main;
        if (cam == null) return;
        foreach (KeyValuePair<VRRig, Tag> kv in tags)
        {
            if (kv.Value.go == null) continue;
            if (kv.Value.local)
            {
                // Round 53 (Sans): your own tag must not copy your head
                // rotation - it pitched and rolled with his head and he
                // could only read it in the mirror. Hold the rig's yaw,
                // upright, like the game's own name text.
                Transform hc = kv.Key != null ? kv.Key.headConstraint : null;
                if (hc != null)
                {
                    Vector3 f = hc.forward; f.y = 0f;
                    if (f.sqrMagnitude > 0.001f)
                        kv.Value.go.transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
                }
            }
            else
            {
                kv.Value.go.transform.rotation = cam.transform.rotation;
            }
        }
    }

    // Round 52 perf (Sans's lag spikes every 0.5 s): the old Scan()
    // ran a full FindObjectsOfType<VRRig>() scene walk plus a fresh
    // HashSet and List every 0.5 s, forever, with tags on by default.
    // Now the scene walk happens at most every 2.5 s into a cached
    // list, the 0.5 s tick only syncs text against the cache, and the
    // scratch collections are reused - no steady-state allocation.
    private void RefreshRigs()
    {
        rigCache.Clear();
        rigCache.AddRange(FindObjectsOfType<VRRig>());
    }

    private void SyncTags()
    {
        if (!FalconPlayerOptions.NameTags)
        {
            if (tags.Count > 0) ClearAll();
            return;
        }
        if (rigCache.Count == 0 && tags.Count == 0) RefreshRigs();
        seen.Clear();
        foreach (VRRig rig in rigCache)
        {
            Photon.Realtime.Player rp = FalconStaffNet.PlayerOf(rig);
            if (rig == null || rp == null) continue;
            if (rig.headConstraint == null) continue;
            seen.Add(rig);
            Tag tag;
            if (!tags.TryGetValue(rig, out tag))
            {
                if (tagFont == null && rig.playerText != null) tagFont = rig.playerText.font;
                if (tagFont == null) continue;
                // Round 34: your OWN tag too (Sans asked - it's how he
                // checks tags in the mirror), riding higher (+0.9)
                // than everyone else's (+0.55) so it stays out of
                // his own view.
                tag = CreateTag(rig, rp.IsLocal ? 0.9f : 0.55f);
                tag.local = rp.IsLocal;
                tags[rig] = tag;
            }
            // Round 43 (Sans): FPS on the tag too. Each client
            // publishes its own frame rate as the "falcon_fps" player
            // property (FalconStaffNet, 1 Hz); tags just read it back.
            // Players on older builds never publish it - no FPS line
            // for them, name + ID as before.
            string want = rp.NickName + "\n" + rp.UserId;
            object fv;
            if (rp.CustomProperties != null && rp.CustomProperties.TryGetValue("falcon_fps", out fv) && fv != null)
                want += "\n" + fv + " FPS";
            else if (rp.IsLocal && FalconStaffNet.LocalFps > 0)
                want += "\n" + FalconStaffNet.LocalFps + " FPS"; // round 53: own tag reads the local measurement directly
            if (tag.tm.text != want) tag.tm.text = want;
        }
        stale.Clear();
        foreach (KeyValuePair<VRRig, Tag> kv in tags)
            if (!seen.Contains(kv.Key)) stale.Add(kv.Key);
        foreach (VRRig rig in stale) { Destroy(tags[rig].go); tags.Remove(rig); }
    }

    private Tag CreateTag(VRRig rig, float height)
    {
        var go = new GameObject("FalconNameTag");
        go.transform.SetParent(rig.headConstraint, false);
        go.transform.localPosition = new Vector3(0f, height, 0f);
        var tm = go.AddComponent<TextMesh>();
        tm.font = tagFont;
        tm.fontSize = 64;
        tm.characterSize = 0.06f; // round 90 (Sans): a tiny bit bigger
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null && tagFont != null) mr.sharedMaterial = tagFont.material;
        return new Tag { go = go, tm = tm };
    }

    private void ClearAll()
    {
        foreach (KeyValuePair<VRRig, Tag> kv in tags)
            if (kv.Value.go != null) Destroy(kv.Value.go);
        tags.Clear();
    }
}
