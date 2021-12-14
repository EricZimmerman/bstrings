#if !NET6_0
using Alphaleonis.Win32.Filesystem;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;
#else
using Path = System.IO.Path;
using Directory = System.IO.Directory;
#endif
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Streams;
using Exceptionless;
using NLog;
using NLog.Config;
using NLog.Targets;
using RawDiskLib;

namespace bstrings
{
    internal class Program
    {
        private static Logger _logger;
        private static Stopwatch _sw;
        private static readonly Dictionary<string, string> RegExPatterns = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> RegExDesc = new Dictionary<string, string>();

        private static string[] _args;

        private static readonly string Header =
            $"bstrings version {Assembly.GetExecutingAssembly().GetName().Version}" +
            "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
            "\r\nhttps://github.com/EricZimmerman/bstrings";

        private static readonly string Footer = @"Examples: bstrings.exe -f ""C:\Temp\UsrClass 1.dat"" --ls URL" + "\r\n\t " +
                                                @"   bstrings.exe -f ""C:\Temp\someFile.txt"" --lr guid" + "\r\n\t " +
                                                @"   bstrings.exe -f ""C:\Temp\aBigFile.bin"" --fs c:\temp\searchStrings.txt --fr c:\temp\searchRegex.txt -s" +
                                                "\r\n\t " +
                                                @"   bstrings.exe -d ""C:\Temp"" --mask ""*.dll""" + "\r\n\t " +
                                                @"   bstrings.exe -d ""C:\Temp"" --ar ""[\x20-\x37]""" + "\r\n\t " +
                                                @"   bstrings.exe -d ""C:\Temp"" --cp 10007" + "\r\n\t " +
                                                @"   bstrings.exe -d ""C:\Temp"" --ls test" + "\r\n\t " +
                                                @"   bstrings.exe -f ""C:\Temp\someOtherFile.txt"" --lr cc --sa" + "\r\n\t " +
                                                @"   bstrings.exe -f ""C:\Temp\someOtherFile.txt"" --lr cc --sa -m 15 -x 22" + "\r\n\t " +
                                                @"   bstrings.exe -f ""C:\Temp\UsrClass 1.dat"" --ls mui --sl";

        private static RootCommand _rootCommand;

        private static IFileSystem _fileSystem;

        private static readonly string BaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private static async Task Main(string[] args)
        {
            ExceptionlessClient.Default.Startup("Kruacm8p1B6RFAw2WMnKcEqkQcnWRkF3RmPSOzlW");

            SetupNLog();
            SetupPatterns();

            _args = args;

            _logger = LogManager.GetCurrentClassLogger();

            _rootCommand = new RootCommand
            {
                new Option<string>(
                    "-f",
                    "File to search. Either this or -d is required"),

                new Option<string>(
                    "-d",
                    "Directory to recursively process. Either this or -f is required"),

                new Option<string>(
                    "-o",
                    "File to save results to"),

                new Option<bool>(
                    "-a",
                    () => true,
                    "If set, look for ASCII strings. Use -a false to disable"),

                new Option<bool>(
                    "-u",
                    () => true,
                    "If set, look for Unicode strings. Use -u false to disable"),

                new Option<int>(
                    "-m",
                    () => 3,
                    "Minimum string length"),

                new Option<int>(
                    "-b",
                    () => 512,
                    "Chunk size in MB. Valid range is 1 to 1024. Default is 512"),

                new Option<bool>(
                    "-q",
                    () => false,
                    "Quiet mode (Do not show header or total number of hits)"),

                new Option<bool>(
                    "-s",
                    () => false,
                    "Really Quiet mode (Do not display hits to console. Speeds up processing when using -o)"),

                new Option<int>(
                    "-x",
                    () => -1,
                    "Maximum string length. Default is unlimited"),

                new Option<bool>(
                    "-p",
                    () => false,
                    "Display list of built in regular expressions"),

                new Option<string>(
                    "--ls",
                    "String to look for. When set, only matching strings are returned"),

                new Option<string>(
                    "--lr",
                    "Regex to look for. When set, only strings matching the regex are returned"),

                new Option<string>(
                    "--fs",
                    "File containing strings to look for. When set, only matching strings are returned"),

                new Option<string>(
                    "--fr",
                    "Directory to save bodyfile formatted results to. --bdl is also required when using this option"),

                new Option<string>(
                    "--ar",
                    () => "[\x20-\x7E]",
                    @"Range of characters to search for in 'Code page' strings. Specify as a range of characters in hex format and enclose in quotes. Default is [\x20 -\x7E]"),

                new Option<string>(
                    "--ur",
                    () => "[\u0020-\u007E]",
                    @"Range of characters to search for in 'Code page' strings. Specify as a range of characters in hex format and enclose in quotes. Default is [\x20 -\x7E]"),

                new Option<int>(
                    "--cp",
                    () => 1252,
                    "Code page to use. Default is 1252. Use the Identifier value for code pages at https://goo.gl/ig6DxW"),

                new Option<string>(
                    "--mask",
                    "When using -d, file mask to search for. * and ? are supported. This option has no effect when using -f"),

                new Option<int>(
                    "--ms",
                    () => -1,
                    "When using -d, maximum file size to process. This option has no effect when using -f"),

                new Option<bool>(
                    "--ro",
                    () => false,
                    "When true, list the string matched by regex pattern vs string the pattern was found in (This may result in duplicate strings in output. ~ denotes approx. offset)"),

                new Option<bool>(
                    "--off",
                    () => false,
                    "Show offset to hit after string, followed by the encoding (A=1252, U=Unicode)\r\n"),

                new Option<bool>(
                    "--sa",
                    () => false,
                    "Sort results alphabetically"),

                new Option<bool>(
                    "--sl",
                    () => false,
                    "When true, use LF vs CRLF for Sort results by length")
            };

            _rootCommand.Description = Header + "\r\n\r\n" + Footer;

            _rootCommand.Handler = CommandHandler.Create(DoWork);

            await _rootCommand.InvokeAsync(args);
        }

