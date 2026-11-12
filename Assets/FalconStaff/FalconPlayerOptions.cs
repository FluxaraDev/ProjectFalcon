using UnityEngine;
using GorillaLocomotion;

// Project Falcon player options (Sans, 2026-11-12): the computer's
// ITEMS page, available to EVERY player - not staff-gated. Three
// toggles, all default ON:
//   option 1: long arms (INTERIM: reach cap removed - see below)
//   option 2: speed boost (jump 9.1 / mult 1.54 - Sans's 1.4x pair;
//             the old menu's 12 / 2.0 was "way too high")
//   option 3: optional preds (FalconPreds, 0.06 default)
//
// LONG ARMS HISTORY: round 6 scaled the head->hand offset inside
// Player.Current*HandPosition (FalconArmScale). Build #10 verdict
// (Sans): it broke movement and detached the camera - those methods
// ARE the locomotion (distanceTraveled = current - last hand pos),
// and the last positions are seeded from the RAW transforms, so the
// scaled reads poisoned the physics from the first frame. Round 12
// reverted Player.cs completely. Until Sans picks the real route
// (whole-player scale 1.4 vs visual-only arm stretch), option 1 just
// removes the reach cap, the original scene-edit spec.
public static class FalconPlayerOptions
{
    public static bool LongArms = true;
    public static bool SpeedBoost = true;
    public static bool Preds = true;

    public const float ArmCapOff = 10000f;
    public const float ArmCapStock = 1.5f;
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
        Player p = Player.Instance;
        if (p == null) return;
        p.maxArmLength = LongArms ? ArmCapOff : ArmCapStock;
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

// Keeps the options applied: game code resets the jump pair (and can
// reset the arm cap) on room joins, so while a toggle is ON the
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
        if (FalconPlayerOptions.LongArms) FalconPlayerOptions.ApplyArms();
    }
}
