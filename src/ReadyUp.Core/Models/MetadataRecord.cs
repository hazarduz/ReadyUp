namespace ReadyUp.Core.Models;

/// <summary>
/// A provider's proposed values for one game, prior to merge. Fields left
/// null were simply not supplied by that provider (not "should be cleared").
/// </summary>
public sealed class MetadataRecord
{
    public required string ProviderName { get; init; }

    /// <summary>0.0-1.0 confidence used by the aggregator to break ties between providers.</summary>
    public double Confidence { get; init; } = 0.5;

    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Publisher { get; set; }
    public string? Developer { get; set; }
    public List<string>? Tags { get; set; }

    public List<ArtCandidate>? ArtCandidates { get; set; }
}
