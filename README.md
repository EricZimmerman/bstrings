# bstrings

A better strings utility: finds code page (ASCII) and Unicode (UTF-16LE) strings in files, folders or piped input, and
can keep only those that match strings or regular expressions, including a library of built in patterns (GUIDs, IP
addresses, SIDs, paths, wallet addresses and more). On Windows, locked files are read through raw disk access.

| | |
|---|---|
| **Version** | 2026.10.1 |
| **Download** | [.NET 10](https://download.ericzimmermanstools.com/net/bstrings.zip) (recommended) &middot; [.NET Framework 4.7.2](https://download.ericzimmermanstools.com/bstrings.zip) |
| **All tools** | [ericzimmerman.github.io](https://ericzimmerman.github.io) or [Get-ZimmermanTools](https://download.ericzimmermanstools.com/Get-ZimmermanTools.zip), which downloads them all and keeps them current |
| **Changes** | [ChangeLog](https://ericzimmerman.github.io/changelog/) |
| **Issues** | [EricZimmerman/bstrings](https://github.com/EricZimmerman/bstrings/issues) or [EricZimmerman/Issues](https://github.com/EricZimmerman/Issues/issues) |

## Requirements

- **.NET 10 build**: the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0). Run `bstrings.exe` on Windows,
  or `dotnet bstrings.dll` on any system with the runtime.
- **.NET Framework build**: .NET Framework 4.7.2 or later (Windows only).

## Command line

```text
Usage:
  bstrings [options]

Options:
  -f <f>          File to search. Either this or -d is required. When neither is given, input piped to stdin is searched
  -d <d>          Directory to recursively process. Either this or -f is required
  -o <o>          File to save results to. Results are appended if the file exists
  -a              If set, look for ASCII strings. Use -a false to disable
  -u              If set, look for Unicode strings. Use -u false to disable
  -m <m>          Minimum string length [default: 3]
  -b <b>          Chunk size in MB. Valid range is 1 to 1024 [default: 512]
  -q              Quiet mode (Do not show header or total number of hits)
  -s              Really Quiet mode (Do not display hits to console. Speeds up processing when using -o)
  -x <x>          Maximum string length. Default is unlimited [default: -1]
  -p              Display list of built in regular expressions
  --ls <ls>       String to look for. When set, only matching strings are returned
  --lr <lr>       Regex to look for. When set, only strings matching the regex are returned
  --fs <fs>       File containing strings to look for. When set, only matching strings are returned
  --fr <fr>       File containing regex patterns to look for. When set, only strings matching regex patterns are returned
  --ar <ar>       Range of characters to search for in 'Code page' strings. Specify as a range of characters in hex format and enclose in quotes. Default is [\x20 -\x7E] [default: [ -~]]
  --ur <ur>       Range of characters to search for in Unicode strings. Specify as a range of characters in hex format and enclose in quotes. Default is [\\u0020-\\u007E] [default: [ -~]]
  --cp <cp>       Code page to use. Default is 1252. Use the Identifier value for code pages at https://goo.gl/ig6DxW [default: 1252]
  --mask <mask>   When using -d, file mask to search for. * and ? are supported. This option has no effect when using -f
  --ms <ms>       When using -d, maximum file size in bytes to process. This option has no effect when using -f [default: -1]
  --ro            When true, list the string matched by regex pattern vs string the pattern was found in (This may result in duplicate strings in output. ~ denotes approx. offset)
  --off           Show offset to hit after string, followed by the encoding (A=Code page, U=Unicode)
  --sa            Sort results alphabetically
  --sl            Sort results by length
  --debug         Show debug information during processing
  --trace         Show trace information during processing
  --co            When true, save all console messages to <timestamp>_bstrings_<version>_ConsoleLog.txt in the folder of the -o file. Use --co false to turn it off
  -?, -h, --help  Show help and usage information
  --version       Show version information
```

Short options (a single letter) start with one dash, long options with two.

## Examples

```text
bstrings.exe -f "C:\Temp\UsrClass 1.dat" --ls URL
bstrings.exe -f "C:\Temp\someFile.txt" --lr guid
bstrings.exe -f "C:\Temp\aBigFile.bin" --fs c:\temp\searchStrings.txt --fr c:\temp\searchRegex.txt
bstrings.exe -d "C:\Temp" --mask "*.dll"
bstrings.exe -d "C:\Temp" --ar "[\x20-\x37]"
bstrings.exe -d "C:\Temp" --cp 10007
bstrings.exe -d "C:\Temp" --ls test
bstrings.exe -f "C:\Temp\someOtherFile.txt" --lr cc --sa
bstrings.exe -f "C:\Temp\someOtherFile.txt" --lr cc --sa -m 15 -x 22
bstrings.exe -f "C:\Temp\UsrClass 1.dat" --ls mui --sl
```

## Output

- **Console**: one string per line, after a header and followed by the total number of hits (`-q` leaves those out,
  `-s` doesn't show the strings).
- **File** (`-o`): the same strings, appended if the file exists.
- **Console log** (`--co`, on by default; `--co false` turns it off): the console messages (header, command line, counts, warnings; not the strings), with times
  and levels, in `<yyyyMMddHHmmss>_bstrings_<version>_ConsoleLog.txt` (UTC time) in the folder of the `-o` file.
- With `--off`, each string is followed by its offset in the file and its encoding (`A` for code page, `U` for
  Unicode). `--sa` and `--sl` sort the results alphabetically or by length.

Each string is written once, even when several filters match it (except with `--ro`, which writes every regex match).
Filters match only the string, never the offset after it. String filters ignore case, and so do regular expressions.

## Built in regular expressions

Run `bstrings.exe -p` to see this list. To use a built in pattern, supply its name to `--lr` (for example
`--lr guid`); any other value of `--lr` is used as a regular expression.

```text
  Name            Description
  aeon            Finds Aeon wallet addresses
  b64             Finds valid formatted base 64 strings
  bitcoin         Finds BitCoin wallet addresses
  bitlocker       Finds Bitlocker recovery keys
  bytecoin        Finds ByteCoin wallet addresses
  cc              Finds credit card numbers
  dashcoin        Finds DashCoin wallet addresses (D*)
  dashcoin2       Finds DashCoin wallet addresses (7|X)*
  email           Finds embedded email addresses
  fantomcoin      Finds Fantomcoin wallet addresses
  guid            Finds GUIDs
  ipv4            Finds IP version 4 addresses
  ipv6            Finds IP version 6 addresses
  mac             Finds MAC addresses
  monero          Finds Monero wallet addresses
  reg_path        Finds paths related to Registry hives
  sid             Finds Microsoft Security Identifiers (SID)
  ssn             Finds US Social Security Numbers
  sumokoin        Finds SumoKoin wallet addresses
  unc             Finds UNC paths
  url3986         Finds URLs according to RFC 3986
  urlUser         Finds usernames in URLs
  usPhone         Finds US phone numbers
  var_set         Finds environment variables being set (OS=Windows_NT)
  win_path        Finds Windows style paths (C:\folder1\folder2\file.txt)
  xml             Finds XML/HTML tags
  zip             Finds zip codes
```

## How strings are found

Files are read in chunks (`-b`). A string that runs past the end of a chunk is carried over to the next one, so results
are the same whatever the chunk size and strings are never split at chunk boundaries. Unicode (UTF-16LE) strings are
found at both even and odd byte offsets. With `--off`, the offset is the exact file offset of the first character of
the string (after leading whitespace is trimmed).

With `-d`, folders are searched recursively; empty files and reparse points are skipped, and folders that can't be read
are reported and the rest is still searched. Reading locked files through raw disk access needs Windows.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success. Any file is valid input, so file contents never give 1; an empty redirected input is 0 too |
| `1` | The tool failed: invalid arguments, a missing `-f` or `-d` path, a file it can't open or read (locked and the raw read also failed, IO errors), or an `-o` file that can't be written. Folders under `-d` that can't be read are reported (`Unable to read folder <path>: <reason>`) and the rest is still processed |

## What's new in 2026.10

- About 1.6x faster with about a third less memory (152 MB library: 1.50 s -> 0.95 s, 1.27 GB -> 0.81 GB peak); console
  output about 3x faster.
- Strings that cross chunk boundaries are found whole instead of as fragments, so output no longer depends on `-b`.
- Exact offsets for Unicode strings, and for `--off` with trimmed whitespace or multi-byte code pages; UTF-16LE strings
  that start at odd byte offsets are found.
- Regex filters match only the string, not the offset suffix (anchored patterns such as `b64`, `sid`, `cc` and `zip`
  failed with `--off`); regex filters no longer match across spaces, and the `url3986` and `reg_path` patterns are
  fixed.
- A string that matches several filters is written once.
- `-f` and `-d` work when input is redirected (#17); directory scans skip inaccessible folders, empty files and reparse
  points instead of stopping.
- Exit code 1 when the tool fails (it was always 0). Error reports include source file and line numbers.
- New `--co` (on by default; `--co false` turns it off): saves the console messages to `<timestamp>_bstrings_<version>_ConsoleLog.txt` in the folder of the `-o`
  file (Issues #259) (2026.10.1)

The full list is in the [ChangeLog](https://ericzimmerman.github.io/changelog/).

## Building

```text
dotnet build bstrings.slnx -c Release
```

Builds `net10.0` and `net472`.

## Further reading

- [Introducing bstrings, a Better Strings utility!](https://binaryforay.blogspot.com/2015/07/introducing-bstrings-better-strings.html)
- [bstrings 0.9.0.0 released](https://binaryforay.blogspot.com/2015/07/bstrings-0900-released.html)
- [bstrings 0.9.5.0 released](https://binaryforay.blogspot.com/2015/07/bstrings-0950-released.html)
- [A few updates](https://binaryforay.blogspot.com/2015/08/a-few-updates.html)
- [bstrings 0.9.7.0 released](https://binaryforay.blogspot.com/2015/11/bstrings-0970-released.html)
- [bstrings 0.9.8.0 released](https://binaryforay.blogspot.com/2015/12/bstrings-0980-released.html)
- [bstrings 0.9.9.0 released!](https://binaryforay.blogspot.com/2016/02/bstrings-0990-released.html)
- [bstrings 1.0 released!](https://binaryforay.blogspot.com/2016/02/bstrings-10-released.html)
- [bstrings v1.1 released!](https://binaryforay.blogspot.com/2016/04/bstrings-v11-released.html)
- [Everything gets an update, Sept 2018 edition](https://binaryforay.blogspot.com/2018/09/everything-gets-update-sept-2018-edition.html)

## Special thanks

Open source development funding and support provided by the [SANS Institute](https://www.sans.org/) and
[SANS DFIR](https://www.sans.org/digital-forensics-incident-response/).

## License

[MIT](LICENSE.md), Copyright (c) Eric R. Zimmerman 2027.
