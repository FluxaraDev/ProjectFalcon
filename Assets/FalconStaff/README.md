# Falcon Staff — setup & status

Staff system for Project Falcon, built against the Spring 2023 source tree
(the fangame base). Spec: `../FALCON-STAFF-MENU-SPEC.md`.

## Files

- `FalconStaffRole.cs` — PlayFab role read at login (`falcon_role` in
  Read-Only User Data), gates everything. Call `FalconStaffRole.FetchRole()`
  once right after the successful LoginWithCustomID callback in the
  authenticator, before any report/menu UI can open.
- `FalconStaffBootstrap.cs` — spawns the whole staff system at runtime
  (AfterSceneLoad). No scene editing needed anywhere.
- `FalconCoordCube.cs` — Coordinates Cube (Move tab toggle): a 0.5 m
  cube carried in the right hand; A drops / picks it up; its world
  coordinates float above it for map-editing measurements.
- `FalconVoidGuard.cs` — game-wide (every player): falling below
  y = -50 teleports you back to the stump spawn (-64, 12.53, -83)
  instead of the old disconnect kick or an infinite void fall.
  `GorillaNetworkDisconnectTrigger` was patched to the same behavior.
- `FalconBoardTexts.cs` — applies the Falcon code of conduct to the
  scene's static CoC boards at startup (glitches / exploits /
  out-of-bounds explicitly count as cheating).
- `FalconStaffHandMenu.cs` — the in-VR staff menu: a world-space panel
  on the left hand (left menu button opens it). Aim the right hand at
  the panel, pull the right trigger to press buttons; aim away from the
  panel to fire an armed gun. Tabs: Staff (kick gun), Troll (headadmin,
  event-gated guns + spooky music), Sound (soundboard), Move (long arms
  / speed boost toggles that re-assert against join resets).
- `FalconStaffMenu.cs` — the original IMGUI menu. Kept for reference /
  PC-mirror debugging; NOT spawned anymore (the hand menu supersedes it).
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

1. Nothing in the scene. `FalconStaffBootstrap` creates the staff
   object at runtime on every build.
2. Snowball gun: still unwired — it needs a snowball prefab loadable by
   name (Resources folder, like the game's own throwables) now that
   there is no Inspector to assign one in. Interval defaults to 100 ms;
   0.044 = 44 ms. The Troll tab labels it honestly until then.
3. Paste the webhook URL into `FalconStaffWebhook.WebhookUrl`.
4. Grant roles with the PlayFab helper (login PlayFabIds only):
   `pf.py set-role <PlayFabId> headadmin`.
   (Sans's own account 9BAF8E542FA8281 / FLUXDEV is headadmin since
   2026-11-12.)
5. Hand-menu fit: if the panel sits wrong on the hand, tune
   PanelLocalPos / PanelLocalEuler at the top of FalconStaffHandMenu.cs.

## Honest status

- APIs are grounded in this exact source tree (Player fields and Turn(),
  VRRig.myPlayer / tryOnSet / InitializeNoobMaterialLocal,
  CosmeticsController.ApplyCosmeticItemToSet, GorillaParent.vrrigDict,
  PhotonVoiceView.SpeakerInUse, RaiseEvent patterns from
  CosmeticsController). The original staff code compiled clean in UBA
  build #4 (2026-11-12) and the game runs on device; the hand menu's
  compile test is the next UBA build.
- Hand menu text uses legacy UnityEngine.UI.Text with a font cloned at
  runtime from an in-scene Text (scoreboard/computer): this build's
  scripts contain no TextMeshPro, so TMP was not an option.
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

## RAVE tab (2026-11-12)
The computer's CREDITS tab is now the RAVE tab (label + screen + ENTER rewired
in GorillaComputer; the state enum value is unchanged internally).
`FalconRave` plays SOPHIE - VYZEE from `Assets/Resources/rave_song.mp3`
(map-wide 2D audio), cycles every directional light + fog + ambient through
disco colors, enables colored fog, drops gravity to -2.0, and boosts jump to
15 / 2.4 for the length of the song (3:18). Start rules: not in a room (solo;
all joins, triggers included, are locked until the song ends with a countdown
on the computer board), or master client of a private (invisible) room, where
event 210 starts it lobby-wide on a synced clock. On room leave every effect
is restored locally, so nothing leaks into the next lobby.
REQUIRED: `Assets/Resources/rave_song.mp3` must exist in the repo before
building - it is a binary and rides outside the snapshot push (uploaded via
the GitHub web UI). Without it the RAVE page reports the song as missing.

