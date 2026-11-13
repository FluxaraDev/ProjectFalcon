using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// FalconBoardTexts: in-code board text overrides applied at startup.
// The code-of-conduct boards are static scene Text objects, and the MOTD
// board is fed by a title-data path that is stripped in this export (it
// sat on "-LOADING-" forever in build #8). Rewriting both here keeps the
// Falcon wording shippable as a small script instead of an 80 MB
// scene-file commit.
public class FalconBoardTexts : MonoBehaviour
{
    // Tightened 2026-11-12 at Sans's direction: glitches, exploits and
    // getting out of bounds explicitly count as cheating. Round 5: the
    // cheats line is compressed - the round-2 wording overflowed the
    // physical board on device.
    public const string CodeOfConduct =
        "-NO RACISM, SEXISM, HOMOPHOBIA, TRANSPHOBIA, OR OTHER BIGOTRY\n\n" +
        "-NO CHEATS, MODS, GLITCHES OR EXPLOITS. OUT OF BOUNDS IS CHEATING\n\n" +
        "-DO NOT HARASS OTHER PLAYERS OR INTENTIONALLY MAKE THEM UNCOMFORTABLE\n\n" +
        "-DO NOT TROLL OR GRIEF LOBBIES BY BEING UNCATCHABLE OR BY ESCAPING THE MAP. TRY TO MAKE SURE EVERYONE IS HAVING FUN\n\n" +
        "-IF SOMEONE IS BREAKING THIS CODE, PLEASE REPORT THEM\n\n" +
        "-PLEASE BE NICE GORILLAS AND HAVE A GOOD TIME";

    // Same text as the PlayFab Title Data MOTD (kept there too), with
    // real newlines for direct display.
    public const string Motd =
        "<b><color=#FFD75E>Welcome</color></b>\n" +
        "<b><color=#FFD75E>To Project Falcon</color></b>\n" +
        "<color=#9FD8FF>(Developed by fluxtech)</color>\n" +
        "Main founders: <color=#FF9FF3>FluxDev</color>, <color=#7CFFB2>Nico</color>, <color=#FFA07A>Jacob</color>, <color=#C792FF>Royal</color>\n" +
        "<color=#FFE27A>Thanks for playing!</color>";

    // Staff board (round 25, Sans 2026-11-13): a duplicate of the CoC
    // board - green frame image + text - placed on the stump computer
    // wall. Sans measured two diagonal corners with the coord cube:
    // top (-68.52, 12.43, -84.46), bottom (-68.63, 11.87, -83.76).
    // The clone is scaled so the green area spans the corners' width
    // (0.71 m); height follows the board's own aspect. Placeholder
    // names are TEST until Sans fills the roster.
    public const string StaffText =
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
    private static bool staffBoardBuilt;
    private Text nearestCocText;
    private float nearestCocDist;


    private void Start()
    {
        StartCoroutine(ApplyBoards());
    }

    // Several passes over the first seconds: other board components get
    // their say at startup too (fallbacks, loading text), and the Falcon
    // wording must be the last word.
    private IEnumerator ApplyBoards()
    {
        float[] delays = { 0.2f, 1.5f, 4f, 8f };
        foreach (float d in delays)
        {
            yield return new WaitForSeconds(d);
            ApplyOnce();
        }
    }

    // The board rects are sized for the stock wording; the Falcon
    // cheats line pushed the last lines off the bottom edge (Sans's
    // build #8 screenshot: "-PLEASE BE NICE GORILLAS..." never made it
    // onto the board). Best-fit shrinks the rendered size only as much
    // as the rect requires - every word stays.
    private static void FitText(Text t)
    {
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 10;
        t.resizeTextMaxSize = t.fontSize;
    }

    private void ApplyOnce()
    {
        foreach (Text t in Resources.FindObjectsOfTypeAll<Text>())
        {
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            if (t.text != null && t.text.StartsWith("-NO RACISM"))
            {
                t.text = CodeOfConduct;
                FitText(t);
                float dc = Vector3.Distance(t.transform.position, StaffCenter());
                if (nearestCocText == null || dc < nearestCocDist)
                {
                    nearestCocText = t;
                    nearestCocDist = dc;
                }
            }
            else if (t.gameObject.name == "motdtext" || (t.text != null && t.text.Trim() == "-LOADING-"))
            {
                t.supportRichText = true;
                t.text = Motd;
                FitText(t);
            }
        }
        if (!staffBoardBuilt && nearestCocText != null)
            staffBoardBuilt = BuildStaffBoard(nearestCocText);
    }

    private static Vector3 StaffCenter()
    {
        return (StaffTopCorner + StaffBottomCorner) * 0.5f;
    }

    // Clone the green board image (the "CodeOfConduct" Image in the same
    // canvas) and the CoC text itself, keep them in the same canvas so
    // rendering is identical to the original board, then move/rotate/
    // scale the pair onto Sans's measured corners. The original board's
    // facing is yaw-corrected onto the wall normal derived from the
    // corners - the wall normal points from the corner line back toward
    // the stump spawn.
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

        GameObject textClone = Instantiate(source.gameObject, source.transform.parent);
        textClone.name = "FalconStaffText";
        textClone.transform.position = center + dq * ((source.transform.position - boardImage.transform.position) * fit);
        textClone.transform.rotation = dq * source.transform.rotation;
        textClone.transform.localScale = source.transform.localScale * fit;
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
