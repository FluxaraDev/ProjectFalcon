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
    private class Tag { public GameObject go; public TextMesh tm; }
    private readonly Dictionary<VRRig, Tag> tags = new Dictionary<VRRig, Tag>();
    private Font tagFont;
    private float nextScan;

    private void Update()
    {
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + 0.5f;
            Scan();
        }
        Camera cam = Camera.main;
        if (cam == null) return;
        foreach (KeyValuePair<VRRig, Tag> kv in tags)
        {
            if (kv.Value.go != null) kv.Value.go.transform.rotation = cam.transform.rotation;
        }
    }

    private void Scan()
    {
        if (!FalconPlayerOptions.NameTags)
        {
            if (tags.Count > 0) ClearAll();
            return;
        }
        var seen = new HashSet<VRRig>();
        foreach (VRRig rig in FindObjectsOfType<VRRig>())
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
                tags[rig] = tag;
            }
            string want = rp.NickName + "\n" + rp.UserId;
            if (tag.tm.text != want) tag.tm.text = want;
        }
        var stale = new List<VRRig>();
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
        tm.characterSize = 0.055f;
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
