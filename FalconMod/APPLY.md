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
