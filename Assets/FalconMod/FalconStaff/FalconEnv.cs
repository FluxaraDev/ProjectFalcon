using UnityEngine;

// Round 82 (Sans): lobby environment overrides - a weather
// changer and a time changer in the staff Settings tab. They
// apply to THIS LOBBY only: the change is broadcast as a Falcon
// event (EvEnv) to everyone in the room, every client applies
// it locally, and leaving the room clears it.
//
// Mechanism (from BetterDayNightManager's own code, read
// 2026-11-19): the manager computes phase + weather from the
// UTC clock in its UpdateTimeOfDay coroutine and only repaints
// (ChangeMaps) when the clock's phase index changes. Its
// public SetOverrideIndex(i) sets the phase and repaints
// immediately, but the clock reclaims the maps at its next
// phase change - so while an override is active this driver
// re-asserts it once a second. Weather is a separate slot in
// the seeded weatherCycle[]: forcing rain/clear means pointing
// currentWeatherIndex at a slot of the wanted type (the
// coroutine never recomputes it per tick, only +1 on clock
// phase changes - the same 1 s re-assert covers that).
// Clearing glues back to the clock: stop asserting, then
// SetOverrideIndex(the clock's own current index) once to
// repaint the clock's texture pair immediately.
public class FalconEnv : MonoBehaviour
{
    public static int TimeOverride = -1;    // -1 = auto (the game's clock), else phase 0..9
    public static int WeatherOverride = -1; // -1 = auto, 0 = clear, 1 = rain

    private float nextTick;
    private bool clearPending;

    public static bool AnyActive { get { return TimeOverride >= 0 || WeatherOverride >= 0; } }

    public static string PhaseName(int i)
    {
        try
        {
            var mgr = BetterDayNightManager.instance;
            if (mgr != null && mgr.dayNightLightmaps != null && i >= 0 && i < mgr.dayNightLightmaps.Length && mgr.dayNightLightmaps[i] != null)
                return mgr.dayNightLightmaps[i].name;
        }
        catch { }
        return "PHASE " + i;
    }

    public static string WeatherName(int w)
    {
        return w == 0 ? "CLEAR" : w == 1 ? "RAIN" : "AUTO";
    }

    public static string TimeName(int t)
    {
        return t < 0 ? "AUTO" : PhaseName(t);
    }

    // Called by the Settings steppers (broadcast = true) and by
    // the EvEnv receiver in FalconStaffNet (broadcast = false).
    public static void SetOverrides(int time, int weather, bool broadcast)
    {
        TimeOverride = time;
        WeatherOverride = weather;
        ApplyNow();
        if (broadcast)
        {
            FalconStaffNet.SendToAll(FalconStaffNet.EvEnv, new object[] { time, weather });
            FalconStaffWebhook.Log("ENV SET time=" + TimeName(time) + " weather=" + WeatherName(weather) + " (this lobby)");
        }
        FalconSessionLog.Log("ENV SET time=" + TimeName(time) + " weather=" + WeatherName(weather) + (broadcast ? " (sent)" : " (rx)"));
    }

    public static void CycleWeather(int dir)
    {
        int w = WeatherOverride + dir;
        if (w > 1) w = -1;
        if (w < -1) w = 1;
        SetOverrides(TimeOverride, w, true);
    }

    public static void CycleTime(int dir)
    {
        int t = TimeOverride + dir;
        if (t > 9) t = -1;
        if (t < -1) t = 9;
        SetOverrides(t, WeatherOverride, true);
    }

    // Leaving the room ends the override (it was lobby-scoped).
    public static void ClearLocal()
    {
        if (!AnyActive) return;
        TimeOverride = -1;
        WeatherOverride = -1;
        var inst = FindObjectOfType<FalconEnv>();
        if (inst != null) inst.clearPending = true;
    }

    private static void ApplyNow()
    {
        var mgr = BetterDayNightManager.instance;
        if (mgr == null) return;
        try
        {
            // Round 88: RTX is removed entirely (Sans: scrapped,
            // to be redone later), so this driver is the only
            // environment driver again - overrides always go
            // through SetOverrideIndex, no RTX branch.
            if (TimeOverride >= 0)
            {
                // SetOverrideIndex also writes the weather slot
                // (= the phase number). When weather is on AUTO,
                // preserve whatever weather the clock had.
                int keepWeather = mgr.currentWeatherIndex;
                mgr.SetOverrideIndex(TimeOverride);
                if (WeatherOverride < 0) mgr.currentWeatherIndex = keepWeather;
            }
            if (WeatherOverride >= 0) ForceWeatherSlot(mgr, WeatherOverride == 1);
        }
        catch { }
    }

    private static void ForceWeatherSlot(BetterDayNightManager mgr, bool rain)
    {
        var cycle = mgr.weatherCycle;
        if (cycle == null || cycle.Length == 0) return;
        int cur = mgr.currentWeatherIndex;
        if (cur >= 0 && cur < cycle.Length && cycle[cur] != BetterDayNightManager.WeatherType.All
            && (cycle[cur] == BetterDayNightManager.WeatherType.Raining) == rain) return;
        for (int k = 1; k <= cycle.Length; k++)
        {
            int idx = (cur + k) % cycle.Length;
            var w = cycle[idx];
            if (w == BetterDayNightManager.WeatherType.All) continue;
            if ((w == BetterDayNightManager.WeatherType.Raining) == rain) { mgr.currentWeatherIndex = idx; return; }
        }
    }

    private void Update()
    {
        if (Time.time < nextTick) return;
        nextTick = Time.time + 1f;
        if (clearPending)
        {
            clearPending = false;
            var mgr = BetterDayNightManager.instance;
            if (mgr != null)
            {
                // The coroutine has already reclaimed the index
                // fields; repaint the clock's own pair at once.
                try { mgr.SetOverrideIndex(mgr.currentTimeIndex); } catch { }
            }
            return;
        }
        if (AnyActive) ApplyNow();
    }
}
