using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using Player = GorillaLocomotion.Player;

// Falcon Staff HAND menu: a world-space panel parented to the left hand,
// in the spirit of the Flux menu but built from this game's own parts
// (legacy UI Text, like the scoreboard and computer - this build has no
// TextMeshPro in its scripts). Spawned at runtime by FalconStaffBootstrap,
// so no scene editing is needed. The IMGUI FalconStaffMenu is NOT spawned
// anymore; this is the in-VR menu. All effects still live in the shared
// FalconStaffNet / FalconStaffOutfits / FalconSoundboard layer.
//
// Interaction: aim the RIGHT hand at the panel - the button under the
// laser highlights - and pull the right trigger to press it. When a gun
// is armed, the same trigger fires the gun UNLESS the laser is on the
// panel (the panel always wins its own clicks). Open/close: left
// controller menu button. Everything is gated by FalconStaffRole, and
// troll guns additionally need headadmin + an open trolling event
// (Sans's Discord-stage rule: firing outside an event logs a denial).
//
// Tunables for the first on-device fit check live at the top: if the
// panel sits wrong on the hand (mirrored, face-down, too far out),
// PanelLocalPos / PanelLocalEuler are the one-line fixes.
public class FalconStaffHandMenu : MonoBehaviour
{
    // ---- hand fit tunables (adjust from Sans's device report) ----
    private static readonly Vector3 PanelLocalPos = new Vector3(0f, 0.04f, 0.17f);
    private static readonly Vector3 PanelLocalEuler = new Vector3(0f, 0f, 0f); // was 180 - read backwards on device (build #9)
    private const float PanelScale = 0.0009f; // canvas unit -> meters
    private const float PanelW = 300f;
    private const float PanelH = 420f; // round 54: room for the new Settings rows

    // ---- colors ----
    private static readonly Color BgColor = new Color(0.05f, 0.05f, 0.08f, 0.88f);
    private static readonly Color BtnNormal = new Color(0.16f, 0.17f, 0.22f, 0.95f);
    private static readonly Color BtnOn = new Color(0.13f, 0.45f, 0.18f, 0.95f);
    private static readonly Color BtnArmed = new Color(0.55f, 0.14f, 0.14f, 0.95f);
    private static readonly Color BtnHover = new Color(0.9f, 0.68f, 0.12f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.92f, 0.95f, 1f);
    private static readonly Color DimColor = new Color(0.7f, 0.7f, 0.75f, 1f);

    private class Btn
    {
        public float cx, cy, w, h;
        public Image img;
        public Text txt;
        public float yOff;        // round 64: settings scroll offset (hover math)
        public bool scrollHidden; // round 64: scrolled out of the settings band
        public System.Func<int> state; // 0 normal, 1 on, 2 armed
        public string label;
        public System.Action onClick;
    }

    private GameObject panelRoot;
    private RectTransform contentRoot;

    // Round 64 (Sans): the Settings tab outgrew the panel - its rows
    // ran off the top and collided with the tab row. Settings rows
    // now build into a scroll sub-root, clipped to the band under
    // the tabs, and the LEFT thumbstick scrolls them (see Update).
    private RectTransform buildParent; // non-null only while building Settings
    private bool buildingSettings;
    private RectTransform settingsRoot;
    private RectTransform settingsThumb;
    private float settingsScroll;
    private string lastBuiltTabId = "";
    // Round 85 (Sans): the menu is for EVERYONE now - it renames
    // itself by role ("PLAYER MENU" / "STAFF MENU") and the tab
    // bar is built per role: players get Guns / Move / Mods,
    // staff add their tabs, troll/sound/settings stay headadmin.
    private Text titleText;
    private readonly List<Btn> tabBtns = new List<Btn>();
    private readonly List<string> curTabs = new List<string>();
    private float curMaxScroll = SettingsMaxScroll;
    private float nextCheckerRefresh;
    private float nextReportAllowed;
    // Round 86 (Sans): a VISUAL tab, a horizontally scrolling
    // tab bar (full-size tabs instead of seven crammed slivers
    // interfering with each other), and the checker rebuilds
    // only when its data actually changed.
    // Round 88 (Sans): RTX and PhaseSync are REMOVED entirely
    // (RTX to be redone later). The tab bar is reworked: tabs
    // slide under a RectMask2D instead of popping via
    // SetActive, and a real scrollbar under the bar shows the
    // scroll position.
    private readonly List<float> tabBaseCx = new List<float>();
    private float tabOffset;
    private RectTransform tabBarRoot;
    private Image tabScrollTrack, tabScrollThumb;
    private string lastModsSig = "";
    // Round 87 (Sans): the checker is a list -> detail UI now.
    // -1 = the name list; -7 = your own detail (offline);
    // otherwise the Photon actor number being viewed.
    private int modsDetailActor = -1;
    private readonly System.Collections.Generic.List<Btn> settingsBtns = new System.Collections.Generic.List<Btn>();
    private readonly System.Collections.Generic.List<Text> settingsTexts = new System.Collections.Generic.List<Text>();
    private const float SettingsRebase = 82f;    // rows were laid out from the panel top; shift down under the tab row
    private const float SettingsViewTop = 96f;   // visible band, panel coords
    private const float SettingsViewBottom = -148f;
    private const float SettingsMaxScroll = 230f; // round 82: grew with the lobby env rows
    private Text titleRoleText;
    private Text footerText;
    private Text diagStatusText;
    private string lastAction = "";
    private GameObject pointerBeam;
    private MeshRenderer pointerBeamRenderer;
    public static int MenuButtonPresses;
    private Font donorFont;
    private readonly List<Btn> clickables = new List<Btn>();
    private readonly List<Btn> contentBtns = new List<Btn>();
    private Btn hovered;

    private bool open;
    private int tab; // 0 staff, 1 troll, 2 sound, 3 move, 4 bot
    private string armedGun = "";
    private bool prevMenu;
    private bool prevTrigger;
    private float nextSnowball;
    private int pendingKickActor = -1;
    private float pendingKickUntil;
    private string banReason = "toxicity"; // ban gun payload; cycled from the Staff tab
    private float footerTimer;
    private string lastRole = null;

    // Movement toggles (Move tab). ON values match the staff spec: arm cap
    // effectively removed, jump above the 1.4x scene-edit values. While a
    // toggle is on, the values are re-asserted every frame, because
    // PhotonNetworkController resets jump values at room join.
    private const float HeadAdminArmScale = 1.4f; // Sans's long-arms factor
    private const float ArmReachOn = 10000f;
    private const float JumpSpeedOn = 12f;
    private const float JumpMultOn = 2f;
    private const float ArmReachOff = 1.5f;
    private const float JumpSpeedOff = 6.5f;
    private const float JumpMultOff = 1.1f;

    // Snowball spam interval lives in FalconStaffSettings (Settings tab).

    private FalconSoundboard soundboard;
    private FalconCoordCube coordCube;

    private void Awake()
    {
        FalconStaffWebhook.Init(this);
    }

    private void Start()
    {
        soundboard = GetComponent<FalconSoundboard>();
        if (soundboard == null) soundboard = gameObject.AddComponent<FalconSoundboard>();
        coordCube = GetComponent<FalconCoordCube>();
    }

