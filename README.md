# bstrings
A better strings utility!


    λ .\bstrings.exe
  
    bstrings version 0.9.5.0

    Author: Eric Zimmerman (saericzimmerman@gmail.com)
    https://github.com/EricZimmerman/bstrings
    
            a               If set, look for ASCII strings. Default is true. Use -a false to disable
            b               Chunk size in MB. Valid range is 1 to 1024. Default is 512
            f               File to search. This is required
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
              bstrings.exe -f "C:\Temp\someOtherFile.txt" --lr cc -sa
              bstrings.exe -f "C:\Temp\someOtherFile.txt" --lr cc -sa -m 15 -x 22
              bstrings.exe -f "C:\Temp\UsrClass 1.dat" --ls mui -sl
