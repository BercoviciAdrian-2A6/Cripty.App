using System;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Cripty.Services;

internal static class CriptyInteraction
{
    public static event EventHandler? Occurred;

    public static void Attach(InputElement inputRoot)
    {
        ArgumentNullException.ThrowIfNull(inputRoot);

        RoutingStrategies routes =
            RoutingStrategies.Tunnel |
            RoutingStrategies.Bubble;

        inputRoot.AddHandler(
            InputElement.KeyDownEvent,
            ReportKeyInteraction,
            routes,
            handledEventsToo: true);

        inputRoot.AddHandler(
            InputElement.TextInputEvent,
            ReportTextInteraction,
            routes,
            handledEventsToo: true);

        inputRoot.AddHandler(
            InputElement.PointerPressedEvent,
            ReportPointerPressedInteraction,
            routes,
            handledEventsToo: true);

        inputRoot.AddHandler(
            InputElement.PointerMovedEvent,
            ReportPointerInteraction,
            routes,
            handledEventsToo: true);

        inputRoot.AddHandler(
            InputElement.PointerWheelChangedEvent,
            ReportPointerWheelInteraction,
            routes,
            handledEventsToo: true);
    }

    public static void Report()
    {
        Occurred?.Invoke(
            sender: null,
            EventArgs.Empty);
    }

    private static void ReportKeyInteraction(
        object? sender,
        KeyEventArgs eventArgs)
    {
        Report();
    }

    private static void ReportPointerInteraction(
        object? sender,
        PointerEventArgs eventArgs)
    {
        Report();
    }

    private static void ReportTextInteraction(
        object? sender,
        TextInputEventArgs eventArgs)
    {
        Report();
    }

    private static void ReportPointerPressedInteraction(
        object? sender,
        PointerPressedEventArgs eventArgs)
    {
        Report();
    }

    private static void ReportPointerWheelInteraction(
        object? sender,
        PointerWheelEventArgs eventArgs)
    {
        Report();
    }
}
