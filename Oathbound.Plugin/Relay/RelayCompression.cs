using System;
using System.IO;
using System.IO.Compression;

namespace Oathbound.Plugin.Relay;

/// collar/catalog-sync: gzip, matching protocol/constants.json's `compression` choice. Both ends of this
/// are always this same plugin (Owner and Sub are two installs of the identical binary), so cross-runtime
/// gzip-format compatibility is not a concern the way the crypto envelope's cross-runtime shape is.
public static class RelayCompression
{
    public static byte[] Compress(byte[] plaintext)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            gzip.Write(plaintext, 0, plaintext.Length);
        return output.ToArray();
    }

    /// Bounded decompression: refuses to allocate/write past `maxDecompressedBytes`, so a maliciously
    /// crafted small compressed blob (a decompression bomb) can never force unbounded memory use - task
    /// 6.4 "decompression-bomb...snapshots leave existing imports untouched."
    public static byte[] Decompress(byte[] compressed, int maxDecompressedBytes)
    {
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        var buffer = new byte[81920];
        int read;
        while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + read > maxDecompressedBytes)
                throw new InvalidDataException($"Decompressed size exceeds the {maxDecompressedBytes}-byte limit; refusing to continue (possible decompression bomb).");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
}
