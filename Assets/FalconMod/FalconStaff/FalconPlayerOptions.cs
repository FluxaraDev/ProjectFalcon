using UnityEngine;
using GorillaLocomotion;

// Project Falcon player options (Sans, 2026-11-12): the computer's
// ITEMS page, available to EVERY player - not staff-gated.
//   option 1: long arms (Player.FalconArmScale 1.4x reach)
//   option 2: speed boost (see below)
//   option 3: name tags
// Optional Preds were removed for good (Sans, build #10 isolation).
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
// 1.4/1.1 - Sans's original boost (stock 1.1 -> 1.4) expressed as a
// factor, so stock modes get exactly 1.4 and every other state
// keeps its proportions. maxJumpSpeed is NEVER written: the cap is
// the game's own, per mode and state. (The round 33 lesson stands:
// an uncapped 999 let every hit stack velocity without limit.)
public static class FalconPlayerOptions
{
    public static bool LongArms = true;
    public static bool SpeedBoost = true;
    public static bool NameTags = true;

    public const float ArmScale = 1.4f;
    // Sans's boost: jumpMultiplier 1.1 -> 1.4, as a factor.
    public const float BoostFactor = 1.4f / 1.1f;

    public static void ToggleLongArms() { LongArms = !LongArms; ApplyArms(); }
    public static void ToggleSpeedBoost() { SpeedBoost = !SpeedBoost; }
    public static void ToggleNameTags() { NameTags = !NameTags; }

    public static void ApplyAll() { ApplyArms(); }

    public static void ApplyArms()
    {
        Player.FalconArmScale = LongArms ? ArmScale : 1f;
    }
}

// Runs after VRRig's LateUpdate so the game's own per-frame write
// lands first and the boost scales it - no fighting, no flattening.
[DefaultExecutionOrder(1000)]
public class FalconOptionsDriver : MonoBehaviour
{
    private void Start()
    {
        FalconPlayerOptions.ApplyAll();
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
