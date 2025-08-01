using codecrafters_bittorrent.src.Models;
using System.Buffers.Binary;
using System.Collections;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace codecrafters_bittorrent.src;

public class PeerClient
{
    private readonly List<string> _ips;
    private readonly TcpClient _tcpClient;
    private readonly string _ip;
    private const int BlockSize = 16384;
    private FileStream? _stream;
    public PeerClient(string ip)
    {
        _ips = new();
        _ip = ip;
        var address = ip.Split(':');
        _tcpClient = new TcpClient(address[0], int.Parse(address[1]));
    }
    public PeerClient(List<string> ips)
    {
        _ips = ips;
        _ip = ips[0];
        var address = ips[0].Split(':');
        _tcpClient = new TcpClient(address[0], int.Parse(address[1]));
    }
    private int GetPieceLength(long fileLength, int piecelength, int pieceIndex)
    {
        if (fileLength - pieceIndex * piecelength < 0)
        {
            var result = piecelength - (int)Math.Abs(fileLength - pieceIndex * piecelength);
            return result;
        }
        return piecelength;
    }
    public async Task<string> InitiatePeerHandshakeAsync(string infoHashHex, bool isExtensionHandshake = false)
    {
        var message = "BitTorrent protocol";

        var handshakeBytes = new byte[68];
        var messageBytes = Encoding.ASCII.GetBytes(message);
        byte[] zeroBytes = new byte[8];

        if (isExtensionHandshake)
        {
            var bitArray = new BitArray(zeroBytes);
            bitArray.Set(44, true);
            bitArray.CopyTo(zeroBytes, 0);
        }

        var sha1Bytes = Convert.FromHexString(infoHashHex);
        var randomBytes = new byte[20];
        Random.Shared.NextBytes(randomBytes);

        handshakeBytes[0] = 19;
        messageBytes.AsSpan().CopyTo(handshakeBytes.AsSpan(1));
        zeroBytes.AsSpan().CopyTo(handshakeBytes.AsSpan(20));
        sha1Bytes.AsSpan().CopyTo(handshakeBytes.AsSpan(28));
        randomBytes.AsSpan().CopyTo(handshakeBytes.AsSpan(48));

        await _tcpClient.GetStream().WriteAsync(handshakeBytes);

        var responseBytes = await ReadExactAsync(_tcpClient.GetStream(), 68);

        var peerBytes = responseBytes[48..];

        var hexString = Convert.ToHexString(peerBytes).ToLowerInvariant();
        return hexString;
    }
    private async Task<byte[]> ReadExactAsync(NetworkStream stream, int length)
    {
        var buffer = new byte[length];
        int totalRead = 0;
        while (totalRead < length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, length - totalRead));
            if (read == 0)
            {
                throw new IOException();
            }
            totalRead += read;
        }
        return buffer;
    }

    private async Task SendRequestMessageAsync(int pieceIndex, int begin, int length)
    {
        var request = new byte[17];
        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(0), 13);
        request[4] = 6;
        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(5), pieceIndex);
        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(9), begin);
        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(13), length);

        Console.WriteLine($"Requesting Piece: {pieceIndex}, Block offset: {begin}, Length: {length}");
        await _tcpClient.GetStream().WriteAsync(request);
    }

    public async Task DownloadPieceAsync(TorrentFileExtractedInfo extractedInfo, string outputPath, int pieceIndex, bool performHandshake = true, bool saveAsPieces = true)
    {
        if (performHandshake)
        {
            await InitiatePeerHandshakeAsync(extractedInfo.InfoHashHex);
        }

        var pieceLength = GetPieceLength(extractedInfo.Length, extractedInfo.PieceLength, pieceIndex + 1);
        int totalBlocks = (int)Math.Ceiling((double)pieceLength / BlockSize);
        var blocksBuffer = new byte[pieceLength];

        int receivedBlocks = 0;

        while (_tcpClient.Connected)
        {
            var lengthBytes = await ReadExactAsync(_tcpClient.GetStream(), 4);
            int msgLength = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);

            if (msgLength == 0)
            {
                // Keep-alive message
                Console.WriteLine("Received keep-alive from peer, waiting...");
                continue;
            }

            var msgPayload = await ReadExactAsync(_tcpClient.GetStream(), msgLength);
            byte messageId = msgPayload[0];

            if (messageId == 0) // choke
            {
                Console.WriteLine("Peer choked us, waiting...");
                continue;
            }
            else if (messageId == 1) // unchoke
            {
                for (int j = 0; j < totalBlocks; j++)
                {
                    var begin = j * BlockSize;
                    var request = new byte[17];
                    var blockLength = (int)Math.Min(BlockSize, pieceLength - begin);
                    BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(0), 13);
                    request[4] = 6; // request message id
                    BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(5), pieceIndex);
                    BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(9), begin);
                    BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(13), blockLength);

                    await _tcpClient.GetStream().WriteAsync(request);
                }
            }
            else if (messageId == 5) // bitfield
            {
                var interestedMessage = new byte[5];
                BinaryPrimitives.WriteInt32BigEndian(interestedMessage.AsSpan(0), 1);
                interestedMessage[4] = 2;
                await _tcpClient.GetStream().WriteAsync(interestedMessage);
            }
            else if (messageId == 7) // piece
            {
                var blockPayload = msgPayload[9..];
                int beginBlock = receivedBlocks * BlockSize;
                blockPayload.CopyTo(blocksBuffer.AsSpan(beginBlock, blockPayload.Length));
                receivedBlocks++;

                if (receivedBlocks == totalBlocks)
                {
                    break;
                }
            }
            else
            {
                Console.WriteLine($"Unhandled message id {messageId}");
            }
        }

        var hexString = Convert.ToHexString(SHA1.HashData(blocksBuffer)).ToLowerInvariant();
        if (hexString == extractedInfo.PieceHashes[pieceIndex])
        {
            if (saveAsPieces)
            {
                await File.WriteAllBytesAsync(outputPath, blocksBuffer);
            }
            else
            {
                await _stream!.WriteAsync(blocksBuffer.AsMemory());
            }
            Console.WriteLine($"Piece {pieceIndex} downloaded and verified.");
            pieceIndex++;
            if (pieceIndex == extractedInfo.PieceHashes.Count) return;

            int nextPieceLength = GetPieceLength(extractedInfo.Length, extractedInfo.PieceLength, pieceIndex + 1);
            int nextTotalBlocks = (int)Math.Ceiling((double)nextPieceLength / BlockSize);
            Console.WriteLine($"\n--- Proactively requesting next piece {pieceIndex} ---");

            for (int blockIndex = 0; blockIndex < nextTotalBlocks; blockIndex++)
            {
                var begin = blockIndex * BlockSize;
                var blockLength = Math.Min(BlockSize, nextPieceLength - begin);
                await SendRequestMessageAsync(pieceIndex, begin, blockLength);
            }

        }
        else
        {
            Console.WriteLine($"Piece {pieceIndex} hash mismatch! Download corrupted.");
        }
    }
    public async Task DownloadFileAsync(TorrentFileExtractedInfo extractedInfo, string outputPath)
    {
        var pieces = extractedInfo.PieceHashes;
        _stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);

        try
        {
            Console.WriteLine($"Pre-allocating file '{outputPath}' with size {extractedInfo.Length} bytes.");
            _stream.SetLength(extractedInfo.Length);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error pre-allocating file. Do you have enough disk space? Details: {ex.Message}");
            return;
        }

        await InitiatePeerHandshakeAsync(extractedInfo.InfoHashHex);

        for (int i = 0; i < pieces.Count; i++)
        {
            await DownloadPieceAsync(extractedInfo, "", i, false, false);
        }
        Console.WriteLine($"\nDownload finished.");
    }

}
