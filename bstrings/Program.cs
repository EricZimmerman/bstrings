#if !NET9_0_OR_GREATER
using Alphaleonis.Win32.Filesystem;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;
#else
using System.IO.Enumeration;
using System.Runtime.Versioning;
using Path = System.IO.Path;
using Directory = System.IO.Directory;
using File = System.IO.File;
using FileInfo = System.IO.FileInfo;
#endif
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
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
using Exceptionless;
using RawDiskLib;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace bstrings;

internal class Program
{


    private static readonly string Header =
        $"bstrings version {Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}" +
        "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
        "\r\nhttps://github.com/EricZimmerman/bstrings";

    private static readonly string Footer = @"Examples: bstrings.exe -f ""C:\Temp\UsrClass 1.dat"" --ls URL" +
                                            "\r\n\t " +
                                            @"   bstrings.exe -f ""C:\Temp\someFile.txt"" --lr guid" + "\r\n\t " +
                                            @"   bstrings.exe -f ""C:\Temp\aBigFile.bin"" --fs c:\temp\searchStrings.txt --fr c:\temp\searchRegex.txt" +
                                            "\r\n\t " +
                                            @"   bstrings.exe -d ""C:\Temp"" --mask ""*.dll""" + "\r\n\t " +
                                            @"   bstrings.exe -d ""C:\Temp"" --ar ""[\x20-\x37]""" + "\r\n\t " +
                                            @"   bstrings.exe -d ""C:\Temp"" --cp 10007" + "\r\n\t " +
                                            @"   bstrings.exe -d ""C:\Temp"" --ls test" + "\r\n\t " +
                                            @"   bstrings.exe -f ""C:\Temp\someOtherFile.txt"" --lr cc --sa" +
                                            "\r\n\t " +
                                            @"   bstrings.exe -f ""C:\Temp\someOtherFile.txt"" --lr cc --sa -m 15 -x 22" +
                                            "\r\n\t " +
                                            @"   bstrings.exe -f ""C:\Temp\UsrClass 1.dat"" --ls mui --sl";

    private static RootCommand _rootCommand;

    // Raw NTFS access (used for locked files on Windows), one file system per drive letter
    private static readonly Dictionary<char, DiscFileSystem> RawFileSystems = new Dictionary<char, DiscFileSystem>();

