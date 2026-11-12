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
        "Main founders: <color=#FF9FF3>FluxDev</color>, <color=#7CFFB2>Nico</color>, <color=#FFA07A>Jacob</color>\n" +
        "<color=#FFE27A>Thanks for playing!</color>";

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

    private void ApplyOnce()
    {
        foreach (Text t in Resources.FindObjectsOfTypeAll<Text>())
        {
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            if (t.text != null && t.text.StartsWith("-NO RACISM"))
            {
                t.text = CodeOfConduct;
            }
            else if (t.gameObject.name == "motdtext" || (t.text != null && t.text.Trim() == "-LOADING-"))
            {
                t.supportRichText = true;
                t.text = Motd;
            }
        }
    }
}
