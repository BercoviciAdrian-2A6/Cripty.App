using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Cripty.Services;

internal sealed class VaultInactivityService : IDisposable
{
    public static readonly TimeSpan DefaultTimeout =
        TimeSpan.FromMinutes(5);

    private readonly TimeSpan _timeout;
    private readonly Action<VaultInactivityEvaluation?>
        _warningChanged;
    private readonly Func<Task> _timeoutAction;
    private readonly DispatcherTimer _timer;

    private long _lastInteractionTimestamp;
    private bool _isMonitoring;
    private bool _isTimingOut;
    private bool _isWarningVisible;
    private bool _disposed;

    public VaultInactivityService(
        Action<VaultInactivityEvaluation?> warningChanged,
        Func<Task> timeoutAction,
        TimeSpan? timeout = null)
    {
        _warningChanged = warningChanged ??
            throw new ArgumentNullException(
                nameof(warningChanged));

        _timeoutAction = timeoutAction ??
            throw new ArgumentNullException(
                nameof(timeoutAction));

        _timeout = timeout ?? DefaultTimeout;

        _ = VaultInactivityPolicy.Evaluate(
            TimeSpan.Zero,
            _timeout);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _timer.Tick += HandleTimerTick;
        CriptyInteraction.Occurred += HandleInteraction;
    }

    public void Start()
    {
        ThrowIfDisposed();

        _lastInteractionTimestamp =
            Stopwatch.GetTimestamp();

        _isMonitoring = true;
        _isTimingOut = false;
        HideWarning();
        _timer.Start();
    }

    public void Stop()
    {
        if (_disposed)
            return;

        _isMonitoring = false;
        _isTimingOut = false;
        _timer.Stop();
        HideWarning();
    }

    public void RecordInteraction()
    {
        if (!_isMonitoring ||
            _isTimingOut ||
            _disposed)
        {
            return;
        }

        _lastInteractionTimestamp =
            Stopwatch.GetTimestamp();

        HideWarning();
    }

    private void HandleInteraction(
        object? sender,
        EventArgs eventArgs)
    {
        RecordInteraction();
    }

    private async void HandleTimerTick(
        object? sender,
        EventArgs eventArgs)
    {
        if (!_isMonitoring ||
            _isTimingOut ||
            _disposed)
        {
            return;
        }

        VaultInactivityEvaluation evaluation =
            VaultInactivityPolicy.Evaluate(
                Stopwatch.GetElapsedTime(
                    _lastInteractionTimestamp),
                _timeout);

        if (evaluation.IsExpired)
        {
            _isMonitoring = false;
            _isTimingOut = true;
            _timer.Stop();

            _warningChanged(evaluation);

            try
            {
                await _timeoutAction();
            }
            finally
            {
                _isTimingOut = false;
            }

            return;
        }

        if (evaluation.ShouldWarn)
        {
            _isWarningVisible = true;
            _warningChanged(evaluation);
        }
        else
        {
            HideWarning();
        }
    }

    private void HideWarning()
    {
        if (!_isWarningVisible)
            return;

        _isWarningVisible = false;
        _warningChanged(null);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;

        _timer.Tick -= HandleTimerTick;
        CriptyInteraction.Occurred -= HandleInteraction;
    }
}
