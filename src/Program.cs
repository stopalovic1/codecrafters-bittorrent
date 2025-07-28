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
    if (Char.IsDigit(encodedValue[0]))
    {
        var decodedValue = Bencoding.DecodeString(encodedValue);
        Console.WriteLine(JsonSerializer.Serialize(decodedValue));
    }
    else if (encodedValue[0] == 'i')
    {
        var decodedValue = Bencoding.DecodeInteger(encodedValue);
        Console.WriteLine(JsonSerializer.Serialize(decodedValue));
    }
    else if (encodedValue[0] == 'l')
    {
        var decodedValue = Bencoding.DecodeList(encodedValue);
        Console.WriteLine(decodedValue);
    }
    else
    {
        throw new InvalidOperationException("Unhandled encoded value: " + encodedValue);
    }
}
else
{
    throw new InvalidOperationException($"Invalid command: {command}");
}






