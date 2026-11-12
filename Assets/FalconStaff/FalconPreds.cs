using UnityEngine;

// Optional Preds (Sans, 2026-11-12): in-game pose prediction using the
// OculusPreds math (velocity * predictionTime added to the tracked
// poses), applied ONLY to the local rig's visual hand transforms.
//
// ROUND 11 SAFETY REWRITE: build #10 wrote the offsets into
// Player.leftHandTransform/rightHandTransform and, for the head, into
// Player.headCollider - which in this build sits on the MAIN CAMERA
// GameObject itself (Main Camera < TurnParent < GorillaPlayer).
// Shoving the camera's pose detached the camera from the player
// (camera under the map, model above, locomotion reads the same
// transform for the body). Nothing may write the camera transform.
// The rig re-derives its hand transforms from the tracked anchors
// every frame, so offsets here are visual-only, self-erasing, and
// can never feed locomotion. Head prediction is NOT possible from a
// script in this game - the only head transform is the camera; head
// preds are the runtime's job (ADB setprops).
[DefaultExecutionOrder(200)]
public class FalconPreds : MonoBehaviour
{
    public static bool Enabled;
    public static float PredictionTime = 0.06f;

    private void LateUpdate()
    {
        if (!Enabled || PredictionTime <= 0f) return;
        if (GorillaTagger.Instance == null) return;
        VRRig rig = GorillaTagger.Instance.offlineVRRig;
        if (rig == null) return;

        float t = PredictionTime;
        if (rig.leftHandTransform != null)
        {
            rig.leftHandTransform.localPosition += OVRInput.GetLocalControllerVelocity(OVRInput.Controller.LTouch) * t;
            rig.leftHandTransform.localRotation *= Quaternion.Euler(OVRInput.GetLocalControllerAngularVelocity(OVRInput.Controller.LTouch) * Mathf.Rad2Deg * t);
        }
        if (rig.rightHandTransform != null)
        {
            rig.rightHandTransform.localPosition += OVRInput.GetLocalControllerVelocity(OVRInput.Controller.RTouch) * t;
            rig.rightHandTransform.localRotation *= Quaternion.Euler(OVRInput.GetLocalControllerAngularVelocity(OVRInput.Controller.RTouch) * Mathf.Rad2Deg * t);
        }
    }
}
