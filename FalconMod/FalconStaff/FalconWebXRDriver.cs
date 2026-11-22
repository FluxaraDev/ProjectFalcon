// Project Falcon - WebXR side of the tracking bridge
// (round 91). Three pieces, all WebGL-only in effect:
//
//   FalconWebXRHost   - the GameObject named "FalconWebXR"
//                       the jslib SendMessage()s pose packets
//                       to. Parses the flat CSV packet and
//                       stores Unity-space poses on
//                       WebXRTrackingBridge.
//   FalconWebXRDriver - writes the bridge's hand poses into
//                       the SAME anchor transforms the
//                       Oculus stack writes on Quest
//                       (Player.leftHandTransform /
//                       rightHandTransform), so locomotion,
//                       VRRig and every consumer downstream
//                       cannot tell the difference.
//
// Both self-disable on any non-WebGL platform in Awake -
// on Quest this file is inert, byte for byte. Head-pose
// writing is deliberately NOT here yet: on Quest the head
// is driven through the camera rig chain, and writing the
// wrong node would break the view; hands are milestone 1,
// head joins when the session work is verified (round 92+).
using UnityEngine;
using GorillaLocomotion;

public class FalconWebXRHost : MonoBehaviour
{
    // Packet layout (from FalconWebXR.jslib), WebXR space:
    // head px,py,pz, qx,qy,qz,qw, left ..., right ...
    // 21 comma-separated floats. Converted here: position
    // (x, y, -z); rotation (x, y, -z, -w) - the standard
    // right-handed to Unity left-handed mapping. UNVERIFIED
    // on device until the first real session (milestone 2).
    public void OnPoses(string packet)
    {
        if (string.IsNullOrEmpty(packet)) return;
        string[] parts = packet.Split(',');
        if (parts.Length < 21) return;
        float[] f = new float[21];
        for (int i = 0; i < 21; i++)
            if (!float.TryParse(parts[i], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out f[i]))
                return;
        WebXRTrackingBridge.HeadPos = ConvPos(f, 0);
        WebXRTrackingBridge.HeadRot = ConvRot(f, 3);
        WebXRTrackingBridge.LeftPos = ConvPos(f, 7);
        WebXRTrackingBridge.LeftRot = ConvRot(f, 10);
        WebXRTrackingBridge.RightPos = ConvPos(f, 14);
        WebXRTrackingBridge.RightRot = ConvRot(f, 17);
        WebXRTrackingBridge.HasPose = true;
    }

    private static Vector3 ConvPos(float[] f, int i)
    {
        return new Vector3(f[i], f[i + 1], -f[i + 2]);
    }
    private static Quaternion ConvRot(float[] f, int i)
    {
        return new Quaternion(f[i], f[i + 1], -f[i + 2], -f[i + 3]);
    }
}

public class FalconWebXRDriver : MonoBehaviour
{
    private Player player;

    private void Awake()
    {
        if (!FalconTracking.IsWebGL) { enabled = false; return; }
        // The jslib addresses a GameObject named exactly
        // "FalconWebXR" - a dedicated child, so the shared
        // FalconMod host keeps its own name.
        var host = new GameObject("FalconWebXR");
        host.transform.SetParent(transform, false);
        host.AddComponent<FalconWebXRHost>();
#if UNITY_WEBGL && !UNITY_EDITOR
        FalconWebXR_Init();
#endif
    }

    private void LateUpdate()
    {
        if (!WebXRTrackingBridge.HasPose) return;
        if (player == null) player = Player.Instance;
        if (player == null) return;
        IXRTrackingBridge b = FalconTracking.Current;
        if (player.leftHandTransform != null)
        {
            player.leftHandTransform.localPosition = b.LeftHandPosition;
            player.leftHandTransform.localRotation = b.LeftHandRotation;
        }
        if (player.rightHandTransform != null)
        {
            player.rightHandTransform.localPosition = b.RightHandPosition;
            player.rightHandTransform.localRotation = b.RightHandRotation;
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void FalconWebXR_Init();
#endif
}
