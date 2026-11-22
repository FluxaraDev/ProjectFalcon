// Project Falcon - WebXR browser side of the tracking
// bridge (round 91). Runs only inside the WebGL build.
//
// FalconWebXR_Init: checks WebXR support, then waits for
// the first user click (browsers require a user gesture to
// start an XR session) and requests an immersive-vr
// session. Each XR frame it reads the viewer pose and both
// grip poses and pushes one flat CSV packet to the Unity
// object "FalconWebXR", method "OnPoses":
//   head px,py,pz,qx,qy,qz,qw, left ..., right ...
// in raw WebXR space; the C# host converts to Unity space.
//
// If WebXR is absent the page just runs flat (desktop view)
// - the game still loads, which is milestone 1.
mergeInto(LibraryManager.library, {
  FalconWebXR_Init: function () {
    if (!('xr' in navigator)) {
      console.log('FalconWebXR: no WebXR in this browser, running flat');
      return;
    }
    navigator.xr.isSessionSupported('immersive-vr').then(function (supported) {
      if (!supported) {
        console.log('FalconWebXR: immersive-vr not supported, running flat');
        return;
      }
      var start = function () {
        document.removeEventListener('click', start);
        navigator.xr.requestSession('immersive-vr', {
          optionalFeatures: ['local-floor', 'bounded-floor']
        }).then(function (session) {
          var refSpace;
          session.requestReferenceSpace('local').then(function (space) {
            refSpace = space;
            var onFrame = function (time, frame) {
              session.requestAnimationFrame(onFrame);
              var viewer = frame.getViewerPose(refSpace);
              if (!viewer) return;
              var vals = [];
              var push = function (pose) {
                if (!pose) { vals.push(0, 0, 0, 0, 0, 0, 1); return; }
                var p = pose.transform.position;
                var q = pose.transform.orientation;
                vals.push(p.x, p.y, p.z, q.x, q.y, q.z, q.w);
              };
              push(viewer);
              var left = null, right = null;
              for (var i = 0; i < frame.session.inputSources.length; i++) {
                var src = frame.session.inputSources[i];
                if (!src.gripSpace) continue;
                var gp = frame.getPose(src.gripSpace, refSpace);
                if (src.handedness === 'left') left = gp;
                else if (src.handedness === 'right') right = gp;
              }
              push(left);
              push(right);
              SendMessage('FalconWebXR', 'OnPoses', vals.join(','));
            };
            session.requestAnimationFrame(onFrame);
            console.log('FalconWebXR: session running, poses flowing');
          });
        }).catch(function (e) {
          console.log('FalconWebXR: session request failed: ' + e);
        });
      };
      document.addEventListener('click', start);
      console.log('FalconWebXR: ready - click the page to enter VR');
    });
  }
});
