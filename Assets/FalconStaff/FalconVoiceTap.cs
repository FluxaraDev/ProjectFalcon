using System;
using System.Collections.Generic;
using UnityEngine;

// Project Falcon voice clips (round 29, Sans): reports should let staff
// LISTEN to what led up to the report, not just see who was reported.
// Every remote player's voice speaker gets one of these taps; it keeps
// a rolling 25-second ring buffer of that player's decoded voice
// (mono, decimated to ~16 kHz). Audio passes through untouched - the
// tap only reads. On a report, FalconStaffWebhook.BuildClipWav mixes
// every buffer onto one timeline and uploads the WAV.
public class FalconVoiceTap : MonoBehaviour
{
    public class Buffer
    {
        public float[] ring;
        public int writePos;
        public long totalWritten;
        public double lastWriteDsp;
        public readonly object gate = new object();
    }

    public const int ClipSeconds = 25;
    private static readonly Dictionary<int, Buffer> buffers = new Dictionary<int, Buffer>();
    private static int decim = -1;

    public static int StoreRate
    {
        get { int r = AudioSettings.outputSampleRate; return r / Decim; }
    }

    private static int Decim
    {
        get
        {
            if (decim < 0) decim = Mathf.Max(1, Mathf.RoundToInt(AudioSettings.outputSampleRate / 16000f));
            return decim;
        }
    }

    private int actor;
    private bool bound;

    public void Bind(int actorNumber)
    {
        actor = actorNumber;
        bound = true;
        EnsureBuffer(actorNumber);
    }

    private static Buffer EnsureBuffer(int a)
    {
        Buffer b;
        if (!buffers.TryGetValue(a, out b))
        {
            b = new Buffer { ring = new float[StoreRate * ClipSeconds] };
            buffers[a] = b;
        }
        return b;
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (!bound) return;
        Buffer b;
        if (!buffers.TryGetValue(actor, out b)) return;
        int d = Decim;
        int frames = data.Length / channels;
        lock (b.gate)
        {
            for (int i = 0; i < frames; i += d)
            {
                float m = 0f;
                for (int c = 0; c < channels; c++) m += data[i * channels + c];
                b.ring[b.writePos] = m / channels;
                b.writePos = (b.writePos + 1) % b.ring.Length;
                b.totalWritten++;
            }
            b.lastWriteDsp = AudioSettings.dspTime;
        }
    }

    // Mix every player's buffer onto one 25 s timeline ending now and
    // encode 16-bit PCM WAV. Null when nobody actually said anything.
    public static byte[] BuildClipWav()
    {
        int rate = StoreRate;
        int len = rate * ClipSeconds;
        float[] mix = new float[len];
        double now = AudioSettings.dspTime;
        foreach (KeyValuePair<int, Buffer> kv in buffers)
        {
            Buffer b = kv.Value;
            lock (b.gate)
            {
                long avail = Math.Min(b.totalWritten, (long)b.ring.Length);
                if (avail <= 0) continue;
                int endPos = len - (int)((now - b.lastWriteDsp) * rate);
                for (long i = 0; i < avail; i++)
                {
                    int dest = endPos - (int)avail + (int)i;
                    if (dest < 0 || dest >= len) continue;
                    int src = (int)((b.writePos - avail + i) % b.ring.Length);
                    if (src < 0) src += b.ring.Length;
                    mix[dest] += b.ring[src];
                }
            }
        }
        float peak = 0f;
        for (int i = 0; i < len; i++) peak = Mathf.Max(peak, Mathf.Abs(mix[i]));
        if (peak < 0.001f) return null;
        float gain = peak > 0.9f ? 0.9f / peak : 1f;
        byte[] wav = new byte[44 + len * 2];
        WriteStr(wav, 0, "RIFF"); WriteI32(wav, 4, 36 + len * 2); WriteStr(wav, 8, "WAVE");
        WriteStr(wav, 12, "fmt "); WriteI32(wav, 16, 16); WriteI16(wav, 20, 1); WriteI16(wav, 22, 1);
        WriteI32(wav, 24, rate); WriteI32(wav, 28, rate * 2); WriteI16(wav, 32, 2); WriteI16(wav, 34, 16);
        WriteStr(wav, 36, "data"); WriteI32(wav, 40, len * 2);
        for (int i = 0; i < len; i++)
        {
            short smp = (short)(Mathf.Clamp(mix[i] * gain, -1f, 1f) * 32767f);
            wav[44 + i * 2] = (byte)(smp & 0xff);
            wav[45 + i * 2] = (byte)((smp >> 8) & 0xff);
        }
        return wav;
    }

    private static void WriteStr(byte[] b, int o, string s) { for (int i = 0; i < s.Length; i++) b[o + i] = (byte)s[i]; }
    private static void WriteI32(byte[] b, int o, int v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); b[o + 2] = (byte)(v >> 16); b[o + 3] = (byte)(v >> 24); }
    private static void WriteI16(byte[] b, int o, int v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); }
}
