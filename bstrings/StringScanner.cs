using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace bstrings;

internal readonly struct Hit : IEquatable<Hit>
{
    public Hit(string text, long offset, char encoding)
    {
        Text = text;
        Offset = offset;
        Encoding = encoding;
    }

    public string Text { get; }
    public long Offset { get; }
    public char Encoding { get; }

    public bool Equals(Hit other) =>
        Offset == other.Offset && Encoding == other.Encoding && string.Equals(Text, other.Text);

    public override bool Equals(object obj) => obj is Hit other && Equals(other);

    public override int GetHashCode() => unchecked((Text.GetHashCode() * 397) ^ Offset.GetHashCode() ^ Encoding);
}

/// <summary>
///     Finds runs of characters in a range in one encoding of a byte stream that is fed to it in chunks.
///     A run that reaches the end of a chunk is carried over to the next chunk so strings are never split
/// </summary>
internal sealed class StringScanner
{
    private readonly Decoder _decoder;
    private readonly Encoding _encoding;
    private readonly bool[] _inRange;
    private readonly char _label;
    private readonly int _maxCarry;
    private readonly Regex _regex;
    private readonly int _skip;
    private readonly int _width;
    private readonly bool _withOffsets;

    private long _bufferOffset;
    private char[] _carry = new char[0];
    private int _carryLength;
    private long _charBase;
    private int _cursorIndex;
    private long _cursorOffset;
    private bool _started;

    /// <summary>
    ///     For each UTF-16 code unit, whether it matches the character range (e.g. [\x20-\x7E]) on its own
    /// </summary>
    public static bool[] BuildRangeTable(string range)
    {
        var single = new Regex($"^(?:{range})\\z", RegexOptions.CultureInvariant);
        var table = new bool[char.MaxValue + 1];
        for (var c = 0; c <= char.MaxValue; c++)
        {
            table[c] = single.IsMatch(((char)c).ToString());
        }

        return table;
    }

    /// <param name="encoding">Encoding of the strings</param>
    /// <param name="skip">Bytes to skip at the start of the stream (1 to find UTF-16 strings at odd offsets)</param>
    /// <param name="label">Encoding label used with offsets</param>
    /// <param name="range">Regex character range, such as [\x20-\x7E]</param>
    /// <param name="inRange">Lookup built by BuildRangeTable for range</param>
    /// <param name="minLength">Minimum string length</param>
    /// <param name="maxLength">Maximum string length, or -1 for unlimited</param>
    /// <param name="withOffsets">Whether to work out the file offset of each hit</param>
    /// <param name="maxCarry">A run longer than this many characters is not carried over to the next chunk</param>
    public StringScanner(Encoding encoding, int skip, char label, string range, bool[] inRange, int minLength,
        int maxLength, bool withOffsets, int maxCarry)
    {
        _encoding = encoding;
        _decoder = encoding.GetDecoder();
        _skip = skip;
        _label = label;
        _inRange = inRange;
        _withOffsets = withOffsets;
        _maxCarry = maxCarry;
        // Bytes per char when that is fixed, otherwise 0 and offsets are worked out with GetByteCount
        _width = encoding is UnicodeEncoding ? 2 : encoding.IsSingleByte ? 1 : 0;

        var quantifier = maxLength == -1 ? $"{{{minLength},}}" : $"{{{minLength},{maxLength}}}";
        _regex = new Regex(range + quantifier, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    /// <summary>
    ///     Prepares the scanner for a new stream
    /// </summary>
    public void Reset()
    {
        _decoder.Reset();
        _carryLength = 0;
        _charBase = 0;
        _bufferOffset = 0;
        _started = false;
    }

    /// <summary>
    ///     Scans the next chunk of the stream
    /// </summary>
    /// <param name="data">Chunk</param>
    /// <param name="count">Number of bytes in data</param>
    /// <param name="final">True for the last chunk of the stream</param>
    /// <param name="buffer">Work buffer, shared between scanners and grown as needed</param>
    /// <param name="hits">Hits are added here</param>
    public void Process(byte[] data, int count, bool final, ref char[] buffer, ISet<Hit> hits)
    {
        var start = 0;
        if (!_started)
        {
            _started = true;
            start = Math.Min(_skip, count);
        }

        var needed = _carryLength + _encoding.GetMaxCharCount(count - start) + 2;
        if (buffer.Length < needed)
        {
            buffer = new char[needed];
        }

        Array.Copy(_carry, buffer, _carryLength);
        var total = _carryLength + _decoder.GetChars(data, start, count - start, buffer, _carryLength, final);

        // Hold back a run of in-range characters at the end, as it may continue in the next chunk
        var end = total;
        if (!final)
        {
            var tail = total;
            while (tail > 0 && _inRange[buffer[tail - 1]])
            {
                tail -= 1;
            }

            if (total - tail < _maxCarry)
            {
                end = tail;
            }
        }

        _cursorIndex = 0;
        _cursorOffset = _bufferOffset;

#if NET9_0_OR_GREATER
        var matches = _regex.EnumerateMatches(buffer.AsSpan(0, end));
        while (matches.MoveNext())
        {
            AddHit(buffer, matches.Current.Index, matches.Current.Length, hits);
        }
#else
        var text = new string(buffer, 0, end);
        for (var match = _regex.Match(text); match.Success; match = match.NextMatch())
        {
            AddHit(buffer, match.Index, match.Length, hits);
        }
#endif

        if (_withOffsets && _width == 0)
        {
            _bufferOffset = OffsetOf(buffer, end);
        }

        _carryLength = total - end;
        if (_carry.Length < _carryLength)
        {
            _carry = new char[Math.Max(_carryLength, _carry.Length * 2)];
        }

        Array.Copy(buffer, end, _carry, 0, _carryLength);
        _charBase += end;
    }

    private void AddHit(char[] buffer, int index, int length, ISet<Hit> hits)
    {
        // Same as string.Trim(), without making the untrimmed string
        var lead = 0;
        while (lead < length && char.IsWhiteSpace(buffer[index + lead]))
        {
            lead += 1;
        }

        var trail = length;
        while (trail > lead && char.IsWhiteSpace(buffer[index + trail - 1]))
        {
            trail -= 1;
        }

        if (trail == lead)
        {
            return;
        }

        var text = new string(buffer, index + lead, trail - lead);

        hits.Add(_withOffsets ? new Hit(text, OffsetOf(buffer, index + lead), _label) : new Hit(text, 0, ' '));
    }

    /// <summary>
    ///     File offset of buffer[index]. For variable width encodings, index must not go down within a chunk
    /// </summary>
    private long OffsetOf(char[] buffer, int index)
    {
        if (_width > 0)
        {
            return _skip + _width * (_charBase + index);
        }

        _cursorOffset += _encoding.GetByteCount(buffer, _cursorIndex, index - _cursorIndex);
        _cursorIndex = index;
        return _cursorOffset;
    }
}
