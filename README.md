# bstrings
A better strings utility!


    λ bstrings.exe

    bstrings version 0.9.7.0
    
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
            lr              Regex to look for. When set, only matching strings are returned.
            sa              Sort results alphabetically
            sl              Sort results by length
    
    Examples: bstrings.exe -f "C:\Temp\UsrClass 1.dat" --ls URL
              bstrings.exe -f "C:\Temp\someFile.txt" --lr guid
              bstrings.exe -d "C:\Temp" --ls test
              bstrings.exe -f "C:\Temp\someOtherFile.txt" --lr cc -sa
              bstrings.exe -f "C:\Temp\someOtherFile.txt" --lr cc -sa -m 15 -x 22
              bstrings.exe -f "C:\Temp\UsrClass 1.dat" --ls mui -sl
    
    Either -f or -d is required. Exiting
