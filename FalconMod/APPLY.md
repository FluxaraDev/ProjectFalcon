# FalconMod — port kit

Everything that turns a clean Gorilla Tag (Spring 2023, Unity
2021.3.33f1) export into Project Falcon, in one folder. Built so a
project/update switch is a copy-paste, not a re-derivation.

## Layout

- `Assets/FalconMod/FalconStaff/` — the staff system (roles, hand
  menu, guns, rave, mod call buttons, name tags, voice clips,
  room orders, fly, size guns). Self-contained: it bootstraps
  itself from code at scene load. **No scene edits anywhere.**
- `FalconMod/GamePatches/` — edited game files, stored at their
  real project-relative paths. Copy each one over the same path
  in the new project.
- `FalconMod/CloudScript/main_rev13.js` — the PlayFab CloudScript
  (ban ladder). Publish as `main.js`.

## Port steps

1. Copy `Assets/FalconMod/FalconStaff/` into the new project's
   `Assets/FalconMod/FalconStaff/`. Unity generates .meta files.
2. Copy every file under `FalconMod/GamePatches/` over the same
   relative path in the new project (see the list below).
3. Assets that must exist (already in this repo, copy them too on
   a truly fresh project):
   - `Assets/vr_spalsh.png` — VR splash (ProjectSettings points at
     its GUID; keep the .meta so the GUID survives).
   - `Assets/Resources/rave_song.mp3` — the rave song
     (FalconRave loads `Resources/rave_song`).
   - `Assets/FalconMod/FalconStaff/Resources/FalconLightmapTintMat.mat`
     rides along with the staff folder (name-loaded).
4. Backend IDs — the patch copies carry Falcon's, swap them if the
   new project is a different title:
   - PlayFab TitleId `114812`: in PlayFabAuthenticator.cs.
   - Photon Realtime `9c5612c9-328a-48a2-92e0-ed33cdbfeb88` and
     Voice `960e4c20-8d21-4c30-b0b3-e1c598d9bdc0`: in
     PhotonServerSettings.asset (and the AppId fields in
     PlayFabAuthenticator.cs).
5. CloudScript: publish `main_rev13.js` via the Admin API
   (`UpdateCloudScript`, Publish=true) or Game Manager.
6. Title Data keys the game reads at runtime:
   - `falcon_clip_hook` — staff Discord webhook URL. NEVER in the
     repo; set it in Game Manager / pf.py on the new title.
   - `MOTD_<bundleVersion>` and `MOTD` — the MOTD board text
     (bundleVersion is currently 1.1.43; the key suffix must match
     ProjectSettings' bundleVersion).
   - `falcon_room_orders` — written by staff tooling only; the
     game just polls it.
   - `falcon_transfer` — `{"code": "ROOMCODE", "ts": <unix>}`;
     a fresh order moves every logged-in client into that room
     (the meeting transfer). Written by staff tooling only.
7. Roles: `pf.py set-role <PlayFabId> <role>` with role in
   staff / admin / headadmin / admin2 / headadmin2. Tier-2 roles
   (the 2s) get menus, kick and reports but NO instant ban, and
   tier-2 staff cannot kick each other.

## What each game patch does

- `PlayFabAuthenticator.cs` — backend swap (title + Photon
  AppIds), FalconStaffRole.FetchRole() hook at login, ban strings
  rebranded to PROJECT FALCON.
- `PhotonNetworkController.cs` — default-name join gate +
  rave join lock.
- `GorillaComputer.cs` — CREDITS page = RAVE, VISUALS page =
  ITEMS (long arms / speed boost / name tags), transient failure
  text, bad-name Azure calls guarded.
- `GorillaPlayerScoreboardLine.cs` — report flow: staff instant
  ban (EvBan + CloudScript AdminBan + voice clip), staff-lock and
  tier-2 rules, report-spam lockout.
- `GorillaText.cs` — stock bug fix (DisableFailedState).
- `Player.cs` — FalconArmScale hand-position scaling (long arms).
- `GorillaNetworkDisconnectTrigger.cs` — teleports to spawn
  instead of disconnecting.
- `OculusProjectConfig.asset` — Meta system splash = the Falcon
  logo.
- `PhotonServerSettings.asset` — Falcon Photon AppIds.
- `ProjectSettings.asset` — bundleVersion 1.1.43, VR splash
  GUID, company/product names still stock (Another Axiom /
  Gorilla Tag) — change on a rebrand.

## Falcon event codes (Photon RaiseEvent)

100 kick · 101 fling · 102 fake lag · 103 creepify · 104 slip ·
105 spin · 106 loud mic · 107 outfit random · 108 outfit all ·
109 spooky music · 110/111 rave start/end · 112 ban · 113 quit ·
114 close lobby · 115 tiny · 116 grow. Never use 200+ (PUN
reserves them — that was the round-22 room-kick bug).

## Loading scene + entry anti-cheat (round 50)
- `Assets/FalconMod/Loader/FalconLoading.unity` is build scene 0 (EditorBuildSettings), GorillaTagSJR is scene 1. The loader parks the OVRCameraRig where the metro map's editor spotlight pointed, builds the backdrop from Resources/metro_*.txt (base64 chunks of an FMM1 blob made by extract_metro.py from Sans's glTF), plays Resources/falcon_load_music (binary - web-upload it), and runs FalconAntiCheat ONCE: official package id, Quest identity words (oculus/quest/meta/eureka/hollywood/panther in OS+device strings), PC denied, root, Frida (file/port/maps), external .so from /data/local/tmp, MelonLoader, FusionCore/BepInEx. Flags are shown in-world and appended to Android/data/<package>/anticheat_log. Clean -> async load with percent bar + name tip, "ENJOY PROJECT FALCON", fade to black + music fade, then activation. Main scenes carry NO checks (open SJR directly for editor testing).
- Bloom: Packages/manifest.json gains com.unity.postprocessing 3.2.2; FalconLoader wires it by reflection (compiles/runs without the package too).
- The FalconStaff bootstrap root is deactivated during loading and reactivated at handover so staff objects never appear in the backdrop.