    private void Update()
    {
        // Input is read BEFORE the role gate (round 5): after build #8's
        // Two input paths: the XR menu button, and the game's own
        // ControllerInputPoller (left secondary / Y), whose data the
        // whole game runs on. Presses are counted either way; the count
        // shows on the status page and the computer's RAVE page.
        InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        bool menuBtn;
        left.TryGetFeatureValue(CommonUsages.menuButton, out menuBtn);
        bool yBtn = ControllerInputPoller.instance != null && ControllerInputPoller.instance.leftControllerSecondaryButton;
        bool anyBtn = menuBtn || yBtn;
        bool menuEdge = anyBtn && !prevMenu;
        prevMenu = anyBtn;
        if (menuEdge)
        {
            MenuButtonPresses++;
            FalconStaffRole.EnsureRole();
            // Round 85 (Sans): everyone gets the menu now - it is
            // the PLAYER menu for players (player tabs only) and
            // the STAFF menu for staff. This reverses the build
            // #12 no-open rule on purpose: the player tabs are
            // the point of the feature.
            open = !open;
            if (open) { EnsurePanel(); RebuildContent(); }
            SetPanelVisible(open);
        }

        bool staff = FalconStaffRole.IsStaffOrHigher;

        // Role can arrive (login) or change while the menu exists.
        if (lastRole != FalconStaffRole.Role)
        {
            lastRole = FalconStaffRole.Role;
            RefreshTitle();
            RefreshTabs();
            if (open) RebuildContent();
        }

        InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool trig;
        right.TryGetFeatureValue(CommonUsages.triggerButton, out trig);
        trig = trig || (ControllerInputPoller.instance != null && ControllerInputPoller.instance.rightControllerIndexFloat > 0.5f);
        bool trigEdge = trig && !prevTrigger;
        prevTrigger = trig;

        if (open && panelRoot == null) { EnsurePanel(); if (panelRoot != null) RebuildContent(); }

        // Round 64: scrollable tabs scroll with the LEFT
        // thumbstick (the menu hand) - push down to reveal the
        // lower rows. Round 85: any tab that built a scroll root
        // (Settings, Mods) scrolls, with its own max.
        if (open && settingsRoot != null)
        {
            Vector2 axis;
            if (left.TryGetFeatureValue(CommonUsages.primary2DAxis, out axis) && Mathf.Abs(axis.y) > 0.25f)
            {
                float ns = Mathf.Clamp(settingsScroll - axis.y * 320f * Time.deltaTime, 0f, curMaxScroll);
                if (ns != settingsScroll) { settingsScroll = ns; ApplySettingsScroll(); }
            }
        }

        hovered = open ? ComputeHover() : null;
        PaintButtons();

        // Round 86: horizontal tab scroll - hover any tab and
        // push the LEFT thumbstick sideways.
        if (open && hovered != null && tabBtns.Contains(hovered))
        {
            Vector2 ax;
            if (left.TryGetFeatureValue(CommonUsages.primary2DAxis, out ax) && Mathf.Abs(ax.x) > 0.25f)
            {
                float no = tabOffset - ax.x * 300f * Time.deltaTime;
                if (Mathf.Abs(no - tabOffset) > 0.01f) { tabOffset = no; ApplyTabScroll(); }
            }
        }

        if (open && hovered != null)
        {
            if (trigEdge && hovered.onClick != null) { FalconSessionLog.Log("TAP " + hovered.label); hovered.onClick(); }
        }
        else if (staff && armedGun == "snowball")
        {
            if (trig) SnowballSpam();
        }
        else if (trigEdge && (staff || armedGun == "report" || armedGun == "mute"))
        {
            // Round 85: the player guns fire for everyone; staff
            // guns keep their staff gate inside Fire().
            GunFire();
        }


        UpdatePointerLine();

        if (open)
        {
            footerTimer -= Time.deltaTime;
            if (footerTimer <= 0f) { footerTimer = 0.5f; RefreshFooter(); if (!staff) RefreshDiagStatus(); }
            // Round 85: the mod checker is live while it is the
            // open tab. Round 86: the 1 s tick rebuilds ONLY
            // when the data it would draw actually changed -
            // the blind rebuild churned the panel every second.
            if (panelRoot != null && curTabs.Count > 0 && curTabs[Mathf.Clamp(tab, 0, curTabs.Count - 1)] == "mods" && Time.time >= nextCheckerRefresh)
            {
                nextCheckerRefresh = Time.time + 1f;
                string sig = ModsSignature();
                if (sig != lastModsSig || lastBuiltTabId != "mods") { lastModsSig = sig; RebuildContent(); }
            }
        }
    }

    // Round 85: the menu renames itself by the holder's role.
    private void RefreshTitle()
    {
        if (panelRoot == null) return;
        bool staff = FalconStaffRole.IsStaffOrHigher;
        if (titleText != null) titleText.text = staff ? "FALCON STAFF MENU" : "FALCON PLAYER MENU";
        if (titleRoleText != null) titleRoleText.text = staff ? "FALCON STAFF - " + FalconStaffRole.Role.ToUpperInvariant() : "FALCON PLAYER";
    }

    // Round 85: build the tab bar for the holder's role.
    // Round 86 (Sans): everyone gets Guns / Move / Visual /
    // Mods; staff add Staff; headadmin adds Troll / Sound /
    // Settings. The bar scrolls horizontally at full size.
    private void RefreshTabs()
    {
        if (panelRoot == null) return;
        foreach (Btn b in tabBtns)
        {
            clickables.Remove(b);
            if (b.img != null) Destroy(b.img.gameObject);
            if (b.txt != null) Destroy(b.txt.gameObject);
        }
        tabBtns.Clear();
        tabBaseCx.Clear();
        curTabs.Clear();
        bool staff = FalconStaffRole.IsStaffOrHigher;
        bool head = FalconStaffRole.IsHeadAdmin;
        curTabs.Add("guns");
        curTabs.Add("move");
        curTabs.Add("visual");
        curTabs.Add("mods");
        if (staff) curTabs.Add("staff");
        if (head) { curTabs.Add("troll"); curTabs.Add("sound"); curTabs.Add("settings"); }
        if (tab >= curTabs.Count) tab = 0;
        // Round 86: tabs are FULL SIZE (56 wide) and the bar
        // scrolls horizontally (left stick X while hovering a
        // tab). Round 88: tabs live under the TabBar mask root
        // (local cy 0; panel cy stays 116 for hover math) and
        // are clipped by the mask, never SetActive-popped;
        // scrollHidden only gates clicks, by center position.
        const float w = 56f;
        for (int i = 0; i < curTabs.Count; i++)
        {
            int idx = i;
            string id = curTabs[i];
            string label = id == "staff" ? "Staff" : id == "troll" ? "Troll" : id == "sound" ? "Sound" : id == "guns" ? "Guns" : id == "move" ? "Move" : id == "visual" ? "Visual" : id == "settings" ? "Settings" : "Mods";
            float baseCx = -96f + i * 62f;
            Btn b = MakeButton(tabBarRoot != null ? tabBarRoot : panelRoot.GetComponent<RectTransform>(), baseCx, 0f, w, 28f, label,
                () => { tab = idx; RebuildContent(); },
                () => tab == idx ? 1 : 0);
            b.cy = 116f; // hover math is panel-space; the mask root sits at cy 116
            tabBtns.Add(b);
            tabBaseCx.Add(baseCx);
            clickables.Add(b);
        }
        tabOffset = 0f;
        ApplyTabScroll();
    }

    private float TabMinOffset()
    {
        if (tabBaseCx.Count == 0) return 0f;
        float right = tabBaseCx[tabBaseCx.Count - 1] + 28f;
        return Mathf.Min(0f, 132f - right);
    }

    private void ApplyTabScroll()
    {
        tabOffset = Mathf.Clamp(tabOffset, TabMinOffset(), 0f);
        for (int i = 0; i < tabBtns.Count && i < tabBaseCx.Count; i++)
        {
            Btn b = tabBtns[i];
            b.cx = tabBaseCx[i] + tabOffset;
            // Round 88: NO SetActive windowing - the RectMask2D
            // on the TabBar root clips tabs as they slide, so
            // nothing pops in or out at the edges. scrollHidden
            // is click-gating only: a tab whose center is
            // outside the window cannot be clicked.
            b.scrollHidden = b.cx < -134f || b.cx > 134f;
            if (b.img != null) b.img.rectTransform.anchoredPosition = new Vector2(b.cx, 0f);
            if (b.txt != null) b.txt.rectTransform.anchoredPosition = new Vector2(b.cx, 0f);
        }
        // Round 88 (Sans: "there's no bar showing you can
        // scroll"): a real scrollbar under the tab bar - the
        // track shows while the bar overflows, the thumb's
        // width is the visible fraction of the strip and its
        // position is the scroll fraction.
        float min = TabMinOffset();
        bool overflow = min < 0f && tabBaseCx.Count > 0;
        if (tabScrollTrack != null) tabScrollTrack.gameObject.SetActive(overflow);
        if (tabScrollThumb != null)
        {
            tabScrollThumb.gameObject.SetActive(overflow);
            if (overflow)
            {
                float totalW = (tabBaseCx[tabBaseCx.Count - 1] + 28f) - (tabBaseCx[0] - 28f);
                float thumbW = Mathf.Clamp(268f * 268f / totalW, 24f, 268f);
                float frac = tabOffset / min;
                float thumbCx = -134f + thumbW / 2f + frac * (268f - thumbW);
                tabScrollThumb.rectTransform.sizeDelta = new Vector2(thumbW, 3f);
                tabScrollThumb.rectTransform.anchoredPosition = new Vector2(thumbCx, 98.5f);
            }
        }
    }

    // Round 86: signature of what the mod checker would draw -
    // the 1 s tick rebuilds only when this changes.
    private static string ModsSignature()
    {
        var sb = new System.Text.StringBuilder();
        try
        {
            if (PhotonNetwork.InRoom && PhotonNetwork.PlayerList != null)
            {
                foreach (var p in PhotonNetwork.PlayerList)
                {
                    if (p == null) continue;
                    sb.Append(p.ActorNumber).Append(':');
                    object mo;
                    if (p.CustomProperties != null && p.CustomProperties.TryGetValue("falcon_mods", out mo) && mo != null) sb.Append(mo.ToString());
                    sb.Append(';');
                }
            }
            else sb.Append("off:").Append(FalconOptionsDriver.CurrentFps);
        }
        catch { }
        return sb.ToString();
    }

    // ---------------- panel construction ----------------

    private Font FindDonorFont()
    {
        if (donorFont != null) return donorFont;
        foreach (Text t in FindObjectsOfType<Text>())
        {
            if (t != null && t.font != null) { donorFont = t.font; break; }
        }
        return donorFont;
    }

