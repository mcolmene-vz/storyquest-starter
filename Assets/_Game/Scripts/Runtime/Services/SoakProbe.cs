#if SQ_PROBE || UNITY_EDITOR
using System.Globalization;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace Game.Runtime
{
    /// The soak probe of ARCHITECTURE §15.8. Every 30 s it writes one console line that the saved devicectl log is
    /// judged by (STATUS, producer ruling 2026-10-16 item 4). It sends nothing anywhere. It exists only in SQ_PROBE
    /// builds, plus the editor so the compile gate sees it; Services.Create adds it only under SQ_PROBE.
    public sealed class SoakProbe : MonoBehaviour
    {
        const float WindowSeconds = 30f;
        // The minimum is the slowest whole second, like Xcode's FPS gauge, so a single hitch does not read as 30 fps.
        const float BucketSeconds = 1f;
        const double BytesPerMB = 1024d * 1024d;

        readonly FrameTiming[] timings = new FrameTiming[1];
        readonly StringBuilder line = new StringBuilder(192);
        ulong lastFrameStart;

        float windowTime;
        int windowFrames;
        float bucketTime;
        int bucketFrames;
        float minFps;
        double cpuSum;
        int cpuSamples;
        int groupFrames;
        double gpuSum;
        double gpuMax;
        int gpuSamples;

        // Render counters exist in development players only; they stand in for the Frame Debugger (PLAN-M2 readings).
        bool counters;
        ProfilerRecorder drawCalls;
        ProfilerRecorder setPassCalls;
        ProfilerRecorder triangles;
        long drawMax;
        long setPassMax;
        long trianglesMax;

        void Awake()
        {
            counters = Debug.isDebugBuild;
            if (counters)
            {
                drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            }
            ResetWindow();
        }

        void OnDestroy()
        {
            if (!counters)
                return;
            drawCalls.Dispose();
            setPassCalls.Dispose();
            triangles.Dispose();
        }

        void Update()
        {
            // Unscaled: the hit-stop sets timeScale to 0 and those frames still count.
            float dt = Time.unscaledDeltaTime;
            windowTime += dt;
            windowFrames++;
            bucketTime += dt;
            bucketFrames++;
            if (bucketTime >= BucketSeconds)
            {
                float fps = bucketFrames / bucketTime;
                if (fps < minFps)
                    minFps = fps;
                bucketTime = 0f;
                bucketFrames = 0;
            }

            bool group = BattleCameras.GroupShotLive;
            if (group)
                groupFrames++;

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].frameStartTimestamp != lastFrameStart)
            {
                var timing = timings[0];
                lastFrameStart = timing.frameStartTimestamp;
                cpuSum += timing.cpuMainThreadFrameTime;
                cpuSamples++;
                if (group && timing.gpuFrameTime > 0d)
                {
                    gpuSum += timing.gpuFrameTime;
                    if (timing.gpuFrameTime > gpuMax)
                        gpuMax = timing.gpuFrameTime;
                    gpuSamples++;
                }
            }

            if (counters && group)
            {
                if (drawCalls.LastValue > drawMax) drawMax = drawCalls.LastValue;
                if (setPassCalls.LastValue > setPassMax) setPassMax = setPassCalls.LastValue;
                if (triangles.LastValue > trianglesMax) trianglesMax = triangles.LastValue;
            }

            if (windowTime >= WindowSeconds)
            {
                Write();
                ResetWindow();
            }
        }

        // Invariant culture: a device set to a comma-decimal language must still print "59.8" for the thresholds.
        void Write()
        {
            var inv = CultureInfo.InvariantCulture;
            float avgFps = windowFrames / windowTime;
            float lowFps = minFps == float.MaxValue ? avgFps : minFps;
            line.Clear();
            line.AppendFormat(inv, "[Probe] t={0:F0}s flow={1} fps avg {2:F1} min {3:F1} | gpu group ",
                Time.realtimeSinceStartup, FlowController.Current, avgFps, lowFps);
            if (groupFrames == 0)
                line.Append('-');
            else if (gpuSamples == 0)
                line.Append("avg 0 max 0 ms");
            else
                line.AppendFormat(inv, "avg {0:F1} max {1:F1} ms", gpuSum / gpuSamples, gpuMax);
            if (cpuSamples == 0)
                line.Append(" | cpu -");
            else
                line.AppendFormat(inv, " | cpu {0:F1} ms", cpuSum / cpuSamples);
            line.AppendFormat(inv, " | mem {0:F1} MB", Profiler.GetTotalReservedMemoryLong() / BytesPerMB);
            if (counters)
            {
                if (groupFrames == 0)
                    line.Append(" | group draws - setpass - tris -");
                else
                    line.AppendFormat(inv, " | group draws {0} setpass {1} tris {2}", drawMax, setPassMax, trianglesMax);
            }
            Debug.Log(line.ToString());
        }

        void ResetWindow()
        {
            windowTime = 0f;
            windowFrames = 0;
            bucketTime = 0f;
            bucketFrames = 0;
            minFps = float.MaxValue;
            cpuSum = 0d;
            cpuSamples = 0;
            groupFrames = 0;
            gpuSum = 0d;
            gpuMax = 0d;
            gpuSamples = 0;
            drawMax = 0;
            setPassMax = 0;
            trianglesMax = 0;
        }
    }
}
#endif
