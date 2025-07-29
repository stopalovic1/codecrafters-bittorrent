using codecrafters_bittorrent.src;
using codecrafters_bittorrent.src.Models;
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
    var result = await TorrentPeersHandler.GetTorrentPeersAsync(path);
    var output = string.Join("\n", result);
    Console.WriteLine(output);
}
else if (command == "handshake")
{
    var path = param;
    var parsedTorrentFile = await TorrentFileParser.ParseAsync(path);

    var memoryStream = new MemoryStream();

    var message = "BitTorrent protocol";
    var messageBytes = Encoding.ASCII.GetBytes(message);
    //var messageLengthBytes = Encoding.ASCII.GetBytes(message.Length.ToString());
    var sha1Bytes = Convert.FromHexString(parsedTorrentFile.InfoHashHex);
    memoryStream.WriteByte(19);
    memoryStream.Write(messageBytes, 0, messageBytes.Length);
    byte[] zeroBytes = new byte[8];

    memoryStream.Write(zeroBytes, 0, zeroBytes.Length);
    memoryStream.Write(sha1Bytes, 0, sha1Bytes.Length);
    var randomBytes = new byte[20];

    Random.Shared.NextBytes(randomBytes);
    memoryStream.Write(randomBytes, 0, randomBytes.Length);


    var peers = await TorrentPeersHandler.GetTorrentPeersAsync(path);
    var ip = peers[0].Split(':');

    using var client = new TcpClient(ip[0], int.Parse(ip[1]));
    var networkStream = client.GetStream();
    memoryStream.Position = 0;
    memoryStream.CopyTo(networkStream);

    var serverResponse = new byte[memoryStream.Length];
    int totalRead = 0;
    while (totalRead < serverResponse.Length)
    {
        var read = networkStream.Read(serverResponse, totalRead, serverResponse.Length - totalRead);

        if (read == 0)
        {
            break;
        }
        totalRead += read;
    }

    var hexString = Convert.ToHexString(serverResponse).ToLowerInvariant();
    Console.WriteLine($"Peer ID: {hexString}");
}
else
{
    throw new InvalidOperationException($"Invalid command: {command}");
}






