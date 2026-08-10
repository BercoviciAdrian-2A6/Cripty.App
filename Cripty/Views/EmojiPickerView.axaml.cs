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

        Populate(
            FacesPanel,
            Faces);

        Populate(
            GesturesPanel,
            Gestures);

        Populate(
            ObjectsPanel,
            Objects);
    }

    private void Populate(
        Panel panel,
        string[] emojis)
    {
        foreach (string emoji in emojis)
        {
            Button button = new()
            {
                Content = emoji
            };

            button.Classes.Add(
                "emoji-choice");

            button.Click += InsertEmoji;
            panel.Children.Add(button);
        }
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
