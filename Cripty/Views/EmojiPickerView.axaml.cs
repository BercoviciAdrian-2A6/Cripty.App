using Avalonia.Controls;
using Avalonia.Interactivity;
using Cripty.ViewModels;

namespace Cripty.Views;

public partial class EmojiPickerView : UserControl
{
    private static readonly string[] Faces =
    [
        "😀", "😃", "😄", "😁", "😆", "😅",
        "😂", "🙂", "🙃", "😉", "😊", "😎",
        "🤔", "😐", "😕", "😢", "😭", "😡",
        "🤯", "🥳"
    ];

    private static readonly string[] Gestures =
    [
        "👍", "👎", "👌", "✌️", "🤞", "👏",
        "🙌", "👋", "🤝", "💪", "🙏", "❤️",
        "💔", "🔥", "⭐", "✅", "❌", "⚠️"
    ];

    private static readonly string[] Objects =
    [
        "🔒", "🔓", "🔑", "🛡️", "💻", "📱",
        "✉️", "📎", "📌", "📝", "💡", "🎯",
        "🚀", "🎉", "☕", "🌍", "⚙️", "🔔"
    ];

    public EmojiPickerView()
    {
        InitializeComponent();

        FacesItems.ItemsSource = Faces;
        GesturesItems.ItemsSource = Gestures;
        ObjectsItems.ItemsSource = Objects;
    }

    private void InsertEmoji(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        if (DataContext is not
                EntryTextFieldViewModel field ||
            sender is not Button
            {
                Content: string emoji
            })
        {
            return;
        }

        field.InsertTextAtCaret(
            emoji);
    }
}