    private static async Task<int> Main(string[] args)
    {
        ExceptionlessClient.Default.Startup("Kruacm8p1B6RFAw2WMnKcEqkQcnWRkF3RmPSOzlW");

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var fOpt = new Option<string>("-f")
        {
            Description = "File to search. Either this or -d is required. When neither is given, input piped to stdin is searched"
        };

        var dOpt = new Option<string>("-d")
        {
            Description = "Directory to recursively process. Either this or -f is required"
        };

        var oOpt = new Option<string>("-o")
        {
            Description = "File to save results to. Results are appended if the file exists"
        };

        var aOpt = new Option<bool>("-a")
        {
            Description = "If set, look for ASCII strings. Use -a false to disable",
            DefaultValueFactory = _ => true
        };

        var uOpt = new Option<bool>("-u")
        {
            Description = "If set, look for Unicode strings. Use -u false to disable",
            DefaultValueFactory = _ => true
        };

        var mOpt = new Option<int>("-m")
        {
            Description = "Minimum string length",
            DefaultValueFactory = _ => 3
        };

        var bOpt = new Option<int>("-b")
        {
            Description = "Chunk size in MB. Valid range is 1 to 1024",
            DefaultValueFactory = _ => 512
        };

        var qOpt = new Option<bool>("-q")
        {
            Description = "Quiet mode (Do not show header or total number of hits)",
            DefaultValueFactory = _ => false
        };

        var sOpt = new Option<bool>("-s")
        {
            Description = "Really Quiet mode (Do not display hits to console. Speeds up processing when using -o)",
            DefaultValueFactory = _ => false
        };

        var xOpt = new Option<int>("-x")
        {
            Description = "Maximum string length. Default is unlimited",
            DefaultValueFactory = _ => -1
        };

        var pOpt = new Option<bool>("-p")
        {
            Description = "Display list of built in regular expressions",
            DefaultValueFactory = _ => false
        };

        var lsOpt = new Option<string>("--ls")
        {
            Description = "String to look for. When set, only matching strings are returned"
        };

        var lrOpt = new Option<string>("--lr")
        {
            Description = "Regex to look for. When set, only strings matching the regex are returned"
        };

        var fsOpt = new Option<string>("--fs")
        {
            Description = "File containing strings to look for. When set, only matching strings are returned"
        };

        var frOpt = new Option<string>("--fr")
        {
            Description =
                "File containing regex patterns to look for. When set, only strings matching regex patterns are returned"
        };

        var arOpt = new Option<string>("--ar")
        {
            Description =
                @"Range of characters to search for in 'Code page' strings. Specify as a range of characters in hex format and enclose in quotes. Default is [\x20 -\x7E]",
            DefaultValueFactory = _ => "[\x20-\x7E]"
        };

        var urOpt = new Option<string>("--ur")
        {
            Description =
                @"Range of characters to search for in Unicode strings. Specify as a range of characters in hex format and enclose in quotes. Default is [\\u0020-\\u007E]",
            DefaultValueFactory = _ => "[\u0020-\u007E]"
        };

        var cpOpt = new Option<int>("--cp")
        {
            Description =
                "Code page to use. Default is 1252. Use the Identifier value for code pages at https://goo.gl/ig6DxW",
            DefaultValueFactory = _ => 1252
        };

        var maskOpt = new Option<string>("--mask")
        {
            Description =
                "When using -d, file mask to search for. * and ? are supported. This option has no effect when using -f"
        };

        var msOpt = new Option<long>("--ms")
        {
            Description =
                "When using -d, maximum file size in bytes to process. This option has no effect when using -f",
            DefaultValueFactory = _ => -1
        };

        var roOpt = new Option<bool>("--ro")
        {
            Description =
                "When true, list the string matched by regex pattern vs string the pattern was found in (This may result in duplicate strings in output. ~ denotes approx. offset)",
            DefaultValueFactory = _ => false
        };

        var offOpt = new Option<bool>("--off")
        {
            Description = "Show offset to hit after string, followed by the encoding (A=Code page, U=Unicode)",
            DefaultValueFactory = _ => false
        };

        var saOpt = new Option<bool>("--sa")
        {
            Description = "Sort results alphabetically",
            DefaultValueFactory = _ => false
        };
        var slOpt = new Option<bool>("--sl")
        {
            Description = "Sort results by length",
            DefaultValueFactory = _ => false
        };
        var debugOpt = new Option<bool>("--debug")
        {
            Description = "Show debug information during processing",
            DefaultValueFactory = _ => false
        };
        var traceOpt = new Option<bool>("--trace")
        {
            Description = "Show trace information during processing",
            DefaultValueFactory = _ => false
        };

        _rootCommand = new RootCommand
        {
            fOpt,
            dOpt,
            oOpt,
            aOpt,
            uOpt,
            mOpt,
            bOpt,
            qOpt,
            sOpt,
            xOpt,
            pOpt,
            lsOpt,
            lrOpt,
            fsOpt,
            frOpt,
            arOpt,
            urOpt,
            cpOpt,
            maskOpt,
            msOpt,
            roOpt,
            offOpt,
            saOpt,
            slOpt,
            debugOpt,
            traceOpt
        };

        _rootCommand.Description = Header + "\r\n\r\n" + Footer;

        _rootCommand.SetAction(result => DoWork(result.GetValue(fOpt), result.GetValue(dOpt), result.GetValue(oOpt),
            result.GetValue(aOpt), result.GetValue(uOpt), result.GetValue(mOpt), result.GetValue(bOpt),
            result.GetValue(qOpt), result.GetValue(sOpt), result.GetValue(xOpt), result.GetValue(pOpt),
            result.GetValue(lsOpt), result.GetValue(lrOpt), result.GetValue(fsOpt), result.GetValue(frOpt),
            result.GetValue(arOpt), result.GetValue(urOpt), result.GetValue(cpOpt), result.GetValue(maskOpt),
            result.GetValue(msOpt), result.GetValue(roOpt), result.GetValue(offOpt), result.GetValue(saOpt),
            result.GetValue(slOpt), result.GetValue(debugOpt), result.GetValue(traceOpt)));

        var exitCode = await _rootCommand.Parse(args).InvokeAsync();

        Log.CloseAndFlush();

        return exitCode;
    }

