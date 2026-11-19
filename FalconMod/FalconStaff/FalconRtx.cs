using System;
using System.Collections.Generic;
using UnityEngine;

// Project Falcon RTX (Sans's spec, 2026-11-18) - ITEMS option 4,
// toggled with the 4 key on the computer's ITEMS page. EVERY player,
// not staff-gated. DEFAULT OFF (Sans: "by default it's off in case
// it breaks") and the ITEMS page carries an experimental warning.
//
// WHAT IT DOES (Sans's words, assembled): with RTX on, the maps'
// lighting goes REALTIME instead of baked, so other lights and
// shadows work; the default Unity sky shows; the graphics upgrade
// overall - slightly less bloom than the loading screen, exposure
// slightly darkened, saturation slightly up, a slight film grain,
// high-quality shadows over every object, arms included. Weather
// drives fog per map: rain makes a greenish-blueish-grayish fog,
// snow a white fog, night a slightly black fog (and everything
// glows slightly at night), sunset/sunrise/dawn a slight orange
// fog, less heavy than night. The campfire glows orange, its fire
// makes real light, and it casts a shadow - basically every light
// has a shadow, so a glowing light throws your gorilla's shadow.
// Turning RTX off reverts ALL of it.
//
// HOW (the mechanism, honest): the game's map materials read the
// baked day/night lightmap globals through custom shaders, and
// that path ignores dynamic lights completely (proven in the rave
// investigation, Sans-confirmed 2026-11-13). So at toggle-on this
// driver swaps the day/night-supported materials - the exact list
// BetterDayNightManager itself carries - onto the game's own
// Standard shader references (the video's step 1: "make it
// standard"), disables the baked sky meshes, puts the default
// Unity procedural sky up, forces realtime shadow-casting lights,
// and arms a post stack (bloom / ACES / contrast / exposure /
// saturation / grain) through the same reflection route the
// loader's bloom uses. EVERYTHING touched is snapshotted first,
// and Revert() puts every value back. Nothing here runs while
// RTX is off beyond one bool check per frame.
public class FalconRtx : MonoBehaviour
{
    private bool applied;
    private float nextFogCheck;
    private string fogKey = "";

    // ---- snapshots (filled by Apply, drained by Revert) ----
    private readonly List<KeyValuePair<Material, Shader>> savedShaders = new List<KeyValuePair<Material, Shader>>();
    private readonly List<KeyValuePair<Light, LightShadows>> savedLightShadows = new List<KeyValuePair<Light, LightShadows>>();
    private readonly List<KeyValuePair<Light, LightmapBakeType>> savedLightBake = new List<KeyValuePair<Light, LightmapBakeType>>();
    private readonly List<KeyValuePair<Camera, CameraClearFlags>> savedCamFlags = new List<KeyValuePair<Camera, CameraClearFlags>>();
    private readonly List<KeyValuePair<Camera, bool>> savedCamHdr = new List<KeyValuePair<Camera, bool>>();
    private readonly List<KeyValuePair<Renderer, bool>> savedSkyRenderers = new List<KeyValuePair<Renderer, bool>>();
    private readonly List<RendererShadowState> savedRigShadows = new List<RendererShadowState>();
    private Material savedSkybox;
    private UnityEngine.Rendering.AmbientMode savedAmbientMode;
    private bool savedFog;
    private FogMode savedFogMode;
    private Color savedFogColor;
    private float savedFogDensity;
    private float savedFogStart;
    private float savedFogEnd;
    private ShadowQuality savedShadowQuality;
    private ShadowResolution savedShadowResolution;
    private float savedShadowDistance;

    private Material proceduralSky;
    private GameObject postVolumeGo;
    private readonly List<Component> addedLayers = new List<Component>();
    private readonly List<GameObject> fireLights = new List<GameObject>();
    private object bloomSettings;
    private Type bloomTypeRef;

    private struct RendererShadowState
    {
        public Renderer renderer;
        public UnityEngine.Rendering.ShadowCastingMode cast;
        public bool receive;
    }

