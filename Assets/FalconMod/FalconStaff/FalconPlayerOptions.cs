using UnityEngine;
using GorillaLocomotion;

// Project Falcon player options (Sans, 2026-11-12): the computer's
// ITEMS page, available to EVERY player - not staff-gated.
//   option 1: long arms (Player.FalconArmScale reach)
//   option 2: speed boost (see below)
//   option 3: name tags
//   Round 88 (Sans): options 4 (RTX) and 5 (PhaseSync) are
//   REMOVED entirely - RTX was ugly and never changed, and
//   PhaseSync never worked. RTX gets redone later.
// Optional Preds were removed for good (Sans, build #10 isolation)
// - that was the old script-preds code.
//
// Round 85 (Sans): long arms and speed boost became SLIDERS in
// the player menu's Movement tab.
//   Long arms: 0.99x (1% SHORTER than regular arms) .. 1.40x
//              (the long arms we ship today = the max).
//   Speed:     target jump multiplier 1.05 (just under default
//              1.1) .. 1.82 (1.3x past today's 1.4 max).
//
// SPEED BOOST, REBUILT round 38 (Sans's device verdict on the old
// version: "makes you slower even as normal monkey and lava monkey
// than just turning it off"). He was right, and here is why: the
// game's speeds are NOT constants. VRRig.LateUpdate writes the pair
// EVERY frame from GorillaGameManager.LocalPlayerSpeed() - per mode
// and per state: infection's infected/noob values interpolate with
// the infected count, hunt's target is {8.5, 1.3}, battle has a
// deliberate {2, 0.5} slow state, and so on. The old driver stomped
// that with a constant {6.5, 1.4} in its own LateUpdate - flattening
// every mode's speed design and fighting VRRig frame by frame
// (which is also why it only "worked half the time").
//
// The boost is now RELATIVE and single-purpose: the driver runs
// AFTER VRRig (execution order), reads the game's own live pair
// from LocalPlayerSpeed(), and scales ONLY the multiplier by
// BoostTarget/1.1 - Sans's original boost (stock 1.1 -> 1.4)
// expressed as a factor, so stock modes get exactly the target
// and every other state keeps its proportions. maxJumpSpeed is
// NEVER written: the cap is the game's own, per mode and state.
// (The round 33 lesson stands: an uncapped 999 let every hit
// stack velocity without limit.)
public static class FalconPlayerOptions
{
    public static bool LongArms = true;
    public static bool SpeedBoost = true;
    public static bool NameTags = true;

    // Player-adjustable values (round 85 sliders).
    public static float ArmScaleValue = 1.4f;  // 0.99 .. 1.40
    public static float BoostTarget = 1.4f;    // 1.05 .. 1.82 (target jump multiplier at stock)
    public static float BoostFactor { get { return BoostTarget / 1.1f; } }

    public static void ToggleLongArms() { LongArms = !LongArms; ApplyArms(); }
    public static void ToggleSpeedBoost() { SpeedBoost = !SpeedBoost; }
    public static void ToggleNameTags() { NameTags = !NameTags; }

    public static void SetArmScale(float v)
    {
        ArmScaleValue = Mathf.Clamp(v, 0.99f, 1.40f);
        ApplyArms();
    }

    public static void SetBoostTarget(float v)
    {
        BoostTarget = Mathf.Clamp(v, 1.05f, 1.82f);
    }

    public static void ApplyAll() { ApplyArms(); }

    public static void ApplyArms()
    {
        Player.FalconArmScale = LongArms ? ArmScaleValue : 1f;
    }

    // The mod-checker string this client publishes about itself
    // (round 85): active mods + realtime FPS. (Round 87 removed
    // the staff mask word - see below.)
    public static string ModStateString(float fps)
    {
        // Round 87 (Sans): staff used to publish "discreet" -
        // which told every player exactly who the staff were.
        // Staff now publish the same honest string as everyone
        // else (their arms/speed/fps state, nothing more).
        var parts = new System.Collections.Generic.List<string>();
        if (LongArms) parts.Add("ARMS x" + ArmScaleValue.ToString("0.00"));
        if (SpeedBoost) parts.Add("SPEED x" + BoostTarget.ToString("0.00"));
        string mods = parts.Count > 0 ? string.Join("  ", parts.ToArray()) : "no mods";
        return mods + "  ·  " + Mathf.RoundToInt(fps) + " FPS";
    }
}

// Runs after VRRig's LateUpdate so the game's own per-frame write
// lands first and the boost scales it - no fighting, no flattening.
[DefaultExecutionOrder(1000)]
public class FalconOptionsDriver : MonoBehaviour
{
    // Round 85: the mod checker's offline row reads the local FPS.
    public static float CurrentFps = 72f;

    private float fpsEma = 72f;
    private float nextPublish;
    private string lastPublished = "";

    private void Start()
    {
        FalconPlayerOptions.ApplyAll();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f) { fpsEma = Mathf.Lerp(fpsEma, 1f / dt, 0.05f); CurrentFps = fpsEma; }
        if (Time.time < nextPublish) return;
        nextPublish = Time.time + 1f;
        PublishModState();
    }

    // Round 85: publish my mod state as a Photon player property
    // so every client's mod-checker tab can read it. 1 s cadence
    // (FPS moves), and only when the string actually changed.
    private void PublishModState()
    {
        try
        {
            if (Photon.Pun.PhotonNetwork.LocalPlayer == null) return;
            string s = FalconPlayerOptions.ModStateString(fpsEma);
            if (s == lastPublished) return;
            lastPublished = s;
            var ht = new ExitGames.Client.Photon.Hashtable();
            ht["falcon_mods"] = s;
            Photon.Pun.PhotonNetwork.LocalPlayer.SetCustomProperties(ht);
        }
        catch { }
    }

    private void LateUpdate()
    {
        if (!FalconPlayerOptions.SpeedBoost || FalconRave.IsRaving) return;
        Player p = Player.Instance;
        if (p == null) return;
        GorillaGameManager gm = GorillaGameManager.instance;
        if (gm == null) return;
        float[] raw = gm.LocalPlayerSpeed();
        if (raw == null || raw.Length < 2) return;
        float boosted = raw[1] * FalconPlayerOptions.BoostFactor;
        if (p.jumpMultiplier != boosted) p.jumpMultiplier = boosted;
        // maxJumpSpeed deliberately untouched - the game's cap rules.
    }
}