## Round 52 (2026-11-15) - loader rework + perf
- FalconLoader: rig placed at the spotlight position but LEVEL (yaw + composed pitch only - never roll an XR rig); editor lights rebuilt (dark ambient, the glTF spotlight with shadows, violet directional); title "PROJECT FALCON" / subtitle "ANTICHEAT STATION" in the game's Utopium font (scene-wired `gameFont` field on FalconLoader - rewire it if the scene is rebuilt: Utopium.asset guid e3dbf7c7582cd0c44815e6dbb65b1675, fileID 12800000).
- FalconAntiCheat: staged API (Stages()) - VERIFYING BUILD / CHECKING DEVICE / ROOT CHECK / FRIDA SCAN / MOD LOADER SCAN / SELF TEST; clean passes are logged too; self-test hook = a file named `anticheat_selftest` in the game's files folder forces a deny.
- Perf: FalconNameTags no longer scene-scans every 0.5 s (cached rig list, 2.5 s refresh, zero steady-state allocation); FalconWallSign gives up after 12 build tries instead of scanning forever.

## Round 60 addendum (2026-11-15) - map conversion fix + loader framing
- extract_metro.py bug (builds #8-#10): the glTF->Unity conversion applied S*M*S to the transform matrix but fed it RAW points - the points never got the mirror, so chunks whose rotations mix axes landed scrambled (in-game: "upside down + inverted"). Fix: transform in glTF space, mirror Z on the OUTPUT positions/normals. The metro_*.txt chunks in this kit are the FIXED regeneration (verified bounds y -10.4..29.3, downtown centroid (-2.7, -173.3)). If the map is ever re-exported, re-run the fixed extractor; never resurrect the old chunks.
- Loader framing no longer uses the glTF light/camera nodes (the authored spotlight aims UP in Unity space). FalconLoader composes the shot: CamPos (-82.7, 48, -243.3), CamForward (0.733, -0.229, 0.641) - an aerial over the downtown skyscrapers; the spotlight (his color 0.255/0.047/0.365, intensity 3) hangs at (-2.7, 35, -173.3) and pools down on the city center.
- Loader sky: procedural skybox graded to the game's time-of-day 5.5 dusk (SkyTint 0.35/0.28/0.45, GroundColor 0.28/0.18/0.13, Exposure 0.8, AtmosphereThickness 1.5), skybox ambient, fog off - loader scene only.

## Rounds 65-71 addendum (2026-11-18) - loader/anticheat/staff lessons for the next port
- Loader framing (supersedes the round-60 addendum): the rig is NEVER moved. PlaceMap yaws the MAP to the player - the authored camera point CamPos (-3.13, 23.30, -157.71) lands on the player's eye, yaw-aligned to his facing; CamForward (-0.354, -0.351, -0.867). Sky: skybox REMOVED, cameras clear SolidColor near-black (0.015, 0.008, 0.03); the ONLY downtown light is the GLB's authored point light (PointLight at (-4.71, 18.62, -161.55), color 0.255/0.047/0.365, intensity 6, range 260, soft shadows) + the scene's FalconSun directional restyled (0.718, 0, 1) at 0.8. NO fake glow/shade sprites - Sans banned them (build #13). Real exponential fog 0.0045 in (0.06, 0.03, 0.08). Metro materials matte: _Metallic 0, _Glossiness 0.18; the flat ground/water sheet (bounds >150m x/z, <2m y) gets color x0.3 (dark teal, not neon blue).
- Loader UI recipe (the one that WORKS - build #14, after builds #10-#13 showed nothing): canvas directly usable with NO CanvasScaler (WorldSpace scaler recomputes the scale), panel = a DEDICATED Image object (never a Text with an Image bolted on + Destroy), font = the serialized Utopium (gameFont field, scene-wired) with builtin Arial as fallback. ALL display strings ALL CAPS (Utopium is an all-caps font - same one as every text board in the game). Panel placement (round 71): ONE-TIME world placement straight ahead of the player's facing at load (yaw only, eye + fwd*4.5, -0.15 down) - NOT head-locked. Fade: separate GIANT panel (1200x1200 at 0.005 = 6m) parented to CenterEyeAnchor - the fade stays screen-locked on purpose. All loader UI on layer 0.
- Bloom (round 71 root cause): PostProcessProfile has NO AddSettings(ScriptableObject) overload - reflecting it returns null and the profile stays EMPTY (bloom "armed", nothing renders, builds #8-#14). Use AddSettings(Type) and configure the RETURNED settings (params are parameter objects: set .value + .overrideState). Force cam.allowHDR = true on the rig cameras. bloomDiag in anticheat_log reports the profile's settings count - 1 means Bloom is really in.
- Anticheat KnownLibs MUST include every native lib the official APK ships, or the LIBRARY AUDIT false-denies the official build (builds #12-#13): libovrplugin.so, libopenxr_loader.so, libsdktelemetry.so enter at build time from the Oculus/OpenXR plugins and are NOT in the export tree - re-inventory the built APK's lib/arm64-v8a on any new GT base and add what's legitimately there. Cert pin: read the fingerprint from the keystore FILE with keytool, never transcribe it by eye (the round-63 pin lost "E0" at position 38 and false-denied for two builds).
- Staff net: SendToAll uses RaiseEventOptions.Default = ReceiverGroup.OTHERS - the sender never receives its own broadcast. Any "everyone" effect must ALSO apply locally on the sender (round 71 CloseLobby fix: closer exits too). Size guns (tiny/grow): the assert ticks from Update AND LateUpdate (VRRig reads scaleFactor in its LateUpdate); every client posts a "SIZE RX" receipt to the staff log per shot.

## Round 72 addendum (2026-11-18) — RTX, ITEMS option 4

Sans asked for a mod named RTX in the ITEMS tab, after QIX | Quantum's "HOW TO ADD RTX TO YOUR GORILLA TAG COPY!" video. The video's method is a permanent editor-side URP pipeline conversion (materials to Standard, URP asset in Graphics + Quality, convert project materials, global Volume with Bloom + ACES + contrast 20, post on at the camera) — that cannot be a runtime toggle and would destroy this port's custom day/night lightmap shaders, so Falcon's RTX reproduces Sans's own fuller spec at runtime instead. His spec: realtime lighting instead of baked; default Unity sky; slightly less bloom than the loading screen; exposure slightly darkened; saturation slightly up; slight film grain; high-quality shadows over every object incl. arms; weather fog per map (rain = greenish-blueish-grayish, snow = white, night = slightly black + everything glows slightly, dawn/sunrise/sunset = slight orange, less heavy than night); the campfire glows orange, makes real light and casts a shadow; every light has a shadow; off reverts everything. DEFAULT OFF, and the ITEMS page warns it is experimental.

Implementation (`FalconStaff/FalconRtx.cs`, spawned by FalconStaffBootstrap, driven by `FalconPlayerOptions.Rtx` + `ToggleRtx()`, GorillaComputer ITEMS case "4" + screen line): on toggle-on it snapshots then (1) swaps BetterDayNightManager's own dayNightSupportedMaterials (+Cutout) onto the manager's own Standard shader references — the only way dynamic lights land, since the baked shader path ignores them; (2) replaces the sky with a runtime Procedural skybox material + Skybox ambient and disables renderers whose shader name contains DayNightLerpSky; (3) forces QualitySettings shadows All/VeryHigh/150 and sets every scene Light to Realtime bake + Soft shadows; rigs cast + receive; (4) adds an orange shadow-casting PointLight (2.4, range 15) at every Campfire's topFire; (5) arms a PPv2 profile via the loader's reflection route: Bloom 2.2/0.85/0.5, ColorGrading ACES + postExposure -0.2 + contrast 20 + saturation 15, FilmGrain Thin1 0.3/0.8; (6) a 0.5 s fog driver reads BetterDayNightManager.instance (weatherCycle[currentWeatherIndex], currentTimeIndex, currentTimeOfDay) — rain/snow beat time-of-day, rave owns fog while raving; night also raises bloom to 2.8. NOTE: this build's WeatherType enum is only {None, Raining, All} — there is no snow weather in Spring 2023; the snow branch keys off the weather name if one ever appears. Revert restores every snapshot (shaders, skybox, ambient mode, fog set, quality trio, light modes, camera flags/HDR, sky renderers, rig shadow modes) and destroys the volume/layers/fire lights. NOT device-verified yet.

Round 73 correction (same day): Sans — "Mountains snows randomly." Right: the global cycle's Raining state falls as SNOW in the mountains; there is no separate snow state to read. FalconRtx now uses the game's own `CurrentWeather()` and picks the white snow fog when it is Raining AND the player's head is in the mountains quadrant (x > -40, z in (-140,-80), y in (5,55) — anchored on the mountains mod-call spot; approximate, fog-tint only). The round-72 note above stands corrected on this point.

## Round 74 addendum (2026-11-18) — build #15 fixes

Three fixes from Sans's build #15 report. (1) CLOSE LOBBY LATCHED THE COMPUTER: both close-lobby paths (FalconStaffNet.CloseLobby local exit + the EvCloseAll receiver case) called GorillaComputer.UpdateFailureText WITHOUT FalconFailure.Mark() — the only two Falcon failure-text sites missing it — so FalconComputerFix never cleared the state and the computer stayed on the failure screen forever (public lobbies only). Both now Mark() first, like every gate. (2) COMPUTER SCREEN DEFAULT COLOR: Sans — instead of default green, default purple. Assets/Material/screen.mat _EmissionColor (0, 0.2358, 0.0158) green -> (0.42, 0.04, 0.72) purple; screenRed.mat (failure) untouched. Mirrored at GamePatches/Assets/Material/screen.mat. (3) LOADER TEXT SIZE: the panel sits 4.5 m away since round 71 but kept close-up font sizes — Title 44->52, Sub 26->32, Status 40->48, Stages 17->30, Tip 18->24, Flags 18->26.

BLOOM ROOT CAUSE (found in ProjectSettings, same evening): m_StereoRenderingPath: 2 = Single Pass Instanced, and Unity's Post Processing v2 does not support SPI — the post chain silently never composites in the headset, which fits "no bloom in any build" across every profile/volume/HDR fix. This also means RTX's PPv2 grade (round 72) cannot show on Quest as shipped; RTX's lighting/sky/fog/shadow half is plain rendering and unaffected. Flipping the project to SinglePass/MultiPass would enable post at a real FPS cost game-wide — Sans's call, pending.

## Round 75 addendum (2026-11-18) — Single Pass flip (Sans's decision)

Sans chose bloom over the instancing speed: the project flips from Single Pass Instanced to Single Pass so Post Processing v2 can composite. TWO knobs, both set to 1 (SinglePass): ProjectSettings/ProjectSettings.asset `m_StereoRenderingPath: 2 -> 1` and Assets/XR/Settings/OculusSettings.asset `m_StereoRenderingModeAndroid: 2 -> 1` (the Oculus plugin's own mode was Multiview; flipping only PlayerSettings would have left the Quest build instanced). The setting is PROJECT-WIDE — the whole game renders Single Pass; the post effects themselves only exist in the loader and RTX. If the FPS cost hurts on device, reverting is these same two values back to 2. OculusSettings.asset is newly mirrored at GamePatches/Assets/XR/Settings/.

## Round 78 addendum (build #18 verdicts)
- SELF-QUIT IS REAL (Sans's decision, reversing the round-12 dry rule for quit only): FireSelf's quit branch calls FalconStaffNet.SelfQuit() -> the exact OnQuit victim path (board text, LeaveRoom, QuitSoon hard-kill). Kick self-shots stay dry (kick already proven on other players); ban self-shots stay dry (a real self-ban would strike your own account via CloudScript).
- MOD CALL BUTTONS VANISHING - ROOT CAUSE: the buttons built once in Start, which since the loader arrived (round 50) runs in the LOADING scene; LoadSceneAsync(Single) destroys all non-persistent objects on the transition, plates included, and the surviving component never rebuilt them. Fix: BuildButtons() extracted, roots DontDestroyOnLoad, Update rebuilds the set the moment it finds it destroyed.
- LOADER TEXT: the Utopium font renders well under its nominal fontSize on this canvas - round 74's bump (17->30 stages) was real but invisible in practice. Round 78 roughly doubles again: Title 104, Sub 64, Status 96, Stages 48, Tip 48, Flags 52. If it STILL reads small, the fallback is board-style TextMesh text, not bigger numbers.
- RTX EXTENSIONS: (1) scene-wide material swap - any scene material whose shader is one of the day/night family's lit shaders (collected from the manager's own arrays) gets the same Standard/StandardCutout swap, snapshotted identically (build #18: Forest walls were on the list; ground/stump/props/targets were not). (2) FalconRtxSun - RTX spawns its own shadow-casting directional (BetterDayNightManager owns NO Light - verified; the scene's lights are points, so nothing could cast the gorilla's shadow). Destroyed on revert. (3) The procedural sky is driven by the same phase key as the fog (night near-black, dawn/dusk orange, rain/snow gray, day default). (4) Mountains ice: name-matched materials get _Glossiness 0.92 / _Metallic 0.05 while RTX is on, snapshotted and restored.
- BLOOM: unchanged this round on purpose - the loader's anticheat_log DIAG line (resources found/NULL, settings count, cameras armed) names the dead link; no more blind bloom edits until it is read.

## Round 79 addendum
- RTX SUN DISC: the procedural skybox has no sun object - its sun disc is shader-drawn from RenderSettings.sun. FalconRtx now registers its FalconRtxSun directional as RenderSettings.sun while RTX is on (snapshot + restore on revert), so the sky shows a sun in the same direction that casts the shadows.

## Round 80 addendum
- RTX SUN INTERACTION: the FalconRtxSun now rides the day/night phase clock (currentTimeIndex + currentLerp): dawn low in the east, noon high, sunset low in the west, below the horizon at night; color/intensity follow the phase key (night dim blue 0.2, dawn/dusk warm orange 0.9, rain/snow overcast 0.7, day 1.15). At night the sky material's _SunDisk is set to 0 (no sun disc in a night sky) and restored otherwise. Fog note: fog is a per-shader feature - the stock day/night shaders do not implement it, so weather fog only becomes visible scene-wide once materials are on Standard (the round-78 extension); in build #18 most materials were still custom, which is why weather showed no fog across maps.

## Round 81 addendum - Apply FalconMod (editor bake tool, for Sans's Christmas laptop)
- NEW: Assets/FalconMod/Editor/FalconBakeWindow.cs - a top-bar menu "Apply FalconMod" opening a window with "Load Objects" and "Remove Baked Objects". Load Objects bakes Falcon's scene furniture into the open scene at its real positions under its real names: the 7 mod call buttons (cube + MOD CALL label + popup), the wall sign (backing + TMP text, font borrowed from a scene TMP object), the board text rewrites (GORILLA CODE OF CONDUCT -> FALCON CODE OF CONDUCT, CoC body, MOTD) and the staff board clone (FalconStaffBoardFrame + FalconStaffText at Sans's measured corners), plus a "FalconBaked" marker object. Undo works; the tool does NOT save the scene (Ctrl+S is Sans's). Remove Baked Objects deletes the furniture again (texts stay - identical to runtime wording).
- ADOPT-DON'T-DUPLICATE (the "stop the loading scripts" half): each runtime spawner checks for its baked object first - FalconModCallButtons.AdoptBaked (scene-owned roots with matching names replace the runtime set, labels rebound), FalconWallSign.TryBuild (finds "FalconWallSign"), FalconBoardTexts.ApplyOnce (finds "FalconStaffText"). A baked scene therefore runs with the baked objects live and no doubles, and an unbaked scene spawns exactly as before - UBA builds are unaffected either way. Player-attached systems (hand menu, tags, fly, rave, RTX) have no scene furniture and are not baked.
- The editor script MIRRORS constants from the runtime files (button spots, sign position/text, board strings, staff corners) - it references no runtime types, so it cannot break the player build's compile; UBA still compiles editor scripts, so keep it API-conservative. Keep the mirrors in sync when the runtime values change.

## Round 82 addendum (build #19 verdicts)
- LOADER TEXT = TEXTMESH: three fontSize doublings (44/52/104) never changed the rendered size - the UI font's glyphs sit small in their em. The loader readout is now TextMesh (the mod-call mechanism): characterSize in meters, children of the unscaled holder, per-frame SyncTm mirrors the hidden UI texts (percent, stages, flags, tip, ENJOY). UI texts remain as fallback if no font resolves. Diag line: "TextMesh readout live".
- FOG ROOT CAUSE (strongest lead): BOTH shipped scenes had RenderSettings fog OFF (m_Fog: 0), so Unity's automatic fog-variant stripping likely removed the fog shader variants from the build - runtime RenderSettings.fog then does nothing on any material, in the loader AND in RTX, on every build so far. Fix: FalconLoading.unity now ships m_Fog: 1, m_FogMode: 2 (Exponential - the mode Falcon code uses). If fog STILL does not render in build #20, the cause is elsewhere and the loader's own fog (0.0045 over the metro) is the cheapest test scene.
- RTX: sun disc is now EXPLICIT (_SunDisk 2 + _SunSize 0.08 in day/orange/rain/snow phases; the savedSunDisk snapshot read the shader default, which behaved as 0 = no disc in any phase). Campfire light 2.4 -> 4.5 intensity, 15 -> 18 range (read as faint in #19 once the sun existed). Sun shadowDistance 150 -> 55 (at 150 m a gorilla's shadow was a few texels - Sans's "two dots") + shadowNormalBias 0.03. Glass-named materials get the ice reflective treatment (diag "glass N"). The UNLIT family joins the swap (gorillaUnlit -> standard, gorillaUnlitCutout -> standardCutout, diag "unlit N") - the stump/ground candidate the lit-family extension missed.
- LOBBY ENV CHANGER (new, FalconEnv.cs + EvEnv 117): Settings tab (headadmin) gains "Lobby weather" (AUTO/CLEAR/RAIN) and "Lobby time" (AUTO + the 10 phases by their lightmap names) steppers. Broadcast to the room; every client applies locally; leaving the room clears it. Mechanism: the day/night manager's own SetOverrideIndex repaints the phase but the UTC clock reclaims the maps at its next phase change, so a 1 s driver re-asserts; weather = pointing currentWeatherIndex at a weatherCycle slot of the wanted type. On clear, one SetOverrideIndex(clock index) repaints the clock's pair immediately. Works with RTX off; RTX's fog/sky/sun driver reads the manager, so it follows overrides too.
- BLOOM SOLVED ON PAPER (2026-11-19, from the PPv2 3.4.0 package source - the version UBA resolves): PostProcessResources is NOT loadable by name. The package deliberately keeps the asset out of any Resources folder (its own class doc says so), so FalconLoader/FalconRtx's Resources.Load("PostProcessResources") returned NULL in every build; Init(resources) was therefore never called; PostProcessLayer.OnEnable calls Init(null), which leaves m_Resources null; at render time the effect renderers get null resources and silently draw nothing - "armed" diag, zero bloom, zero grade, no error. FIX SHIPPED HERE: the package's own PostProcessResources.asset now lives in FalconMod/Loader/Resources/ (new GUID fe3c56fe62434dd2a5b66d386b32213a in its .meta - never reuse the package GUID d82512f9...), so Resources.Load finds it and its package-stable shader/texture GUIDs resolve at build time. One fix covers loader bloom AND the RTX grade. First build containing it is the true bloom test.

## Round 83 addendum (same push as round 82)
- SETTINGS PERSISTENCE (Sans): FalconStaffSettings values (snowball interval + burst, spin, fling, fly speed, tiny/grow scales) now live in PlayerPrefs (falcon_set_* keys), flushed via PlayerPrefs.Save() on EVERY change (the quit gun's killProcess hard-kill eats unflushed writes) and restored by FalconStaffSettings.Load() at bootstrap, clamped to the stepper ranges. The staff lock already persisted the same way (falcon_lock + Save + republish at login) - verified, unchanged. NOT persisted, per Sans: the lobby env overrides (per-lobby by design) and the ITEMS mods (per-session toggles).
- BAN GUN: verified it already calls CloudScript AdminBan (target's PlayFabId + offense + room + speakers) alongside EvBan - in-game close AND server ban both, which is what makes it work on Photon bots (their client can ignore the event; the account ban lands at login regardless). No change needed. Staff lock chain re-verified intact the same day (menu refusal, victim-side drop + LOCK BLOCKED log, scoreboard refusal, server-side staff-ban block in rev 15).

## Round 84 addendum (2026-11-19, after the rounds 82+83 push)
- VERSION GATE MADE REAL (Sans caught it: build #19 still joins on live1123). Diagnosis from the code: PlayFabAuthenticator.checkforupdate() is an EMPTY STUB in this export, and GorillaComputer.OnReturnCurrentVersion only honors Fail:true / ResultCode!=0 - it never compares the returned "Version" string, and rev 15's handler ignores args anyway. The CurrentVersion the client sends (PhotonNetworkController._gameVersionString from serialized fields) is identical in every build, so NO server-only gate is possible. Fix (client): PlayFabAuthenticator gains a VersionString getter over the existing version field; OnReturnCurrentVersion compares the returned Version against it - mismatch (incl. "BANNED") -> GeneralFailureMessage(versionMismatch), the update screen. From the first build carrying round 84 onward, version cuts work exactly as Sans intended (bump authenticator + publish a rev with the new string). Builds BEFORE round 84 (#19, #20) can never be version-gated - nothing they send differs; per-account bans are their only server-side kill.

## Rev 16 note (2026-11-19, server-side, no build)
- CloudScript rev 16 = rev 15 + a Title Data kill switch in ReturnCurrentVersionNew: falcon_gamedown="1" returns Fail:true, the one response pre-gate clients DO honor (update screen). All-or-nothing by necessity - old builds send nothing a revision could select on. Fixture-proven both ways 2026-11-19 (normal: live1123/no Fail; down: Fail true; restored: normal). Switch left OFF ("0"). Kit copy: FalconMod/CloudScript/main_rev16.js (rides the next push).

## Round 85 addendum (2026-11-19, build #20 verdicts + the player menu)
- LOADER GRAY BUG (Sans: text gray head-on, metro fog patchy gray): the loader scene's fog was NEUTRAL GRAY (0.5,0.5,0.5) at density 0.01 exponential - over a ~150-unit map everything distant drowns in gray. FalconLoading.unity now: fog color deep purple (0.08,0.03,0.14), density 0.0035. Same fix family as the in-game washes.
- RAIN FOG (build #20: white/blue in city, bloom drowned): RTX rain tint darker/grayer (0.30,0.36,0.40), density 0.012 -> 0.0095.
- PLAYER SHADOWS IN LOBBIES (build #20: shadows only on the offline rig): the rig shadow pass ran once at Apply, when only the offline rig exists; lobby rigs spawn later and never got it. FalconRtx now rescans rigs every 2.5 s (known-set guard) and takes late rigs through the identical snapshot+set pass.
- SWAP REACHED FOREST ONLY (build #20): the material census also ran once, at toggle time, against the then-loaded scene. On any scene load while RTX is on, FalconRtx now rebuilds (Revert+Apply, 1 s debounce) against the loaded world - city/canyons/mountains materials included. Mountains fog follows the same path (fog state is re-derived per phase tick).
- RTX vs ENV FIGHT (build #20: override snapped back constantly): single driver. While RTX is on, FalconEnv no longer calls SetOverrideIndex (the lightmap repaint that fought the clock); it writes FalconRtx.PhaseOverride instead, and RTX's sun/fog/sky read the override index. Weather-slot forcing still applies (never repaints). Clearing the override clears PhaseOverride too.
- RAVE FOG (build #20: "fog rave sucked, old rave was better"): the rave forced its own exponential fog; now that fog actually renders, it washed the disco out. FalconRave now turns fog OFF for the rave's duration (snapshot/restore unchanged).
- SUN DISC (build #20: still none, after two rounds of _SunDisk/_SunSize on the procedural sky): the disc is now a REAL OBJECT - a 42-unit sphere (58 at dawn/dusk) on the sun light's axis, 700 m from the head, positioned every frame from the same arc math as the shadow-casting light; hidden at night and in rain/snow. The game's own sky draws its sun/moon as world objects the same way.
- CAMPFIRE (Sans: bigger glow + flicker): fire lights range 18 -> 26, and Update flickers each fire's intensity with per-fire Perlin noise (0.85x..1.15x of 4.5) so they don't pulse in sync. New-map fires join via the same 2.5 s rescan.
- BOARDS GREEN OUTSIDE CANYONS/CAVES (Sans: those two share a board model; the other maps' model differs): no second screen .mat exists in the project, so the other model's material is embedded/instanced and the file edit could never reach it. FalconBoardTexts now re-skins at runtime: every renderer material named "screen" (not screenRed) gets the Falcon purple emission (0.42,0.04,0.72), at startup passes and on every scene load.
- ICE/GLASS (build #20: no visible change): smoothness had nothing local to reflect. RTX now spawns a realtime ReflectionProbe (128, ViaScripting, rendered per apply/map cycle) riding the player's head.
- ITEMS 5 = PHASESYNC (Sans): Move tab toggle + FalconPlayerOptions.PhaseSync0. ON = PhaseSync 0, OFF = 4. The export's managed OVR layer has NO PhaseSync API (OVRPlugin.cs/OVRManager.cs searched, zero hits), so FalconPhaseSync works in the open: reflection sweep of the Oculus assemblies for any PhaseSync member the built DLLs carry (calls a static setter if found), then guarded P/Invoke attempts on the native OVRPlugin lib. Every attempt + outcome lands in the session log ("PHASESYNC ...") - one device run settles the route. No promise beyond that.
- PLAYER MENU (Sans's spec, PLAYER-MENU-SPEC.md): the hand menu opens for EVERYONE and renames itself - "FALCON PLAYER MENU" for players, "FALCON STAFF MENU" for staff. Tabs are built per role: players get Guns / Move / Mods; staff add Staff; headadmin adds Troll / Sound / Settings; admins keep ban/kick guns but never troll. Player guns: REPORT gun (Player Report embed + voice clip, NEVER bans, 2 s cooldown) and MUTE gun (the scoreboard's own mute from the hand: same PlayerPrefs UserId key, rig.muted, MutePlayer event, and the board's mute button flips red immediately; client-side only). Move tab: long-arms slider x0.99..x1.40 (max = today's long arms; min = 1% shorter than stock) + speed slider x1.05..x1.82 (max = 1.3x past today's 1.4 max; min = just under stock 1.1), RTX + PhaseSync toggles, staff extras (fly, coord cube) below for staff. MOD CHECKER tab: everyone in the room by name with their published mods + realtime FPS; each client publishes its own state as the "falcon_mods" Photon player property once a second (FalconOptionsDriver); STAFF PUBLISH "discreet" - their mods never display. Scroll: point the laser to focus, left thumbstick scrolls the focused scroll tab (Settings + Mods share the machinery).
- Files: FalconMod/FalconStaff/{FalconStaffHandMenu, FalconPlayerOptions, FalconPhaseSync (NEW), FalconRtx, FalconEnv, FalconRave, FalconBoardTexts}.cs + FalconMod/Loader/FalconLoading.unity. FalconPhaseSync.cs ships without a .meta like the other FalconStaff scripts.
