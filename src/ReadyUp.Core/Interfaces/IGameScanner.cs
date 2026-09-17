using ReadyUp.Core.Models;

namespace ReadyUp.Core.Interfaces;

/// <summary>One source of candidate games (a drive walk, the registry, a specific launcher's library, ...).</summary>
public interface IGameScanner
{
    string Name { get; }

    IAsyncEnumerable<ScanResult> ScanAsync(CancellationToken ct = default);
}

/// <summary>Lets a user point directly at an executable or install folder to add a game manually.</summary>
public interface IManualGameAdder
{
    ScanResult FromExecutable(string executablePath);
    ScanResult? FromDirectory(string directoryPath);
}
