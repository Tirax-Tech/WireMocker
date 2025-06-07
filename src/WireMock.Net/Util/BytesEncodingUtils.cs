// Copyright © WireMock.Net

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WireMock.Util;

/// <summary>
/// Based on:
/// http://utf8checker.codeplex.com
/// https://github.com/0x53A/Mvvm/blob/master/src/Mvvm/src/Utf8Checker.cs
///
/// References:
/// http://anubis.dkuug.dk/JTC1/SC2/WG2/docs/n1335
/// http://www.cl.cam.ac.uk/~mgk25/ucs/ISO-10646-UTF-8.html
/// http://www.unicode.org/versions/corrigendum1.html
/// http://www.ietf.org/rfc/rfc2279.txt
/// </summary>
static class BytesEncodingUtils
{
    public static readonly Encoding DefaultEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Tries the get the Encoding from an array of bytes.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    public static Encoding? TryGetEncoding(byte[] bytes)
        => StartsWith(bytes, [0xff, 0xfe, 0x00, 0x00]) ? Encoding.UTF32
           : StartsWith(bytes, [0xfe, 0xff])           ? Encoding.BigEndianUnicode
           : StartsWith(bytes, [0xff, 0xfe])           ? Encoding.Unicode
           : StartsWith(bytes, [0xef, 0xbb, 0xbf])     ? Encoding.UTF8
           : IsUtf8(bytes, bytes.Length)               ? DefaultEncoding
           : bytes.All(b => b < 80)                    ? Encoding.ASCII
                                                         : null;

    static bool StartsWith(IEnumerable<byte> data, IReadOnlyCollection<byte> other)
    {
        byte[] arraySelf = data.Take(other.Count).ToArray();
        return other.SequenceEqual(arraySelf);
    }

    static bool IsUtf8(IReadOnlyList<byte> buffer, int length)
    {
        int position = 0;
        int bytes = 0;
        while (position < length)
        {
            if (!IsValid(buffer, position, length, ref bytes))
            {
                return false;
            }
            position += bytes;
        }
        return true;
    }

#pragma warning disable S3776 // Cognitive Complexity of methods should not be too high
    static bool IsValid(IReadOnlyList<byte> buffer, int position, int length, ref int bytes)
    {
        if (length > buffer.Count)
        {
            throw new ArgumentException("Invalid length");
        }

        if (position > length - 1)
        {
            bytes = 0;
            return true;
        }

        byte ch = buffer[position];
        if (ch <= 0x7F)
        {
            bytes = 1;
            return true;
        }

        if (ch is >= 0xc2 and <= 0xdf)
        {
            if (position >= length - 2)
            {
                bytes = 0;
                return false;
            }

            if (buffer[position + 1] < 0x80 || buffer[position + 1] > 0xbf)
            {
                bytes = 0;
                return false;
            }

            bytes = 2;
            return true;
        }

        if (ch == 0xe0)
        {
            if (position >= length - 3)
            {
                bytes = 0;
                return false;
            }

            if (buffer[position + 1] < 0xa0 || buffer[position + 1] > 0xbf ||
                buffer[position + 2] < 0x80 || buffer[position + 2] > 0xbf)
            {
                bytes = 0;
                return false;
            }

            bytes = 3;
            return true;
        }

        if (ch is >= 0xe1 and <= 0xef)
        {
            if (position >= length - 3)
            {
                bytes = 0;
                return false;
            }

            if (buffer[position + 1] < 0x80 || buffer[position + 1] > 0xbf ||
                buffer[position + 2] < 0x80 || buffer[position + 2] > 0xbf)
            {
                bytes = 0;
                return false;
            }

            bytes = 3;
            return true;
        }

        if (ch == 0xf0)
        {
            if (position >= length - 4)
            {
                bytes = 0;
                return false;
            }

            if (buffer[position + 1] < 0x90 || buffer[position + 1] > 0xbf ||
                buffer[position + 2] < 0x80 || buffer[position + 2] > 0xbf ||
                buffer[position + 3] < 0x80 || buffer[position + 3] > 0xbf)
            {
                bytes = 0;
                return false;
            }

            bytes = 4;
            return true;
        }

        if (ch == 0xf4)
        {
            if (position >= length - 4)
            {
                bytes = 0;
                return false;
            }

            if (buffer[position + 1] < 0x80 || buffer[position + 1] > 0x8f ||
                buffer[position + 2] < 0x80 || buffer[position + 2] > 0xbf ||
                buffer[position + 3] < 0x80 || buffer[position + 3] > 0xbf)
            {
                bytes = 0;
                return false;
            }

            bytes = 4;
            return true;
        }

        if (ch is >= 0xf1 and <= 0xf3)
        {
            if (position >= length - 4)
            {
                bytes = 0;
                return false;
            }

            if (buffer[position + 1] < 0x80 || buffer[position + 1] > 0xbf ||
                buffer[position + 2] < 0x80 || buffer[position + 2] > 0xbf ||
                buffer[position + 3] < 0x80 || buffer[position + 3] > 0xbf)
            {
                bytes = 0;
                return false;
            }

            bytes = 4;
            return true;
        }

        return false;
    }
}
#pragma warning restore S3776 // Cognitive Complexity of methods should not be too high