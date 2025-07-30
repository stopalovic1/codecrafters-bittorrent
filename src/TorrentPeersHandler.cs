using codecrafters_bittorrent.src.Models;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace codecrafters_bittorrent.src;

public static class TorrentPeersHandler
{
    private static async Task<TrackerResponse?> GetTorrentTrackerInfoAsync(string path)
    {
        var torrentInfo = await TorrentFileParser.ParseAsync(path);

        var sha1Hash = Convert.FromHexString(torrentInfo.InfoHashHex);
        string urlEncodedInfoHash = string.Concat(sha1Hash.Select(b => $"%{b:X2}"));

        var queryParams = new Dictionary<string, string>
        {
            {"info_hash", urlEncodedInfoHash },
            {"peer_id", "t3kHfGUZNeY0KOW1FnHA" },
            {"port", "6881" },
            {"uploaded", "0" },
            {"downloaded", "0" },
            {"left", torrentInfo.Length.ToString() },
            {"compact", "1" },
        };

        var query = string.Join("&", queryParams.Select(kv => $"{kv.Key}={kv.Value}"));
        var client = new HttpClient
        {
            BaseAddress = new Uri(torrentInfo.TrackerUrl)
        };

        var httpResponse = await client.GetAsync($"?{query}");
        var byteArrayResponse = await httpResponse.Content.ReadAsByteArrayAsync();

        (var decodedResult, _) = Bencoding.Decode(byteArrayResponse, 0);

        var json = JsonSerializer.Serialize(decodedResult);
        var trackerResponse = JsonSerializer.Deserialize<TrackerResponse>(json);

        return trackerResponse;
    }
    private static List<string> ParseTorrentPeersInfo(TrackerResponse? response)
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
    public static async Task<List<string>> GetTorrentPeersAsync(string path)
    {
        var trackerResponse = await GetTorrentTrackerInfoAsync(path);
        var parsedPeersInfo = ParseTorrentPeersInfo(trackerResponse);
        return parsedPeersInfo;
    }

    public static async Task<string> InitiatePeerHandshakeAsync(string path, string ip)
    {
        var memoryStream = new MemoryStream();
        var parsedTorrentFile = await TorrentFileParser.ParseAsync(path);
        var message = "BitTorrent protocol";
        var messageBytes = Encoding.ASCII.GetBytes(message);

        var sha1Bytes = Convert.FromHexString(parsedTorrentFile.InfoHashHex);

        memoryStream.WriteByte(19);
        memoryStream.Write(messageBytes, 0, messageBytes.Length);
        byte[] zeroBytes = new byte[8];
        memoryStream.Write(zeroBytes, 0, zeroBytes.Length);
        memoryStream.Write(sha1Bytes, 0, sha1Bytes.Length);
        var randomBytes = new byte[20];

        Random.Shared.NextBytes(randomBytes);
        memoryStream.Write(randomBytes, 0, randomBytes.Length);
        // var peers = await GetTorrentPeersAsync(path);
        var adress = ip.Split(':');

        using var client = new TcpClient(adress[0], int.Parse(adress[1]));
        var networkStream = client.GetStream();
        memoryStream.Position = 0;
        await memoryStream.CopyToAsync(networkStream);

        var serverResponse = new byte[memoryStream.Length];

        var read = await networkStream.ReadAsync(serverResponse);

        var peerResponseBytes = serverResponse[^20..];

        var hexString = Convert.ToHexString(peerResponseBytes).ToLowerInvariant();
        return hexString;
    }





}