    private void EnsurePanel()
    {
        if (panelRoot != null) { AttachPanel(); return; }
        if (FindDonorFont() == null) return; // scene texts not up yet; next open retries

        panelRoot = new GameObject("FalconStaffPanel");
        RectTransform rt = panelRoot.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(PanelW, PanelH);
        Canvas cv = panelRoot.AddComponent<Canvas>();
        cv.renderMode = RenderMode.WorldSpace;
        CanvasScaler scaler = panelRoot.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 8f;

        MakeImage(rt, 0f, 0f, PanelW, PanelH, BgColor);
        titleText = MakeText(rt, 0f, 168f, 280f, 26f, 17, TextAnchor.MiddleCenter);
        titleRoleText = MakeText(rt, 0f, 146f, 280f, 18f, 12, TextAnchor.MiddleCenter);
        titleRoleText.color = DimColor;
        lastRole = FalconStaffRole.Role;
        RefreshTitle();
        // Round 88: the TabBar mask root must exist before
        // RefreshTabs builds the tabs under it.
        var tabBarGo = new GameObject("TabBar");
        tabBarRoot = tabBarGo.AddComponent<RectTransform>();
        tabBarRoot.SetParent(rt, false);
        tabBarRoot.anchorMin = tabBarRoot.anchorMax = tabBarRoot.pivot = new Vector2(0.5f, 0.5f);
        tabBarRoot.anchoredPosition = new Vector2(0f, 116f);
        tabBarRoot.sizeDelta = new Vector2(268f, 30f);
        tabBarGo.AddComponent<RectMask2D>();
        RefreshTabs();
        // Round 88: the tab scrollbar (track + thumb, driven
        // by ApplyTabScroll; hidden when the bar fits).
        tabScrollTrack = MakeImage(rt, 0f, 98.5f, 268f, 3f, new Color(1f, 1f, 1f, 0.16f));
        tabScrollThumb = MakeImage(rt, 0f, 98.5f, 60f, 3f, new Color(1f, 1f, 1f, 0.55f));
        ApplyTabScroll();

        var contentGo = new GameObject("Content");
        contentRoot = contentGo.AddComponent<RectTransform>();
        contentRoot.SetParent(rt, false);
        contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
        contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
        contentRoot.pivot = new Vector2(0.5f, 0.5f);
        contentRoot.anchoredPosition = Vector2.zero;
        contentRoot.sizeDelta = new Vector2(PanelW, PanelH);

        footerText = MakeText(rt, 0f, -164f, 280f, 24f, 11, TextAnchor.MiddleCenter);
        footerText.color = DimColor;

        AttachPanel();
        SetPanelVisible(open);
    }

    private void AttachPanel()
    {
        if (panelRoot == null || Player.Instance == null || Player.Instance.leftHandTransform == null) return;
        Transform anchor = Player.Instance.leftHandTransform;
        if (panelRoot.transform.parent != anchor)
        {
            panelRoot.transform.SetParent(anchor, false);
            panelRoot.transform.localPosition = PanelLocalPos;
            panelRoot.transform.localRotation = Quaternion.Euler(PanelLocalEuler);
            panelRoot.transform.localScale = Vector3.one * PanelScale;
        }
    }

    private void SetPanelVisible(bool v)
    {
        if (panelRoot != null) panelRoot.SetActive(v);
    }

    private Image MakeImage(RectTransform parent, float cx, float cy, float w, float h, Color color)
    {
        var go = new GameObject("img");
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cx, cy);
        rt.sizeDelta = new Vector2(w, h);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private Text MakeText(RectTransform parent, float cx, float cy, float w, float h, int size, TextAnchor align)
    {
        var go = new GameObject("txt");
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cx, cy);
        rt.sizeDelta = new Vector2(w, h);
        Text t = go.AddComponent<Text>();
        t.font = FindDonorFont();
        t.fontSize = size;
        t.alignment = align;
        t.color = TextColor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private Btn MakeButton(RectTransform parent, float cx, float cy, float w, float h, string label, System.Action onClick, System.Func<int> state)
    {
        Image img = MakeImage(parent, cx, cy, w, h, BtnNormal);
        Text t = MakeText(parent, cx, cy, w, h, 13, TextAnchor.MiddleCenter);
        t.text = label;
        return new Btn { cx = cx, cy = cy, w = w, h = h, img = img, txt = t, label = label, onClick = onClick, state = state };
    }

    // ---------------- content ----------------

    private void ClearContent()
    {
        foreach (Btn b in contentBtns) clickables.Remove(b);
        contentBtns.Clear();
        settingsBtns.Clear();
        settingsTexts.Clear();
        settingsRoot = null;
        settingsThumb = null;
        buildParent = null;
        buildingSettings = false;
        if (contentRoot != null)
        {
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
                Destroy(contentRoot.GetChild(i).gameObject);
        }
    }

    private Btn ContentButton(float cx, float cy, float w, float h, string label, System.Action onClick, System.Func<int> state)
    {
        Btn b = MakeButton(buildParent != null ? buildParent : contentRoot, cx, cy, w, h, label, onClick, state);
        contentBtns.Add(b);
        clickables.Add(b);
        if (buildingSettings) settingsBtns.Add(b);
        return b;
    }

    private Text ContentText(float cx, float cy, float w, float h, int size, string text)
    {
        Text t = MakeText(buildParent != null ? buildParent : contentRoot, cx, cy, w, h, size, TextAnchor.MiddleCenter);
        t.color = DimColor;
        t.text = text;
        if (buildingSettings) settingsTexts.Add(t);
        return t;
    }

    // Round 64: slide the settings sub-root, hide rows outside the
    // visible band (so they can neither draw over the tabs/footer
    // nor be clicked through them), and move the scrollbar thumb.
    private void ApplySettingsScroll()
    {
        if (settingsRoot == null) return;
        float off = settingsScroll - SettingsRebase;
        settingsRoot.anchoredPosition = new Vector2(0f, off);
        foreach (Btn b in settingsBtns)
        {
            b.yOff = off;
            float y = b.cy + off;
            b.scrollHidden = y > SettingsViewTop || y < SettingsViewBottom;
            if (b.img != null) b.img.gameObject.SetActive(!b.scrollHidden);
            if (b.txt != null) b.txt.gameObject.SetActive(!b.scrollHidden);
        }
        foreach (Text t in settingsTexts)
        {
            if (t == null) continue;
            float y = t.rectTransform.anchoredPosition.y + off;
            t.gameObject.SetActive(y <= SettingsViewTop && y >= SettingsViewBottom);
        }
        if (settingsThumb != null)
        {
            float frac = curMaxScroll > 0f ? settingsScroll / curMaxScroll : 0f;
            settingsThumb.anchoredPosition = new Vector2(141f, 19.5f - frac * 91f);
        }
    }

