// Project Falcon frame-time hitch logger (round 53, 2026-11-15).
// Built to catch Sans's lag spikes with evidence instead of theories:
// every frame that takes 50 ms or longer (a real hitch at 72 Hz) is
// recorded with its session timestamp and context, and the records
// flush every 15 s to frametime_log.txt in the game's files folder
// (Android/data/com.fluxtech.ProjectFalcon/files/frametime_log.txt).
// Send that file after a laggy session and the spike cadence is
// readable directly: are the hitches really on a 0.5 s grid, do they
// worsen over time, do they only happen in rooms. Zero allocation on
// normal frames - strings are built only on hitch frames.
using System;
using System.Collections.Generic;
using System.IO;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FalconFrameLog : MonoBehaviour
{
    private const float HitchSeconds = 0.05f;
    private readonly List<string> pending = new List<string>();
    private int hitchCount;
    private int droppedCount;
    private float nextFlush;
    private float sessionStart;

    private void Start()
    {
        sessionStart = Time.realtimeSinceStartup;
        nextFlush = Time.realtimeSinceStartup + 15f;
        try
        {
            File.AppendAllText(LogPath(),
                "==== session boot " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC ====\r\n");
        }
        catch { }
    }

    private static string LogPath()
    {
        return Application.persistentDataPath + "/frametime_log.txt";
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt >= HitchSeconds)
        {
            hitchCount++;
            if (pending.Count < 300)
            {
                float t = Time.realtimeSinceStartup - sessionStart;
                int players = 0;
                try { players = PhotonNetwork.InRoom && PhotonNetwork.PlayerList != null ? PhotonNetwork.PlayerList.Length : 0; } catch { }
                pending.Add("HITCH t=" + t.ToString("F1") + "s dt=" + Mathf.RoundToInt(dt * 1000f) + "ms scene=" +
                    SceneManager.GetActiveScene().name + " room=" + (players > 0 ? "yes(" + players + ")" : "no"));
            }
            else droppedCount++;
        }
        if (Time.realtimeSinceStartup >= nextFlush)
        {
            nextFlush = Time.realtimeSinceStartup + 15f;
            Flush();
        }
    }

    private void Flush()
    {
        if (pending.Count == 0 && droppedCount == 0) return;
        try
        {
            string header = "-- flush " + DateTime.UtcNow.ToString("HH:mm:ss") + " UTC | total hitches=" + hitchCount +
                " | options: tags=" + FalconPlayerOptions.NameTags + " arms=" + FalconPlayerOptions.LongArms +
                " speed=" + FalconPlayerOptions.SpeedBoost +
                (droppedCount > 0 ? " | +" + droppedCount + " hitches dropped (buffer full)" : "") + "\r\n";
            File.AppendAllText(LogPath(), header + string.Join("\r\n", pending.ToArray()) + "\r\n");
        }
        catch { }
        pending.Clear();
        droppedCount = 0;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Flush();
    }
}
