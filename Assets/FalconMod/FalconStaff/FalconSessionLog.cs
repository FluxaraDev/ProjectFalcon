// Project Falcon session logger (round 54, 2026-11-15, Sans's spec):
// the bootstrapper logs what happens - menu taps, gun sends and
// receives, room joins/leaves (wherever he goes), scene loads, mod
// calls, rave, settings changes, staff actions - SILENTLY, in memory
// only. Nothing is written per event (constant writes would add the
// very lag we're hunting). The Settings tab's SAVE LOG button writes
// everything in ONE write to session_log.txt in the game's files
// folder, with the frame-hitch records (FalconFrameLog) appended.
// Also auto-saved once when the app pauses/quits, so a crash or a
// forgotten save doesn't lose the session.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class FalconSessionLog
{
    private static readonly List<string> lines = new List<string>();
    private static int dropped;
    private static float t0 = -1f;

    public static void Log(string msg)
    {
        try
        {
            if (t0 < 0f) t0 = Time.realtimeSinceStartup;
            if (lines.Count >= 4000) { lines.RemoveAt(0); dropped++; }
            lines.Add("[" + (Time.realtimeSinceStartup - t0).ToString("F1") + "] " + msg);
        }
        catch { }
    }

    public static bool Save()
    {
        try
        {
            var all = new List<string>();
            all.Add("==== Project Falcon session log - saved " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC ====");
            if (dropped > 0) all.Add("(dropped " + dropped + " oldest entries - buffer cap)");
            all.AddRange(lines);
            all.AddRange(FalconFrameLog.DrainLines());
            File.WriteAllText(Application.persistentDataPath + "/session_log.txt",
                string.Join("\r\n", all.ToArray()) + "\r\n");
            Log("LOG SAVED (" + all.Count + " lines)");
            return true;
        }
        catch { return false; }
    }
}