    private void RebuildContent()
    {
        if (panelRoot == null || contentRoot == null) return;
        ClearContent();
        // Round 85: tabs are role-built ids now (see RefreshTabs);
        // everyone reaches this - players get their own tabs.
        string tid = curTabs.Count > 0 ? curTabs[Mathf.Clamp(tab, 0, curTabs.Count - 1)] : "guns";
        if (tid != lastBuiltTabId) { if (tid == "settings" || tid == "mods") settingsScroll = 0f; lastBuiltTabId = tid; }
        if (tid != "mods") modsDetailActor = -1; // round 87: leaving the checker returns it to the name list

        if (tid == "staff")
        {
            ContentButton(0f, 84f, 268f, 24f, "Kick gun (staff+)", () => { armedGun = "kick"; RebuildContent(); }, () => armedGun == "kick" ? 2 : 0);
            if (FalconStaffRole.CanInstantBan)
            {
                ContentButton(-68f, 57f, 132f, 24f, "Ban gun", () => { armedGun = "ban"; RebuildContent(); }, () => armedGun == "ban" ? 2 : 0);
                ContentButton(68f, 57f, 132f, 24f, "Reason: " + banReason, () =>
                {
                    banReason = banReason == "toxicity" ? "cheating" : (banReason == "cheating" ? "hatespeech" : "toxicity");
                    RebuildContent();
                }, () => 0);
            }
            else
            {
                ContentText(0f, 57f, 268f, 24f, 11, FalconStaffRole.Tier2 ? "Ban gun: not for tier-2 roles." : "Ban gun: admin tier and up.");
            }
            ContentButton(-68f, 30f, 132f, 24f, "Disarm", () => { armedGun = ""; RebuildContent(); }, () => 0);
            ContentButton(68f, 30f, 132f, 24f, FalconStaffRole.LockOn ? "Staff lock: ON" : "Staff lock: OFF", () =>
            {
                FalconStaffRole.SetLock(!FalconStaffRole.LockOn);
                FalconSessionLog.Log(FalconStaffRole.LockOn ? "LOCK ON" : "LOCK OFF");
                lastAction = FalconStaffRole.LockOn ? "lock on - staff can't touch you" : "lock off";
                RebuildContent();
            }, () => FalconStaffRole.LockOn ? 1 : 0);
            ContentText(0f, 4f, 268f, 20f, 11, "Lock ON: other staff can't kick, ban,\ntroll or quit you. Self-tests still work.");
            ContentButton(0f, -24f, 268f, 24f, "Close lobby (everyone leaves)", () =>
            {
                FalconStaffNet.CloseLobby();
                lastAction = "lobby closed";
            }, () => 0);
            ContentText(0f, -56f, 268f, 44f, 11,
                FalconStaffRole.CanInstantBan
                    ? "Your reports ban instantly (admin tier)."
                    : (FalconStaffRole.Tier2
                        ? "Tier-2: reports log only, no instant ban.\nTier-2 staff can't kick or ban each other."
                        : "Your reports log to the webhook only."));
            ContentText(0f, -86f, 268f, 20f, 11, "Close lobby boots EVERYONE here to the stump.");
        }
        else if (tid == "troll" && FalconStaffRole.IsHeadAdmin)
        {
            ContentButton(-68f, 80f, 132f, 24f, "Start rave", () =>
            {
                FalconRave.TryStartFromComputer();
                lastAction = FalconRave.LastResult == "" ? "rave: no result" : FalconRave.LastResult.ToLowerInvariant();
                if (lastAction.Length > 44) lastAction = lastAction.Substring(0, 44);
            }, () => FalconRave.IsRaving ? 1 : 0);
            ContentButton(68f, 80f, 132f, 24f, "End rave", () =>
            {
                FalconRave.StopRaveFromMenu();
                lastAction = FalconRave.LastResult == "" ? "rave: no result" : FalconRave.LastResult.ToLowerInvariant();
            }, () => FalconRave.IsRaving ? 1 : 0);
            ContentButton(0f, 53f, 268f, 24f, "Spooky music (lobby)", () =>
            {
                FalconStaffNet.SendToAll(FalconStaffNet.EvSpookyMusic, new object[0]);
                lastAction = "spooky sent";
            }, () => 0);

            string[] guns = { "fling", "fakelag", "creepify", "slip", "spin", "loudmic", "outfitrandom", "outfitall", "snowball", "quit", "tiny", "grow" };
            for (int i = 0; i < guns.Length; i++)
            {
                string g = guns[i];
                float cx = (i % 2 == 0) ? -68f : 68f;
                float cy = 26f - 27f * (i / 2);
                ContentButton(cx, cy, 132f, 24f, g, () => { armedGun = g; RebuildContent(); }, () => armedGun == g ? 2 : 0);
            }
            ContentText(0f, -128f, 268f, 18f, 11, "Headadmin: guns are live. Tiny/grow last 10 s.");
        }
        else if (tid == "sound" && FalconStaffRole.IsHeadAdmin)
        {
            ContentButton(-68f, 80f, 132f, 24f, "Refresh", () => { if (soundboard != null) soundboard.Refresh(); RebuildContent(); }, () => 0);
            ContentButton(68f, 80f, 132f, 24f, "Stop", () => { if (soundboard != null) soundboard.Stop(); }, () => 0);
            if (soundboard != null)
            {
                int shown = 0;
                for (int i = 0; i < soundboard.Files.Count && shown < 7; i++)
                {
                    string path = soundboard.Files[i];
                    float cy = 44f - 27f * shown;
                    ContentButton(0f, cy, 268f, 24f, System.IO.Path.GetFileName(path), () => soundboard.Play(path), () => 0);
                    shown++;
                }
                if (soundboard.Files.Count > shown)
                    ContentText(0f, -152f, 268f, 16f, 11, "+" + (soundboard.Files.Count - shown) + " more (paging later)");
                if (soundboard.Files.Count == 0)
                    ContentText(0f, 30f, 268f, 20f, 11, "No files in the Soundboard folder.");
            }
        }
        else if (tid == "move")
        {
            // Round 85 (Sans): the player Movement tab - adjustable
            // long arms + speed boost, open to EVERY role (the
            // computer ITEMS page toggles the same options).
            // Nametags live on the Visual tab. Staff extras below.
            ContentButton(-75f, 84f, 118f, 24f, "Arms: " + (FalconPlayerOptions.LongArms ? "ON" : "OFF"),
                () => { FalconPlayerOptions.ToggleLongArms(); FalconSessionLog.Log("MENU arms " + FalconPlayerOptions.LongArms); RebuildContent(); },
                () => FalconPlayerOptions.LongArms ? 1 : 0);
            ContentButton(28f, 84f, 36f, 24f, "-", () => { FalconPlayerOptions.SetArmScale(FalconPlayerOptions.ArmScaleValue - 0.05f); RebuildContent(); }, () => 0);
            ContentText(66f, 84f, 44f, 24f, 12, "x" + FalconPlayerOptions.ArmScaleValue.ToString("0.00"));
            ContentButton(104f, 84f, 36f, 24f, "+", () => { FalconPlayerOptions.SetArmScale(FalconPlayerOptions.ArmScaleValue + 0.05f); RebuildContent(); }, () => 0);

            ContentButton(-75f, 57f, 118f, 24f, "Speed: " + (FalconPlayerOptions.SpeedBoost ? "ON" : "OFF"),
                () => { FalconPlayerOptions.ToggleSpeedBoost(); FalconSessionLog.Log("MENU speed " + FalconPlayerOptions.SpeedBoost); RebuildContent(); },
                () => FalconPlayerOptions.SpeedBoost ? 1 : 0);
            ContentButton(28f, 57f, 36f, 24f, "-", () => { FalconPlayerOptions.SetBoostTarget(FalconPlayerOptions.BoostTarget - 0.05f); RebuildContent(); }, () => 0);
            ContentText(66f, 57f, 44f, 24f, 12, "x" + FalconPlayerOptions.BoostTarget.ToString("0.00"));
            ContentButton(104f, 57f, 36f, 24f, "+", () => { FalconPlayerOptions.SetBoostTarget(FalconPlayerOptions.BoostTarget + 0.05f); RebuildContent(); }, () => 0);

            if (FalconStaffRole.IsStaffOrHigher)
            {
                ContentText(0f, -26f, 268f, 18f, 11, "- STAFF EXTRAS -");
                ContentButton(0f, -53f, 268f, 24f, "Fly: " + (FalconFly.Enabled ? "ON" : "OFF"),
                    () => { FalconFly.Enabled = !FalconFly.Enabled; FalconSessionLog.Log(FalconFly.Enabled ? "FLY ON" : "FLY OFF"); lastAction = FalconFly.Enabled ? "fly on" : "fly off"; RebuildContent(); },
                    () => FalconFly.Enabled ? 1 : 0);
                ContentButton(0f, -80f, 268f, 24f, "Coord cube: " + (coordCube != null && coordCube.CubeEnabled ? "ON" : "OFF"),
                    () => { if (coordCube != null) coordCube.SetEnabled(!coordCube.CubeEnabled); RebuildContent(); },
                    () => coordCube != null && coordCube.CubeEnabled ? 1 : 0);
                ContentText(0f, -108f, 268f, 28f, 10, "Fly: left stick = head direction, right stick Y = up/down.\nCube rides the right hand; A = drop / pick up.");
            }
            else
            {
                ContentText(0f, -40f, 268f, 44f, 11, "Arms: x0.99 (shorter than stock) to x1.40.\nSpeed: x1.05 (just under stock) to x1.82.");
            }
        }
        else if (tid == "visual")
        {
            // Round 88 (Sans): RTX and PhaseSync are REMOVED
            // entirely - RTX was ugly and never changed,
            // PhaseSync never worked; RTX gets redone later.
            // The Visual tab keeps nametags, its one survivor.
            ContentButton(0f, 84f, 268f, 24f, "Nametags: " + (FalconPlayerOptions.NameTags ? "ON" : "OFF"),
                () => { FalconPlayerOptions.ToggleNameTags(); FalconSessionLog.Log("MENU nametags " + FalconPlayerOptions.NameTags); RebuildContent(); },
                () => FalconPlayerOptions.NameTags ? 1 : 0);
            ContentText(0f, -8f, 268f, 44f, 11, "Nametags: names over players' heads.");
        }
        else if (tid == "settings" && FalconStaffRole.IsHeadAdmin)
        {
            // Settings tab (Jacob's suggestion): gun tuning with
            // steppers. Round 54 (Sans): fly speed + shrink/grow scale
            // steppers and the SAVE LOG button join the tab; rows
            // compressed to fit (panel is 420 tall since round 54).
            // Every change lands in the session log.
            // Round 64: rows build into the scroll sub-root (see the
            // fields block) - ApplySettingsScroll at the end of the
            // branch positions and clips them.
            curMaxScroll = SettingsMaxScroll;
            var sgo = new GameObject("SettingsScroll");
            settingsRoot = sgo.AddComponent<RectTransform>();
            settingsRoot.SetParent(contentRoot, false);
            settingsRoot.anchorMin = new Vector2(0.5f, 0.5f);
            settingsRoot.anchorMax = new Vector2(0.5f, 0.5f);
            settingsRoot.pivot = new Vector2(0.5f, 0.5f);
            settingsRoot.anchoredPosition = Vector2.zero;
            settingsRoot.sizeDelta = new Vector2(PanelW, PanelH);
            buildParent = settingsRoot;
            buildingSettings = true;
            ContentText(0f, 170f, 268f, 18f, 11, "Snowball spam interval");
            ContentButton(-100f, 146f, 52f, 24f, "-10", () => { FalconStaffSettings.SnowballMs = Mathf.Clamp(FalconStaffSettings.SnowballMs - 10, 1, 1000); FalconSessionLog.Log("SET snowball ms = " + FalconStaffSettings.SnowballMs); RebuildContent(); }, () => 0);
            ContentButton(100f, 146f, 52f, 24f, "+10", () => { FalconStaffSettings.SnowballMs = Mathf.Clamp(FalconStaffSettings.SnowballMs + 10, 1, 1000); FalconSessionLog.Log("SET snowball ms = " + FalconStaffSettings.SnowballMs); RebuildContent(); }, () => 0);
            ContentText(0f, 146f, 120f, 24f, 13, FalconStaffSettings.SnowballMs + " ms");

            ContentText(0f, 124f, 268f, 18f, 11, "Spin speed (sent to target)");
            ContentButton(-100f, 100f, 52f, 24f, "-1", () => { FalconStaffSettings.SpinSpeed = Mathf.Clamp(FalconStaffSettings.SpinSpeed - 1, 1, 30); FalconSessionLog.Log("SET spin = " + FalconStaffSettings.SpinSpeed); RebuildContent(); }, () => 0);
            ContentButton(100f, 100f, 52f, 24f, "+1", () => { FalconStaffSettings.SpinSpeed = Mathf.Clamp(FalconStaffSettings.SpinSpeed + 1, 1, 30); FalconSessionLog.Log("SET spin = " + FalconStaffSettings.SpinSpeed); RebuildContent(); }, () => 0);
            ContentText(0f, 100f, 120f, 24f, 13, "speed " + FalconStaffSettings.SpinSpeed);

            ContentText(0f, 78f, 268f, 18f, 11, "Fling force");
            ContentButton(-100f, 54f, 52f, 24f, "-5", () => { FalconStaffSettings.FlingForce = Mathf.Clamp(FalconStaffSettings.FlingForce - 5f, 5f, 100f); FalconSessionLog.Log("SET fling = " + FalconStaffSettings.FlingForce.ToString("0")); RebuildContent(); }, () => 0);
            ContentButton(100f, 54f, 52f, 24f, "+5", () => { FalconStaffSettings.FlingForce = Mathf.Clamp(FalconStaffSettings.FlingForce + 5f, 5f, 100f); FalconSessionLog.Log("SET fling = " + FalconStaffSettings.FlingForce.ToString("0")); RebuildContent(); }, () => 0);
            ContentText(0f, 54f, 120f, 24f, 13, FalconStaffSettings.FlingForce.ToString("0"));

            ContentText(0f, 32f, 268f, 18f, 11, "Snowballs per fire (burst)");
            ContentButton(-100f, 8f, 52f, 24f, "-10", () => { FalconStaffSettings.SnowballBurst = Mathf.Clamp(FalconStaffSettings.SnowballBurst - 10, 1, 50); FalconSessionLog.Log("SET burst = " + FalconStaffSettings.SnowballBurst); RebuildContent(); }, () => 0);
            ContentButton(100f, 8f, 52f, 24f, "+10", () => { FalconStaffSettings.SnowballBurst = Mathf.Clamp(FalconStaffSettings.SnowballBurst + 10, 1, 50); FalconSessionLog.Log("SET burst = " + FalconStaffSettings.SnowballBurst); RebuildContent(); }, () => 0);
            ContentText(0f, 8f, 120f, 24f, 13, FalconStaffSettings.SnowballBurst > 30 ? "x" + FalconStaffSettings.SnowballBurst + " LAG!" : "x" + FalconStaffSettings.SnowballBurst);

            ContentText(0f, -14f, 268f, 18f, 11, "Fly speed");
            ContentButton(-100f, -38f, 52f, 24f, "-1", () => { FalconStaffSettings.FlySpeed = Mathf.Clamp(FalconStaffSettings.FlySpeed - 1f, 1f, 20f); FalconSessionLog.Log("SET fly speed = " + FalconStaffSettings.FlySpeed.ToString("0")); RebuildContent(); }, () => 0);
            ContentButton(100f, -38f, 52f, 24f, "+1", () => { FalconStaffSettings.FlySpeed = Mathf.Clamp(FalconStaffSettings.FlySpeed + 1f, 1f, 20f); FalconSessionLog.Log("SET fly speed = " + FalconStaffSettings.FlySpeed.ToString("0")); RebuildContent(); }, () => 0);
            ContentText(0f, -38f, 120f, 24f, 13, FalconStaffSettings.FlySpeed.ToString("0") + " m/s");

            ContentText(0f, -60f, 268f, 18f, 11, "Shrink (tiny gun) scale");
            ContentButton(-100f, -84f, 52f, 24f, "-.1", () => { FalconStaffSettings.TinyScale = Mathf.Clamp(FalconStaffSettings.TinyScale - 0.1f, 0.1f, 0.9f); FalconSessionLog.Log("SET tiny scale = " + FalconStaffSettings.TinyScale.ToString("0.0")); RebuildContent(); }, () => 0);
            ContentButton(100f, -84f, 52f, 24f, "+.1", () => { FalconStaffSettings.TinyScale = Mathf.Clamp(FalconStaffSettings.TinyScale + 0.1f, 0.1f, 0.9f); FalconSessionLog.Log("SET tiny scale = " + FalconStaffSettings.TinyScale.ToString("0.0")); RebuildContent(); }, () => 0);
            ContentText(0f, -84f, 120f, 24f, 13, FalconStaffSettings.TinyScale.ToString("0.0") + "x");

            ContentText(0f, -106f, 268f, 18f, 11, "Grow gun scale");
            ContentButton(-100f, -130f, 52f, 24f, "-.2", () => { FalconStaffSettings.GrowScale = Mathf.Clamp(FalconStaffSettings.GrowScale - 0.2f, 1.2f, 4f); FalconSessionLog.Log("SET grow scale = " + FalconStaffSettings.GrowScale.ToString("0.0")); RebuildContent(); }, () => 0);
            ContentButton(100f, -130f, 52f, 24f, "+.2", () => { FalconStaffSettings.GrowScale = Mathf.Clamp(FalconStaffSettings.GrowScale + 0.2f, 1.2f, 4f); FalconSessionLog.Log("SET grow scale = " + FalconStaffSettings.GrowScale.ToString("0.0")); RebuildContent(); }, () => 0);
            ContentText(0f, -130f, 120f, 24f, 13, FalconStaffSettings.GrowScale.ToString("0.0") + "x");

            // Round 82 (Sans): lobby weather + time changers.
            // Room-scoped and broadcast - everyone in this lobby
            // gets the change (see FalconEnv); leaving clears it.
            ContentText(0f, -152f, 268f, 18f, 11, "Lobby weather (everyone here)");
            ContentButton(-100f, -176f, 52f, 24f, "<", () => { FalconEnv.CycleWeather(-1); RebuildContent(); }, () => 0);
            ContentButton(100f, -176f, 52f, 24f, ">", () => { FalconEnv.CycleWeather(1); RebuildContent(); }, () => 0);
            ContentText(0f, -176f, 120f, 24f, 13, FalconEnv.WeatherName(FalconEnv.WeatherOverride));

            ContentText(0f, -198f, 268f, 18f, 11, "Lobby time (everyone here)");
            ContentButton(-100f, -222f, 52f, 24f, "<", () => { FalconEnv.CycleTime(-1); RebuildContent(); }, () => 0);
            ContentButton(100f, -222f, 52f, 24f, ">", () => { FalconEnv.CycleTime(1); RebuildContent(); }, () => 0);
            ContentText(0f, -222f, 150f, 24f, 13, FalconEnv.TimeName(FalconEnv.TimeOverride));

            ContentButton(0f, -258f, 268f, 26f, "SAVE LOG", () => { bool ok = FalconSessionLog.Save(); lastAction = ok ? "log saved (files/session_log.txt)" : "LOG SAVE FAILED"; RebuildContent(); }, () => 0);
            ContentText(0f, -286f, 268f, 18f, 10, "Session is logged silently in memory - SAVE LOG writes it once.");
            buildParent = null;
            buildingSettings = false;
            // Scrollbar: track + thumb on the panel's right edge.
            MakeImage(contentRoot, 141f, -26f, 6f, 244f, new Color(1f, 1f, 1f, 0.15f));
            var thumbImg = MakeImage(contentRoot, 141f, 19.5f, 6f, 153f, new Color(1f, 1f, 1f, 0.55f));
            settingsThumb = thumbImg.rectTransform;
            ApplySettingsScroll();
        }
        else if (tid == "guns")
        {
            // Round 85 (Sans): the player guns. Report = files a
            // report for staff review, NEVER a ban. Mute = the
            // scoreboard mute on your client only.
            ContentText(0f, 84f, 268f, 20f, 12, "Player guns - point and pull the trigger.");
            ContentButton(0f, 57f, 268f, 26f, "Report gun (no ban)", () => { armedGun = "report"; RebuildContent(); }, () => armedGun == "report" ? 2 : 0);
            ContentButton(0f, 28f, 268f, 26f, "Mute gun (mutes for YOU)", () => { armedGun = "mute"; RebuildContent(); }, () => armedGun == "mute" ? 2 : 0);
            ContentButton(0f, -1f, 268f, 26f, "Disarm", () => { armedGun = ""; RebuildContent(); }, () => 0);
            ContentText(0f, -52f, 268f, 60f, 11, "Report gun: sends a report + voice clip\nto staff. It never bans anyone.\nMute gun: flips their mute button on the\nscoreboard (red) - only you stop hearing them.");
        }
        else if (tid == "mods")
        {
            // Round 85: the mod checker - every client publishes
            // its mod state + FPS as the "falcon_mods" player
            // property (FalconOptionsDriver).
            // Round 87 (Sans): "think of actual UI" - this is a
            // LIST -> DETAIL tab now. The list is a scrollable
            // column of player-name BUTTONS; tapping a name opens
            // that player's detail page. Round 87 also removed
            // the staff "discreet" string (it outed staff); staff
            // publish the same string as everyone (see
            // FalconPlayerOptions.ModStateString).
            var players = PhotonNetwork.PlayerList;
            Photon.Realtime.Player detail = null;
            if (modsDetailActor >= 0 && players != null)
            {
                foreach (var p in players)
                    if (p != null && p.ActorNumber == modsDetailActor) { detail = p; break; }
                if (detail == null) modsDetailActor = -1;
            }
            if (modsDetailActor == -7 || detail != null)
            {
                // ---- detail page ----
                string nm; string mods;
                if (modsDetailActor == -7)
                {
                    nm = "YOU (not in a room)";
                    mods = FalconPlayerOptions.ModStateString(FalconOptionsDriver.CurrentFps);
                }
                else
                {
                    nm = string.IsNullOrEmpty(detail.NickName) ? "???" : detail.NickName;
                    if (detail.IsLocal) nm += " (you)";
                    mods = "no data yet - their client has not published (old build, or still loading)";
                    object v;
                    if (detail.CustomProperties != null && detail.CustomProperties.TryGetValue("falcon_mods", out v))
                    {
                        string sv = v as string;
                        if (!string.IsNullOrEmpty(sv)) mods = sv;
                    }
                }
                ContentButton(-75f, 84f, 118f, 24f, "< Back", () => { modsDetailActor = -1; RebuildContent(); }, () => 0);
                ContentText(0f, 48f, 268f, 24f, 15, nm);
                ContentText(0f, 6f, 268f, 40f, 12, mods);
                ContentText(0f, -44f, 268f, 20f, 11, "Live - updates as their state changes.");
            }
            else
            {
                // ---- list page ----
                var mgo = new GameObject("SettingsScroll");
                settingsRoot = mgo.AddComponent<RectTransform>();
                settingsRoot.SetParent(contentRoot, false);
                settingsRoot.anchorMin = new Vector2(0.5f, 0.5f);
                settingsRoot.anchorMax = new Vector2(0.5f, 0.5f);
                settingsRoot.pivot = new Vector2(0.5f, 0.5f);
                settingsRoot.anchoredPosition = Vector2.zero;
                settingsRoot.sizeDelta = new Vector2(PanelW, PanelH);
                buildParent = settingsRoot;
                buildingSettings = true;
                ContentText(0f, 170f, 268f, 18f, 12, "MOD CHECKER - tap a name");
                float y = 138f;
                if (players == null || players.Length == 0)
                {
                    ContentButton(0f, y, 268f, 24f, "YOU (not in a room)", () => { modsDetailActor = -7; RebuildContent(); }, () => 0);
                    y -= 30f;
                }
                else
                {
                    foreach (var p in players)
                    {
                        if (p == null) continue;
                        int actor = p.ActorNumber;
                        string nm = string.IsNullOrEmpty(p.NickName) ? "???" : p.NickName;
                        if (p.IsLocal) nm += " (you)";
                        ContentButton(0f, y, 268f, 24f, nm, () => { modsDetailActor = actor; RebuildContent(); }, () => modsDetailActor == actor ? 1 : 0);
                        y -= 27f;
                    }
                }
                curMaxScroll = Mathf.Max(0f, 190f - y);
                buildParent = null;
                buildingSettings = false;
                MakeImage(contentRoot, 141f, -26f, 6f, 244f, new Color(1f, 1f, 1f, 0.15f));
                var mThumbImg = MakeImage(contentRoot, 141f, 19.5f, 6f, 153f, new Color(1f, 1f, 1f, 0.55f));
                settingsThumb = mThumbImg.rectTransform;
                ApplySettingsScroll();
            }
        }
        else
        {
            ContentText(0f, 40f, 268f, 24f, 12, "Headadmin only.");
        }

        RefreshFooter();
        PaintButtons();
    }

