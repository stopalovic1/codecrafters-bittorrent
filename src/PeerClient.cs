using codecrafters_bittorrent.src.Models;
using System.Buffers.Binary;
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
    public async Task<string> InitiatePeerHandshakeAsync(string infoHashHex)
    {
        var message = "BitTorrent protocol";

        var handshakeBytes = new byte[68];
        var messageBytes = Encoding.ASCII.GetBytes(message);
        byte[] zeroBytes = new byte[8];
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
        // Length prefix: 13
        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(0), 13);
        // Message ID: 6 (request)
        request[4] = 6;
        // Piece Index
        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(5), pieceIndex);
        // Begin offset
        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(9), begin);
        // Block Length
        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(13), length);

        Console.WriteLine($"Requesting Piece: {pieceIndex}, Block offset: {begin}, Length: {length}");
        await _tcpClient.GetStream().WriteAsync(request);
    }

    public async Task DownloadPieceAsync(TorrentFileExtractedInfo extractedInfo, string outputPath, int pieceIndex, bool performHandshake = true, bool resendInterested = false)
    {
        if (performHandshake)
        {
            await InitiatePeerHandshakeAsync(extractedInfo.InfoHashHex);
        }

        //if (resendInterested)
        //{
        //    var im = new byte[5];
        //    BinaryPrimitives.WriteInt32BigEndian(im.AsSpan(0), 1);
        //    im[4] = 2; // interested
        //    Console.WriteLine($"Sending interested message before downloading piece {pieceIndex}");
        //    await _tcpClient.GetStream().WriteAsync(im);
        //}

        var pieceLength = GetPieceLength(extractedInfo.Length, extractedInfo.PieceLength, pieceIndex + 1);
        int totalBlocks = (int)Math.Ceiling((double)pieceLength / BlockSize);
        var blocksBuffer = new byte[pieceLength];

        int receivedBlocks = 0;
        bool unchoked = false;

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

            Console.WriteLine($"Received message id: {messageId} (length {msgLength})");

            if (messageId == 0) // choke
            {
                Console.WriteLine("Peer choked us, waiting...");
                unchoked = false;
            }
            else if (messageId == 1) // unchoke
            {
                Console.WriteLine("Peer unchoked us, sending requests...");
                unchoked = true;

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

                    Console.WriteLine($"Requesting block {j} for piece {pieceIndex}, begin={begin}, length={blockLength}");
                    await _tcpClient.GetStream().WriteAsync(request);
                }
            }
            else if (messageId == 5) // bitfield
            {
                Console.WriteLine("Received bitfield, sending interested again...");
                var interestedMessage = new byte[5];
                BinaryPrimitives.WriteInt32BigEndian(interestedMessage.AsSpan(0), 1);
                interestedMessage[4] = 2;
                await _tcpClient.GetStream().WriteAsync(interestedMessage);
            }
            else if (messageId == 7) // piece
            {
                //if (!unchoked)
                //{
                //    Console.WriteLine("Received piece message but currently choked, ignoring...");
                //    continue;
                //}

                var blockPayload = msgPayload[9..];
                int beginBlock = receivedBlocks * BlockSize;
                blockPayload.CopyTo(blocksBuffer.AsSpan(beginBlock, blockPayload.Length));
                receivedBlocks++;

                Console.WriteLine($"Received block {receivedBlocks}/{totalBlocks} for piece {pieceIndex}");

                if (receivedBlocks == totalBlocks)
                {
                    Console.WriteLine($"All blocks for piece {pieceIndex} received");
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
            await File.WriteAllBytesAsync(outputPath, blocksBuffer);
            Console.WriteLine($"Piece {pieceIndex} downloaded and verified, saved to {outputPath}");
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
        await InitiatePeerHandshakeAsync(extractedInfo.InfoHashHex);
        var folder = Path.GetDirectoryName(outputPath);
        for (int i = 0; i < pieces.Count; i++)
        {
            var pieceName = $"{Path.GetFileNameWithoutExtension(extractedInfo.FileName)}_piece_{i}";
            var fullPath = Path.GetFullPath(Path.Combine(folder!, pieceName));
            await DownloadPieceAsync(extractedInfo, fullPath, i, false, i != 0);
        }

        var fileBuffer = new byte[extractedInfo.Length];

        int copyPlace = 0;
        for (int i = 0; i < pieces.Count; i++)
        {
            var pieceName = $"{Path.GetFileNameWithoutExtension(extractedInfo.FileName)}_piece_{i}";
            var fullPath = Path.GetFullPath(Path.Combine(folder!, pieceName));
            var pieceBytes = await File.ReadAllBytesAsync(fullPath);
            pieceBytes.AsSpan().CopyTo(fileBuffer.AsSpan(copyPlace));
            copyPlace += pieceBytes.Length;
        }

        var filePath = Path.GetFullPath(Path.Combine(folder!, extractedInfo.FileName));

        await File.WriteAllBytesAsync(filePath, fileBuffer);

    }

}
