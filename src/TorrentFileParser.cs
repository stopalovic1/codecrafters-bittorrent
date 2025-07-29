using codecrafters_bittorrent.src.Models;
using System.Text;
using System.Text.Json;

namespace codecrafters_bittorrent.src;

public static class TorrentFileParser
{
    public static async Task<TorrentFileMetaInfo?> GetTorrentFileMetaInfoAsync(string path)
    {
        var torrentFile = await File.ReadAllBytesAsync(path);
        var (decodedValue, _) = Bencoding.Decode(torrentFile, 0);
        var json = JsonSerializer.Serialize(decodedValue);
        var result = JsonSerializer.Deserialize<TorrentFileMetaInfo>(json);

        return result;
    }


}