    private void RefreshFooter()
    {
        if (footerText == null) return;
        string playing = soundboard != null && soundboard.NowPlaying != "" ? " | " + soundboard.NowPlaying : "";
        string cube = "";
        if (coordCube != null && coordCube.CubeEnabled)
        {
            Vector3 p = coordCube.CubePosition;
            cube = string.Format(" | Cube {0:0.0}, {1:0.0}, {2:0.0}{3}", p.x, p.y, p.z, coordCube.Held ? " (held)" : "");
        }
        footerText.text = "Armed: " + (armedGun == "" ? "none" : armedGun) + playing + cube + (lastAction == "" ? "" : " | " + lastAction) + (FalconStaffNet.LastReceived == "" ? "" : " | rx " + FalconStaffNet.LastReceived);
    }

    private string DiagStatusString()
    {
        return "ROLE: " + (FalconStaffRole.Role == "" ? "NONE" : FalconStaffRole.Role.ToUpperInvariant())
            + "\nFETCH: " + FalconStaffRole.FetchState.ToUpperInvariant()
            + (FalconStaffRole.LastError == "" ? "" : "\nERROR: " + FalconStaffRole.LastError)
            + "\nMENU PRESSES: " + MenuButtonPresses;
    }

    private void RefreshDiagStatus()
    {
        if (diagStatusText != null) diagStatusText.text = DiagStatusString();
    }

