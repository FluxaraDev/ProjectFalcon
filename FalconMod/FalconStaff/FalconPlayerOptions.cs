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
// AFTER VRRig (execution order) and scales ONLY the multiplier by
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

    // Round 90 (Sans): controller prediction (FalconPreds) -
    // EXPERIMENTAL and DEFAULT OFF, session-only on purpose:
    // a relaunch always comes back with preds off ("default
    // is off so no rig troubles - you can relaunch and you
    // can play").
    public static bool Preds = false;
    public static float PredsMs = 20f; // 0..75, step 5

    // Player-adjustable values (round 85 sliders).
    public static float ArmScaleValue = 1.4f;  // 0.99 .. 1.40
    public static float BoostTarget = 1.4f;    // 1.05 .. 1.82 (target jump multiplier at stock)
    public static float BoostFactor { get { return BoostTarget / 1.1f; } }

    public static void ToggleLongArms() { LongArms = !LongArms; ApplyArms(); }
    public static void ToggleSpeedBoost() { SpeedBoost = !SpeedBoost; }
    public static void ToggleNameTags() { NameTags = !NameTags; }
    public static void TogglePreds() { Preds = !Preds; }
    public static void SetPredsMs(float v) { PredsMs = Mathf.Clamp(v, 0f, 75f); }

    public static void SetArmScale(float v)
    {
        ArmScaleValue = Mathf.Clamp(v, 0.99f, 1.40f);
        ApplyArms();
    }

    public static void SetBoostTarget(float v)
    {
        BoostTarget = Mathf.Clamp(v, 1.05f, 1.82f);
        // Round 90 (Sans): the speed value PERSISTS now, like
        // the Settings-tab values do (before, only settings
        // persisted - the boost reset every launch). Flush on
        // every change, same pattern as FalconStaffSettings.
        PlayerPrefs.SetFloat("falcon_opt_boost", BoostTarget);
        PlayerPrefs.Save();
    }

    public static void LoadPersisted()
    {
        BoostTarget = Mathf.Clamp(PlayerPrefs.GetFloat("falcon_opt_boost", BoostTarget), 1.05f, 1.82f);
    }

    // Round 90 (Sans): per-state caps on the FINAL boosted
    // multiplier. Rock monkey maxes at 1.1, lava monkey at
    // 1.3, everyone in hunt at 1.9; casual keeps the slider
    // (no cap). Rock's 1.1 is a cap on the ADDED boost, not
    // a set - setting it would make rock slower than stock.
    // The cap only ever limits: under it, nothing changes.
    // State reads: the local rig's material index (1 = rock,
    // 2 = lava - the same index that drives the rock/lava
    // particles) and the room's gameMode string for hunt.
    public static float CurrentSpeedCap()
    {
        float cap = 0f;
        try
        {
            var room = Photon.Pun.PhotonNetwork.CurrentRoom;
            object gm;
            if (room != null && room.CustomProperties != null
                && room.CustomProperties.TryGetValue("gameMode", out gm) && gm != null
                && gm.ToString().IndexOf("HUNT", System.StringComparison.OrdinalIgnoreCase) >= 0)
                cap = 1.9f;
            var tagger = GorillaTagger.Instance;
            VRRig rig = tagger != null ? (tagger.myVRRig != null ? tagger.myVRRig : tagger.offlineVRRig) : null;
            if (rig != null)
            {
                if (rig.setMatIndex == 1) cap = cap > 0f ? Mathf.Min(cap, 1.1f) : 1.1f;
                else if (rig.setMatIndex == 2) cap = cap > 0f ? Mathf.Min(cap, 1.3f) : 1.3f;
            }
        }
        catch { }
        return cap;
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
        if (Preds) parts.Add("PREDS " + PredsMs.ToString("0") + "MS");
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
        FalconPlayerOptions.LoadPersisted(); // round 90: boost value survives relaunches
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
        // Round 90 (Sans: "speed boost doesn't work in casual"):
        // scale the value VRRig JUST wrote this frame instead of
        // re-querying the game manager. VRRig's LateUpdate runs
        // earlier (default order vs this driver's 1000) and writes
        // the mode's live multiplier - or its flat 1.1 fallback
        // when no manager instance exists, which is the casual
        // case: the old code re-queried the manager, got null,
        // and returned without boosting at all. Reading the
        // written value makes the boost follow the game's own
        // choice in every mode, fallback included. VRRig
        // rewrites it fresh every frame, so this never
        // compounds. maxJumpSpeed untouched - the cap rules.
        float boosted = p.jumpMultiplier * FalconPlayerOptions.BoostFactor;
        // Round 90 (Sans): state caps (rock 1.1 / lava 1.3 /
        // hunt 1.9; casual uncapped). Because this recomputes
        // from VRRig's fresh write every frame, leaving the
        // state restores the casual value on its own - the
        // "uncap and set back" he asked for is automatic.
        float cap = FalconPlayerOptions.CurrentSpeedCap();
        if (cap > 0f && boosted > cap) boosted = cap;
        if (p.jumpMultiplier != boosted) p.jumpMultiplier = boosted;
    }
}
