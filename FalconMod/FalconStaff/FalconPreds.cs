using UnityEngine;
using Player = GorillaLocomotion.Player;

// FalconPreds (round 90, Sans): controller pose prediction,
// adapted from the OculusPreds script Sans supplied (from
// Ires Tag, per his note). EXPERIMENTAL, DEFAULT OFF.
//
// What it does: while on, each frame the local player's
// hand targets are placed at the tracked controller pose
// extrapolated forward by the prediction amount (position
// += velocity * t, rotation += angular velocity * t) -
// 0 ms = no prediction (arms barely flick), 75 ms = a lot
// of flick, default 20 ms.
//
// Two deliberate deviations from the supplied script, both
// from its own setup notes ("THE HEAD PART IS OPTIONAL AND
// YOU DO NOT NEED IT BECAUSE DEPENDING ON THE PROJECT YOU
// USE IT WILL BREAK"):
//   - the head target is NEVER written;
//   - writes happen in LateUpdate and ONLY while the
//     option is on. When off, the component touches
//     nothing, so a relaunch (which always starts with
//     preds off) hands the transforms straight back to
//     the rig - no rig troubles, no stuck controllers.
public class FalconPreds : MonoBehaviour
{
    private void LateUpdate()
    {
        if (!FalconPlayerOptions.Preds) return;
        Player p = Player.Instance;
        if (p == null) return;
        float t = FalconPlayerOptions.PredsMs / 1000f;
        Apply(p.leftHandTransform, OVRInput.Controller.LTouch, t);
        Apply(p.rightHandTransform, OVRInput.Controller.RTouch, t);
    }

    private static void Apply(Transform target, OVRInput.Controller c, float t)
    {
        if (target == null) return;
        Vector3 pos = OVRInput.GetLocalControllerPosition(c);
        Vector3 vel = OVRInput.GetLocalControllerVelocity(c);
        Quaternion rot = OVRInput.GetLocalControllerRotation(c);
        Vector3 angVel = OVRInput.GetLocalControllerAngularVelocity(c);
        target.localPosition = pos + vel * t;
        target.localRotation = rot * Quaternion.Euler(angVel * Mathf.Rad2Deg * t);
    }
}
