using codecrafters_bittorrent.src;
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
    var result = await TorrentFileParser.GetTorrentFileMetaInfoAsync(path);
    var hash = TorrentFileParser.CalculateInfoHash(result);
    var pieceHashes = TorrentFileParser.ExtractHashes(result!.Info.Pieces);
    Console.WriteLine($"Tracker URL: {result!.Announce}");
    Console.WriteLine($"Length: {result!.Info.Length}");
    Console.WriteLine($"Info Hash: {hash}");
    Console.WriteLine($"Piece Length: {result.Info.PieceLength}");
    Console.WriteLine("Piece Hashes:");
    var stringHashes = string.Join("\n", pieceHashes);
    Console.WriteLine(stringHashes);
}
else
{
    throw new InvalidOperationException($"Invalid command: {command}");
}






