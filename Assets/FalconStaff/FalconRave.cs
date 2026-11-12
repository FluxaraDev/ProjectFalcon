using System.Collections.Generic;
using ExitGames.Client.Photon;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Player = GorillaLocomotion.Player;

// RAVE mode (Sans, 2026-11-12). The computer's old CREDITS tab is now the
// RAVE tab (GorillaComputer routes its ENTER key and screen text here).
// Pressing ENTER starts the rave: Sans's song plays map-wide (2D audio so
// distance does not matter), the lighting goes disco, fog rolls in and
// cycles color, gravity drops, and movement speed jumps.
//
// Rules from Sans:
//  - Start only while NOT in a room (solo rave; joining stays locked until
//    the song ends) OR as the master of a PRIVATE lobby, where it runs
//    lobby-wide through a Photon event and every client plays its own copy
//    of the song.
//  - Leaving the room ends the rave locally and restores EVERYTHING, so no
//    effect (low gravity, speed) can leak into another lobby.
// The song ships in the project at Assets/Resources/rave_song.mp3 and is
// loaded with Resources.Load; if it is missing the rave refuses to start.
public class FalconRave : MonoBehaviour, IOnEventCallback, IMatchmakingCallbacks
{
    public const byte EvRaveStart = 210;

    // Tunables ------------------------------------------------------------
    private const float RaveGravityY = -2.0f;    // stock gravity is -9.81
    private const float RaveJumpSpeed = 15f;     // below the game's own 2x
    private const float RaveJumpMult = 2.4f;     // tripwires (17 / 2.6)
    private const float RaveFogDensity = 0.015f;
    private const float FallbackSongSeconds = 197.8f; // used if a client is missing the clip
    private const string ClipResourceName = "rave_song";
    private const string SongTitle = "SOPHIE - VYZEE";

    public static FalconRave Instance;
    public static bool IsRaving { get; private set; }

    private static float raveEndTime;
    private static bool soloRave;

    private AudioSource audioSource;
    private AudioClip clip;
    private bool clipMissing;

    private Vector3 savedGravity;
    private float savedJumpSpeed = 6.5f;
    private float savedJumpMult = 1.1f;
    private bool savedFog;
    private Color savedFogColor;
    private FogMode savedFogMode;
    private float savedFogDensity, savedFogStart, savedFogEnd;
    private Color savedAmbient;
    private readonly List<Light> discoLights = new List<Light>();
    private readonly List<Color> discoLightColors = new List<Color>();
    private Material[] unlitMats = new Material[0];
    private Color[] unlitBase = new Color[0];
    private Material[] darkerMats = new Material[0];
    private Color[] darkerBase = new Color[0];
    private Material[] dayMats = new Material[0];
    private Color[] dayBase = new Color[0];
    private Material[] dayCutoutMats = new Material[0];
    private Color[] dayCutoutBase = new Color[0];
    private int totalMats;

    // Last lifecycle step, shown on the RAVE page (round 5): if a start
    // path dies mid-way again, the page says exactly how far it got.
    public static string LastStep { get; private set; } = "IDLE";

    // Outcome of the last start attempt, for the staff menu's Start Rave
    // button (no computer screen to read refusals on in city).
    public static string LastResult { get; private set; } = "";
    private float hue;
    private bool matsOk;
    private float nextScreenPush;

    // True while a solo rave is running: every join path (computer AND zone
    // triggers, which all funnel through PhotonNetworkController) is locked.
    public static bool JoinLocked => IsRaving && soloRave;

    public static int SecondsLeft =>
        IsRaving ? Mathf.Max(0, Mathf.CeilToInt(raveEndTime - Time.time)) : 0;

    public static string CountdownText()
    {
        int s = SecondsLeft;
        return (s / 60) + ":" + (s % 60).ToString("00") + " LEFT";
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
        if (IsRaving) EndRave();
    }

    // ---- computer hooks ---------------------------------------------------

    // Screen text for the RAVE page (GorillaComputer's old Credits case).
    public static string GetScreenText()
    {
        string staff = "\nSTAFF: " + (FalconStaffRole.Role == "" ? "NONE (" + FalconStaffRole.FetchState.ToUpperInvariant() + ")" : FalconStaffRole.Role.ToUpperInvariant()) + " BTN:" + FalconStaffHandMenu.MenuButtonPresses;
        if (IsRaving)
        {
            int s = SecondsLeft;
            return "RAVE MODE\n\nTIME LEFT: " + (s / 60) + ":" + (s % 60).ToString("00")
                + "\n\nLOW GRAVITY + SPEED BOOST"
                + (soloRave ? "\nJOINING LOCKED TILL SONG ENDS" : "")
                + "\nSTEP: " + LastStep + staff;
        }
        // Screen budget measured from Sans's build #8 photo: ~11 lines
        // at ~41 chars before the last line sinks behind the bezel.
        // This layout is 10 lines with every line under 30 chars.
        return "RAVE MODE\n\nPRESS ENTER TO START\nSONG: " + SongTitle
            + "\nDISCO LIGHTS - LOW GRAVITY\nSPEED BOOST - WHOLE MAP"
            + (FalconStaffRole.IsHeadAdmin ? "\n\nHEADADMIN: ANY LOBBY OK" : "\n\nSOLO OR PRIVATE HOST ONLY")
            + "\nSTEP: " + LastStep + staff;
    }

