using System.Text;
using CharlieMJFocus.Application.Abstractions;

namespace CharlieMJFocus.Infrastructure.Storage;

/// <summary>Writes exported files into the user's export folder.</summary>
public sealed class FileExportStore(AppPaths paths) : IExportStore
{
    public string ExportDirectory => paths.ExportDirectory;

    public async Task<string> SaveTextAsync(string fileName, string content, CancellationToken cancellationToken = default)
    {
        // Strip any directory parts so a file name can never escape the export folder.
        var safeName = Path.GetFileName(fileName);
        var path = Path.Combine(paths.ExportDirectory, safeName);

        // A UTF-8 byte-order mark makes Excel read accented characters correctly.
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
        return path;
    }
}
