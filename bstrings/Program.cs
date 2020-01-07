using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Alphaleonis.Win32.Filesystem;
using Exceptionless;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Streams;
using Fclp;
using Fclp.Internals.Extensions;
using NLog;
using NLog.Config;
using NLog.Targets;
using RawDiskLib;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace bstrings
{
    internal class Program
    {
        private static Logger _logger;
        private static Stopwatch _sw;
        private static readonly Dictionary<string, string> RegExPatterns = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> RegExDesc = new Dictionary<string, string>();
        private static FluentCommandLineParser<ApplicationArguments> _fluentCommandLineParser;

        private static void Main(string[] args)
        {
            ExceptionlessClient.Default.Startup("Kruacm8p1B6RFAw2WMnKcEqkQcnWRkF3RmPSOzlW");

            SetupNLog();
            SetupPatterns();

            _logger = LogManager.GetCurrentClassLogger();

            _fluentCommandLineParser = new FluentCommandLineParser<ApplicationArguments>
            {
                IsCaseSensitive = false
            };

            _fluentCommandLineParser.Setup(arg => arg.File)
                .As('f')
                .WithDescription("File to search. Either this or -d is required");

            _fluentCommandLineParser.Setup(arg => arg.Directory)
                .As('d')
                .WithDescription("Directory to recursively process. Either this or -f is required");

            _fluentCommandLineParser.Setup(arg => arg.SaveTo)
                .As('o')
                .WithDescription("File to save results to");

            _fluentCommandLineParser.Setup(arg => arg.GetAscii)
                .As('a')
                .SetDefault(true)
                .WithDescription("If set, look for ASCII strings. Default is true. Use -a false to disable");

            _fluentCommandLineParser.Setup(arg => arg.GetUnicode)
                .As('u')
                .SetDefault(true)
                .WithDescription("If set, look for Unicode strings. Default is true. Use -u false to disable");

            _fluentCommandLineParser.Setup(arg => arg.MinimumLength)
                .As('m').SetDefault(3).WithDescription("Minimum string length. Default is 3");

            _fluentCommandLineParser.Setup(arg => arg.BlockSizeMb)
                .As('b').SetDefault(512).WithDescription("Chunk size in MB. Valid range is 1 to 1024. Default is 512");

            _fluentCommandLineParser.Setup(arg => arg.Quiet)
                .As('q').SetDefault(false).WithDescription("Quiet mode (Do not show header or total number of hits)");

            _fluentCommandLineParser.Setup(arg => arg.QuietQuiet)
                .As('s')
                .SetDefault(false)
                .WithDescription(
                    "Really Quiet mode (Do not display hits to console. Speeds up processing when using -o)");

            _fluentCommandLineParser.Setup(arg => arg.MaximumLength)
                .As('x').SetDefault(-1).WithDescription("Maximum string length. Default is unlimited\r\n");

            _fluentCommandLineParser.Setup(arg => arg.GetPatterns)
                .As('p').SetDefault(false).WithDescription("Display list of built in regular expressions");

            _fluentCommandLineParser.Setup(arg => arg.LookForString)
                .As("ls")
                .SetDefault(string.Empty)
                .WithDescription("String to look for. When set, only matching strings are returned");

            _fluentCommandLineParser.Setup(arg => arg.LookForRegex)
                .As("lr")
                .SetDefault(string.Empty)
                .WithDescription("Regex to look for. When set, only strings matching the regex are returned");

            _fluentCommandLineParser.Setup(arg => arg.StringFile)
                .As("fs")
                .SetDefault(string.Empty)
                .WithDescription("File containing strings to look for. When set, only matching strings are returned");

            _fluentCommandLineParser.Setup(arg => arg.RegexFile)
                .As("fr")
                .SetDefault(string.Empty)
                .WithDescription(
                    "File containing regex patterns to look for. When set, only strings matching regex patterns are returned\r\n");


            _fluentCommandLineParser.Setup(arg => arg.AsciiRange)
                .As("ar")
                .SetDefault("[\x20-\x7E]")
                .WithDescription(
                    @"Range of characters to search for in 'Code page' strings. Specify as a range of characters in hex format and enclose in quotes. Default is [\x20 -\x7E]");

            _fluentCommandLineParser.Setup(arg => arg.UnicodeRange)
                .As("ur")
                .SetDefault("[\u0020-\u007E]")
                .WithDescription(
                    "Range of characters to search for in Unicode strings. Specify as a range of characters in hex format and enclose in quotes. Default is [\\u0020-\\u007E]\r\n");

            _fluentCommandLineParser.Setup(arg => arg.CodePage)
                .As("cp")
                .SetDefault(1252)
                .WithDescription(
                    "Code page to use. Default is 1252. Use the Identifier value for code pages at https://goo.gl/ig6DxW");

            _fluentCommandLineParser.Setup(arg => arg.FileMask)
                .As("mask")
                .SetDefault(string.Empty)
                .WithDescription(
                    "When using -d, file mask to search for. * and ? are supported. This option has no effect when using -f");

            _fluentCommandLineParser.Setup(arg => arg.RegexOnly)
                .As("ro")
                .SetDefault(false)
                .WithDescription(
                    "When true, list the string matched by regex pattern vs string the pattern was found in (This may result in duplicate strings in output. ~ denotes approx. offset)");

            _fluentCommandLineParser.Setup(arg => arg.ShowOffset)
                .As("off")
                .SetDefault(false)
                .WithDescription(
                    $"Show offset to hit after string, followed by the encoding (A={_fluentCommandLineParser.Object.CodePage}, U=Unicode)\r\n");

            _fluentCommandLineParser.Setup(arg => arg.SortAlpha)
                .As("sa").SetDefault(false).WithDescription("Sort results alphabetically");

            _fluentCommandLineParser.Setup(arg => arg.SortLength)
                .As("sl").SetDefault(false).WithDescription("Sort results by length");


            var header =
                $"bstrings version {Assembly.GetExecutingAssembly().GetName().Version}" +
                "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
                "\r\nhttps://github.com/EricZimmerman/bstrings";

            var footer = @"Examples: bstrings.exe -f ""C:\Temp\UsrClass 1.dat"" --ls URL" + "\r\n\t " +
                         @" bstrings.exe -f ""C:\Temp\someFile.txt"" --lr guid" + "\r\n\t " +
                         @" bstrings.exe -f ""C:\Temp\aBigFile.bin"" --fs c:\temp\searchStrings.txt --fr c:\temp\searchRegex.txt -s" +
                         "\r\n\t " +
                         @" bstrings.exe -d ""C:\Temp"" --mask ""*.dll""" + "\r\n\t " +
                         @" bstrings.exe -d ""C:\Temp"" --ar ""[\x20-\x37]""" + "\r\n\t " +
                         @" bstrings.exe -d ""C:\Temp"" --cp 10007" + "\r\n\t " +
                         @" bstrings.exe -d ""C:\Temp"" --ls test" + "\r\n\t " +
                         @" bstrings.exe -f ""C:\Temp\someOtherFile.txt"" --lr cc -sa" + "\r\n\t " +
                         @" bstrings.exe -f ""C:\Temp\someOtherFile.txt"" --lr cc -sa -m 15 -x 22" + "\r\n\t " +
                         @" bstrings.exe -f ""C:\Temp\UsrClass 1.dat"" --ls mui -sl" + "\r\n\t ";

            _fluentCommandLineParser.SetupHelp("?", "help")
                .WithHeader(header)
                .Callback(text => _logger.Info(text + "\r\n" + footer));

            var result = _fluentCommandLineParser.Parse(args);

            if (result.HelpCalled)
            {
                return;
            }

            if (_fluentCommandLineParser.Object.GetPatterns)
            {
                _logger.Warn("Name \t\tDescription");
                foreach (var regExPattern in RegExPatterns.OrderBy(t=>t.Key))
                {
                    var desc = RegExDesc[regExPattern.Key];
                    _logger.Info($"{regExPattern.Key}\t{desc}");
                }

                _logger.Info("");
                _logger.Info("To use a built in pattern, supply the Name to the --lr switch\r\n");

                return;
            }

            if (result.HasErrors)
            {
                _logger.Error("");
                _logger.Error(result.ErrorText);

                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                return;
            }

            if (_fluentCommandLineParser.Object.File.IsNullOrEmpty() &&
                _fluentCommandLineParser.Object.Directory.IsNullOrEmpty())
            {
                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                _logger.Warn("Either -f or -d is required. Exiting");
                return;
            }

            if (_fluentCommandLineParser.Object.File.IsNullOrEmpty() == false &&
                !File.Exists(_fluentCommandLineParser.Object.File) &&
                _fluentCommandLineParser.Object.FileMask.Length == 0)
            {
                _logger.Warn($"File '{_fluentCommandLineParser.Object.File}' not found. Exiting");
                return;
            }

            if (_fluentCommandLineParser.Object.Directory.IsNullOrEmpty() == false &&
                !Directory.Exists(_fluentCommandLineParser.Object.Directory) &&
                _fluentCommandLineParser.Object.FileMask.Length == 0)
            {
                _logger.Warn($"Directory '{_fluentCommandLineParser.Object.Directory}' not found. Exiting");
                return;
            }

            if (!_fluentCommandLineParser.Object.Quiet)
            {
                _logger.Info(header);
                _logger.Info("");
            }

            var files = new List<string>();

            if (_fluentCommandLineParser.Object.File.IsNullOrEmpty() == false)
            {
                files.Add(Path.GetFullPath(_fluentCommandLineParser.Object.File));
            }
            else
            {
                try
                {
                    if (_fluentCommandLineParser.Object.FileMask.Length > 0)
                    {
                        files.AddRange(Directory.EnumerateFiles(Path.GetFullPath(_fluentCommandLineParser.Object.Directory),
                            _fluentCommandLineParser.Object.FileMask, SearchOption.AllDirectories));
                    }
                    else
                    {
                        files.AddRange(Directory.EnumerateFiles(Path.GetFullPath(_fluentCommandLineParser.Object.Directory), "*",
                            SearchOption.AllDirectories));
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(
                        $"Error getting files in '{_fluentCommandLineParser.Object.Directory}'. Error message: {ex.Message}");
                    return;
                }
            }

            if (!_fluentCommandLineParser.Object.Quiet)
            {
                _logger.Info($"Command line: {string.Join(" ", args)}");
                _logger.Info("");
            }

            StreamWriter sw = null;

            var globalCounter = 0;
            var globalHits = 0;
            double globalTimespan = 0;
            var withBoundaryHits = false;

            if (_fluentCommandLineParser.Object.SaveTo.IsNullOrEmpty() == false &&  _fluentCommandLineParser.Object.SaveTo.Length > 0)
            {
                _fluentCommandLineParser.Object.SaveTo = _fluentCommandLineParser.Object.SaveTo.TrimEnd('\\');

                var dir = Path.GetDirectoryName(_fluentCommandLineParser.Object.SaveTo);

                if (dir != null && Directory.Exists(dir) == false)
                {
                    try
                    {
                        Directory.CreateDirectory(dir);
                    }
                    catch (Exception)
                    {
                        _logger.Warn(
                            $"Invalid path: '{_fluentCommandLineParser.Object.SaveTo}'. Results will not be saved to a file.");
                        _logger.Info("");
                        _fluentCommandLineParser.Object.SaveTo = string.Empty;
                    }
                }
                else
                {
                    if (dir == null)
                    {
                        _logger.Warn($"Invalid path: '{_fluentCommandLineParser.Object.SaveTo}");
                        _fluentCommandLineParser.Object.SaveTo = string.Empty;
                    }
                }

                if (_fluentCommandLineParser.Object.SaveTo.Length > 0 && !_fluentCommandLineParser.Object.Quiet)
                {
                    _logger.Info($"Saving hits to '{_fluentCommandLineParser.Object.SaveTo}'");
                    _logger.Info("");
                }

                if (_fluentCommandLineParser.Object.SaveTo.Length > 0)
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
                    sw = new StreamWriter(_fluentCommandLineParser.Object.SaveTo, true);
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


                var regPattern = _fluentCommandLineParser.Object.LookForRegex;

                if (RegExPatterns.ContainsKey(_fluentCommandLineParser.Object.LookForRegex))
                {
                    regPattern = RegExPatterns[_fluentCommandLineParser.Object.LookForRegex];
                }

                if (regPattern.Length > 0 && !_fluentCommandLineParser.Object.Quiet)
                {
                    _logger.Info($"Searching via RegEx pattern: {regPattern}");
                    _logger.Info("");
                }

                var minLength = 3;
                if (_fluentCommandLineParser.Object.MinimumLength > 0)
                {
                    minLength = _fluentCommandLineParser.Object.MinimumLength;
                }

                var maxLength = -1;

                if (_fluentCommandLineParser.Object.MaximumLength > minLength)
                {
                    maxLength = _fluentCommandLineParser.Object.MaximumLength;
                }

                var chunkSizeMb = _fluentCommandLineParser.Object.BlockSizeMb < 1 ||
                                  _fluentCommandLineParser.Object.BlockSizeMb > 1024
                    ? 512
                    : _fluentCommandLineParser.Object.BlockSizeMb;
                var chunkSizeBytes = chunkSizeMb*1024*1024;

                var fileSizeBytes = new FileInfo(file).Length;
                var bytesRemaining = fileSizeBytes;
                long offset = 0;

                var chunkIndex = 1;
                var totalChunks = fileSizeBytes/chunkSizeBytes + 1;
                var hsuffix = totalChunks == 1 ? "" : "s";

                if (!_fluentCommandLineParser.Object.Quiet)
                {
                    _logger.Info(
                        $"Searching {totalChunks:N0} chunk{hsuffix} ({chunkSizeMb} MB each) across {GetSizeReadable(fileSizeBytes)} in '{file}'");
                    _logger.Info("");
                }

                try
                {
                    MappedStream ms = null;

                    try
                    {
                       var fs =
                            File.Open(File.GetFileSystemEntryInfo(file).LongFullPath, FileMode.Open, FileAccess.Read,
                                FileShare.Read, PathFormat.LongFullPath);

                        ms = MappedStream.FromStream(fs,Ownership.None);
                    }
                    catch (Exception)
                    {
                       _logger.Warn($"Unable to open file directly. This usually means the file is in use. Switching to raw access\r\n");
                    }

                    if (ms == null)
                    {
                        //raw mode
                       var ss =  OpenFile(file);

                        ms = MappedStream.FromStream(ss,Ownership.None);
                    }
                    

                    using (ms)
                    {
                        while (bytesRemaining > 0)
                        {
                            if (bytesRemaining <= chunkSizeBytes)
                            {
                                chunkSizeBytes = (int) bytesRemaining;
                            }
                            var chunk = new byte[chunkSizeBytes];

                            ms.Read(chunk, 0, chunkSizeBytes);

                            if (_fluentCommandLineParser.Object.GetUnicode)
                            {
                                var uh = GetUnicodeHits(chunk, minLength, maxLength, offset,
                                    _fluentCommandLineParser.Object.ShowOffset);
                                foreach (var h in uh)
                                {
                                    hits.Add(h);
                                }
                            }

                            if (_fluentCommandLineParser.Object.GetAscii)
                            {
                                var ah = GetAsciiHits(chunk, minLength, maxLength, offset,
                                    _fluentCommandLineParser.Object.ShowOffset);
                                foreach (var h in ah)
                                {
                                    hits.Add(h);
                                }
                            }

                            offset += chunkSizeBytes;
                            bytesRemaining -= chunkSizeBytes;

                            if (!_fluentCommandLineParser.Object.Quiet)
                            {
                                _logger.Info(
                                    $"Chunk {chunkIndex:N0} of {totalChunks:N0} finished. Total strings so far: {hits.Count:N0} Elapsed time: {_sw.Elapsed.TotalSeconds:N3} seconds. Average strings/sec: {hits.Count/_sw.Elapsed.TotalSeconds:N0}");
                            }
                            
                            chunkIndex += 1;
                        }

                        //do chunk boundary checks to make sure we get everything and not split things

                        if (!_fluentCommandLineParser.Object.Quiet)
                        {
                            _logger.Info(
                                "Primary search complete. Looking for strings across chunk boundaries...");
                        }

                        bytesRemaining = fileSizeBytes;
                        chunkSizeBytes = chunkSizeMb*1024*1024;
                        offset = chunkSizeBytes - _fluentCommandLineParser.Object.MinimumLength*10*2;
                        //move starting point backwards for our starting point
                        chunkIndex = 0;

                        var boundaryChunkSize = _fluentCommandLineParser.Object.MinimumLength*10*2*2;
                        //grab the same # of bytes on both sides of the boundary

                        while (bytesRemaining > 0)
                        {
                            if (offset + boundaryChunkSize > fileSizeBytes)
                            {
                                break;
                            }

                         
                            var chunk = new byte[boundaryChunkSize];

                            ms.Read(chunk, 0, boundaryChunkSize);

                            if (_fluentCommandLineParser.Object.GetUnicode)
                            {
                                var uh = GetUnicodeHits(chunk, minLength, maxLength, offset,
                                    _fluentCommandLineParser.Object.ShowOffset);
                                foreach (var h in uh)
                                {
                                    hits.Add("  " + h);
                                }

                                if (withBoundaryHits == false && uh.Count > 0)
                                {
                                    withBoundaryHits = uh.Count > 0;
                                }
                            }

                            if (_fluentCommandLineParser.Object.GetAscii)
                            {
                                var ah = GetAsciiHits(chunk, minLength, maxLength, offset,
                                    _fluentCommandLineParser.Object.ShowOffset);
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

                if (!_fluentCommandLineParser.Object.Quiet)
                {
                    _logger.Info("Search complete.");
                    _logger.Info("");
                }

                if (_fluentCommandLineParser.Object.SortAlpha)
                {
                    _logger.Info("Sorting alphabetically...");
                    _logger.Info("");
                    var tempList = hits.ToList();
                    tempList.Sort();
                    hits = new HashSet<string>(tempList);
                }
                else if (_fluentCommandLineParser.Object.SortLength)
                {
                    _logger.Info("Sorting by length...");
                    _logger.Info("");
                    var tempList = SortByLength(hits.ToList()).ToList();
                    hits = new HashSet<string>(tempList);
                }

                var fileStrings = new HashSet<string>();
                var regexStrings = new HashSet<string>();

                //set up highlighting
                if (_fluentCommandLineParser.Object.LookForString.Length > 0)
                {
                    fileStrings.Add(_fluentCommandLineParser.Object.LookForString);
                }

                if (_fluentCommandLineParser.Object.LookForRegex.Length > 0)
                {
                    regexStrings.Add(regPattern);
                }

                if (_fluentCommandLineParser.Object.StringFile.IsNullOrEmpty() == false || _fluentCommandLineParser.Object.RegexFile.IsNullOrEmpty() == false)
                {
                    if (_fluentCommandLineParser.Object.StringFile.Length > 0)
                    {
                        if (File.Exists(_fluentCommandLineParser.Object.StringFile))
                        {
                            fileStrings.UnionWith(new HashSet<string>( File.ReadAllLines(_fluentCommandLineParser.Object.StringFile)));
                        }
                        else
                        {
                            _logger.Error($"Strings file '{_fluentCommandLineParser.Object.StringFile}' not found.");
                        }
                    }
                    
                    if (_fluentCommandLineParser.Object.RegexFile.Length > 0)
                    {
                        if (File.Exists(_fluentCommandLineParser.Object.RegexFile))
                        {
                            regexStrings.UnionWith(new HashSet<string>(File.ReadAllLines(_fluentCommandLineParser.Object.RegexFile)));
                        }
                        else
                        {
                            _logger.Error($"Regex file '{_fluentCommandLineParser.Object.RegexFile}' not found.");
                        }
                    }
                }

                AddHighlightingRules(fileStrings.ToList());

                if (_fluentCommandLineParser.Object.RegexOnly == false)
                {
                    AddHighlightingRules(regexStrings.ToList(), true);
                }

              
                if (!_fluentCommandLineParser.Object.Quiet)
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

                            if (_fluentCommandLineParser.Object.QuietQuiet == false)
                            {
                                _logger.Info(hit);
                            }

                            sw?.WriteLine(hit);
                        }

                        var hitoffset = "";
                        if (_fluentCommandLineParser.Object.ShowOffset)
                        {
                            hitoffset = $"~{hit.Split('\t').Last()}";
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

                                if (_fluentCommandLineParser.Object.RegexOnly)
                                {
                                    foreach (var match in reg1.Matches(hit))
                                    {
                                        if (_fluentCommandLineParser.Object.QuietQuiet == false)
                                        {
                                            _logger.Info($"{match}\t{hitoffset}");
                                        }

                                        sw?.WriteLine($"{match}\t{hitoffset}");
                                    }
                                }
                                else
                                {
                                    if (_fluentCommandLineParser.Object.QuietQuiet == false)
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
                        
                        if (_fluentCommandLineParser.Object.QuietQuiet == false)
                        {
                            _logger.Info(hit);
                        }
                        
                        sw?.WriteLine(hit);
                    }
                }

                if (_fluentCommandLineParser.Object.Quiet)
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
                    $"Found {counter:N0} string{suffix} in {_sw.Elapsed.TotalSeconds:N3} seconds. Average strings/sec: {hits.Count/_sw.Elapsed.TotalSeconds:N0}");
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

            if (!_fluentCommandLineParser.Object.Quiet && files.Count > 1)
            {
                var suffix = globalCounter == 1 ? "" : "s";

                _logger.Info("");
                _logger.Info(
                    $"Found {globalCounter:N0} string{suffix} in {globalTimespan:N3} seconds. Average strings/sec: {globalHits/globalTimespan:N0}");
            }
        }

        private static IFileSystem _fileSystem;
        private static SparseStream OpenFile(string path)
        {
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
            readable = readable/1024;

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
            RegExPatterns.Add("ipv4",@"\b(?:(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])\b");
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
            var target = (ColoredConsoleTarget) LogManager.Configuration.FindTargetByName("console");
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
                var r = new ConsoleWordHighlightingRule {IgnoreCase = true};
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
            bool withOffsets)
        {
            var maxString = maxSize == -1 ? "" : maxSize.ToString();
            var mi2 = $"{"{"}{minSize}{","}{maxString}{"}"}";

            var uniRange = _fluentCommandLineParser.Object.UnicodeRange; 
            var regUni = new Regex($"{uniRange}{mi2}", RegexOptions.Compiled);
            var uniString = Encoding.Unicode.GetString(bytes);

            var hits = new List<string>();

            foreach (Match match in regUni.Matches(uniString))
            {
                if (withOffsets)
                {
                    var actualOffset = (currentOffset + match.Index)*2;

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
            bool withOffsets)
        {
            var maxString = maxSize == -1 ? "" : maxSize.ToString();
            var mi2 = $"{"{"}{minSize}{","}{maxString}{"}"}";

            var ascRange = _fluentCommandLineParser.Object.AsciiRange; 
            var regAsc = new Regex($"{ascRange}{mi2}", RegexOptions.Compiled);
            var ascString = Encoding.GetEncoding(_fluentCommandLineParser.Object.CodePage).GetString(bytes);

            var hits = new List<string>();

            foreach (Match match in regAsc.Matches(ascString))
            {
                if (withOffsets)
                {
                    var matchBytes = Encoding.GetEncoding(_fluentCommandLineParser.Object.CodePage)
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

        private static readonly string BaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static void SetupNLog()
        {
            if (File.Exists( Path.Combine(BaseDirectory,"Nlog.config")))
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

    internal class ApplicationArguments
    {
        public string File { get; set; }
        public string Directory { get; set; }
        public string SaveTo { get; set; } = string.Empty;
        public bool GetAscii { get; set; } = true;
        public bool GetUnicode { get; set; } = true;
        public string LookForString { get; set; } = string.Empty;
        public string FileMask { get; set; } = string.Empty;
        public string StringFile { get; set; } = string.Empty;
        public string RegexFile { get; set; } = string.Empty;
        public string LookForRegex { get; set; } = string.Empty;
        public string AsciiRange { get; set; } = "[\x20-\x7E]";
        public string UnicodeRange { get; set; } = "[\u0020-\u007E]";
        public int MinimumLength { get; set; } = 3;
        public int MaximumLength { get; set; } = -1;
        public int CodePage { get; set; } = 1252;
        public int BlockSizeMb { get; set; } = 512;
        public bool ShowOffset { get; set; } = false;
        public bool SortLength { get; set; } = false;
        public bool SortAlpha { get; set; } = false;
        public bool Quiet { get; set; } = false;
        public bool QuietQuiet { get; set; } = false;
        public bool GetPatterns { get; set; } = false;
        public bool RegexOnly { get; set; } = false;
    }
}