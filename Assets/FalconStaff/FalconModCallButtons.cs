using System.Collections.Generic;
using UnityEngine;
using GorillaLocomotion;

// Project Falcon MOD CALL buttons (Sans, 2026-11-14, round 34):
// physical buttons placed on every map at positions Sans picked -
// they replace the computer MOD CALL tab entirely (better mobility:
// you call from where the trouble is). Modelled on the game's own
// pressable buttons (GorillaPressableButton pattern: a plate you
// touch, a label, a debounce). Same footprint as the coordinate
// cube (0.15 m), slightly larger touch face, gentle pulsate.
// THREE presses to fire, so nobody calls by accident:
//   press 1: label "MOD CALL!"
//   press 2: label "ARE YOU SURE?"
//   press 3: fires - plate turns red, a billboard popup appears:
//            "fake calls = ban / 25 seconds of ingame audio is
//            sent to us", and the call + voice clip go to staff
//            (FalconModCall). Stages reset after 5 s untouched.
public class FalconModCallButtons : MonoBehaviour
{
    private class Btn
    {
        public string map;
        public Transform root;
        public Transform cube;
        public MeshRenderer rend;
        public TextMesh label;
        public TextMesh popup;
        public int stage;
        public float stageAt;
        public float firedUntil;
        public float punchUntil;
        public bool touching;
        public bool armed = true;
        public float lastPressAt;
        public float phase;
    }

    private static readonly string[] Names = { "Stump", "Forest", "Canyons", "City", "City 2", "Mountains", "Competitive" };
    private static readonly Vector3[] Spots =
    {
        new Vector3(-67.64f, 11.59f, -80.34f),   // Stump (Sans)
        new Vector3(-62.29f, 2.69f, -60.36f),    // Forest
        new Vector3(-88.56f, 9.85f, -108.03f),   // Canyons
        new Vector3(-59.96f, 16.18f, -106.66f),  // City
        new Vector3(-48.05f, 17.60f, -118.56f),  // City second spot - quick abuse report
        new Vector3(-22.90f, 17.68f, -101.52f),  // Mountains
        new Vector3(-45.89f, 15.35f, -117.66f),  // Competitive course
    };

    private readonly List<Btn> btns = new List<Btn>();
    private Font btnFont;
    private float nextFontTry;

    private static readonly Color IdleCol = new Color(0.85f, 0.85f, 0.88f);
    private static readonly Color ArmCol = new Color(1f, 0.85f, 0.2f);
    private static readonly Color SureCol = new Color(1f, 0.5f, 0.1f);
    private static readonly Color FiredCol = new Color(1f, 0.1f, 0.1f);

    private void Start()
    {
        for (int i = 0; i < Spots.Length; i++)
        {
            var root = new GameObject("FalconModCallButton_" + Names[i]);
            root.transform.position = Spots[i];
            var cubeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeGo.transform.SetParent(root.transform, false);
            cubeGo.transform.localScale = Vector3.one * 0.15f;
            var col = cubeGo.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var b = new Btn
            {
                map = Names[i],
                root = root.transform,
                cube = cubeGo.transform,
                rend = cubeGo.GetComponent<MeshRenderer>(),
                phase = i * 1.37f
            };
            if (b.rend != null) b.rend.material.color = IdleCol;
            btns.Add(b);
        }
    }

    private void EnsureLabels()
    {
        if (btnFont == null)
        {
            if (Time.time < nextFontTry) return;
            nextFontTry = Time.time + 1f;
            foreach (VRRig rig in FindObjectsOfType<VRRig>())
                if (rig != null && rig.playerText != null && rig.playerText.font != null) { btnFont = rig.playerText.font; break; }
            if (btnFont == null) return;
        }
        foreach (Btn b in btns)
        {
            if (b.label != null) continue;
            b.label = MakeText(b.root, 0.26f, 0.05f, "MOD CALL");
            b.popup = MakeText(b.root, 0.52f, 0.042f, "fake calls = ban\n25 seconds of ingame audio is sent to us");
            b.popup.gameObject.SetActive(false);
        }
    }

