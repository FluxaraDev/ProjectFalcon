// Project Falcon frame-time hitch logger (round 53, 2026-11-15;
// round 54: buffer-only). Records every frame that takes 50 ms or
// longer (a real hitch at 72 Hz) with its session timestamp and
// context, IN MEMORY ONLY - round 54 (Sans): no loop writing, the
// records ride out with FalconSessionLog.Save() (Settings tab SAVE
// LOG button, or the auto-save on pause/quit). Zero allocation on
// normal frames; strings are built only on hitch frames.
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FalconFrameLog : MonoBehaviour
{
    private const float HitchSeconds = 0.05f;
    public static readonly List<string> Pending = new List<string>();
    public static int TotalHitches;
    private static int dropped;
    private float sessionStart;

    public static List<string> DrainLines()
    {
        var outLines = new List<string>();
        outLines.Add("-- frame hitches: " + TotalHitches + " total" + (dropped > 0 ? " (" + dropped + " dropped, buffer full)" : ""));
        outLines.AddRange(Pending);
        Pending.Clear();
        dropped = 0;
        return outLines;
    }

    private void Start()
    {
        sessionStart = Time.realtimeSinceStartup;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt < HitchSeconds) return;
        TotalHitches++;
        if (Pending.Count >= 300) { dropped++; return; }
        float t = Time.realtimeSinceStartup - sessionStart;
        int players = 0;
        try { players = PhotonNetwork.InRoom && PhotonNetwork.PlayerList != null ? PhotonNetwork.PlayerList.Length : 0; } catch { }
        Pending.Add("HITCH t=" + t.ToString("F1") + "s dt=" + Mathf.RoundToInt(dt * 1000f) + "ms scene=" +
            SceneManager.GetActiveScene().name + " room=" + (players > 0 ? "yes(" + players + ")" : "no"));
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) FalconSessionLog.Save();
    }

    private void OnApplicationQuit()
    {
        FalconSessionLog.Save();
    }
}
