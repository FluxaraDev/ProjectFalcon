using System;
using System.Reflection;
using System.Runtime.InteropServices;

// Round 85 (Sans): ITEMS 5 - the PhaseSync toggle. ON = PhaseSync
// 0, OFF = back to 4 (4 is where its scale stops; his overlay
// reads PHASE: 4 and no offered control goes lower).
//
// This project's managed OVR layer exposes NO PhaseSync API -
// verified 2026-11-19 by full-text search of the export's
// OVRPlugin.cs and OVRManager.cs (zero hits). The native XR
// plugin knows the concept (libOculusXRPlugin carries the
// PhaseSync distortion-window flag and a "phaseSync" config
// string), so Apply() works in the open, in order:
//   1. a reflection sweep of the Oculus-related assemblies for
//      any PhaseSync member the BUILT DLLs actually carry -
//      a static setter/property, if one exists, is called;
//   2. guarded P/Invoke attempts on the native OVRPlugin lib.
// Every candidate and outcome lands in the session log, so one
// on-device run settles which route (if any) this build has -
// no blind promises either way.
public static class FalconPhaseSync
{
    public static string LastResult = "not tried";

    [DllImport("OVRPlugin", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool ovrp_SetPhaseSyncEnabled(bool enabled);

    [DllImport("OVRPlugin", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ovrp_SetPhaseSync(int value);

    public static void Apply(bool zero)
    {
        int want = zero ? 0 : 4;
        string log = "";
        bool done = false;
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an = asm.GetName().Name ?? "";
                if (an.IndexOf("Oculus", StringComparison.OrdinalIgnoreCase) < 0
                    && an.IndexOf("OVR", StringComparison.OrdinalIgnoreCase) < 0
                    && an.IndexOf("firstpass", StringComparison.OrdinalIgnoreCase) < 0) continue;
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    if (t == null) continue;
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        if (m.Name.IndexOf("PhaseSync", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        log += "found " + t.Name + "." + m.Name + "; ";
                        if (done || !m.IsStatic) continue;
                        var ps = m.GetParameters();
                        try
                        {
                            if (ps.Length == 1 && ps[0].ParameterType == typeof(bool)) { m.Invoke(null, new object[] { zero }); done = true; log += "called(bool) "; }
                            else if (ps.Length == 1 && ps[0].ParameterType == typeof(int)) { m.Invoke(null, new object[] { want }); done = true; log += "called(int) "; }
                        }
                        catch (Exception e) { log += "call failed " + e.GetType().Name + "; "; }
                    }
                    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        if (p.Name.IndexOf("PhaseSync", StringComparison.OrdinalIgnoreCase) < 0 || !p.CanWrite) continue;
                        log += "found prop " + t.Name + "." + p.Name + "; ";
                        if (done) continue;
                        try
                        {
                            if (p.PropertyType == typeof(bool)) { p.SetValue(null, zero, null); done = true; log += "set prop(bool) "; }
                            else if (p.PropertyType == typeof(int)) { p.SetValue(null, want, null); done = true; log += "set prop(int) "; }
                        }
                        catch (Exception e) { log += "prop failed " + e.GetType().Name + "; "; }
                    }
                }
            }
        }
        catch (Exception e) { log += "sweep failed " + e.GetType().Name + "; "; }

        if (!done)
        {
            try { bool r = ovrp_SetPhaseSyncEnabled(zero); done = true; log += "native SetPhaseSyncEnabled -> " + r + "; "; }
            catch (Exception e) { log += "native1 " + e.GetType().Name + "; "; }
        }
        if (!done)
        {
            try { int r = ovrp_SetPhaseSync(want); done = true; log += "native SetPhaseSync -> " + r + "; "; }
            catch (Exception e) { log += "native2 " + e.GetType().Name + "; "; }
        }

        LastResult = (done ? "SET " : "NO ROUTE for ") + want + " (" + log.Trim() + ")";
        FalconSessionLog.Log("PHASESYNC " + LastResult);
    }
}
