using System.Text;
using System.Text.Json;

namespace codecrafters_bittorrent.src;

public static class Bencoding
{
    private static readonly HashSet<string> StringValueKeys = new()
    {
        "announce",
        "comment",
        "created by",
        "encoding",
        "name",
        "path"
    };
    private static (long, int) DecodeInteger(string encodedValue, int pos)
    {
        if (encodedValue[pos] != 'i') throw new Exception("Expected 'i'");
        pos++;

        var lastIndex = encodedValue.IndexOf("e", pos);

        var numberString = encodedValue.Substring(pos, lastIndex - pos);
        var isParsed = long.TryParse(numberString, out var parsedValue);
        if (!isParsed)
        {
            throw new InvalidOperationException("Unhandled encoded value: " + encodedValue);
        }

        return (parsedValue, lastIndex + 1);
    }
    private static (object, int) DecodeString(string encodedValue, int pos)
    {
        var colonIndex = encodedValue.IndexOf(':', pos);
        if (colonIndex != -1)
        {
            var strLength = int.Parse(encodedValue.Substring(pos, colonIndex - pos));
            var strValue = encodedValue.Substring(colonIndex + 1, strLength);
            pos += colonIndex - pos + strValue.Length + 1;
            return (strValue, pos);
        }
        else
        {
            throw new InvalidOperationException("Invalid encoded value: " + encodedValue);
        }
    }
    private static (object, int) DecodeListRec(string encodedValue, int pos)
    {
        var list = new List<object>();
        while (encodedValue[pos] != 'e')
        {
            (object, int) value = Decode(encodedValue, pos);
            pos = value.Item2;
            list.Add(value.Item1);
        }
        pos++;
        return (list, pos);
    }
    private static (object, int) DecodeDictionaryRec(string encodedValue, int pos)
    {
        var dic = new Dictionary<string, object>();
        while (encodedValue[pos] != 'e')
        {
            (object, int) key = Decode(encodedValue, pos);
            pos = key.Item2;
            (object, int) value = Decode(encodedValue, pos);
            pos = value.Item2;
            dic.Add(key.Item1.ToString()!, value.Item1);
        }

        pos++;
        return (dic, pos);
    }
    public static (object, int) Decode(string encodedValue, int pos)
    {
        if (encodedValue[pos] == 'l')
        {
            pos += 1;
            return DecodeListRec(encodedValue, pos);
        }
        else if (encodedValue[pos] == 'd')
        {
            pos += 1;
            return DecodeDictionaryRec(encodedValue, pos);
        }
        else if (encodedValue[pos] == 'i')
        {
            var decodedValue = DecodeInteger(encodedValue, pos);
            return decodedValue;
        }
        else if (char.IsDigit(encodedValue[pos]))
        {
            var decodedValue = DecodeString(encodedValue, pos);
            return decodedValue;
        }
        throw new InvalidOperationException($"Invalid bencoded data at position {pos}");
    }
    public static (byte[], int) DecodeString(byte[] data, int pos)
    {
        var colonIndex = Array.IndexOf(data, (byte)':', pos);
        if (colonIndex != -1)
        {
            var s = Encoding.ASCII.GetString(data[pos..colonIndex]);
            var strLength = int.Parse(s);
            byte[] strData = data[(colonIndex + 1)..(colonIndex + 1 + strLength)];
            return (strData, colonIndex + 1 + strLength);
        }
        else
        {
            throw new InvalidOperationException("Invalid encoded value: " + data);
        }

    }
    public static (long, int) DecodeInteger(byte[] data, int pos)
    {
        if (data[pos] != (byte)'i') throw new Exception("Expected 'i'");
        pos++;

        var lastIndex = Array.IndexOf(data, (byte)'e', pos);

        var byteNumber = data[pos..lastIndex];
        var stringValue = Encoding.ASCII.GetString(byteNumber);
        var isParsed = long.TryParse(stringValue, out var parsedValue);
        if (!isParsed)
        {
            throw new InvalidOperationException("Unhandled encoded value: " + data);
        }

        return (parsedValue, lastIndex + 1);
    }
    private static (object, int) DecodeListRec(byte[] data, int pos)
    {
        var list = new List<object>();
        while (data[pos] != (byte)'e')
        {
            (object, int) value = Decode(data, pos);
            pos = value.Item2;
            list.Add(value.Item1);
        }
        pos++;
        return (list, pos);
    }
    private static (object, int) DecodeDictionaryRec(byte[] data, int pos)
    {
        var dic = new Dictionary<string, object>();
        while (data[pos] != (byte)'e')
        {
            var (keyBytes, nextPos) = Decode(data, pos);
            pos = nextPos;

            var (valueRaw, valuePos) = Decode(data, pos);
            pos = valuePos;

            var keyStr = Encoding.UTF8.GetString((byte[])keyBytes);

            if (valueRaw is byte[] valueBytes && StringValueKeys.Contains(keyStr))
            {
                dic[keyStr] = Encoding.UTF8.GetString(valueBytes);
            }
            else
            {
                dic[keyStr] = valueRaw;
            }
        }

        pos++;
        return (dic, pos);
    }
    public static (object, int) Decode(byte[] data, int pos)
    {
        if (data[pos] == (byte)'l')
        {
            pos += 1;
            return DecodeListRec(data, pos);
        }
        else if (data[pos] == (byte)'d')
        {
            pos += 1;
            return DecodeDictionaryRec(data, pos);
        }
        else if (data[pos] == (byte)'i')
        {
            var decodedValue = DecodeInteger(data, pos);
            return decodedValue;
        }
        else if (char.IsDigit((char)data[pos]))
        {
            var decodedValue = DecodeString(data, pos);
            return decodedValue;
        }
        throw new InvalidOperationException($"Invalid bencoded data at position {pos}");
    }
    public static byte[] EncodeDictionary(Dictionary<string, JsonElement> pairs)
    {
        var memoryStream = new MemoryStream();
        memoryStream.WriteByte((byte)'d');

        foreach (var kv in pairs.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var encodedKeyBytes = Encoding.ASCII.GetBytes(kv.Key);
            var lengthBytes = Encoding.ASCII.GetBytes(encodedKeyBytes.Length.ToString());
            memoryStream.Write(lengthBytes, 0, lengthBytes.Length);
            memoryStream.WriteByte((byte)':');
            memoryStream.Write(encodedKeyBytes, 0, encodedKeyBytes.Length);

            if (kv.Key == "pieces")
            {
                var bytes = kv.Value.Deserialize<byte[]>();
                var encodedBytes = Encoding.ASCII.GetBytes(bytes!.Length.ToString());
                memoryStream.Write(encodedBytes, 0, encodedBytes.Length);
                memoryStream.WriteByte((byte)':');
                memoryStream.Write(bytes!, 0, bytes!.Length);

            }
            else if (kv.Value.ValueKind == JsonValueKind.String)
            {
                var encodedValueBytes = Encoding.ASCII.GetBytes(kv.Value.ToString());
                var lengthValueBytes = Encoding.ASCII.GetBytes(encodedValueBytes.Length.ToString());

                memoryStream.Write(lengthValueBytes, 0, lengthValueBytes.Length);
                memoryStream.WriteByte((byte)':');
                memoryStream.Write(encodedValueBytes, 0, encodedValueBytes.Length);
            }
            else if (kv.Value.ValueKind == JsonValueKind.Number)
            {
                var number = kv.Value.GetInt64();
                var numberBytes = Encoding.ASCII.GetBytes(number.ToString());
                memoryStream.WriteByte((byte)'i');
                memoryStream.Write(numberBytes, 0, numberBytes.Length);
                memoryStream.WriteByte((byte)'e');
            }
        }

        memoryStream.WriteByte((byte)'e');
        var array = memoryStream.ToArray();
        return array;
    }
}
