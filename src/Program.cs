using codecrafters_bittorrent.src;
using System.Text.Json;

// Parse arguments
var (command, param1, param2) = args.Length switch
{
    0 => throw new InvalidOperationException("Usage: your_bittorrent.sh <command> <param>"),
    1 => throw new InvalidOperationException("Usage: your_bittorrent.sh <command> <param>"),
    3 => (args[0], args[1], args[2]),
    _ => (args[0], args[1], null)
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
    var result = await TorrentFileParser.ParseAsync(path);
    Console.WriteLine(result.ToString());
}
else if (command == "peers")
{
    var path = param1;
    var result = await TorrentPeersHandler.GetTorrentPeersAsync(path);
    var output = string.Join("\n", result);
    Console.WriteLine(output);
}
else if (command == "handshake")
{
    var path = param1;
    var hexString = await TorrentPeersHandler.InitiatePeerHandshakeAsync(path, param2!);
    Console.WriteLine($"Peer ID: {hexString}");
}
else if (command == "download_piece")
{
    var result = await TorrentPeersHandler.DownloadPieceAsync(param1!, int.Parse(param2!));
    Console.WriteLine(result);
}
else
{
    throw new InvalidOperationException($"Invalid command: {command}");
}






