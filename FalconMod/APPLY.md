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
