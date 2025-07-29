using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace codecrafters_bittorrent.src.Models;

public class TrackerResponse
{
    [JsonPropertyName("complete")]
    public int? Complete { get; set; }

    [JsonPropertyName("incomplete")]
    public int? Incomplete { get; set; }

    [JsonPropertyName("interval")]
    public int? Interval { get; set; }

    [JsonPropertyName("min interval")]
    public int? MinInterval { get; set; }

    [JsonPropertyName("peers")]
    public byte[] Peers { get; set; }
}
