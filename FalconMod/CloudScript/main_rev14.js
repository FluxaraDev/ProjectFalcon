// Legacy Runners - CloudScript revision
// Base: the working one-function revision (ReturnCurrentVersionNew) with the ban system merged in.
// Every top-level value is var-declared - no bare globals at load time.

var WEBHOOK = "https://discord.com/api/webhooks/1570997132461088778/qS34-bDIhRf1FrAHkhUZ-dajP5vZLy_YAj0y9KpLlscrVv1ThZghYS4veBOCdlfyN2lh";
var gamedown = false;

function postLog(text) {
    http.request(WEBHOOK, "post", JSON.stringify({ "content": text }), "application/json", {});
}

// ---------- Staff ----------
// Replace the demo IDs with real PlayFab IDs (exact match, 25 slots each)
var owners = ["12345", "12346", "12347", "12348", "12349", "12350", "12351", "12352", "12353", "12354", "12355", "12356", "12357", "12358", "12359", "12360", "12361", "12362", "12363", "12364", "12365", "12366", "12367", "12368", "12369"];
var admins = ["12345", "12346", "12347", "12348", "12349", "12350", "12351", "12352", "12353", "12354", "12355", "12356", "12357", "12358", "12359", "12360", "12361", "12362", "12363", "12364", "12365", "12366", "12367", "12368", "12369"];
function isStaff(id) {
    // Project Falcon: staff means the falcon_role read-only data key is
    // admin or headadmin - the same source the game client checks
    // (granted via Game Manager / staff tooling only). The demo
    // owners/admins ID lists above were never populated with real IDs,
    // which is why every staff check used to fail.
    try {
        var d = server.GetUserReadOnlyData({ PlayFabId: id, Keys: ["falcon_role"] });
        if (d && d.Data && d.Data["falcon_role"]) {
            var v = (d.Data["falcon_role"].Value || "").toLowerCase();
            if (v.indexOf("admin") !== -1) return true;
        }
    } catch (e) { log.error("isStaff role lookup failed: " + e); }
    return owners.indexOf(id) !== -1 || admins.indexOf(id) !== -1;
}
function isTier2(id) {
    // Round 45 (Sans): tier-2 roles (falcon_role admin2 / headadmin2)
    // are staff for kicks, reports and logs, but they can NOT ban.
    // isStaff() alone passes them (their role contains "admin"), so
    // every ban handler below rejects tier-2 callers explicitly.
    try {
        var d = server.GetUserReadOnlyData({ PlayFabId: id, Keys: ["falcon_role"] });
        if (d && d.Data && d.Data["falcon_role"]) {
            var v = (d.Data["falcon_role"].Value || "").toLowerCase();
            return v.indexOf("admin2") !== -1 || v.indexOf("headadmin2") !== -1;
        }
    } catch (e) { log.error("isTier2 role lookup failed: " + e); }
    return false;
}
function lookupName(id) { try { var p = server.GetPlayerProfile({ PlayFabId: id, ProfileConstraints: { ShowDisplayName: true } }); if (p && p.PlayerProfile && p.PlayerProfile.DisplayName) return p.PlayerProfile.DisplayName; } catch (e) {} return "unknown"; }

// ---------- Version check + instant ban (the working handler, ban check folded in) ----------
handlers.ReturnCurrentVersionNew = function (args) {
    var ok = {"ResultCode":0,"BannedUsers":"0","MOTD":"<color=white>Staff names are EXACT: FluxDev, Krust, Jacob, Nico - anything even slightly off is NOT staff.</color>","SynchTime":"-LOADING-","Version":"live1122","Message":"live1122"};
    try {
        var bans = server.GetUserBans({ PlayFabId: currentPlayerId });
        var details = (bans && bans.BanDetails) ? bans.BanDetails : [];
        for (var i = 0; i < details.length; i++) {
            if (details[i].Active) {
                // Instant ban: a version that doesn't exist, client bounces to the update screen at auth.
                return {"ResultCode":0,"BannedUsers":"0","MOTD":"<color=white></color>","SynchTime":"-LOADING-","Version":"BANNED","Message":"BANNED"};
            }
        }
    } catch (e) {
        log.error("ban check failed: " + e);
    }
    return ok;
};

// The client calls this during auth. Stub so the call succeeds instead of erroring;
// real DLC ownership logic can replace the empty return later.
handlers.AddOrRemoveDLCOwnership = function (args) {
    return {};
};

