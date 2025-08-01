using codecrafters_bittorrent.src.Models;
using System.Security.Cryptography;
using System.Text.Json;
using System.Web;

namespace codecrafters_bittorrent.src;

public class TorrentParser
{
    private string _path;
    private TorrentFileExtractedInfo? _extractedInfo;
    public TorrentParser(string path)
    {
        _path = path;
    }

    private async Task<TorrentFileMetaInfo?> GetTorrentFileMetaInfoAsync(string path)
    {
        var torrentFile = await File.ReadAllBytesAsync(path);
        var (decodedValue, _) = Bencoding.Decode(torrentFile, 0);
        var json = JsonSerializer.Serialize(decodedValue);
        var result = JsonSerializer.Deserialize<TorrentFileMetaInfo>(json);
        return result;
    }
    private string CalculateInfoHash(TorrentFileMetaInfo? info)
    {
        var infoJson = JsonSerializer.Serialize(info!.Info);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(infoJson);

        var encodedBytes = Bencoding.EncodeDictionary(dict!);
        var hexString = Convert.ToHexString(SHA1.HashData(encodedBytes));
        return hexString.ToLowerInvariant();
    }
    private List<string> ExtractHashes(byte[] data)
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
    public async Task<TorrentFileExtractedInfo> ParseAsync()
    {
        var metaInfo = await GetTorrentFileMetaInfoAsync(_path);
        var infoHashHex = CalculateInfoHash(metaInfo);
        var pieceHashes = ExtractHashes(metaInfo!.Info.Pieces);

        var metadata = new TorrentFileExtractedInfo
        {
            FileName = metaInfo.Info.Name,
            TrackerUrl = metaInfo!.Announce,
            Length = metaInfo.Info.Length ?? -1,
            InfoHashHex = infoHashHex,
            PieceLength = metaInfo.Info.PieceLength!.Value,
            PieceHashes = pieceHashes.ToList()
        };
        _extractedInfo = metadata;
        return metadata;
    }
    private async Task<TrackerResponse?> GetTorrentTrackerInfoAsync()
    {
        if (_extractedInfo == null)
        {
            throw new Exception("Torrent file not parsed.");
        }

        var sha1Hash = Convert.FromHexString(_extractedInfo.InfoHashHex);
        string urlEncodedInfoHash = string.Concat(sha1Hash.Select(b => $"%{b:X2}"));

        var queryParams = new Dictionary<string, string>
        {
            {"info_hash", urlEncodedInfoHash },
            {"peer_id", "t3kHfGUZNeY0KOW1FnHA" },
            {"port", "6881" },
            {"uploaded", "0" },
            {"downloaded", "0" },
            {"left", _extractedInfo.Length.ToString() },
            {"compact", "1" },
        };

        var query = string.Join("&", queryParams.Select(kv => $"{kv.Key}={kv.Value}"));
        var client = new HttpClient
        {
            BaseAddress = new Uri(_extractedInfo.TrackerUrl)
        };

        var httpResponse = await client.GetAsync($"?{query}");
        var byteArrayResponse = await httpResponse.Content.ReadAsByteArrayAsync();

        (var decodedResult, _) = Bencoding.Decode(byteArrayResponse, 0);

        var json = JsonSerializer.Serialize(decodedResult);
        var trackerResponse = JsonSerializer.Deserialize<TrackerResponse>(json);

        return trackerResponse;
    }
    private List<string> ParseTorrentPeersInfo(TrackerResponse? response)
    {
        int range = 6;
        var ips = new List<string>();

        for (int i = 0; i < response!.Peers.Length; i += range)
        {
            var peer = response!.Peers[i..(i + range)];
            var ip = string.Join(".", peer[0..4].Select(b => (int)b));
            var portBytes = peer[4..6];
            var port = BitConverter.ToUInt16(portBytes.Reverse().ToArray());
            ips.Add($"{ip}:{port}");
        }

        return ips;
    }
    public async Task<List<string>> GetTorrentPeersAsync()
    {
        var trackerResponse = await GetTorrentTrackerInfoAsync();
        var parsedPeersInfo = ParseTorrentPeersInfo(trackerResponse);
        return parsedPeersInfo;
    }
    public static TorrentMagnetLink ParseMagnetLink(string magnetLink)
    {
        var queryParamsUrl = magnetLink.Split("?").Last();

        var queryParams = queryParamsUrl
            .Split("&")
            .ToDictionary(t => t.Split("=").First(), t => t.Split("=").Last());

        var info = new TorrentMagnetLink
        {
            DownloadName = queryParams["dn"],
            InfoHash = queryParams["xt"].Split(":").Last(),
            TrackerUrl = HttpUtility.UrlDecode(queryParams["tr"])
        };
        return info;
    }

}