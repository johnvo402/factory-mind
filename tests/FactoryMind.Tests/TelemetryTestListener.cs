using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using FactoryMind.Shared.Observability;

namespace FactoryMind.Tests;

internal sealed class TelemetryTestListener : IDisposable {
    private readonly MeterListener _meterListener = new();
    private readonly ActivityListener _activityListener;
    private readonly ConcurrentBag<TelemetryMeasurement> _measurements = [];
    private readonly ConcurrentBag<ActivitySnapshot> _activities = [];

    public TelemetryTestListener() {
        _meterListener.InstrumentPublished = (instrument, listener) => {
            if (instrument.Meter.Name == FactoryMindTelemetry.MeterName) {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            AddMeasurement(instrument, measurement, tags));
        _meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            AddMeasurement(instrument, measurement, tags));
        _meterListener.Start();

        _activityListener = new ActivityListener {
            ShouldListenTo = source => source.Name == FactoryMindTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _activities.Add(new ActivitySnapshot(
                activity.OperationName,
                activity.TraceId,
                activity.SpanId,
                activity.ParentSpanId,
                activity.Tags.ToDictionary(tag => tag.Key, tag => tag.Value)))
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    public IReadOnlyList<TelemetryMeasurement> Measurements => _measurements.ToList();
    public IReadOnlyList<ActivitySnapshot> Activities => _activities.ToList();

    public void Dispose() {
        _meterListener.Dispose();
        _activityListener.Dispose();
    }

    private void AddMeasurement<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags) where T : struct {
        _measurements.Add(new TelemetryMeasurement(
            instrument.Name,
            Convert.ToDouble(measurement, System.Globalization.CultureInfo.InvariantCulture),
            tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
    }
}

internal sealed record TelemetryMeasurement(
    string Name,
    double Value,
    IReadOnlyDictionary<string, object?> Tags) {
    public bool HasTags(params (string Key, string Value)[] expected) => expected.All(value =>
        Tags.TryGetValue(value.Key, out var actual)
        && string.Equals(Convert.ToString(actual), value.Value, StringComparison.Ordinal));
}

internal sealed record ActivitySnapshot(
    string Name,
    ActivityTraceId TraceId,
    ActivitySpanId SpanId,
    ActivitySpanId ParentSpanId,
    IReadOnlyDictionary<string, string?> Tags);
