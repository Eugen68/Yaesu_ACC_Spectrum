// ACC Firmware Flasher: writes the ACC board's firmware to its ESP32-S3.
// Copyright (C) 2026 Colin Campbell MM5AGM
// SPDX-License-Identifier: GPL-3.0-only (see LICENSE in the top folder)

using System.Buffers.Binary;

namespace AccFirmwareFlasher;

/// <summary>
/// A merged firmware file flashes at address 0 and covers the flash from the
/// bootloader to the end of the app, so it also covers the NVS partition where
/// the board keeps its Wi-Fi network and SPI mode. That part of the file is
/// blank, and writing it wipes those settings. This reads the partition table
/// inside the file and splits the file into the pieces either side of any NVS
/// partition, so a firmware update keeps the board's settings.
/// </summary>
internal static class MergedImage
{
    /// <summary>Where the partition table sits in a merged file.</summary>
    public const int TableOffset = 0x8000;

    private const int TableSize = 0xC00;
    private const int EntrySize = 32;
    private const byte DataType = 0x01;
    private const byte NvsSubtype = 0x02;
    private const byte NvsKeysSubtype = 0x04;

    /// <summary>One part of the file to write: its flash address, which is also where it starts in the file, and its length.</summary>
    public readonly record struct Piece(int Offset, int Length);

    /// <summary>
    /// The pieces of the file to write. A file with no partition table at
    /// 0x8000, or with no NVS partition inside it, is one piece at 0, as before.
    /// </summary>
    public static IReadOnlyList<Piece> Pieces(ReadOnlySpan<byte> image)
    {
        var skip = new List<(int Start, int End)>();
        for (int at = TableOffset; at + EntrySize <= Math.Min(image.Length, TableOffset + TableSize); at += EntrySize)
        {
            var e = image.Slice(at, EntrySize);
            if (e[0] != 0xAA || e[1] != 0x50) break;     // 0xEBEB (the table's MD5) or blank ends the table
            if (e[2] != DataType || (e[3] != NvsSubtype && e[3] != NvsKeysSubtype)) continue;
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(e.Slice(4, 4));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(e.Slice(8, 4));
            if (offset >= (uint)image.Length || size == 0) continue;
            skip.Add(((int)offset, (int)Math.Min((long)offset + size, image.Length)));
        }
        skip.Sort();

        var pieces = new List<Piece>();
        int from = 0;
        foreach (var (start, end) in skip)
        {
            if (start > from) pieces.Add(new Piece(from, start - from));
            from = Math.Max(from, end);
        }
        if (from < image.Length) pieces.Add(new Piece(from, image.Length - from));
        return pieces;
    }

    /// <summary>
    /// Writes each piece of the file to its own file in the folder and returns
    /// the flash addresses with the files, ready for esptool.
    /// </summary>
    public static IReadOnlyList<(int Offset, string Path)> Split(string firmware, string folder)
    {
        var image = File.ReadAllBytes(firmware);
        var pieces = Pieces(image);
        if (pieces.Count == 1 && pieces[0].Offset == 0) return new[] { (0, firmware) };
        Directory.CreateDirectory(folder);
        var parts = new List<(int, string)>();
        foreach (var p in pieces)
        {
            var path = Path.Combine(folder, $"part-0x{p.Offset:x}.bin");
            File.WriteAllBytes(path, image.AsSpan(p.Offset, p.Length).ToArray());
            parts.Add((p.Offset, path));
        }
        return parts;
    }
}
