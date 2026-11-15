using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// Project Falcon MOD CALL (Sans, 2026-11-13; buttons round 34;
// in-client round 35, 2026-11-14): CloudScript is OUT per Sans -
// the call is posted straight to the staff webhook from here
// (FalconStaffWebhook) with the last 25 s of lobby voice attached.
// Cooldown and spam rules are Sans's numbers, enforced locally:
// 2-minute cooldown between calls; over 5 calls in 9 seconds is a
// 30-minute lockout (and staff get a spam note on the webhook).
// State lives in PlayerPrefs so restarting the game dodges nothing.
public static class FalconModCall
{
    private static bool sending;

    private static long NowMs()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    // done receives one of: PINGED / COOLDOWN / BANNED / FAILED / NOT IN A LOBBY
    public static void TryCallFromButton(string map, Action<string> done)
    {
        var innerDone = done;
        done = delegate (string outcome) { FalconSessionLog.Log("MOD CALL " + map + " -> " + outcome); if (innerDone != null) innerDone(outcome); };
        if (sending) { if (done != null) done("FAILED"); return; }
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            if (done != null) done("NOT IN A LOBBY");
            return;
        }
        long now = NowMs();
        long lockoutUntil = 0;
        long.TryParse(PlayerPrefs.GetString("falcon_mc_lockout", "0"), out lockoutUntil);
        if (now < lockoutUntil) { if (done != null) done("BANNED"); return; }

        // spam guard: attempts in a 9 s window
        var attempts = new List<long>();
        string saved = PlayerPrefs.GetString("falcon_mc_spam", "");
        if (saved.Length > 0)
            foreach (string part in saved.Split(','))
            {
                long t;
                if (long.TryParse(part, out t) && now - t < 9000) attempts.Add(t);
            }
        attempts.Add(now);
        var parts = new List<string>();
        foreach (long t in attempts) parts.Add(t.ToString());
        PlayerPrefs.SetString("falcon_mc_spam", string.Join(",", parts.ToArray()));
        if (attempts.Count > 5)
        {
            PlayerPrefs.SetString("falcon_mc_lockout", (now + 30L * 60L * 1000L).ToString());
            PlayerPrefs.Save();
            FalconStaffWebhook.Post("```MOD CALL LOCKOUT\nPlayer: " + PhotonNetwork.LocalPlayer.NickName + " (" + PhotonNetwork.LocalPlayer.UserId + ")\nReason: mod call spam - over 5 calls in 9 seconds\nDuration: 30 minutes (local lockout)```", null);
            if (done != null) done("BANNED");
            return;
        }

        long last = 0;
        long.TryParse(PlayerPrefs.GetString("falcon_mc_last", "0"), out last);
        if (now - last < 120000) { if (done != null) done("COOLDOWN"); return; }
        PlayerPrefs.SetString("falcon_mc_last", now.ToString());
        PlayerPrefs.Save();

        sending = true;
        string code = PhotonNetwork.CurrentRoom.Name;
        string from = PhotonNetwork.LocalPlayer.NickName + " (" + PhotonNetwork.LocalPlayer.UserId + ")";
        // Round 44 (Sans, build #7: "Mod call audio doesn't work but
        // the report does"): the clip is now built and uploaded
        // IMMEDIATELY at press time, on its own - exactly the shape
        // of the scoreboard report path, which works. The old flow
        // built the clip inside the embed post's completion callback
        // and only if that post succeeded, so any hiccup in the
        // embed post silently took the clip down with it. The empty
        // case still posts its note (round 37), now with the tap
        // census so the next failure says WHY it was empty.
        try
        {
            byte[] wav = FalconVoiceTap.BuildClipWav();
            if (wav != null)
            {
                FalconStaffWebhook.UploadReportClip(wav,
                    "MOD CALL from " + from + " in room " + code + " at " + map + " - last 25s of lobby voice");
            }
            else
            {
                FalconStaffWebhook.Post("(mod call voice clip unavailable - no speech captured in the last 25s; " + FalconVoiceTap.DebugStats() + ")", null);
            }
        }
        catch { }
        FalconStaffWebhook.PostEmbed("Mod Call", 15548997, "@mods @admin @headadmin\nFrom: " + from + "\nCode: " + code + "\nMap: " + map, delegate (bool ok)
        {
            sending = false;
            if (done != null) done(ok ? "PINGED" : "FAILED");
        });
    }
}
