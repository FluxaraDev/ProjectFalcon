# Falcon Staff — setup & status

Staff system for Project Falcon, built against the Spring 2023 source tree
(the fangame base). Spec: `../FALCON-STAFF-MENU-SPEC.md`.

## Files

- `FalconStaffRole.cs` — PlayFab role read at login (`falcon_role` in
  Read-Only User Data), gates everything. Call `FalconStaffRole.FetchRole()`
  once right after the successful LoginWithCustomID callback in the
  authenticator, before any report/menu UI can open.
- `FalconStaffMenu.cs` — IMGUI staff menu (left menu button). Tabs: Staff
  (kick gun), Troll (headadmin, event-gated guns + spooky music), Sound
  (soundboard), Move (long arms / jump sliders above the scene defaults).
- `FalconStaffNet.cs` — Photon room events 200-209 carrying every effect,
  targeted at the victim's client or broadcast (loud mic, spooky music).
- `FalconStaffOutfits.cs` — screw-outfit and load-up-cosmetics guns, built
  on the rig's try-on set, so unowned cosmetics are session-only.
- `FalconSoundboard.cs` — loads MP3/WAV/OGG from
  `Android/data/com.fluxtech.ProjectFalcon/files/Soundboard` (fallback:
  persistentDataPath/Soundboard off-Android).
- `FalconStaffWebhook.cs` — posts every ban and kick (and troll use/denials)
  to the staff Discord webhook. Paste the URL into `WebhookUrl`.
- `SteamLongArm.cs` — Sans's scale-method long arms, verbatim.

## Unity hookup (the only manual steps)

1. Create an empty GameObject named `FalconStaff` in the main scene.
2. Add `FalconStaffMenu`. It auto-adds `FalconStaffNet` and
   `FalconSoundboard` at runtime.
3. Snowball gun: assign a snowball prefab (must sit in a Resources folder
   for PhotonNetwork.Instantiate, like the game's own throwables) to the
   menu's Snowball Prefab slot. Interval defaults to 100 ms; 0.044 = 44 ms.
4. Paste the webhook URL into `FalconStaffWebhook.WebhookUrl`.
5. Grant roles with the PlayFab helper (login PlayFabIds only):
   `pf.py set-role <PlayFabId> headadmin`.

## Honest status

- APIs are grounded in this exact source tree (Player fields and Turn(),
  VRRig.myPlayer / tryOnSet / InitializeNoobMaterialLocal,
  CosmeticsController.ApplyCosmeticItemToSet, GorillaParent.vrrigDict,
  PhotonVoiceView.SpeakerInUse, RaiseEvent patterns from
  CosmeticsController). NOT compile-tested: there is no Unity in this
  environment, so the first import in the real project is the compile test.
- Photon bot tab is a hook, not an implementation: the internal bot joins
  through Sans's existing Photon bot module when that code is in the
  project; the menu button is where it plugs in.
- Soundboard voice push: clips play locally and fire
  `FalconSoundboard.OnClipStarted`; routing into the Photon Voice recorder
  needs a custom recorder input in the project (Voice 2 Recorder input
  factory) — that one hook is marked, not faked.
- Spooky music finds scene AudioSources named *music* or *siren*; rename
  the filter in `FalconStaffNet.SpookyMusic` if a map names them otherwise.
- Spin gun maps "smooth turn speed 9" to 540 deg/s for 10 s; the constant
  lives at the top of the Spin coroutine if it needs tuning.
- Trust model (per Sans's in-game design): the role value can only be set
  server-side, but effect events are trusted once sent, like every other
  event in this codebase. A determined cheater with a fake client is out of
  scope for a fangame staff tool.

## Self testing + dummy (added 2026-11-10 21:01)

- Staff tab has a **Self test** toggle. With it on, every gun can be
  pointed at your own rig: the effect runs locally through the same
  receiver code a victim's client runs (FalconStaffNet.ApplyLocal), gates
  unchanged (headadmin + open event for troll guns). Kick self-test is a
  dry run that only logs — a real self kick would just leave the room.
- **Spawn test dummy** button: plops the game's Gorilla Player Networked
  prefab in front of you as a frozen local rig for aim/raycast practice.
  It is not a room member and has no locomotion, so movement effects
  cannot move it; self test covers those.
