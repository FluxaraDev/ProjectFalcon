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

    // His editor spotlight's position, converted glTF -> Unity.
    private static readonly Vector3 SpotPos = new Vector3(-4.707f, 18.624f, -161.549f);
    // His editor camera's forward (converted), roll leveled off by
    // LookRotation with world up - pitch and yaw as he composed them.
    private static readonly Vector3 CamForward = new Vector3(0.354f, 0.351f, 0.867f);
    // The spotlight beam direction: the converted node quaternion
    // points the beam up in Unity terms, but his editor render pools
    // the light on the city below - so the beam aims down-city.
    private static readonly Vector3 SpotAim = new Vector3(-0.244f, -0.503f, -0.829f);

    private Text statusText;
    private Text stageText;
    private Text tipText;
    private Text flagText;
    private RectTransform barFill;
    private CanvasGroup fadeGroup;
    private AudioSource music;
    private GameObject staffRoot;

    private Font Font
    {
        get
        {
            if (gameFont != null) return gameFont;
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }

    private IEnumerator Start()
    {
        // Park the FalconStaff bootstrap root for the whole load: it
        // is DontDestroyOnLoad (spawned by the first scene's
        // RuntimeInitializeOnLoadMethod) and its parts belong in the
        // game, not in this backdrop. Re-activated just before the
        // main scene activates, so everything works in-game.
        staffRoot = GameObject.Find("FalconStaff");
        if (staffRoot != null) staffRoot.SetActive(false);

        PlaceRig();
        BuildLights();
        try { FalconMetroMap.Build(); } catch (Exception e) { Debug.Log("FalconLoader: metro map build failed: " + e.Message); }
        BuildUi();
        TryBloom();
        PlayMusic();

        // ---- the ANTICHEAT STATION: staged checks, on screen --------
        var stages = FalconAntiCheat.Stages();
        var flags = new List<string>();
        var done = new List<string>();
        for (int i = 0; i < stages.Count; i++)
        {
            stageText.text = StageLines(done, stages[i].Name);
            List<string> f = null;
            try { f = stages[i].Run(); }
            catch (Exception e) { f = new List<string> { stages[i].Name + " error: " + e.Message }; }
            if (f != null && f.Count > 0) { flags.AddRange(f); done.Add("[!!] " + stages[i].Name); }
            else done.Add("[OK] " + stages[i].Name);
            SetProgress((i + 1) / (float)stages.Count * 0.6f, "ANTICHEAT STATION");
            yield return new WaitForSeconds(0.5f);
        }
        stageText.text = StageLines(done, null);
        FalconAntiCheat.SaveResult(flags);

        if (flags.Count > 0)
        {
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

        yield return LoadMain();
    }

    private string StageLines(List<string> done, string current)
    {
        string s = "";
        foreach (string d in done) s += d + "\n";
        if (current != null) s += ">> " + current + " ...";
        return s;
    }

    private void PlaceRig()
    {
        var rig = GameObject.Find("OVRCameraRig");
        if (rig == null) return;
        rig.transform.position = SpotPos;
        // Yaw + the composed pitch only - LookRotation against world
        // up can never roll the horizon.
        rig.transform.rotation = Quaternion.LookRotation(CamForward.normalized, Vector3.up);
    }

    private void BuildLights()
    {
        // Dark like his editor - the round-50 overbright ambient is gone.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.03f, 0.16f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.04f, 0.0f, 0.09f);
        RenderSettings.fogStartDistance = 30f;
        RenderSettings.fogEndDistance = 240f;

        // His spotlight, rebuilt from the glTF's own values.
        var spotGo = new GameObject("FalconSpot");
        spotGo.transform.position = SpotPos;
        spotGo.transform.rotation = Quaternion.LookRotation(SpotAim.normalized, Vector3.up);
        var spot = spotGo.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(0.255f, 0.047f, 0.365f);
        spot.intensity = 3.0f;
        spot.range = 320f;
        spot.spotAngle = 150f;
        spot.innerSpotAngle = 40f;
        spot.shadows = LightShadows.Soft;
        spot.shadowStrength = 0.9f;

        // His directional (violet, 0.91) restyles the scene's sun.
        var sun = GameObject.Find("FalconSun");
        if (sun != null)
        {
            var dl = sun.GetComponent<Light>();
            if (dl != null)
            {
                dl.color = new Color(0.718f, 0f, 1f);
                dl.intensity = 0.9f;
            }
        }
    }

    private void BuildUi()
    {
        var rig = GameObject.Find("OVRCameraRig");
        Vector3 origin = rig != null ? rig.transform.position : SpotPos;
        Quaternion rot = Quaternion.LookRotation(CamForward.normalized, Vector3.up);

        var holder = new GameObject("FalconLoaderUi");
        holder.transform.position = origin + CamForward.normalized * 4.5f;
        holder.transform.rotation = rot;

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

        // Full-screen fade overlay (screen space, renders over the
        // VR view at activation time).
        var fadeGo = new GameObject("FalconFade");
        var fc = fadeGo.AddComponent<Canvas>();
        fc.renderMode = RenderMode.ScreenSpaceOverlay;
        fc.sortingOrder = 999;
        fadeGroup = fadeGo.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        var fimg = new GameObject("FadeImg");
        fimg.transform.SetParent(fadeGo.transform, false);
        var fir = fimg.AddComponent<RectTransform>();
        fir.anchorMin = Vector2.zero; fir.anchorMax = Vector2.one;
        fir.offsetMin = Vector2.zero; fir.offsetMax = Vector2.zero;
        fimg.AddComponent<Image>().color = Color.black;
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

    private void TryBloom()
    {
        // Post Processing v2 via reflection - compiles and runs with
        // or without the package; garnish only, all try/caught.
        try
        {
            var rig = GameObject.Find("OVRCameraRig");
            if (rig == null) return;
            var cams = rig.GetComponentsInChildren<Camera>(true);
            if (cams.Length == 0) return;
            Camera cam = cams[0];

            var layerType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime");
            var resType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessResources, Unity.Postprocessing.Runtime");
            var volType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessVolume, Unity.Postprocessing.Runtime");
            var profType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessProfile, Unity.Postprocessing.Runtime");
            var bloomType = Type.GetType("UnityEngine.Rendering.PostProcessing.Bloom, Unity.Postprocessing.Runtime");
            if (layerType == null || volType == null || profType == null || bloomType == null) return;

            var layer = cam.gameObject.AddComponent(layerType);
            var resources = Resources.Load("PostProcessResources", resType);
            if (resources != null)
                layerType.GetMethod("Init", new[] { resType }).Invoke(layer, new object[] { resources });
            layerType.GetField("volumeLayer").SetValue(layer, (LayerMask)1);

            var profile = ScriptableObject.CreateInstance(profType);
            var settings = Activator.CreateInstance(bloomType);
            bloomType.GetField("enabled").SetValue(settings, true);
            SetParam(bloomType, settings, "intensity", 1.8f);   // round 52: heavier, against the dark scene
            SetParam(bloomType, settings, "threshold", 0.05f);
            SetParam(bloomType, settings, "softKnee", 0.4f);
            var addSettings = profType.GetMethod("AddSettings", new[] { typeof(ScriptableObject) });
            if (addSettings != null) addSettings.Invoke(profile, new object[] { settings });

            var volGo = new GameObject("FalconBloomVolume");
            var vol = volGo.AddComponent(volType);
            volType.GetField("isGlobal").SetValue(vol, true);
            volType.GetField("sharedProfile").SetValue(vol, profile);
        }
        catch { }
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
        if (staffRoot != null) staffRoot.SetActive(true);
        op.allowSceneActivation = true;
    }
}
