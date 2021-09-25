# bstrings

A better strings utility!

## Command Line Interface

    bstrings version 1.5.1.0
    
    Author: Eric Zimmerman (saericzimmerman@gmail.com)
    https://github.com/EricZimmerman/bstrings
    
            a               If set, look for ASCII strings. Default is true. Use -a false to disable
            b               Chunk size in MB. Valid range is 1 to 1024. Default is 512
            d               Directory to recursively process. Either this or -f is required
            f               File to search. Either this or -d is required
            m               Minimum string length. Default is 3
            o               File to save results to
            p               Display list of built in regular expressions
            q               Quiet mode (Do not show header or total number of hits)
            s               Really Quiet mode (Do not display hits to console. Speeds up processing when using -o)
            u               If set, look for Unicode strings. Default is true. Use -u false to disable
            x               Maximum string length. Default is unlimited
    
            ls              String to look for. When set, only matching strings are returned
            lr              Regex to look for. When set, only strings matching the regex are returned
            fs              File containing strings to look for. When set, only matching strings are returned
            fr              File containing regex patterns to look for. When set, only strings matching regex patterns are returned
    
            ar              Range of characters to search for in 'Code page' strings. Specify as a range of characters in hex format and enclose in quotes. Default is [\x20 -\x7E]
            ur              Range of characters to search for in Unicode strings. Specify as a range of characters in hex format and enclose in quotes. Default is [\u0020-\u007E]
    
            cp              Code page to use. Default is 1252. Use the Identifier value for code pages at https://goo.gl/ig6DxW
            mask            When using -d, file mask to search for. * and ? are supported. This option has no effect when using -f
            ms              When using -d, maximum file size to process. This option has no effect when using -f
            ro              When true, list the string matched by regex pattern vs string the pattern was found in (This may result in duplicate strings in output. ~ denotes approx. offset)
            off             Show offset to hit after string, followed by the encoding (A=1252, U=Unicode)

            sa              Sort results alphabetically
            sl              Sort results by length

    Examples: bstrings.exe -f "C:\Temp\UsrClass 1.dat" --ls URL
              bstrings.exe -f "C:\Temp\someFile.txt" --lr guid
              bstrings.exe -f "C:\Temp\aBigFile.bin" --fs c:\temp\searchStrings.txt --fr c:\temp\searchRegex.txt -s
              bstrings.exe -d "C:\Temp" --mask "*.dll"
              bstrings.exe -d "C:\Temp" --ar "[\x20-\x37]"
              bstrings.exe -d "C:\Temp" --cp 10007
              bstrings.exe -d "C:\Temp" --ls test
              bstrings.exe -f "C:\Temp\someOtherFile.txt" --lr cc --sa
              bstrings.exe -f "C:\Temp\someOtherFile.txt" --lr cc --sa -m 15 -x 22
              bstrings.exe -f "C:\Temp\UsrClass 1.dat" --ls mui --sl

## Built In Regular Expressions

Run `bstrings.exe -p` to see the following list of built in Regular Expressions:

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
              
              To use a built in pattern, supply the Name to the --lr switch 

## Documentation

[Introducing bstrings, a Better Strings utility!](https://binaryforay.blogspot.com/2015/07/introducing-bstrings-better-strings.html)

[bstrings 0.9.0.0 released](https://binaryforay.blogspot.com/2015/07/bstrings-0900-released.html)

[bstrings 0.9.5.0 released](https://binaryforay.blogspot.com/2015/07/bstrings-0950-released.html)

[A few updates](https://binaryforay.blogspot.com/2015/08/a-few-updates.html)

[bstrings 0.9.7.0 released](https://binaryforay.blogspot.com/2015/11/bstrings-0970-released.html)

[bstrings 0.9.8.0 released](https://binaryforay.blogspot.com/2015/12/bstrings-0980-released.html)

[bstrings 0.9.9.0 released!](https://binaryforay.blogspot.com/2016/02/bstrings-0990-released.html)

[bstrings 1.0 released!](https://binaryforay.blogspot.com/2016/02/bstrings-10-released.html)

[bstrings v1.1 released!](https://binaryforay.blogspot.com/2016/04/bstrings-v11-released.html)

[Everything gets an update, Sept 2018 edition](https://binaryforay.blogspot.com/2018/09/everything-gets-update-sept-2018-edition.html?q=bstrings)

# Download Eric Zimmerman's Tools

All of Eric Zimmerman's tools can be downloaded [here](https://ericzimmerman.github.io/#!index.md). Use the [Get-ZimmermanTools](https://f001.backblazeb2.com/file/EricZimmermanTools/Get-ZimmermanTools.zip) PowerShell script to automate the download and updating of the EZ Tools suite. Additionally, you can automate each of these tools using [KAPE](https://www.kroll.com/en/services/cyber-risk/incident-response-litigation-support/kroll-artifact-parser-extractor-kape)!

# Special Thanks

Open Source Development funding and support provided by the following contributors: [SANS Institute](http://sans.org/) and [SANS DFIR](http://dfir.sans.org/).
