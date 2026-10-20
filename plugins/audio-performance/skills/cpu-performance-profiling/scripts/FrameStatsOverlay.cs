using System;
using TMPro;
using Unity.Profiling;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FrameStatsOverlay : MonoBehaviour
{
    private const int MaxWindow = 300;
    private const double NearFullFrameRatio = 0.8;

    [SerializeField] private TMP_Text label;
    [SerializeField, Min(1f)] private float targetFrameRate = 60f;
    [SerializeField, Range(1, MaxWindow)] private int windowFrames = 60;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;
    [SerializeField] private bool developmentBuildsOnly = true;
    [SerializeField] private string[] scriptMarkers =
    {
        "BehaviourUpdate",
        "FixedBehaviourUpdate",
        "PreLateUpdate.ScriptRunBehaviourLateUpdate",
    };

    private readonly FrameTiming[] _latest = new FrameTiming[1];
    private readonly char[] _text = new char[1024];
    private readonly char[] _digits = new char[20];

    private double[] _cpuFrame;
    private double[] _cpuMain;
    private double[] _cpuRender;
    private double[] _gpu;
    private int _head;
    private int _filled;
    private int _length;
    private float _nextRefresh;

    private ProfilerRecorder _gcAllocBytes;
    private ProfilerRecorder _gcAllocCount;
    private ProfilerRecorder[] _markers = Array.Empty<ProfilerRecorder>();

    private void Awake()
    {
        if (developmentBuildsOnly && !Debug.isDebugBuild)
        {
            if (label != null)
            {
                label.enabled = false;
            }

            enabled = false;
            return;
        }

        _cpuFrame = new double[MaxWindow];
        _cpuMain = new double[MaxWindow];
        _cpuRender = new double[MaxWindow];
        _gpu = new double[MaxWindow];
    }

    private void OnEnable()
    {
        if (_cpuFrame == null)
        {
            return;
        }

        _head = 0;
        _filled = 0;
        _gcAllocBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", windowFrames);
        _gcAllocCount = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count", windowFrames);

        _markers = new ProfilerRecorder[scriptMarkers.Length];
        for (int i = 0; i < scriptMarkers.Length; i++)
        {
            _markers[i] = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, scriptMarkers[i], windowFrames);
        }
    }

    private void OnDisable()
    {
        _gcAllocBytes.Dispose();
        _gcAllocCount.Dispose();
        for (int i = 0; i < _markers.Length; i++)
        {
            _markers[i].Dispose();
        }

        _markers = Array.Empty<ProfilerRecorder>();
    }

    private void Update()
    {
        FrameTimingManager.CaptureFrameTimings();
        if (FrameTimingManager.GetLatestTimings(1, _latest) > 0)
        {
            int window = Mathf.Clamp(windowFrames, 1, MaxWindow);
            _head = (_head + 1) % window;
            _cpuFrame[_head] = _latest[0].cpuFrameTime;
            _cpuMain[_head] = _latest[0].cpuMainThreadFrameTime;
            _cpuRender[_head] = _latest[0].cpuRenderThreadFrameTime;
            _gpu[_head] = _latest[0].gpuFrameTime;
            _filled = Math.Min(_filled + 1, window);
        }

        if (Time.unscaledTime < _nextRefresh || label == null)
        {
            return;
        }

        _nextRefresh = Time.unscaledTime + refreshInterval;
        Compose();
        label.SetCharArray(_text, 0, _length);
    }

    private void Compose()
    {
        _length = 0;
        double budget = 1000.0 / targetFrameRate;
        double cpuFrame = Average(_cpuFrame, out _);
        double main = Average(_cpuMain, out double mainMax);
        double render = Average(_cpuRender, out double renderMax);
        double gpu = Average(_gpu, out double gpuMax);

        Append("Budget ");
        AppendFixed(budget, 2);
        Append(" ms @ ");
        AppendInt((long)targetFrameRate);
        Append(" fps\nFrame  ");
        AppendFixed(cpuFrame, 2);
        Append(" ms  (");
        AppendFixed(cpuFrame > 0.0 ? 1000.0 / cpuFrame : 0.0, 1);
        Append(" fps)\nMain   ");
        AppendTime(main, mainMax, budget);
        Append("\nRender ");
        AppendTime(render, renderMax, budget);
        Append("\nGPU    ");
        if (gpu > 0.0)
        {
            AppendTime(gpu, gpuMax, budget);
        }
        else
        {
            Append("n/a");
        }

        Append("\nBound  ");
        Append(Classify(cpuFrame, main, render, gpu));

        Append("\nGC     ");
        if (_gcAllocBytes.Valid && _gcAllocCount.Valid)
        {
            SampleStats(_gcAllocBytes, out double bytesAvg, out long bytesMax);
            SampleStats(_gcAllocCount, out double countAvg, out _);
            AppendInt((long)bytesAvg);
            Append(" B/frame avg, ");
            AppendInt(bytesMax);
            Append(" B max, ");
            AppendFixed(countAvg, 1);
            Append(" allocs/frame");
        }
        else
        {
            Append("n/a");
        }

        for (int i = 0; i < _markers.Length; i++)
        {
            Append("\n");
            Append(scriptMarkers[i]);
            Append("  ");
            if (_markers[i].Valid)
            {
                SampleStats(_markers[i], out double nsAvg, out long nsMax);
                AppendFixed(nsAvg * 1e-6, 2);
                Append(" ms avg, ");
                AppendFixed(nsMax * 1e-6, 2);
                Append(" max");
            }
            else
            {
                Append("n/a");
            }
        }
    }

    private string Classify(double cpuFrame, double main, double render, double gpu)
    {
        if (cpuFrame <= 0.0 || gpu <= 0.0)
        {
            return "unknown";
        }

        double nearFull = NearFullFrameRatio * cpuFrame;
        bool cpuNearFull = main > nearFull || render > nearFull;
        if (gpu > nearFull && !cpuNearFull)
        {
            return "GPU";
        }

        if (gpu < nearFull && cpuNearFull)
        {
            return main >= render ? "CPU main thread" : "CPU render thread";
        }

        if (!cpuNearFull && gpu < nearFull)
        {
            return "present / vsync limited";
        }

        return "balanced";
    }

    private double Average(double[] values, out double max)
    {
        max = 0.0;
        if (_filled == 0)
        {
            return 0.0;
        }

        double sum = 0.0;
        for (int i = 0; i < _filled; i++)
        {
            double v = values[i];
            sum += v;
            if (v > max)
            {
                max = v;
            }
        }

        return sum / _filled;
    }

    private static void SampleStats(ProfilerRecorder recorder, out double average, out long max)
    {
        average = 0.0;
        max = 0;
        int count = recorder.Count;
        if (count == 0)
        {
            return;
        }

        long sum = 0;
        for (int i = 0; i < count; i++)
        {
            long v = recorder.GetSample(i).Value;
            sum += v;
            if (v > max)
            {
                max = v;
            }
        }

        average = (double)sum / count;
    }

    private void AppendTime(double average, double max, double budget)
    {
        AppendFixed(average, 2);
        Append(" ms avg, ");
        AppendFixed(max, 2);
        Append(" max, ");
        AppendInt((long)Math.Round(average / budget * 100.0));
        Append("% budget");
    }

    private void Append(string value)
    {
        for (int i = 0; i < value.Length && _length < _text.Length; i++)
        {
            _text[_length++] = value[i];
        }
    }

    private void AppendChar(char c)
    {
        if (_length < _text.Length)
        {
            _text[_length++] = c;
        }
    }

    private void AppendInt(long value)
    {
        if (value < 0)
        {
            AppendChar('-');
            value = -value;
        }

        int count = 0;
        do
        {
            _digits[count++] = (char)('0' + (int)(value % 10));
            value /= 10;
        }
        while (value > 0 && count < _digits.Length);

        while (count > 0)
        {
            AppendChar(_digits[--count]);
        }
    }

    private void AppendFixed(double value, int decimals)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            Append("n/a");
            return;
        }

        long scale = 1;
        for (int i = 0; i < decimals; i++)
        {
            scale *= 10;
        }

        long scaled = (long)Math.Round(value * scale);
        if (scaled < 0)
        {
            AppendChar('-');
            scaled = -scaled;
        }

        AppendInt(scaled / scale);
        if (decimals == 0)
        {
            return;
        }

        AppendChar('.');
        long fraction = scaled % scale;
        for (long divisor = scale / 10; divisor > 0; divisor /= 10)
        {
            AppendChar((char)('0' + (int)(fraction / divisor % 10)));
        }
    }
}
