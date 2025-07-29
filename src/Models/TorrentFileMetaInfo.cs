using System.Text.Json.Serialization;

namespace codecrafters_bittorrent.src.Models;

public class TorrentFileMetaInfo
{
    [JsonPropertyName("announce")]
    public string Announce { get; set; }

    [JsonPropertyName("created by")]
    public string CreatedBy { get; set; }

    [JsonPropertyName("info")]
    public Info Info { get; set; }
}
public class Info
{
    [JsonPropertyName("length")]
    public int? Length { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("piece length")]
    public int? PieceLength { get; set; }

    [JsonPropertyName("pieces")]
    public string Pieces { get; set; }
}