## Round 5 (2026-11-12, build #9): build #8 field repairs

- **Staff menu reachability.** Input is read before the role gate now,
  from two paths: the XR menu button and the game's own
  ControllerInputPoller (left secondary / Y). With no role the panel
  opens as a status page (role, fetch state, last PlayFab error, press
  counter) with a retry-fetch button; guns/movement stay gated.
  FalconStaffRole tracks FetchState/LastError and can re-fetch
  (EnsureRole) when the player pokes the menu button.
- **Computer failed-state repair (FalconComputerFix +
  GorillaComputer.FalconClearFailureState).** The stock build only
  restores the red failed state after a no-internet failure, so Falcon
  gate messages bricked the computer until restart. Falcon gates call
  FalconFailure.Mark(); the watcher clears the state once the blocking
  condition is gone (or on room join). Stock errors untouched.
- **MOTD board.** The title-data display path is stripped in this
  export, so FalconBoardTexts now writes the Falcon MOTD directly
  (multi-pass over the first seconds), and the CoC cheats line is
  compressed to fit the physical board.
- **Rave.** Page text shortened to fit the computer screen, with a
  STEP + STAFF status line. Private-lobby start no longer relies on
  the Photon event looping back to the host (host begins locally too).
  Disco now tints the day/night manager's unlit material lists - the
  map is unlit, so lights/fog alone only ever tinted lit objects; the
  darker list's base colors are restored at rave end because the
  manager carries hue forward between ticks.
