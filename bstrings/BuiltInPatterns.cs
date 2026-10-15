using System.Collections.Generic;

namespace bstrings;

/// <summary>
///     Built-in regular expressions, used by name with --lr
/// </summary>
internal static class BuiltInPatterns
{
    public static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>();
    public static readonly Dictionary<string, string> Patterns = new Dictionary<string, string>();

    static BuiltInPatterns()
    {
        Descriptions.Add("guid", "\tFinds GUIDs");
        Descriptions.Add("usPhone", "\tFinds US phone numbers");
        Descriptions.Add("unc", "\tFinds UNC paths");
        Descriptions.Add("mac", "\tFinds MAC addresses");
        Descriptions.Add("ssn", "\tFinds US Social Security Numbers");
        Descriptions.Add("cc", "\tFinds credit card numbers");

        Descriptions.Add("ipv4", "\tFinds IP version 4 addresses");
        Descriptions.Add("ipv6", "\tFinds IP version 6 addresses");
        Descriptions.Add("email", "\tFinds embedded email addresses");
        Descriptions.Add("zip", "\tFinds zip codes");
        Descriptions.Add("urlUser", "\tFinds usernames in URLs");
        Descriptions.Add("url3986", "\tFinds URLs according to RFC 3986");
        Descriptions.Add("xml", "\tFinds XML/HTML tags");
        Descriptions.Add("sid", "\tFinds Microsoft Security Identifiers (SID)");
        Descriptions.Add("win_path", @"Finds Windows style paths (C:\folder1\folder2\file.txt)");
        Descriptions.Add("var_set", "\tFinds environment variables being set (OS=Windows_NT)");
        Descriptions.Add("reg_path", "Finds paths related to Registry hives");
        Descriptions.Add("b64", "\tFinds valid formatted base 64 strings");
        Descriptions.Add("bitlocker", "Finds Bitlocker recovery keys");
        Descriptions.Add("bitcoin", "\tFinds BitCoin wallet addresses");
        Descriptions.Add("aeon", "\tFinds Aeon wallet addresses");
        Descriptions.Add("bytecoin", "Finds ByteCoin wallet addresses");
        Descriptions.Add("dashcoin", "Finds DashCoin wallet addresses (D*)");
        Descriptions.Add("dashcoin2", "Finds DashCoin wallet addresses (7|X)*");
        Descriptions.Add("fantomcoin", "Finds Fantomcoin wallet addresses");
        Descriptions.Add("monero", "\tFinds Monero wallet addresses");
        Descriptions.Add("sumokoin", "Finds SumoKoin wallet addresses");

        Patterns.Add("bitcoin", @"\b[13][a-km-zA-HJ-NP-Z1-9]{25,34}\b");
        Patterns.Add("aeon", @"Wm[st]{1}[0-9a-zA-Z]{94}");
        Patterns.Add("bytecoin", @"2[0-9AB][0-9a-zA-Z]{93}");

        Patterns.Add("dashcoin", "D[0-9a-zA-Z]{94}");
        Patterns.Add("dashcoin2", "(7|X)[a-zA-Z0-9]{33}");
        Patterns.Add("fantomcoin", "6[0-9a-zA-Z]{94}");
        Patterns.Add("monero", "4[0-9AB][0-9a-zA-Z]{93}|4[0-9AB][0-9a-zA-Z]{104}");
        Patterns.Add("sumokoin", "Sumoo[0-9a-zA-Z]{94}");


        Patterns.Add("b64",
            @"^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=|[A-Za-z0-9+/]{4})$");

        Patterns.Add("bitlocker", @"[0-9]{6}?-[0-9]{6}-[0-9]{6}-[0-9]{6}-[0-9]{6}-[0-9]{6}-[0-9]{6}-[0-9]{6}");

        Patterns.Add("reg_path", @"([a-z0-9]\\)*(software|sam|system|security)\\[a-z0-9\\]+");
        Patterns.Add("var_set", @"^[a-z_0-9]+=[\\/:\*\?<>|;\- _a-z0-9]+");
        Patterns.Add("win_path",
            @"(?:""?[a-zA-Z]\:|\\\\[^\\\/\:\*\?\<\>\|]+\\[^\\\/\:\*\?\<\>\|]*)\\(?:[^\\\/\:\*\?\<\>\|]+\\)*\w([^\\\/\:\*\?\<\>\|])*");
        Patterns.Add("sid", @"^S-\d-\d+-(\d+-){1,14}\d+$");
        Patterns.Add("xml", @"\A<([A-Z][A-Z0-9]*)\b[^>]*>(.*?)</\1>\z");
        Patterns.Add("guid", @"\b[A-F0-9]{8}(?:-[A-F0-9]{4}){3}-[A-F0-9]{12}\b");
        Patterns.Add("usPhone", @"\(?\b[2-9][0-9]{2}\)?[-. ]?[2-9][0-9]{2}[-. ]?[0-9]{4}\b");
        Patterns.Add("unc", @"^\\\\(?<server>[a-z0-9 %._-]+)\\(?<share>[a-z0-9 $%._-]+)");
        Patterns.Add("mac", "\\b[0-9A-F]{2}([-:]?)(?:[0-9A-F]{2}\\1){4}[0-9A-F]{2}\\b");
        Patterns.Add("ssn", "\\b(?!000)(?!666)[0-8][0-9]{2}[- ](?!00)[0-9]{2}[- ](?!0000)[0-9]{4}\\b");
        // Patterns.Add("cc","^(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|6(?:011|5[0-9][0-9])[0-9]{12}|3[47][0-9]{13}|3(?:0[0-5]|[68][0-9])[0-9]{11}|(?:2131|1800|35\\d{3})\\d{11})$");
        Patterns.Add("cc",
            @"^[ -]*(?:4[ -]*(?:\d[ -]*){11}(?:(?:\d[ -]*){3})?\d|5[ -]*[1-5](?:[ -]*[0-9]){14}|6[ -]*(?:0[ -]*1[ -]*1|5[ -]*\d[ -]*\d)(?:[ -]*[0-9]){12}|3[ -]*[47](?:[ -]*[0-9]){13}|3[ -]*(?:0[ -]*[0-5]|[68][ -]*[0-9])(?:[ -]*[0-9]){11}|(?:2[ -]*1[ -]*3[ -]*1|1[ -]*8[ -]*0[ -]*0|3[ -]*5(?:[ -]*[0-9]){3})(?:[ -]*[0-9]){11})[ -]*$");
        Patterns.Add("ipv4",
            @"\b(?:(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])\b");
        Patterns.Add("ipv6", @"(?<![:.\w])(?:[A-F0-9]{1,4}:){7}[A-F0-9]{1,4}(?![:.\w])");
        //         Patterns.Add("email",@"[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?");
        Patterns.Add("email", @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,6}\b");
        Patterns.Add("zip", @"\A\b[0-9]{5}(?:-[0-9]{4})?\b\z");
        Patterns.Add("urlUser", @"^[a-z0-9+\-.]+://(?<user>[a-z0-9\-._~%!$&'()*+,;=]+)@");
        Patterns.Add("url3986", @"(?x)^
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
}
