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
    private Text titleText;
    private Text subText;
    // Round 82: TextMesh readout (see BuildUi) + its sync caches.
    private TextMesh tmTitle, tmSub, tmStatus, tmStages, tmTip, tmFlags;
    private string tmCacheStatus = "", tmCacheStages = "", tmCacheTip = "", tmCacheFlags = "";
    private RectTransform barFill;
    private CanvasGroup fadeGroup;
    private AudioSource music;
    private GameObject staffRoot;
    private GameObject uiHolder;
    private Transform uiEye;
    // Round 69: loader diagnostics, appended to anticheat_log via
    // FalconAntiCheat.AppendDiag - the file Sans actually sends.
    private readonly System.Collections.Generic.List<string> diag = new System.Collections.Generic.List<string>();
    private bool uiParked;
    private GameObject mapRoot;
    private Quaternion mapYaw = Quaternion.identity;
    private ShadowQuality prevShadows;
    private float prevShadowDist = -1f;

    private Font Font
    {
        get
        {
            // Round 71: Utopium FIRST again (Sans wants the game's
            // font; all display text is caps, which Utopium needs).
            // Round 70's Arial run (build #14) PROVED the canvas
            // path end-to-end, and the other two round-52 changes
            // (CanvasScaler, the Text-then-Destroy panel) are gone
            // - so Utopium now gets a clean trial: if the text dies
            // again, the font is convicted and Arial returns for
            // good. The diag line records which font a build ran.
            if (gameFont != null) return gameFont;
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
        diag.Add("PLATFORM: " + Application.platform); // round 91: the runtime platform detection the WebGL swap keys on
        try { BuildUi(); } catch (Exception e) { Debug.Log("FalconLoader: UI build failed: " + e.Message); diag.Add("UI EXCEPTION: " + e.Message); }

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
            // Round 69: the independent TextMesh readout also names
            // the stage it is on - "what it's doing", on a path that
            // does not depend on the canvas at all.
            yield return new WaitForSeconds(0.5f);
        }
        if (stageText != null) stageText.text = StageLines(done, null);
        FalconAntiCheat.SaveResult(flags);
        try { FalconAntiCheat.AppendDiag(string.Join(" | ", diag.ToArray())); } catch { }

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
                flagText.text = "FLAGGED:\n" + Caps(string.Join("\n", flags.ToArray()));
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

    // Round 71: one-time world placement of the panel - straight
    // ahead of the player's facing at loader start (yaw only, no
    // head pitch/roll), 4.5 m out, a touch below eye level so it
    // floats in front without sitting on the skyline. Never
    // re-placed afterwards: it does not follow the head. Also
    // late-parks the fade onto the eye if BuildUi ran before the
    // eye anchor existed (the fade DOES stay screen-locked).
    private void PlaceUi()
    {
        if (uiHolder == null || uiParked) return;
        var eye = FindEye();
        if (eye == null) return;
        uiEye = eye;
        Vector3 fwd = eye.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward; else fwd.Normalize();
        uiHolder.transform.SetParent(null);
        uiHolder.transform.position = eye.position + fwd * 4.5f + new Vector3(0f, -0.15f, 0f);
        uiHolder.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        if (fadeGroup != null && fadeGroup.transform.parent == null)
        {
            fadeGroup.transform.SetParent(eye, false);
            fadeGroup.transform.localPosition = new Vector3(0f, 0f, 0.45f);
            fadeGroup.transform.localRotation = Quaternion.identity;
        }
        uiParked = true;
        diag.Add("UI placed ahead of player");
    }

    private void Update()
    {
        SyncTm();
        // Round 71: place the panel once the eye anchor exists
        // (one-time world placement - see PlaceUi).
        if (!uiParked && uiHolder != null) PlaceUi();
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

    // Round 70 (Sans): Utopium is an all-caps display font - the
    // same one on every text board in the game - and lowercase in
    // it looks wrong. Every loader DISPLAY string goes through
    // Caps() (the anticheat_log file keeps the true case).
    private static string Caps(string s)
    {
        return s != null ? s.ToUpperInvariant() : s;
    }

    private string StageLines(List<string> done, string current)
    {
        string s = "";
        foreach (string d in done) s += d + "\n";
        if (current != null) s += ">> " + current + " ...";
        return Caps(s);
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
        // Round 69 (Sans: "no edge fog around the shade of the
        // buildings - use real fog"): this IS Unity's real fog -
        // but round 68 dialed it to 0.0016 in the clear color,
        // which at downtown distances (10-80 m) changed pixels by
        // single-digit percents toward a color identical to the
        // shadows: invisible. Now a real atmosphere: density
        // 0.0045 (about 20% at 50 m, 36% at 100 m, 59% at 200 m)
        // in a dark magenta-purple haze slightly above the clear
        // color, so building silhouettes melt at their edges the
        // way his Prisma render does.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.06f, 0.03f, 0.08f, 1f);
        RenderSettings.fogDensity = 0.0045f;
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

        // Round 69: the round-68 glow sprite is GONE at Sans's
        // order ("there's already a point light in the gltf you
        // added a big pink object don't do that"). The authored
        // point light above is the only downtown light source;
        // no fake glow objects.

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

        // Round 71 (Sans, build #14): NOT head-locked any more.
        // The panel is placed ONCE in the world, straight ahead
        // of where the player faces when the loader starts
        // (PlaceUi, yaw only), floating - he can look around the
        // map without it glued to his face blocking the view.
        // (Rounds 65-70 head-locked it; it worked, he hated it.)
        // If the eye does not exist yet, the holder waits at the
        // authored fallback below and Update places it the moment
        // the eye appears - still one-time.
        Transform eye = FindEye();
        uiEye = eye;
        diag.Add("UI eye=" + (eye != null ? eye.name : "NULL") + " font=" + (Font != null ? Font.name : "NULL"));

        var holder = new GameObject("FalconLoaderUi");
        uiHolder = holder;
        holder.transform.position = origin + CamForward.normalized * 4.5f;
        holder.transform.rotation = rot;
        uiParked = false;
        PlaceUi();

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(holder.transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(640f, 400f);
        canvasGo.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);
        // Round 70: NO CanvasScaler - the working round-51 canvas
        // had none (in WorldSpace it recomputes the canvas scale
        // from pixel ratios instead of leaving the set value).

        // Round 70: the panel is a DEDICATED Image object, like
        // the working round-51 loader - not a Text with an Image
        // bolted on and the Text Destroy()ed afterwards (two
        // Graphics on one object share a single CanvasRenderer;
        // which one survives is not guaranteed).
        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var prt = panelGo.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
        var img = panelGo.AddComponent<Image>();
        // Round 69: brighter violet, near-opaque - the round-65
        // panel color sat within a few percent of the sky clear
        // color and read as nothing even if it rendered.
        img.color = new Color(0.09f, 0.04f, 0.16f, 0.95f);

        // A solid magenta title strip - pure Image, no font
        // involved, so the panel skeleton is unmistakable even if
        // every text on it fails.
        var strip = new GameObject("TitleStrip");
        strip.transform.SetParent(canvasGo.transform, false);
        var stripRt = strip.AddComponent<RectTransform>();
        stripRt.anchorMin = new Vector2(0f, 0.94f); stripRt.anchorMax = new Vector2(1f, 1f);
        stripRt.offsetMin = Vector2.zero; stripRt.offsetMax = Vector2.zero;
        strip.AddComponent<Image>().color = new Color(0.85f, 0.3f, 1f, 1f);

        titleText = MakeText(canvasGo.transform, "Title", "PROJECT FALCON", 104, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 1f));
        Place(titleText, 0.78f, 0.97f);
        subText = MakeText(canvasGo.transform, "Sub", "ANTICHEAT STATION", 64, TextAnchor.MiddleCenter, new Color(0.85f, 0.6f, 1f));
        Place(subText, 0.70f, 0.79f);

        statusText = MakeText(canvasGo.transform, "Status", "0%", 96, TextAnchor.MiddleCenter, Color.white);
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

        stageText = MakeText(canvasGo.transform, "Stages", "", 48, TextAnchor.UpperLeft, new Color(0.8f, 0.95f, 0.8f));
        var srt = stageText.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.1f, 0.16f); srt.anchorMax = new Vector2(0.9f, 0.50f);
        srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;

        tipText = MakeText(canvasGo.transform, "Tip", "LOBBY NOT WORKING? CHANGE YOUR NAME TO PLAY", 48, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.6f));
        Place(tipText, 0.03f, 0.15f);

        flagText = MakeText(canvasGo.transform, "Flags", "", 52, TextAnchor.UpperCenter, new Color(1f, 0.45f, 0.4f));
        var frt = flagText.GetComponent<RectTransform>();
        frt.anchorMin = new Vector2(0.05f, 0.10f); frt.anchorMax = new Vector2(0.95f, 0.50f);
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        flagText.gameObject.SetActive(false);

        // Round 82 (Sans, build #19 verdict: the loader text is
        // STILL small after three fontSize doublings - 44 to 52
        // to 104 changed nothing he could see): the UI font's
        // glyphs render small at ANY nominal size, so the readout
        // moves to TextMesh - the mod-call labels' mechanism -
        // where characterSize is WORLD size in meters and the
        // font's metrics cannot cap it. The meshes are children
        // of the unscaled holder, laid out in meters over the
        // panel; Update's SyncTm mirrors the UI texts' content
        // into them every frame, and the UI texts are hidden on
        // success. No font -> the UI texts stay as the fallback.
        // (This replaces the round-69 witness idea at full size,
        // as the only readout - not a duplicate.)
        try
        {
            Font tmFont = null;
            try { tmFont = gameFont != null ? gameFont : Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            if (tmFont != null)
            {
                tmTitle = MakeTm(holder.transform, new Vector3(0f, 0.72f, 0.01f), 0.19f, TextAnchor.MiddleCenter, TextAlignment.Center, new Color(1f, 0.85f, 1f), tmFont);
                tmTitle.text = "PROJECT FALCON";
                tmSub = MakeTm(holder.transform, new Vector3(0f, 0.50f, 0.01f), 0.10f, TextAnchor.MiddleCenter, TextAlignment.Center, new Color(0.85f, 0.6f, 1f), tmFont);
                tmSub.text = "ANTICHEAT STATION";
                tmStatus = MakeTm(holder.transform, new Vector3(0f, 0.24f, 0.01f), 0.17f, TextAnchor.MiddleCenter, TextAlignment.Center, Color.white, tmFont);
                tmStatus.text = "0%";
                tmStages = MakeTm(holder.transform, new Vector3(-1.30f, -0.02f, 0.01f), 0.075f, TextAnchor.UpperLeft, TextAlignment.Left, new Color(0.8f, 0.95f, 0.8f), tmFont);
                tmTip = MakeTm(holder.transform, new Vector3(0f, -0.86f, 0.01f), 0.065f, TextAnchor.MiddleCenter, TextAlignment.Center, new Color(1f, 0.95f, 0.6f), tmFont);
                tmTip.text = "LOBBY NOT WORKING? CHANGE YOUR NAME TO PLAY";
                tmFlags = MakeTm(holder.transform, new Vector3(-1.30f, -0.02f, 0.01f), 0.085f, TextAnchor.UpperLeft, TextAlignment.Left, new Color(1f, 0.45f, 0.4f), tmFont);
                tmFlags.gameObject.SetActive(false);
                if (titleText != null) titleText.gameObject.SetActive(false);
                if (subText != null) subText.gameObject.SetActive(false);
                if (statusText != null) statusText.gameObject.SetActive(false);
                if (stageText != null) stageText.gameObject.SetActive(false);
                if (tipText != null) tipText.gameObject.SetActive(false);
                if (flagText != null) flagText.gameObject.SetActive(false);
                diag.Add("TextMesh readout live (font " + tmFont.name + ")");
            }
        }
        catch (System.Exception ex) { diag.Add("TextMesh readout failed: " + ex.GetType().Name); }

        // Round 71: the round-69 TextMesh witness is REMOVED -
        // build #14 proved the canvas path (the witness rendered
        // too, doubled over the panel, and Sans: "the text is a
        // bit big"). One readout, the panel, is the UI now.

        // Round 59: the fade is a world-space panel riding the
        // center eye - ScreenSpaceOverlay canvases do not render in
        // VR at all, so the old fade was invisible in the headset.
        // Round 71 (Sans): make it GIANT - a full screen-locked
        // black panel, nowhere to look around it.
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
        fcrt.sizeDelta = new Vector2(1200f, 1200f); // 6 m x 6 m at the eye (round 71: giant)
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
        diag.Add("UI built ok parent=" + (holder.transform.parent != null ? holder.transform.parent.name : "none"));
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    // Round 82: TextMesh readout helpers (see BuildUi). The sync
    // mirrors the hidden UI texts into the meshes - every write
    // site in the load flow already targets the UI texts, so one
    // mirror covers percent, stage lines, deny flags and the
    // ENJOY line with no missed path.
    private TextMesh MakeTm(Transform parent, Vector3 localPos, float charSize, TextAnchor anchor, TextAlignment align, Color color, Font font)
    {
        var go = new GameObject("tm");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.fontSize = 64;
        tm.characterSize = charSize;
        tm.anchor = anchor;
        tm.alignment = align;
        tm.color = color;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null && font.material != null)
        {
            // Round 88 (Sans: fix the gray text "for the last
            // time"): each readout mesh gets its OWN material
            // cloned from the font's, instead of sharing the
            // font material with every other text user in the
            // game - nothing else can mute or retint it now.
            // The clone tracks the font atlas if it rebuilds.
            var mat = new Material(font.material);
            mr.sharedMaterial = mat;
            var fnt = font;
            fnt.textureRebuiltCallback += (Font rebuilt) =>
            {
                if (mat != null && fnt.material != null)
                    mat.mainTexture = fnt.material.mainTexture;
            };
        }
        return tm;
    }

    private static void SetTmText(TextMesh tm, string s, ref string cache)
    {
        if (tm == null) return;
        if (s == null) s = "";
        if (s == cache) return;
        cache = s;
        tm.text = s;
    }

    private void SyncTm()
    {
        if (tmStatus == null) return;
        if (statusText != null)
        {
            SetTmText(tmStatus, statusText.text, ref tmCacheStatus);
            tmStatus.color = statusText.color;
            if (statusText.gameObject.activeSelf) statusText.gameObject.SetActive(false);
        }
        if (stageText != null)
        {
            SetTmText(tmStages, stageText.text, ref tmCacheStages);
            if (stageText.gameObject.activeSelf) stageText.gameObject.SetActive(false);
        }
        if (tipText != null)
        {
            SetTmText(tmTip, tipText.text, ref tmCacheTip);
            if (tipText.gameObject.activeSelf) tipText.gameObject.SetActive(false);
        }
        if (flagText != null && tmFlags != null)
        {
            bool on = flagText.gameObject.activeSelf || tmFlags.gameObject.activeSelf;
            SetTmText(tmFlags, flagText.text, ref tmCacheFlags);
            if (tmFlags.gameObject.activeSelf != on) tmFlags.gameObject.SetActive(on);
            if (on && flagText.gameObject.activeSelf) flagText.gameObject.SetActive(false);
        }
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

    // Round 88 (Sans): ALL bloom is REMOVED from the loader -
    // "remove all bloom, fix the gray text for the last time."
    // Three rounds of threshold tuning never killed the wash;
    // with no post-processing built in this scene at all,
    // nothing sits between the readout's authored colors and
    // the eye. The PostProcessResources asset stays in
    // Resources untouched - the loader simply never loads it.

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
