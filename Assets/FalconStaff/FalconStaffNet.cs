using System.Collections;
using ExitGames.Client.Photon;
using GorillaLocomotion;
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
    public static int SpinSpeed = 9;      // game turn-speed units, 1..30
    public static float FlingForce = 30f; // fling velocity magnitude, 5..100

    public static float SpinDegPerSec { get { return SpinSpeed * 60f; } } // speed 9 = 540 deg/s
}

public class FalconStaffNet : MonoBehaviour, IOnEventCallback
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

    private void OnEnable() { PhotonNetwork.AddCallbackTarget(this); }
    private void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); }

    // ---------- sending (called by the menu, role already checked) ----------

    public static void SendTo(byte code, Photon.Realtime.Player target, object[] data)
    {
        var opts = new RaiseEventOptions { TargetActors = new[] { target.ActorNumber } };
        PhotonNetwork.RaiseEvent(code, data, opts, SendOptions.SendReliable);
    }

    public static void SendToAll(byte code, object[] data)
    {
        PhotonNetwork.RaiseEvent(code, data, RaiseEventOptions.Default, SendOptions.SendReliable);
    }

    // ---------- receiving ----------

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == EvKick) { OnKick(photonEvent); return; }
        ApplyLocal(photonEvent.Code, photonEvent.CustomData as object[]);
    }

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
        }
    }

    private void OnKick(EventData photonEvent)
    {
        // Real kick, two layers: the menu sends CloseConnection directly
        // when the shooter is MasterClient (server-grade removal); this
        // handler is the fallback that always works because this code ships
        // in the game: the kicked client leaves the room on receipt.
        PhotonNetwork.LeaveRoom();
    }

    // Instance access for the menu's self-test path.
    public static FalconStaffNet Instance { get; private set; }
    private void Awake() { Instance = this; }

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

    private IEnumerator FakeLag()
    {
        int previous = Application.targetFrameRate;
        Application.targetFrameRate = 10;
        yield return new WaitForSeconds(2f);
        Application.targetFrameRate = previous;
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

    private void LoudMic(int targetActor)
    {
        // Every client raises the target rig's voice AudioSource. Runs on
        // all clients (event is broadcast). Second shot toggles back to 1.
        if (GorillaParent.instance == null) return;
        foreach (var pair in GorillaParent.instance.vrrigDict)
        {
            if (pair.Key.ActorNumber != targetActor || pair.Value == null) continue;
            var voiceView = pair.Value.GetComponent<PhotonVoiceView>();
            AudioSource src = null;
            if (voiceView != null && voiceView.SpeakerInUse != null)
                src = voiceView.SpeakerInUse.GetComponent<AudioSource>();
            if (src == null) src = pair.Value.GetComponentInChildren<AudioSource>();
            if (src != null) src.volume = src.volume > 1.5f ? 1f : 999f;
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
