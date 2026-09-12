using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;

namespace DropboxOpener
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                if (args.Length < 1 || !args[0].StartsWith("opendbx:", StringComparison.OrdinalIgnoreCase))
                    return 2;

                // Parse opendbx://<account>/<relative/path>
                // account: "business" | "personal" (optional, defaults to business if available)
                // Inno's registry command quotes the URL, but browsers/other invokers may not —
                // join args defensively so a single URL arg is always what we parse.
                string url = string.Join(" ", args);
                if (url.StartsWith("\"") && url.EndsWith("\"") && url.Length > 1) url = url.Substring(1, url.Length - 2);
                url = url.TrimEnd('/');

                string rest = url.Substring("opendbx://".Length);
                if (rest.StartsWith("opendbx:", StringComparison.OrdinalIgnoreCase)) // malformed edge
                    rest = url.Substring("opendbx:".Length);

                string relative;
                string account = "business";

                int firstSlash = rest.IndexOf('/');
                string firstSegment = HttpUtility.UrlDecode(firstSlash < 0 ? rest : rest.Substring(0, firstSlash));

                if (firstSegment.Equals("business", StringComparison.OrdinalIgnoreCase) ||
                    firstSegment.Equals("personal", StringComparison.OrdinalIgnoreCase))
                {
                    account = firstSegment.ToLowerInvariant();
                    relative = firstSlash < 0 ? "" : rest.Substring(firstSlash + 1);
                }
                else
                {
                    relative = rest;
                }

                relative = HttpUtility.UrlDecode(relative).Replace('/', Path.DirectorySeparatorChar);
                relative = relative.TrimStart(Path.DirectorySeparatorChar);

                // Security: the protocol can be invoked by any process/web page, and the
                // URL only carries a location. Two hardening rules:
                //   1. Never execute anything — we only ever ask explorer.exe to navigate.
                //      (Nothing here passes user input to a shell.)
                //   2. The resolved target must stay inside the Dropbox roots. Reject
                //      traversal (".."), drive/UNC-absolute paths, and empty segments so a
                //      crafted link can never reach folders outside Dropbox.
                string[] segments = relative.Split(new[] { Path.DirectorySeparatorChar },
                    StringSplitOptions.RemoveEmptyEntries);
                foreach (string s in segments)
                {
                    if (s == ".." || s == "." || s.IndexOf(':') >= 0 ||
                        s.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                        return 3;
                }
                relative = string.Join(Path.DirectorySeparatorChar.ToString(), segments);

                string[] rootCandidates = ResolveRoot(account);

                string target = relative.Length == 0
                    ? rootCandidates.FirstOrDefault()
                    : rootCandidates
                        .Select(candidate => Path.Combine(candidate, relative))
                        .FirstOrDefault(t => Directory.Exists(t) || File.Exists(t));

                if (target == null || (!Directory.Exists(target) && !File.Exists(target)))
                    return 3;

                // Open the containing folder when a file path is given
                string explorerArg = Directory.Exists(target)
                    ? "\"" + target + "\""
                    : "/select,\"" + target + "\"";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = explorerArg,
                    UseShellExecute = true
                });
                return 0;
            }
            catch
            {
                return 1;
            }
        }

        // Reads the Dropbox folder paths for the given account from info.json.
        // Newer Dropbox builds use %LOCALAPPDATA%\Dropbox\info.json; older use %APPDATA%\Dropbox\info.json.
        // Returns candidates in priority order: team root, then user's own folder.
        static string[] ResolveRoot(string account)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            string json = null;
            foreach (string candidate in new[]
            {
                Path.Combine(localAppData, "Dropbox", "info.json"),
                Path.Combine(appData, "Dropbox", "info.json")
            })
            {
                if (File.Exists(candidate)) { json = File.ReadAllText(candidate); break; }
            }

            string teamRoot = null, userFolder = null;

            if (json != null)
            {
                ReadPathsFromJson(json, account, out teamRoot, out userFolder);
            }

            if (teamRoot == null && userFolder == null)
            {
                // Fallback instances (multi-account installs keep info.json per instance)
                string instances = Path.Combine(localAppData, "Dropbox");
                if (Directory.Exists(instances))
                {
                    foreach (string dir in Directory.GetDirectories(instances, "instance*"))
                    {
                        string candidate = Path.Combine(dir, "info.json");
                        if (File.Exists(candidate))
                        {
                            ReadPathsFromJson(File.ReadAllText(candidate), account, out teamRoot, out userFolder);
                            if (teamRoot != null || userFolder != null) break;
                        }
                    }
                }
            }

            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (account == "personal" && userFolder == null)
                userFolder = Path.Combine(user, "Dropbox (Personal)");
            else if (userFolder == null)
                userFolder = Path.Combine(user, "Dropbox");

            return new[] { teamRoot, userFolder }
                .Where(p => p != null && Directory.Exists(p))
                .Concat(new[] { userFolder })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        // Minimal JSON read without external dependencies: finds
        // "<account>": { ... "path": "..." , "root_path": "..." }
        static void ReadPathsFromJson(string json, string account, out string rootPath, out string path)
        {
            rootPath = null; path = null;

            string sectionStart = "\"" + account + "\":";
            int idx = json.IndexOf(sectionStart, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                int braceStart = json.IndexOf('{', idx);
                if (braceStart >= 0)
                {
                    int depth = 0, i = braceStart;
                    for (; i < json.Length; i++)
                    {
                        if (json[i] == '{') depth++;
                        else if (json[i] == '}') { depth--; if (depth == 0) break; }
                    }
                    string section = json.Substring(braceStart, i - braceStart + 1);
                    rootPath = ReadStringMember(section, "root_path");
                    path = ReadStringMember(section, "path");
                }
            }
        }

        static string ReadStringMember(string json, string member)
        {
            int idx = json.IndexOf("\"" + member + "\":", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int firstQuote = json.IndexOf('"', idx + member.Length + 2);
            int lastQuote = firstQuote >= 0 ? json.IndexOf('"', firstQuote + 1) : -1;
            if (lastQuote < 0) return null;
            return json.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
        }
    }
}
