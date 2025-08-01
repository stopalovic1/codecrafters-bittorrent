using codecrafters_bittorrent.src;
using System.Security.Cryptography;
using System.Text.Json;
using System.Web;

// Parse arguments
var (command, param1, param2, param3, param4) = args.Length switch
{
    0 => throw new InvalidOperationException("Usage: your_bittorrent.sh <command> <param>"),
    1 => throw new InvalidOperationException("Usage: your_bittorrent.sh <command> <param>"),
    2 => (args[0], args[1], null, null, null),
    3 => (args[0], args[1], args[2], null, null),
    4 => (args[0], args[1], args[2], args[3], null),
    _ => (args[0], args[1], args[2], args[3], args[4]),
};

if (command == "decode")
{
    var encodedValue = param1;
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
    var path = param1;
    var torrentParser = new TorrentParser(path);
    var result = await torrentParser.ParseAsync();
    Console.WriteLine(result.ToString());
}
else if (command == "peers")
{
    var path = param1;
    var torrentParser = new TorrentParser(path);
    await torrentParser.ParseAsync();
    var result = await torrentParser.GetTorrentPeersAsync();
    var output = string.Join("\n", result);
    Console.WriteLine(output);
}
else if (command == "handshake")
{
    var path = param1;

    var torrentParser = new TorrentParser(path);
    var torrentFile = await torrentParser.ParseAsync();

    var peerClient = new PeerClient(param2!);
    var hexString = await peerClient.InitiatePeerHandshakeAsync(torrentFile.InfoHashHex);

    Console.WriteLine($"Peer ID: {hexString}");
}
else if (command == "download_piece")
{
    var torrentParser = new TorrentParser(param3!);
    var torrentFile = await torrentParser.ParseAsync();

    var peers = await torrentParser.GetTorrentPeersAsync();
    var peerClient = new PeerClient(peers);

    await peerClient.DownloadPieceAsync(torrentFile, param2!, int.Parse(param4!));
}
else if (command == "download")
{
    var torrentParser = new TorrentParser(param3!);
    var torrentFile = await torrentParser.ParseAsync();

    var peers = await torrentParser.GetTorrentPeersAsync();
    var peerClient = new PeerClient(peers);

    await peerClient.DownloadFileAsync(torrentFile, param2!);
}
else if (command == "magnet_parse")
{
    var magnetInfo = TorrentParser.ParseMagnetLink(param1!);

    Console.WriteLine($"Tracker URL: {magnetInfo.TrackerUrl}");
    Console.WriteLine($"Info Hash: {magnetInfo.InfoHash}");
}
else
{
    throw new InvalidOperationException($"Invalid command: {command}");
}