- **Grace-window rave exploit closed (pre-#9 catch by Sans):** a solo rave started during the leave/join transition ("not in a room" reads true mid-join) survived into the joined public lobby - effects with no master and no server-side rave. Solo starts now require a settled Photon state (ConnectedToMasterServer or Disconnected), and OnJoinedRoom ends any running rave.
- **CoC/MOTD auto-fit:** Sans's build #8 screenshot showed the CoC's last lines running off the board's bottom edge. FalconBoardTexts now enables best-fit on both boards (max = serialized size, min 10) - words unchanged, rendered size shrinks only as much as the rect needs.

## Round 6 (2026-11-12, build #10): build #9 field repairs

- **Panel backwards:** PanelLocalEuler 180 -> 0 (it read mirrored on device).
- **Pointer line:** a green LineRenderer now traces the right hand's ray
  whenever the menu is open or a gun is armed - the same ray the guns use.
- **Guns report back:** the menu footer shows the last gun result
  (sent / no target / denied + why) instead of failing silently, and
  targeting falls back to a 12-degree cone pick over all rigs when the
  physics raycast finds no rig collider.
- **Long arms + speed are headadmin spawn state, not toggles.**
  Player.cs gained FalconArmScale (1.4), applied to the head->hand
  offset inside the three Current*HandPosition methods before the
  maxArmLength clamp. Raising the cap alone never did anything - the
  physical hand cannot reach it (Sans called this in #9). Headadmins
  also get cap 10000 + jump 12 / 2.0 asserted per frame.
- **Coord cube:** 0.5 m -> 0.15 m (label lowered to match).
- **Rave disco v3:** the forest/city surfaces live in the day/night
  manager's LIGHTMAPPED lists (dayNightSupportedMaterials + Cutout),
  which round 5 never tinted - only genuinely lit objects reacted.
  All four lists are now tinted at full disco brightness (dark-phase
  bases no longer dim it to nothing) and restored at rave end; the
  RAVE page STEP line reports the material count (MATS n).

## Round 7 (2026-11-12, build #10): support role field + gates removed

- **SUPPORT page correction:** round 5.4 pasted Sans's three role
  formats as a static legend; he meant the field to show the SIGNED-IN
  player's own role. Now: ROLE + FalconRoleText() -> HEADADMIN
  (EVERYTHING) / ADMIN (INSTABAN) / STAFF / PLAYER (DEFAULT).
- **Safety toggles removed (Sans, build #9: "too many safety toggles -
  head admins know what they click"):** the trolling-event gate and the
  self-test toggle are gone. Headadmin guns fire when clicked, spooky
  music sends immediately, and shooting your own rig self-tests without
  arming anything first. Troll use is still webhook-logged.

## Round 8 (2026-11-12, build #10): Start Rave button in the menu

- Sans: city has no computer, so add a Start Rave button to the staff
  menu. It's on the Troll tab: "Start rave (this lobby)" calls the
  computer's own FalconRave.TryStartFromComputer() verbatim (headadmin
  any-lobby rule included). Refusals now also land in FalconRave.
  LastResult and show in the menu footer, since city has no screen.
- Physical city computer clone: NOT a prop copy in this build - recon
  of the scene found exactly ONE GorillaComputer logic component (on a
  childless logic object under Photon Manager) shared by ~3 keyboard
  desks (138 buttons), all binding to the singleton instance at Start.
  A real second desk = second logic instance + rewiring a cloned desk
  group's screen and buttons to it. Deferred pending Sans's word; his
  spot for it: (-50.76, 16.62, -99.83).

## Round 9 (2026-11-12, build #10): Optional Preds + RGB fallback plan

- **Optional Preds (Items tab, option 3):** FalconPreds.cs - the
  OculusPreds math (velocity x predictionTime on head + hands,
  OVRPlugin/OVRInput velocities), default predictionTime 0.06 (the
  author's value, "about 50 preds at 72 Hz" - Sans's call, changed
  from 0.02). Applied as offsets in LateUpdate, restored first thing
  in Update (execution order -100) so locomotion never reads a
  predicted pose. Items tab also holds the test dummy (1) and coord
  cube (2); pred time adjusts in 10 ms steps (0-150). Sans's target:
  preds that show on OVR Metrics - #10's off-vs-on PRED read is the
  verdict. Spark's prior: metrics PRED is the runtime compositor's
  number and app-side offsets likely won't move it; hand feel may
  still change. His headset decides.
- **RGB fallback (Sans, conditional - only if disco v3 fails in
  #10):** one point light covering the whole map, hue-cycled by his
  RainbowPointLight script (~/workspace/user/files/
  RainbowPointLight_62_mjbf.cs). Not built; logged as the plan.

## Round 10 (2026-11-12, build #10): ITEMS page is for everyone

- **Correction of round 9's placement (Sans):** the Items tab is the
  COMPUTER's ITEMS page (stock Visuals state), open to EVERY player -
  staff mods do not belong there. The hand menu's Items tab is removed
  again (dummy/cube stay on Staff/Move as before).
- **ITEMS page now holds exactly three toggles, all default ON:**
  option 1 = long arms (FalconArmScale 1.4x), option 2 = speed boost
  (jump 9.1 / mult 1.54 - Sans's 1.4x pair; the 12 / 2.0 he called
  way too high), option 3 = Optional Preds (0.06). FalconPlayerOptions
  + FalconOptionsDriver (re-asserts the speed pair against join
  resets). The stock page's particles / instrument-volume controls
  are replaced, per his "only these three".
- **The headadmin always-on movement perk is retired:** ApplyMovement
  is gone; headadmins use the same ITEMS toggles as everyone.
- **Coord cube label doubled** (canvas scale 0.0016 -> 0.0032, raised
  to y 0.26) - build #9's text was too small to read up close.

## Round 11 (2026-11-12, build #11): preds camera bug - hotfix

- **Build #10 broke the camera (Sans: camera under the map, model
  above, can't walk). Root cause:** round 9's FalconPreds wrote pose
  offsets into Player.headCollider - in this build that collider sits
  on the MAIN CAMERA GameObject (Main Camera < TurnParent <
  GorillaPlayer), and locomotion reads the same transform for body
  placement. NEVER write the camera transform.
- **Fix:** FalconPreds rewritten - no writes to any Player transform,
  no snapshot/restore. Offsets now land on the local offline VRRig's
  visual hand transforms only (re-derived from the tracked anchors
  every frame, so they self-erase and cannot feed physics). Head
  prediction removed: from a script it is impossible in this game -
  the only head transform is the camera. Head preds = runtime/ADB.
- Defaults unchanged (ITEMS toggles all ON); with the camera path
  gone, default-on preds are visual-only and safe.

## Round 12 (2026-11-12, build #11): long arms reverted - Sans was right

- **Build #10's camera break was the LONG ARMS, not the preds**
  (Sans's isolation). Round 6's FalconArmScale multiplied the
  head->hand offset inside Player.Current*HandPosition - those
  methods ARE the locomotion (body movement = current minus last
  hand position), the last positions are seeded from the RAW hand
  transforms, and the head reference is the camera itself, so scaled
  reads dragged the body (and with it the rig/camera root) under
  the map from spawn. Player.cs is fully reverted to stock.
- The round-11 preds rewrite stands on its own merits (never write
  the camera transform), but it was not this bug.
- Option 1 interim: reach cap removed (10000) while ON - the
  original scene-edit spec. The real long-arms route is Sans's pick:
  whole-player scale 1.4 (the game's own scale field - every radius,
  clamp and unstick distance already multiplies by it) vs visual-only
  arm stretch (his SteamLongArm approach) + cap off.

## Round 13 (2026-11-12, build #11): pointer beam replaces the line

- Build #10 (Sans): "still no arrow on staff menu and I can't
  select". The round-6 pointer was a LineRenderer whose material
  came from Shader.Find("Sprites/Default") - stripped from this
  build, so material creation threw inside the menu's Update every
  frame it was open, killing the beam and everything after it in
  Update (selection included). Replaced with a stretched primitive
  cube beam (the coord cube's proven render path), collider removed
  so it can never eat a raycast, tinted green, 8 mm thick.

## Round 14 (2026-11-12, build #11): preds REMOVED, arms restored

- **Sans's final isolation (overrides the round-12 call): "It was
  the preds. Somehow something related to movement breaks the rig.
  Remove preds, keep long arms and speed boost - they work."**
- Optional Preds are DELETED: FalconPreds gutted to a tombstone,
  bootstrap hook removed, ITEMS option 3 and all Preds state removed
  from FalconPlayerOptions/GorillaComputer.
- Round 12's Player.cs revert is itself reverted: the round-6 arm
  scaling (FalconArmScale + ScaledHandPosition, 9 call sites) is
  restored exactly as build #10 shipped it - Sans verified long
  arms + speed boost working. Option 1 drives FalconArmScale again.
- Round 13's pointer beam is unaffected and stays.

## Round 15 (2026-11-12, build #11): End Rave button + disco v4

- **End rave button (Sans):** Troll tab now has Start rave / End rave
  side by side. End = FalconRave.StopRaveFromMenu(): Photon event
  211 to the rest of the lobby + local EndRave; full restore runs on
  every client. Solo: just ends locally.
- **Disco v4 - the map finally gets a real lever.** Build #10 verdict:
  leaves disco'd, the map didn't respond. Root cause in
  BetterDayNightManager: the map's custom shader reads two GLOBAL
  textures (_GlobalDayNightLightmap1/2) + a lerp float. It does not
  read material.color (v3's day-list writes were invisible on it) and
  it does not evaluate dynamic lights AT ALL - so the rainbow point
  light fallback could never have repainted the ground either.
  v4: at BeginRave, capture the current global pair, blit-tinted
  copies (Falcon/LightmapTint shader, disco color multiply) into
  RenderTextures, re-point the globals every frame; EndRave hands
  the originals back. Phase changes mid-rave are adopted as the new
  base. The shader is anchored by a Resources material so it cannot
  be stripped (round 13's lesson). STEP line shows "LM" when the
  tint path is live.

## Round 16 (2026-11-12, build #11): self-test hardened

- Sans: "No self test - I can't shoot myself with any of the troll
  guns." Two stacked causes: in build #10 the pointer exception
  killed menu clicks, so guns could not even be armed (fixed round
  13); and the hit path itself - a forward physics raycast almost
  never finds a VRRig on your own colliders, and ConePickRig's 12
  degree cone at your own head demands near-perfect aim at your own
  face. GunFire now checks a dedicated self cone FIRST: your own
  head within 30 degrees and 2.5 m = FireSelf, always.
- Fling now sustains its velocity for 0.35 s of FixedUpdates - a
  single velocity set is overwritten by the locomotion immediately
  and reads as "nothing happened".
- Still open (Sans, same message): bot features from his Python
  photon bot work (rocket, track, etc.) natively in-game for staff -
  specifics being pinned down with him before building.

## Round 17 (2026-11-12, build #12): name-gate deadlock + JSON role auth

- **Name gate deadlock (Sans, build #11: "locks you out the computer,
  you can't change it" - everyone).** The gate's red failure screen
  bricked the computer, and FalconComputerFix held that screen until
  the name was fixed - but the NAME page lives on the bricked
  computer. Catch-22 for every default-named player. Fix: Falcon
  failure messages are now transient (clear after the 2 s grace,
  always); the gates still block every join and re-show the message
  per attempt.
- **Name save hardening:** ProcessNameState ran the CheckForBadName
  CloudScript call BEFORE saving the name; the function does not
  exist on the Falcon title (Azure Functions registry, not legacy
  CloudScript). The check now runs after the save, try/catch guarded;
  same guard on the room-code check so it can never eat a join.
- **Role parse is now tolerant:** plain, quoted, or JSON-blob values
  ({"falcon_role":"headadmin"}) in the falcon_role read-only data all
  auth the same (Sans: "staff menu doesn't auth with json").
