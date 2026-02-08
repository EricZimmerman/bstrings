namespace memory_generator;

internal class Program
{
    static void Main(string[] args)
    {
        //
        // Generate random file content and 
        // add some strings that are supposed to be found by the following command
        //
        // bstrings.exe -f "benchmark.dmp" --ls bstrings --lr unc -b 10 --off
        //

        // Step 1: Generate a file with random content
        //         Random content does not contain ASCII characters that are recognized by bstrings
        var fileSize = 512 * 1024 * 1024;

        var rng = new Random(0x42);
        var asiiRangeLowerBound = 0x20;
        var asiiRangeUpperBound = 0x7E;
        var maxRng = 256 - (asiiRangeUpperBound - asiiRangeLowerBound + 1);
        var memstream = new MemoryStream(fileSize);

        for (int i = 0; i < fileSize; i++)
        {
            var nextByte = rng.Next(maxRng);

            if (nextByte >= asiiRangeLowerBound)
                nextByte += asiiRangeUpperBound - asiiRangeLowerBound + 1;

            memstream.WriteByte((byte)nextByte);
        }

        // Step 2: All all test strings that should be found by bstrings
        //

        // Test 1: Unicode text over the end of the second chunk (address 0x27FFFAC)
        var chunkBoundaryText = System.Text.Encoding.Unicode.GetBytes("DFIR with bstrings rocks");
        memstream.Position = 20 * 1024 * 1024 - chunkBoundaryText.Length / 2;
        memstream.Write(chunkBoundaryText, 0, chunkBoundaryText.Length);

        // Test 2: Ascii UNC path (address 0x4000000)
        var uncPathText = System.Text.Encoding.ASCII.GetBytes(@"\\.\root");
        memstream.Position = 0x0400_0000;
        memstream.Write(uncPathText, 0, uncPathText.Length);

        // Step 3: Write the binary content to the output file
        memstream.Position = 0;
        using (FileStream fileStream = new FileStream("benchmark.dmp", FileMode.Create, FileAccess.Write))
        {
            memstream.CopyTo(fileStream);
        }
    }
}
