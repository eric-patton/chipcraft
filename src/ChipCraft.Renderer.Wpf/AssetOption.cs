using System.IO;

namespace ChipCraft.Renderer.Wpf;

public sealed record AssetOption(
    string FullPath,
    string DisplayName,
    string RelativeFolder,
    string LibraryName,
    DateTime LastWriteTimeUtc)
{
    public string LocationHint => string.IsNullOrWhiteSpace(RelativeFolder) ? LibraryName : RelativeFolder;

    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               LocationHint.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               FullPath.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    public static AssetOption Create(string fullPath, string libraryRoot, string libraryName)
    {
        string relativePath = Path.GetRelativePath(libraryRoot, fullPath);
        string? relativeFolder = Path.GetDirectoryName(relativePath);
        if (string.Equals(relativeFolder, ".", StringComparison.Ordinal))
            relativeFolder = string.Empty;

        return new AssetOption(
            Path.GetFullPath(fullPath),
            Path.GetFileName(fullPath),
            relativeFolder ?? string.Empty,
            libraryName,
            File.GetLastWriteTimeUtc(fullPath));
    }
}
