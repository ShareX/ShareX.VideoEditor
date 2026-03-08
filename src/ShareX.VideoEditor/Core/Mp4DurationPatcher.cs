#region License Information (GPL v3)

/*
    ShareX.VideoEditor - The UI-agnostic Video Editor library for ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.Buffers.Binary;

namespace ShareX.VideoEditor.Core;

internal static class Mp4DurationPatcher
{
    private const uint ExactMovieTimescale = 1_000_000;

    private static readonly HashSet<string> ContainerBoxes =
    [
        "edts",
        "mdia",
        "moov",
        "trak"
    ];

    public static void PatchExactDuration(string filePath, TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));

        ulong exactDuration = checked((ulong)Math.Round(
            duration.TotalSeconds * ExactMovieTimescale,
            MidpointRounding.AwayFromZero));

        var patchMap = new Mp4PatchMap();

        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        ReadBoxes(stream, 0, stream.Length, patchMap);

        if (patchMap.MovieHeader is null)
        {
            throw new InvalidOperationException("MP4 movie header was not found.");
        }

        WriteUInt32(stream, patchMap.MovieHeader.Value.TimescaleOffset, ExactMovieTimescale);
        WriteDuration(stream, patchMap.MovieHeader.Value.DurationField, exactDuration);

        foreach (DurationField trackHeader in patchMap.TrackHeaders)
        {
            WriteDuration(stream, trackHeader, exactDuration);
        }

        foreach (DurationField editListEntry in patchMap.EditLists)
        {
            WriteDuration(stream, editListEntry, exactDuration);
        }

        foreach (MediaHeaderField mediaHeader in patchMap.MediaHeaders)
        {
            ulong mediaDuration = checked((ulong)Math.Round(
                duration.TotalSeconds * mediaHeader.Timescale,
                MidpointRounding.AwayFromZero));

            WriteDuration(stream, mediaHeader.DurationField, mediaDuration);
        }
    }

    private static void ReadBoxes(
        FileStream stream,
        long start,
        long end,
        Mp4PatchMap patchMap)
    {
        stream.Position = start;

        while (stream.Position < end)
        {
            long boxStart = stream.Position;
            uint smallSize = ReadUInt32(stream);
            string boxType = ReadType(stream);
            long headerSize = 8;
            long boxSize = smallSize;

            if (smallSize == 1)
            {
                boxSize = checked((long)ReadUInt64(stream));
                headerSize = 16;
            }
            else if (smallSize == 0)
            {
                boxSize = end - boxStart;
            }

            if (boxSize < headerSize)
            {
                throw new InvalidOperationException($"Invalid MP4 box size for '{boxType}'.");
            }

            long boxDataStart = boxStart + headerSize;
            long boxEnd = boxStart + boxSize;

            if (boxEnd > end)
            {
                throw new InvalidOperationException($"MP4 box '{boxType}' exceeds its parent bounds.");
            }

            stream.Position = boxDataStart;

            switch (boxType)
            {
                case "elst":
                    ReadEditList(stream, patchMap);
                    break;
                case "mdhd":
                    patchMap.MediaHeaders.Add(ReadMediaHeader(stream));
                    break;
                case "mvhd":
                    patchMap.MovieHeader = ReadMovieHeader(stream);
                    break;
                case "tkhd":
                    patchMap.TrackHeaders.Add(ReadTrackHeader(stream));
                    break;
            }

            if (ContainerBoxes.Contains(boxType))
            {
                ReadBoxes(stream, boxDataStart, boxEnd, patchMap);
            }

            stream.Position = boxEnd;
        }
    }

    private static MovieHeaderField ReadMovieHeader(FileStream stream)
    {
        byte version = ReadByte(stream);
        stream.Position += 3;

        if (version == 1)
        {
            stream.Position += 16;
            long timescaleOffset = stream.Position;
            _ = ReadUInt32(stream);
            long durationOffset = stream.Position;
            _ = ReadUInt64(stream);

            return new MovieHeaderField(
                timescaleOffset,
                new DurationField(durationOffset, version));
        }

        stream.Position += 8;
        long version0TimescaleOffset = stream.Position;
        _ = ReadUInt32(stream);
        long version0DurationOffset = stream.Position;
        _ = ReadUInt32(stream);

        return new MovieHeaderField(
            version0TimescaleOffset,
            new DurationField(version0DurationOffset, version));
    }

    private static DurationField ReadTrackHeader(FileStream stream)
    {
        byte version = ReadByte(stream);
        stream.Position += 3;

        if (version == 1)
        {
            stream.Position += 28;
            return new DurationField(stream.Position, version);
        }

        stream.Position += 20;
        return new DurationField(stream.Position, version);
    }

    private static MediaHeaderField ReadMediaHeader(FileStream stream)
    {
        byte version = ReadByte(stream);
        stream.Position += 3;

        if (version == 1)
        {
            stream.Position += 16;
            uint timescale = ReadUInt32(stream);
            long durationOffset = stream.Position;
            _ = ReadUInt64(stream);

            return new MediaHeaderField(timescale, new DurationField(durationOffset, version));
        }

        stream.Position += 8;
        uint version0Timescale = ReadUInt32(stream);
        long version0DurationOffset = stream.Position;
        _ = ReadUInt32(stream);

        return new MediaHeaderField(
            version0Timescale,
            new DurationField(version0DurationOffset, version));
    }

    private static void ReadEditList(FileStream stream, Mp4PatchMap patchMap)
    {
        byte version = ReadByte(stream);
        stream.Position += 3;
        uint entryCount = ReadUInt32(stream);

        if (entryCount == 0)
        {
            return;
        }

        patchMap.EditLists.Add(new DurationField(stream.Position, version));
    }

    private static void WriteDuration(FileStream stream, DurationField field, ulong value)
    {
        if (field.Version == 1)
        {
            WriteUInt64(stream, field.Offset, value);
            return;
        }

        if (value > uint.MaxValue)
        {
            throw new InvalidOperationException("The target duration exceeds the MP4 version 0 limit.");
        }

        WriteUInt32(stream, field.Offset, (uint)value);
    }

    private static byte ReadByte(FileStream stream)
    {
        int value = stream.ReadByte();
        if (value < 0)
        {
            throw new EndOfStreamException("Unexpected end of MP4 stream.");
        }

        return (byte)value;
    }

    private static uint ReadUInt32(FileStream stream)
    {
        Span<byte> buffer = stackalloc byte[4];
        FillBuffer(stream, buffer);
        return BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    private static ulong ReadUInt64(FileStream stream)
    {
        Span<byte> buffer = stackalloc byte[8];
        FillBuffer(stream, buffer);
        return BinaryPrimitives.ReadUInt64BigEndian(buffer);
    }

    private static string ReadType(FileStream stream)
    {
        Span<byte> buffer = stackalloc byte[4];
        FillBuffer(stream, buffer);
        return System.Text.Encoding.ASCII.GetString(buffer);
    }

    private static void WriteUInt32(FileStream stream, long offset, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        stream.Position = offset;
        stream.Write(buffer);
    }

    private static void WriteUInt64(FileStream stream, long offset, ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
        stream.Position = offset;
        stream.Write(buffer);
    }

    private static void FillBuffer(FileStream stream, Span<byte> buffer)
    {
        int bytesRead = stream.Read(buffer);
        if (bytesRead != buffer.Length)
        {
            throw new EndOfStreamException("Unexpected end of MP4 stream.");
        }
    }

    private readonly record struct DurationField(long Offset, byte Version);

    private readonly record struct MediaHeaderField(uint Timescale, DurationField DurationField);

    private readonly record struct MovieHeaderField(long TimescaleOffset, DurationField DurationField);

    private sealed class Mp4PatchMap
    {
        public MovieHeaderField? MovieHeader { get; set; }

        public List<DurationField> EditLists { get; } = [];

        public List<MediaHeaderField> MediaHeaders { get; } = [];

        public List<DurationField> TrackHeaders { get; } = [];
    }
}
