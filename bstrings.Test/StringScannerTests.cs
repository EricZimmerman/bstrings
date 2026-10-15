using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using NUnit.Framework;

namespace bstrings.Test;

[TestFixture]
public class StringScannerTests
{
    private const string AsciiRange = "[\x20-\x7E]";
    private const string UnicodeRange = "[\u0020-\u007E]";

    private static readonly Encoding Utf16 = new UnicodeEncoding(false, false);

    static StringScannerTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    ///     Feeds data to the scanners in chunks the way Program.Search does. Like Search, this relies on a HashSet
    ///     that is only added to enumerating in insertion order
    /// </summary>
    private static List<Hit> Scan(byte[] data, int chunkSize, IReadOnlyList<StringScanner> scanners)
    {
        var hits = new HashSet<Hit>();
        var buffer = new char[0];
        var chunk = new byte[chunkSize];

        foreach (var scanner in scanners)
        {
            scanner.Reset();
        }

        var position = 0;
        while (true)
        {
            var count = Math.Min(chunkSize, data.Length - position);
            Array.Copy(data, position, chunk, 0, count);
            position += count;
            var final = count < chunkSize;

            foreach (var scanner in scanners)
            {
                scanner.Process(chunk, count, final, ref buffer, hits);
            }

            if (final)
            {
                return hits.ToList();
            }
        }
    }

    private static List<StringScanner> AllScanners(int min = 3, int max = -1, bool offsets = true,
        int maxCarry = int.MaxValue, int codePage = 1252)
    {
        var uniTable = StringScanner.BuildRangeTable(UnicodeRange);
        return new List<StringScanner>
        {
            new StringScanner(Utf16, 0, 'U', UnicodeRange, uniTable, min, max, offsets, maxCarry),
            new StringScanner(Utf16, 1, 'U', UnicodeRange, uniTable, min, max, offsets, maxCarry),
            new StringScanner(Encoding.GetEncoding(codePage), 0, 'A', AsciiRange,
                StringScanner.BuildRangeTable(AsciiRange), min, max, offsets, maxCarry)
        };
    }

    /// <summary>
    ///     Random bytes that are never in the default ranges, so the only strings are the ones put there
    /// </summary>
    private static byte[] Noise(int size, int seed)
    {
        var random = new Random(seed);
        var pool = Enumerable.Range(0, 256).Where(b => b < 0x20 || b > 0x7E).Select(b => (byte)b).ToArray();
        var data = new byte[size];
        for (var i = 0; i < size; i++)
        {
            data[i] = pool[random.Next(pool.Length)];
        }

        return data;
    }

    private static void Put(byte[] data, int offset, byte[] bytes)
    {
        Array.Copy(bytes, 0, data, offset, bytes.Length);
    }

    private static string Format(Hit hit) => $"{hit.Text}@0x{hit.Offset:X}({hit.Encoding})";

    [Test]
    public void FindsAsciiAndUnicodeAtEvenAndOddOffsets()
    {
        var data = Noise(4096, 1);
        Put(data, 0x10, Encoding.ASCII.GetBytes("HelloWorld"));
        Put(data, 0x100, Utf16.GetBytes("EvenUnicode"));
        Put(data, 0x201, Utf16.GetBytes("OddUnicode"));

        Scan(data, data.Length, AllScanners()).Select(Format).Should().BeEquivalentTo(
            "HelloWorld@0x10(A)", "EvenUnicode@0x100(U)", "OddUnicode@0x201(U)");
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(7)]
    [TestCase(64)]
    [TestCase(1000)]
    public void StringsAcrossChunkBoundariesAreFoundWhole(int chunkSize)
    {
        var data = Noise(5000, 2);
        var ascii = new string('L', 120) + "-ascii-across-" + new string('R', 150);
        var even = new string('U', 40) + "-utf16-even-" + new string('V', 60);
        var odd = new string('O', 40) + "-utf16-odd-" + new string('W', 60);
        Put(data, 900, Encoding.ASCII.GetBytes(ascii));
        Put(data, 1800, Utf16.GetBytes(even));
        Put(data, 2901, Utf16.GetBytes(odd));

        Scan(data, chunkSize, AllScanners()).Select(Format).Should().BeEquivalentTo(
            $"{ascii}@0x{900:X}(A)", $"{even}@0x{1800:X}(U)", $"{odd}@0x{2901:X}(U)");
    }

