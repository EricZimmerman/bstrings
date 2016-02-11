# bstrings
A better strings utility!


    λ bstrings.exe

    bstrings version 1.0.0.0
    
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
            u               If set, look for Unicode strings. Default is true. Use -u false to disable
            x               Maximum string length. Default is unlimited
            ls              String to look for. When set, only matching strings are returned.
            ar              Range of characters to search for in 'Codepage' strings. Specify as a range of characters in hex format and enclose in quotes. Default is [\x20 -\x7E]
            ur              Range of characters to search for in Unicode strings. Specify as a range of characters in hex format and enclose in quotes. Default is [\u0020-\u007E]
            cp              Codepage to use. Default is 1252. Use the Identifier value for code pages at https://goo.gl/ig6DxW
            mask            When using -d, file mask to search for. * and ? are supported. This option has no effect when using -f
            lr              Regex to look for. When set, only matching strings are returned.
            sa              Sort results alphabetically
            sl              Sort results by length
    
    Examples: bstrings.exe -f "C:\Temp\UsrClass 1.dat" --ls URL
              bstrings.exe -f "C:\Temp\someFile.txt" --lr guid
              bstrings.exe -d "C:\Temp" --mask "*.dll"
              bstrings.exe -d "C:\Temp" --ar "[\x20-\x37]"
              bstrings.exe -d "C:\Temp" --cp 10007
              bstrings.exe -d "C:\Temp" --ls test
              bstrings.exe -f "C:\Temp\someOtherFile.txt" --lr cc -sa
              bstrings.exe -f "C:\Temp\someOtherFile.txt" --lr cc -sa -m 15 -x 22
              bstrings.exe -f "C:\Temp\UsrClass 1.dat" --ls mui -sl
    
    Either -f or -d is required. Exiting
