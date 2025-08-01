using codecrafters_bittorrent.src.Models;
using System;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace codecrafters_bittorrent.src;

public static class TorrentPeersHandler
{
    private static async Task<TrackerResponse?> GetTorrentTrackerInfoAsync(string path)
    {
        var torrentInfo = await TorrentFileParser.ParseAsync(path);

        var sha1Hash = Convert.FromHexString(torrentInfo.InfoHashHex);
        string urlEncodedInfoHash = string.Concat(sha1Hash.Select(b => $"%{b:X2}"));

        var queryParams = new Dictionary<string, string>
        {
            {"info_hash", urlEncodedInfoHash },
            {"peer_id", "t3kHfGUZNeY0KOW1FnHA" },
            {"port", "6881" },
            {"uploaded", "0" },
            {"downloaded", "0" },
            {"left", torrentInfo.Length.ToString() },
            {"compact", "1" },
        };

        var query = string.Join("&", queryParams.Select(kv => $"{kv.Key}={kv.Value}"));
        var client = new HttpClient
        {
            BaseAddress = new Uri(torrentInfo.TrackerUrl)
        };

        var httpResponse = await client.GetAsync($"?{query}");
        var byteArrayResponse = await httpResponse.Content.ReadAsByteArrayAsync();

        (var decodedResult, _) = Bencoding.Decode(byteArrayResponse, 0);

        var json = JsonSerializer.Serialize(decodedResult);
        var trackerResponse = JsonSerializer.Deserialize<TrackerResponse>(json);

        return trackerResponse;
    }
    private static List<string> ParseTorrentPeersInfo(TrackerResponse? response)
    {
        int range = 6;
        var ips = new List<string>();

        for (int i = 0; i < response!.Peers.Length; i += range)
        {
            var peer = response!.Peers[i..(i + range)];
            var ip = string.Join(".", peer[0..4].Select(b => (int)b));
            var portBytes = peer[4..6];
            var port = BitConverter.ToUInt16(portBytes.Reverse().ToArray());
            ips.Add($"{ip}:{port}");
        }

        return ips;
    }
    public static async Task<List<string>> GetTorrentPeersAsync(string path)
    {
        var trackerResponse = await GetTorrentTrackerInfoAsync(path);
        var parsedPeersInfo = ParseTorrentPeersInfo(trackerResponse);
        return parsedPeersInfo;
    }