    [Test]
    public void ResultsDoNotDependOnChunkSize()
    {
        // Real text mixed with binary, so there are lots of strings of every length, some touching each other
        var random = new Random(3);
        var data = new byte[64 * 1024];
        random.NextBytes(data);
        var words = new[] { "alpha", "be", "gamma delta", "  padded  ", "x", "C:\\Windows\\System32\\cmd.exe" };
        for (var i = 0; i < 400; i++)
        {
            var word = words[random.Next(words.Length)];
            var bytes = random.Next(2) == 0 ? Encoding.ASCII.GetBytes(word) : Utf16.GetBytes(word);
            Put(data, random.Next(data.Length - bytes.Length), bytes);
        }

        var expected = Scan(data, data.Length + 1, AllScanners()).Select(Format).OrderBy(t => t, StringComparer.Ordinal)
            .ToList();
        expected.Should().NotBeEmpty();

        foreach (var chunkSize in new[] { 1, 2, 3, 5, 16, 333, 4096, data.Length })
        {
            Scan(data, chunkSize, AllScanners()).Select(Format).OrderBy(t => t, StringComparer.Ordinal).Should()
                .Equal(expected, $"chunk size {chunkSize} should give the same hits as one chunk");
        }
    }

    [Test]
    public void OffsetsPointAtTheStringBytes()
    {
        var random = new Random(4);
        var data = new byte[256 * 1024];
        random.NextBytes(data);
        for (var i = 0; i < 300; i++)
        {
            var bytes = Utf16.GetBytes("unicode string " + i);
            Put(data, random.Next(data.Length - bytes.Length), bytes);
        }

        var hits = Scan(data, 10_000, AllScanners());

        hits.Should().HaveCountGreaterThan(300);
        foreach (var hit in hits)
        {
            var bytes = hit.Encoding == 'U' ? Utf16.GetBytes(hit.Text) : Encoding.GetEncoding(1252).GetBytes(hit.Text);
            data.Skip((int)hit.Offset).Take(bytes.Length).Should().Equal(bytes, Format(hit));
        }
    }

    [Test]
    public void LeadingWhitespaceIsTrimmedAndAddedToTheOffset()
    {
        var data = Noise(256, 5);
        Put(data, 0x20, Encoding.ASCII.GetBytes("   padded   "));
        Put(data, 0x60, Utf16.GetBytes("  wide "));

        Scan(data, 7, AllScanners()).Select(Format).Should().BeEquivalentTo("padded@0x23(A)", "wide@0x64(U)");
    }

    [Test]
    public void WhitespaceOnlyRunsAreNotHits()
    {
        var data = Noise(64, 6);
        Put(data, 8, Encoding.ASCII.GetBytes("      "));

        Scan(data, data.Length, AllScanners()).Should().BeEmpty();
    }

    [Test]
    public void ShortRunsAreIgnored()
    {
        var data = Noise(64, 7);
        Put(data, 8, Encoding.ASCII.GetBytes("ab"));
        Put(data, 20, Encoding.ASCII.GetBytes("abcd"));

        Scan(data, data.Length, AllScanners(4)).Select(t => t.Text).Should().Equal("abcd");
    }

