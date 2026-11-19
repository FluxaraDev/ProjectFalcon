using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// Apply FalconMod (Sans, 2026-11-19, round 81) - EDITOR ONLY.
//
// The problem this solves: Falcon's scene furniture (mod call
// buttons, the wall sign, the staff board, the board texts) is
// spawned by scripts at runtime, so the project OPENED in the
// editor looks stock - you only see Falcon after pressing Play.
//
// "Apply FalconMod" in the top menu bar opens this window.
// "Load Objects" bakes the furniture INTO the open scene:
// the objects appear in the hierarchy, at their real positions,
// with their real names, and the runtime scripts ADOPT them on
// Play instead of spawning duplicates (each spawner checks for
// its baked object first - FalconModCallButtons.AdoptBaked,
// FalconWallSign.TryBuild, FalconBoardTexts.ApplyOnce).
// "Remove Baked Objects" deletes the baked furniture again
// (board TEXT rewrites stay - they are identical to what the
// runtime applies on boot anyway).
//
// Player-attached systems (hand menu, name tags, coord cube,
// fly, rave, RTX) have no scene furniture and are NOT baked -
// they still arrive at runtime, as designed.
//
// The constants below MIRROR the runtime files (button spots:
// FalconModCallButtons; sign: FalconWallSign; board texts and
// staff corners: FalconBoardTexts). Keep them in sync - the
// adopt-by-name contract depends on the names matching.
// This file lives in an Editor folder: it never ships in a
// player build, but it MUST keep compiling - UBA builds run
// the editor, and a broken editor script fails the build.
// ============================================================
public class FalconBakeWindow : EditorWindow
{
    private string report = "Nothing baked yet this session.";

    // ---- mirrors of FalconModCallButtons ----
    private static readonly string[] BtnNames = { "Stump", "Forest", "Canyons", "City", "City 2", "Mountains", "Competitive" };
    private static readonly Vector3[] BtnSpots =
    {
        new Vector3(-67.64f, 11.59f, -80.34f),
        new Vector3(-62.29f, 2.69f, -60.36f),
        new Vector3(-88.56f, 9.85f, -108.03f),
        new Vector3(-59.96f, 16.18f, -106.66f),
        new Vector3(-48.05f, 17.60f, -118.56f),
        new Vector3(-22.90f, 17.68f, -101.52f),
        new Vector3(-45.89f, 15.35f, -117.66f),
    };

    // ---- mirrors of FalconWallSign ----
    private static readonly Vector3 SignPosition = new Vector3(-147.43f, 31.63f, -93.09f);
    private const string SignText = "If lobbies don't work change your name";

    // ---- mirrors of FalconBoardTexts ----
    private const string CodeOfConduct =
        "-NO RACISM, SEXISM, HOMOPHOBIA, TRANSPHOBIA, OR OTHER BIGOTRY\n\n" +
        "-NO CHEATS, MODS, GLITCHES OR EXPLOITS. OUT OF BOUNDS IS CHEATING\n\n" +
        "-DO NOT HARASS OTHER PLAYERS OR INTENTIONALLY MAKE THEM UNCOMFORTABLE\n\n" +
        "-DO NOT TROLL OR GRIEF LOBBIES BY BEING UNCATCHABLE OR BY ESCAPING THE MAP. TRY TO MAKE SURE EVERYONE IS HAVING FUN\n\n" +
        "-IF SOMEONE IS BREAKING THIS CODE, PLEASE REPORT THEM\n\n" +
        "-PLEASE BE NICE GORILLAS AND HAVE A GOOD TIME";
    private const string Motd =
        "<b><color=#FFD75E>Welcome</color></b>\n" +
        "<b><color=#FFD75E>To Project Falcon</color></b>\n" +
        "<color=#9FD8FF>(Developed by fluxtech)</color>\n" +
        "Main founders: <color=#FF9FF3>FluxDev</color>, <color=#7CFFB2>Nico</color>, <color=#FFA07A>Jacob</color>, <color=#C792FF>Royal</color>\n" +
        "<color=#FFE27A>Thanks for playing!</color>";
    private const string StaffText =
        "<b><color=#FFD75E>OUR STAFF</color></b>\n\n" +
        "<color=#FFD75E>MAIN FOUNDERS</color>\n" +
        "FLUXDEV - ROYAL\n" +
        "NICO - JACOB\n\n" +
        "<color=#FFD75E>HEADADMINS</color>\n" +
        "TEST\n\n" +
        "<color=#FFD75E>ADMINS</color>\n" +
        "TEST\n\n" +
        "<color=#FFD75E>MODS</color>\n" +
        "TEST\n\n" +
        "<color=#FFD75E>TRIAL MODS</color>\n" +
        "TEST\n\n" +
        "<color=#FFD75E>FIRST PLAYER</color>\n" +
        "TEST";
    private static readonly Vector3 StaffTopCorner = new Vector3(-68.52f, 12.43f, -84.46f);
    private static readonly Vector3 StaffBottomCorner = new Vector3(-68.63f, 11.87f, -83.76f);

