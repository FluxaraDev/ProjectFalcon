## Round 92 — Fix the three compile errors both builds died on (2026-11-21)

Sans ran the first WebGL build AND an Android build
together; both failed at 'Execute scripts' on the SAME
three errors - every one in Falcon code, none in the game,
Photon or the Oculus assemblies. The WebGL build got
through everything else in the project, which is the
milestone-1 signal that matters: the fence list is these
three lines, not the whole SDK.

1. FalconTracking.cs(63,30) + (71,30) CS1501 - round 91
   called OVRPlugin.GetNodePose(node, step, out pos, out
   rot); that overload does not exist in this SDK.
   Verified in the project's own OVRPlugin.cs (line 3867):
   GetNodePose(Node, Step) RETURNS a Posef (.Position /
   .Orientation, fields verified line 704). The Quest
   bridge head getters now use exactly that.
2. FalconLoader.cs(622,17) CS1061 - round 88 hooked the
   readout materials' atlas tracking with
   Font.textureRebuiltCallback, which does not exist in
   Unity 2021.3 (this Android build was the first to
   include round 88, which is why it surfaces only now).
   The event hook is GONE: MakeTm registers each (clone
   material, font) pair, and SyncTm - which already runs
   on every loader text update - re-points each clone's
   mainTexture at its font's current atlas. Same net
   effect for a loading screen (the atlas only rebuilds
   when new glyphs appear, and glyphs arrive with text
   changes), zero version-sensitive API.

### Files
- FalconMod/FalconStaff/FalconTracking.cs (edited)
- FalconMod/Loader/FalconLoader.cs (edited)
