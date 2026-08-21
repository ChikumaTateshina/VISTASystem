using VISTASystem.Interop;

namespace VISTASystem.Monitoring;

/// <summary>
/// 前面アプリの変化を定期的に検出し、登録済みのマッピングを通知する。
/// UI や VRChat API には依存しない。
/// </summary>
internal sealed class ActiveApplicationMonitor
{
    private readonly TimeSpan _pollInterval;
    private readonly Func<string?> _getActiveProcessName;

    public ActiveApplicationMonitor(TimeSpan pollInterval)
        : this(pollInterval, ActiveWindowDetector.GetActiveProcessName)
    {
    }

    internal ActiveApplicationMonitor(TimeSpan pollInterval, Func<string?> getActiveProcessName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
        _pollInterval = pollInterval;
        _getActiveProcessName = getActiveProcessName;
    }

    public async Task RunAsync(
        IReadOnlyDictionary<string, StatusMapping> mappings,
        Func<string, StatusMapping, CancellationToken, Task> onMatch,
        CancellationToken cancellationToken)
    {
        string? lastProcess = null;
        using var timer = new PeriodicTimer(_pollInterval);

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? current = _getActiveProcessName();
            if (!string.IsNullOrWhiteSpace(current)
                && !string.Equals(current, lastProcess, StringComparison.OrdinalIgnoreCase))
            {
                lastProcess = current;
                if (mappings.TryGetValue(current, out var mapping))
                    await onMatch(current, mapping, cancellationToken).ConfigureAwait(false);
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
    }
}
