using System;
using System.Collections;
using ExitGames.Client.Photon;
using GorillaLocomotion;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using Photon.Voice.Unity;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice.Unity;
using Photon.Voice.PUN;
using Player = GorillaLocomotion.Player;
using UnityEngine;

// Falcon staff network events. Every troll/staff effect travels as a Photon
// room event so the whole lobby applies it (PUN has no dedicated server;
// "serversided" here = networked through the room, gated by the sender's
// PlayFab role, which only the server side can set).
//
// Targeted effects (kick, fling, fake lag, creepify, slip, spin, outfits)
// are sent ONLY to the target's client, because only the target's own
// client can move its rig, change its cosmetics/color (its wear state then
// syncs to everyone through the rig's normal stream), or cap its FPS.
// Loud mic and spooky music go to ALL clients, because every client renders
// the target's voice / the lobby music locally.
//
// Event codes 100-109 are Falcon staff codes, 110-111 the rave's. They
// MUST stay under 200: Photon reserves 200+ for its own internal events
// and RaiseEvent refuses to send them (proven in Sans's kick.txt logcat
// 2026-11-13: "RaiseEvent(210) failed... eventCode must be less than
// 200"). The game's own codes use low numbers (cosmetics = 9), so the
// 100s are free. Original codes were 200-209 and never left the client.
// Values from the staff menu's Settings tab (Jacob's suggestion,
// 2026-11-13). Snowball interval and fling force apply on the
// SHOOTER's client; spin speed travels inside the EvSpin payload so
// the victim's client spins at the shooter's chosen speed.
public static class FalconStaffSettings
{
    public static int SnowballMs = 100;   // spam interval, 1..1000 ms, step 10
    public static int SnowballBurst = 1;  // snowballs per fire, 1..50 (round 30; cap 50 round 47 - a 100-burst hung a lobby; >30 warns: lobby lag)
    public static int SpinSpeed = 9;      // game turn-speed units, 1..30
    public static float FlingForce = 30f; // fling velocity magnitude, 5..100
    public static float FlySpeed = 6f;    // thumbstick fly, 1..20 m/s (round 54)
    public static float TinyScale = 0.5f; // shrink gun scale, 0.1..0.9 (round 54)
    public static float GrowScale = 2f;   // grow gun scale, 1.2..4 (round 54)

    public static float SpinDegPerSec { get { return SpinSpeed * 60f; } } // speed 9 = 540 deg/s
}

public class FalconStaffNet : MonoBehaviour, IOnEventCallback, IMatchmakingCallbacks
{
    public const byte EvKick = 100;
    public const byte EvFling = 101;
    public const byte EvFakeLag = 102;
    public const byte EvCreepify = 103;
    public const byte EvSlip = 104;
    public const byte EvSpin = 105;
    public const byte EvLoudMic = 106;
    public const byte EvOutfitRandom = 107;
    public const byte EvOutfitAll = 108;
    public const byte EvSpookyMusic = 109;
    public const byte EvBan = 112;
    public const byte EvQuit = 113;
    public const byte EvCloseAll = 114;
    public const byte EvTiny = 115;
    public const byte EvGrow = 116;