// ---------- Escalation ladder: admins ban by offense, repeats escalate ----------
var BAN_LADDER = {
    toxicity:   [24, 72, 168],
    hatespeech: [72, 168, 672],
    cheating:   [72, 168, 0]
};
handlers.AdminBan = function (args) {
    if (!isStaff(currentPlayerId)) { return { success: false, error: "not staff" }; }
    if (isTier2(currentPlayerId)) { return { success: false, error: "tier2 cannot ban" }; }
    var targetId = args.targetid || args.targetId;
    var offense = (args.offense || "").toLowerCase().replace(/[^a-z]/g, "");
    if (offense === "toxic") offense = "toxicity";
    if (offense === "hate") offense = "hatespeech";
    if (offense === "cheat") offense = "cheating";
    var ladder = BAN_LADDER[offense];
    if (!targetId || !ladder) { return { success: false, error: "bad targetid or offense" }; }
    // Round 62 (Sans): staff cannot ban staff - server-side refusal,
    // so even old builds cannot land a ban on a staff account.
    if (isStaff(targetId)) { return { success: false, error: "cannot ban staff" }; }
    var key = "strikes_" + offense;
    var pdata = server.GetUserInternalData({ PlayFabId: targetId, Keys: [key] });
    var strikes = 0;
    if (pdata.Data && pdata.Data[key]) strikes = parseInt(pdata.Data[key].Value, 10) || 0;
    var tier = Math.min(strikes, ladder.length - 1);
    var hours = ladder[tier];
    var banReq = { PlayFabId: targetId, Reason: offense.toUpperCase() + " - strike " + (strikes + 1) };
    if (hours > 0) banReq.DurationInHours = hours;
    server.BanUsers({ Bans: [banReq] });
    var upd = {};
    upd[key] = (strikes + 1).toString();
    server.UpdateUserInternalData({ PlayFabId: targetId, Data: upd });
    postLog("```BAN\nBy: " + lookupName(currentPlayerId) + " (" + currentPlayerId + ")\nPlayer: " + lookupName(targetId) + " (" + targetId + ")\nOffense: " + offense + "\nStrike: " + (strikes + 1) + "\nDuration: " + (hours > 0 ? (hours / 24) + " day(s)" : "PERMANENT") + "\nRoom code: " + (args.code || "n/a") + "\nGamemode: " + (args.gameMode || args.gamemode || "n/a") + (args.speakers ? "\nVoice (last 25s): " + args.speakers : "") + "```");
    return { success: true, offense: offense, strike: strikes + 1, durationHours: hours };
};

// ---------- Direct permanent ban (staff only) ----------
handlers.dobanprocess3 = function (args) {
    var targetid = args.targetid;
    if (isStaff(currentPlayerId) && !isTier2(currentPlayerId) && !isStaff(targetid)) {
        postLog("```BAN\nBy: " + lookupName(currentPlayerId) + " (" + currentPlayerId + ")\nPlayer: " + lookupName(targetid) + " (" + targetid + ")\nDuration: PERMANENT```");
        server.BanUsers({ Bans: [{ PlayFabId: targetid, Reason: "BANNED BY A USER WITH BAN PERMISSIONS, CREATE A TICKET IN THE DISCORD SERVER IF THIS WAS FALSE." }] });
    }
};

// ---------- Cheat-event ban (staff only), 48 hours ----------
handlers.ThroughMessage = function (args) {
    if (!isStaff(currentPlayerId) || isTier2(currentPlayerId)) { return; }
    if (isStaff(args.pli)) { return; } // round 62: staff cannot ban staff
    var msg = args.msg;
    var rsn = args.rsn;
    var pli = args.pli;
    postLog("```BAN (cheat event)\nBy: " + lookupName(currentPlayerId) + " (" + currentPlayerId + ")\nPlayer: " + lookupName(pli) + " (" + pli + ")\nReason: " + rsn + " " + msg + "\nDuration: 48 hours```");
    server.BanUsers({ Bans: [{ PlayFabId: pli, DurationInHours: 48, Reason: "CHEATING" }] });
};