    // ENTER pressed on the RAVE page.
    public static void TryStartFromComputer()
    {
        if (Instance == null) return;
        if (IsRaving)
        {
            Refuse("THE RAVE IS ALREADY GOING. " + CountdownText());
            return;
        }
        if (!Instance.TryLoadClip())
        {
            Refuse("RAVE SONG FILE IS MISSING FROM THIS BUILD.");
            return;
        }
        if (PhotonNetwork.InRoom)
        {
            // Headadmins can rave any lobby, public included (Sans,
            // 2026-11-12). Everyone else still needs host-of-private.
            if (!FalconStaffRole.IsHeadAdmin)
            {
                if (!PhotonNetwork.IsMasterClient)
                {
                    Refuse("ONLY THE HOST OF A PRIVATE LOBBY CAN START THE RAVE.");
                    return;
                }
                if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.IsVisible)
                {
                    Refuse("THE RAVE ONLY WORKS SOLO OR IN A PRIVATE LOBBY.");
                    return;
                }
            }
            double startAt = PhotonNetwork.Time + 1.0;
            var options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            PhotonNetwork.RaiseEvent(EvRaveStart, startAt, options, SendOptions.SendReliable);
            LastStep = "EVENT SENT";
            // Round 5: the host starts locally too. Build #8 froze
            // briefly (the clip loading) then never fired in private
            // lobbies - relying on the event looping back to the sender
            // was the fragile link. The looped-back event still arrives,
            // but BeginRave no-ops while a rave is running.
            LastResult = "RAVE STARTED";
            Instance.BeginRave(solo: false, audioDelay: 1.0);
        }
        else if (PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer
                 || PhotonNetwork.NetworkClientState == ClientState.Disconnected)
        {
            LastResult = "RAVE STARTED";
            Instance.BeginRave(solo: true, audioDelay: 0.0);
        }
        else
        {
            // Transitional states (joining / leaving / connecting) also
            // read as "not in a room". Starting the solo rave inside that
            // window let it survive into the lobby being joined - full
            // effects in a public room, no master, no server-side rave
            // (Sans's field report, 2026-11-12). Refuse until settled.
            Refuse("WAIT UNTIL YOU ARE ALL THE WAY IN OR OUT OF THE LOBBY TO START THE RAVE.");
        }
    }

    private static void Refuse(string message)
    {
        LastResult = message;
        FalconFailure.Mark();
        if (GorillaComputer.instance != null)
            GorillaComputer.instance.UpdateFailureText(message);
    }

    // ---- rave lifecycle ---------------------------------------------------

    private bool TryLoadClip()
    {
        if (clip != null) return true;
        if (clipMissing) return false;
        clip = Resources.Load<AudioClip>(ClipResourceName);
        if (clip == null)
        {
            clipMissing = true;
            Debug.LogError("[FalconRave] Resources/" + ClipResourceName + " not found; rave disabled.");
            return false;
        }
        return true;
    }

    private void BeginRave(bool solo, double audioDelay)
    {
        if (IsRaving) return;
        TryLoadClip();
        float duration = clip != null ? clip.length : FallbackSongSeconds;
        soloRave = solo;
        IsRaving = true;
        raveEndTime = Time.time + (float)audioDelay + duration;

        savedGravity = Physics.gravity;
        Physics.gravity = new Vector3(0f, RaveGravityY, 0f);

        if (Player.Instance != null)
        {
            savedJumpSpeed = Player.Instance.maxJumpSpeed;
            savedJumpMult = Player.Instance.jumpMultiplier;
            Player.Instance.maxJumpSpeed = RaveJumpSpeed;
            Player.Instance.jumpMultiplier = RaveJumpMult;
        }

        savedFog = RenderSettings.fog;
        savedFogColor = RenderSettings.fogColor;
        savedFogMode = RenderSettings.fogMode;
        savedFogDensity = RenderSettings.fogDensity;
        savedFogStart = RenderSettings.fogStartDistance;
        savedFogEnd = RenderSettings.fogEndDistance;
        savedAmbient = RenderSettings.ambientLight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = RaveFogDensity;

        discoLights.Clear();
        discoLightColors.Clear();
        foreach (Light l in FindObjectsOfType<Light>())
        {
            if (l != null && l.type == LightType.Directional)
            {
                discoLights.Add(l);
                discoLightColors.Add(l.color);
            }
        }

        // Cache the day/night manager's unlit material bases for the
        // disco tint (see LateUpdate). The darker list MUST be restored
        // at EndRave: the manager preserves its hue between ticks, so a
        // leftover rave hue would otherwise stick forever.
        matsOk = false;
        try
        {
            BetterDayNightManager bdn = BetterDayNightManager.instance;
            if (bdn != null)
            {
                unlitMats = bdn.standardMaterialsUnlit ?? new Material[0];
                unlitBase = new Color[unlitMats.Length];
                for (int i = 0; i < unlitMats.Length; i++)
                    unlitBase[i] = unlitMats[i] != null ? unlitMats[i].color : Color.white;
                darkerMats = bdn.standardMaterialsUnlitDarker ?? new Material[0];
                darkerBase = new Color[darkerMats.Length];
                for (int i = 0; i < darkerMats.Length; i++)
                    darkerBase[i] = darkerMats[i] != null ? darkerMats[i].color : Color.white;
                // The lightmapped lists: forest/city surfaces live here,
                // NOT in the unlit lists - round 5 tinted the wrong lists
                // for the forest, which is why the disco only ever showed
                // on lit objects (Sans: "the lighting is baked").
                dayMats = bdn.dayNightSupportedMaterials ?? new Material[0];
                dayBase = new Color[dayMats.Length];
                for (int i = 0; i < dayMats.Length; i++)
                    dayBase[i] = dayMats[i] != null ? dayMats[i].color : Color.white;
                dayCutoutMats = bdn.dayNightSupportedMaterialsCutout ?? new Material[0];
                dayCutoutBase = new Color[dayCutoutMats.Length];
                for (int i = 0; i < dayCutoutMats.Length; i++)
                    dayCutoutBase[i] = dayCutoutMats[i] != null ? dayCutoutMats[i].color : Color.white;
                totalMats = unlitMats.Length + darkerMats.Length + dayMats.Length + dayCutoutMats.Length;
                matsOk = totalMats > 0;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[FalconRave] material capture failed: " + e.Message);
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f; // 2D: heard everywhere on the map
            audioSource.playOnAwake = false;
        }
        audioSource.Stop();
        if (clip != null)
        {
            audioSource.clip = clip;
            audioSource.volume = 1f;
            audioSource.loop = false;
            audioSource.PlayScheduled(AudioSettings.dspTime + System.Math.Max(0.0, audioDelay));
        }
        LastStep = (solo ? "BEGAN (SOLO)" : "BEGAN (LOBBY)") + (matsOk ? " MATS " + totalMats : " - NO MAP FX");
        Debug.Log("[FalconRave] Rave started. Solo=" + soloRave + " Duration=" + duration);
    }

    private void EndRave()
    {
        if (!IsRaving) return;
        IsRaving = false;
        soloRave = false;

        Physics.gravity = savedGravity;
        if (Player.Instance != null)
        {
            Player.Instance.maxJumpSpeed = savedJumpSpeed;
            Player.Instance.jumpMultiplier = savedJumpMult;
        }

        RenderSettings.fog = savedFog;
        RenderSettings.fogColor = savedFogColor;
        RenderSettings.fogMode = savedFogMode;
        RenderSettings.fogDensity = savedFogDensity;
        RenderSettings.fogStartDistance = savedFogStart;
        RenderSettings.fogEndDistance = savedFogEnd;
        RenderSettings.ambientLight = savedAmbient;
        for (int i = 0; i < discoLights.Count; i++)
        {
            if (discoLights[i] != null) discoLights[i].color = discoLightColors[i];
        }
        discoLights.Clear();
        discoLightColors.Clear();
        for (int i = 0; i < unlitMats.Length; i++)
        {
            if (unlitMats[i] != null) unlitMats[i].color = unlitBase[i];
        }
        for (int i = 0; i < darkerMats.Length; i++)
        {
            if (darkerMats[i] != null) darkerMats[i].color = darkerBase[i];
        }
        for (int i = 0; i < dayMats.Length; i++)
        {
            if (dayMats[i] != null) dayMats[i].color = dayBase[i];
        }
        for (int i = 0; i < dayCutoutMats.Length; i++)
        {
            if (dayCutoutMats[i] != null) dayCutoutMats[i].color = dayCutoutBase[i];
        }

        if (audioSource != null) audioSource.Stop();
        LastStep = "ENDED";
        Debug.Log("[FalconRave] Rave ended; everything restored.");
    }

    private void Update()
    {
        if (!IsRaving) return;
        if (Time.time >= raveEndTime)
        {
            EndRave();
            return;
        }
        // Re-assert the speed boost so room-join resets cannot stomp it.
        if (Player.Instance != null)
        {
            Player.Instance.maxJumpSpeed = RaveJumpSpeed;
            Player.Instance.jumpMultiplier = RaveJumpMult;
        }
        // Countdown on the computer screen while the RAVE page is open.
        if (Time.time >= nextScreenPush)
        {
            nextScreenPush = Time.time + 0.25f;
            PushScreenText();
        }
    }

    private void LateUpdate()
    {
        if (!IsRaving) return;
        // LateUpdate so the disco wins the frame over the day/night manager.
        hue = (hue + Time.deltaTime * 0.22f) % 1f;
        Color c = Color.HSVToRGB(hue, 0.85f, 1f);
        RenderSettings.fogColor = c;
        RenderSettings.ambientLight = Color.Lerp(Color.black, c, 0.55f);
        for (int i = 0; i < discoLights.Count; i++)
        {
            if (discoLights[i] != null) discoLights[i].color = c;
        }
        // The map is unlit: its colors live in the day/night manager's
        // material lists, not in lights or fog (build #8's disco only
        // tinted lit objects). Absolute writes from the bases cached at
        // BeginRave, so the manager's per-tick rewrites can never
        // compound the tint.
        // Full-brightness disco (round 6): multiplying by the captured
        // day-phase brightness made the tint vanish in dark phases.
        for (int i = 0; i < unlitMats.Length; i++)
        {
            if (unlitMats[i] != null)
                unlitMats[i].color = c;
        }
        for (int i = 0; i < darkerMats.Length; i++)
        {
            if (darkerMats[i] != null)
            {
                Color.RGBToHSV(darkerBase[i], out _, out _, out float bv);
                darkerMats[i].color = Color.HSVToRGB(hue, 0.85f, Mathf.Max(bv, 0.65f));
            }
        }
        for (int i = 0; i < dayMats.Length; i++)
        {
            if (dayMats[i] != null)
                dayMats[i].color = DiscoOver(dayBase[i], c);
        }
        for (int i = 0; i < dayCutoutMats.Length; i++)
        {
            if (dayCutoutMats[i] != null)
                dayCutoutMats[i].color = DiscoOver(dayCutoutBase[i], c);
        }
    }

    // Disco color over an authored base color, with the base normalized
    // so dark authored colors still take the full disco.
    private static Color DiscoOver(Color baseColor, Color disco)
    {
        float m = Mathf.Max(baseColor.r, Mathf.Max(baseColor.g, baseColor.b));
        if (m < 0.05f) return disco;
        return new Color(disco.r * baseColor.r / m, disco.g * baseColor.g / m, disco.b * baseColor.b / m);
    }

    private GorillaComputer[] allComputers;
    private float computerCacheTime = -100f;

    // Every map computer is its own GorillaComputer with its own screen;
    // GorillaComputer.instance is only the first one that loaded, and a
    // computer's screen otherwise redraws only on its own keypresses.
    // Push the countdown to whichever computers are showing the page.
    private void PushScreenText()
    {
        if (allComputers == null || Time.time - computerCacheTime > 5f)
        {
            allComputers = FindObjectsOfType<GorillaComputer>();
            computerCacheTime = Time.time;
        }
        if (allComputers == null) return;
        string text = GetScreenText();
        for (int i = 0; i < allComputers.Length; i++)
        {
            GorillaComputer gc = allComputers[i];
            if (gc != null && gc.currentState == GorillaComputer.ComputerState.Credits && gc.screenText != null)
                gc.screenText.Text = text;
        }
    }

    // ---- Photon -----------------------------------------------------------

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != EvRaveStart || IsRaving) return;
        double startAt = photonEvent.CustomData is double d ? d : PhotonNetwork.Time;
        double delay = startAt - PhotonNetwork.Time;
        LastStep = "EVENT RECEIVED";
        BeginRave(solo: false, audioDelay: delay);
    }

    // Leaving the room for any reason cuts the rave for this player and
    // restores everything, so nothing leaks into the next lobby.
    public void OnLeftRoom()
    {
        if (IsRaving) EndRave();
    }

    public void OnFriendListUpdate(System.Collections.Generic.List<FriendInfo> friendList) { }
    public void OnCreatedRoom() { }
    public void OnCreateRoomFailed(short returnCode, string message) { }
    public void OnJoinedRoom()
    {
        // A rave may exist solo (not in a room) or in the room it was
        // started in. If a join completes while one is running - the
        // leave/join grace-window route above - it ends here, so the
        // effects can never ride into a lobby.
        if (IsRaving) EndRave();
    }
    public void OnJoinRoomFailed(short returnCode, string message) { }
    public void OnJoinRandomFailed(short returnCode, string message) { }
}