    [TestCase(1)]
    [TestCase(4)]
    [TestCase(9)]
    [TestCase(100)]
    public void MaximumLengthSplitsRunsTheSameWayWhateverTheChunkSize(int chunkSize)
    {
        var data = Noise(100, 8);
        Put(data, 10, Encoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwxy")); // 25 characters

        Scan(data, chunkSize, AllScanners(3, 10)).Select(Format).Should().Equal(
            "abcdefghij@0xA(A)", "klmnopqrst@0x14(A)", "uvwxy@0x1E(A)");
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(1000)]
    public void MultiByteCodePageOffsetsAreExact(int chunkSize)
    {
        // Shift-JIS: kanji are two bytes, so character index and byte offset differ after them,
        // and small chunks split the two bytes of a kanji
        var sjis = Encoding.GetEncoding(932);
        var data = new List<byte>();
        data.AddRange(new byte[] { 1, 1, 1 });
        data.AddRange(sjis.GetBytes("日本語"));
        data.Add(1);
        var first = data.Count;
        data.AddRange(sjis.GetBytes("ascii-after-kanji"));
        data.Add(1);
        data.AddRange(sjis.GetBytes("テスト"));
        var second = data.Count;
        data.AddRange(sjis.GetBytes("second-one"));
        data.Add(1);

        var ascii = new StringScanner(sjis, 0, 'A', AsciiRange, StringScanner.BuildRangeTable(AsciiRange), 3, -1,
            true, int.MaxValue);

        Scan(data.ToArray(), chunkSize, new[] { ascii }).Select(Format).Should().Equal(
            $"ascii-after-kanji@0x{first:X}(A)", $"second-one@0x{second:X}(A)");
    }

    [Test]
    public void WithoutOffsetsTheSameStringIsOneHit()
    {
        var data = Noise(256, 9);
        Put(data, 0x10, Encoding.ASCII.GetBytes("repeat"));
        Put(data, 0x40, Encoding.ASCII.GetBytes("repeat"));
        Put(data, 0x80, Utf16.GetBytes("repeat"));

        Scan(data, data.Length, AllScanners(offsets: false)).Select(t => t.Text).Should().Equal("repeat");
        Scan(data, data.Length, AllScanners()).Should().HaveCount(3);
    }

    [Test]
    public void ResetStartsANewStream()
    {
        var scanners = AllScanners();
        var data = Noise(64, 10);
        Put(data, 60, Encoding.ASCII.GetBytes("tail"));

        // The first stream ends with a run that would be carried over if the stream continued
        var hits = new HashSet<Hit>();
        var buffer = new char[0];
        foreach (var scanner in scanners)
        {
            scanner.Process(data, data.Length, false, ref buffer, hits);
        }

        hits.Should().BeEmpty();

        var second = Noise(64, 11);
        Put(second, 0, Encoding.ASCII.GetBytes("head"));

        Scan(second, second.Length, scanners).Select(Format).Should().Equal("head@0x0(A)");
    }

    [Test]
    public void RunsLongerThanMaxCarryAreReportedInPieces()
    {
        var data = Noise(100, 12);
        var text = new string('z', 60);
        Put(data, 20, Encoding.ASCII.GetBytes(text));

        var ascii = new StringScanner(Encoding.GetEncoding(1252), 0, 'A', AsciiRange,
            StringScanner.BuildRangeTable(AsciiRange), 3, -1, true, 25);

        var hits = Scan(data, 25, new[] { ascii });

        string.Concat(hits.Select(t => t.Text)).Should().Be(text);
        hits.Should().HaveCountGreaterThan(1);
    }

    [Test]
    public void EmptyStreamHasNoHits()
    {
        Scan(new byte[0], 16, AllScanners()).Should().BeEmpty();
    }

    [Test]
    public void BuildRangeTableMatchesTheRange()
    {
        var table = StringScanner.BuildRangeTable("[\x20-\x37]");

        table.Count(t => t).Should().Be(0x18);
        table[0x20].Should().BeTrue();
        table[0x37].Should().BeTrue();
        table[0x38].Should().BeFalse();
        table[0x1F].Should().BeFalse();
    }

    [Test]
    public void InvalidRangeThrows()
    {
        Action act = () => StringScanner.BuildRangeTable("[\x20-");

        act.Should().Throw<ArgumentException>();
    }
}