    private static int DoWork(string f, string d, string o, bool a, bool u, int m, int b, bool q, bool s, int x,
        bool p, string ls, string lr, string fs, string fr, string ar, string ur, int cp, string mask, long ms,
        bool ro, bool off, bool sa, bool sl, bool debug, bool trace)
    {
        var levelSwitch = new LoggingLevelSwitch();

        var template = "{Message:lj}{NewLine}{Exception}";

        if (debug)
        {
            levelSwitch.MinimumLevel = LogEventLevel.Debug;
            template = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";
        }

        if (trace)
        {
            levelSwitch.MinimumLevel = LogEventLevel.Verbose;
            template = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";
        }

        var conf = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: template)
            .MinimumLevel.ControlledBy(levelSwitch);

        Log.Logger = conf.CreateLogger();


        if (p)
        {
            Log.Information("Name \t\tDescription");
            foreach (var regExPattern in BuiltInPatterns.Patterns.OrderBy(t => t.Key))
            {
                var desc = BuiltInPatterns.Descriptions[regExPattern.Key];
                Log.Information("{Key}\t{Desc}", regExPattern.Key, desc);
            }

            Console.WriteLine();
            Log.Information("To use a built in pattern, supply the Name to the --lr switch\r\n");

            return 0;
        }

        Encoding codePage;
        try
        {
            codePage = Encoding.GetEncoding(cp);
        }
        catch (Exception)
        {
            Log.Warning(
                "Invalid codepage: '{Cp}'. Use the Identifier value for code pages at https://goo.gl/ig6DxW. Verify codepage value and try again",
                cp);
            return 1;
        }

        string stdinTempFile = null;

        if (string.IsNullOrEmpty(f) && string.IsNullOrEmpty(d))
        {
            // Only fall back to stdin when no file or directory was given. A redirected stdin alone is not
            // a reason to ignore -f/-d (callers such as Go's exec.Command redirect it to /dev/null)
            if (!Console.IsInputRedirected)
            {
                var helpAction = new CustomHelpAction(new HelpAction());
                helpAction.Invoke(_rootCommand.Parse(""));

                return 1;
            }

            stdinTempFile = Path.GetTempFileName();

            using (var stdinStream = Console.OpenStandardInput())
            using (var tempFileStream = new FileStream(stdinTempFile, FileMode.Create, FileAccess.Write))
            {
                stdinStream.CopyTo(tempFileStream);
            }

            f = stdinTempFile;
        }
        else if (string.IsNullOrEmpty(f) == false)
        {
            if (!File.Exists(f))
            {
                Log.Warning("File '{F}' not found. Exiting", f);
                return 1;
            }
        }
        else if (!Directory.Exists(d))
        {
            Log.Warning("Directory '{D}' not found. Exiting", d);
            return 1;
        }

