using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace BCH.TextureTool.Avalonia
{
    /// <summary>
    /// Simple modal text-input dialog. Replaces WinForms Interaction.InputBox.
    /// </summary>
    public class InputDialog : Window
    {
        private readonly TextBox _textBox;

        public InputDialog(string title, string defaultValue = "")
        {
            Title = title;
            Width = 320;
            Height = 130;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            _textBox = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(10, 10, 10, 6),
                SelectionStart = 0,
                SelectionEnd = defaultValue.Length
            };

            var ok = new Button { Content = "OK", Width = 80, Margin = new Thickness(4) };
            var cancel = new Button { Content = "Cancel", Width = 80, Margin = new Thickness(4) };

            ok.Click += (_, _) => Close(_textBox.Text?.Replace(" ", "") ?? "");
            cancel.Click += (_, _) => Close(null);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(4),
                Children = { ok, cancel }
            };

            Content = new StackPanel { Children = { _textBox, buttons } };
        }

        public static async System.Threading.Tasks.Task<string?> ShowAsync(Window parent, string title, string defaultValue = "")
        {
            var dialog = new InputDialog(title, defaultValue);
            return await dialog.ShowDialog<string?>(parent);
        }
    }
}
