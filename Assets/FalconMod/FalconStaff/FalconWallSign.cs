using TMPro;
using UnityEngine;

// Project Falcon wall sign (Sans, 2026-11-13): a fixed help sign at
// his coordinates with the one fix that un-breaks lobbies. Text:
// "If lobbies don't work change your name". The font is the game's own
// TMP font, borrowed at runtime from one of the game's text objects -
// the same font every other game text uses. The sign yaws to face the
// viewer so it reads from any side.
public class FalconWallSign : MonoBehaviour
{
    private static readonly Vector3 SignPosition = new Vector3(-147.43f, 31.63f, -93.09f);
    private GameObject signRoot;
    private float nextTry;

    private void Update()
    {
        if (signRoot == null)
        {
            if (Time.time < nextTry) return;
            nextTry = Time.time + 2f;
            TryBuild();
            return;
        }
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 to = cam.transform.position - signRoot.transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            signRoot.transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
    }

    private void TryBuild()
    {
        TMP_FontAsset font = null;
        foreach (TextMeshPro tmp3d in FindObjectsOfType<TextMeshPro>())
        {
            if (tmp3d != null && tmp3d.font != null) { font = tmp3d.font; break; }
        }
        if (font == null)
        {
            foreach (TextMeshProUGUI ugui in FindObjectsOfType<TextMeshProUGUI>())
            {
                if (ugui != null && ugui.font != null) { font = ugui.font; break; }
            }
        }
        if (font == null) return;

        signRoot = new GameObject("FalconWallSign");
        signRoot.transform.position = SignPosition;

        var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
        back.name = "SignBacking";
        var bc = back.GetComponent<Collider>(); if (bc != null) Destroy(bc);
        back.transform.SetParent(signRoot.transform, false);
        back.transform.localPosition = new Vector3(0f, 0f, -0.04f);
        back.transform.localScale = new Vector3(9.2f, 1.6f, 0.06f);
        var br = back.GetComponent<MeshRenderer>();
        if (br != null)
        {
            var bm = new Material(br.sharedMaterial);
            bm.color = new Color(0.07f, 0.07f, 0.09f);
            br.material = bm;
        }

        var textGo = new GameObject("SignText");
        textGo.transform.SetParent(signRoot.transform, false);
        textGo.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        var tmp = textGo.AddComponent<TextMeshPro>();
        tmp.font = font;
        tmp.text = "If lobbies don't work change your name";
        tmp.fontSize = 8f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.ForceMeshUpdate();
        float w = tmp.textBounds.size.x;
        if (w > 0.1f)
        {
            float sc = Mathf.Clamp(8.4f / w, 0.2f, 3f);
            textGo.transform.localScale = new Vector3(sc, sc, sc);
        }
    }
}
