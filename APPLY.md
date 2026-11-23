## Round 94 — Voice clips: rolling-buffer semantics (Sans, 2026-11-22)

Sans: the 25 s voice clip on mod calls and reports was a
coin flip, worst in young lobbies - "make it a rolling
buffer so if the lobby's under 25s it works."

What the code actually did (FalconVoiceTap.BuildClipWav,
round 29): capture WAS a per-player ring, but the mix was
placed on a fixed 25 s canvas anchored at WALL-CLOCK now:
each buffer's endPos = len - (now - lastWriteDsp) * rate.
A quiet gap between the last words and the report pushed
buffers off the canvas (every dest < 0), the mix peaked
at zero, and the clip returned null. The report and ban
guns then swallowed the null silently (the mod-call path
at least posted its round-44 diagnostic). Compounding
it, the static buffer dict was never reset between
rooms: actor numbers are per-room, so a reused number in
a new lobby inherited the previous room's ring.

### The fix
- BuildClipWav anchors the timeline at the LAST SPEECH
  captured (max lastWriteDsp across buffers), not now.
  Every buffer is placed relative to that anchor, so a
  gap between the last words and the report can no
  longer push audio off the end. The clip is the last
  25 s OF VOICE, ending at the most recent sample.
- The silent head is trimmed: the WAV spans only the
  covered audio. A lobby with 6 s of speech yields a
  ~6 s clip instead of a padded file or a failure.
- FalconVoiceTap.ResetAll() clears every ring's
  counters; FalconStaffNet calls it from OnJoinedRoom
  and OnLeftRoom - the same per-room rule the file
  already applies to loud/size state.
- Report gun and ban gun now post the round-44-style
  diagnostic ("(report voice clip unavailable - no
  speech captured; N taps, M samples captured)") when a
  clip genuinely has nothing, instead of vanishing.

Unchanged: the taps, the 0.25 s binding sampler, the
decimation, the WAV format, the upload path, and the
mod-call flow.

### Files
- FalconMod/FalconStaff/FalconVoiceTap.cs (edited)
- FalconMod/FalconStaff/FalconStaffNet.cs (edited)
- FalconMod/FalconStaff/FalconStaffHandMenu.cs (edited)

ROUND 95 (2026-11-23): FalconTracking Quest head getters - manual Posef conversion. Both new-account builds (WebGL 2022 + Android 2021.3) failed at Execute scripts on the same two errors: FalconTracking.cs(64,22)/(68,22) CS0029 - this SDK's OVRPlugin.Vector3f/Quatf have NO implicit conversion to Unity Vector3/Quaternion and OVRPlugin.cs ships no ToVector3/ToQuaternion helper (verified in the project's own file). Head getters now convert manually via ToUnity helpers (position x,y,-z; quaternion -x,-y,z,w - the standard handedness flip; bridge is inert on Quest, flip compile-verified only). Clone step PASSED on the new org - the private repo connection works.

ROUND 96 (2026-11-23): WebGL graphics API pinned to WebGL 2.0. WebGL build #3 (2021.3) compiled with ZERO errors, then the player build died instantly: "Error building Player: Incompatible color space with graphics API" -> export directory empty. Cause verified in ProjectSettings.asset: project color space is Linear (m_ActiveColorSpace: 1) and there was NO WebGLSupport entry in m_BuildTargetGraphicsAPIs at all (only iOS/Android/Windows) - the export never targeted WebGL, so it resolved to WebGL 1.0, which cannot do Linear. Fix: added WebGLSupport entry, m_APIs 0e000000 (OpenGLES3 = WebGL 2.0), m_Automatic 0. Per-platform list - the Android entry and the Quest build are untouched, color space stays Linear everywhere.
