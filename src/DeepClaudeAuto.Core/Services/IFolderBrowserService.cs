namespace DeepClaudeAuto.Core.Services;

public interface IFolderBrowserService
{
    string? BrowseForFolder(string title, string initialPath);
}
