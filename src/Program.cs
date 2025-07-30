using codecrafters_bittorrent.src;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

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
    var result = await TorrentFileParser.ParseAsync(path);
    var output = string.Join("\n", result);
    Console.WriteLine(output);
}
else if (command == "handshake")
{
    var path = param;
    var hexString = TorrentPeersHandler.InitiatePeerHandshakeAsync(path);
    Console.WriteLine($"Peer ID: {hexString}");
}
else
{
    throw new InvalidOperationException($"Invalid command: {command}");
}






