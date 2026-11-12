using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

// Falcon staff roles, read from PlayFab Read-Only User Data at login.
//
// The role itself lives on the player's PlayFab account under the key
// "falcon_role" (staff / admin / headadmin, no key = no role). Clients can
// read their own read-only data but can never write it, so the value can
// only be set server-side (Game Manager, staff tooling, CloudScript).
//
// Enforcement is IN-GAME by design (Sans, 2026-11-10): this check runs in
// the client right after login, caches the role, and the report flow asks
// IsAdminOrHigher before it grants the instant-ban ability. CloudScript is
// not in the instant-ban path.
//
// Paste location: call FalconStaffRole.FetchRole() once, right after the
// successful LoginWithCustomID callback in the authenticator, before the
// player can open the report UI or the staff menu.

public static class FalconStaffRole
{
    public const string RoleKey = "falcon_role";

    // Cached at login. "" until the fetch lands, and for regular players.
    public static string Role { get; private set; } = "";

    // Fetch diagnostics (round 5, after build #8 shipped with a silently
    // dead staff menu): the staff menu's status page and the computer's
    // RAVE page display these, so a failed fetch is visible in-game
    // instead of looking like "the menu button does nothing".
    public static string FetchState { get; private set; } = "not run";
    public static string LastError { get; private set; } = "";
    public static int FetchCount { get; private set; }
    private static float lastFetchRealtime = -100f;

    public static bool IsStaffOrHigher
    {
        get { return Role == "staff" || IsAdminOrHigher; }
    }

    // Admin tier: this is the instant-ban gate.
    public static bool IsAdminOrHigher
    {
        get { return Role == "admin" || Role == "headadmin"; }
    }

    public static bool IsHeadAdmin
    {
        get { return Role == "headadmin"; }
    }

    public static void FetchRole()
    {
        FetchState = "pending";
        FetchCount++;
        lastFetchRealtime = Time.realtimeSinceStartup;
        try
        {
            var request = new GetUserDataRequest
            {
                Keys = new List<string> { RoleKey }
                // PlayFabId left unset: defaults to the logged-in player.
            };
            PlayFabClientAPI.GetUserReadOnlyData(request, OnRole, OnError);
        }
        catch (System.Exception e)
        {
            FetchState = "error";
            LastError = e.GetType().Name + ": " + e.Message;
            Role = "";
        }
    }

    // Re-run the fetch when it never ran, errored, or has been pending
    // suspiciously long. Called when the player pokes the staff menu, so
    // a transient login-time failure can heal itself in-session.
    public static void EnsureRole()
    {
        if (FetchState == "ok") return;
        if (FetchState == "pending" && Time.realtimeSinceStartup - lastFetchRealtime < 5f) return;
        FetchRole();
    }

    private static void OnRole(GetUserDataResult result)
    {
        FetchState = "ok";
        LastError = "";
        UserDataRecord record;
        if (result.Data != null && result.Data.TryGetValue(RoleKey, out record))
            Role = record.Value ?? "";
        else
            Role = "";
        Debug.Log("[FalconStaff] role = " + (Role == "" ? "none" : Role));
    }

    private static void OnError(PlayFabError error)
    {
        // Fail closed: no role read, no staff ability this session.
        FetchState = "error";
        LastError = error.ErrorMessage ?? error.Error.ToString();
        Role = "";
        Debug.LogError("[FalconStaff] role fetch failed: " + error.GenerateErrorReport());
    }
}

// Report-flow usage, in whatever class handles reporting a player:
//
//   void OnReportPlayer(string targetPlayFabId, string reason)
//   {
//       if (FalconStaffRole.IsAdminOrHigher)
//       {
//           // Admin / headadmin report: instant ban, in-game.
//           // Run the game's existing ban path for targetPlayFabId here,
//           // then post the ban to the staff webhook log.
//       }
//       else
//       {
//           // Regular report: webhook log only, no ban.
//       }
//   }
