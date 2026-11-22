## Round 91 — WebGL milestone 1: the game loads in a browser (Sans, 2026-11-21)

Sans's route for players who can't sideload: NOT a remake -
THIS project compiled with Unity's WebGL compiler, bridged
to WebXR. His design: one IXRTrackingBridge interface, two
implementations, detected at runtime on the loading screen
and swapped there, Quest untouched. This round is
milestone 1 only: a WebGL build that gets through the
loader and loads the game. Tracking sessions are plumbed
but head-pose writing and buttons are milestone 2.

### The platform gate (the thing that would have killed it)
- FalconAntiCheat.CheckBuild flagged EVERY non-Android
  platform as "Quest-only" - a WebGL build died at stage 1.
  WebGLPlayer now passes stage 1 explicitly, which passes
  the Android stages too (they already defer to stage 1 on
  non-Android; there is no APK to fingerprint, no root and
  no Frida in a browser). The Android path is unchanged,
  and any other platform still flags exactly as before.
- FalconLoader logs "PLATFORM: <platform>" as its first
  diag line - the runtime detection the swap keys on, and
  the first thing to check in a loader DIAG from the web
  build.

### The bridge (Sans's interface, verbatim)
- NEW FalconTracking.cs: IXRTrackingBridge exactly as
  Sans wrote it (head/left/right position + rotation).
  FalconTracking.Current picks by runtime platform:
  WebGLPlayer -> WebXRTrackingBridge, else
  QuestTrackingBridge. The Quest implementation is a pure
  reader of the same OVRInput.GetLocalController* /
  OVRPlugin.GetNodePose values the game already produces
  (the calls FalconPreds uses) - shipping it changes
  nothing on Quest.
- NEW FalconWebXRDriver.cs: FalconWebXRHost receives pose
  packets from the browser (SendMessage to a dedicated
  child GameObject named "FalconWebXR"), converts WebXR
  right-handed space to Unity space (position z-flip,
  quaternion z/w sign flip - standard mapping, marked
  UNVERIFIED until a real session runs), and stores them
  on WebXRTrackingBridge. FalconWebXRDriver (added by the
  bootstrap, self-disables in Awake on any non-WebGL
  platform) writes the bridge's hand poses into
  Player.leftHandTransform / rightHandTransform - the
  same anchors the Oculus stack writes on Quest, so
  locomotion, VRRig and preds downstream cannot tell the
  difference. Head writing is deliberately NOT in this
  round: the head runs through the camera rig chain and
  the wrong node breaks the view; hands first.
- NEW Plugins/WebGL/FalconWebXR.jslib: the browser side.
  Checks navigator.xr + immersive-vr support, waits for
  the first click (sessions need a user gesture), requests
  a local-floor session, and per XR frame pushes viewer +
  both grip poses as one CSV packet. No WebXR = the page
  runs flat and the game still loads.

### What Sans does for the first build
UBA: add a second build target in the same project -
platform WebGL, same repo/branch, Unity 2021.3.45f2 -
and run it. Expect the first attempt to surface WebGL
compile errors in Oculus/game code (native plugin
references are the usual suspects); that error list is
the work list for milestone 1 fixes. The Android target
and its builds are unaffected by everything in this
round.

### Files
- FalconMod/Loader/FalconAntiCheat.cs (edited)
- FalconMod/Loader/FalconLoader.cs (edited)
- FalconMod/FalconStaff/FalconTracking.cs (new)
- FalconMod/FalconStaff/FalconWebXRDriver.cs (new)
- FalconMod/FalconStaff/FalconStaffBootstrap.cs (edited)
- FalconMod/Plugins/WebGL/FalconWebXR.jslib (new)
