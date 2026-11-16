// Project Falcon loading scene (Sans's spec, 2026-11-14; round 52
// rework, 2026-11-15, from his build #9 verdicts):
//  - Boots into FalconLoading: his Prisma3D metro map as the backdrop,
//    his music, and the entry anti-cheat running as VISIBLE STAGES
//    ("ANTICHEAT STATION") before GorillaTagSJR loads behind a
//    percent bar. Clean: "ENJOY PROJECT FALCON", fade to black, in.
//    Flagged: the player is told exactly what flagged, the music
//    stops, the game never loads.
//  - Round 52 camera fix: the rig stands where his editor spotlight
//    stands and looks the way his editor camera looked - but LEVEL.
//    Round 50 rolled the whole XR rig by the light's raw quaternion
//    ("the entire thing is sideways"); a VR rig only ever yaws, the
//    horizon is not ours to tilt.
//  - Round 52 lighting fix: round 50's cranked flat ambient washed
//    the scene white ("the lighting is ass, no shadows"). Now the
//    scene is DARK like his editor: near-black purple ambient, his
//    actual glTF lights rebuilt - the deep-purple spotlight (color
//    0.255/0.047/0.365, wide cone) pouring down on the city with
//    shadows on, the violet directional as fill, plus heavy bloom.
//  - Font is the game's own Utopium (scene-wired serialized field).
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FalconLoader : MonoBehaviour
{
    // The game's Utopium font, wired in FalconLoading.unity
    // (Assets/Font/Utopium.asset). Null-safe: falls back to Arial.
    public Font gameFont;

    private const string MainScene = "GorillaTagSJR";

    // Round 61 (Sans): the camera sits AT the block - his scene's
    // Camera/XRCamPos marker position - looking down-city along the
    // camera node's own forward. (Round 60's aerial was far, up and
    // tilted by comparison; renders from the block frame the
    // downtown - hero spire tower center - like his Prisma shot.)
    // His included light is a POINT light at the authored light
    // position, mid-towers over the downtown's south edge; the pool
    // and rims in his render radiate from it in every direction.
    private static readonly Vector3 CamPos = new Vector3(-3.13f, 23.30f, -157.71f);
    private static readonly Vector3 CamForward = new Vector3(-0.354f, -0.351f, -0.867f);
    private static readonly Vector3 LightPos = new Vector3(-4.71f, 18.62f, -161.55f);

    private Text statusText;
    private Text stageText;
    private Text tipText;
    private Text flagText;
    private RectTransform barFill;
    private CanvasGroup fadeGroup;
    private AudioSource music;
    private GameObject staffRoot;
    private GameObject uiHolder;
    private Transform uiEye;
    private bool uiParked;
    private GameObject mapRoot;
    private Quaternion mapYaw = Quaternion.identity;
    private ShadowQuality prevShadows;
    private float prevShadowDist = -1f;

    private Font Font
    {
        get
        {
            if (gameFont != null) return gameFont;
            // Round 57: the builtin Arial lookup can throw in a
            // built player - never let a font fallback kill the load.
            try { return Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            catch { return null; }
        }
    }

    private IEnumerator Start()
    {
        beat = Time.realtimeSinceStartup;
        // Park the FalconStaff bootstrap root for the whole load: it
        // is DontDestroyOnLoad (spawned by the first scene's
        // RuntimeInitializeOnLoadMethod) and its parts belong in the
        // game, not in this backdrop. Re-activated just before the
        // main scene activates, so everything works in-game.
        staffRoot = GameObject.Find("FalconStaff");
        if (staffRoot != null) staffRoot.SetActive(false);

        // Round 65: one frame for XR tracking to deliver a real
        // eye pose before anything measures it.
        yield return null;
        SetupCameras();
        PlaceMap();
        BuildLights();
        // Round 57 flow hardening: music first, and every cosmetic
        // step isolated - in build #10 a single failure in this
        // stretch took the UI, the music, the bloom, the anticheat
        // stages and the scene load down with it (silent, stuck).
        PlayMusic();
        try { BuildUi(); } catch (Exception e) { Debug.Log("FalconLoader: UI build failed: " + e.Message); }
        TryBloom();

        // ---- the ANTICHEAT STATION: staged checks, on screen --------
        // Round 58: stage SETUP is fail-open - if the stage list
        // itself cannot be built, the load goes on without it (a
        // stage that runs and errors still flags, per-stage below).
        // And every stageText write is null-guarded: if the UI build
        // failed, the checks and the load must still complete -
        // build #10 trapped Sans in this scene entirely.
        var stages = new List<FalconAntiCheat.Stage>();
        try { stages = FalconAntiCheat.Stages(); }
        catch (Exception e) { Debug.Log("FalconLoader: anticheat stage setup failed: " + e.Message); }
        var flags = new List<string>();
        var done = new List<string>();
        for (int i = 0; i < stages.Count; i++)
        {
            beat = Time.realtimeSinceStartup;
            if (stageText != null) stageText.text = StageLines(done, stages[i].Name);
            List<string> f = null;
            try { f = stages[i].Run(); }
            catch (Exception e) { f = new List<string> { stages[i].Name + " error: " + e.Message }; }
            if (f != null && f.Count > 0) { flags.AddRange(f); done.Add("[!!] " + stages[i].Name); }
            else done.Add("[OK] " + stages[i].Name);
            SetProgress((i + 1) / (float)stages.Count * 0.6f, "ANTICHEAT STATION");
            yield return new WaitForSeconds(0.5f);
        }
        if (stageText != null) stageText.text = StageLines(done, null);
        FalconAntiCheat.SaveResult(flags);

        if (flags.Count > 0)
        {
            denied = true; // a real deny - the watchdog stands down
            SetProgress(1f, "ACCESS DENIED");
            if (statusText != null) statusText.text = "ACCESS DENIED";
            if (statusText != null) statusText.color = new Color(1f, 0.35f, 0.3f);
            if (tipText != null) tipText.text = "";
            if (flagText != null)
            {
                flagText.gameObject.SetActive(true);
                flagText.text = "FLAGGED:\n" + string.Join("\n", flags.ToArray());
            }
            if (music != null) music.Stop();
            yield break; // the main scene never loads
        }

        BeginLoadMain();
    }

    // Round 58 watchdog: the loader must NEVER be able to trap the
    // player (build #10: a dead flow = stuck in this scene, no way
    // into the game). The load coroutine updates `beat` as it
    // progresses; if 20 s pass with no heartbeat and no real deny
    // was issued, the flow is presumed dead and Update force-starts
    // the main load. A real ACCESS DENIED sets `denied` and the
    // watchdog stands down - denies stay denies.
    private float beat;
    private bool denied;
    private bool loadingMain;

    private void Update()
    {
        // Round 65: if the eye anchor did not exist when the UI
        // was built, park the panel + fade on it the moment it
        // appears (both are head-locked once parked, see BuildUi).
        if (!uiParked && uiHolder != null)
        {
            var e = FindEye();
            if (e != null)
            {
                uiEye = e;
                uiHolder.transform.SetParent(e, false);
                uiHolder.transform.localPosition = new Vector3(0f, 0f, 4.5f);
                uiHolder.transform.localRotation = Quaternion.identity;
                var fg = GameObject.Find("FalconFade");
                if (fg != null && fg.transform.parent == null)
                {
                    fg.transform.SetParent(e, false);
                    fg.transform.localPosition = new Vector3(0f, 0f, 0.45f);
                    fg.transform.localRotation = Quaternion.identity;
                }
                uiParked = true;
            }
        }
        if (denied || loadingMain) return;
        if (beat <= 0f) return;
        if (Time.realtimeSinceStartup - beat > 20f)
        {
            Debug.Log("FalconLoader: watchdog - load flow stalled, forcing main load");
            BeginLoadMain();
        }
    }

    private void BeginLoadMain()
    {
        if (loadingMain) return;
        loadingMain = true;
        StartCoroutine(LoadMain());
    }

    private string StageLines(List<string> done, string current)
    {
        string s = "";
        foreach (string d in done) s += d + "\n";
        if (current != null) s += ">> " + current + " ...";
        return s;
    }

    // Round 65 (build #11 verdicts: "STILL TILTED", "no UI in
    // sight", the grade nothing like his render): the rig is NEVER
    // moved or rotated any more. Rounds 57-62 rotated the RIG to
    // the authored shot, but a tracked headset composes its own
    // live rotation on top - Sans's head yaw/pitch at load stacked
    // with the authored ones and the city came out steep-dived and
    // diagonal. Instead the MAP comes to the player: PlaceMap yaws
    // the whole backdrop around the authored camera point and lands
    // that point exactly on the player's eye, so looking straight
    // ahead frames the downtown the way his Prisma shot does, and
    // the horizon is always his own. The panel is head-locked to
    // the eye (BuildUi) - the parking the fade already used.
    private Transform FindEye()
    {
        var rig = GameObject.Find("OVRCameraRig");
        if (rig == null) return null;
        foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            if (t.name == "CenterEyeAnchor") return t;
        foreach (var cam in rig.GetComponentsInChildren<Camera>(true))
            return cam.transform;
        return null;
    }

    private void SetupCameras()
    {
        var rig = GameObject.Find("OVRCameraRig");
        if (rig == null) return;
        foreach (var cam in rig.GetComponentsInChildren<Camera>(true))
        {
            // Round 68: SOLID clear, no skybox. Every skybox route
            // (rounds 57-62) rendered bright on device - build #12:
            // "this night sky looks like day". A solid near-black
            // purple cannot do that; the city and its glow carry
            // the frame, like his render.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.008f, 0.03f, 1f);
        }
    }

    private void PlaceMap()
    {
        GameObject root = null;
        try { root = FalconMetroMap.Build(); }
        catch (Exception e) { Debug.Log("FalconLoader: metro map build failed: " + e.Message); }
        if (root == null) return;
        mapRoot = root;
        var eye = FindEye();
        if (eye == null) return; // authored orientation - still a backdrop
        Vector3 f = eye.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.001f) return;
        f.Normalize();
        Vector3 cf = CamForward; cf.y = 0f; cf.Normalize();
        float dy = Mathf.Atan2(f.x, f.z) - Mathf.Atan2(cf.x, cf.z);
        mapYaw = Quaternion.Euler(0f, dy * Mathf.Rad2Deg, 0f);
        root.transform.rotation = mapYaw;
        root.transform.position = eye.position - mapYaw * CamPos;
    }

    private void BuildLights()
    {
        // Rounds 60-61 (Sans, 2026-11-15): the loader sky is the
        // game's time-of-day 5.5 dusk, graded DARK like his Prisma
        // render - near-black sky, the city mostly silhouette, the
        // point light doing the work - via the procedural skybox and
        // skybox ambient, no fog. Only in this scene: RenderSettings
        // are per-scene, so SJR keeps the game's own lighting as stock.
        // Round 65: ambient is FLAT now, not skybox-driven. The
        // skybox-ambient path (rounds 59-62) never landed dark on
        // device - build #11's ground sheet blew out bright blue
        // under it. A flat deep-purple ambient is deterministic:
        // shadow sides keep a whisper of his render's purple fill
        // and nothing can wash white. The procedural sky material
        // below is now only the visible background, not the light.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.06f, 0.03f, 0.10f, 1f);
        // (fog is configured with the sky block below, round 68)
        // Round 68: no skybox at all (see SetupCameras) - and the
        // two depth cues his render has that the loader lacked:
        // env reflections nearly off (the plastic sheen), and a
        // whisper of exponential fog in the clear color, so the
        // map's far edge melts into the dark instead of ending at
        // a hard void line ("the void is consuming the bottom").
        RenderSettings.skybox = null;
        RenderSettings.reflectionIntensity = 0.15f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.015f, 0.008f, 0.03f, 1f);
        RenderSettings.fogDensity = 0.0016f;
        // Round 57 (kept): this project's Android quality tiers ship
        // with realtime shadows OFF (QualitySettings shadows: 0) -
        // that, not the lights, is why build #10 had no shadows.
        // Force them on for the loader only; LoadMain restores the
        // game's own settings before SJR activates.
        prevShadows = QualitySettings.shadows;
        prevShadowDist = QualitySettings.shadowDistance;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowDistance = 220f;

        // His included point light (round 61, Sans: "beam from the
        // included point light"), at the authored light position
        // with the glTF's own color and intensity. A real PointLight:
        // it radiates over the downtown in every direction and pools
        // on the streets, like his editor render. Soft shadows on -
        // the loader forces shadow support at the top of this method.
        var spotGo = new GameObject("FalconPointLight");
        // Round 65: the light rides the map pivot - PlaceMap moved
        // the authored world, so the authored light position is a
        // LOCAL position under the map root now.
        if (mapRoot != null)
        {
            spotGo.transform.SetParent(mapRoot.transform, false);
            spotGo.transform.localPosition = LightPos;
        }
        else spotGo.transform.position = LightPos;
        var spot = spotGo.AddComponent<Light>();
        spot.type = LightType.Point;
        spot.color = new Color(0.255f, 0.047f, 0.365f);
        spot.intensity = 6.0f; // round 65: wider, stronger pool - his render bathes the whole downtown
        spot.range = 260f;
        spot.shadows = LightShadows.Soft;
        spot.shadowStrength = 0.9f;

        // Round 68: the visible glow core. Post-processing bloom
        // has never landed on device, and a bare PointLight throws
        // light but shows no source - Sans: "no gloss on the point
        // light at all". A camera-facing radial-gradient sprite at
        // the light's position gives the downtown the hot magenta
        // core his Prisma render has, package or no package.
        try
        {
            var glowTex = MakeGlowTexture();
            var glowGo = new GameObject("FalconGlow");
            if (mapRoot != null) { glowGo.transform.SetParent(mapRoot.transform, false); glowGo.transform.localPosition = LightPos; }
            else glowGo.transform.position = LightPos;
            var sr = glowGo.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(glowTex, new Rect(0f, 0f, glowTex.width, glowTex.height), new Vector2(0.5f, 0.5f), 128f);
            sr.color = new Color(0.85f, 0.25f, 1f, 0.55f);
            glowGo.transform.localScale = new Vector3(110f, 110f, 1f);
        }
        catch { }

        // His directional (violet, 0.91) restyles the scene's sun.
        var sun = GameObject.Find("FalconSun");
        if (sun != null)
        {
            var dl = sun.GetComponent<Light>();
            if (dl != null)
            {
                dl.color = new Color(0.718f, 0f, 1f);
                // Round 65: yawed with the map pivot, so the light
                // still rakes across the downtown from the same
                // side. Kept strong (0.8) for contrast - build #12
                // verdict was "too flat, not enough shadows", and
                // the neon-blue blowout is now handled at the
                // source: FalconMetroMap dims the ground sheet
                // itself, and the flat ambient no longer fills the
                // shadow sides back in.
                dl.intensity = 0.8f;
                dl.transform.rotation = mapYaw * dl.transform.rotation;
                dl.shadows = LightShadows.Soft; // authored scene had m_Type 0 (none)
            }
        }
    }

    private void BuildUi()
    {
        var rig = GameObject.Find("OVRCameraRig");
        Vector3 origin = rig != null ? rig.transform.position : CamPos;
        Quaternion rot = Quaternion.LookRotation(CamForward.normalized, Vector3.up);

        // Round 65 (Sans, build #11: "No UI in sight" - and the
        // first-ever loader HAD one): round 62 placed the panel in
        // WORLD space along the AUTHORED forward from the eye. If
        // Sans's head yaw at load differed from the authored yaw,
        // the panel sat off-view or behind him for the whole load -
        // present in the scene, never in a frame. The panel is now
        // HEAD-LOCKED to the eye anchor (parented, local z 4.5) -
        // the exact parking the fade below already used - so it is
        // centered in view wherever he looks, for the whole load.
        // If the eye does not exist yet, Update parks it the moment
        // it appears.
        Transform eye = FindEye();
        uiEye = eye;

        var holder = new GameObject("FalconLoaderUi");
        uiHolder = holder;
        if (eye != null)
        {
            holder.transform.SetParent(eye, false);
            holder.transform.localPosition = new Vector3(0f, 0f, 4.5f);
            holder.transform.localRotation = Quaternion.identity;
            uiParked = true;
        }
        else
        {
            holder.transform.position = origin + CamForward.normalized * 4.5f;
            holder.transform.rotation = rot;
            uiParked = false;
        }

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(holder.transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(640f, 400f);
        canvasGo.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);
        canvasGo.AddComponent<CanvasScaler>();

        var panel = MakeText(canvasGo.transform, "Panel", "", 24, TextAnchor.MiddleCenter, new Color(0f, 0f, 0f, 0f));
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
        var img = panel.gameObject.AddComponent<Image>();
        img.color = new Color(0.02f, 0f, 0.05f, 0.72f);
        Destroy(panel); // keep only the Image on this object

        var title = MakeText(canvasGo.transform, "Title", "PROJECT FALCON", 44, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 1f));
        Place(title, 0.78f, 0.97f);
        var sub = MakeText(canvasGo.transform, "Sub", "ANTICHEAT STATION", 26, TextAnchor.MiddleCenter, new Color(0.85f, 0.6f, 1f));
        Place(sub, 0.70f, 0.79f);

        statusText = MakeText(canvasGo.transform, "Status", "0%", 40, TextAnchor.MiddleCenter, Color.white);
        Place(statusText, 0.56f, 0.71f);

        var barBg = new GameObject("BarBg");
        barBg.transform.SetParent(canvasGo.transform, false);
        var bgrt = barBg.AddComponent<RectTransform>();
        bgrt.anchorMin = new Vector2(0.1f, 0.52f); bgrt.anchorMax = new Vector2(0.9f, 0.56f);
        bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;
        barBg.AddComponent<Image>().color = new Color(0.25f, 0.1f, 0.35f, 0.9f);
        var fillGo = new GameObject("BarFill");
        fillGo.transform.SetParent(barBg.transform, false);
        barFill = fillGo.AddComponent<RectTransform>();
        barFill.anchorMin = Vector2.zero; barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = Vector2.zero; barFill.offsetMax = Vector2.zero;
        fillGo.AddComponent<Image>().color = new Color(0.85f, 0.3f, 1f);

        stageText = MakeText(canvasGo.transform, "Stages", "", 17, TextAnchor.UpperLeft, new Color(0.8f, 0.95f, 0.8f));
        var srt = stageText.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.1f, 0.16f); srt.anchorMax = new Vector2(0.9f, 0.50f);
        srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;

        tipText = MakeText(canvasGo.transform, "Tip", "LOBBY NOT WORKING? CHANGE YOUR NAME TO PLAY", 18, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.6f));
        Place(tipText, 0.03f, 0.15f);

        flagText = MakeText(canvasGo.transform, "Flags", "", 18, TextAnchor.UpperCenter, new Color(1f, 0.45f, 0.4f));
        var frt = flagText.GetComponent<RectTransform>();
        frt.anchorMin = new Vector2(0.05f, 0.10f); frt.anchorMax = new Vector2(0.95f, 0.50f);
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        flagText.gameObject.SetActive(false);

        // Round 59: the fade is a world-space panel riding the
        // center eye - ScreenSpaceOverlay canvases do not render in
        // VR at all, so the old fade was invisible in the headset.
        var fadeGo = new GameObject("FalconFade");
        if (eye != null)
        {
            fadeGo.transform.SetParent(eye, false);
            fadeGo.transform.localPosition = new Vector3(0f, 0f, 0.45f);
            fadeGo.transform.localRotation = Quaternion.identity;
        }
        else
        {
            fadeGo.transform.position = origin + CamForward.normalized * 0.45f;
            fadeGo.transform.rotation = rot;
        }
        var fc = fadeGo.AddComponent<Canvas>();
        fc.renderMode = RenderMode.WorldSpace;
        var fcrt = fadeGo.GetComponent<RectTransform>();
        fcrt.sizeDelta = new Vector2(200f, 140f);
        fadeGo.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);
        fadeGroup = fadeGo.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        var fimg = new GameObject("FadeImg");
        fimg.transform.SetParent(fadeGo.transform, false);
        var fir = fimg.AddComponent<RectTransform>();
        fir.anchorMin = Vector2.zero; fir.anchorMax = Vector2.one;
        fir.offsetMin = Vector2.zero; fir.offsetMax = Vector2.zero;
        fimg.AddComponent<Image>().color = Color.black;

        // Round 59: all loader UI lives on the Default layer - if
        // the rig cameras' culling masks exclude the UI layer, a
        // canvas on layer 5 never reaches the headset's eyes.
        SetLayerRecursive(holder, 0);
        SetLayerRecursive(fadeGo, 0);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    private Text MakeText(Transform parent, string name, string text, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private void Place(Text t, float minY, float maxY)
    {
        var rt = t.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.02f, minY); rt.anchorMax = new Vector2(0.98f, maxY);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private void SetProgress(float p, string label)
    {
        p = Mathf.Clamp01(p);
        if (statusText != null) statusText.text = Mathf.RoundToInt(p * 100f) + "%";
        if (barFill != null) barFill.anchorMax = new Vector2(p, 1f);
    }

    private static Texture2D MakeGlowTexture()
    {
        int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color[n * n];
        float half = n / 2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                float a = Mathf.Clamp01(1f - d);
                px[y * n + x] = new Color(1f, 1f, 1f, a * a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private void TryBloom()
    {
        // Post Processing v2 via reflection - compiles and runs with
        // or without the package; garnish only, all try/caught.
        // Round 68: the volume is built FIRST and every camera is
        // armed inside its own try - in the old order one throwing
        // camera Init silently killed the volume creation below it,
        // which fits "no bloom at all" in every build so far. Every
        // step now logs, so the next logcat names the dead link.
        try
        {
            var rig = GameObject.Find("OVRCameraRig");
            if (rig == null) { Debug.Log("FalconLoader bloom: no rig"); return; }
            var cams = rig.GetComponentsInChildren<Camera>(true);
            if (cams.Length == 0) { Debug.Log("FalconLoader bloom: no cameras"); return; }

            var layerType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime");
            var resType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessResources, Unity.Postprocessing.Runtime");
            var volType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessVolume, Unity.Postprocessing.Runtime");
            var profType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessProfile, Unity.Postprocessing.Runtime");
            var bloomType = Type.GetType("UnityEngine.Rendering.PostProcessing.Bloom, Unity.Postprocessing.Runtime");
            if (layerType == null || volType == null || profType == null || bloomType == null || resType == null)
            {
                Debug.Log("FalconLoader bloom: post types missing (layer=" + (layerType != null) + " vol=" + (volType != null) + " prof=" + (profType != null) + " bloom=" + (bloomType != null) + " res=" + (resType != null) + ")");
                return;
            }

            var profile = ScriptableObject.CreateInstance(profType);
            var settings = Activator.CreateInstance(bloomType);
            bloomType.GetField("enabled").SetValue(settings, true);
            SetParam(bloomType, settings, "intensity", 3.0f);   // round 61 (Sans): "a lot of bloom"
            SetParam(bloomType, settings, "threshold", 0.0f);
            SetParam(bloomType, settings, "softKnee", 0.5f);
            var addSettings = profType.GetMethod("AddSettings", new[] { typeof(ScriptableObject) });
            if (addSettings != null) addSettings.Invoke(profile, new object[] { settings });

            var volGo = new GameObject("FalconBloomVolume");
            var vol = volGo.AddComponent(volType);
            volType.GetField("isGlobal").SetValue(vol, true);
            volType.GetField("sharedProfile").SetValue(vol, profile);

            var resources = Resources.Load("PostProcessResources", resType);
            Debug.Log("FalconLoader bloom: volume up, resources " + (resources != null ? "found" : "NULL"));
            int armed = 0;
            foreach (var cam in cams)
            {
                try
                {
                    var layer = cam.gameObject.AddComponent(layerType);
                    if (resources != null)
                        layerType.GetMethod("Init", new[] { resType }).Invoke(layer, new object[] { resources });
                    layerType.GetField("volumeLayer").SetValue(layer, (LayerMask)1);
                    armed++;
                }
                catch (Exception ce) { Debug.Log("FalconLoader bloom: camera arm failed: " + ce.Message); }
            }
            Debug.Log("FalconLoader bloom: armed on " + armed + "/" + cams.Length + " cameras");
        }
        catch (Exception e) { Debug.Log("FalconLoader bloom: failed: " + e.Message); }
    }

    private void SetParam(Type bloomType, object settings, string field, float value)
    {
        try
        {
            var f = bloomType.GetField(field);
            if (f == null) return;
            var param = f.GetValue(settings);
            if (param == null) return;
            param.GetType().GetField("value").SetValue(param, value);
            param.GetType().GetField("overrideState").SetValue(param, true);
        }
        catch { }
    }

    private void PlayMusic()
    {
        try
        {
            var clip = Resources.Load<AudioClip>("falcon_load_music");
            if (clip == null) return; // music not in the build yet - silent, never an error
            var go = new GameObject("FalconLoadMusic");
            music = go.AddComponent<AudioSource>();
            music.clip = clip;
            music.loop = true;
            music.volume = 0.9f;
            music.spatialBlend = 0f;
            music.Play();
        }
        catch { }
    }

    private IEnumerator LoadMain()
    {
        var op = SceneManager.LoadSceneAsync(MainScene);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f)
        {
            beat = Time.realtimeSinceStartup;
            SetProgress(0.6f + 0.4f * (op.progress / 0.9f), null);
            yield return null;
        }
        SetProgress(1f, null);
        if (statusText != null) statusText.text = "ENJOY PROJECT FALCON";
        if (tipText != null) tipText.text = "";
        yield return new WaitForSeconds(1.2f);

        // Cinematics (his spec): screen fades to black, music fades out.
        float t = 0f;
        float startVol = music != null ? music.volume : 0f;
        while (t < 1.5f)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 1.5f);
            if (fadeGroup != null) fadeGroup.alpha = k;
            if (music != null) music.volume = startVol * (1f - k);
            yield return null;
        }
        // Round 57: hand the game back its own quality settings
        // (the loader forced realtime shadows on for its scene only).
        if (prevShadowDist >= 0f)
        {
            QualitySettings.shadows = prevShadows;
            QualitySettings.shadowDistance = prevShadowDist;
        }
        if (staffRoot != null) staffRoot.SetActive(true);
        op.allowSceneActivation = true;
    }
}
