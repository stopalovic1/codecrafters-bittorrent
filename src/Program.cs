using codecrafters_bittorrent.src;
using codecrafters_bittorrent.src.Models;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.Json;
using System.Web;

// Parse arguments
var (command, param) = args.Length switch
{
    0 => throw new InvalidOperationException("Usage: your_bittorrent.sh <command> <param>"),
    1 => throw new InvalidOperationException("Usage: your_bittorrent.sh <command> <param>"),
    _ => (args[0], args[1])
};

if (command == "decode")
{
    var encodedValue = param;
    try
    {
        var decodedValue = Bencoding.Decode(encodedValue, 0);
        Console.WriteLine(JsonSerializer.Serialize(decodedValue.Item1));
    }
    catch
    {
        throw new InvalidOperationException("Unhandled encoded value: " + encodedValue);
    }
}
else if (command == "info")
{
    var path = param;
    var result = await TorrentFileParser.ParseAsync(path);
    Console.WriteLine(result.ToString());
}
else if (command == "peers")
{
    var path = param;
    var torrentInfo = await TorrentFileParser.ParseAsync(path);
    var client = new HttpClient();
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


    var queryBuilder = QueryString.Create(queryParams);
    client.BaseAddress = new Uri(torrentInfo.TrackerUrl);

    var response = await client.GetAsync($"?{query}");
    var result = await response.Content.ReadAsByteArrayAsync();

    var decodedResult = Bencoding.Decode(result, 0);

    var info = JsonSerializer.Serialize(decodedResult.Item1);
    var trackerResponse = JsonSerializer.Deserialize<TrackerResponse>(info);

    int range = 6;
    var ips = new List<string>();
    for (int i = 0; i < trackerResponse!.Peers.Length; i += range)
    {
        var peer = trackerResponse!.Peers[i..(i + range)];
        var ip = string.Join(".", peer[0..4].Select(b => (int)b));
        var portBytes = peer[4..6];
        var port = BitConverter.ToUInt16(portBytes.Reverse().ToArray());
        ips.Add($"{ip}:{port}");
    }
    var output = string.Join("\n", ips);
    Console.WriteLine(output);
}
else
{
    throw new InvalidOperationException($"Invalid command: {command}");
}






