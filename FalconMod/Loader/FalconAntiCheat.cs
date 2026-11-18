// Project Falcon entry anti-cheat (Sans's spec, 2026-11-14; staged
// rework round 52, 2026-11-15). Runs ONLY in the FalconLoading scene,
// in named stages the loader displays one by one, before the main
// scene is allowed to load. Opening GorillaTagSJR directly (editor
// testing) runs none of this. On any flag the loader shows the player
// exactly what flagged and appends it to the log at
// Android/data/<package>/anticheat_log. A clean pass is logged too
// (round 52 - Sans: "I can't tell if the anticheat would actually
// work", so every boot leaves a receipt).
//
// SELF TEST: drop a file named "anticheat_selftest" into the game's
// files folder (Android/data/com.fluxtech.ProjectFalcon/files) and
// the SELF TEST stage raises a flag on purpose - the deny screen and
// the log entry prove the whole path works. Delete the file to pass
// again.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class FalconAntiCheat
{
    public const string OfficialId = "com.fluxtech.ProjectFalcon";

    public class Stage
    {
        public string Name;
        public Func<List<string>> Run;
    }

    public static List<Stage> Stages()
    {
        return new List<Stage>
        {
            new Stage { Name = "VERIFYING BUILD", Run = CheckBuild },
            new Stage { Name = "APK INTEGRITY", Run = CheckSignature },
            new Stage { Name = "CHECKING DEVICE", Run = CheckDevice },
            new Stage { Name = "ROOT CHECK", Run = CheckRoot },
            new Stage { Name = "FRIDA SCAN", Run = CheckFrida },
            new Stage { Name = "MOD LOADER SCAN", Run = CheckLoaders },
            new Stage { Name = "LIBRARY AUDIT", Run = CheckLibs },
            new Stage { Name = "SELF TEST", Run = CheckSelfTest },
        };
    }

    // /proc/self/maps is read once and shared by the stages that scan
    // it (the round-50 version re-read the whole file per check).
    private static string mapsCache;
    private static string Maps
    {
        get
        {
            if (mapsCache == null)
            {
                try { mapsCache = File.ReadAllText("/proc/self/maps").ToLowerInvariant(); }
                catch { mapsCache = ""; }
            }
            return mapsCache;
        }
    }

    // ---- stage 1: platform + official build -------------------------
    private static List<string> CheckBuild()
    {
        var flags = new List<string>();
        if (Application.platform != RuntimePlatform.Android)
        {
            flags.Add("PC / non-Android platform (" + Application.platform + ") - Project Falcon is Quest-only");
            return flags;
        }
        if (Application.identifier != OfficialId)
            flags.Add("unofficial build - package is '" + Application.identifier + "', official is '" + OfficialId + "'");
        return flags;
    }

    // ---- stage 1b: APK signing certificate (round 63, Sans) ---------
    // The mod route Sans described: extract the APK, drop a menu .so
    // into lib/, patch classes.dex (UnityPlayer) with a loadLibrary
    // const-string, repackage. A repack CANNOT carry the official
    // signature - it must be re-signed with a foreign key - so the
    // installed app's signing cert is pinned to the Falcon release
    // keystore's fingerprint. One check covers the lib, the dex
    // patch and every other APK edit at once. Unreadable cert = no
    // positive evidence = fail open (same rule as the other stages).
    // Round 67: this pin was WRONG for two days - the fingerprint
    // was mistyped when the keystore was generated (two chars,
    // "E0", dropped at position 38; the constant was 62 chars, not
    // 64), so build #12's official key false-flagged. The value
    // below is re-read from falcon-release.keystore itself
    // (keytool, 2026-11-16) - trust the file, never a transcription.
    private const string OfficialCertSha256 = "D84F136958C2886249A0DFB94A1E7D25746791E002522ADD152C2F24BFD81832";

    private static List<string> CheckSignature()
    {
        var flags = new List<string>();
        if (Application.platform != RuntimePlatform.Android) return flags; // already flagged in stage 1
        try
        {
            string hex = SigningCertSha256();
            if (hex == null) return flags;
            if (hex != OfficialCertSha256)
                flags.Add("repacked APK - the signing certificate is not the official Falcon key (cert SHA-256 " + hex + "). Repacked/modded builds are denied."); // round 67: full hash in the flag - a mismatch must be diagnosable from one log read
        }
        catch { }
        return flags;
    }

    private static string SigningCertSha256()
    {
        try
        {
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var pm = activity.Call<AndroidJavaObject>("getPackageManager");
            string pkg = Application.identifier;
            AndroidJavaObject[] sigs = null;
            try
            {
                var info = pm.Call<AndroidJavaObject>("getPackageInfo", pkg, 0x08000000); // GET_SIGNING_CERTIFICATES (API 28+)
                var signingInfo = info.Get<AndroidJavaObject>("signingInfo");
                sigs = signingInfo.Call<AndroidJavaObject[]>("getApkContentsSigners");
            }
            catch { }
            if (sigs == null || sigs.Length == 0)
            {
                var info = pm.Call<AndroidJavaObject>("getPackageInfo", pkg, 64); // GET_SIGNATURES (legacy)
                sigs = info.Get<AndroidJavaObject[]>("signatures");
            }
            if (sigs == null || sigs.Length == 0) return null;
            byte[] cert = sigs[0].Call<byte[]>("toByteArray");
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(cert);
                var sb = new System.Text.StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("X2"));
                return sb.ToString();
            }
        }
        catch { return null; }
    }

    // ---- stage 2: is this a Quest? ----------------------------------
    // Sans's rule: the words Oculus / Quest / Meta / Eureka /
    // Hollywood / Panther anywhere in the OS mean Quest. (Eureka,
    // Hollywood and Panther are Meta's internal Quest codenames.)
    private static List<string> CheckDevice()
    {
        var flags = new List<string>();
        if (Application.platform != RuntimePlatform.Android) return flags; // already flagged in stage 1
        string hay = (SystemInfo.operatingSystem + " " + SystemInfo.deviceModel + " " + SystemInfo.deviceName + " " + BuildFields()).ToLowerInvariant();
        string[] words = { "oculus", "quest", "meta", "eureka", "hollywood", "panther" };
        bool quest = false;
        foreach (string w in words) if (hay.Contains(w)) { quest = true; break; }
        if (!quest) flags.Add("not a Quest device - no Quest marker (oculus/quest/meta/eureka/hollywood/panther) in the OS identity");
        return flags;
    }

    private static string BuildFields()
    {
        try
        {
            var build = new AndroidJavaClass("android.os.Build");
            string[] fields = { "FINGERPRINT", "MODEL", "MANUFACTURER", "DEVICE", "PRODUCT", "TAGS", "HARDWARE" };
            string s = "";
            foreach (string f in fields)
            {
                try { s += " " + build.GetStatic<string>(f); } catch { }
            }
            return s;
        }
        catch { return ""; }
    }

    // ---- stage 3: root ----------------------------------------------
    private static List<string> CheckRoot()
    {
        var flags = new List<string>();
        string[] suPaths =
        {
            "/system/bin/su", "/system/xbin/su", "/sbin/su", "/su/bin/su",
            "/data/local/xbin/su", "/data/local/bin/su", "/data/local/su",
            "/system/sd/xbin/su", "/system/bin/failsafe/su",
        };
        foreach (string p in suPaths)
            if (File.Exists(p)) { flags.Add("root - su binary at " + p); break; }
        if (Directory.Exists("/data/adb/magisk")) flags.Add("root - Magisk data folder present");
        if (Directory.Exists("/data/adb/modules")) flags.Add("root - Magisk modules folder present");
        try
        {
            var build = new AndroidJavaClass("android.os.Build");
            string tags = build.GetStatic<string>("TAGS");
            if (tags != null && tags.Contains("test-keys")) flags.Add("root - test-keys build (TAGS=" + tags + ")");
        }
        catch { }
        return flags;
    }

    // ---- stage 4: Frida + injected external libraries ---------------
    private static List<string> CheckFrida()
    {
        var flags = new List<string>();
        string[] fridaFiles =
        {
            "/data/local/tmp/frida-server", "/data/local/tmp/frida",
            "/data/local/tmp/re.frida.server", "/data/local/tmp/frida-gadget.so",
            "/data/local/tmp/frida-server-16", "/data/local/tmp/frida-server-17",
        };
        foreach (string p in fridaFiles)
            if (File.Exists(p)) flags.Add("Frida - file present: " + p);
        if (PortOpen(27042)) flags.Add("Frida - server answering on port 27042");
        string maps = Maps;
        if (maps.Contains("frida")) flags.Add("Frida - frida code mapped into the game process");
        if (maps.Contains("gadget")) flags.Add("Frida - gadget mapped into the game process");
        if (maps.Contains("/data/local/tmp/") && maps.Contains(".so"))
            flags.Add("external library - a .so from /data/local/tmp is loaded in the game process");
        return flags;
    }

    private static bool PortOpen(int port)
    {
        try
        {
            using (var client = new System.Net.Sockets.TcpClient())
            {
                var ar = client.BeginConnect("127.0.0.1", port, null, null);
                bool ok = ar.AsyncWaitHandle.WaitOne(200);
                if (ok && client.Connected) { client.EndConnect(ar); return true; }
                return false;
            }
        }
        catch { return false; }
    }

    // ---- stage 5: mod loaders (MelonLoader, FusionCore / BepInEx) ---
    private static List<string> CheckLoaders()
    {
        var flags = new List<string>();
        string pkgDir = "";
        try { pkgDir = Directory.GetParent(Application.persistentDataPath).FullName; } catch { }
        string[] dirs =
        {
            pkgDir + "/MelonLoader", Application.persistentDataPath + "/MelonLoader",
            "/sdcard/MelonLoader", "/sdcard/Android/data/" + OfficialId + "/MelonLoader",
            pkgDir + "/BepInEx", Application.persistentDataPath + "/BepInEx",
            "/sdcard/BepInEx",
        };
        foreach (string d in dirs)
        {
            if (!Directory.Exists(d)) continue;
            if (d.Contains("MelonLoader")) flags.Add("MelonLoader - folder present: " + d);
            else flags.Add("FusionCore / BepInEx - folder present: " + d);
        }
        string maps = Maps;
        if (maps.Contains("melonloader")) flags.Add("MelonLoader - mapped into the game process");
        if (maps.Contains("bepinex")) flags.Add("FusionCore / BepInEx - mapped into the game process");
        if (maps.Contains("fusioncore")) flags.Add("FusionCore - mapped into the game process");
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string n = asm.GetName().Name.ToLowerInvariant();
                if (n.Contains("melonloader")) flags.Add("MelonLoader - assembly loaded: " + asm.GetName().Name);
                if (n.Contains("bepinex")) flags.Add("FusionCore / BepInEx - assembly loaded: " + asm.GetName().Name);
                if (n.Contains("fusioncore")) flags.Add("FusionCore - assembly loaded: " + asm.GetName().Name);
            }
        }
        catch { }
        return flags;
    }

    // ---- stage 5b: library audit (round 63, Sans) --------------------
    // Every .so mapped into the process, judged by WHERE it loaded
    // from (the maps text is lowercased, so all compares are too):
    //  - system/vendor/apex/odm/product: allowed by prefix, so a
    //    Horizon update renaming its own libs cannot false-flag.
    //  - the game's OWN install folder: the filename must be one of
    //    the stock set below - this is the check that catches a menu
    //    .so dropped into lib/ by an APK repack (the APK INTEGRITY
    //    stage catches the repack itself; this names the lib).
    //  - another app's install folder: NOT judged (round 67 - the
    //    round-63 rule flagged every Meta lib on the headset: the
    //    OpenXR runtime is its own app and its libs load into every
    //    VR process by design, and overlay tools like the OVR
    //    Metrics Tool do the same; build #12 false-flagged them
    //    all). A menu cannot ride in from another app's folder
    //    without that app loading it, which is not the repack route.
    //  - the game's OWN data folder / shared storage: legit game
    //    libs never load from there - flagged.
    //  - /data/local/tmp is FRIDA SCAN's flag, not repeated here.
    // The stock set is what the project ships (Unity trio + Photon
    // Voice pair + the Oculus plugins in the export). If a native
    // plugin is ever added to the project, add its filename here.
    private static readonly string[] KnownLibs =
    {
        "libunity.so", "libil2cpp.so", "libmain.so", "lib_burst_generated.so",
        "libopus_egpv.so", "libwebrtc-audio.so",
        "liboculusxrplugin.so", "libovrlipsync.so",
        "libaudiopluginoculusspatializer.so", "libovrplatformloader.so",
        "libmonodebuggerhandling.so",
        // Round 69: proven official by the Falcon-signed build's own
        // LIBS OBSERVED (anticheat_log, 2026-11-16): the Oculus XR
        // plugin ships libovrplugin + libopenxr_loader in the game's
        // own lib folder, and the Oculus SDK ships libsdktelemetry.
        // Rounds 63-68 missed all three and false-denied the official
        // build on them (builds #12 and #13).
        "libovrplugin.so", "libopenxr_loader.so", "libsdktelemetry.so",
    };

    // Every unique .so path seen by the audit, appended to the
    // anticheat_log by SaveResult - the ground-truth list for
    // trimming KnownLibs if a stock lib is ever missed.
    private static readonly List<string> LastLibs = new List<string>();

    private static List<string> CheckLibs()
    {
        var flags = new List<string>();
        string maps = Maps;
        if (string.IsNullOrEmpty(maps)) return flags;
        LastLibs.Clear();
        var seen = new HashSet<string>();
        foreach (string rawLine in maps.Split('\n'))
        {
            string line = rawLine.Trim();
            int slash = line.IndexOf('/');
            if (slash < 0) continue;
            string path = line.Substring(slash).Trim();
            if (path.EndsWith(" (deleted)")) path = path.Substring(0, path.Length - 10).Trim();
            if (!path.EndsWith(".so")) continue;
            if (!seen.Add(path)) continue;
            LastLibs.Add(path);
            if (path.Contains("/data/local/tmp/")) continue; // FRIDA SCAN owns this flag
            if (path.StartsWith("/system/") || path.StartsWith("/vendor/") || path.StartsWith("/apex/")
                || path.StartsWith("/odm/") || path.StartsWith("/product/")) continue;
            string file = path.Substring(path.LastIndexOf('/') + 1);
            if (path.Contains("/data/app/"))
            {
                if (path.Contains(OfficialId.ToLowerInvariant()))
                {
                    bool known = false;
                    foreach (string k in KnownLibs) if (file == k) { known = true; break; }
                    if (!known) flags.Add("extra library in the game's own lib folder: " + file + " - not part of the official build");
                }
                // round 67: another app's folder is not judged -
                // the Meta OpenXR runtime + overlay tools load from
                // their own install folders into every VR process.
                continue;
            }
            if (path.Contains("/storage/") || path.Contains("/sdcard/"))
                flags.Add("library loaded from shared storage: " + file);
            else if ((path.Contains("/data/data/") || path.Contains("/data/user/")) && path.Contains(OfficialId.ToLowerInvariant()))
                flags.Add("library loaded from the game's data folder: " + file);
            // anything else (dev/anon mappings) is not judged
        }
        return flags;
    }

    // ---- stage 6: self test ------------------------------------------
    private static List<string> CheckSelfTest()
    {
        var flags = new List<string>();
        try
        {
            if (File.Exists(Application.persistentDataPath + "/anticheat_selftest"))
                flags.Add("SELF TEST flag - anticheat_selftest file present (delete it to pass). The deny path works.");
        }
        catch { }
        return flags;
    }

    // ---- the log ------------------------------------------------------
    // Appends one boot record to anticheat_log in the package folder
    // (Android/data/<package>/anticheat_log - next to the files/
    // folder, NOT inside it). Flags are listed, or RESULT: CLEAN.
    public static void SaveResult(List<string> flags)
    {
        try
        {
            string dir = Directory.GetParent(Application.persistentDataPath).FullName;
            string path = Path.Combine(dir, "anticheat_log");
            string stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
            string body;
            if (flags == null || flags.Count == 0)
                body = "[" + stamp + "] RESULT: CLEAN (all stages passed)\r\n";
            else
                body = "[" + stamp + "] FLAGGED:\r\n - " + string.Join("\r\n - ", flags.ToArray()) + "\r\n";
            if (LastLibs.Count > 0)
                body += "LIBS OBSERVED: " + string.Join(", ", LastLibs.ToArray()) + "\r\n";
            File.AppendAllText(path, body);
        }
        catch { }
    }

    // Round 69: the loader's own diagnostics ride the same file -
    // it is the one artifact Sans reliably sends, while Debug.Log
    // output (logcat) never reached Spark across four loader builds.
    public static void AppendDiag(string text)
    {
        try
        {
            string dir = Directory.GetParent(Application.persistentDataPath).FullName;
            string path = Path.Combine(dir, "anticheat_log");
            File.AppendAllText(path, "LOADER DIAG: " + text + "\r\n");
        }
        catch { }
    }
}