    // ---------------- interaction ----------------

    private Btn ComputeHover()
    {
        if (panelRoot == null || !panelRoot.activeInHierarchy) return null;
        if (Player.Instance == null || Player.Instance.rightHandTransform == null) return null;
        Transform hand = Player.Instance.rightHandTransform;
        Transform pt = panelRoot.transform;
        Plane plane = new Plane(pt.forward, pt.position);
        Ray ray = new Ray(hand.position, hand.forward);
        float dist;
        if (!plane.Raycast(ray, out dist)) return null;
        Vector3 local = pt.InverseTransformPoint(ray.GetPoint(dist));
        for (int i = clickables.Count - 1; i >= 0; i--)
        {
            Btn b = clickables[i];
            if (b.scrollHidden) continue;
            if (Mathf.Abs(local.x - b.cx) <= b.w / 2f && Mathf.Abs(local.y - (b.cy + b.yOff)) <= b.h / 2f)
                return b;
        }
        return null;
    }

    private void PaintButtons()
    {
        foreach (Btn b in clickables)
        {
            if (b.img == null) continue;
            if (b == hovered) { b.img.color = BtnHover; continue; }
            int s = b.state != null ? b.state() : 0;
            b.img.color = s == 2 ? BtnArmed : (s == 1 ? BtnOn : BtnNormal);
        }
    }

    // Visible aim line from the right hand (Sans, build #9: "add a line
    // to the controller so I can point"). Shown while the menu is open
    // or a gun is armed; it traces the same ray the guns use.
    private void EnsurePointerLine()
    {
        // Round 13: the beam is a stretched primitive cube, NOT a
        // LineRenderer - round 6 built its material from
        // Shader.Find("Sprites/Default"), which is stripped from this
        // build; material creation threw inside Update every frame
        // the menu was open, killing the beam AND menu selection
        // (build #10: "no arrow, can't select"). CreatePrimitive's
        // material path is the same one the coord cube uses - it
        // demonstrably renders on device.
        if (pointerBeam != null) return;
        pointerBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pointerBeam.name = "FalconPointerBeam";
        Collider c = pointerBeam.GetComponent<Collider>();
        if (c != null) Destroy(c); // the beam must never eat raycasts
        pointerBeam.transform.SetParent(transform, false);
        pointerBeamRenderer = pointerBeam.GetComponent<MeshRenderer>();
        Material m = new Material(pointerBeamRenderer.sharedMaterial);
        m.color = new Color(0.45f, 1f, 0.45f);
        pointerBeamRenderer.material = m;
        pointerBeam.SetActive(false);
    }

