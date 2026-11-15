using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Project Falcon loading scene conductor (the ONLY scene with the entry
// anti-cheat). Flow: park the XR rig where the editor spotlight points,
// build the metro map backdrop, play the loading music, run the checks -
// if flagged, show exactly what flagged and stop; if clean, async-load
// GorillaTagSJR behind a percent bar, land on "ENJOY PROJECT FALCON",
// then fade the screen to black and the music out before handing over.
public class FalconLoader : MonoBehaviour
{
    // Sans's editor spotlight, converted from glTF to Unity space: the
    // camera and UI sit where the light points.
    private static readonly Vector3 SpotPos = new Vector3(-4.707f, 18.624f, -161.549f);
    private static readonly Quaternion SpotRot = new Quaternion(0.0974f, -0.2758f, -0.6726f, -0.6798f);

    private Text statusText;
    private Text tipText;
    private Text flagText;
    private RectTransform barFill;
    private CanvasGroup fadeGroup;
    private AudioSource music;
    private GameObject staffRoot;

    private void Start()
    {
        // Park the XR rig at the spotlight.
        var rig = GameObject.Find("OVRCameraRig");
        if (rig != null)
        {
            rig.transform.position = SpotPos;
            rig.transform.rotation = SpotRot.normalized;
        }

        // The FalconStaff bootstrap root also spawns in this scene; park
        // it so no menus/buttons/signs leak into the loading backdrop.
        // It is reactivated right before the main scene takes over.
        staffRoot = GameObject.Find("FalconStaff");
        if (staffRoot != null) staffRoot.SetActive(false);

        // Overbright purple wash over the backdrop.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(1.5f, 1.25f, 1.7f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.42f, 0.08f, 0.58f);
        RenderSettings.fogStartDistance = 45f;
        RenderSettings.fogEndDistance = 280f;

        // Backdrop map (best effort - the load continues without it).
        try { FalconMetroMap.Build(); }
        catch (Exception e) { Debug.LogError("FalconLoader: metro map failed: " + e.Message); }

        BuildUI(rig);
        TryBloom();

        // Loading music (uploaded separately as a Resources binary; the
        // load proceeds silently if it is not in the build yet).
        var clip = Resources.Load<AudioClip>("falcon_load_music");
        if (clip != null)
        {
            music = gameObject.AddComponent<AudioSource>();
            music.clip = clip;
            music.loop = true;
            music.volume = 0.9f;
            music.Play();
        }

        // The gate.
        List<string> flags = FalconAntiCheat.RunChecks();
        if (flags.Count > 0)
        {
            FalconAntiCheat.SaveLog(flags);
            ShowDenied(flags);
            return;
        }
        StartCoroutine(LoadMain());
    }

    private void ShowDenied(List<string> flags)
    {
        if (music != null) music.Stop();
        if (statusText != null) statusText.text = "ACCESS DENIED";
        if (tipText != null) tipText.text = "";
        if (flagText != null)
        {
            flagText.gameObject.SetActive(true);
            flagText.text = string.Join("\n", flags.ToArray()) + "\n\nLog saved to Android/data/" + Application.identifier + "/anticheat_log";
        }
    }

    private IEnumerator LoadMain()
    {
        var op = SceneManager.LoadSceneAsync("GorillaTagSJR");
        op.allowSceneActivation = false;
        while (op.progress < 0.9f)
        {
            SetProgress(op.progress / 0.9f);
            yield return null;
        }
        SetProgress(1f);
        if (statusText != null) statusText.text = "ENJOY PROJECT FALCON";
        if (tipText != null) tipText.text = "";
        yield return new WaitForSeconds(1.2f);

        // Cinematic handover: screen fades to black, music fades out.
        float startVol = music != null ? music.volume : 0f;
        float t = 0f;
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

    private void SetProgress(float p)
    {
        if (statusText != null) statusText.text = Mathf.RoundToInt(p * 100f) + "%";
        if (barFill != null)
        {
            var a = barFill.anchorMax;
            a.x = Mathf.Clamp01(p);
            barFill.anchorMax = a;
        }
    }

    private void BuildUI(GameObject rig)
    {
        Font font = null;
        try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }

        // World-space panel where the light points: 4.5 m in front of
        // the rig, facing the same way.
        var holder = new GameObject("FalconLoaderUI");
        Vector3 fwd = SpotRot * Vector3.forward;
        holder.transform.position = SpotPos + fwd * 4.5f;
        holder.transform.rotation = SpotRot;
        var canvas = holder.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = holder.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(640f, 360f);
        holder.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);

        var panel = MakeImage(holder.transform, "panel", new Color(0.03f, 0f, 0.08f, 0.82f));
        panel.rectTransform.anchorMin = Vector2.zero;
        panel.rectTransform.anchorMax = Vector2.one;
        panel.rectTransform.offsetMin = Vector2.zero;
        panel.rectTransform.offsetMax = Vector2.zero;

        var title = MakeText(holder.transform, font, 44, TextAnchor.MiddleCenter, new Color(1f, 0.65f, 1f));
        title.text = "PROJECT FALCON";
        Place(title.rectTransform, 0.5f, 0.86f, 600f, 70f);

        statusText = MakeText(holder.transform, font, 64, TextAnchor.MiddleCenter, Color.white);
        statusText.text = "0%";
        Place(statusText.rectTransform, 0.5f, 0.58f, 600f, 90f);

        var barBg = MakeImage(holder.transform, "barbg", new Color(0.15f, 0.05f, 0.25f, 1f));
        Place(barBg.rectTransform, 0.5f, 0.42f, 520f, 26f);
        barFill = MakeImage(holder.transform, "barfill", new Color(0.95f, 0.3f, 1f, 1f)).rectTransform;
        barFill.anchorMin = new Vector2(0.5f, 0.42f);
        barFill.anchorMax = new Vector2(0.5f, 0.42f);
        barFill.pivot = new Vector2(0f, 0.5f);
        barFill.anchoredPosition = new Vector2(-260f, 0f);
        barFill.sizeDelta = new Vector2(520f, 26f);
        // Fill is driven by anchorMax inside a full-width proxy: simpler
        // to scale a fixed rect - wrap it: reparent under a stretch box.
        var barBox = new GameObject("barbox");
        barBox.transform.SetParent(holder.transform, false);
        var boxRt = barBox.AddComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.42f);
        boxRt.anchorMax = new Vector2(0.5f, 0.42f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.anchoredPosition = Vector2.zero;
        boxRt.sizeDelta = new Vector2(520f, 26f);
        barFill.SetParent(boxRt, false);
        barFill.anchorMin = new Vector2(0f, 0f);
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = Vector2.zero;
        barFill.offsetMax = Vector2.zero;

        tipText = MakeText(holder.transform, font, 26, TextAnchor.MiddleCenter, new Color(0.9f, 0.85f, 1f));
        tipText.text = "LOBBY NOT WORKING? CHANGE YOUR NAME TO PLAY";
        Place(tipText.rectTransform, 0.5f, 0.28f, 620f, 40f);

        flagText = MakeText(holder.transform, font, 24, TextAnchor.UpperCenter, new Color(1f, 0.45f, 0.45f));
        Place(flagText.rectTransform, 0.5f, 0.45f, 620f, 200f);
        flagText.gameObject.SetActive(false);

        // Fullscreen fade-to-black overlay.
        var fadeGo = new GameObject("FalconFade");
        var fcanvas = fadeGo.AddComponent<Canvas>();
        fcanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fcanvas.sortingOrder = 999;
        fadeGroup = fadeGo.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        var black = MakeImage(fadeGo.transform, "black", Color.black);
        black.rectTransform.anchorMin = Vector2.zero;
        black.rectTransform.anchorMax = Vector2.one;
        black.rectTransform.offsetMin = Vector2.zero;
        black.rectTransform.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rt, float ax, float ay, float w, float h)
    {
        rt.anchorMin = new Vector2(ax, ay);
        rt.anchorMax = new Vector2(ax, ay);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
    }

    private static Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static Text MakeText(Transform parent, Font font, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject("text");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    // Bloom via the Post Processing v2 package, wired by reflection so
    // this file compiles and runs whether or not the package is in the
    // project. Garnish only - any failure is silent.
    private void TryBloom()
    {
        try
        {
            var layerType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime");
            if (layerType == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            var resources = Resources.Load("PostProcessResources");
            if (resources == null) return;
            var layer = cam.gameObject.AddComponent(layerType);
            layerType.GetMethod("Init").Invoke(layer, new object[] { resources });

            var profileType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessProfile, Unity.Postprocessing.Runtime");
            var bloomType = Type.GetType("UnityEngine.Rendering.PostProcessing.Bloom, Unity.Postprocessing.Runtime");
            var volumeType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessVolume, Unity.Postprocessing.Runtime");
            if (profileType == null || bloomType == null || volumeType == null) return;
            var profile = ScriptableObject.CreateInstance(profileType);
            var bloom = profileType.GetMethod("AddSettings", new[] { typeof(Type) }).Invoke(profile, new object[] { bloomType });
            SetParam(bloom, bloomType, "intensity", 1.6f);
            SetParam(bloom, bloomType, "threshold", 0.1f);
            SetParam(bloom, bloomType, "softKnee", 0.5f);
            var volGo = new GameObject("FalconBloomVolume");
            var vol = volGo.AddComponent(volumeType);
            volumeType.GetField("isGlobal").SetValue(vol, true);
            volumeType.GetField("sharedProfile").SetValue(vol, profile);
            volumeType.GetField("weight").SetValue(vol, 1f);
        }
        catch { }
    }

    private static void SetParam(object settings, Type settingsType, string field, float value)
    {
        try
        {
            var param = settingsType.GetField(field).GetValue(settings);
            param.GetType().GetProperty("value").SetValue(param, value, null);
        }
        catch { }
    }
}
