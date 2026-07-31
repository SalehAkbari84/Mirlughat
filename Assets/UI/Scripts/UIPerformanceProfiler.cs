using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

/// <summary>
/// نسخه بهبودیافته — GC spike ها رو فریم به فریم لاگ می‌زنه
/// تا بفهمیم دقیقاً کدوم فریم و چرا allocation سنگین داره.
/// </summary>
public class UIPerformanceProfiler : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int   logEveryNFrames     = 60;
    [SerializeField] private float gcWarnThresholdKB   = 100f;  // اگه بیشتر از این KB بود warning بزنه
    [SerializeField] private float frameWarnThresholdMs = 20f;  // اگه فریم از این ms بیشتر شد warning

    private ProfilerRecorder _gcAllocRecorder;
    private ProfilerRecorder _mainThreadRecorder;
    private ProfilerRecorder _layoutRecorder;
    private ProfilerRecorder _renderRecorder;

    private float _worstFrame;
    private long  _worstGC;
    private int   _gcSpikeCount;

    private void OnEnable()
    {
        _gcAllocRecorder    = ProfilerRecorder.StartNew(ProfilerCategory.Memory,   "GC.Alloc");
        _mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
        _layoutRecorder     = ProfilerRecorder.StartNew(ProfilerCategory.Render,   "UI.Layout");
        _renderRecorder     = ProfilerRecorder.StartNew(ProfilerCategory.Render,   "UI.Render");
    }

    private void OnDisable()
    {
        _gcAllocRecorder.Dispose();
        _mainThreadRecorder.Dispose();
        _layoutRecorder.Dispose();
        _renderRecorder.Dispose();
    }

    private void Update()
    {
        long  gcBytes   = _gcAllocRecorder.LastValue;
        float gcKB      = gcBytes / 1024f;
        float frameMs   = GetAvgFrameTime();
        float layoutMs  = _layoutRecorder.LastValue  / 1_000_000f;
        float renderMs  = _renderRecorder.LastValue  / 1_000_000f;

        if (frameMs > _worstFrame) _worstFrame = frameMs;
        if (gcBytes > _worstGC)   _worstGC    = gcBytes;

        // فریم‌هایی که GC بالاست رو فوری لاگ بزن
        if (gcKB > gcWarnThresholdKB)
        {
            _gcSpikeCount++;
            Debug.LogWarning(
                $"[UIProfiler] 🔴 GC SPIKE  frame={Time.frameCount}  " +
                $"gc={gcKB:F1} KB  frameTime={frameMs:F2}ms"
            );
        }

        // فریم‌هایی که کند هستن
        if (frameMs > frameWarnThresholdMs)
        {
            Debug.LogWarning(
                $"[UIProfiler] 🟡 SLOW FRAME  frame={Time.frameCount}  " +
                $"time={frameMs:F2}ms  gc={gcKB:F1} KB  " +
                $"layout={layoutMs:F2}ms  render={renderMs:F2}ms"
            );
        }

        // خلاصه هر N فریم
        if (Time.frameCount % logEveryNFrames == 0)
        {
            Debug.Log(
                $"[UIProfiler] ── frame {Time.frameCount} ──\n" +
                $"  Avg Frame : {frameMs:F2}ms  ({(frameMs > 0 ? 1000f/frameMs : 0):F0} FPS)\n" +
                $"  Worst Frame: {_worstFrame:F2}ms\n" +
                $"  GC/frame  : {gcKB:F1} KB   Worst GC: {_worstGC/1024f:F1} KB\n" +
                $"  GC Spikes : {_gcSpikeCount} (>{gcWarnThresholdKB}KB)\n" +
                $"  Layout    : {layoutMs:F2}ms   Render: {renderMs:F2}ms"
            );
            _worstFrame   = 0;
            _worstGC      = 0;
            _gcSpikeCount = 0;
        }
    }

    private float GetAvgFrameTime()
    {
        if (!_mainThreadRecorder.Valid || _mainThreadRecorder.Count == 0) return 0;
        long sum = 0;
        for (int i = 0; i < _mainThreadRecorder.Count; i++)
            sum += _mainThreadRecorder.GetSample(i).Value;
        return sum / (float)_mainThreadRecorder.Count / 1_000_000f;
    }
}