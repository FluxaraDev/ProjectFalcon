using UnityEngine;
using UnityEngine.XR;
using Player = GorillaLocomotion.Player;

// Thumbstick fly for staff (round 45, Sans). Toggle lives on the
// hand menu's Move tab. While on:
//   left stick   = move, in the direction your head points
//   right stick Y = rise / sink
// Movement writes Player.transform.position directly - the same
// mechanism FalconVoidGuard's respawn uses (device-verified in
// this project) - with the rigidbody's gravity off and velocity
// zeroed so the fall never accumulates while you hover. Toggling
// off hands gravity straight back.
public class FalconFly : MonoBehaviour
{
    public static bool Enabled;
    private const float Speed = 6f;

    private Rigidbody rb;
    private bool gravityOff;

    private void Update()
    {
        Player p = Player.Instance;
        if (p == null) return;
        if (rb == null) rb = p.GetComponent<Rigidbody>();

        if (!Enabled)
        {
            if (gravityOff && rb != null) { rb.useGravity = true; gravityOff = false; }
            return;
        }
        if (rb != null)
        {
            rb.useGravity = false;
            gravityOff = true;
            rb.velocity = Vector3.zero;
        }

        Vector2 left = ReadAxis(XRNode.LeftHand);
        Vector2 right = ReadAxis(XRNode.RightHand);
        Transform head = Camera.main != null ? Camera.main.transform : p.transform;
        Vector3 wish = head.forward * left.y + head.right * left.x + Vector3.up * right.y;
        if (wish.sqrMagnitude > 1f) wish.Normalize();
        if (wish.sqrMagnitude > 0.0001f)
            p.transform.position += wish * Speed * Time.deltaTime;
    }

    private static Vector2 ReadAxis(XRNode node)
    {
        InputDevice d = InputDevices.GetDeviceAtXRNode(node);
        Vector2 v;
        if (d.isValid && d.TryGetFeatureValue(CommonUsages.primary2DAxis, out v)) return v;
        return Vector2.zero;
    }
}