    public static async Task<string> InitiatePeerHandshakeAsync(string path, string ip)
    {
        var memoryStream = new MemoryStream();
        var parsedTorrentFile = await TorrentFileParser.ParseAsync(path);
        var message = "BitTorrent protocol";
        var messageBytes = Encoding.ASCII.GetBytes(message);

        var sha1Bytes = Convert.FromHexString(parsedTorrentFile.InfoHashHex);

        memoryStream.WriteByte(19);
        memoryStream.Write(messageBytes, 0, messageBytes.Length);
        byte[] zeroBytes = new byte[8];
        memoryStream.Write(zeroBytes, 0, zeroBytes.Length);
        memoryStream.Write(sha1Bytes, 0, sha1Bytes.Length);
        var randomBytes = new byte[20];

        Random.Shared.NextBytes(randomBytes);
        memoryStream.Write(randomBytes, 0, randomBytes.Length);
        // var peers = await GetTorrentPeersAsync(path);
        var adress = ip.Split(':');

        using var client = new TcpClient(adress[0], int.Parse(adress[1]));
        var networkStream = client.GetStream();
        memoryStream.Position = 0;
        await memoryStream.CopyToAsync(networkStream);

        var serverResponse = new byte[memoryStream.Length];

        var read = await networkStream.ReadAsync(serverResponse);

        var peerResponseBytes = serverResponse[^20..];

        var hexString = Convert.ToHexString(peerResponseBytes).ToLowerInvariant();
        return hexString;
    }
    private static void WriteIntBigEndian(int value, byte[] buffer, int offset)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        Array.Copy(bytes, 0, buffer, offset, 4);
    }
    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length)
    {
        var buffer = new byte[length];
        int totalRead = 0;
        while (totalRead < length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, length - totalRead));
            if (read == 0)
                throw new IOException("Connection closed while reading");
            totalRead += read;
        }
        return buffer;
    }
    public static async Task DownloadPieceAsync(string path, string outputPath, int pieceIndex)
    {
        const int BlockSize = 16384;

        var parsedTorrentFile = await TorrentFileParser.ParseAsync(path);
        var handshakeMessage = "BitTorrent protocol";
        var handshakeBytes = new byte[68];

        var messageBytes = Encoding.ASCII.GetBytes(handshakeMessage);
        var sha1Bytes = Convert.FromHexString(parsedTorrentFile.InfoHashHex);
        byte[] zeroBytes = new byte[8];
        var randomBytes = new byte[20];

        handshakeBytes[0] = 19;
        Random.Shared.NextBytes(randomBytes);
        Array.Copy(messageBytes, 0, handshakeBytes, 1, messageBytes.Length);
        Array.Copy(zeroBytes, 0, handshakeBytes, 20, zeroBytes.Length);
        Array.Copy(sha1Bytes, 0, handshakeBytes, 28, sha1Bytes.Length);
        Array.Copy(randomBytes, 0, handshakeBytes, 48, randomBytes.Length);

        var peers = await GetTorrentPeersAsync(path);
        var address = peers[0].Split(':');

        using var client = new TcpClient(address[0], int.Parse(address[1]));
        var networkStream = client.GetStream();

        await networkStream.WriteAsync(handshakeBytes);

        var responseBytes = new byte[68];

        await networkStream.ReadAsync(responseBytes); //handshake

        var peerBytes = responseBytes[48..68];
        var reservedBytes = responseBytes[20..28];
        //Console.WriteLine(BitConverter.ToString(reservedBytes));

        var handshakeHashHex = Convert.ToHexString(peerBytes).ToLowerInvariant();



        var byteMessage = new byte[5];
        int totalBlocks = (int)Math.Ceiling((double)parsedTorrentFile.PieceLength / BlockSize);
        var pieceLength = parsedTorrentFile.PieceLength;
        var blocksBuffer = new byte[pieceLength];

        int receivedBlocks = 0;

        while (client.Connected)
        {
            var lengthBytes = await ReadExactAsync(networkStream, 4);

            int msgLength = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);

            if (msgLength == 0)
            {
                break;
            }

            var msgPayload = await ReadExactAsync(networkStream, msgLength);
            byte messageId = msgPayload[0];

            if (messageId == 5) //bitfield
            {
                var interestedMessage = new byte[5];
                WriteIntBigEndian(1, interestedMessage, 0);
                interestedMessage[4] = 2;
                await networkStream.WriteAsync(interestedMessage); //intrested
            }
            else if (messageId == 1) //unchoke
            {
                for (int j = 0; j < totalBlocks; j++)
                {
                    var begin = j * BlockSize;
                    var request = new byte[17];
                    var blockLength = Math.Min(BlockSize, pieceLength - begin);
                    WriteIntBigEndian(13, request, 0);
                    request[4] = (byte)6;
                    WriteIntBigEndian(pieceIndex, request, 5);
                    WriteIntBigEndian(begin, request, 9);
                    WriteIntBigEndian(blockLength, request, 13);
                    await networkStream.WriteAsync(request);
                }
            }
            else if (messageId == 7)
            {
                var beginBlock = receivedBlocks * BlockSize;
                var blockLength = Math.Min(BlockSize, pieceLength - beginBlock);
                var pieceMessageBuffer = new byte[8 + blockLength];
                //await networkStream.ReadAsync(pieceMessageBuffer.AsMemory(0, pieceMessageBuffer.Length));
                var blockBytes = msgPayload[9..];
                Array.Copy(blockBytes, 0, blocksBuffer, beginBlock, blockBytes.Length);
                receivedBlocks++;
                if (receivedBlocks == totalBlocks) break;
            }
        }

        var hexString = Convert.ToHexString(SHA1.HashData(blocksBuffer)).ToLowerInvariant();
        if (hexString == parsedTorrentFile.PieceHashes[pieceIndex])
        {
            await File.WriteAllBytesAsync(outputPath, blocksBuffer);
            Console.WriteLine($"Piece {pieceIndex} downloaded to {outputPath}.");
        }
    }

}

