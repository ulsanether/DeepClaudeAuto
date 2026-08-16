using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;

namespace DeepClaudeAuto.UI.Views.Steps;

public partial class Step06_ServerView : UserControl
{
    public Step06_ServerView() => InitializeComponent();

    private void OnAddressClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBlock tb && !string.IsNullOrEmpty(tb.Text))
            Process.Start(new ProcessStartInfo { FileName = tb.Text, UseShellExecute = true });
    }
}
