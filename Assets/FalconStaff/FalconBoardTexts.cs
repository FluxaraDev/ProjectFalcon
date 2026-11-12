using UnityEngine;
using UnityEngine.UI;

// FalconBoardTexts: in-code board text overrides applied at startup.
// The code-of-conduct boards are static scene Text objects; rewriting
// them here keeps the tightened wording shippable as a small script
// instead of an 80 MB scene-file commit. Any board whose shipped text
// starts with "-NO RACISM" gets the Falcon code of conduct.
public class FalconBoardTexts : MonoBehaviour
{
    // Tightened 2026-11-12 at Sans's direction: glitches, exploits and
    // getting out of bounds explicitly count as cheating - "it isn't a
    // cheat, it's just a glitch" is not a defense anymore.
    public const string CodeOfConduct =
        "-NO RACISM, SEXISM, HOMOPHOBIA, TRANSPHOBIA, OR OTHER BIGOTRY\n\n" +
        "-NO CHEATS OR MODS - THIS INCLUDES USING GLITCHES OR EXPLOITS, AND GETTING OUT OF BOUNDS COUNTS AS CHEATING\n\n" +
        "-DO NOT HARASS OTHER PLAYERS OR INTENTIONALLY MAKE THEM UNCOMFORTABLE\n\n" +
        "-DO NOT TROLL OR GRIEF LOBBIES BY BEING UNCATCHABLE OR BY ESCAPING THE MAP. TRY TO MAKE SURE EVERYONE IS HAVING FUN\n\n" +
        "-IF SOMEONE IS BREAKING THIS CODE, PLEASE REPORT THEM\n\n" +
        "-PLEASE BE NICE GORILLAS AND HAVE A GOOD TIME";

    private void Start()
    {
        foreach (Text t in Resources.FindObjectsOfTypeAll<Text>())
        {
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            if (t.text != null && t.text.StartsWith("-NO RACISM"))
                t.text = CodeOfConduct;
        }
    }
}
