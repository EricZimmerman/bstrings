using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using NUnit.Framework;

namespace bstrings.Test;

/// <summary>
///     Runs the built bstrings.dll (copied next to the tests by the project reference) as a separate process
/// </summary>
[TestFixture]
public class CliTests
{
    private string _dir;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bstrings-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_dir, true);
    }

    private sealed class Result
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }

        /// <summary>
        ///     Non-empty stdout lines (with -q these are the hits)
        /// </summary>
        public string[] Lines => Output.Split('\n').Select(t => t.TrimEnd('\r')).Where(t => t.Length > 0).ToArray();
    }

    /// <param name="stdin">Bytes to pipe to stdin. When null, stdin is still redirected but closed straight away</param>
    private static Result Run(byte[] stdin, params string[] args)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(Path.Combine(TestContext.CurrentContext.TestDirectory, "bstrings.dll"));
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        if (stdin != null)
        {
            process.StandardInput.BaseStream.Write(stdin, 0, stdin.Length);
        }

        process.StandardInput.Close();

        process.WaitForExit(120_000).Should().BeTrue("bstrings should finish");
        stderr.Result.Should().BeEmpty();

        return new Result { ExitCode = process.ExitCode, Output = stdout.Result };
    }

    private static Result Run(params string[] args) => Run(null, args);

    private string WriteFile(string name, params (int Offset, byte[] Bytes)[] strings)
    {
        var size = strings.Length == 0 ? 64 : strings.Max(t => t.Offset + t.Bytes.Length) + 16;
        var data = new byte[size];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = 0x01;
        }

        foreach (var (offset, bytes) in strings)
        {
            Array.Copy(bytes, 0, data, offset, bytes.Length);
        }

        var path = Path.Combine(_dir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data);
        return path;
    }

    private static byte[] A(string s) => Encoding.ASCII.GetBytes(s);
    private static byte[] U(string s) => Encoding.Unicode.GetBytes(s);

    [Test]
    public void FileWithRedirectedStdinIsSearched()
    {
        // Issue #17: callers such as Go's exec.Command redirect stdin, which used to make -f fail
        var file = WriteFile("a.bin", (8, A("HelloWorld")));

        var result = Run("-f", file, "-q");

        result.ExitCode.Should().Be(0);
        result.Lines.Should().Equal("HelloWorld");
    }

    [Test]
    public void StdinIsSearchedWhenNoFileOrDirectoryIsGiven()
    {
        var result = Run(Encoding.ASCII.GetBytes("\x01\x01piped-in-text\x01"), "-q");

        result.ExitCode.Should().Be(0);
        result.Lines.Should().Equal("piped-in-text");
    }

    [Test]
    public void MissingFileExitsWithOne()
    {
        Run("-f", Path.Combine(_dir, "nope.bin"), "-q").ExitCode.Should().Be(1);
    }

    [Test]
    public void MissingDirectoryExitsWithOne()
    {
        Run("-d", Path.Combine(_dir, "nope"), "-q").ExitCode.Should().Be(1);
    }

    [Test]
    public void InvalidCodePageExitsWithOne()
    {
        var file = WriteFile("a.bin", (8, A("HelloWorld")));

        Run("-f", file, "--cp", "99999", "-q").ExitCode.Should().Be(1);
    }

    [Test]
    public void InvalidRangeExitsWithOne()
    {
        var file = WriteFile("a.bin", (8, A("HelloWorld")));

        Run("-f", file, "--ar", "[\\x20-", "-q").ExitCode.Should().Be(1);
    }

    [Test]
    public void MissingStringsFileExitsWithOne()
    {
        var file = WriteFile("a.bin", (8, A("HelloWorld")));

        Run("-f", file, "--fs", Path.Combine(_dir, "nope.txt"), "-q").ExitCode.Should().Be(1);
    }

    [Test]
    public void RandomBytesExitWithZero()
    {
        // Any file is valid input for bstrings: 1 is only for a failure of the tool, never for the file's contents
        var data = new byte[64 * 1024];
        new Random(1).NextBytes(data);
        var file = Path.Combine(_dir, "random.bin");
        File.WriteAllBytes(file, data);

        Run("-f", file, "-q", "-s").ExitCode.Should().Be(0);
    }

    [Test]
    public void OutputThatCannotBeWrittenExitsWithOne()
    {
        var file = WriteFile("a.bin", (8, A("HelloWorld")));

        // the -o folder is an existing file
        Run("-f", file, "-q", "-o", Path.Combine(file, "out.txt")).ExitCode.Should().Be(1);
    }

    [Test]
    public void OffsetsAreFileOffsetsWithEncoding()
    {
        var file = WriteFile("a.bin", (0x10, A("HelloWorld")), (0x100, U("EvenUnicode")), (0x201, U("OddUnicode")),
            (0x300, A("   padded")));

        var result = Run("-f", file, "--off", "-q");

        result.Lines.Should().BeEquivalentTo("HelloWorld\t0x10 (A)", "EvenUnicode\t0x100 (U)", "OddUnicode\t0x201 (U)",
            "padded\t0x303 (A)");
    }

    [Test]
    public void OutputDoesNotDependOnChunkSize()
    {
        // Strings straddle the 1 MB chunk boundaries
        const int mb = 1024 * 1024;
        var file = WriteFile("big.bin", (mb - 150, A(new string('L', 120) + "-across-" + new string('R', 150))),
            (2 * mb - 100, U(new string('U', 40) + "-utf16-" + new string('V', 60))),
            (3 * mb - 101, U(new string('O', 40) + "-odd-" + new string('W', 60))),
            (3 * mb + 400, A("small")));

        var chunked = Run("-f", file, "-b", "1", "--off", "-q").Lines;
        var whole = Run("-f", file, "--off", "-q").Lines;

        chunked.Should().HaveCount(4);
        chunked.Should().BeEquivalentTo(whole);
    }

    [Test]
    public void AnchoredBuiltInPatternWorksWithOffsets()
    {
        // The offset used to be part of the text that filters ran against, so ^...$ patterns never matched
        var file = WriteFile("a.bin", (8, A("QUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVo=")));

        var result = Run("-f", file, "--lr", "b64", "--off", "-q");

        result.Lines.Should().Equal("QUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVo=\t0x8 (A)");
    }

    [Test]
    public void HitMatchingSeveralFiltersIsWrittenOnce()
    {
        var file = WriteFile("a.bin", (8, A("HelloWorld")), (40, A("unrelated")));
        var strings = Path.Combine(_dir, "strings.txt");
        File.WriteAllLines(strings, new[] { "hello", "world", "" });

        var result = Run("-f", file, "--ls", "ello", "--fs", strings, "--lr", "w.rld", "-q");

        result.Lines.Should().Equal("HelloWorld");
    }

    [Test]
    public void RegexFilterTreatsSpacesLiterally()
    {
        var file = WriteFile("a.bin", (8, A("foo bar")), (40, A("foobar")));

        Run("-f", file, "--lr", "foo bar", "-q").Lines.Should().Equal("foo bar");
    }

    [Test]
    public void RegexOnlyWritesEachMatch()
    {
        var file = WriteFile("a.bin", (8, A("ids 1234 and 5678 here")));

        var result = Run("-f", file, "--lr", @"\d{4}", "--ro", "--off", "-q");

        result.Lines.Should().Equal("1234\t~0x8 (A)", "5678\t~0x8 (A)");
    }

    [Test]
    public void MaximumLengthEqualToMinimumIsUsed()
    {
        var file = WriteFile("a.bin", (8, A("abcdefgh")));

        Run("-f", file, "-m", "4", "-x", "4", "-q").Lines.Should().Equal("abcd", "efgh");
    }

    [Test]
    public void SortsAlphabeticallyAndByLength()
    {
        var file = WriteFile("a.bin", (8, A("ccc")), (20, A("a-long-one")), (40, A("bb-medium")));

        Run("-f", file, "--sa", "-q").Lines.Where(t => !t.EndsWith("...")).Should()
            .Equal("a-long-one", "bb-medium", "ccc");
        Run("-f", file, "--sl", "-q").Lines.Where(t => !t.EndsWith("...")).Should()
            .Equal("ccc", "bb-medium", "a-long-one");
    }

    [Test]
    public void OutputFileIsAppendedTo()
    {
        var file = WriteFile("a.bin", (8, A("HelloWorld")));
        var output = Path.Combine(_dir, "out", "hits.txt");

        Run("-f", file, "-q", "-s", "-o", output).ExitCode.Should().Be(0);
        Run("-f", file, "-q", "-s", "-o", output).ExitCode.Should().Be(0);

        File.ReadAllLines(output).Should().Equal("HelloWorld", "HelloWorld");
    }

    [Test]
    public void ReallyQuietWritesNoHitsToTheConsole()
    {
        var file = WriteFile("a.bin", (8, A("HelloWorld")));

        Run("-f", file, "-q", "-s").Lines.Should().BeEmpty();
    }

    [Test]
    public void DirectoryIsSearchedRecursivelyWithMask()
    {
        WriteFile("a.bin", (8, A("in-root")));
        WriteFile(Path.Combine("sub", "b.dat"), (8, A("in-subfolder")));
        WriteFile(Path.Combine("sub", "c.bin"), (8, A("other-ext")));
        File.WriteAllBytes(Path.Combine(_dir, "empty.dat"), new byte[0]);

        var all = Run("-d", _dir, "-q");
        all.ExitCode.Should().Be(0);
        all.Lines.Should().BeEquivalentTo("in-root", "in-subfolder", "other-ext");

        Run("-d", _dir, "--mask", "*.dat", "-q").Lines.Should().Equal("in-subfolder");
    }

    [Test]
    public void ListsBuiltInPatterns()
    {
        var result = Run("-p");

        result.ExitCode.Should().Be(0);
        foreach (var name in BuiltInPatterns.Patterns.Keys)
        {
            result.Output.Should().Contain(name);
        }
    }

    [Test]
    public void EmptyStdinFinishesCleanly()
    {
        // With no -f or -d and a redirected stdin, stdin is searched even when it is empty
        Run("-q").ExitCode.Should().Be(0);
    }
}