    private void Update()
    {
        bool want = FalconPlayerOptions.Rtx;
        if (want && !applied) Apply();
        else if (!want && applied) Revert();
        if (applied && Time.time >= nextFogCheck)
        {
            nextFogCheck = Time.time + 0.5f;
            UpdateFogAndGlow();
        }
    }

    private void OnDestroy()
    {
        if (applied) Revert();
    }

    // ================= APPLY =================

    private void Apply()
    {
        string diag = "";
        try
        {
            var mgr = BetterDayNightManager.instance;

            // 1) Materials: day/night-supported -> the game's own
            // Standard shaders (snapshot first, revert restores).
            if (mgr != null)
            {
                SwapSet(mgr.dayNightSupportedMaterials, mgr.standard);
                SwapSet(mgr.dayNightSupportedMaterialsCutout, mgr.standardCutout);
                diag += "mats " + savedShaders.Count + " ";
            }
            else diag += "mats NO-MGR ";

            // 2) Default Unity sky: procedural skybox material +
            // skybox ambient, baked sky meshes off, cameras clear
            // to skybox.
            savedSkybox = RenderSettings.skybox;
            savedAmbientMode = RenderSettings.ambientMode;
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                proceduralSky = new Material(skyShader);
                RenderSettings.skybox = proceduralSky;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                try { DynamicGI.UpdateEnvironment(); } catch { }
            }
            var renderers = FindObjectsOfType<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var m = r.sharedMaterial;
                if (m == null || m.shader == null) continue;
                if (m.shader.name.IndexOf("DayNightLerpSky", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    savedSkyRenderers.Add(new KeyValuePair<Renderer, bool>(r, r.enabled));
                    r.enabled = false;
                }
            }
            diag += "sky " + savedSkyRenderers.Count + " ";

            // 3) Cameras: clear to skybox (HDR + post layer happen
            // in the post step below).
            var cams = RigCameras();
            foreach (var cam in cams)
            {
                if (cam == null) continue;
                savedCamFlags.Add(new KeyValuePair<Camera, CameraClearFlags>(cam, cam.clearFlags));
                cam.clearFlags = CameraClearFlags.Skybox;
            }

            // 4) Shadows: high quality globally, every scene light
            // realtime + soft shadows, rigs cast and receive.
            savedShadowQuality = QualitySettings.shadows;
            savedShadowResolution = QualitySettings.shadowResolution;
            savedShadowDistance = QualitySettings.shadowDistance;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowDistance = 150f;
            var lights = FindObjectsOfType<Light>();
            foreach (var l in lights)
            {
                if (l == null) continue;
                savedLightShadows.Add(new KeyValuePair<Light, LightShadows>(l, l.shadows));
                savedLightBake.Add(new KeyValuePair<Light, LightmapBakeType>(l, l.lightmapBakeType));
                l.shadows = LightShadows.Soft;
                l.lightmapBakeType = LightmapBakeType.Realtime;
            }
            diag += "lights " + savedLightShadows.Count + " ";
            var rigs = FindObjectsOfType<VRRig>();
            foreach (var rig in rigs)
            {
                if (rig == null) continue;
                foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    savedRigShadows.Add(new RendererShadowState { renderer = r, cast = r.shadowCastingMode, receive = r.receiveShadow });
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadow = true;
                }
            }

            // 5) Campfire: a real orange shadow-casting point light
            // at every campfire's fire.
            var fires = FindObjectsOfType<Campfire>();
            foreach (var cf in fires)
            {
                if (cf == null) continue;
                var anchor = cf.topFire != null ? cf.topFire : cf.transform;
                var go = new GameObject("FalconRtxFireLight");
                go.transform.SetParent(anchor, false);
                go.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                var pl = go.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = new Color(1f, 0.52f, 0.2f);
                pl.intensity = 2.4f;
                pl.range = 15f;
                pl.shadows = LightShadows.Soft;
                pl.shadowStrength = 0.85f;
                fireLights.Add(go);
            }
            diag += "fires " + fireLights.Count + " ";

