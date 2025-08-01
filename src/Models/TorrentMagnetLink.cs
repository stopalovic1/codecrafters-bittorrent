namespace codecrafters_bittorrent.src.Models;

public class TorrentMagnetLink
{
    public string InfoHashHex { get; set; } = string.Empty;
    public string TrackerUrl { get; set; } = string.Empty;
    public string DownloadName { get; set; } = string.Empty;
}
