using DeepClaudeAuto.Core.Services;

namespace DeepClaudeAuto.Services;

public sealed class WinFormsFolderBrowserService : IFolderBrowserService
{
    public string? BrowseForFolder(string title, string initialPath)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = title,
            SelectedPath = initialPath,
            UseDescriptionForTitle = true
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }
}
