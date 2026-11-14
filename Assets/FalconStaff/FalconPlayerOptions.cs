using UnityEngine;
using GorillaLocomotion;

// Project Falcon player options (Sans, 2026-11-12): the computer's
// ITEMS page, available to EVERY player - not staff-gated. Two
// toggles, both default ON:
//   option 1: long arms (Player.FalconArmScale 1.4x reach)
//   option 2: speed boost (jumpMultiplier 1.4; the maxJumpSpeed cap
//             stays at the STOCK 6.5 - round 33: Sans tried cap 999
//             on device and every hit stacked velocity past any
//             limit until he zoomed unfairly fast)
//
// Optional Preds were option 3 and are REMOVED (Sans, build #10
// isolation, final): the preds' interaction with movement broke the
// rig (camera detached, body left above, couldn't walk). Long arms
// and speed boost are verified working by Sans - do not touch.
public static class FalconPlayerOptions
{
    public static bool LongArms = true;
    public static bool SpeedBoost = true;
    public static bool NameTags = true;

    public const float ArmScale = 1.4f;
    public const float JumpSpeed = 6.5f; // stock cap, Sans round 33 (999 stacked velocity without limit)
    public const float JumpMult = 1.4f;
    private const float StockJumpSpeed = 6.5f;
    private const float StockJumpMult = 1.1f;

    public static void ToggleLongArms() { LongArms = !LongArms; ApplyArms(); }
    public static void ToggleSpeedBoost() { SpeedBoost = !SpeedBoost; ApplySpeed(); }
    public static void ToggleNameTags() { NameTags = !NameTags; }

    public static void ApplyAll() { ApplyArms(); ApplySpeed(); }

    public static void ApplyArms()
    {
        Player.FalconArmScale = LongArms ? ArmScale : 1f;
    }

    public static void ApplySpeed()
    {
        Player p = Player.Instance;
        if (p == null) return;
        if (SpeedBoost) { p.maxJumpSpeed = JumpSpeed; p.jumpMultiplier = JumpMult; }
        else { p.maxJumpSpeed = StockJumpSpeed; p.jumpMultiplier = StockJumpMult; }
    }
}

// Keeps the options applied. THREE stock writers reset the jump
// pair - PhotonNetworkController on room join, VRRig from its speed
// array, VRRig from its own serialized fields - and the old 0.25 s
// re-assert tick lost that race half the time (Sans, round 31: "only
// works half the time"). Now asserted EVERY frame in LateUpdate,
// after every stock Update has run, writing only on drift. The rave
// manages the pair itself while it runs.
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
        if (p.maxJumpSpeed != FalconPlayerOptions.JumpSpeed) p.maxJumpSpeed = FalconPlayerOptions.JumpSpeed;
        if (p.jumpMultiplier != FalconPlayerOptions.JumpMult) p.jumpMultiplier = FalconPlayerOptions.JumpMult;
    }
}