    private void OnEnable() { PhotonNetwork.AddCallbackTarget(this); }
    private void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); }

    // ---------- sending (called by the menu, role already checked) ----------

    public static void SendTo(byte code, Photon.Realtime.Player target, object[] data)
    {
        // Round 26: targeted effects ride the BROADCAST path with the
        // target's actor number prefixed into the payload, and only the
        // addressed client applies them (see OnEvent). Alt build #1
        // verdict (Sans, 2026-11-13): the TargetActors form of
        // RaiseEvent never landed a single troll effect or kick on the
        // other player, while broadcast events (the rave) demonstrably
        // reached the whole lobby - so targeted effects now travel the
        // path that is proven to arrive.
        object[] payload = new object[(data != null ? data.Length : 0) + 1];
        payload[0] = target.ActorNumber;
        if (data != null) Array.Copy(data, 0, payload, 1, data.Length);
        PhotonNetwork.RaiseEvent(code, payload, RaiseEventOptions.Default, SendOptions.SendReliable);
        FalconSessionLog.Log("SEND " + CodeName(code) + " -> " + (target != null ? target.NickName : "?"));
    }

    public static void SendToAll(byte code, object[] data)
    {
        PhotonNetwork.RaiseEvent(code, data, RaiseEventOptions.Default, SendOptions.SendReliable);
        FalconSessionLog.Log("SENDALL " + CodeName(code));
    }

    // Close lobby (round 45, Sans): the Staff tab's close button.
    // Broadcast - every client in the room, the presser included,
    // leaves to the stump (see the EvCloseAll case in ApplyLocal).
    // The round-39 Title Data room orders remain the way to close a
    // room no staff client is inside; this is the in-the-room one.
    public static void CloseLobby()
    {
        if (!PhotonNetwork.InRoom) return;
        string code = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "?";
        string me = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.NickName : "?";
        SendToAll(EvCloseAll, new object[0]);
        FalconStaffWebhook.Log("LOBBY CLOSED by " + me + " (room " + code + ")");
        FalconSessionLog.Log("CLOSE LOBBY room " + code);
        // Round 71 (build #14 verdict: "close lobby doesn't
        // work"): SendToAll rides RaiseEventOptions.Default, whose
        // ReceiverGroup is OTHERS - the closer never received
        // their own EvCloseAll. Solo the button did nothing at
        // all; in a group the closer stayed behind in the emptied
        // room. The event is already sent, so apply the same exit
        // locally (same lines the EvCloseAll case runs).
        // Round 74: FalconFailure.Mark() FIRST - without it the
        // failure screen is never cleared (build #15: the closed
        // message latched the computer forever, public lobbies
        // only). Every gate that shows a Falcon failure text marks
        // it; these two call sites were the only ones missing it.
        try { FalconFailure.Mark(); if (GorillaNetworking.GorillaComputer.instance != null) GorillaNetworking.GorillaComputer.instance.UpdateFailureText("THIS LOBBY WAS CLOSED BY STAFF."); } catch { }
        ExitToStump();
    }

    // The game's own room exit (round 55): Gorilla Tag never leaves a
    // room with a bare PhotonNetwork.LeaveRoom() - its computer and
    // triggers call PhotonNetworkController.AttemptDisconnect(), a
    // FULL disconnect that runs DisconnectCleanup and walks the
    // connection state machine back to the stump, which then
    // reconnects on its own. A bare LeaveRoom() leaves the controller
    // believing it is still in the room: the client looks in-game but
    // is silently locked (Sans, build #9 close-lobby verdict). Every
    // Falcon exit that leaves the player IN the game routes here.
    public static void ExitToStump()
    {
        try
        {
            GorillaNetworking.PhotonNetworkController c = GorillaNetworking.PhotonNetworkController.Instance;
            if (c != null) { c.AttemptDisconnect(); return; }
        }
        catch { }
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
    }

    // ---------- receiving ----------

    // Last targeted effect this client received and applied, shown in
    // the staff menu footer ("rx fling from NAME") - delivery problems
    // become visible on the victim's own screen instead of silent.
    public static string LastReceived = "";

    private static bool IsTargeted(byte code)
    {
        return code == EvKick || code == EvFling || code == EvFakeLag || code == EvCreepify
            || code == EvSlip || code == EvSpin || code == EvOutfitRandom || code == EvOutfitAll
            || code == EvBan || code == EvQuit;
    }

    private static string CodeName(byte code)
    {
        switch (code)
        {
            case EvKick: return "kick";
            case EvFling: return "fling";
            case EvFakeLag: return "fakelag";
            case EvCreepify: return "creepify";
            case EvSlip: return "slip";
            case EvSpin: return "spin";
            case EvOutfitRandom: return "outfitrandom";
            case EvOutfitAll: return "outfitall";
            case EvBan: return "ban";
            case EvQuit: return "quit";
            case EvCloseAll: return "closelobby";
            case EvTiny: return "tiny";
            case EvGrow: return "grow";
            default: return "code" + code;
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        byte code = photonEvent.Code;
        object[] data = photonEvent.CustomData as object[];
        if (IsTargeted(code))
        {
            // Addressed delivery (round 26): payload[0] is the target
            // actor. Everyone receives the broadcast; only the addressed
            // client applies it. There is deliberately NO staff immunity
            // here - staff can troll each other (Sans's directive).
            // Round 40: the actor parse is tolerant now (any integer
            // type Photon hands back) - the strict "is int" check was
            // a silent-return suspect in the dead-guns investigation.
            int targetActor;
            if (data == null || data.Length == 0 || !TryActor(data[0], out targetActor)) return;
            if (PhotonNetwork.LocalPlayer == null || targetActor != PhotonNetwork.LocalPlayer.ActorNumber) return;
            object[] rest = new object[data.Length - 1];
            Array.Copy(data, 1, rest, 0, rest.Length);
            string sender = "?";
            if (PhotonNetwork.CurrentRoom != null)
            {
                Photon.Realtime.Player sp = PhotonNetwork.CurrentRoom.GetPlayer(photonEvent.Sender);
                if (sp != null) sender = sp.NickName;
            }
            // Staff lock (round 43, Sans): while I am locked, targeted
            // effects from OTHER staff do nothing to me - no kick, no
            // ban, no quit, no troll guns. My own self-tests still
            // apply (sender == me). The PlayFab half of a ban is
            // refused on the shooter's side (menu/scoreboard check
            // the same lock property before firing), because a server
            // ban cannot be dropped here.
            if (FalconStaffRole.LockOn && PhotonNetwork.LocalPlayer != null
                && photonEvent.Sender != PhotonNetwork.LocalPlayer.ActorNumber)
            {
                LastReceived = "blocked " + CodeName(code) + " (locked)";
                FalconStaffWebhook.Log("LOCK BLOCKED " + CodeName(code) + " from " + sender);
                return;
            }
            // Tier-2 peer protection (round 45, Sans): tier-2 staff
            // (admin2 / headadmin2) cannot kick EACH OTHER. If I am
            // tier-2 and the shooter is tier-2, the kick drops here.
            // (Tier-2 has no ban path at all - CanInstantBan is false
            // for tier 2 everywhere - so only the kick needs this.)
            if (code == EvKick && FalconStaffRole.Tier2
                && photonEvent.Sender != PhotonNetwork.LocalPlayer.ActorNumber)
            {
                Photon.Realtime.Player sp2 = PhotonNetwork.CurrentRoom != null
                    ? PhotonNetwork.CurrentRoom.GetPlayer(photonEvent.Sender) : null;
                if (sp2 != null && FalconStaffRole.IsTier2(sp2))
                {
                    LastReceived = "blocked kick (tier2 peer)";
                    FalconStaffWebhook.Log("TIER2 KICK BLOCKED from " + sender);
                    return;
                }
            }
            LastReceived = CodeName(code) + " from " + sender;
            FalconSessionLog.Log("RX " + LastReceived);
            // Round 40: every targeted delivery ADDRESSED TO ME posts
            // a receipt to the staff log. One test now answers the
            // dead-guns question by itself: no RX post = the event
            // never arrived; RX post = it arrived, look at ApplyLocal.
            FalconStaffWebhook.Log("RX " + CodeName(code) + " from " + sender);
            if (code == EvKick) { OnKick(photonEvent); return; }
            if (code == EvBan) { OnBan(rest); return; }
            if (code == EvQuit) { OnQuit(); return; }
            ApplyLocal(code, rest);
            return;
        }
        // Broadcast effects that carry a target actor in the payload
        // (loud mic, tiny, grow): the staff lock covers these too -
        // if the target is me and I am locked, the effect drops
        // (round 45; round 43's lock only guarded targeted events).
        if ((code == EvLoudMic || code == EvTiny || code == EvGrow) && data != null && data.Length > 0)
        {
            int tActor;
            if (TryActor(data[0], out tActor) && PhotonNetwork.LocalPlayer != null)
            {
                if (tActor == PhotonNetwork.LocalPlayer.ActorNumber
                    && FalconStaffRole.LockOn && photonEvent.Sender != PhotonNetwork.LocalPlayer.ActorNumber)
                {
                    LastReceived = "blocked " + CodeName(code) + " (locked)";
                    return;
                }
                if (code == EvTiny || code == EvGrow)
                {
                    float sc = 1f;
                    if (data.Length > 1 && data[1] is float) sc = (float)data[1];
                    else if (data.Length > 1 && data[1] is double) sc = (float)(double)data[1];
                    ApplySizeEvent(tActor, sc);
                    // Round 71: SIZE RX receipt (build #14 verdict:
                    // "grow guns work not serverside"). The receive
                    // + record + re-assert path reads correct in
                    // code, so every client now posts a receipt per
                    // size shot: one two-headset test convicts the
                    // exact hop (no receipt on other clients = the
                    // event never arrived; receipt + no scale = the
                    // rig assert is losing on remote clients).
                    FalconStaffWebhook.Log("SIZE RX " + CodeName(code) + " actor " + tActor + " scale " + sc + " sender " + photonEvent.Sender);
                    FalconSessionLog.Log("SIZE RX actor " + tActor + " scale " + sc);
                    return;
                }
            }
        }
        ApplyLocal(code, data);
    }

    // Keep the published role property fresh: the fetch usually lands
    // before joining a room, but re-publish once in a room (and if the
    // role ever changes mid-session).
    private string publishedRole;
    private float nextVoiceSample;
    private int fpsFrames;
    public static int LocalFps;
    private static bool fpsPublishLogged;
    private float fpsTime;
    private int publishedFps = -1;
    private void Update()
    {
        SizeTick(); // round 71: size assert also runs pre-LateUpdate (see SizeTick)
        if (Time.realtimeSinceStartup < fakeLagUntil)
            System.Threading.Thread.Sleep(185); // fake lag: ~5 FPS (round 30)
        if (PhotonNetwork.InRoom && publishedRole != FalconStaffRole.Role)
        {
            publishedRole = FalconStaffRole.Role;
            FalconStaffRole.PublishRoleProperty();
        }
        // Name-tag FPS (round 43, Sans): publish my frame rate as a
        // player property once a second (only when it actually moved)
        // so other clients can print it on my tag; my own tag reads
        // the same property back.
        fpsFrames++;
        fpsTime += Time.unscaledDeltaTime;
        if (fpsTime >= 1f)
        {
            int fps = Mathf.RoundToInt(fpsFrames / fpsTime);
            fpsFrames = 0;
            fpsTime = 0f;
            LocalFps = fps; // round 53: the local tag reads this directly (no Photon property needed)
            if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null && Mathf.Abs(fps - publishedFps) >= 2)
            {
                publishedFps = fps;
                try { PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "falcon_fps", fps } }); }
                catch (System.Exception e)
                {
                    // Round 53: was a silent catch - if this throws, the
                    // FPS name tag can never work and nobody sees why
                    // (Sans, build #9: "still no fps name tag"). Log once.
                    if (!fpsPublishLogged) { fpsPublishLogged = true; Debug.Log("Falcon fps publish failed: " + e.Message); }
                }
            }
        }
        // Voice activity sampling for report logs (round 28): who spoke
        // in the last 15 s rides along with every report the client
        // sends (Sans: reports should show it if possible - it is).
        if (Time.time < nextVoiceSample) return;
        nextVoiceSample = Time.time + 0.25f;
        if (!PhotonNetwork.InRoom) return;
        foreach (VRRig rig in FindObjectsOfType<VRRig>())
        {
            Photon.Realtime.Player rp = PlayerOf(rig);
            if (rig == null || rp == null || rp.IsLocal) continue;
            PhotonVoiceView pvv = rig.GetComponent<PhotonVoiceView>();
            if (pvv == null) continue;
            // Round 29: keep a voice tap on this player's speaker so a
            // report can attach the last 25 s of audio.
            if (pvv.SpeakerInUse != null)
            {
                FalconVoiceTap tap = pvv.SpeakerInUse.GetComponent<FalconVoiceTap>();
                if (tap == null) tap = pvv.SpeakerInUse.gameObject.AddComponent<FalconVoiceTap>();
                tap.Bind(rp.ActorNumber);
                // Loud mic state is pushed onto the tap here, every
                // tick (round 45) - see LoudMic for why.
                tap.Loud = (rp.ActorNumber == loudActor);
            }
            if (pvv.IsSpeaking)
                FalconVoiceLog.Mark(rp.ActorNumber, rp.NickName + " (" + rp.UserId + ")");
        }
    }

    // -------- lobby creation logging (round 28; in-client round 35) --------
    // Fires on the creator's client only; posted straight to the
    // staff webhook (CloudScript is out, per Sans).
    public void OnCreatedRoom()
    {
        try
        {
            Room room = PhotonNetwork.CurrentRoom;
            if (room == null) return;
            string gmode = "unknown";
            if (room.CustomProperties != null)
            {
                object gm;
                if (room.CustomProperties.TryGetValue("gameMode", out gm) && gm != null) gmode = gm.ToString();
            }
            // Round 35: posted in-client (CloudScript is out).
            string pname = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.NickName : "unknown";
            string pid = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.UserId : "unknown";
            FalconStaffWebhook.PostEmbed("Lobby Created", 5763719, "By: " + pname + " (" + pid + ")\nCode: " + room.Name + "\nType: " + (room.IsVisible ? "PUBLIC" : "PRIVATE") + "\nGamemode: " + gmode, null);
        }
        catch { }
    }

    public void OnCreateRoomFailed(short returnCode, string message) { }
    public void OnJoinedRoom()
    {
        // Round 66 (Sans, build #11: "you have an fps name tag but
        // other people don't"): publishedFps survives across rooms,
        // and player properties do not. On a locked ~72 Hz the fps
        // rarely moves 2+ from the value published in the PREVIOUS
        // room, so in every room after the first (and GT zone hops
        // are room changes) nobody re-published and remote tags got
        // no FPS - while the local tag hid the same failure behind
        // its LocalFps fallback. Reset on join: the next 1 s tick
        // always publishes fresh into this room.
        publishedFps = -1;
        string rn = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "?";
        int n = PhotonNetwork.PlayerList != null ? PhotonNetwork.PlayerList.Length : 0;
        FalconSessionLog.Log("JOINED ROOM " + rn + " (" + n + " players)");
    }
    public void OnJoinRoomFailed(short returnCode, string message) { }
    public void OnJoinRandomFailed(short returnCode, string message) { }
    public void OnLeftRoom()
    {
        FalconSessionLog.Log("LEFT ROOM");
        // Actor numbers are per-room: never carry loud/size state
        // into the next room, where the same number is a stranger.
        loudActor = -1;
        if (sizeFx.Count > 0)
        {
            sizeFx.Clear();
            if (Player.Instance != null) Player.Instance.scale = 1f;
        }
    }
    public void OnFriendListUpdate(List<Photon.Realtime.FriendInfo> friendList) { }

    // The one effect table, used by network events AND by self-testing:
    // the menu calls this directly when you shoot yourself, so self tests
    // run the exact code a victim's client runs. Kick is the exception: it
    // is never applied locally (a self kick test would just leave the room;
    // the menu logs it as a dry run instead).
    public void ApplyLocal(byte code, object[] data)
    {
        switch (code)
        {
            case EvFling: if (data != null && data.Length > 0) StartCoroutine(Fling((Vector3)data[0])); break;
            case EvFakeLag: StartCoroutine(FakeLag()); break;
            case EvCreepify: StartCoroutine(Creepify()); break;
            case EvSlip: StartCoroutine(Slip()); break;
            case EvSpin: StartCoroutine(Spin(data != null && data.Length > 0 && data[0] is float ? (float)data[0] : FalconStaffSettings.SpinDegPerSec)); break;
            case EvLoudMic: if (data != null && data.Length > 0) LoudMic((int)data[0]); break;
            case EvOutfitRandom: FalconStaffOutfits.RandomOutfit(); break;
            case EvOutfitAll: StartCoroutine(FalconStaffOutfits.AllCosmetics()); break;
            case EvSpookyMusic: SpookyMusic(); break;
            case EvCloseAll:
                try { FalconFailure.Mark(); if (GorillaNetworking.GorillaComputer.instance != null) GorillaNetworking.GorillaComputer.instance.UpdateFailureText("THIS LOBBY WAS CLOSED BY STAFF."); } catch { }
                ExitToStump();
                break;
        }
    }

    private void OnKick(EventData photonEvent)
    {
        // Real kick (round 43 fix): the kicked client leaves the room
        // on receipt - and that is now the ONLY layer. The old second
        // layer (the shooter's client calling CloseConnection when it
        // was MasterClient) disconnected the victim from Photon
        // ENTIRELY, and the LeaveRoom/CloseConnection race wedged the
        // game's connection state machine: build #6 verdict (Sans) -
        // kicked players could not rejoin lobbies at all.
        // Round 55 correction: the event-only kick's bare LeaveRoom()
        // wedged the state machine the same way from the other side -
        // the controller never learned the room ended (build #9:
        // close lobby left clients looking in-game but locked). The
        // exit is now the game's own AttemptDisconnect via
        // ExitToStump: cleanup runs, the client lands at the stump
        // and reconnects by itself, free to join any lobby.
        ExitToStump();
    }

    // Quit gun (round 43, Sans): the pointed-at player's game closes.
    // Board note, leave the room, quit after 2 s - the ban landing
    // (OnBan) without the ban.
    private void OnQuit()
    {
        try
        {
            if (GorillaNetworking.GorillaComputer.instance != null)
                GorillaNetworking.GorillaComputer.instance.UpdateFailureText("YOUR GAME WILL NOW CLOSE.");
        }
        catch { }
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        StartCoroutine(QuitSoon(2f));
    }

    // Round 78 (Sans's call): the quit gun fired at YOURSELF is
    // real - it runs the exact victim path, so the gun can be
    // proven end-to-end on your own headset. (Kick and ban
    // self-shots stay dry-run in the menu: kick is already proven
    // on other players, and a real self-ban would strike your
    // own account.)
    public void SelfQuit()
    {
        FalconStaffWebhook.Log("SELF QUIT (real, quit gun self-test)");
        FalconSessionLog.Log("SELF QUIT (real)");
        OnQuit();
    }

    private static bool TryActor(object o, out int actor)
    {
        actor = -1;
        if (o == null) return false;
        try { actor = System.Convert.ToInt32(o); return true; }
        catch { return false; }
    }

    // Report-ban landing (round 40, Sans): until now the kicked
    // player just saw "not connected". Now their own client shows
    // the ban on the computer board, leaves the room, and closes
    // their game after 5 s. The persistent PlayFab ban itself is
    // applied by the Discord staff bot from the report log embed.
    private void OnBan(object[] rest)
    {
        // Round 62 (Sans): staff cannot be banned by staff. A staff
        // victim's client refuses the ban event outright - this also
        // covers shooters on older builds, whose CloudScript call is
        // refused server-side by rev 14 the same way.
        if (FalconStaffRole.IsStaffOrHigher)
        {
            FalconStaffWebhook.Log("BAN BLOCKED (staff target) - ban event refused by " + PhotonNetwork.LocalPlayer.NickName);
            FalconSessionLog.Log("BAN BLOCKED (staff target)");
            return;
        }
        string offense = rest != null && rest.Length > 0 && rest[0] is string ? (string)rest[0] : "toxicity";
        try
        {
            if (GorillaNetworking.GorillaComputer.instance != null)
                GorillaNetworking.GorillaComputer.instance.UpdateFailureText("YOU HAVE BEEN BANNED FROM PROJECT FALCON. OFFENSE: " + offense.ToUpperInvariant() + ". YOUR GAME WILL NOW CLOSE.");
        }
        catch { }
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        StartCoroutine(QuitSoon(5f));
    }

    private IEnumerator QuitSoon(float delay)
    {
        yield return new WaitForSeconds(delay);
        Application.Quit();
        // Round 56: Horizon ignores Application.Quit() - build #9
        // verdict (Sans): the quit gun landed and BOTH games stayed
        // open. If the app is still alive half a second later,
        // hard-kill the process the Android way. This coroutine also
        // serves the ban landing, so the fix covers both.
        yield return new WaitForSeconds(0.5f);
        try
        {
            using (var proc = new AndroidJavaClass("android.os.Process"))
                proc.CallStatic("killProcess", proc.CallStatic<int>("myPid"));
        }
        catch { }
        try { System.Diagnostics.Process.GetCurrentProcess().Kill(); } catch { }
    }

    // Master-side backstop for OnBan: give the ban event 3 s to
    // land (board + quit on the victim's client), then force the
    // disconnect if the player is somehow still in the room.
    public IEnumerator DelayedClose(Photon.Realtime.Player p)
    {
        yield return new WaitForSeconds(3f);
        try
        {
            if (PhotonNetwork.IsMasterClient && p != null && PhotonNetwork.CurrentRoom != null
                && PhotonNetwork.CurrentRoom.GetPlayer(p.ActorNumber) != null)
                PhotonNetwork.CloseConnection(p);
        }
        catch { }
    }

    // Instance access for the menu's self-test path.
    public static FalconStaffNet Instance { get; private set; }
    private void Awake() { Instance = this; }

    // VRRig.myPlayer is NEVER assigned anywhere in this game export -
    // declared by the original devs, then abandoned; it is null on
    // every rig. Every Falcon feature that trusted it silently did
    // nothing (round 41, found from Sans's "only shooting myself
    // works"): gun targeting skipped every remote rig, voice taps
    // never bound (so report clips were always empty), name tags
    // skipped every rig. The live owner reference is the PhotonView.
    public static Photon.Realtime.Player PlayerOf(VRRig rig)
    {
        if (rig == null) return null;
        Photon.Pun.PhotonView pv = rig.photonView;
        return pv != null ? pv.Owner : null;
    }

    private static Rigidbody LocalBody()
    {
        return Player.Instance != null ? Player.Instance.GetComponent<Rigidbody>() : null;
    }

    private static void TeleportLocal(Vector3 destination)
    {
        Rigidbody rb = LocalBody();
        if (rb == null) return;
        Vector3 head = Camera.main != null ? Camera.main.transform.position : rb.position;
        rb.MovePosition(rb.position + (destination - head));
        rb.velocity = Vector3.zero;
    }

    private IEnumerator Fling(Vector3 velocity)
    {
        Rigidbody rb = LocalBody();
        if (rb == null) yield break;
        // Sustain the velocity: a single set is overwritten by the
        // locomotion within a frame or two and the fling reads as
        // "nothing happened" (the self-test complaint).
        float until = Time.time + 0.35f;
        while (Time.time < until)
        {
            rb.velocity = velocity;
            yield return new WaitForFixedUpdate();
        }
    }

    private float fakeLagUntil; // realtime deadline; Update stalls while active

    private IEnumerator FakeLag()
    {
        // Round 30: the old version set Application.targetFrameRate,
        // which the Quest runtime IGNORES (it drives frame pacing
        // itself) - fake lag did literally nothing. Real fake lag =
        // stall the main thread in Update: ~185 ms a frame lands at
        // ~5 FPS for 5 seconds (Sans's numbers).
        fakeLagUntil = Time.realtimeSinceStartup + 5f;
        yield break;
    }

    private IEnumerator Creepify()
    {
        Rigidbody rb = LocalBody();
        if (rb == null) yield break;
        Vector3 home = rb.position;
        TeleportLocal(new Vector3(9999f, 9999f, 9999f));
        yield return new WaitForSeconds(4f);
        TeleportLocal(home + Vector3.up * 0.1f);
    }

    private IEnumerator Slip()
    {
        // Slippery for 20 s: every physics step, hand back the horizontal
        // velocity the ground friction just ate, so the player keeps sliding.
        Rigidbody rb = LocalBody();
        if (rb == null) yield break;
        float end = Time.time + 20f;
        Vector3 last = rb.velocity;
        while (Time.time < end)
        {
            yield return new WaitForFixedUpdate();
            Vector3 v = rb.velocity;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            Vector3 lastFlat = new Vector3(last.x, 0f, last.z);
            if (flat.magnitude < lastFlat.magnitude)
                rb.velocity = new Vector3(lastFlat.x, v.y, lastFlat.z);
            last = rb.velocity;
        }
    }

    private IEnumerator Spin(float degPerSec)
    {
        // Smooth-turn code (Player.Turn) applied nonstop for 10 s.
        // Speed arrives in the event payload (Settings tab on the
        // shooter's client); empty payloads from older builds fall
        // back to the local setting (speed 9 = 540 deg/s).
        if (Player.Instance == null) yield break;
        float end = Time.time + 10f;
        while (Time.time < end)
        {
            Player.Instance.Turn(degPerSec * Time.deltaTime);
            yield return null;
        }
    }

    private static int loudActor = -1;

    private void LoudMic(int targetActor)
    {
        // Round 45 rework (Sans, build #7: "loud mic doesn't work
        // still"). Round 43 put the gain in a NEW component added to
        // the speaker at shoot time - one shot, one chance: if the
        // speaker object was not up yet, or got swapped when the
        // voice stream restarted, the gain silently never entered
        // the path. The gain now lives INSIDE FalconVoiceTap - the
        // component already proven to sit in the voice path (report
        // clips record through it) - and this method only flips WHO
        // is loud; the 0.25 s voice sampling loop pushes that state
        // onto every remote tap continuously, so late-appearing or
        // replaced speakers pick it up too. One loud target at a
        // time: shooting the loud player again turns them back down,
        // shooting someone else moves the loud to them.
        loudActor = (loudActor == targetActor) ? -1 : targetActor;
        if (PhotonNetwork.IsMasterClient) StartCoroutine(LoudDiag(targetActor));
    }

    private IEnumerator LoudDiag(int actor)
    {
        // One diagnostic post per loud shot, from the master client
        // only (it hears the whole lobby): 4 s later, report whether
        // the target's tap actually gained samples. callbacks=0 =
        // the tap never processed loud audio on this client;
        // callbacks>0 with a peak = the gain IS in the sample
        // stream here. Turns the next loud test into a verdict
        // instead of another guess.
        yield return new WaitForSeconds(4f);
        foreach (VRRig rig in FindObjectsOfType<VRRig>())
        {
            Photon.Realtime.Player rp = PlayerOf(rig);
            if (rp == null || rp.ActorNumber != actor) continue;
            PhotonVoiceView pvv = rig.GetComponent<PhotonVoiceView>();
            if (pvv == null || pvv.SpeakerInUse == null) continue;
            FalconVoiceTap tap = pvv.SpeakerInUse.GetComponent<FalconVoiceTap>();
            if (tap == null) continue;
            FalconStaffWebhook.Log("LOUDMIC DIAG actor " + actor + ": loud=" + (loudActor == actor)
                + " gained callbacks " + tap.LoudCallbacks + " peak " + tap.LoudPeak.ToString("0.00"));
            yield break;
        }
        FalconStaffWebhook.Log("LOUDMIC DIAG actor " + actor + ": no voice tap found on master client");
    }

    // ---- tiny / grow guns (round 45, Sans) ----
    // The size mechanism is copied from the basement shrinker
    // (SizeManager): Player.scale drives the local player's physics
    // (gravity compensation, body collider, arm reach) and
    // VRRig.scaleFactor drives the rig's visible size. The game's
    // own SizeManagers lerp both back to 1 outside a changer zone,
    // so while an effect is live this LateUpdate re-asserts it every
    // frame; at expiry Player.scale is reset to 1 and the rigs lerp
    // home on their own. 10 seconds per shot (Sans's number).
    private class SizeFx { public float scale; public float until; }
    private static readonly Dictionary<int, SizeFx> sizeFx = new Dictionary<int, SizeFx>();

    public static void ApplySizeEvent(int actor, float scale)
    {
        sizeFx[actor] = new SizeFx { scale = scale, until = Time.time + 10f };
    }

    private void LateUpdate() { SizeTick(); }

    // Round 71: the assert also ticks from Update. VRRig writes
    // the rig's localScale FROM scaleFactor in its own
    // LateUpdate, so asserting in Update (which always runs
    // before every LateUpdate) guarantees the rig pass reads the
    // gun's scale this frame instead of racing it.
    private void SizeTick()
    {
        if (sizeFx.Count == 0) return;
        int me = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
        var expired = new List<int>();
        foreach (var kv in sizeFx) if (Time.time > kv.Value.until) expired.Add(kv.Key);
        foreach (int a in expired)
        {
            sizeFx.Remove(a);
            if (a == me && Player.Instance != null) Player.Instance.scale = 1f;
        }
        if (sizeFx.Count == 0) return;
        foreach (VRRig rig in FindObjectsOfType<VRRig>())
        {
            Photon.Realtime.Player rp = PlayerOf(rig);
            if (rp == null) continue;
            SizeFx fx;
            if (!sizeFx.TryGetValue(rp.ActorNumber, out fx)) continue;
            rig.scaleFactor = fx.scale;
            rig.transform.localScale = Vector3.one * fx.scale;
            if (rp.IsLocal && Player.Instance != null) Player.Instance.scale = fx.scale;
        }
    }

    private void SpookyMusic()
    {
        // Lobby-wide: every client starts its own city music player at 2x
        // speed and full volume (999 clamps at the source's max in practice).
        // Hook is name-based because the music players are scene objects;
        // see README if a map names them differently.
        foreach (AudioSource src in FindObjectsOfType<AudioSource>())
        {
            string n = src.gameObject.name.ToLowerInvariant();
            if (n.Contains("music") || n.Contains("siren"))
            {
                src.pitch = 2f;
                src.volume = 999f;
                if (!src.isPlaying) src.Play();
            }
        }
    }
}

// Rolling record of who is speaking, for report logs (round 28).
// Sampled by FalconStaffNet from each rig's PhotonVoiceView.IsSpeaking;
// GorillaPlayerScoreboardLine.ReportPlayer reads RecentSpeakers() so
// every report shows the last 15 seconds of lobby voice activity.
public static class FalconVoiceLog
{
    private static readonly Dictionary<int, float> lastSpoke = new Dictionary<int, float>();
    private static readonly Dictionary<int, string> names = new Dictionary<int, string>();

    public static void Mark(int actor, string label)
    {
        lastSpoke[actor] = Time.realtimeSinceStartup;
        names[actor] = label;
    }

    public static string RecentSpeakers()
    {
        float now = Time.realtimeSinceStartup;
        var parts = new List<string>();
        foreach (KeyValuePair<int, float> kv in lastSpoke)
        {
            string label;
            if (now - kv.Value <= 25f && names.TryGetValue(kv.Key, out label)) parts.Add(label);
        }
        return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "none";
    }
}

// (The round-43 FalconLoudMic gain component lived here. Round 45
// moved the gain into FalconVoiceTap itself - see LoudMic above.)