            // 6) Fog snapshot (the fog driver writes live values).
            savedFog = RenderSettings.fog;
            savedFogMode = RenderSettings.fogMode;
            savedFogColor = RenderSettings.fogColor;
            savedFogDensity = RenderSettings.fogDensity;
            savedFogStart = RenderSettings.fogStartDistance;
            savedFogEnd = RenderSettings.fogEndDistance;
            fogKey = "";

            // 7) Post stack (bloom / ACES / contrast / exposure /
            // saturation / grain) via reflection, loader-style.
            diag += ApplyPost(cams);

            applied = true;
            UpdateFogAndGlow();
            FalconSessionLog.Log("RTX ON (" + diag.Trim() + ")");
            Debug.Log("FalconRtx ON: " + diag);
        }
        catch (Exception e)
        {
            Debug.Log("FalconRtx apply failed, reverting: " + e.Message);
            try { Revert(); } catch { }
            applied = false;
            FalconSessionLog.Log("RTX APPLY FAILED: " + e.Message);
        }
    }

    private void SwapSet(Material[] mats, Shader target)
    {
        if (mats == null || target == null) return;
        foreach (var m in mats)
        {
            if (m == null || m.shader == null || m.shader == target) continue;
            savedShaders.Add(new KeyValuePair<Material, Shader>(m, m.shader));
            m.shader = target;
        }
    }

    private Camera[] RigCameras()
    {
        var rig = GameObject.Find("OVRCameraRig");
        if (rig != null)
        {
            var cams = rig.GetComponentsInChildren<Camera>(true);
            if (cams.Length > 0) return cams;
        }
        var main = Camera.main;
        return main != null ? new[] { main } : new Camera[0];
    }

    // ================= POST (PPv2 via reflection) =================

    private string ApplyPost(Camera[] cams)
    {
        try
        {
            var layerType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime");
            var resType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessResources, Unity.Postprocessing.Runtime");
            var volType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessVolume, Unity.Postprocessing.Runtime");
            var profType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessProfile, Unity.Postprocessing.Runtime");
            var bloomType = Type.GetType("UnityEngine.Rendering.PostProcessing.Bloom, Unity.Postprocessing.Runtime");
            var gradeType = Type.GetType("UnityEngine.Rendering.PostProcessing.ColorGrading, Unity.Postprocessing.Runtime");
            var grainType = Type.GetType("UnityEngine.Rendering.PostProcessing.FilmGrain, Unity.Postprocessing.Runtime");
            if (layerType == null || volType == null || profType == null || bloomType == null || gradeType == null || grainType == null || resType == null)
                return "post MISSING ";

            var profile = ScriptableObject.CreateInstance(profType);
            var addSettings = profType.GetMethod("AddSettings", new[] { typeof(Type) });
            if (addSettings == null) return "post NO-ADD ";

            // Bloom: slightly less than the loading screen's 3.0.
            var bloom = addSettings.Invoke(profile, new object[] { bloomType });
            if (bloom == null) return "post BLOOM-NULL ";
            bloomType.GetField("enabled").SetValue(bloom, true);
            SetParam(bloomType, bloom, "intensity", 2.2f);
            SetParam(bloomType, bloom, "threshold", 0.85f);
            SetParam(bloomType, bloom, "softKnee", 0.5f);
            bloomSettings = bloom;
            bloomTypeRef = bloomType;

            // Color grading: ACES (the video's tonemapper),
            // contrast 20 (the video's value), exposure slightly
            // darkened, saturation slightly up.
            var grade = addSettings.Invoke(profile, new object[] { gradeType });
            if (grade != null)
            {
                gradeType.GetField("enabled").SetValue(grade, true);
                SetParamEnum(gradeType, grade, "tonemapper", 2); // ACES
                SetParam(gradeType, grade, "postExposure", -0.2f);
                SetParam(gradeType, grade, "contrast", 20f);
                SetParam(gradeType, grade, "saturation", 15f);
            }

            // Film grain: slight.
            var grain = addSettings.Invoke(profile, new object[] { grainType });
            if (grain != null)
            {
                grainType.GetField("enabled").SetValue(grain, true);
                SetParamEnum(grainType, grain, "type", 0); // Thin1
                SetParam(grainType, grain, "intensity", 0.3f);
                SetParam(grainType, grain, "response", 0.8f);
            }

            postVolumeGo = new GameObject("FalconRtxVolume");
            var vol = postVolumeGo.AddComponent(volType);
            volType.GetField("isGlobal").SetValue(vol, true);
            volType.GetField("sharedProfile").SetValue(vol, profile);

            var resources = Resources.Load("PostProcessResources", resType);
            int armed = 0;
            foreach (var cam in cams)
            {
                if (cam == null) continue;
                try
                {
                    savedCamHdr.Add(new KeyValuePair<Camera, bool>(cam, cam.allowHDR));
                    cam.allowHDR = true;
                    var layer = cam.gameObject.AddComponent(layerType);
                    addedLayers.Add(layer);
                    if (resources != null)
                        layerType.GetMethod("Init", new[] { resType }).Invoke(layer, new object[] { resources });
                    layerType.GetField("volumeLayer").SetValue(layer, (LayerMask)1);
                    armed++;
                }
                catch { }
            }
            return "post " + armed + "/" + cams.Length + " ";
        }
        catch (Exception e) { return "post EXC " + e.Message + " "; }
    }

    private void SetParam(Type type, object settings, string field, float value)
    {
        try
        {
            var f = type.GetField(field);
            if (f == null) return;
            var param = f.GetValue(settings);
            if (param == null) return;
            param.GetType().GetField("value").SetValue(param, value);
            param.GetType().GetField("overrideState").SetValue(param, true);
        }
        catch { }
    }

    private void SetParamEnum(Type type, object settings, string field, int value)
    {
        try
        {
            var f = type.GetField(field);
            if (f == null) return;
            var param = f.GetValue(settings);
            if (param == null) return;
            var valueField = param.GetType().GetField("value");
            if (valueField == null) return;
            valueField.SetValue(param, Enum.ToObject(valueField.FieldType, value));
            param.GetType().GetField("overrideState").SetValue(param, true);
        }
        catch { }
    }

    // ================= FOG + NIGHT GLOW =================

    private void UpdateFogAndGlow()
    {
        try
        {
            if (FalconRave.IsRaving) return; // the rave owns fog while it runs
            var mgr = BetterDayNightManager.instance;
            if (mgr == null) return;

            string key = "day";
            // Weather: the global cycle only ever rolls Raining or
            // None (its generator writes nothing else; WeatherType
            // .All is just a wildcard other components match
            // against). Mountains precipitation IS snow though -
            // Sans: "Mountains snows randomly" - the same Raining
            // state falls as snow up there, so rain + standing in
            // the mountains = the snow fog.
            try
            {
                if (mgr.CurrentWeather() == BetterDayNightManager.WeatherType.Raining)
                    key = InMountains() ? "snow" : "rain";
            }
            catch { }
            if (key == "day")
            {
                string tod = (mgr.currentTimeOfDay ?? "").ToLowerInvariant();
                int idx = mgr.currentTimeIndex;
                if (tod.Contains("night") || idx == 0 || idx == 8 || idx == 9) key = "night";
                else if (tod.Contains("dawn") || tod.Contains("dusk") || tod.Contains("sunset") || tod.Contains("sunrise") || idx == 1 || idx == 6 || idx == 7) key = "orange";
            }
            if (key == fogKey) return;
            fogKey = key;

            switch (key)
            {
                case "rain":
                    SetFog(new Color(0.36f, 0.46f, 0.47f), 0.012f);
                    break;
                case "snow":
                    SetFog(new Color(0.88f, 0.91f, 0.95f), 0.014f);
                    break;
                case "night":
                    SetFog(new Color(0.01f, 0.01f, 0.02f), 0.008f);
                    break;
                case "orange":
                    SetFog(new Color(0.38f, 0.20f, 0.09f), 0.005f);
                    break;
                default:
                    RenderSettings.fog = false;
                    break;
            }
            // Night: everything glows slightly - bloom breathes up.
            if (bloomSettings != null && bloomTypeRef != null)
                SetParam(bloomTypeRef, bloomSettings, "intensity", key == "night" ? 2.8f : 2.2f);
        }
        catch { }
    }

    // Mountains quadrant test, anchored on the mountains mod-call
    // button spot (-22.90, 17.68, -101.52): mountains is the map's
    // east outlier (every other area sits at x <= -45), elevated,
    // and below the clouds deck. Approximate by design - it only
    // picks a fog tint during a storm.
    private bool InMountains()
    {
        try
        {
            var p = GorillaLocomotion.Player.Instance;
            if (p == null || p.headCollider == null) return false;
            Vector3 pos = p.headCollider.transform.position;
            return pos.x > -40f && pos.z < -80f && pos.z > -140f && pos.y > 5f && pos.y < 55f;
        }
        catch { return false; }
    }

    private void SetFog(Color color, float density)
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = color;
        RenderSettings.fogDensity = density;
    }

    // ================= REVERT =================

    private void Revert()
    {
        // Post off first so nothing renders through a half state.
        foreach (var layer in addedLayers)
            if (layer != null) Destroy(layer);
        addedLayers.Clear();
        if (postVolumeGo != null) Destroy(postVolumeGo);
        postVolumeGo = null;
        bloomSettings = null;
        bloomTypeRef = null;
        foreach (var kv in savedCamHdr)
            if (kv.Key != null) kv.Key.allowHDR = kv.Value;
        savedCamHdr.Clear();

        foreach (var go in fireLights)
            if (go != null) Destroy(go);
        fireLights.Clear();

        foreach (var kv in savedLightShadows)
            if (kv.Key != null) kv.Key.shadows = kv.Value;
        savedLightShadows.Clear();
        foreach (var kv in savedLightBake)
            if (kv.Key != null) kv.Key.lightmapBakeType = kv.Value;
        savedLightBake.Clear();
        QualitySettings.shadows = savedShadowQuality;
        QualitySettings.shadowResolution = savedShadowResolution;
        QualitySettings.shadowDistance = savedShadowDistance;

        foreach (var s in savedRigShadows)
        {
            if (s.renderer == null) continue;
            s.renderer.shadowCastingMode = s.cast;
            s.renderer.receiveShadow = s.receive;
        }
        savedRigShadows.Clear();

        foreach (var kv in savedCamFlags)
            if (kv.Key != null) kv.Key.clearFlags = kv.Value;
        savedCamFlags.Clear();

        foreach (var kv in savedSkyRenderers)
            if (kv.Key != null) kv.Key.enabled = kv.Value;
        savedSkyRenderers.Clear();
        RenderSettings.skybox = savedSkybox;
        RenderSettings.ambientMode = savedAmbientMode;
        if (proceduralSky != null) Destroy(proceduralSky);
        proceduralSky = null;
        try { DynamicGI.UpdateEnvironment(); } catch { }

        foreach (var kv in savedShaders)
            if (kv.Key != null) kv.Key.shader = kv.Value;
        savedShaders.Clear();

        RenderSettings.fog = savedFog;
        RenderSettings.fogMode = savedFogMode;
        RenderSettings.fogColor = savedFogColor;
        RenderSettings.fogDensity = savedFogDensity;
        RenderSettings.fogStartDistance = savedFogStart;
        RenderSettings.fogEndDistance = savedFogEnd;
        fogKey = "";

        applied = false;
        FalconSessionLog.Log("RTX OFF (reverted)");
        Debug.Log("FalconRtx OFF: reverted");
    }
}
