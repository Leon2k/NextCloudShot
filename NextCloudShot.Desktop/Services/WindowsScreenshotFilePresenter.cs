using System.Diagnostics;
using NextCloudShot.Core.Contracts;
using NextCloudShot.Core.Models;

namespace NextCloudShot.Desktop.Services;

public sealed class WindowsScreenshotFilePresenter : IScreenshotFilePresenter
{
    public Task ShowInFolderAsync(LocalScreenshotResult result, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) return Task.CompletedTask;
        if (!File.Exists(result.LocalPath)) return Task.CompletedTask;

        ProcessStartInfo startInfo = new()
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{result.LocalPath}\"",
            UseShellExecute = true
        };
        Process.Start(startInfo);
        return Task.CompletedTask;
    }
}
