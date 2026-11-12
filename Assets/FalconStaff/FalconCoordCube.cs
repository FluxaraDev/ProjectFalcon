using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using Player = GorillaLocomotion.Player;

// Falcon Coordinates Cube: a map-editing measuring tool for staff.
// Turn it on in the staff hand menu (Move tab) and a 0.5 m cube appears
// in your right hand. Press A (right primary button) to drop it where
// you stand, press A again to pick it back up. Its exact world
// coordinates float above it, so positions for placing things later
// come from reading the cube - never from guessing.
public class FalconCoordCube : MonoBehaviour
{
    public bool CubeEnabled { get; private set; }
    public bool Held { get; private set; }
    public Vector3 CubePosition
    {
        get { return cubeRoot != null ? cubeRoot.transform.position : Vector3.zero; }
    }

    private GameObject cubeRoot;
    private Text labelText;
    private Font donorFont;
    private bool prevA;

    private static readonly Vector3 HeldOffset = new Vector3(0f, -0.02f, 0.12f);
    private static readonly Color CubeColor = new Color(1f, 0.55f, 0.1f, 1f);
    private static readonly Color LabelBg = new Color(0.05f, 0.05f, 0.08f, 0.85f);

    public void SetEnabled(bool on)
    {
        CubeEnabled = on;
        if (on)
        {
            EnsureCube();
            if (cubeRoot != null)
            {
                cubeRoot.SetActive(true);
                PickUp();
            }
        }
        else if (cubeRoot != null)
        {
            cubeRoot.SetActive(false);
        }
    }

    private void Update()
    {
        if (!CubeEnabled || cubeRoot == null || !cubeRoot.activeSelf) return;
        if (!FalconStaffRole.IsStaffOrHigher) { SetEnabled(false); return; }

        InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool a;
        right.TryGetFeatureValue(CommonUsages.primaryButton, out a);
        a = a || (ControllerInputPoller.instance != null && ControllerInputPoller.instance.rightControllerPrimaryButton);
        bool aEdge = a && !prevA;
        prevA = a;
        if (aEdge)
        {
            if (Held) Drop();
            else PickUp();
        }

        if (labelText != null)
        {
            if (labelText.font == null)
            {
                Font f = FindDonorFont();
                if (f != null) labelText.font = f;
            }
            Vector3 p = cubeRoot.transform.position;
            labelText.text = string.Format("{0:0.00}, {1:0.00}, {2:0.00}", p.x, p.y, p.z);
            if (Camera.main != null)
                labelText.transform.parent.rotation = Camera.main.transform.rotation;
        }
    }

    private void PickUp()
    {
        if (cubeRoot == null || Player.Instance == null || Player.Instance.rightHandTransform == null) return;
        cubeRoot.transform.SetParent(Player.Instance.rightHandTransform, false);
        cubeRoot.transform.localPosition = HeldOffset;
        cubeRoot.transform.localRotation = Quaternion.identity;
        Held = true;
    }

    private void Drop()
    {
        if (cubeRoot == null) return;
        cubeRoot.transform.SetParent(null, true);
        Held = false;
    }

    private Font FindDonorFont()
    {
        if (donorFont != null) return donorFont;
        foreach (Text t in FindObjectsOfType<Text>())
        {
            if (t != null && t.font != null) { donorFont = t.font; break; }
        }
        return donorFont;
    }

    private void EnsureCube()
    {
        if (cubeRoot != null) return;

        cubeRoot = new GameObject("FalconCoordCube");

        GameObject meshGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        meshGo.name = "Cube";
        meshGo.transform.SetParent(cubeRoot.transform, false);
        meshGo.transform.localScale = Vector3.one * 0.5f;
        Collider col = meshGo.GetComponent<Collider>();
        if (col != null) Destroy(col);
        MeshRenderer rend = meshGo.GetComponent<MeshRenderer>();
        if (rend != null) rend.material.color = CubeColor;

        // Floating coordinate readout above the cube (world-space canvas,
        // same legacy-Text approach as the hand menu - no TextMeshPro in
        // this build). Billboarded toward the main camera every frame.
        var labelRoot = new GameObject("CoordLabel");
        RectTransform lrt = labelRoot.AddComponent<RectTransform>();
        lrt.SetParent(cubeRoot.transform, false);
        lrt.localPosition = new Vector3(0f, 0.48f, 0f);
        lrt.sizeDelta = new Vector2(760f, 100f);
        Canvas cv = labelRoot.AddComponent<Canvas>();
        cv.renderMode = RenderMode.WorldSpace;
        CanvasScaler scaler = labelRoot.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 4f;
        labelRoot.transform.localScale = Vector3.one * 0.0016f;

        var bgGo = new GameObject("bg");
        RectTransform bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.SetParent(lrt, false);
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.sizeDelta = new Vector2(760f, 100f);
        Image bg = bgGo.AddComponent<Image>();
        bg.color = LabelBg;
        bg.raycastTarget = false;

        var txGo = new GameObject("txt");
        RectTransform txRt = txGo.AddComponent<RectTransform>();
        txRt.SetParent(lrt, false);
        txRt.anchorMin = txRt.anchorMax = txRt.pivot = new Vector2(0.5f, 0.5f);
        txRt.anchoredPosition = Vector2.zero;
        txRt.sizeDelta = new Vector2(760f, 100f);
        labelText = txGo.AddComponent<Text>();
        labelText.font = FindDonorFont();
        labelText.fontSize = 46;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        labelText.verticalOverflow = VerticalWrapMode.Overflow;
        labelText.raycastTarget = false;
        labelText.text = "0.00, 0.00, 0.00";
    }
}