    private TextMesh MakeText(Transform parent, float y, float charSize, string text)
    {
        var go = new GameObject("txt");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, y, 0f);
        var tm = go.AddComponent<TextMesh>();
        tm.font = btnFont;
        tm.fontSize = 64;
        tm.characterSize = charSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
        tm.text = text;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = btnFont.material;
        return tm;
    }

    private void Update()
    {
        EnsureLabels();
        Player p = Player.Instance;
        Camera cam = Camera.main;
        float now = Time.time;
        foreach (Btn b in btns)
        {
            // pulsate (plus a punch dip right after a press)
            float pulse = 1f + 0.07f * Mathf.Sin(now * 3.2f + b.phase);
            float punch = now < b.punchUntil ? 0.8f : 1f;
            b.cube.localScale = Vector3.one * (0.15f * pulse * punch);
            // billboard the texts
            if (cam != null)
            {
                if (b.label != null) b.label.transform.rotation = cam.transform.rotation;
                if (b.popup != null) b.popup.transform.rotation = cam.transform.rotation;
            }
            // fired window expiry
            if (b.firedUntil > 0f && now > b.firedUntil)
            {
                b.firedUntil = 0f;
                b.stage = 0;
                if (b.rend != null) b.rend.material.color = IdleCol;
                if (b.label != null) b.label.text = "MOD CALL";
                if (b.popup != null) b.popup.gameObject.SetActive(false);
            }
            // stage timeout
            if (b.stage > 0 && b.firedUntil == 0f && now - b.stageAt > 5f)
            {
                b.stage = 0;
                if (b.rend != null) b.rend.material.color = IdleCol;
                if (b.label != null) b.label.text = "MOD CALL";
            }
            // Touch, with hysteresis (round 37): Sans's on-device test
            // showed stages double-counting - a VR "press" crosses the
            // 0.17 m line more than once (push in, tremble, pull back),
            // so the old edge trigger fired the call on what felt like
            // two presses. Now a press only registers on entering
            // 0.17 m while ARMED; after a press the button disarms
            // until every hand has left 0.30 m, and presses are at
            // least 0.45 s apart. One deliberate push per stage.
            bool near = false, inZone = false;
            if (p != null)
            {
                if (p.leftHandTransform != null)
                {
                    float d = Vector3.Distance(p.leftHandTransform.position, b.root.position);
                    if (d < 0.17f) near = true;
                    if (d < 0.30f) inZone = true;
                }
                if (p.rightHandTransform != null)
                {
                    float d = Vector3.Distance(p.rightHandTransform.position, b.root.position);
                    if (d < 0.17f) near = true;
                    if (d < 0.30f) inZone = true;
                }
            }
            if (!inZone) b.armed = true;
            if (near && !b.touching && b.armed && b.firedUntil == 0f && now - b.lastPressAt >= 0.45f)
            {
                b.armed = false;
                b.lastPressAt = now;
                Press(b, now);
            }
            b.touching = near;
        }
    }

    private void Press(Btn b, float now)
    {
        b.punchUntil = now + 0.12f;
        b.stageAt = now;
        b.stage++;
        if (b.stage == 1)
        {
            if (b.label != null) b.label.text = "MOD CALL!";
            if (b.rend != null) b.rend.material.color = ArmCol;
        }
        else if (b.stage == 2)
        {
            if (b.label != null) b.label.text = "ARE YOU SURE?";
            if (b.rend != null) b.rend.material.color = SureCol;
        }
        else
        {
            b.stage = 0;
            if (b.label != null) b.label.text = "CALLING...";
            string map = b.map;
            FalconModCall.TryCallFromButton(map, delegate (string outcome)
            {
                b.firedUntil = Time.time + 6f;
                if (b.rend != null) b.rend.material.color = FiredCol;
                if (b.label != null)
                    b.label.text = outcome == "PINGED" ? "STAFF PINGED" : outcome;
                if (b.popup != null) b.popup.gameObject.SetActive(outcome == "PINGED");
            });
        }
    }
}
