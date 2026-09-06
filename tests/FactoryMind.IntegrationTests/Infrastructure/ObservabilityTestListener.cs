using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using FactoryMind.Shared.Observability;

namespace FactoryMind.IntegrationTests.Infrastructure;

internal sealed class ObservabilityTestListener : IDisposable {
    private readonly MeterListener _meterListener = new();
    private readonly ActivityListener _activityListener;
    private readonly ConcurrentBag<ObservabilityMeasurement> _measurements = [];
    private readonly ConcurrentBag<ObservabilityActivity> _activities = [];

    public ObservabilityTestListener() {
        _meterListener.InstrumentPublished = (instrument, listener) => {
            if (instrument.Meter.Name == FactoryMindTelemetry.MeterName) {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            Capture(instrument, value, tags));
        _meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            Capture(instrument, value, tags));
        _meterListener.Start();
        _activityListener = new ActivityListener {
            ShouldListenTo = source => source.Name == FactoryMindTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _activities.Add(new ObservabilityActivity(
                activity.OperationName,
                activity.SpanId,
                activity.ParentSpanId,
                activity.Tags.ToDictionary(tag => tag.Key, tag => tag.Value)))
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    public IReadOnlyList<ObservabilityMeasurement> Measurements => _measurements.ToList();
    public IReadOnlyList<ObservabilityActivity> Activities => _activities.ToList();

    public void Dispose() {
        _meterListener.Dispose();
        _activityListener.Dispose();
    }

    private void Capture<T>(
        Instrument instrument,
        T value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags) where T : struct {
        _measurements.Add(new ObservabilityMeasurement(
            instrument.Name,
            Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture),
            tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
    }
}

internal sealed record ObservabilityMeasurement(
    string Name,
    double Value,
    IReadOnlyDictionary<string, object?> Tags);

internal sealed record ObservabilityActivity(
    string Name,
    ActivitySpanId SpanId,
    ActivitySpanId ParentSpanId,
    IReadOnlyDictionary<string, string?> Tags);
