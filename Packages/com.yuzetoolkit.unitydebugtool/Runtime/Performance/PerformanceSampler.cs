#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace YuzeToolkit
{
    internal sealed class PerformanceSampler
    {
        private readonly Queue<float> _fpsSamples = new();
        private readonly Queue<float> _reservedSamples = new();
        private readonly Queue<float> _allocatedSamples = new();
        private readonly Queue<float> _monoSamples = new();
        private readonly float[] _spectrum = new float[PerformanceHudConstants.SpectrumSamples];
        private float _metricsTimer;

        public PerformanceUpdate Tick(float unscaledDeltaTime)
        {
            var delta = Mathf.Max(unscaledDeltaTime, 0.000001f);
            var fps = 1f / delta;
            EnqueueSample(_fpsSamples, fps);

            _metricsTimer += unscaledDeltaTime;

            PerformanceMetricsSnapshot? metrics = null;

            if (_metricsTimer >= 0.25f)
            {
                _metricsTimer = 0f;
                metrics = new PerformanceMetricsSnapshot(
                    CaptureFps(delta, fps),
                    CaptureRam(),
                    CaptureAudio());
            }

            return new PerformanceUpdate(metrics);
        }

        public void Reset()
        {
            _fpsSamples.Clear();
            _reservedSamples.Clear();
            _allocatedSamples.Clear();
            _monoSamples.Clear();
            _metricsTimer = 0f;
            Array.Clear(_spectrum, 0, _spectrum.Length);
        }

        private FpsSnapshot CaptureFps(float delta, float fps)
        {
            var values = _fpsSamples.ToArray();
            Array.Sort(values);

            var average = 0f;
            for (var i = 0; i < values.Length; i++)
                average += values[i];
            if (values.Length > 0)
                average /= values.Length;

            return new FpsSnapshot(
                fps,
                delta * 1000f,
                average,
                LowAverage(values, 0.01f),
                LowAverage(values, 0.001f),
                _fpsSamples.ToArray());
        }

        private RamSnapshot CaptureRam()
        {
            var allocated = Profiler.GetTotalAllocatedMemoryLong() / 1048576f;
            var reserved = Profiler.GetTotalReservedMemoryLong() / 1048576f;
            var mono = Profiler.GetMonoUsedSizeLong() / 1048576f;

            EnqueueSample(_allocatedSamples, allocated);
            EnqueueSample(_reservedSamples, reserved);
            EnqueueSample(_monoSamples, mono);

            return new RamSnapshot(
                reserved,
                allocated,
                mono,
                _reservedSamples.ToArray(),
                _allocatedSamples.ToArray(),
                _monoSamples.ToArray());
        }

        private AudioSnapshot CaptureAudio()
        {
            AudioListener.GetSpectrumData(_spectrum, 0, FFTWindow.Blackman);

            var highest = 0f;
            for (var i = 0; i < _spectrum.Length; i++)
                highest = Mathf.Max(highest, _spectrum[i]);

            float? decibels = null;
            if (highest > 0.000001f)
                decibels = Mathf.Clamp(20f * Mathf.Log10(highest), -80f, 0f);

            var samples = new float[PerformanceHudConstants.GraphSamples];
            for (var i = 0; i < samples.Length; i++)
            {
                var index = Mathf.Clamp(
                    Mathf.RoundToInt(i / (float)(samples.Length - 1) * (_spectrum.Length - 1)),
                    0,
                    _spectrum.Length - 1);
                samples[i] = Mathf.Sqrt(Mathf.Clamp01(_spectrum[index] * 80f));
            }

            return new AudioSnapshot(decibels, samples);
        }

        private static void EnqueueSample(Queue<float> queue, float value)
        {
            queue.Enqueue(value);
            while (queue.Count > PerformanceHudConstants.SampleCapacity)
                queue.Dequeue();
        }

        private static float LowAverage(float[] sortedValues, float ratio)
        {
            if (sortedValues.Length == 0) return 0f;
            var count = Mathf.Clamp(Mathf.CeilToInt(sortedValues.Length * ratio), 1, sortedValues.Length);
            var total = 0f;
            for (var i = 0; i < count; i++)
                total += sortedValues[i];
            return total / count;
        }

    }
}
