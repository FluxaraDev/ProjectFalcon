using UnityEngine;
using GorillaLocomotion;

// Project Falcon player options (Sans, 2026-11-12): the computer's
// ITEMS page, available to EVERY player - not staff-gated. Three
// toggles, all default ON:
//   option 1: long arms (FalconArmScale 1.4x reach)
//   option 2: speed boost (jump 9.1 / mult 1.54 - Sans's 1.4x pair;
//             the old menu's 12 / 2.0 was "way too high")
//   option 3: optional preds (FalconPreds, 0.06 default)
public static class FalconPlayerOptions
{
    public static bool LongArms = true;
    public static bool SpeedBoost = true;
    public static bool Preds = true;

    public const float ArmScale = 1.4f;
    public const float JumpSpeed = 9.1f;
    public const float JumpMult = 1.54f;
    private const float StockJumpSpeed = 6.5f;
    private const float StockJumpMult = 1.1f;

    public static void ToggleLongArms() { LongArms = !LongArms; ApplyArms(); }
    public static void ToggleSpeedBoost() { SpeedBoost = !SpeedBoost; ApplySpeed(); }
    public static void TogglePreds() { Preds = !Preds; ApplyPreds(); }

    public static void ApplyAll() { ApplyArms(); ApplySpeed(); ApplyPreds(); }

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

    public static void ApplyPreds()
    {
        FalconPreds.Enabled = Preds;
    }
}

// Keeps the options applied: PhotonNetworkController resets the jump
// pair to stock on every room join, so while speed boost is ON the
// driver re-asserts it on a slow tick.
public class FalconOptionsDriver : MonoBehaviour
{
    private float next;

    private void Start()
    {
        FalconPlayerOptions.ApplyAll();
    }

    private void Update()
    {
        if (Time.time < next) return;
        next = Time.time + 0.25f;
        if (FalconPlayerOptions.SpeedBoost) FalconPlayerOptions.ApplySpeed();
    }
}