        private static void DoWork(string f, string d, string o, bool a, bool u, int m, int b, bool q, bool s, int x, bool p, string ls, string lr, string fs, string fr, string ar, string ur, int cp, string mask, int ms, bool ro, bool off, bool sa, bool sl)
        {
            if (p)
            {
                _logger.Warn("Name \t\tDescription");
                foreach (var regExPattern in RegExPatterns.OrderBy(t => t.Key))
                {
                    var desc = RegExDesc[regExPattern.Key];
                    _logger.Info($"{regExPattern.Key}\t{desc}");
                }

                _logger.Info("");
                _logger.Info("To use a built in pattern, supply the Name to the --lr switch\r\n");

                return;
            }

            var cpTest = CodePagesEncodingProvider.Instance.GetEncoding(1252);

            if (cpTest == null)
            {
                _logger.Warn($"Invalid codepage: '{cp}'. Use the Identifier value for code pages at https://goo.gl/ig6DxW. Verify codepage value and try again");
                return;
            }

            if (string.IsNullOrEmpty(f) && string.IsNullOrEmpty(d))
            {
                var helpBld = new HelpBuilder(LocalizationResources.Instance, Console.WindowWidth);
                var hc = new HelpContext(helpBld, _rootCommand, Console.Out);

                helpBld.Write(hc);

                _logger.Warn("Either -f or -d is required. Exiting");
                return;
            }

            if (string.IsNullOrEmpty(f) == false &&
                !File.Exists(f) &&
                mask?.Length == 0)
            {
                _logger.Warn($"File '{f}' not found. Exiting");
                return;
            }

            if (string.IsNullOrEmpty(d) == false &&
                !Directory.Exists(d) &&
                mask?.Length == 0)
            {
                _logger.Warn($"Directory '{d}' not found. Exiting");
                return;
            }

            if (!q)
            {
                _logger.Info(Header);
                _logger.Info("");
            }

            var files = new List<string>();

            if (string.IsNullOrEmpty(f) == false)
            {
                files.Add(Path.GetFullPath(f));
            }
            else
            {
                try
                {
                    if (mask?.Length > 0)
                    {
                        files.AddRange(Directory.EnumerateFiles(Path.GetFullPath(d!),
                            mask, SearchOption.AllDirectories));
                    }
                    else
                    {
                        files.AddRange(Directory.EnumerateFiles(Path.GetFullPath(d!), "*",
                            SearchOption.AllDirectories));
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(
                        $"Error getting files in '{d}'. Error message: {ex.Message}");
                    return;
                }
            }

            if (!q)
            {
                _logger.Info($"Command line: {string.Join(" ", _args)}");
                _logger.Info("");
            }

            StreamWriter sw = null;

            var globalCounter = 0;
            var globalHits = 0;
            double globalTimespan = 0;
            var withBoundaryHits = false;

            if (string.IsNullOrEmpty(o) == false && o.Length > 0)
            {
                o = o.TrimEnd('\\');

                var dir = Path.GetDirectoryName(o);

                if (dir != null && Directory.Exists(dir) == false)
                {
                    try
                    {
                        Directory.CreateDirectory(dir);
                    }
                    catch (Exception)
                    {
                        _logger.Warn(
                            $"Invalid path: '{o}'. Results will not be saved to a file.");
                        _logger.Info("");
                        o = string.Empty;
                    }
                }
                else
                {
                    if (dir == null)
                    {
                        _logger.Warn($"Invalid path: '{o}");
                        o = string.Empty;
                    }
                }

                if (o.Length > 0 && !q)
                {
                    _logger.Info($"Saving hits to '{o}'");
                    _logger.Info("");
                }

                if (o.Length > 0)
                {
//                    try
//                    {
//                        File.Create(_fluentCommandLineParser.Object.SaveTo);
//                    }
//                    catch (Exception e)
//                    {
//                        _logger.Fatal($"Unable to create output file '{_fluentCommandLineParser.Object.SaveTo}'! Check permissions and try again! Error: {e.Message}");
//                        return;
//                    }
                    sw = new StreamWriter(o, true);
                }
            }


            foreach (var file in files)
            {
                if (File.Exists(file) == false)
                {
                    _logger.Warn($"'{file}' does not exist! Skipping");
                }

                _sw = new Stopwatch();
                _sw.Start();
                var counter = 0;
                var hits = new HashSet<string>();


                var regPattern = lr;

                if (regPattern != null && RegExPatterns.ContainsKey(lr))
                {
                    regPattern = RegExPatterns[lr];
                }

                if (regPattern?.Length > 0 && !q)
                {
                    _logger.Info($"Searching via RegEx pattern: {regPattern}");
                    _logger.Info("");
                }

                var minLength = 3;
                if (m > 0)
                {
                    minLength = m;
                }

                var maxLength = -1;

                if (x > minLength)
                {
                    maxLength = x;
                }

                var chunkSizeMb = b < 1 ||
                                  b > 1024
                    ? 512
                    : b;
                var chunkSizeBytes = chunkSizeMb * 1024 * 1024;

                var fileSizeBytes = new FileInfo(file).Length;

                if (ms > 0)
                {
                    if (fileSizeBytes > ms)
                    {
                        _logger.Warn($"'{file}' is bigger than max file size of {ms:N0} bytes! Skipping...");
                        continue;
                    }
                }

                var bytesRemaining = fileSizeBytes;
                long offset = 0;

                var chunkIndex = 1;
                var totalChunks = fileSizeBytes / chunkSizeBytes + 1;
                var hSuffix = totalChunks == 1 ? "" : "s";

                if (!q)
                {
                    _logger.Info(
                        $"Searching {totalChunks:N0} chunk{hSuffix} ({chunkSizeMb} MB each) across {GetSizeReadable(fileSizeBytes)} in '{file}'");
                    _logger.Info("");
                }

                try
                {
                    MappedStream mappedStream = null;

                    try
                    {
                        FileStream fileStream;

#if NET6_0
                        fileStream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
#else
                            fileStream =
                            Alphaleonis.Win32.Filesystem.File.Open(Alphaleonis.Win32.Filesystem.File.GetFileSystemEntryInfo(file).LongFullPath, FileMode.Open, FileAccess.Read,
                                FileShare.Read, Alphaleonis.Win32.Filesystem.PathFormat.LongFullPath);
#endif

                        mappedStream = MappedStream.FromStream(fileStream, Ownership.None);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }

                    if (mappedStream == null)
                    {
                        //raw mode
                        var ss = OpenFile(file);

                        mappedStream = MappedStream.FromStream(ss, Ownership.None);
                    }

                    using (mappedStream)
                    {
                        while (bytesRemaining > 0)
                        {
                            if (bytesRemaining <= chunkSizeBytes)
                            {
                                chunkSizeBytes = (int)bytesRemaining;
                            }

                            var chunk = new byte[chunkSizeBytes];

                            mappedStream.Read(chunk, 0, chunkSizeBytes);

                            if (u)
                            {
                                var uh = GetUnicodeHits(chunk, minLength, maxLength, offset,
                                    off, ur);
                                foreach (var h in uh)
                                {
                                    hits.Add(h);
                                }
                            }

                            if (a)
                            {
                                var ah = GetAsciiHits(chunk, minLength, maxLength, offset,
                                    off, cp, ar);
                                foreach (var h in ah)
                                {
                                    hits.Add(h);
                                }
                            }

                            offset += chunkSizeBytes;
                            bytesRemaining -= chunkSizeBytes;

                            if (!q)
                            {
                                _logger.Info(
                                    $"Chunk {chunkIndex:N0} of {totalChunks:N0} finished. Total strings so far: {hits.Count:N0} Elapsed time: {_sw.Elapsed.TotalSeconds:N3} seconds. Average strings/sec: {hits.Count / _sw.Elapsed.TotalSeconds:N0}");
                            }

                            chunkIndex += 1;
                        }

                        //do chunk boundary checks to make sure we get everything and not split things

                        if (!q)
                        {
                            _logger.Info(
                                "Primary search complete. Looking for strings across chunk boundaries...");
                        }

                        bytesRemaining = fileSizeBytes;
                        chunkSizeBytes = chunkSizeMb * 1024 * 1024;
                        offset = chunkSizeBytes - m * 10 * 2;
                        //move starting point backwards for our starting point
                        chunkIndex = 0;

                        var boundaryChunkSize = m * 10 * 2 * 2;
                        //grab the same # of bytes on both sides of the boundary

                        while (bytesRemaining > 0)
                        {
                            if (offset + boundaryChunkSize > fileSizeBytes)
                            {
                                break;
                            }

                            var chunk = new byte[boundaryChunkSize];

                            mappedStream.Read(chunk, 0, boundaryChunkSize);

                            if (u)
                            {
                                var uh = GetUnicodeHits(chunk, minLength, maxLength, offset,
                                    off, ur);
                                foreach (var h in uh)
                                {
                                    hits.Add("  " + h);
                                }

                                if (withBoundaryHits == false && uh.Count > 0)
                                {
                                    withBoundaryHits = uh.Count > 0;
                                }
                            }

                            if (a)
                            {
                                var ah = GetAsciiHits(chunk, minLength, maxLength, offset,
                                    off, cp, ar);
                                foreach (var h in ah)
                                {
                                    hits.Add("  " + h);
                                }

                                if (withBoundaryHits == false && ah.Count > 0)
                                {
                                    withBoundaryHits = true;
                                }
                            }

                            offset += chunkSizeBytes;
                            bytesRemaining -= chunkSizeBytes;

                            chunkIndex += 1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Info("");
                    _logger.Error($"Error: {ex.Message}");
                }

                _sw.Stop();

                if (!q)
                {
                    _logger.Info("Search complete.");
                    _logger.Info("");
                }

                if (sa)
                {
                    _logger.Info("Sorting alphabetically...");
                    _logger.Info("");
                    var tempList = hits.ToList();
                    tempList.Sort();
                    hits = new HashSet<string>(tempList);
                }
                else if (sl)
                {
                    _logger.Info("Sorting by length...");
                    _logger.Info("");
                    var tempList = SortByLength(hits.ToList()).ToList();
                    hits = new HashSet<string>(tempList);
                }

                var fileStrings = new HashSet<string>();
                var regexStrings = new HashSet<string>();

                //set up highlighting
                if (ls?.Length > 0)
                {
                    fileStrings.Add(ls);
                }

                if (lr?.Length > 0)
                {
                    regexStrings.Add(regPattern);
                }

                if (string.IsNullOrEmpty(fs) == false || string.IsNullOrEmpty(fr) == false)
                {
                    if (fs?.Length > 0)
                    {
                        if (File.Exists(fs))
                        {
                            fileStrings.UnionWith(new HashSet<string>(File.ReadAllLines(fs)));
                        }
                        else
                        {
                            _logger.Error($"Strings file '{fs}' not found.");
                        }
                    }

                    if (fr?.Length > 0)
                    {
                        if (File.Exists(fr))
                        {
                            regexStrings.UnionWith(new HashSet<string>(File.ReadAllLines(fr)));
                        }
                        else
                        {
                            _logger.Error($"Regex file '{fr}' not found.");
                        }
                    }
                }

                AddHighlightingRules(fileStrings.ToList());

                if (ro == false)
                {
                    AddHighlightingRules(regexStrings.ToList(), true);
                }


                if (!q)
                {
                    _logger.Info("Processing strings...");
                    _logger.Info("");
                }

                foreach (var hit in hits)
                {
                    if (hit.Length == 0)
                    {
                        continue;
                    }

                    if (fileStrings.Count > 0 || regexStrings.Count > 0)
                    {
                        foreach (var fileString in fileStrings)
                        {
                            if (fileString.Trim().Length == 0)
                            {
                                continue;
                            }

                            if (hit.IndexOf(fileString, StringComparison.InvariantCultureIgnoreCase) < 0)
                            {
                                continue;
                            }

                            counter += 1;

                            if (s == false)
                            {
                                _logger.Info(hit);
                            }

                            sw?.WriteLine(hit);
                        }

                        var hitOffset = "";
                        if (off)
                        {
                            hitOffset = $"~{hit.Split('\t').Last()}";
                        }

                        foreach (var regString in regexStrings)
                        {
                            if (regString.Trim().Length == 0)
                            {
                                continue;
                            }

                            try
                            {
                                var reg1 = new Regex(regString,
                                    RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

                                if (reg1.IsMatch(hit) == false)
                                {
                                    continue;
                                }

                                counter += 1;

                                if (ro)
                                {
                                    foreach (var match in reg1.Matches(hit))
                                    {
                                        if (s == false)
                                        {
                                            _logger.Info($"{match}\t{hitOffset}");
                                        }

                                        sw?.WriteLine($"{match}\t{hitOffset}");
                                    }
                                }
                                else
                                {
                                    if (s == false)
                                    {
                                        _logger.Info(hit);
                                    }

                                    sw?.WriteLine(hit);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"Error setting up regular expression '{regString}': {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        //dump all strings
                        counter += 1;

                        if (s == false)
                        {
                            _logger.Info(hit);
                        }

                        sw?.WriteLine(hit);
                    }
                }

                if (q)
                {
                    continue;
                }

                var suffix = counter == 1 ? "" : "s";

                _logger.Info("");

                if (withBoundaryHits)
                {
                    _logger.Info("** Strings prefixed with 2 spaces are hits found across chunk boundaries **");
                    _logger.Info("");
                }

                _logger.Info(
                    $"Found {counter:N0} string{suffix} in {_sw.Elapsed.TotalSeconds:N3} seconds. Average strings/sec: {hits.Count / _sw.Elapsed.TotalSeconds:N0}");
                globalCounter += counter;
                globalHits += hits.Count;
                globalTimespan += _sw.Elapsed.TotalSeconds;
                if (files.Count > 1)
                {
                    _logger.Info(
                        "-------------------------------------------------------------------------------------\r\n");
                }
            }

            if (sw != null)
            {
                sw.Flush();
                sw.Close();
            }

            if (q || files.Count <= 1)
            {
                return;
            }

            var suffix2 = globalCounter == 1 ? "" : "s";
            _logger.Info(
                $"Total across {files.Count:N0} files: Found {globalCounter:N0} string{suffix2} in {globalTimespan:N3} seconds. Average strings/sec: {globalHits / globalTimespan:N0}");
            _logger.Info("");
        }

        private static SparseStream OpenFile(string path)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new NotSupportedException("Raw disk access not supported on non-Windows systems. Exiting\r\n");
            }

            var rawPath = path.Substring(3);
            if (_fileSystem != null)
            {
                return _fileSystem.OpenFile(rawPath, FileMode.Open, FileAccess.Read);
            }

            var disk = new RawDisk(path.ToLowerInvariant().First());
            var rawDiskStream = disk.CreateDiskStream();
            _fileSystem = new NtfsFileSystem(rawDiskStream);

            return _fileSystem.OpenFile(rawPath, FileMode.Open, FileAccess.Read);
        }

        private static string GetSizeReadable(long i)
        {
            var sign = i < 0 ? "-" : "";
            double readable;
            string suffix;
            if (i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = i >> 50;
            }
            else if (i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = i >> 40;
            }
            else if (i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = i >> 30;
            }
            else if (i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = i >> 20;
            }
            else if (i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = i >> 10;
            }
            else if (i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = i;
            }
            else
            {
                return i.ToString(sign + "0 B"); // Byte
            }

            readable = readable / 1024;

            return sign + readable.ToString("0.### ") + suffix;
        }

        private static void SetupPatterns()
        {
            RegExDesc.Add("guid", "\tFinds GUIDs");
            RegExDesc.Add("usPhone", "\tFinds US phone numbers");
            RegExDesc.Add("unc", "\tFinds UNC paths");
            RegExDesc.Add("mac", "\tFinds MAC addresses");
            RegExDesc.Add("ssn", "\tFinds US Social Security Numbers");
            RegExDesc.Add("cc", "\tFinds credit card numbers");

            RegExDesc.Add("ipv4", "\tFinds IP version 4 addresses");
            RegExDesc.Add("ipv6", "\tFinds IP version 6 addresses");
            RegExDesc.Add("email", "\tFinds embedded email addresses");
            RegExDesc.Add("zip", "\tFinds zip codes");
            RegExDesc.Add("urlUser", "\tFinds usernames in URLs");
            RegExDesc.Add("url3986", "\tFinds URLs according to RFC 3986");
            RegExDesc.Add("xml", "\tFinds XML/HTML tags");
            RegExDesc.Add("sid", "\tFinds Microsoft Security Identifiers (SID)");
            RegExDesc.Add("win_path", @"Finds Windows style paths (C:\folder1\folder2\file.txt)");
            RegExDesc.Add("var_set", "\tFinds environment variables being set (OS=Windows_NT)");
            RegExDesc.Add("reg_path", "Finds paths related to Registry hives");
            RegExDesc.Add("b64", "\tFinds valid formatted base 64 strings");
            RegExDesc.Add("bitlocker", "Finds Bitlocker recovery keys");
            RegExDesc.Add("bitcoin", "\tFinds BitCoin wallet addresses");
            RegExDesc.Add("aeon", "\tFinds Aeon wallet addresses");
            RegExDesc.Add("bytecoin", "Finds ByteCoin wallet addresses");
            RegExDesc.Add("dashcoin", "Finds DashCoin wallet addresses (D*)");
            RegExDesc.Add("dashcoin2", "Finds DashCoin wallet addresses (7|X)*");
            RegExDesc.Add("fantomcoin", "Finds Fantomcoin wallet addresses");
            RegExDesc.Add("monero", "\tFinds Monero wallet addresses");
            RegExDesc.Add("sumokoin", "Finds SumoKoin wallet addresses");

            RegExPatterns.Add("bitcoin", @"\b[13][a-km-zA-HJ-NP-Z1-9]{25,34}\b");
            RegExPatterns.Add("aeon", @"Wm[st]{1}[0-9a-zA-Z]{94}");
            RegExPatterns.Add("bytecoin", @"2[0-9AB][0-9a-zA-Z]{93}");

            RegExPatterns.Add("dashcoin", "D[0-9a-zA-Z]{94}");
            RegExPatterns.Add("dashcoin2", "(7|X)[a-zA-Z0-9]{33}");
            RegExPatterns.Add("fantomcoin", "6[0-9a-zA-Z]{94}");
            RegExPatterns.Add("monero", "4[0-9AB][0-9a-zA-Z]{93}|4[0-9AB][0-9a-zA-Z]{104}");
            RegExPatterns.Add("sumokoin", "Sumoo[0-9a-zA-Z]{94}");


            RegExPatterns.Add("b64",
                @"^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=|[A-Za-z0-9+/]{4})$");

            RegExPatterns.Add("bitlocker", @"[0-9]{6}?-[0-9]{6}-[0-9]{6}-[0-9]{6}-[0-9]{6}-[0-9]{6}-[0-9]{6}-[0-9]{6}");

            RegExPatterns.Add("reg_path", @"([a-z0-9]\\)*(software\\)|(sam\\)|(system\\)|(security\\)[a-z0-9\\]+");
            RegExPatterns.Add("var_set", @"^[a-z_0-9]+=[\\/:\*\?<>|;\- _a-z0-9]+");
            RegExPatterns.Add("win_path",
                @"(?:""?[a-zA-Z]\:|\\\\[^\\\/\:\*\?\<\>\|]+\\[^\\\/\:\*\?\<\>\|]*)\\(?:[^\\\/\:\*\?\<\>\|]+\\)*\w([^\\\/\:\*\?\<\>\|])*");
            RegExPatterns.Add("sid", @"^S-\d-\d+-(\d+-){1,14}\d+$");
            RegExPatterns.Add("xml", @"\A<([A-Z][A-Z0-9]*)\b[^>]*>(.*?)</\1>\z");
            RegExPatterns.Add("guid", @"\b[A-F0-9]{8}(?:-[A-F0-9]{4}){3}-[A-F0-9]{12}\b");
            RegExPatterns.Add("usPhone", @"\(?\b[2-9][0-9]{2}\)?[-. ]?[2-9][0-9]{2}[-. ]?[0-9]{4}\b");
            RegExPatterns.Add("unc", @"^\\\\(?<server>[a-z0-9 %._-]+)\\(?<share>[a-z0-9 $%._-]+)");
            RegExPatterns.Add("mac", "\\b[0-9A-F]{2}([-:]?)(?:[0-9A-F]{2}\\1){4}[0-9A-F]{2}\\b");
            RegExPatterns.Add("ssn", "\\b(?!000)(?!666)[0-8][0-9]{2}[- ](?!00)[0-9]{2}[- ](?!0000)[0-9]{4}\\b");
            // RegExPatterns.Add("cc","^(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|6(?:011|5[0-9][0-9])[0-9]{12}|3[47][0-9]{13}|3(?:0[0-5]|[68][0-9])[0-9]{11}|(?:2131|1800|35\\d{3})\\d{11})$");
            RegExPatterns.Add("cc", @"^[ -]*(?:4[ -]*(?:\d[ -]*){11}(?:(?:\d[ -]*){3})?\d|5[ -]*[1-5](?:[ -]*[0-9]){14}|6[ -]*(?:0[ -]*1[ -]*1|5[ -]*\d[ -]*\d)(?:[ -]*[0-9]){12}|3[ -]*[47](?:[ -]*[0-9]){13}|3[ -]*(?:0[ -]*[0-5]|[68][ -]*[0-9])(?:[ -]*[0-9]){11}|(?:2[ -]*1[ -]*3[ -]*1|1[ -]*8[ -]*0[ -]*0|3[ -]*5(?:[ -]*[0-9]){3})(?:[ -]*[0-9]){11})[ -]*$");
            RegExPatterns.Add("ipv4", @"\b(?:(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])\b");
            RegExPatterns.Add("ipv6", @"(?<![:.\w])(?:[A-F0-9]{1,4}:){7}[A-F0-9]{1,4}(?![:.\w])");
            //         RegExPatterns.Add("email",@"[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?");
            RegExPatterns.Add("email", @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,6}\b");
            RegExPatterns.Add("zip", @"\A\b[0-9]{5}(?:-[0-9]{4})?\b\z");
            RegExPatterns.Add("urlUser", @"^[a-z0-9+\-.]+://(?<user>[a-z0-9\-._~%!$&'()*+,;=]+)@");
            RegExPatterns.Add("url3986", @"^
		[a-z][a-z0-9+\-.]*://                       # Scheme
		([a-z0-9\-._~%!$&'()*+,;=]+@)?              # User
		(?<host>[a-z0-9\-._~%]+                     # Named host
		|\[[a-f0-9:.]+\]                            # IPv6 host
		|\[v[a-f0-9][a-z0-9\-._~%!$&'()*+,;=:]+\])  # IPvFuture host
		(:[0-9]+)?                                  # Port
		(/[a-z0-9\-._~%!$&'()*+,;=:@]+)*/?          # Path
		(\?[a-z0-9\-._~%!$&'()*+,;=:@/?]*)?         # Query
		(\#[a-z0-9\-._~%!$&'()*+,;=:@/?]*)?         # Fragment
		$");
        }

        private static void AddHighlightingRules(List<string> words, bool isRegEx = false)
        {
            var target = (ColoredConsoleTarget)LogManager.Configuration.FindTargetByName("console");
            var rule = target.WordHighlightingRules.FirstOrDefault();

            var bgColor = ConsoleOutputColor.Green;
            var fgColor = ConsoleOutputColor.Red;

            if (rule != null)
            {
                bgColor = rule.BackgroundColor;
                fgColor = rule.ForegroundColor;
            }

            foreach (var word in words)
            {
                var r = new ConsoleWordHighlightingRule { IgnoreCase = true };
                if (isRegEx)
                {
                    r.Regex = word;
                }
                else
                {
                    r.Text = word;
                }

                r.ForegroundColor = fgColor;
                r.BackgroundColor = bgColor;

                r.WholeWords = false;
                target.WordHighlightingRules.Add(r);
            }
        }

        private static IEnumerable<string> SortByLength(IEnumerable<string> e)
        {
            var sorted = from s in e
                orderby s.Length ascending
                select s;
            return sorted;
        }

        private static List<string> GetUnicodeHits(byte[] bytes, int minSize, int maxSize, long currentOffset,
            bool withOffsets, string ur)
        {
            var maxString = maxSize == -1 ? "" : maxSize.ToString();
            var mi2 = $"{"{"}{minSize}{","}{maxString}{"}"}";

            var uniRange = ur;
            var regUni = new Regex($"{uniRange}{mi2}", RegexOptions.Compiled);
            var uniString = Encoding.Unicode.GetString(bytes);

            var hits = new List<string>();

            foreach (Match match in regUni.Matches(uniString))
            {
                if (withOffsets)
                {
                    var actualOffset = (currentOffset + match.Index) * 2;

                    hits.Add($"{match.Value.Trim()}{'\t'}0x{actualOffset:X} (U)");
                }
                else
                {
                    hits.Add(match.Value.Trim());
                }
            }

            return hits;
        }


        private static int ByteSearch(byte[] searchIn, byte[] searchBytes, int start = 0)
        {
            var found = -1;
            //only look at this if we have a populated search array and search bytes with a sensible start
            if (searchIn.Length > 0 && searchBytes.Length > 0 && start <= searchIn.Length - searchBytes.Length &&
                searchIn.Length >= searchBytes.Length)
            {
                //iterate through the array to be searched
                for (var i = start; i <= searchIn.Length - searchBytes.Length; i++)
                {
                    //if the start bytes match we will start comparing all other bytes
                    if (searchIn[i] == searchBytes[0])
                    {
                        if (searchIn.Length > 1)
                        {
                            //multiple bytes to be searched we have to compare byte by byte
                            var matched = true;
                            for (var y = 1; y <= searchBytes.Length - 1; y++)
                            {
                                if (searchIn[i + y] != searchBytes[y])
                                {
                                    matched = false;
                                    break;
                                }
                            }

                            //everything matched up
                            if (matched)
                            {
                                found = i;
                                break;
                            }
                        }
                        else
                        {
                            //search byte is only one bit nothing else to do
                            found = i;
                            break; //stop the loop
                        }
                    }
                }
            }

            return found;
        }

        private static List<string> GetAsciiHits(byte[] bytes, int minSize, int maxSize, long currentOffset,
            bool withOffsets, int cp, string ar)
        {
            var maxString = maxSize == -1 ? "" : maxSize.ToString();
            var mi2 = $"{"{"}{minSize}{","}{maxString}{"}"}";

            var ascRange = ar;
            var regAsc = new Regex($"{ascRange}{mi2}", RegexOptions.Compiled);

            var codePage = CodePagesEncodingProvider.Instance.GetEncoding(cp);
            var ascString = codePage!.GetString(bytes);

            var hits = new List<string>();

            foreach (Match match in regAsc.Matches(ascString))
            {
                if (withOffsets)
                {
                    var matchBytes = codePage!
                        .GetBytes(match.Value);

                    var pos = ByteSearch(bytes, matchBytes, match.Index);

                    var actualOffset = currentOffset + pos;

                    hits.Add($"{match.Value.Trim()}{'\t'}0x{actualOffset:X} (A)");
                }
                else
                {
                    hits.Add(match.Value.Trim());
                }
            }

            return hits;
        }

        private static void SetupNLog()
        {
            if (File.Exists(Path.Combine(BaseDirectory, "Nlog.config")))
            {
                return;
            }

            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            const string layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
        }
    }
}