namespace codecrafters_bittorrent.src;

public static class Bencoding
{
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

    public static (object, int) Decode(string encodedValue, int pos)
    {
        if (encodedValue[pos] == 'l')
        {
            pos += 1;
            return DecodeListRec(encodedValue, pos);
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

        return (encodedValue, pos);
    }



}