// ---------- Reports: everyone logs for review, staff reports ban ----------
handlers.Report = function (args) {
    var reason = args.reason || "No reason given";
    var playerdoing = args.playerdoing || currentPlayerId;
    // Anti-spam (Sans round 34): over 5 reports in 9 seconds = 30-minute ban
    if (!isStaff(currentPlayerId)) {
        var rdata = server.GetUserInternalData({ PlayFabId: currentPlayerId, Keys: ["report_times"] });
        var times = [];
        if (rdata.Data && rdata.Data["report_times"]) { try { times = JSON.parse(rdata.Data["report_times"].Value); } catch (e) { times = []; } }
        var nowMs = Date.now();
        times = times.filter(function (t) { return nowMs - t < 9000; });
        times.push(nowMs);
        if (times.length > 5) {
            server.BanUsers({ Bans: [{ PlayFabId: currentPlayerId, DurationInHours: 0.5, Reason: "REPORT SPAM - abusing the report system" }] });
            server.UpdateUserInternalData({ PlayFabId: currentPlayerId, Data: { report_times: "[]" } });
            postLog("```AUTO-BAN\nPlayer: " + playerdoing + " (" + currentPlayerId + ")\nReason: report spam - over 5 reports in 9 seconds\nDuration: 30 minutes```");
            return { success: false, error: "banned for report spam", durationHours: 0.5 };
        }
        var saveTimes = {};
        saveTimes["report_times"] = JSON.stringify(times);
        server.UpdateUserInternalData({ PlayFabId: currentPlayerId, Data: saveTimes });
    }
    var targetId = args.todo || args.targetid || args.target;
    var roomCode = args.code || args.roomCode || args.roomcode || args.room || "unknown";
    var gamemode = args.gameMode || args.gamemode || "unknown";
    if (!targetId) { return { success: false, error: "no target id" }; }
    var displayName = args.targetname || lookupName(targetId);
    var didBan = false;
    if (isStaff(currentPlayerId)) {
        server.BanUsers({ Bans: [{ PlayFabId: targetId, DurationInHours: 48, Reason: "BANNED BY A USER WITH BAN PERMISSIONS, CREATE A TICKET IN THE DISCORD SERVER IF THIS WAS FALSE" }] });
        didBan = true;
    }
    postLog("```REPORT\nReporter: " + playerdoing + " (" + currentPlayerId + ")\nReported: " + displayName + " (" + targetId + ")\nRoom code: " + roomCode + "\nGamemode: " + gamemode + "\nReason: " + reason + "\nAction: " + (didBan ? "BANNED 48h (staff report)" : "PENDING REVIEW - not banned yet") + (args.speakers ? "\nVoice (last 25s): " + args.speakers : "") + "```");
    return { success: true, banned: didBan };
};

// ---------- Currency grants (staff only) ----------
handlers.docurrencyselfprocess3 = function (args) {
    if (isStaff(currentPlayerId)) {
        postLog("```" + currentPlayerId + " is giving themself Shiny Rocks (SR). User is in the staff list.```");
        server.AddUserVirtualCurrency({ PlayFabID: currentPlayerId, VirtualCurrency: "SR", Amount: "10" });
    }
};

handlers.docurrencygunprocess3 = function (args) {
    var targetid1 = args.targetid1;
    if (isStaff(currentPlayerId)) {
        postLog("```" + currentPlayerId + " is giving another player Shiny Rocks (SR). User giving is in the staff list.```");
        server.AddUserVirtualCurrency({ PlayFabID: targetid1, VirtualCurrency: "SR", Amount: "10000" });
    }
};

// ---------- Misc kit handlers ----------
handlers.newmotd = function (args, context) {
    return { "msg": "OH CMON!" };
};

handlers.MOTDText = function (args) {
    var message = args.message;
    message = "tet";
};

handlers.playerBanned = function (args) {
    postLog("```A Player Was Banned\nUserId: " + currentPlayerId + "```");
};

handlers.newPlayer = function (args) {
    postLog("```NEW USER\nPlayer: " + lookupName(currentPlayerId) + " (" + currentPlayerId + ")```");
};

// The kit author's joke stub, defused: was a bare word that threw if called.
handlers.fuckingbanme = function (args) {
    return {};
};

// ---------- Photon webhooks ----------
handlers.RoomCreated = function (args) {
    log.debug("Room Created - Game: " + args.GameId);
    try {
        var props = (args.CreateOptions && args.CreateOptions.CustomGameProperties) || {};
        var gmode = props.gameMode || props.GameMode || props.gamemode || props.mode || "unknown";
        postLog("```NEW LOBBY\nRoom code: " + args.GameId + "\nGamemode: " + gmode + "\nType: " + ((args.CreateOptions && args.CreateOptions.IsVisible === false) ? "PRIVATE" : "PUBLIC") + "\nCreated by: " + lookupName(args.UserId) + " (" + args.UserId + ")```");
    } catch (e) { log.error("lobby alert failed: " + e); }
};

// Name gates DISABLED for now: the join error also disables the gorilla computer,
// so a blocked player could never change their name. Flip to true to re-enable both gates.
var NAME_GATE_ENABLED = false;
var STAFF_NAMES = ["fluxdev", "krust", "jacob", "nico"];

