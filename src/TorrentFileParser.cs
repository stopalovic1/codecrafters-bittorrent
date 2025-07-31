using codecrafters_bittorrent.src.Models;
using System.Security.Cryptography;
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

    private static string CalculateInfoHash(TorrentFileMetaInfo? info)
    {
        var infoJson = JsonSerializer.Serialize(info!.Info);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(infoJson);

        var encodedBytes = Bencoding.EncodeDictionary(dict!);
        var hexString = Convert.ToHexString(SHA1.HashData(encodedBytes));
        return hexString.ToLowerInvariant();
    }
    private static List<string> ExtractHashes(byte[] data)
    {
        int range = 20;
        var hashes = new List<string>();

        for (int i = 0; i < data.Length; i += range)
        {
            var byteRange = data[i..(i + range)];
            var hexData = Convert.ToHexString(byteRange).ToLowerInvariant();
            hashes.Add(hexData);
        }
        return hashes;
    }

    public static async Task<TorrentFileExtractedInfo> ParseAsync(string path)
    {
        var metaInfo = await GetTorrentFileMetaInfoAsync(path);
        var infoHashHex = CalculateInfoHash(metaInfo);
        var pieceHashes = ExtractHashes(metaInfo!.Info.Pieces);

        var metadata = new TorrentFileExtractedInfo
        {
            TrackerUrl = metaInfo!.Announce,
            Length = metaInfo.Info.Length!.Value,
            InfoHashHex = infoHashHex,
            PieceLength = metaInfo.Info.PieceLength!.Value,
            PieceHashes = pieceHashes.ToList()
        };
        return metadata;
    }


}