    [MenuItem("Apply FalconMod")]
    public static void Open()
    {
        GetWindow<FalconBakeWindow>("Apply FalconMod");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Apply FalconMod", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Bakes Falcon's scene furniture into the open scene so you can see and edit it " +
            "without pressing Play: the 7 mod call buttons, the wall sign, the staff board, " +
            "and the board texts (MOTD / code of conduct). On Play, the runtime scripts adopt " +
            "the baked objects instead of spawning their own - no doubles.\n\n" +
            "Player-attached systems (hand menu, name tags, fly, rave, RTX) are not scene " +
            "furniture and still arrive at runtime.\n\n" +
            "Save the scene yourself when you're happy (Ctrl+S). Undo works.",
            MessageType.Info);
        if (GUILayout.Button("Load Objects", GUILayout.Height(36f)))
            LoadObjects();
        if (GUILayout.Button("Remove Baked Objects"))
            RemoveBaked();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Last run:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(report, EditorStyles.wordWrappedLabel);
    }

    private void LoadObjects()
    {
        var lines = new List<string>();
        int made = 0, skipped = 0;

        // ---------- mod call buttons ----------
        Font btnFont = FindProjectFont("Utopium");
        for (int i = 0; i < BtnNames.Length; i++)
        {
            string rootName = "FalconModCallButton_" + BtnNames[i];
            if (GameObject.Find(rootName) != null) { skipped++; continue; }
            var root = new GameObject(rootName);
            root.transform.position = BtnSpots[i];
            Undo.RegisterCreatedObjectUndo(root, "Bake mod call button");

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform, false);
            cube.transform.localScale = Vector3.one * 0.15f;
            var col = cube.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            var rend = cube.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                var cm = new Material(rend.sharedMaterial);
                cm.color = new Color(0.85f, 0.85f, 0.88f);
                rend.sharedMaterial = cm;
            }

            MakeLabel(root.transform, 0.26f, 0.05f, "MOD CALL", btnFont, true);
            MakeLabel(root.transform, 0.52f, 0.042f, "fake calls = ban\n25 seconds of ingame audio is sent to us", btnFont, false);
            made++;
        }
        lines.Add("mod call buttons: " + made + " baked, " + skipped + " already present");
        if (btnFont == null) lines.Add("WARNING: Utopium font asset not found - button labels use the default font (runtime re-borrows the rig font only for its own labels; set the font on the baked TextMeshes if they look wrong)");

        // ---------- wall sign ----------
        if (GameObject.Find("FalconWallSign") != null)
        {
            lines.Add("wall sign: already present");
        }
        else
        {
            var signRoot = new GameObject("FalconWallSign");
            signRoot.transform.position = SignPosition;
            Undo.RegisterCreatedObjectUndo(signRoot, "Bake wall sign");

            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "SignBacking";
            var bc = back.GetComponent<Collider>(); if (bc != null) DestroyImmediate(bc);
            back.transform.SetParent(signRoot.transform, false);
            back.transform.localPosition = new Vector3(0f, 0f, -0.04f);
            back.transform.localScale = new Vector3(9.2f, 1.6f, 0.06f);
            var br = back.GetComponent<MeshRenderer>();
            if (br != null)
            {
                var bm = new Material(br.sharedMaterial);
                bm.color = new Color(0.07f, 0.07f, 0.09f);
                br.sharedMaterial = bm;
            }

            var textGo = new GameObject("SignText");
            textGo.transform.SetParent(signRoot.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            var tmp = textGo.AddComponent<TextMeshPro>();
            var tmpFont = FindSceneTmpFont();
            if (tmpFont != null) tmp.font = tmpFont;
            else lines.Add("WARNING: no TMP font found in the scene - the sign text needs a font set by hand");
            tmp.text = SignText;
            tmp.fontSize = 8f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            lines.Add("wall sign: baked");
        }

        // ---------- board texts + staff board ----------
        Text nearestCoc = null;
        float nearestDist = 0f;
        int texts = 0;
        foreach (Text t in Resources.FindObjectsOfTypeAll<Text>())
        {
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            if (t.text != null && t.text.Trim() == "GORILLA CODE OF CONDUCT")
            {
                Undo.RecordObject(t, "Bake board header");
                t.text = "FALCON CODE OF CONDUCT";
                texts++;
            }
            else if (t.text != null && t.text.StartsWith("-NO RACISM"))
            {
                Undo.RecordObject(t, "Bake CoC body");
                t.text = CodeOfConduct;
                FitText(t);
                texts++;
                float dc = Vector3.Distance(t.transform.position, StaffCenter());
                if (nearestCoc == null || dc < nearestDist) { nearestCoc = t; nearestDist = dc; }
            }
            else if (t.gameObject.name == "motdtext" || (t.text != null && t.text.Trim() == "-LOADING-"))
            {
                Undo.RecordObject(t, "Bake MOTD");
                t.supportRichText = true;
                t.text = Motd;
                FitText(t);
                texts++;
            }
        }
        lines.Add("board texts: " + texts + " rewritten");

        if (GameObject.Find("FalconStaffText") != null)
        {
            lines.Add("staff board: already present");
        }
        else if (nearestCoc != null && BuildStaffBoard(nearestCoc))
        {
            lines.Add("staff board: baked next to the CoC board");
        }
        else
        {
            lines.Add("staff board: SKIPPED (CoC board or its frame image not found)");
        }

        // ---------- marker ----------
        if (GameObject.Find("FalconBaked") == null)
        {
            var marker = new GameObject("FalconBaked");
            Undo.RegisterCreatedObjectUndo(marker, "Bake marker");
        }
        lines.Add("marker: FalconBaked (this scene carries baked Falcon objects)");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        report = string.Join("\n", lines.ToArray());
        Debug.Log("[Apply FalconMod] " + report);
        Repaint();
    }

    private void RemoveBaked()
    {
        int killed = 0;
        for (int i = 0; i < BtnNames.Length; i++)
        {
            var go = GameObject.Find("FalconModCallButton_" + BtnNames[i]);
            if (go != null) { Undo.DestroyObjectImmediate(go); killed++; }
        }
        foreach (string n in new[] { "FalconWallSign", "FalconStaffText", "FalconStaffBoardFrame", "FalconBaked" })
        {
            var go = GameObject.Find(n);
            if (go != null) { Undo.DestroyObjectImmediate(go); killed++; }
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        report = "Removed " + killed + " baked objects. Board texts stay rewritten (same wording the runtime applies on boot). Runtime spawning is back to normal.";
        Repaint();
    }

    // ---------- helpers (mirrors of the runtime logic) ----------

    private static void MakeLabel(Transform parent, float y, float charSize, string text, Font font, bool active)
    {
        var go = new GameObject("txt");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, y, 0f);
        var tm = go.AddComponent<TextMesh>();
        if (font != null)
        {
            tm.font = font;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = font.material;
        }
        tm.fontSize = 64;
        tm.characterSize = charSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
        tm.text = text;
        go.SetActive(active);
    }

    private static Font FindProjectFont(string namePart)
    {
        foreach (string guid in AssetDatabase.FindAssets(namePart + " t:Font"))
        {
            var f = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
            if (f != null) return f;
        }
        return null;
    }

    private static TMP_FontAsset FindSceneTmpFont()
    {
        foreach (TextMeshPro tmp in Resources.FindObjectsOfTypeAll<TextMeshPro>())
            if (tmp != null && tmp.gameObject.scene.IsValid() && tmp.font != null) return tmp.font;
        foreach (TextMeshProUGUI ugui in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
            if (ugui != null && ugui.gameObject.scene.IsValid() && ugui.font != null) return ugui.font;
        return null;
    }

    private static void FitText(Text t)
    {
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 10;
        t.resizeTextMaxSize = t.fontSize;
    }

    private static Vector3 StaffCenter()
    {
        return (StaffTopCorner + StaffBottomCorner) * 0.5f;
    }

    // Mirror of FalconBoardTexts.BuildStaffBoard.
    private static bool BuildStaffBoard(Text source)
    {
        Canvas canvas = source.GetComponentInParent<Canvas>();
        if (canvas == null) return false;
        Image boardImage = null;
        foreach (Image img in canvas.GetComponentsInChildren<Image>(true))
        {
            if (img.gameObject.name == "CodeOfConduct") { boardImage = img; break; }
            if (boardImage == null && img.rectTransform.sizeDelta.x > 100f) boardImage = img;
        }
        if (boardImage == null) return false;

        Vector3 horiz = StaffTopCorner - StaffBottomCorner;
        horiz.y = 0f;
        float targetWidth = horiz.magnitude;
        Vector3 normal = new Vector3(-horiz.z, 0f, horiz.x).normalized;
        Vector3 center = StaffCenter() + normal * 0.03f;

        float worldWidth = boardImage.rectTransform.sizeDelta.x * boardImage.transform.lossyScale.x;
        float fit = worldWidth > 0.01f ? targetWidth / worldWidth : 1f;
        if (fit < 0.3f) fit = 0.3f;
        if (fit > 1.5f) fit = 1.5f;

        float yawT = Mathf.Atan2(normal.x, normal.z);
        Vector3 fwd = boardImage.transform.forward;
        float yawO = Mathf.Atan2(fwd.x, fwd.z);
        Quaternion dq = Quaternion.Euler(0f, (yawT - yawO) * Mathf.Rad2Deg, 0f);

        GameObject imgClone = Instantiate(boardImage.gameObject, boardImage.transform.parent);
        imgClone.name = "FalconStaffBoardFrame";
        imgClone.transform.position = center;
        imgClone.transform.rotation = dq * boardImage.transform.rotation;
        imgClone.transform.localScale = boardImage.transform.localScale * fit;
        Undo.RegisterCreatedObjectUndo(imgClone, "Bake staff board frame");

        GameObject textClone = Instantiate(source.gameObject, source.transform.parent);
        textClone.name = "FalconStaffText";
        textClone.transform.position = center + dq * ((source.transform.position - boardImage.transform.position) * fit);
        textClone.transform.rotation = dq * source.transform.rotation;
        textClone.transform.localScale = source.transform.localScale * fit;
        Undo.RegisterCreatedObjectUndo(textClone, "Bake staff board text");
        Text ct = textClone.GetComponent<Text>();
        if (ct != null)
        {
            ct.supportRichText = true;
            ct.text = StaffText;
            FitText(ct);
        }
        return true;
    }
}