    private void UpdatePointerLine()
    {
        Transform hand = Player.Instance != null ? Player.Instance.rightHandTransform : null;
        if ((!open && armedGun == "") || hand == null)
        {
            if (pointerBeam != null) pointerBeam.SetActive(false);
            return;
        }
        EnsurePointerLine();
        Vector3 from = hand.position + hand.forward * 0.05f;
        // Round 26: the no-hit length was 4 m, so aiming at the sky or
        // through a collider-less rig made the laser visibly cut off
        // mid-air (Sans, alt build #1). Default reach is now 30 m; a
        // physics hit shortens it, and with a gun armed the cone-picked
        // target shortens it to exactly touch the victim.
        float endDist = 30f;
        RaycastHit hit;
        if (Physics.Raycast(hand.position, hand.forward, out hit, 100f)) endDist = Mathf.Min(endDist, hit.distance);
        if (armedGun != "" && armedGun != "snowball")
        {
            VRRig picked = ConePickRig(hand);
            if (picked != null)
            {
                Vector3 tp = picked.headMesh != null ? picked.headMesh.transform.position : picked.transform.position + Vector3.up * 1.2f;
                endDist = Mathf.Min(endDist, Vector3.Distance(hand.position, tp));
            }
        }
        Vector3 to = hand.position + hand.forward * endDist;
        Vector3 dir = to - from;
        float len = dir.magnitude;
        if (len < 0.05f) { pointerBeam.SetActive(false); return; }
        pointerBeam.SetActive(true);
        pointerBeam.transform.position = from + dir * 0.5f;
        pointerBeam.transform.rotation = Quaternion.LookRotation(dir);
        pointerBeam.transform.localScale = new Vector3(0.008f, 0.008f, len);
    }

    // ---------------- guns (ported from FalconStaffMenu) ----------------

    private void GunFire()
    {
        if (armedGun == "" || armedGun == "snowball") return;
        Transform hand = Player.Instance != null ? Player.Instance.rightHandTransform : null;
        if (hand == null) return;
        // Self test first: a generous cone at your own head. Pointing a
        // gun at your own body must always register (Sans, build #10:
        // "can't shoot myself with any of the troll guns") - the
        // physics raycast rarely finds a VRRig on your own colliders,
        // and the tight cone pick demanded near-perfect aim at your
        // own face.
        VRRig ownRig = GorillaTagger.Instance != null ? GorillaTagger.Instance.offlineVRRig : null;
        if (ownRig != null && ownRig.headMesh != null)
        {
            Vector3 toOwn = ownRig.headMesh.transform.position - hand.position;
            if (toOwn.magnitude < 2.5f && Vector3.Angle(hand.forward, toOwn) < 30f)
            {
                FireSelf(armedGun);
                return;
            }
        }
        RaycastHit hit;
        VRRig rig = null;
        if (Physics.Raycast(hand.position, hand.forward, out hit, 100f))
            rig = hit.collider.GetComponentInParent<VRRig>();
        if (rig == null) rig = ConePickRig(hand); // rigs may have no raycastable colliders
        Photon.Realtime.Player rigPlayer = FalconStaffNet.PlayerOf(rig);
        if (rig == null || rigPlayer == null) { lastAction = "no target"; return; }
        if (rigPlayer.IsLocal)
        {
            FireSelf(armedGun);
            return;
        }
        Fire(armedGun, rig);
    }

    // Fallback targeting: the rig inside the tightest cone around the
    // hand's forward, no colliders required.
    private VRRig ConePickRig(Transform hand)
    {
        VRRig best = null;
        float bestAngle = 12f;
        foreach (VRRig rig in FindObjectsOfType<VRRig>())
        {
            if (rig == null || FalconStaffNet.PlayerOf(rig) == null) continue;
            Vector3 target = rig.headMesh != null ? rig.headMesh.transform.position : rig.transform.position + Vector3.up * 1.2f;
            Vector3 to = target - hand.position;
            if (to.magnitude < 0.4f || to.magnitude > 60f) continue;
            float angle = Vector3.Angle(hand.forward, to);
            if (angle < bestAngle) { bestAngle = angle; best = rig; }
        }
        return best;
    }

