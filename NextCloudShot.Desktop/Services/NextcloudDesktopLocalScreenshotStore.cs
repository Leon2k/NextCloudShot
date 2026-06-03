using NextCloudShot.Core.Contracts;
using NextCloudShot.Core.Models;

namespace NextCloudShot.Desktop.Services;

public sealed class NextcloudDesktopLocalScreenshotStore : ILocalScreenshotStore
{
    private const FileAttributes PinnedAttribute = (FileAttributes)0x80000;
    private const FileAttributes UnpinnedAttribute = (FileAttributes)0x100000;

    public async Task<LocalScreenshotResult> SaveAsync(
        ScreenshotUpload upload,
        NextcloudConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        SyncFolder syncFolder = FindSyncFolder(settings)
            ?? throw new InvalidOperationException("Не найдена локальная папка Nextcloud для текущего аккаунта.");

        string localFolder = BuildLocalFolder(syncFolder, settings.UploadFolder);
        Directory.CreateDirectory(localFolder);

        string localPath = GetUniquePath(Path.Combine(localFolder, upload.FileName));
        await File.WriteAllBytesAsync(localPath, upload.Bytes, cancellationToken);
        TryMarkPinned(localPath);

        string remotePath = CombineRemote(settings.UploadFolder, Path.GetFileName(localPath));
        return new LocalScreenshotResult(localPath, remotePath, Path.GetFileName(localPath));
    }

    private static SyncFolder? FindSyncFolder(NextcloudConnectionSettings settings)
    {
        string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Nextcloud",
            "nextcloud.cfg");
        if (!File.Exists(configPath)) return null;

        Dictionary<string, string> values = ReadIniLikeConfig(configPath);
        string? accountPrefix = FindAccountPrefix(values, settings);
        IEnumerable<KeyValuePair<string, string>> localPaths = values
            .Where(pair => pair.Key.EndsWith("\\localPath", StringComparison.OrdinalIgnoreCase) &&
                           pair.Key.Contains("\\FoldersWithPlaceholders\\", StringComparison.OrdinalIgnoreCase));

        foreach (KeyValuePair<string, string> localPath in localPaths)
        {
            if (accountPrefix is not null &&
                !localPath.Key.StartsWith(accountPrefix + "\\", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string folderPrefix = localPath.Key[..^"\\localPath".Length];
            string targetPath = values.GetValueOrDefault(folderPrefix + "\\targetPath") ?? "/";
            string paused = values.GetValueOrDefault(folderPrefix + "\\paused") ?? "false";
            if (string.Equals(paused, "true", StringComparison.OrdinalIgnoreCase)) continue;
            if (!RemoteContains(targetPath, settings.UploadFolder)) continue;

            return new SyncFolder(Path.GetFullPath(localPath.Value), NormalizeRemote(targetPath));
        }

        return null;
    }

    private static Dictionary<string, string> ReadIniLikeConfig(string configPath)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in File.ReadLines(configPath))
        {
            int separator = line.IndexOf('=');
            if (separator <= 0) continue;
            values[line[..separator]] = line[(separator + 1)..];
        }

        return values;
    }

    private static string? FindAccountPrefix(IReadOnlyDictionary<string, string> values, NextcloudConnectionSettings settings)
    {
        foreach (KeyValuePair<string, string> value in values)
        {
            if (!value.Key.EndsWith("\\url", StringComparison.OrdinalIgnoreCase)) continue;
            if (!Uri.TryCreate(value.Value, UriKind.Absolute, out Uri? uri)) continue;
            if (!string.Equals(uri.Host, settings.ServerUri.Host, StringComparison.OrdinalIgnoreCase)) continue;

            string prefix = value.Key[..^"\\url".Length];
            string? user = values.GetValueOrDefault(prefix + "\\dav_user");
            if (user is null || string.Equals(user, settings.Username, StringComparison.OrdinalIgnoreCase))
            {
                return prefix;
            }
        }

        return null;
    }

    private static string BuildLocalFolder(SyncFolder syncFolder, string uploadFolder)
    {
        string relativeRemote = GetRelativeRemote(syncFolder.TargetPath, uploadFolder);
        string result = syncFolder.LocalPath;
        foreach (string part in relativeRemote.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            result = Path.Combine(result, part);
        }

        return result;
    }

    private static string GetRelativeRemote(string targetPath, string uploadFolder)
    {
        string target = NormalizeRemote(targetPath);
        string upload = NormalizeRemote(uploadFolder);
        if (target == "/") return upload.TrimStart('/');
        if (string.Equals(target, upload, StringComparison.OrdinalIgnoreCase)) return string.Empty;
        return upload[(target.Length + 1)..];
    }

    private static bool RemoteContains(string targetPath, string uploadFolder)
    {
        string target = NormalizeRemote(targetPath);
        string upload = NormalizeRemote(uploadFolder);
        return target == "/" ||
               string.Equals(target, upload, StringComparison.OrdinalIgnoreCase) ||
               upload.StartsWith(target + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string CombineRemote(string folder, string filename) =>
        $"{NormalizeRemote(folder).TrimEnd('/')}/{filename}";

    private static string NormalizeRemote(string path)
    {
        string normalized = "/" + path.Replace('\\', '/').Trim('/');
        return normalized == "//" ? "/" : normalized;
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path)) return path;

        string? directory = Path.GetDirectoryName(path);
        string stem = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        for (int index = 1; index < 10_000; index++)
        {
            string candidate = Path.Combine(directory ?? string.Empty, $"{stem} ({index}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }

        throw new IOException("Не удалось подобрать свободное имя файла для скриншота.");
    }

    private static void TryMarkPinned(string path)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            FileAttributes attributes = File.GetAttributes(path);
            attributes |= PinnedAttribute;
            attributes &= ~UnpinnedAttribute;
            File.SetAttributes(path, attributes);
        }
        catch
        {
            // Pinning is best-effort: the Nextcloud client still uploads the local file normally.
        }
    }

    private sealed record SyncFolder(string LocalPath, string TargetPath);
}