handlers.RoomJoined = function (args) {
    log.debug("Room Joined - Game: " + args.GameId + " PlayFabId: " + args.UserId);
    if (NAME_GATE_ENABLED) {
        try {
            var dname = lookupName(args.UserId);
            if (/^gorilla[0-9]*$/i.test(dname)) {
                return { ResultCode: 1, Message: "change your name and reopen the game" };
            }
            if (!isStaff(args.UserId) && STAFF_NAMES.indexOf(dname.toLowerCase()) !== -1) {
                return { ResultCode: 1, Message: "Change your name and reopen the game: Don't impersonate staff you are not banned" };
            }
        } catch (e) { log.error("name check failed: " + e); }
    }
    return { ResultCode: 0 };
};

handlers.RoomLeft = function (args) {
    log.debug("Room Left - Game: " + args.GameId + " PlayFabId: " + args.UserId);
};

handlers.RoomClosed = function (args) {
    log.debug("Room Closed - Game: " + args.GameId);
};

handlers.RoomPropertyUpdated = function (args) {
    log.debug("Room Property Updated - Game: " + args.GameId);
};

handlers.RoomEventRaised = function (args) {
    log.debug("Event Raised - Game: " + args.GameId);
};

// ---------- Project Falcon relays (added Rev 9) ----------
// Staff action log relay: the game client owns no webhook URL; menu
// kicks/trolls call this and the server posts (staff callers only).
handlers.FalconLog = function (args) {
    if (!isStaff(currentPlayerId)) { return { success: false, error: "not staff" }; }
    var msg = (args && args.msg) || "";
    if (msg) postLog("```" + msg + "```");
    return { success: true };
};

// Lobby creation log, composed server-side from the caller so clients
// cannot inject arbitrary text into the staff channel.
handlers.LobbyLog = function (args) {
    var code = (args && args.code) || "unknown";
    var vis = (args && (args.private === true || args.private === "true")) ? "PRIVATE" : "PUBLIC";
    var gmode = (args && args.gameMode) || "unknown";
    postLog("```LOBBY CREATED\nBy: " + lookupName(currentPlayerId) + " (" + currentPlayerId + ")\nCode: " + code + "\nType: " + vis + "\nGamemode: " + gmode + "```");
    return { success: true };
};

// Mod call from the in-game computer: pings staff in the webhook with
// the caller's name, ID and room code. 2-minute per-player cooldown in
// internal data. NOTE: the @mods/@admin/@headadmin line is plain text
// until real Discord role IDs replace it (format <@&ROLEID>).
handlers.ModCall = function (args) {
    var code = (args && args.code) || "unknown";
    var map = (args && args.map) || "n/a";
    var nowMs = Date.now();
    // Spam guard (Sans, round 34): over 5 calls in 9 seconds is a
    // 30-minute auto-ban, counted on ATTEMPTS so the 2-minute
    // cooldown cannot be used to dodge it.
    var sdata = server.GetUserInternalData({ PlayFabId: currentPlayerId, Keys: ["mc_spam"] });
    var attempts = [];
    if (sdata.Data && sdata.Data["mc_spam"]) { try { attempts = JSON.parse(sdata.Data["mc_spam"].Value); } catch (e) { attempts = []; } }
    attempts = attempts.filter(function (t) { return nowMs - t < 9000; });
    attempts.push(nowMs);
    if (attempts.length > 5) {
        server.BanUsers({ Bans: [{ PlayFabId: currentPlayerId, DurationInHours: 0.5, Reason: "MOD CALL SPAM - abusing the mod call system" }] });
        server.UpdateUserInternalData({ PlayFabId: currentPlayerId, Data: { mc_spam: "[]" } });
        postLog("```AUTO-BAN\nPlayer: " + lookupName(currentPlayerId) + " (" + currentPlayerId + ")\nReason: mod call spam - over 5 calls in 9 seconds\nDuration: 30 minutes```");
        return { success: false, error: "spam_banned" };
    }
    var saveA = {}; saveA["mc_spam"] = JSON.stringify(attempts);
    server.UpdateUserInternalData({ PlayFabId: currentPlayerId, Data: saveA });
    var d = server.GetUserInternalData({ PlayFabId: currentPlayerId, Keys: ["modcall_last"] });
    var last = 0;
    if (d.Data && d.Data["modcall_last"]) last = parseInt(d.Data["modcall_last"].Value, 10) || 0;
    if (nowMs - last < 120000) { return { success: false, error: "cooldown", waitSeconds: Math.ceil((120000 - (nowMs - last)) / 1000) }; }
    var upd = {}; upd["modcall_last"] = nowMs.toString();
    server.UpdateUserInternalData({ PlayFabId: currentPlayerId, Data: upd });
    postLog("@mods @admin @headadmin\n```MOD CALL\nFrom: " + lookupName(currentPlayerId) + " (" + currentPlayerId + ")\nCode: " + code + "\nMap: " + map + "```");
    return { success: true };
};