    private void FireSelf(string gun)
    {
        if (gun == "kick")
        {
            FalconStaffWebhook.Log("SELFTEST kick (dry run, not applied)");
            lastAction = "selftest kick (dry)";
            return;
        }
        if (gun == "quit")
        {
            // Round 78 (Sans's call, reversing the round-12 dry
            // rule for quit only): a self-shot REALLY closes your
            // game, so the quit gun can be proven on yourself.
            lastAction = "self quit (REAL)";
            if (FalconStaffNet.Instance != null) FalconStaffNet.Instance.SelfQuit();
            return;
        }
        if (gun == "ban")
        {
            FalconStaffWebhook.Log("SELFTEST ban (dry run, not applied)");
            lastAction = "selftest ban (dry)";
            return;
        }
        if (gun == "report" || gun == "mute")
        {
            lastAction = gun == "report" ? "can't report yourself" : "can't mute yourself";
            return;
        }
        if (!FalconStaffRole.IsHeadAdmin) { lastAction = "denied (role)"; return; }
        if (FalconStaffNet.Instance == null) return;
        int me = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
        switch (gun)
        {
            case "fling":
                Vector3 dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) + 0.5f;
                FalconStaffNet.Instance.ApplyLocal(FalconStaffNet.EvFling, new object[] { dir.normalized * FalconStaffSettings.FlingForce });
                break;
            case "fakelag":
                FalconStaffNet.Instance.ApplyLocal(FalconStaffNet.EvFakeLag, new object[0]);
                break;
            case "creepify":
                FalconStaffNet.Instance.ApplyLocal(FalconStaffNet.EvCreepify, new object[0]);
                break;
            case "slip":
                FalconStaffNet.Instance.ApplyLocal(FalconStaffNet.EvSlip, new object[0]);
                break;
            case "spin":
                FalconStaffNet.Instance.ApplyLocal(FalconStaffNet.EvSpin, new object[] { FalconStaffSettings.SpinDegPerSec });
                break;
            case "loudmic":
                FalconStaffNet.Instance.ApplyLocal(FalconStaffNet.EvLoudMic, new object[] { me });
                break;
            case "tiny":
                FalconStaffNet.ApplySizeEvent(me, FalconStaffSettings.TinyScale);
                break;
            case "grow":
                FalconStaffNet.ApplySizeEvent(me, FalconStaffSettings.GrowScale);
                break;
            case "outfitrandom":
                FalconStaffNet.Instance.ApplyLocal(FalconStaffNet.EvOutfitRandom, new object[0]);
                break;
            case "outfitall":
                FalconStaffNet.Instance.ApplyLocal(FalconStaffNet.EvOutfitAll, new object[0]);
                break;
            case "snowball":
                break; // snowball is shooter-side already
        }
        FalconStaffWebhook.Log("SELFTEST " + gun);
        lastAction = "selftest " + gun;
    }

    private static string TargetRole(Photon.Realtime.Player p)
    {
        if (p == null || p.CustomProperties == null) return "";
        object v;
        if (!p.CustomProperties.TryGetValue(FalconStaffRole.RoleKey, out v)) return "";
        string r = v as string;
        return r != null ? r : "";
    }

    private void Fire(string gun, VRRig targetRig)
    {
        Photon.Realtime.Player target = FalconStaffNet.PlayerOf(targetRig);
        string me = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.NickName : "?";
        string them = target.NickName;

        // Staff lock (round 43, Sans): a locked staffer cannot be
        // effected, kicked or banned by other staff. Checked here,
        // before anything is sent - and again on the target's own
        // client, which drops the event if it arrives anyway.
        if (FalconStaffRole.IsLocked(target))
        {
            lastAction = "TARGET LOCKED";
            FalconStaffWebhook.Log("SHOT BLOCKED (lock) " + gun + " on " + them + " by " + me);
            return;
        }

        // Tier-2 peer protection (round 45, Sans): tier-2 staff
        // cannot kick each other. The victim's client drops such
        // kicks too (FalconStaffNet) - this is the up-front refusal
        // so the shooter gets feedback instead of silence.
        if (gun == "kick" && FalconStaffRole.Tier2 && FalconStaffRole.IsTier2(target))
        {
            lastAction = "TIER2 - can't kick tier2";
            FalconStaffWebhook.Log("KICK REFUSED (tier2 peer) on " + them + " by " + me);
            return;
        }

        if (gun == "report")
        {
            // Round 85 (Sans): the PLAYER report gun. Same landing
            // as a non-staff scoreboard report - a "Player Report"
            // embed + the voice clip, for staff review - and it
            // NEVER bans, whoever holds it (staff have the ban gun
            // for that). 2 s local cooldown against spam.
            if (Time.time < nextReportAllowed) { lastAction = "report cooldown"; return; }
            nextReportAllowed = Time.time + 2f;
            string roomCode2 = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "none";
            string speakers2 = FalconVoiceLog.RecentSpeakers();
            FalconStaffWebhook.PostEmbed("Player Report", 15105570, "Reporter: " + me + " (" + PhotonNetwork.LocalPlayer.UserId + ")\nReported: " + them + " (" + target.UserId + ")\nRoom code: " + roomCode2 + "\nReason: Report (gun)\nVoice (last 25s): " + speakers2, null);
            FalconStaffWebhook.UploadReportClip(FalconVoiceTap.BuildClipWav(), "REPORT (gun) on " + them + " (" + target.UserId + ") in room " + roomCode2 + " by " + me + " - last 25s of lobby voice");
            FalconStaffWebhook.Log("REPORT GUN on " + them + " by " + me);
            lastAction = "reported " + them;
            return;
        }

        if (gun == "mute")
        {
            // Round 85 (Sans): the mute gun - literally the
            // scoreboard's mute, fired from the hand: same
            // PlayerPrefs key (their UserId), same rig.muted flag,
            // same Photon mute event, and the board's mute button
            // for them flips red right now. Your client only.
            int next = PlayerPrefs.GetInt(target.UserId, 0) != 0 ? 0 : 1;
            PlayerPrefs.SetInt(target.UserId, next);
            PlayerPrefs.Save();
            if (targetRig != null) targetRig.muted = next != 0;
            foreach (var line in FindObjectsOfType<GorillaPlayerScoreboardLine>())
            {
                if (line == null || line.linePlayer != target) continue;
                if (line.muteButton != null) { line.muteButton.isOn = next != 0; line.muteButton.UpdateColor(); }
            }
            GorillaPlayerScoreboardLine.MutePlayer(target.UserId, target.NickName, next);
            FalconStaffWebhook.Log("MUTE GUN " + (next != 0 ? "muted " : "unmuted ") + them + " by " + me + " (client-side)");
            lastAction = (next != 0 ? "muted " : "unmuted ") + them;
            return;
        }

        if (gun == "ban")
        {
            if (!FalconStaffRole.CanInstantBan) { lastAction = "denied (role)"; return; }
            // Round 62 (Sans): staff cannot ban staff - at all.
            if (FalconStaffRole.IsStaff(target))
            {
                lastAction = "STAFF - can't ban staff";
                FalconStaffWebhook.Log("BAN REFUSED (staff target) on " + them + " by " + me);
                return;
            }
            // Ban gun (round 43, Sans): point, shoot, banned - with
            // the reason picked on the Staff tab. Same landing as a
            // scoreboard report-ban (rounds 40/42): EvBan shows the
            // ban on their board and closes their game, the master's
            // delayed close is the backstop, and CloudScript AdminBan
            // on live revision 12 applies the real PlayFab ban.
            string offense = banReason;
            string roomCode = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "none";
            string speakers = FalconVoiceLog.RecentSpeakers();
            FalconStaffNet.SendTo(FalconStaffNet.EvBan, target, new object[] { offense });
            if (PhotonNetwork.IsMasterClient && FalconStaffNet.Instance != null)
                FalconStaffNet.Instance.StartCoroutine(FalconStaffNet.Instance.DelayedClose(target));
            PlayFab.PlayFabClientAPI.ExecuteCloudScript(new PlayFab.ClientModels.ExecuteCloudScriptRequest
            {
                FunctionName = "AdminBan",
                FunctionParameter = new { targetId = target.UserId, offense = offense, code = roomCode, speakers = speakers },
                GeneratePlayStreamEvent = false
            }, delegate (PlayFab.ClientModels.ExecuteCloudScriptResult r) { }, delegate (PlayFab.PlayFabError e) { });
            FalconStaffWebhook.PostEmbed("Ban Gun", 15548997, "By: " + me + " (" + PhotonNetwork.LocalPlayer.UserId + ")\nPlayer: " + them + " (" + target.UserId + ")\nOffense: " + offense + "\nRoom code: " + roomCode + "\nVoice (last 25s): " + speakers, null);
            FalconStaffWebhook.UploadReportClip(FalconVoiceTap.BuildClipWav(), "BAN GUN (" + offense + ") on " + them + " (" + target.UserId + ") in room " + roomCode + " - last 25s of lobby voice");
            lastAction = "ban sent (" + offense + ")";
            return;
        }

        if (gun == "kick")
        {
            if (!FalconStaffRole.IsStaffOrHigher) { lastAction = "denied (role)"; return; }
            // Round 26 (Sans): kicking staff asks first. The target's
            // role rides their Photon player properties (published by
            // FalconStaffRole at login). First shot on an admin or
            // headadmin only arms the confirm and says so in the
            // footer; a second shot at the same player within 5 s
            // actually kicks. Regular players kick instantly as before.
            string targetRole = TargetRole(target);
            bool confirmPending = pendingKickActor == target.ActorNumber && Time.time < pendingKickUntil;
            if ((targetRole == "admin" || targetRole == "headadmin") && !confirmPending)
            {
                pendingKickActor = target.ActorNumber;
                pendingKickUntil = Time.time + 5f;
                lastAction = "STAFF " + targetRole.ToUpperInvariant() + " - shoot again to kick";
                FalconStaffWebhook.Log("KICK CONFIRM ASKED " + them + " (" + targetRole + ") by " + me);
                return;
            }
            pendingKickActor = -1;
            // Round 43: NO CloseConnection here anymore. It tore the
            // victim off the Photon server entirely, and racing the
            // EvKick LeaveRoom it wedged their connection state
            // machine - build #6: kicked players could not rejoin
            // lobbies. The event alone kicks (their client leaves the
            // room and stays connected, free to join another lobby).
            FalconStaffNet.SendTo(FalconStaffNet.EvKick, target, new object[0]);
            FalconStaffWebhook.Log("KICK " + them + " by " + me);
            lastAction = "kick sent";
            return;
        }

        if (!FalconStaffRole.IsHeadAdmin) { lastAction = "denied (role)"; return; }

        switch (gun)
        {
            case "fling":
                Vector3 dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) + 0.5f;
                FalconStaffNet.SendTo(FalconStaffNet.EvFling, target, new object[] { dir.normalized * FalconStaffSettings.FlingForce });
                break;
            case "fakelag":
                FalconStaffNet.SendTo(FalconStaffNet.EvFakeLag, target, new object[0]);
                break;
            case "creepify":
                FalconStaffNet.SendTo(FalconStaffNet.EvCreepify, target, new object[0]);
                break;
            case "slip":
                FalconStaffNet.SendTo(FalconStaffNet.EvSlip, target, new object[0]);
                break;
            case "spin":
                FalconStaffNet.SendTo(FalconStaffNet.EvSpin, target, new object[] { FalconStaffSettings.SpinDegPerSec });
                break;
            case "loudmic":
                FalconStaffNet.SendToAll(FalconStaffNet.EvLoudMic, new object[] { target.ActorNumber });
                break;
            case "outfitrandom":
                FalconStaffNet.SendTo(FalconStaffNet.EvOutfitRandom, target, new object[0]);
                break;
            case "outfitall":
                FalconStaffNet.SendTo(FalconStaffNet.EvOutfitAll, target, new object[0]);
                break;
            case "quit":
                FalconStaffNet.SendTo(FalconStaffNet.EvQuit, target, new object[0]);
                break;
            case "tiny":
                FalconStaffNet.SendToAll(FalconStaffNet.EvTiny, new object[] { target.ActorNumber, FalconStaffSettings.TinyScale });
                break;
            case "grow":
                FalconStaffNet.SendToAll(FalconStaffNet.EvGrow, new object[] { target.ActorNumber, FalconStaffSettings.GrowScale });
                break;
        }
        FalconStaffWebhook.Log("TROLL " + gun + " on " + them + " by " + me);
        lastAction = gun + " sent";
    }

    private void SnowballSpam()
    {
        if (!FalconStaffRole.IsHeadAdmin) return;
        if (Time.time < nextSnowball) return;
        nextSnowball = Time.time + FalconStaffSettings.SnowballMs / 1000f;
        Transform hand = Player.Instance != null ? Player.Instance.rightHandTransform : null;
        if (hand == null) return;
        // Round 18: real snowballs via the game's own launch path.
        // Round 30: burst - N snowballs per fire (Settings tab, 1..100),
        // slight spread so they don't stack into one.
        int burst = Mathf.Clamp(FalconStaffSettings.SnowballBurst, 1, 50);
        bool anyOk = false;
        for (int i = 0; i < burst; i++)
        {
            Vector3 dir = hand.forward;
            if (burst > 1)
                dir = (dir + hand.right * UnityEngine.Random.Range(-0.03f, 0.03f) + hand.up * UnityEngine.Random.Range(-0.03f, 0.03f)).normalized;
            if (FalconSnowball.Launch(hand.position + hand.forward * 0.25f, dir * 16f)) anyOk = true;
        }
        if (!anyOk) lastAction = "snowballs unavailable here";
    }
}
