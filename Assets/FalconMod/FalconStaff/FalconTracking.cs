// Project Falcon - tracking bridge (round 91, Sans's design).
//
// ONE interface, two implementations. The game never asks a
// platform API for poses: it reads the anchor transforms
// (Player.leftHandTransform / rightHandTransform, the head
// anchor) that SOMETHING writes every frame. On Quest that
// something is the Oculus stack, exactly as today - the Quest
// build is untouched. On WebGL a driver (FalconWebXRDriver)
// writes the same anchors from this bridge instead.
//
// FalconTracking.Current picks the implementation by RUNTIME
// platform (the loader's PLATFORM detection): WebGLPlayer
// gets the WebXR bridge, everything else gets the Quest
// reader. The Quest reader below is a pure reader of the
// values the game already produces (the same OVRInput /
// OVRPlugin calls FalconPreds uses) - shipping it changes
// nothing on Quest.
using UnityEngine;

public interface IXRTrackingBridge
{
    Vector3 HeadPosition { get; }
    Quaternion HeadRotation { get; }

    Vector3 LeftHandPosition { get; }
    Quaternion LeftHandRotation { get; }

    Vector3 RightHandPosition { get; }
    Quaternion RightHandRotation { get; }
}

public static class FalconTracking
{
    private static IXRTrackingBridge current;
    public static bool IsWebGL
    {
        get { return Application.platform == RuntimePlatform.WebGLPlayer; }
    }
    public static IXRTrackingBridge Current
    {
        get
        {
            if (current == null)
                current = IsWebGL
                    ? (IXRTrackingBridge)new WebXRTrackingBridge()
                    : new QuestTrackingBridge();
            return current;
        }
    }
}

// Quest / Android: poses straight from the Oculus stack, in
// tracking space - identical values to what the anchors
// already carry, so any consumer switching to the bridge
// sees no change.
public class QuestTrackingBridge : IXRTrackingBridge
{
    // OVRPlugin.GetNodePose in THIS SDK returns a Posef
    // (verified in the project's own OVRPlugin.cs, line
    // 3867 - the 4-argument out overload I first wrote
    // does not exist here; build log 2026-11-21, CS1501).
    public Vector3 HeadPosition
    {
        get { return OVRPlugin.GetNodePose(OVRPlugin.Node.EyeCenter, OVRPlugin.Step.Render).Position; }
    }
    public Quaternion HeadRotation
    {
        get { return OVRPlugin.GetNodePose(OVRPlugin.Node.EyeCenter, OVRPlugin.Step.Render).Orientation; }
    }
    public Vector3 LeftHandPosition { get { return OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch); } }
    public Quaternion LeftHandRotation { get { return OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTouch); } }
    public Vector3 RightHandPosition { get { return OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch); } }
    public Quaternion RightHandRotation { get { return OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch); } }
}

// WebGL: the latest poses pushed in from the browser's WebXR
// session by FalconWebXR.jslib (through the FalconWebXR host
// object below). Until a session runs, the poses are
// identity - the game loads and stands still, which is
// milestone 1. Coordinate conversion (WebXR right-handed
// z-back to Unity left-handed z-forward) happens in the
// host parser, so these getters speak Unity/tracking space
// exactly like the Quest reader does.
public class WebXRTrackingBridge : IXRTrackingBridge
{
    public static Vector3 HeadPos, LeftPos, RightPos;
    public static Quaternion HeadRot = Quaternion.identity;
    public static Quaternion LeftRot = Quaternion.identity;
    public static Quaternion RightRot = Quaternion.identity;
    public static bool HasPose;

    public Vector3 HeadPosition { get { return HeadPos; } }
    public Quaternion HeadRotation { get { return HeadRot; } }
    public Vector3 LeftHandPosition { get { return LeftPos; } }
    public Quaternion LeftHandRotation { get { return LeftRot; } }
    public Vector3 RightHandPosition { get { return RightPos; } }
    public Quaternion RightHandRotation { get { return RightRot; } }
}
