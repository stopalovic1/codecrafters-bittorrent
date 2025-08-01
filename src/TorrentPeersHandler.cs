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
    public static async Task<string> InitiatePeerHandshakeAsync(NetworkStream networkStream, TorrentFileExtractedInfo parsedTorrentFile)
    {
        var message = "BitTorrent protocol";

        var handshakeBytes = new byte[68];
        var messageBytes = Encoding.ASCII.GetBytes(message);
        byte[] zeroBytes = new byte[8];
        var sha1Bytes = Convert.FromHexString(parsedTorrentFile.InfoHashHex);
        var randomBytes = new byte[20];
        Random.Shared.NextBytes(randomBytes);

        handshakeBytes[0] = 19;
        messageBytes.AsSpan().CopyTo(handshakeBytes.AsSpan(1));
        zeroBytes.AsSpan().CopyTo(handshakeBytes.AsSpan(20));
        sha1Bytes.AsSpan().CopyTo(handshakeBytes.AsSpan(28));
        randomBytes.AsSpan().CopyTo(handshakeBytes.AsSpan(48));

        await networkStream.WriteAsync(handshakeBytes);

        var responseBytes = new byte[68];

        await networkStream.ReadAsync(responseBytes);

        var peerBytes = responseBytes[48..];

        var hexString = Convert.ToHexString(peerBytes).ToLowerInvariant();
        return hexString;
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length)
    {
        var buffer = new byte[length];
        int totalRead = 0;
        while (totalRead < length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, length - totalRead));
            if (read == 0)
            {
                throw new IOException("Connection closed while reading");
            }
            totalRead += read;
        }
        return buffer;
    }
    private static int GetPieceLength(long fileLength, int piecelength, int pieceIndex)
    {
        if (fileLength - pieceIndex * piecelength < 0)
        {
            var result = piecelength - (int)Math.Abs(fileLength - pieceIndex * piecelength);
            return result;
        }
        return piecelength;
    }
    //public static async Task DownloadPieceAsync(TorrentFileExtractedInfo extractedInfo, string outputPath, int pieceIndex)
    //{
    //    const int BlockSize = 16384;

    //    var parsedTorrentFile = await TorrentFileParser.ParseAsync(path);

    //    var peers = await GetTorrentPeersAsync(path);

    //    var address = peers[0].Split(':');

    //    using var client = new TcpClient(address[0], int.Parse(address[1]));
    //    var networkStream = client.GetStream();

    //    await InitiatePeerHandshakeAsync(networkStream, parsedTorrentFile);

    //    var byteMessage = new byte[5];
    //    var pieceLength = GetPieceLength(parsedTorrentFile.Length, parsedTorrentFile.PieceLength, pieceIndex + 1);
    //    int totalBlocks = (int)Math.Ceiling((double)pieceLength / BlockSize);
    //    var blocksBuffer = new byte[pieceLength];

    //    int receivedBlocks = 0;

    //    while (client.Connected)
    //    {
    //        var lengthBytes = await ReadExactAsync(networkStream, 4);

    //        int msgLength = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);

    //        if (msgLength == 0)
    //        {
    //            break;
    //        }

    //        var msgPayload = await ReadExactAsync(networkStream, msgLength);
    //        byte messageId = msgPayload[0];

    //        if (messageId == 5) //bitfield
    //        {
    //            var interestedMessage = new byte[5];
    //            BinaryPrimitives.WriteInt32BigEndian(interestedMessage.AsSpan(0), 1);
    //            interestedMessage[4] = 2;
    //            await networkStream.WriteAsync(interestedMessage); //intrested
    //        }
    //        else if (messageId == 1) //unchoke
    //        {
    //            for (int j = 0; j < totalBlocks; j++)
    //            {
    //                var begin = j * BlockSize;
    //                var request = new byte[17];
    //                var blockLength = (int)Math.Min(BlockSize, pieceLength - begin);
    //                BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(0), 13);
    //                request[4] = 6;
    //                BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(5), pieceIndex);
    //                BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(9), begin);
    //                BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(13), blockLength);


    //                await networkStream.WriteAsync(request);
    //            }
    //        }
    //        else if (messageId == 7)
    //        {
    //            var beginBlock = receivedBlocks * BlockSize;
    //            var blockLength = (int)Math.Min(BlockSize, pieceLength - beginBlock);
    //            var pieceMessageBuffer = new byte[8 + blockLength];
    //            var blockBytes = msgPayload[9..];
    //            Array.Copy(blockBytes, 0, blocksBuffer, beginBlock, blockBytes.Length);
    //            receivedBlocks++;
    //            if (receivedBlocks == totalBlocks) break;
    //        }
    //    }

    //    var hexString = Convert.ToHexString(SHA1.HashData(blocksBuffer)).ToLowerInvariant();
    //    if (hexString == parsedTorrentFile.PieceHashes[pieceIndex])
    //    {
    //        await File.WriteAllBytesAsync(outputPath, blocksBuffer);
    //        Console.WriteLine($"Piece {pieceIndex} downloaded to {outputPath}.");
    //    }
    //}
}

