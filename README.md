# bstrings
A better strings utility!

NOTE: bstrings currently doesn't support files > 2GB. I will add support for large files soon.


    λ .\bstrings.exe
  
    bstrings version 0.9.0.0
  
    Author: Eric Zimmerman (saericzimmerman@gmail.com)
    https://github.com/EricZimmerman/bstrings
  
          a               If true, look for ASCII strings. Default is true
          f               File to search. This is required
          m               Minimum string length. Default is 3
          o               File to save results to
          u               If true, look for Unicode strings. Default is true
          x               Maximum string length. Default is unlimited
          ls              String to look for. When set, only matching strings are returned.
          lr              Regex to look for. When set, only matching strings are returned.
          sa              Sort results alphabetically
          sl              Sort results by length
