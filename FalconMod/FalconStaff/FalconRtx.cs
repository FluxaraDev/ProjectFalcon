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
    // Round 85: FalconEnv reads IsOn / writes PhaseOverride so the
    // lobby time override and RTX stop fighting over the day
    // system (build #20: constant snap-back between the two).
    public static FalconRtx Instance;
    public static bool IsOn { get { return Instance != null && Instance.applied; } }
    public static int PhaseOverride = -1;

    // Round 86: per-phase sky keyframes on the game's own
    // 10-phase clock (0 night, 1 dawn, 2 morning, 3-5 day,
    // 6 dusk, 7 sunset, 8 evening, 9 night), lerped live.
    private static readonly Color[] SkyTints = {
        new Color(0.03f, 0.03f, 0.07f), new Color(0.55f, 0.32f, 0.18f),
        new Color(0.33f, 0.45f, 0.62f), new Color(0.34f, 0.49f, 0.72f),
        new Color(0.35f, 0.50f, 0.74f), new Color(0.36f, 0.47f, 0.66f),
        new Color(0.52f, 0.33f, 0.20f), new Color(0.56f, 0.30f, 0.16f),
        new Color(0.16f, 0.14f, 0.26f), new Color(0.03f, 0.03f, 0.07f) };
    private static readonly Color[] SkyGrounds = {
        new Color(0.01f, 0.01f, 0.02f), new Color(0.20f, 0.10f, 0.08f),
        new Color(0.25f, 0.24f, 0.22f), new Color(0.30f, 0.28f, 0.26f),
        new Color(0.31f, 0.29f, 0.27f), new Color(0.28f, 0.25f, 0.22f),
        new Color(0.20f, 0.11f, 0.08f), new Color(0.19f, 0.09f, 0.07f),
        new Color(0.05f, 0.04f, 0.09f), new Color(0.01f, 0.01f, 0.02f) };
    private static readonly float[] SkyExpos = { 0.7f, 0.95f, 1.0f, 1.1f, 1.1f, 1.05f, 0.98f, 0.95f, 0.85f, 0.7f };
    // Round 87: the sun's color/intensity ride the same lerp
    // (they used to snap between four buckets).
    private static readonly Color[] SunColors = {
        new Color(0.60f, 0.70f, 1.00f), new Color(1.00f, 0.55f, 0.30f),
        new Color(1.00f, 0.90f, 0.75f), new Color(1.00f, 0.96f, 0.90f),
        new Color(1.00f, 0.97f, 0.92f), new Color(1.00f, 0.92f, 0.80f),
        new Color(1.00f, 0.50f, 0.28f), new Color(1.00f, 0.45f, 0.25f),
        new Color(0.50f, 0.55f, 0.90f), new Color(0.60f, 0.70f, 1.00f) };
    private static readonly float[] SunIntens = { 0.2f, 0.9f, 1.05f, 1.15f, 1.15f, 1.1f, 0.9f, 0.85f, 0.35f, 0.2f }; // -1 = follow the game clock

    private bool applied;
    private float nextFogCheck;
    private string fogKey = "";

    // Round 85 rescans: rigs/fires that appear AFTER Apply (a
    // lobby join spawns the network rigs - build #20: shadows
    // only ever landed on the offline rig) get the same pass.
    private readonly HashSet<Renderer> knownRigRenderers = new HashSet<Renderer>();
    private readonly HashSet<Campfire> knownFires = new HashSet<Campfire>();
    private readonly System.Collections.Generic.List<Light> fireLightLs = new System.Collections.Generic.List<Light>();
    private float nextRescan;
    private float nextEnvRefresh;
    private float lastSceneCycle;
    private GameObject sunDisc;
    private bool sunDiscVisible;
    private GameObject probeGo;

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
    private readonly List<KeyValuePair<Material, Vector2>> savedIce = new List<KeyValuePair<Material, Vector2>>();
    private GameObject rtxSun;
    private Light rtxSunLight;
    private Light savedSunSource;
    private float savedSunDisk = 2f;
    private object bloomSettings;
    private Type bloomTypeRef;

    private struct RendererShadowState
    {
        public Renderer renderer;
        public UnityEngine.Rendering.ShadowCastingMode cast;
        public bool receive;
    }

    private void Awake()
    {
        Instance = this;
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
        if (applied)
        {
            // Campfire flicker (round 85, Sans: "make it flicker
            // like an actual fire") - per-fire noise so they
            // don't pulse in sync.
            for (int i = 0; i < fireLightLs.Count; i++)
            {
                var fl = fireLightLs[i];
                if (fl == null) continue;
                fl.intensity = 4.5f * (0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 3.1f + i * 17.3f, 0.37f));
            }
            PositionSunDisc();
            if (Time.time >= nextRescan)
            {
                nextRescan = Time.time + 2.5f;
                ScanRigs();
                ScanFires();
            }
            // Round 87 (Sans: "the actual lighting being good,
            // not just bland" + "sky tints actually working"):
            // the skybox ambient + environment reflections were
            // captured ONCE at apply time, so the lerped sky
            // changed overhead while the world's lighting stayed
            // frozen at toggle-time values. Refresh the
            // environment from the live sky every few seconds
            // and the tints actually relight the scene.
            if (Time.time >= nextEnvRefresh)
            {
                nextEnvRefresh = Time.time + 5f;
                try { DynamicGI.UpdateEnvironment(); } catch { }
            }
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
                // Round 86 (build #21 verdict: "no mountains fix
                // at all"): every census loop below now uses
                // Resources.FindObjectsOfTypeAll - the old
                // FindObjectsOfType returns ACTIVE renderers
                // only, and whole map zones sit inactive until
                // entered, so mountains/city materials were
                // never even seen when toggling from the stump.
                // Round 78 (build #18 verdict): the manager's list
                // covers only SOME materials - Forest walls swapped,
                // the ground, stump, props and targets stayed baked.
                // Extend the SAME swap scene-wide: any material whose
                // shader is one of the family's lit shaders (the
                // shaders the listed materials were using) gets the
                // same Standard target, snapshotted the same way.
                var litShaders = new HashSet<Shader>();
                var cutShaders = new HashSet<Shader>();
                foreach (var m in mgr.dayNightSupportedMaterials) if (m != null && m.shader != null) litShaders.Add(m.shader);
                foreach (var m in mgr.dayNightSupportedMaterialsCutout) if (m != null && m.shader != null) cutShaders.Add(m.shader);
                int extra = 0;
                foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
                {
                    if (r == null || !r.gameObject.scene.IsValid()) continue;
                    var mats = r.sharedMaterials;
                    if (mats == null) continue;
                    foreach (var m in mats)
                    {
                        if (m == null || m.shader == null) continue;
                        if (cutShaders.Contains(m.shader)) { SwapSet(new[] { m }, mgr.standardCutout); extra++; }
                        else if (litShaders.Contains(m.shader)) { SwapSet(new[] { m }, mgr.standard); extra++; }
                    }
                }
                diag += "ext " + extra + " ";
                // Mountains ice (Sans): reflective. Name-matched
                // materials go high-smoothness once Standard,
                // snapshotted in savedIce and restored on revert.
                int ice = 0;
                foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
                {
                    if (r == null || !r.gameObject.scene.IsValid()) continue;
                    var mats = r.sharedMaterials;
                    if (mats == null) continue;
                    foreach (var m in mats)
                    {
                        if (m == null) continue;
                        if (m.name.IndexOf("ice", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (!m.HasProperty("_Glossiness") || !m.HasProperty("_Metallic")) continue;
                        bool seen = false;
                        foreach (var kv in savedIce) if (kv.Key == m) { seen = true; break; }
                        if (seen) continue;
                        savedIce.Add(new KeyValuePair<Material, Vector2>(m, new Vector2(m.GetFloat("_Metallic"), m.GetFloat("_Glossiness"))));
                        m.SetFloat("_Metallic", 0.05f);
                        m.SetFloat("_Glossiness", 0.92f);
                        ice++;
                    }
                }
                diag += "ice " + ice + " ";
                // Round 82 (Sans, builds #18/#19: city glass
                // "isn't shiny enough"): glass-named materials
                // get the same reflective treatment as the ice.
                int glass = 0;
                foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
                {
                    if (r == null || !r.gameObject.scene.IsValid()) continue;
                    var mats = r.sharedMaterials;
                    if (mats == null) continue;
                    foreach (var m in mats)
                    {
                        if (m == null) continue;
                        if (m.name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (!m.HasProperty("_Glossiness") || !m.HasProperty("_Metallic")) continue;
                        bool seen = false;
                        foreach (var kv in savedIce) if (kv.Key == m) { seen = true; break; }
                        if (seen) continue;
                        savedIce.Add(new KeyValuePair<Material, Vector2>(m, new Vector2(m.GetFloat("_Metallic"), m.GetFloat("_Glossiness"))));
                        m.SetFloat("_Metallic", 0.1f);
                        m.SetFloat("_Glossiness", 0.95f);
                        glass++;
                    }
                }
                diag += "glass " + glass + " ";
                // Round 82 (build #19: stump/ground STILL baked
                // after the lit-family extension): the remaining
                // family is the manager's UNLIT pair. Anything on
                // gorillaUnlit / gorillaUnlitCutout swaps to the
                // same Standard targets, so stump, ground and the
                // unlit props take real light too.
                int unlit = 0;
                if (mgr.gorillaUnlit != null || mgr.gorillaUnlitCutout != null)
                {
                    foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
                    {
                        if (r == null || !r.gameObject.scene.IsValid()) continue;
                        var mats = r.sharedMaterials;
                        if (mats == null) continue;
                        foreach (var m in mats)
                        {
                            if (m == null || m.shader == null) continue;
                            if (mgr.gorillaUnlitCutout != null && m.shader == mgr.gorillaUnlitCutout) { SwapSet(new[] { m }, mgr.standardCutout); unlit++; }
                            else if (mgr.gorillaUnlit != null && m.shader == mgr.gorillaUnlit) { SwapSet(new[] { m }, mgr.standard); unlit++; }
                        }
                    }
                }
                diag += "unlit " + unlit + " ";
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
                if (proceduralSky.HasProperty("_SunDisk")) savedSunDisk = proceduralSky.GetFloat("_SunDisk");
                // Round 86: thinner atmosphere = less horizon
                // haze washing the sky out pale.
                if (proceduralSky.HasProperty("_AtmosphereThickness")) proceduralSky.SetFloat("_AtmosphereThickness", 0.6f);
                RenderSettings.skybox = proceduralSky;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                try { DynamicGI.UpdateEnvironment(); } catch { }
            }
            var renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null || !r.gameObject.scene.IsValid()) continue;
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
            QualitySettings.shadowDistance = 55f; // round 82: 150 m spread the sun's shadow map so thin a gorilla was a few texels ("two dots") - 55 m triples the detail
            var lights = FindObjectsOfType<Light>();
            foreach (var l in lights)
            {
                if (l == null) continue;
                savedLightShadows.Add(new KeyValuePair<Light, LightShadows>(l, l.shadows));
                savedLightBake.Add(new KeyValuePair<Light, LightmapBakeType>(l, l.bakingOutput.lightmapBakeType));
                l.shadows = LightShadows.Soft;
                // Round 77: Light.lightmapBakeType is editor-only -
                // build #17 died on it (CS1061). The runtime route
                // is Light.bakingOutput (a struct: copy, edit, set).
                var bakeOut = l.bakingOutput;
                bakeOut.lightmapBakeType = LightmapBakeType.Realtime;
                l.bakingOutput = bakeOut;
            }
            diag += "lights " + savedLightShadows.Count + " ";
            // Round 78 (build #18 verdict): realtime lights change
            // the gorilla's lighting but he casts NO shadow. The
            // day/night system owns no Light at all (verified in
            // BetterDayNightManager - texture globals only) and the
            // scene's own lights are points, so nothing directional
            // exists to cast with. RTX brings its own sun: a
            // shadow-casting directional, destroyed on revert.
            try
            {
                var sunGo = new GameObject("FalconRtxSun");
                var sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.96f, 0.9f);
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.9f;
                sun.shadowNormalBias = 0.03f;
                sunGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
                rtxSun = sunGo;
                rtxSunLight = sun;
                // Round 79: the procedural skybox draws its sun
                // DISC from RenderSettings.sun (there is no sun
                // object - the disc is shader-drawn from the
                // designated directional). Register ours so the
                // sky shows a sun in the same direction that casts
                // the shadows (build #18 verdict: "no sun in the
                // sky").
                savedSunSource = RenderSettings.sun;
                RenderSettings.sun = sun;
                diag += "sun 1 ";
            }
            catch { diag += "sun FAIL "; }
            // Rigs + campfires go through the round-85 scan
            // methods so late-appearing ones (lobby rigs, other
            // maps' fires) get the identical pass on rescan.
            ScanRigs();

            // 5) Campfire: a real orange shadow-casting point light
            // at every campfire's fire.
            ScanFires();
            diag += "fires " + fireLights.Count + " ";

            // Round 85: a realtime reflection probe riding the
            // player, so the ice/glass smoothness has something
            // local to reflect (build #20: "ice and glass remains
            // the same"). Rendered once per apply / map cycle.
            try
            {
                probeGo = new GameObject("FalconRtxProbe");
                var probe = probeGo.AddComponent<ReflectionProbe>();
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                // Round 86 (build #21: "same glass flicker when
                // you get close"): ViaScripting rendered the cube
                // ONCE from wherever the head was at apply time -
                // walk toward glass and the stale reflection
                // swims. EveryFrame with per-face time slicing
                // keeps it live for a fraction of the cost.
                probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.EveryFrame;
                probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
                probe.resolution = 128;
                probe.nearClipPlane = 0.3f;
                probe.farClipPlane = 150f;
                probe.intensity = 1f;
                var head = GorillaLocomotion.Player.Instance != null ? GorillaLocomotion.Player.Instance.headCollider : null;
                if (head != null) { probeGo.transform.SetParent(head.transform, false); probeGo.transform.localPosition = Vector3.zero; }
                probe.RenderProbe();
                diag += "probe 1 ";
            }
            catch { diag += "probe FAIL "; }
            CreateSunDisc();

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

            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
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
                // Round 85: FalconEnv's lobby override feeds this
                // index directly while RTX is on (single driver -
                // no more lightmap/clock snap-back fighting).
                int idx = PhaseOverride >= 0 ? PhaseOverride : mgr.currentTimeIndex;
                bool byIdxOnly = PhaseOverride >= 0;
                if ((!byIdxOnly && tod.Contains("night")) || idx == 0 || idx == 8 || idx == 9) key = "night";
                else if ((!byIdxOnly && (tod.Contains("dawn") || tod.Contains("dusk") || tod.Contains("sunset") || tod.Contains("sunrise"))) || idx == 1 || idx == 6 || idx == 7) key = "orange";
            }
            // Round 80 (Sans): the sun INTERACTS with the day
            // system - it rides an arc on the phase clock (dawn
            // low in the east, noon high, sunset low in the west,
            // below the horizon at night), and its color and
            // intensity follow the phase. The sky disc and the
            // shadows track it automatically: one light is both.
            if (rtxSun != null && rtxSunLight != null)
            {
                try
                {
                    float t = PhaseOverride >= 0 ? PhaseOverride : mgr.currentTimeIndex + Mathf.Clamp01(mgr.currentLerp);
                    float dayT = Mathf.Clamp01((t - 1f) / 8f);
                    float elev = Mathf.Sin(dayT * Mathf.PI) * 55f;
                    if (key == "night") elev = -25f;
                    float yaw = Mathf.Lerp(-80f, 40f, dayT);
                    rtxSun.transform.rotation = Quaternion.Euler(90f - elev, yaw, 0f);
                    if (key == "rain" || key == "snow") { rtxSunLight.color = new Color(0.9f, 0.95f, 1f); rtxSunLight.intensity = 0.7f; }
                    else
                    {
                        // Round 87: lerped like the sky (was four
                        // snapping buckets).
                        int i0 = ((int)Mathf.Floor(t) % 10 + 10) % 10;
                        int i1 = (i0 + 1) % 10;
                        float f = t - Mathf.Floor(t);
                        rtxSunLight.color = Color.Lerp(SunColors[i0], SunColors[i1], f);
                        rtxSunLight.intensity = Mathf.Lerp(SunIntens[i0], SunIntens[i1], f);
                    }
                }
                catch { }
            }
            sunDiscVisible = (key == "day" || key == "orange");
            if (sunDisc != null) sunDisc.transform.localScale = Vector3.one * (key == "orange" ? 58f : 42f);
            // Round 86 (build #21 verdict: "the sky stays this
            // ugly pale the whole time"): the sky was FIVE flat
            // buckets repainted only on bucket change - one
            // gray tint for the entire day. Now it rides the
            // game's own 10-phase clock with a lerp between
            // per-phase keyframes, repainted every tick, and
            // the day keyframes are BLUE like the game's sky
            // instead of neutral gray. Weather still overrides.
            if (proceduralSky != null && mgr != null)
            {
                try
                {
                    Color tint, ground; float expo;
                    if (key == "rain") { tint = new Color(0.20f, 0.24f, 0.30f); ground = new Color(0.10f, 0.11f, 0.13f); expo = 0.8f; }
                    else if (key == "snow") { tint = new Color(0.55f, 0.58f, 0.62f); ground = new Color(0.40f, 0.42f, 0.45f); expo = 0.95f; }
                    else
                    {
                        int sIdx = PhaseOverride >= 0 ? PhaseOverride : mgr.currentTimeIndex;
                        float sLerp = PhaseOverride >= 0 ? 0f : Mathf.Clamp01(mgr.currentLerp);
                        float tt = sIdx + sLerp;
                        int i0 = ((int)Mathf.Floor(tt) % 10 + 10) % 10;
                        int i1 = (i0 + 1) % 10;
                        float f = tt - Mathf.Floor(tt);
                        tint = Color.Lerp(SkyTints[i0], SkyTints[i1], f);
                        ground = Color.Lerp(SkyGrounds[i0], SkyGrounds[i1], f);
                        expo = Mathf.Lerp(SkyExpos[i0], SkyExpos[i1], f);
                    }
                    proceduralSky.SetColor("_SkyTint", tint);
                    proceduralSky.SetColor("_GroundColor", ground);
                    proceduralSky.SetFloat("_Exposure", expo);
                    proceduralSky.SetFloat("_SunDisk", key == "night" ? 0f : 2f);
                    proceduralSky.SetFloat("_SunSize", 0.08f);
                }
                catch { }
            }
            if (key == fogKey) return;
            fogKey = key;

            switch (key)
            {
                case "rain":
                    // Round 85 (build #20): the old tint read
                    // WHITE/BLUE in city and drowned the bloom.
                    // Darker, grayer, thinner.
                    SetFog(new Color(0.30f, 0.36f, 0.40f), 0.0095f);
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
            // Round 86: the sky itself is repainted ABOVE the
            // fogKey return now (lerped per phase, every tick);
            // the old five-bucket switch that lived here is gone.
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

    // ---- round 85 scans / sun disc / scene cycle ----

    private void ScanRigs()
    {
        foreach (var rig in FindObjectsOfType<VRRig>())
        {
            if (rig == null) continue;
            foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || knownRigRenderers.Contains(r)) continue;
                knownRigRenderers.Add(r);
                savedRigShadows.Add(new RendererShadowState { renderer = r, cast = r.shadowCastingMode, receive = r.receiveShadows });
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
        }
    }

    private void ScanFires()
    {
        foreach (var cf in FindObjectsOfType<Campfire>())
        {
            if (cf == null || knownFires.Contains(cf)) continue;
            knownFires.Add(cf);
            var anchor = cf.topFire != null ? cf.topFire : cf.transform;
            var go = new GameObject("FalconRtxFireLight");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            var pl = go.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(1f, 0.52f, 0.2f);
            pl.intensity = 4.5f;
            pl.range = 26f; // round 85: a BIGGER pool of firelight (was 18 in #20, 12 before)
            pl.shadows = LightShadows.Soft;
            pl.shadowStrength = 0.85f;
            fireLights.Add(go);
            fireLightLs.Add(pl);
        }
    }

    // Round 85 (Sans: still "no sun disc at all" in build #20):
    // two rounds of _SunDisk/_SunSize on the procedural sky drew
    // nothing on device, so the disc is now a real object - a
    // sphere on the sun light's axis, 700 m out from the head,
    // riding the same arc as the light that casts the shadows.
    // The game's own sky draws its sun and moon as world objects
    // the same way.
    private void CreateSunDisc()
    {
        try
        {
            if (sunDisc != null) return;
            sunDisc = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sunDisc.name = "FalconRtxSunDisc";
            var col = sunDisc.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var mr = sunDisc.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                var m = new Material(mr.sharedMaterial);
                m.color = new Color(1f, 0.97f, 0.88f);
                mr.material = m;
            }
            sunDisc.transform.localScale = Vector3.one * 42f;
            sunDisc.SetActive(false);
        }
        catch { }
    }

    private void PositionSunDisc()
    {
        if (sunDisc == null) return;
        var p = GorillaLocomotion.Player.Instance;
        var head = p != null ? p.headCollider : null;
        if (head == null || rtxSunLight == null || !sunDiscVisible) { sunDisc.SetActive(false); return; }
        sunDisc.SetActive(true);
        sunDisc.transform.position = head.transform.position - rtxSunLight.transform.forward * 700f;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // A newly loaded map's materials and lights were never in
        // the census (build #20: the swap reached Forest only).
        // Rebuild the whole RTX state against the loaded world.
        if (!applied) return;
        if (Time.time - lastSceneCycle < 1f) return;
        lastSceneCycle = Time.time;
        try { Revert(); Apply(); } catch { }
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

        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        if (sunDisc != null) Destroy(sunDisc);
        sunDisc = null;
        sunDiscVisible = false;
        if (probeGo != null) Destroy(probeGo);
        probeGo = null;
        knownRigRenderers.Clear();
        knownFires.Clear();
        fireLightLs.Clear();
        foreach (var go in fireLights)
            if (go != null) Destroy(go);
        fireLights.Clear();
        if (rtxSun != null) Destroy(rtxSun);
        rtxSun = null;
        rtxSunLight = null;
        RenderSettings.sun = savedSunSource;
        savedSunSource = null;
        foreach (var kv in savedIce)
        {
            if (kv.Key == null) continue;
            kv.Key.SetFloat("_Metallic", kv.Value.x);
            kv.Key.SetFloat("_Glossiness", kv.Value.y);
        }
        savedIce.Clear();

        foreach (var kv in savedLightShadows)
            if (kv.Key != null) kv.Key.shadows = kv.Value;
        savedLightShadows.Clear();
        foreach (var kv in savedLightBake)
        {
            if (kv.Key == null) continue;
            var restoreOut = kv.Key.bakingOutput;
            restoreOut.lightmapBakeType = kv.Value;
            kv.Key.bakingOutput = restoreOut;
        }
        savedLightBake.Clear();
        QualitySettings.shadows = savedShadowQuality;
        QualitySettings.shadowResolution = savedShadowResolution;
        QualitySettings.shadowDistance = savedShadowDistance;

        foreach (var s in savedRigShadows)
        {
            if (s.renderer == null) continue;
            s.renderer.shadowCastingMode = s.cast;
            s.renderer.receiveShadows = s.receive;
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
