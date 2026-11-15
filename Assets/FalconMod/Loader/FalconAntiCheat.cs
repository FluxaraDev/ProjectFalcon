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
            new Stage { Name = "CHECKING DEVICE", Run = CheckDevice },
            new Stage { Name = "ROOT CHECK", Run = CheckRoot },
            new Stage { Name = "FRIDA SCAN", Run = CheckFrida },
            new Stage { Name = "MOD LOADER SCAN", Run = CheckLoaders },
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
            File.AppendAllText(path, body);
        }
        catch { }
    }
}
