using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

// Project Falcon entry anti-cheat. Runs ONCE, in the loading scene only -
// the main scenes carry no checks, so opening GorillaTagSJR directly in
// the editor skips all of this (by design, for Unity testing).
// Checks: official build id, Quest hardware identity (Oculus / Quest /
// Meta / Eureka / Hollywood / Panther anywhere in the OS/device strings),
// PC instantly denied, root, Frida (server file, loaded gadget, open
// port), external libraries loaded from /data/local/tmp, MelonLoader and
// FusionCore/BepInEx (files, loaded libs, assemblies).
// Every flag is shown to the player and appended to
// Android/data/<package>/anticheat_log.
public static class FalconAntiCheat
{
    public const string OfficialId = "com.fluxtech.ProjectFalcon";

    public static List<string> RunChecks()
    {
        var flags = new List<string>();

        // PC (and anything not Android) is denied outright.
        if (Application.platform != RuntimePlatform.Android)
        {
            flags.Add("PC / non-Android platform detected (" + Application.platform + ") - Project Falcon is Quest only");
            return flags;
        }

        // Official build?
        if (Application.identifier != OfficialId)
            flags.Add("not the official build (package: " + Application.identifier + ")");

        // Quest identity: any of the codenames / brand words anywhere in
        // the OS + device strings.
        string osInfo = (SystemInfo.operatingSystem ?? "") + " " + (SystemInfo.deviceModel ?? "") + " " + (SystemInfo.deviceName ?? "");
        string buildInfo = AndroidBuildString();
        string all = (osInfo + " " + buildInfo).ToLowerInvariant();
        string[] questWords = { "oculus", "quest", "meta", "eureka", "hollywood", "panther" };
        bool isQuest = false;
        foreach (string w in questWords) if (all.Contains(w)) { isQuest = true; break; }
        if (!isQuest)
            flags.Add("device does not look like a Quest (" + osInfo.Trim() + ")");

        // Root.
        string[] suPaths = {
            "/system/bin/su", "/system/xbin/su", "/sbin/su", "/system/su",
            "/data/local/su", "/data/local/bin/su", "/data/local/xbin/su", "/su/bin/su",
            "/system/app/Superuser.apk", "/system/app/SuperSU.apk", "/system/app/Magisk.apk",
            "/data/adb/magisk", "/data/adb/modules"
        };
        foreach (string p in suPaths)
            if (File.Exists(p) || Directory.Exists(p)) { flags.Add("root detected (" + p + ")"); break; }
        if (buildInfo.ToLowerInvariant().Contains("test-keys"))
            flags.Add("root detected (test-keys build)");

        // Frida.
        if (File.Exists("/data/local/tmp/frida-server") || File.Exists("/data/local/tmp/re.frida.server"))
            flags.Add("frida server file present");
        if (FridaPortOpen())
            flags.Add("frida server port open (27042)");

        // Loaded libraries, from our own memory map.
        try
        {
            string maps = File.ReadAllText("/proc/self/maps").ToLowerInvariant();
            if (maps.Contains("frida")) flags.Add("frida library loaded");
            if (maps.Contains("gadget")) flags.Add("frida gadget loaded");
            if (maps.Contains("/data/local/tmp/") && maps.Contains(".so")) flags.Add("external library loaded from /data/local/tmp");
            if (maps.Contains("melonloader")) flags.Add("melonloader library loaded");
            if (maps.Contains("bepinex") || maps.Contains("fusioncore")) flags.Add("fusioncore / bepinex library loaded");
        }
        catch { }

        // Mod loader files next to the game.
        string pkgDir = "";
        try { pkgDir = Directory.GetParent(Application.persistentDataPath).FullName; } catch { }
        string[] mlDirs = {
            pkgDir + "/MelonLoader", pkgDir + "/files/MelonLoader",
            Application.persistentDataPath + "/MelonLoader"
        };
        foreach (string d in mlDirs)
            if (d.Length > 12 && Directory.Exists(d)) { flags.Add("melonloader files present"); break; }
        string[] fcDirs = {
            pkgDir + "/BepInEx", pkgDir + "/FusionCore",
            pkgDir + "/files/BepInEx", pkgDir + "/files/FusionCore",
            Application.persistentDataPath + "/BepInEx", Application.persistentDataPath + "/FusionCore"
        };
        foreach (string d in fcDirs)
            if (d.Length > 12 && Directory.Exists(d)) { flags.Add("fusioncore / bepinex files present"); break; }

        // Loaded assemblies (best effort - IL2CPP exposes only some).
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string n = "";
                try { n = asm.GetName().Name.ToLowerInvariant(); } catch { continue; }
                if (n.Contains("melonloader")) flags.Add("melonloader assembly loaded");
                if (n.Contains("bepinex") || n.Contains("fusioncore")) flags.Add("fusioncore / bepinex assembly loaded");
            }
        }
        catch { }

        return flags;
    }

    private static bool FridaPortOpen()
    {
        try
        {
            using (var client = new TcpClient())
            {
                var ar = client.BeginConnect("127.0.0.1", 27042, null, null);
                bool ok = ar.AsyncWaitHandle.WaitOne(200);
                if (ok && client.Connected) { client.EndConnect(ar); return true; }
                return false;
            }
        }
        catch { return false; }
    }

    private static string AndroidBuildString()
    {
        try
        {
            using (var build = new AndroidJavaClass("android.os.Build"))
            {
                string s = "";
                s += " " + build.GetStatic<string>("FINGERPRINT");
                s += " " + build.GetStatic<string>("MODEL");
                s += " " + build.GetStatic<string>("MANUFACTURER");
                s += " " + build.GetStatic<string>("DEVICE");
                s += " " + build.GetStatic<string>("PRODUCT");
                s += " " + build.GetStatic<string>("TAGS");
                s += " " + build.GetStatic<string>("HARDWARE");
                return s;
            }
        }
        catch { return ""; }
    }

    public static string LogPath()
    {
        try
        {
            string pkgDir = Directory.GetParent(Application.persistentDataPath).FullName;
            return Path.Combine(pkgDir, "anticheat_log");
        }
        catch { return null; }
    }

    public static void SaveLog(List<string> flags)
    {
        try
        {
            string path = LogPath();
            if (path == null) return;
            using (var w = new StreamWriter(path, true))
            {
                w.WriteLine("=== " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC ===");
                w.WriteLine("device: " + SystemInfo.deviceModel + " | os: " + SystemInfo.operatingSystem + " | package: " + Application.identifier);
                foreach (string f in flags) w.WriteLine("FLAGGED: " + f);
                w.WriteLine();
            }
        }
        catch { }
    }
}
