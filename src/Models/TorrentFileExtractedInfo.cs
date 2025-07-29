using System.Text;

namespace codecrafters_bittorrent.src.Models;

public class TorrentFileExtractedInfo
{
    public string TrackerUrl { get; set; } = string.Empty;
    public long Length { get; set; }
    public string InfoHashHex { get; set; } = string.Empty;
    public int PieceLength { get; set; }
    public List<string> PieceHashes { get; set; } = new();
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Tracker URL: {TrackerUrl}");
        sb.AppendLine($"Length: {Length}");
        sb.AppendLine($"Info Hash: {InfoHashHex}");
        sb.AppendLine($"Piece Length: {PieceLength}");
        sb.AppendLine("Piece Hashes:");
        sb.AppendLine(string.Join("\n", PieceHashes));
        return sb.ToString();
    }
}
