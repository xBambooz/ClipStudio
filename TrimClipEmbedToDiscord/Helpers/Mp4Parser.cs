using System;
using System.IO;

namespace BamboozClipStudio.Helpers;

/// <summary>
/// Parses an MP4/MOV file's box tree to extract the video track dimensions
/// from the <c>tkhd</c> (Track Header) box without any external dependency.
/// </summary>
public static class Mp4Parser
{
    // Container atoms that may contain the tkhd we care about.
    private static readonly string[] ContainerAtoms = { "moov", "trak", "mdia" };

    /// <summary>
    /// Returns the pixel dimensions of the first video track found in the file.
    /// Returns (0, 0) on any failure or if no video track is found.
    /// </summary>
    public static (int w, int h) GetDimensions(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);
            return ParseAtom(br, fs.Length);
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <summary>
    /// Recursively walks atoms from the current stream position up to
    /// <paramref name="endPosition"/>, returning the first tkhd dimensions found.
    /// </summary>
    private static (int w, int h) ParseAtom(BinaryReader br, long endPosition)
    {
        while (br.BaseStream.Position + 8 <= endPosition)
        {
            long atomStart = br.BaseStream.Position;

            // Box size is big-endian.
            uint size32 = ReadUInt32BE(br);
            string type = new string(br.ReadChars(4));

            long atomSize;
            if (size32 == 1)
            {
                // Extended 64-bit size immediately follows the type field.
                atomSize = (long)ReadUInt64BE(br);
            }
            else if (size32 == 0)
            {
                // Box extends to EOF.
                atomSize = endPosition - atomStart;
            }
            else
            {
                atomSize = size32;
            }

            if (atomSize < 8)
                break; // Malformed; abort.

            long atomEnd = atomStart + atomSize;
            // Guard against atoms that extend beyond the declared boundary.
            if (atomEnd > endPosition)
                atomEnd = endPosition;

            long payloadStart = br.BaseStream.Position;

            if (Array.IndexOf(ContainerAtoms, type) >= 0)
            {
                // Recurse into container atom.
                var result = ParseAtom(br, atomEnd);
                if (result != (0, 0))
                    return result;
            }
            else if (type == "tkhd")
            {
                var dims = ParseTkhd(br);
                if (dims != (0, 0))
                    return dims;
            }

            // Skip to the end of this atom regardless of how much was consumed.
            if (br.BaseStream.Position != atomEnd)
                br.BaseStream.Seek(atomEnd, SeekOrigin.Begin);
        }

        return (0, 0);
    }

    /// <summary>
    /// Parses a <c>tkhd</c> box from the current stream position and returns
    /// the fixed-point 16.16 width/height as integer pixel values.
    /// </summary>
    private static (int w, int h) ParseTkhd(BinaryReader br)
    {
        // version (1 byte) + flags (3 bytes)
        byte version = br.ReadByte();
        br.ReadBytes(3); // flags

        if (version == 1)
        {
            // version 1: creation_time (8) + modification_time (8) + track_ID (4) + reserved (4) + duration (8)
            br.ReadBytes(8 + 8 + 4 + 4 + 8);
        }
        else
        {
            // version 0: creation_time (4) + modification_time (4) + track_ID (4) + reserved (4) + duration (4)
            br.ReadBytes(4 + 4 + 4 + 4 + 4);
        }

        // reserved (8) + layer (2) + alternate_group (2) + volume (2) + reserved (2)
        br.ReadBytes(8 + 2 + 2 + 2 + 2);

        // matrix (9 × 4 bytes = 36 bytes)
        br.ReadBytes(36);

        // width and height as 16.16 fixed-point, big-endian
        uint rawW = ReadUInt32BE(br);
        uint rawH = ReadUInt32BE(br);

        int w = (int)(rawW >> 16);
        int h = (int)(rawH >> 16);

        return (w, h);
    }

    // ── Big-endian helpers ──────────────────────────────────────────────────

    private static uint ReadUInt32BE(BinaryReader br)
    {
        var b = br.ReadBytes(4);
        return (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
    }

    private static ulong ReadUInt64BE(BinaryReader br)
    {
        var b = br.ReadBytes(8);
        return ((ulong)b[0] << 56) | ((ulong)b[1] << 48) | ((ulong)b[2] << 40) | ((ulong)b[3] << 32)
             | ((ulong)b[4] << 24) | ((ulong)b[5] << 16) | ((ulong)b[6] << 8)  |  (ulong)b[7];
    }
}