        try
        {
            return Search(f, d, o, a, u, m, b, q, s, x, ls, lr, fs, fr, ar, ur, codePage, mask, ms, ro, off, sa, sl);
        }
        finally
        {
            if (stdinTempFile != null)
            {
                try
                {
                    File.Delete(stdinTempFile);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }
    }

    private static int Search(string f, string d, string o, bool a, bool u, int m, int b, bool q, bool s, int x,
        string ls, string lr, string fs, string fr, string ar, string ur, Encoding codePage, string mask, long ms,
        bool ro, bool off, bool sa, bool sl)
    {
        var exitCode = 0;

        if (!q)
        {
            Log.Information("{Header}", Header);
            Console.WriteLine();
        }

        var minLength = m > 0 ? m : 3;

        var maxLength = -1;
        if (x >= minLength)
        {
            maxLength = x;
        }
        else if (x > 0)
        {
            Log.Warning("Maximum length ({X}) is less than minimum length ({MinLength}) and will be ignored", x,
                minLength);
        }

        var chunkSizeMb = b < 1 || b > 1024 ? 512 : b;
        var chunkSizeBytes = chunkSizeMb * 1024 * 1024;

        // Set up the scanners. Unicode strings are looked for at both even and odd byte offsets
        var scanners = new List<StringScanner>();
        try
        {
            if (u)
            {
                var uniTable = StringScanner.BuildRangeTable(ur);
                scanners.Add(new StringScanner(new UnicodeEncoding(false, false), 0, 'U', ur, uniTable, minLength,
                    maxLength, off, chunkSizeBytes));
                scanners.Add(new StringScanner(new UnicodeEncoding(false, false), 1, 'U', ur, uniTable, minLength,
                    maxLength, off, chunkSizeBytes));
            }

            if (a)
            {
                scanners.Add(new StringScanner(codePage, 0, 'A', ar, StringScanner.BuildRangeTable(ar), minLength, maxLength, off,
                    chunkSizeBytes));
            }
        }
        catch (ArgumentException ex)
        {
            Log.Error("Invalid character range: {Message}", ex.Message);
            return 1;
        }

        // Set up the filters
        var fileStrings = new HashSet<string>();
        var regexStrings = new HashSet<string>();

        if (ls?.Trim().Length > 0)
        {
            fileStrings.Add(ls);
        }

        var regPattern = lr;
        if (regPattern != null && BuiltInPatterns.Patterns.TryGetValue(regPattern, out var builtIn))
        {
            regPattern = builtIn;
        }

        if (regPattern?.Length > 0)
        {
            regexStrings.Add(regPattern);
        }

        if (fs?.Length > 0)
        {
            if (File.Exists(fs))
            {
                fileStrings.UnionWith(File.ReadAllLines(fs).Where(t => t.Trim().Length > 0));
            }
            else
            {
                Log.Error("Strings file '{Fs}' not found", fs);
                return 1;
            }
        }

        if (fr?.Length > 0)
        {
            if (File.Exists(fr))
            {
                regexStrings.UnionWith(File.ReadAllLines(fr));
            }
            else
            {
                Log.Error("Regex file '{Fr}' not found", fr);
                return 1;
            }
        }

        var regexList = new List<Regex>();
        foreach (var regString in regexStrings.Where(t => t.Length > 0))
        {
            try
            {
                regexList.Add(new Regex(regString,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error setting up regular expression '{RegString}': {Message}", regString,
                    ex.Message);
                exitCode = 1;
            }
        }

        var filtering = fileStrings.Count > 0 || regexList.Count > 0;

        if (regPattern?.Length > 0 && !q)
        {
            Log.Information("Searching via RegEx pattern: {RegPattern}", regPattern);
            Console.WriteLine();
        }

        // Find the files
        var files = new List<string>();

        if (string.IsNullOrEmpty(f) == false)
        {
            files.Add(Path.GetFullPath(f));
        }
        else
        {
            try
            {
                files.AddRange(EnumerateFiles(Path.GetFullPath(d), string.IsNullOrEmpty(mask) ? "*" : mask, out var unreadable));

                //folders that can't be read are reported as they are found; the run goes on and exits 1
                if (unreadable > 0)
                {
                    exitCode = 1;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting files in '{D}'. Error message: {Message}", d, ex.Message);
                return 1;
            }
        }

        if (!q)
        {
            Log.Information("Command line: {Args}", string.Join(" ", Environment.GetCommandLineArgs().Skip(1)));
            Console.WriteLine();
        }

        StreamWriter fileWriter = null;

        if (string.IsNullOrEmpty(o) == false)
        {
            try
            {
                o = Path.GetFullPath(o);

                var dir = Path.GetDirectoryName(o);

                if (dir != null && Directory.Exists(dir) == false)
                {
                    Directory.CreateDirectory(dir);
                }

                fileWriter = new StreamWriter(o, true);

                if (!q)
                {
                    Log.Information("Saving hits to '{O}'", o);
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Unable to open '{O}' for writing: {Message}", o, ex.Message);
                return 1;
            }
        }

        var consoleEncoding = Console.OutputEncoding;
        if (consoleEncoding.CodePage == 65001)
        {
            consoleEncoding = new UTF8Encoding(false);
        }

        var stdout = new StreamWriter(Console.OpenStandardOutput(), consoleEncoding, 1 << 16) { AutoFlush = false };

        var globalCounter = 0;
        var globalHits = 0;
        double globalTimespan = 0;

        var chunk = new byte[chunkSizeBytes];
        var charBuffer = new char[0];

        try
        {
            foreach (var file in files)
            {
                var stopwatch = Stopwatch.StartNew();
                var counter = 0;
                var hits = new HashSet<Hit>();

                try
                {
                    if (File.Exists(file) == false)
                    {
                        Log.Warning("'{File}' does not exist! Skipping", file);
                        exitCode = 1;
                        continue;
                    }

                    var fileSizeBytes = new FileInfo(file).Length;

                    if (ms > 0 && fileSizeBytes > ms)
                    {
                        Log.Warning("'{File}' is bigger than max file size of {Ms:N0} bytes! Skipping...", file, ms);
                        continue;
                    }

                    using var stream = OpenForRead(file);

                    var totalChunks = (stream.Length + chunkSizeBytes - 1) / chunkSizeBytes;

                    if (!q)
                    {
                        Log.Information(
                            totalChunks == 1
                                ? "Searching {TotalChunks:N0} chunk ({ChunkSizeMb} MB each) across {SizeReadable} in '{File}'"
                                : "Searching {TotalChunks:N0} chunks ({ChunkSizeMb} MB each) across {SizeReadable} in '{File}'",
                            totalChunks, chunkSizeMb, GetSizeReadable(stream.Length), file);
                        Console.WriteLine();
                    }

                    foreach (var scanner in scanners)
                    {
                        scanner.Reset();
                    }

                    var chunkIndex = 1;
                    while (true)
                    {
                        var count = ReadFully(stream, chunk);
                        var final = count < chunk.Length;

                        foreach (var scanner in scanners)
                        {
                            scanner.Process(chunk, count, final, ref charBuffer, hits);
                        }

                        if (count > 0 && !q)
                        {
                            Log.Information(
                                "Chunk {ChunkIndex:N0} of {TotalChunks:N0} finished. Total strings so far: {HitsCount:N0} Elapsed time: {TotalSeconds:N3} seconds. Average strings/sec: {Speed:N0}",
                                chunkIndex, totalChunks, hits.Count, stopwatch.Elapsed.TotalSeconds,
                                hits.Count / stopwatch.Elapsed.TotalSeconds);
                        }

                        if (final)
                        {
                            break;
                        }

                        chunkIndex += 1;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Log.Error(ex, "Error processing '{File}': {Message}", file, ex.Message);
                    exitCode = 1;
                }

                if (!q)
                {
                    Log.Information("Search complete.");
                    Console.WriteLine();
                }

                IEnumerable<Hit> ordered = hits;

                if (sa)
                {
                    Log.Information("Sorting alphabetically...");
                    Console.WriteLine();
                    ordered = hits.OrderBy(t => t.Text, StringComparer.CurrentCulture).ThenBy(t => t.Offset);
                }
                else if (sl)
                {
                    Log.Information("Sorting by length...");
                    Console.WriteLine();
                    ordered = hits.OrderBy(t => t.Text.Length);
                }

                if (!q)
                {
                    Log.Information("Processing strings...");
                    Console.WriteLine();
                }

                foreach (var hit in ordered)
                {
                    var line = off ? $"{hit.Text}\t0x{hit.Offset:X} ({hit.Encoding})" : hit.Text;

                    if (!filtering)
                    {
                        counter += 1;
                        WriteHit(line);
                        continue;
                    }

                    var written = false;

                    foreach (var fileString in fileStrings)
                    {
                        if (hit.Text.IndexOf(fileString, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            counter += 1;
                            WriteHit(line);
                            written = true;
                            break;
                        }
                    }

                    foreach (var regex in regexList)
                    {
                        if (ro)
                        {
                            var matched = false;
                            for (var match = regex.Match(hit.Text); match.Success; match = match.NextMatch())
                            {
                                matched = true;
                                WriteHit(off
                                    ? $"{match.Value}\t~0x{hit.Offset:X} ({hit.Encoding})"
                                    : match.Value);
                            }

                            if (matched)
                            {
                                counter += 1;
                            }
                        }
                        else if (!written && regex.IsMatch(hit.Text))
                        {
                            counter += 1;
                            WriteHit(line);
                            written = true;
                        }
                    }
                }

                stdout.Flush();

                if (q)
                {
                    continue;
                }

                stopwatch.Stop();

                Console.WriteLine();

                Log.Information(
                    counter == 1
                        ? "Found {Counter:N0} string in {TotalSeconds:N3} seconds. Average strings/sec: {Hits:N0}"
                        : "Found {Counter:N0} strings in {TotalSeconds:N3} seconds. Average strings/sec: {Hits:N0}",
                    counter, stopwatch.Elapsed.TotalSeconds, hits.Count / stopwatch.Elapsed.TotalSeconds);

                globalCounter += counter;
                globalHits += hits.Count;
                globalTimespan += stopwatch.Elapsed.TotalSeconds;
                if (files.Count > 1)
                {
                    Log.Information(
                        "-------------------------------------------------------------------------------------");
                    Console.WriteLine();
                }
            }
        }
        finally
        {
            stdout.Flush();
            fileWriter?.Dispose();
        }

        if (q || files.Count <= 1)
        {
            Console.WriteLine();
            return exitCode;
        }

        Log.Information(
            globalCounter == 1
                ? "Total across {FilesCount:N0} files: Found {GlobalCounter:N0} string in {GlobalTimespan:N3} seconds. Average strings/sec: {GlobalAve:N0}"
                : "Total across {FilesCount:N0} files: Found {GlobalCounter:N0} strings in {GlobalTimespan:N3} seconds. Average strings/sec: {GlobalAve:N0}",
            files.Count, globalCounter, globalTimespan, globalHits / globalTimespan);

        Console.WriteLine();

        return exitCode;

        void WriteHit(string line)
        {
            if (s == false)
            {
                stdout.WriteLine(line);
            }

            fileWriter?.WriteLine(line);
        }
    }

    /// <summary>
    ///     Files (not empty) under a folder, recursively, matching a mask (case-insensitive), without recursing into
    ///     junctions/symlinks. A folder that can't be read is reported ("Unable to read folder") and counted in
    ///     <paramref name="unreadable" />; it used to be skipped silently
    /// </summary>
    private static List<string> EnumerateFiles(string directory, string mask, out int unreadable)
    {
#if NET9_0_OR_GREATER
        using var finder = new FileFinder(directory, mask);
        var files = new List<string>();
        while (finder.MoveNext())
        {
            files.Add(finder.Current);
        }

        foreach (var (folder, reason) in finder.Unreadable)
        {
            Log.Error("Unable to read folder {Folder}: {Reason}", folder ?? directory, reason);
        }

        unreadable = finder.Unreadable.Count;
        return files;
#else
        var maskRegex = new Regex("^" + Regex.Escape(mask).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var errors = 0;

        var filters = new DirectoryEnumerationFilters
        {
            InclusionFilter = fsei => fsei.FileSize > 0 && maskRegex.IsMatch(fsei.FileName),
            RecursionFilter = fsei => !fsei.IsMountPoint && !fsei.IsSymbolicLink,
            ErrorFilter = (errorCode, errorMessage, pathProcessed) =>
            {
                Log.Error("Unable to read folder {Folder}: {Reason}", pathProcessed, errorMessage);
                errors += 1;
                return true;
            }
        };

        //no ContinueOnException: with it, AlphaFS never calls ErrorFilter
        var options = DirectoryEnumerationOptions.Files | DirectoryEnumerationOptions.Recursive |
                      DirectoryEnumerationOptions.SkipReparsePoints |
                      DirectoryEnumerationOptions.BasicSearch;

        var files = Directory.EnumerateFileSystemEntries(directory, options, filters).ToList();
        unreadable = errors;
        return files;
#endif
    }

#if NET9_0_OR_GREATER
    /// <summary>
    ///     The search for EnumerateFiles (as FileSystemEnumerable with IgnoreInaccessible did), but folders that can't be read
    ///     are skipped and kept in Unreadable
    /// </summary>
    private sealed class FileFinder : FileSystemEnumerator<string>
    {
        private readonly string _mask;

        //the subfolder whose handle is opened right after ShouldRecurseIntoEntry; an error then is about that folder
        private string _lastRecursed;

        //folder (null: the folder searched, as the base constructor can report it before this one runs) and reason
        public readonly List<(string Folder, string Reason)> Unreadable = new();

        public FileFinder(string directory, string mask) : base(directory,
            new EnumerationOptions { IgnoreInaccessible = false, RecurseSubdirectories = true, AttributesToSkip = 0 })
        {
            _mask = mask;
        }

        protected override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry)
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            _lastRecursed = entry.ToFullPath();
            return true;
        }

        protected override bool ShouldIncludeEntry(ref FileSystemEntry entry)
        {
            _lastRecursed = null;
            return !entry.IsDirectory && entry.Length > 0 && FileSystemName.MatchesSimpleExpression(_mask, entry.FileName);
        }

        protected override string TransformEntry(ref FileSystemEntry entry)
        {
            return entry.ToFullPath();
        }

        protected override bool ContinueOnError(int error)
        {
            Unreadable.Add((_lastRecursed, new System.ComponentModel.Win32Exception(error).Message));
            _lastRecursed = null;
            return true;
        }
    }
#endif

    private static Stream OpenForRead(string file)
    {
        try
        {
#if NET9_0_OR_GREATER
            return new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1,
                FileOptions.SequentialScan);
#else
            return File.Open(File.GetFileSystemEntryInfo(file).LongFullPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, PathFormat.LongFullPath);
#endif
        }
        catch (Exception ex) when (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                                   (ex is IOException || ex is UnauthorizedAccessException))
        {
            Log.Debug("Unable to open '{File}' normally ({Message}). Trying raw disk access", file, ex.Message);

            return OpenRaw(file);
        }
    }

#if NET9_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    private static Stream OpenRaw(string path)
    {
        if (path.Length < 4 || path[1] != ':' || path[2] != '\\')
        {
            throw new NotSupportedException($"Raw disk access requires a drive letter path: '{path}'");
        }

        var drive = char.ToLowerInvariant(path[0]);

        if (!RawFileSystems.TryGetValue(drive, out var fileSystem))
        {
            var disk = new RawDisk(drive);
            fileSystem = new NtfsFileSystem(disk.CreateDiskStream());
            RawFileSystems.Add(drive, fileSystem);
        }

        return fileSystem.OpenFile(path.Substring(3), FileMode.Open, FileAccess.Read);
    }

    private static int ReadFully(Stream stream, byte[] buffer)
    {
        var total = 0;
        int read;
        while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
        {
            total += read;
        }

        return total;
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


    private class CustomHelpAction : SynchronousCommandLineAction
    {
        private readonly HelpAction _defaultHelp;

        public CustomHelpAction(HelpAction action)
        {
            _defaultHelp = action;
        }

        public override int Invoke(ParseResult parseResult)
        {
            var result = _defaultHelp.Invoke(parseResult);

            Log.Warning("Either -f or -d is required. Exiting");

            return result;
        }
    }
}
