using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Fclp;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace bstrings
{
    internal class Program
    {
        private static Logger _logger;
        private static Stopwatch _sw;

        private static void Main(string[] args)
        {
            SetupNLog();

            _logger = LogManager.GetCurrentClassLogger();

            var p = new FluentCommandLineParser<ApplicationArguments>
            {
                IsCaseSensitive = false
            };

            p.Setup(arg => arg.File)
                .As('f')
                .WithDescription("File to search. This is required").Required();

            p.Setup(arg => arg.SaveTo)
                .As('o')
                .WithDescription("File to save results to");

            p.Setup(arg => arg.GetAscii)
                .As('a').SetDefault(true).WithDescription("If true, look for ASCII strings. Default is true");

            p.Setup(arg => arg.GetUnicode)
                .As('u').SetDefault(true).WithDescription("If true, look for Unicode strings. Default is true");

            p.Setup(arg => arg.MinimumLength)
                .As('m').SetDefault(3).WithDescription("Minimum string length. Default is 3");

            p.Setup(arg => arg.MaximumLength)
                .As('x').SetDefault(-1).WithDescription("Maximum string length. Default is unlimited");

            p.Setup(arg => arg.LookForString)
                .As("ls")
                .SetDefault(string.Empty)
                .WithDescription("String to look for. When set, only matching strings are returned.");

            p.Setup(arg => arg.LookForRegex)
                .As("lr")
                .SetDefault(string.Empty)
                .WithDescription("Regex to look for. When set, only matching strings are returned.");

            p.Setup(arg => arg.SortAlpha)
                .As("sa").SetDefault(false).WithDescription("Sort results alphabetically");

            p.Setup(arg => arg.SortLength)
                .As("sl").SetDefault(false).WithDescription("Sort results by length");

            //  p.Setup(arg => arg.ShowOffset).As('o').SetDefault(false).WithDescription("Show offset to hit before string");

            var header =
                $"bstrings version {Assembly.GetExecutingAssembly().GetName().Version}" +
                "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
                "\r\nhttps://github.com/EricZimmerman/bstrings";

            p.SetupHelp("?", "help").WithHeader(header).Callback(text => _logger.Info(text));

            var result = p.Parse(args);

            if (result.HelpCalled)
            {
                return;
            }

            if (result.HasErrors)
            {
                p.HelpOption.ShowHelp(p.Options);

                return;
            }

            if (!File.Exists(p.Object.File))
            {
                _logger.Warn($"'{p.Object.File}' not found. Exiting");
                return;
            }

            _logger.Info(header);
            _logger.Info("");

            _sw = new Stopwatch();
            _sw.Start();

            var rawBytes = File.ReadAllBytes(p.Object.File);

            var hits = new List<string>();

            var minLength = 3;
            if (p.Object.MinimumLength > 0)
            {
                minLength = p.Object.MinimumLength;
            }

            var maxLength = -1;

            if (p.Object.MaximumLength > minLength)
            {
                maxLength = p.Object.MaximumLength;
            }

            if (p.Object.GetUnicode)
            {
                hits.AddRange(GetUnicodeHits(rawBytes, minLength, maxLength));
            }

            if (p.Object.GetAscii)
            {
                hits.AddRange(GetAsciiHits(rawBytes, minLength, maxLength));
            }

            if (p.Object.SortAlpha)
            {
                hits.Sort();
            }
            else if (p.Object.SortLength)
            {
                hits = SortByLength(hits).ToList();
            }

            var counter = 0;

            var set = new HashSet<string>(hits);

            _sw.Stop();

            var reg = new Regex(p.Object.LookForRegex, RegexOptions.IgnoreCase & RegexOptions.Compiled);

            //set up highlighting
            var words = new HashSet<string>();
            if (p.Object.LookForString.Length > 0)
            {
                words.Add(p.Object.LookForString);
            }
            else if (p.Object.LookForRegex.Length > 0)
            {
                words.Add(p.Object.LookForRegex);
            }

            AddHighlightingRules(words.ToList(), p.Object.LookForRegex.Length>0);

            foreach (var hit in set)
            {
                if (hit.Trim().Length == 0)
                {
                    continue;
                }

                if (p.Object.LookForString.Length > 0 || p.Object.LookForRegex.Length > 0)
                {
                    if (p.Object.LookForString.Length > 0 && hit.Contains(p.Object.LookForString))
                    {
                        counter += 1;
                        _logger.Info(hit);
                    }
                    else if (p.Object.LookForRegex.Length > 0)
                    {
                        if (!reg.IsMatch(hit))
                        {
                            continue;
                        }
                        counter += 1;
                        _logger.Info(hit);
                    }
                }
                else
                {
                    counter += 1;
                    _logger.Info(hit);
                }
            }

            var suffix = counter == 1 ? "" : "s";

            _logger.Info("");
            _logger.Info($"Found {counter:N0} string{suffix} in {_sw.Elapsed.TotalSeconds:N3} seconds");
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

            target.WordHighlightingRules.Clear();

            foreach (var word in words)
            {
                var r = new ConsoleWordHighlightingRule();
                r.IgnoreCase = true;
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

        private static List<string> GetUnicodeHits(byte[] bytes, int minSize, int maxSize)
        {
            var maxString = maxSize == -1 ? "" : maxSize.ToString();
            var mi2 = string.Format("{0}{1}{2}{3}{4}", "{", minSize, ",", maxString, "}");

            const string uniRange = "[\u0020-\u007E]";
            var regUni = new Regex($"{uniRange}{mi2}");
            var uniString = Encoding.Unicode.GetString(bytes);

            return (from Match match in regUni.Matches(uniString) select match.Value).ToList();
        }

        private static List<string> GetAsciiHits(byte[] bytes, int minSize, int maxSize)
        {
            var maxString = maxSize == -1 ? "" : maxSize.ToString();
            var mi2 = string.Format("{0}{1}{2}{3}{4}", "{", minSize, ",", maxString, "}");

            const string ascRange = "[\x20-\x7E]";
            var regUni = new Regex($"{ascRange}{mi2}");
            var uniString = Encoding.UTF7.GetString(bytes);

            return (from Match match in regUni.Matches(uniString) select match.Value).ToList();
        }

        private static void SetupNLog()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

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
        public string SaveTo { get; set; } = string.Empty;
        public bool GetAscii { get; set; } = true;
        public bool GetUnicode { get; set; } = true;
        public string LookForString { get; set; } = string.Empty;
        public string LookForRegex { get; set; } = string.Empty;
        public int MinimumLength { get; set; } = 3;
        public int MaximumLength { get; set; } = -1;
        public bool ShowOffset { get; set; } = false;
        public bool SortLength { get; set; } = false;
        public bool SortAlpha { get; set; } = false;
    }
}