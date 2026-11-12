using UnityEngine;
using GorillaLocomotion;

// Optional Preds (Sans, 2026-11-12): in-game pose prediction using the
// OculusPreds math (velocity * predictionTime added to the tracked
// poses). Sans's target: prediction that shows up on OVR Metrics -
// build #10 is the test of that claim; the author's value is 0.06,
// said to be about 50 preds at 72 Hz.
//
// Safety shape: offsets are applied in LateUpdate (after locomotion
// has read the clean tracking poses) and restored first thing in
// Update (execution order -100, before Player.Update), so movement
// physics never consumes a predicted pose and nothing accumulates.
[DefaultExecutionOrder(-100)]
public class FalconPreds : MonoBehaviour
{
    public static bool Enabled;
    public static float PredictionTime = 0.06f;

    private Transform head;
    private Transform leftHand;
    private Transform rightHand;
    private Vector3 headPos, leftPos, rightPos;
    private Quaternion headRot, leftRot, rightRot;
    private bool applied;

    private void Update()
    {
        if (applied) Restore();
    }

    private void LateUpdate()
    {
        if (!Enabled || PredictionTime <= 0f)
        {
            if (applied) Restore();
            return;
        }
        Player p = Player.Instance;
        if (p == null) return;
        if (head == null)
        {
            if (p.headCollider == null || p.leftHandTransform == null || p.rightHandTransform == null) return;
            head = p.headCollider.transform;
            leftHand = p.leftHandTransform;
            rightHand = p.rightHandTransform;
        }

        headPos = head.localPosition; headRot = head.localRotation;
        leftPos = leftHand.localPosition; leftRot = leftHand.localRotation;
        rightPos = rightHand.localPosition; rightRot = rightHand.localRotation;

        float t = PredictionTime;

        OVRPlugin.Vector3f hv = OVRPlugin.GetNodeVelocity(OVRPlugin.Node.Head, OVRPlugin.Step.Render);
        OVRPlugin.Vector3f hav = OVRPlugin.GetNodeAngularVelocity(OVRPlugin.Node.Head, OVRPlugin.Step.Render);
        head.localPosition += new Vector3(hv.x, hv.y, hv.z) * t;
        head.localRotation *= Quaternion.Euler(new Vector3(hav.x, hav.y, hav.z) * Mathf.Rad2Deg * t);

        leftHand.localPosition += OVRInput.GetLocalControllerVelocity(OVRInput.Controller.LTouch) * t;
        leftHand.localRotation *= Quaternion.Euler(OVRInput.GetLocalControllerAngularVelocity(OVRInput.Controller.LTouch) * Mathf.Rad2Deg * t);

        rightHand.localPosition += OVRInput.GetLocalControllerVelocity(OVRInput.Controller.RTouch) * t;
        rightHand.localRotation *= Quaternion.Euler(OVRInput.GetLocalControllerAngularVelocity(OVRInput.Controller.RTouch) * Mathf.Rad2Deg * t);

        applied = true;
    }

    private void Restore()
    {
        if (head != null) { head.localPosition = headPos; head.localRotation = headRot; }
        if (leftHand != null) { leftHand.localPosition = leftPos; leftHand.localRotation = leftRot; }
        if (rightHand != null) { rightHand.localPosition = rightPos; rightHand.localRotation = rightRot; }
        applied = false;
    }

    private void OnDisable()
    {
        if (applied) Restore();
    }
}
