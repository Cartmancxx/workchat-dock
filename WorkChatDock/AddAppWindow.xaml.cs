using System.Windows;
using Microsoft.Win32;

namespace WorkChatDock;

public partial class AddAppWindow : Window
{
    public AddAppWindow()
    {
        InitializeComponent();
    }

    public string AppDisplayName => NameBox.Text.Trim();
    public string ExecutablePath => PathBox.Text.Trim();

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择聊天或办公软件",
            Filter = "Windows 程序 (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        PathBox.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            NameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AppDisplayName) || !File.Exists(ExecutablePath))
        {
            MessageBox.Show(this, "请填写名称并选择有效的 exe 文件。", "添加软件",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
