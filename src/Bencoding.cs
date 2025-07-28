using System.Text.Json;

namespace codecrafters_bittorrent.src;

public static class Bencoding
{
    private static List<object> _bencodingList = new();
    public static long DecodeInteger(string encodedValue)
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

        return parsedValue;
    }
    public static string DecodeString(string encodedValue)
    {
        var colonIndex = encodedValue.IndexOf(':');
        if (colonIndex != -1)
        {
            var strLength = int.Parse(encodedValue[..colonIndex]);
            var strValue = encodedValue.Substring(colonIndex + 1, strLength);
            return strValue;
        }
        else
        {
            throw new InvalidOperationException("Invalid encoded value: " + encodedValue);
        }
    }
    public static string DecodeList(string encodedValue)
    {
        if (string.IsNullOrEmpty(encodedValue))
        {
            if (_bencodingList.Count == 1)
            {
                return JsonSerializer.Serialize(Array.Empty<string>());
            }


            var stack = new Stack<object>();
            for (int i = _bencodingList.Count - 1; i >= 0; i--)
            {
                if (_bencodingList[i] is string || _bencodingList[i] is long)
                {
                    stack.Push(_bencodingList[i]);
                }
                else
                {
                    if (stack.Count > 0)
                    {
                        ((List<object>)_bencodingList[i]).AddRange(stack.ToList());
                        stack.Clear();
                    }
                    else
                    {
                        ((List<object>)_bencodingList[i]).Add(_bencodingList[i + 1]);
                    }

                }
            }
            if (_bencodingList[0].GetType() == typeof(List<object>))
            {
                return JsonSerializer.Serialize(_bencodingList[0]);
            }
            else
            {
                return JsonSerializer.Serialize(new List<object> { _bencodingList[0] });
            }

        }

        if (encodedValue[0] == 'l')
        {
            var firstIndex = encodedValue.IndexOf("l");
            var lastIndex = encodedValue.LastIndexOf("e");
            var strLength = lastIndex - firstIndex;
            var listString = encodedValue.Substring(firstIndex + 1, strLength - 1);
            _bencodingList.Add(new List<object>());
            return DecodeList(listString);
        }
        else if (encodedValue[0] == 'i')
        {
            var decodedInteger = DecodeInteger(encodedValue);
            var lastIndex = encodedValue.IndexOf("e");
            var listString = encodedValue.Substring(lastIndex + 1);
            _bencodingList.Add(decodedInteger);
            return DecodeList(listString);
        }
        else if (Char.IsDigit(encodedValue[0]))
        {
            var decodedValue = DecodeString(encodedValue);
            var decodedValueLength = decodedValue.Length;
            var digitCount = (int)Math.Floor(Math.Log10(decodedValueLength) + 1);
            var listString = encodedValue.Substring(decodedValueLength + digitCount + 1);
            _bencodingList.Add(decodedValue);
            return DecodeList(listString);
        }

        return DecodeList("");
    }
}
