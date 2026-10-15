using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using NUnit.Framework;

namespace bstrings.Test;

[TestFixture]
public class BuiltInPatternTests
{
    // Name, a string the pattern must match, a string it must not match
    private static readonly object[] Samples =
    {
        new object[] { "guid", "{D0C1A7F3-1234-4ABC-9DEF-0123456789AB}", "D0C1A7F3-1234-4ABC-9DEF-0123456789A" },
        new object[] { "usPhone", "call 212-555-1234", "phone 12345" },
        new object[] { "unc", @"\\server\share\file.txt", @"C:\share\file.txt" },
        new object[] { "mac", "00:1A:2B:3C:4D:5E", "00:1A:2B:3C:4D" },
        new object[] { "ssn", "123-45-6789", "000-12-3456" },
        new object[] { "cc", "4111 1111 1111 1111", "4111 1111 1111 111" },
        new object[] { "ipv4", "connect 192.168.1.10 now", "999.168.1.10" },
        new object[] { "ipv6", "2001:0db8:85a3:0000:0000:8a2e:0370:7334", "1:2:3:4:5:6:7" },
        new object[] { "email", "mail bob@example.com", "bob@example" },
        new object[] { "zip", "12345-6789", "123456" },
        new object[] { "urlUser", "ftp://admin@host.example", "http://host.example" },
        new object[] { "url3986", "https://user@www.example.com:8080/path/to?q=1#frag", "www.example.com" },
        new object[] { "xml", "<b>bold</b>", "<b>bold</i>" },
        new object[] { "sid", "S-1-5-21-3623811015-3361044348-30300820-1013", "S-1-5" },
        new object[] { "win_path", @"C:\Windows\System32\cmd.exe", "Windows System32" },
        new object[] { "var_set", "OS=Windows_NT", "=Windows_NT" },
        new object[] { "reg_path", @"HKLM\Software\Microsoft\Windows", "Software Microsoft" },
        new object[] { "b64", "QUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVo=", "QUJD!" },
        new object[] { "bitlocker", "123456-123456-123456-123456-123456-123456-123456-123456", "123456-123456" },
        new object[] { "bitcoin", "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa", "1A1z" },
        new object[] { "aeon", "Wmt" + new string('a', 94), "Wmx" + new string('a', 94) },
        new object[] { "bytecoin", "2A" + new string('a', 93), "2C" + new string('a', 93) },
        new object[] { "dashcoin", "D" + new string('a', 94), "D" + new string('a', 10) },
        new object[] { "dashcoin2", "X" + new string('a', 33), "X" + new string('a', 10) },
        new object[] { "fantomcoin", "6" + new string('a', 94), "6" + new string('a', 5) },
        new object[] { "monero", "4A" + new string('a', 93), "4C" + new string('a', 93) },
        new object[] { "sumokoin", "Sumoo" + new string('a', 94), "Sumo" + new string('a', 94) }
    };

    // Same options Program.Search uses for --lr/--fr
    private static Regex Build(string name) =>
        new Regex(BuiltInPatterns.Patterns[name], RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string RepoFile(string name)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "bstrings.slnx")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the tests run from inside the repository");
        return File.ReadAllText(Path.Combine(dir!.FullName, name));
    }

    [TestCaseSource(nameof(Samples))]
    public void PatternMatchesSampleAndRejectsNonMatch(string name, string match, string nonMatch)
    {
        Build(name).IsMatch(match).Should().BeTrue($"{name} should match '{match}'");
        Build(name).IsMatch(nonMatch).Should().BeFalse($"{name} should not match '{nonMatch}'");
    }

    [Test]
    public void EveryPatternHasASample()
    {
        Samples.Select(t => (string)((object[])t)[0]).Should().BeEquivalentTo(BuiltInPatterns.Patterns.Keys);
    }

    [Test]
    public void EveryPatternHasADescription()
    {
        BuiltInPatterns.Descriptions.Keys.Should().BeEquivalentTo(BuiltInPatterns.Patterns.Keys);
    }

    [Test]
    public void ReadmeListsEveryPattern()
    {
        var readme = RepoFile("README.md");

        foreach (var name in BuiltInPatterns.Patterns.Keys)
        {
            readme.Should().MatchRegex($@"\n\s+{Regex.Escape(name)}\s", $"README should list {name}");
        }
    }

    [Test]
    public void CompareVersionsScriptCoversEveryPattern()
    {
        var script = RepoFile(Path.Combine("dev-tools", "compare-versions-output.ps1"));

        foreach (var name in BuiltInPatterns.Patterns.Keys)
        {
            script.Should().Contain($"\"{name}\"");
        }
    }
}
