using System.Text.Json;

// Parse arguments
var (command, param) = args.Length switch
{
    0 => throw new InvalidOperationException("Usage: your_bittorrent.sh <command> <param>"),
    1 => throw new InvalidOperationException("Usage: your_bittorrent.sh <command> <param>"),
    _ => (args[0], args[1])
};

// Parse command and act accordingly
if (command == "decode")
{
    // You can use print statements as follows for debugging, they'll be visible when running tests.
    //Console.WriteLine("Logs from your program will appear here!");

    //Uncomment this line to pass the first stage
    var encodedValue = param;
    if (Char.IsDigit(encodedValue[0]))
    {
        var decodedValue = DecodeString(encodedValue);
        Console.WriteLine(decodedValue);
    }
    else if (encodedValue[0] == 'i')
    {
        var decodedValue = DecodeInteger(encodedValue);
        Console.WriteLine(decodedValue);
    }
    else if (encodedValue[0] == 'l')
    {
        var decodedValue = DecodeList(encodedValue);
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



string DecodeInteger(string encodedValue)
{
    var firstIndex = encodedValue.IndexOf("i");
    var lastIndex = encodedValue.IndexOf("e");
    if (firstIndex == -1 || lastIndex == -1 || (firstIndex == -1 && lastIndex == -1))
    {
        throw new InvalidOperationException("Unhandled encoded value: " + encodedValue);
    }
    var strLength = lastIndex - firstIndex;
    var numberString = encodedValue.Substring(firstIndex + 1, strLength - 1);
    var isParsed = long.TryParse(numberString, out var parsedValue);
    if (!isParsed)
    {
        throw new InvalidOperationException("Unhandled encoded value: " + encodedValue);
    }

    return JsonSerializer.Serialize(parsedValue);
}
string DecodeString(string encodedValue)
{
    var colonIndex = encodedValue.IndexOf(':');
    if (colonIndex != -1)
    {
        var strLength = int.Parse(encodedValue[..colonIndex]);
        var strValue = encodedValue.Substring(colonIndex + 1, strLength);
        return JsonSerializer.Serialize(strValue);
    }
    else
    {
        throw new InvalidOperationException("Invalid encoded value: " + encodedValue);
    }
}
string DecodeList(string encodedValue)
{
    var firstIndex = encodedValue.IndexOf("l");
    var lastIndex = encodedValue.LastIndexOf("e");
    if (firstIndex == -1 || lastIndex == -1 || (firstIndex == -1 && lastIndex == -1))
    {
        throw new InvalidOperationException("Unhandled encoded value: " + encodedValue);
    }
    var strLength = lastIndex - firstIndex;
    var listString = encodedValue.Substring(firstIndex + 1, strLength - 1);
    var splitArray = listString.Split(':');
    var numberString = int.Parse(splitArray[0]);
    var stringPart = splitArray[1].Substring(0, numberString);

    var numberPart = splitArray[1].Substring(numberString);
    var decodedNumberPart = DecodeInteger(numberPart);
    object[] array = { stringPart, int.Parse(decodedNumberPart) };
    string serialized = JsonSerializer.Serialize(array);
    return serialized;
}

