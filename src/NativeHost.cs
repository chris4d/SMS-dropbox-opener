// Native Messaging host for the SMS DropboxOpener browser extension.
// Reads length-prefixed JSON on stdin ({ "path": "Business/Projects/X" }),
// invokes DropboxOpener.exe with a constructed opendbx:// URL, and replies.
// Run by both Chrome and Edge via %LOCALAPPDATA% registry registration.

using System;
using System.Diagnostics;
using System.IO;

class NativeHost
{
    static int Main(string[] args)
    {
        try
        {
            // Native messaging frames: 32-bit little-endian length, then UTF-8 JSON.
            byte[] lenBytes = new byte[4];
            if (ReadFull(Console.OpenStandardInput(), lenBytes, 4) < 4)
                return 0;

            uint len = BitConverter.ToUInt32(lenBytes, 0);
            if (len == 0 || len > 64 * 1024) { Emit(false, "unusable request length"); return 0; }

            byte[] payload = new byte[len];
            if (ReadFull(Console.OpenStandardInput(), payload, (int)len) < (int)len)
            { Emit(false, "short read"); return 0; }

            string json = System.Text.Encoding.UTF8.GetString(payload);
            string path = ExtractPath(json);
            if (path == null) { Emit(false, "no path in request"); return 0; }

            string exe = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "DropboxOpener.exe");
            if (!File.Exists(exe))
            {
                // Fall back to the registered install location
                exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SMS-DropboxOpener", "DropboxOpener.exe");
            }
            if (!File.Exists(exe)) { Emit(false, "DropboxOpener.exe not found"); return 0; }

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "\"opendbx://" + path + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                CreateNoWindow = true
            };
            using (Process p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd(); // winexe prints nothing, but keep for exit-code wait
                p.WaitForExit();
                Emit(p.ExitCode == 0, "helper exit " + p.ExitCode);
            }
            return 0;
        }
        catch (Exception ex)
        {
            try { Emit(false, "exception " + ex.Message); } catch { }
            return 1;
        }
    }

    static int ReadFull(Stream s, byte[] buf, int count)
    {
        int read = 0;
        while (read < count)
        {
            int n = s.Read(buf, read, count - read);
            if (n <= 0) break;
            read += n;
        }
        return read;
    }

    // Extremely minimal JSON path extraction: {"path":"..."} style, ordered keys irrelevant.
    static string ExtractPath(string json)
    {
        int idx = json.IndexOf("\"path\"", StringComparison.Ordinal);
        if (idx < 0) return null;
        int colon = json.IndexOf(':', idx + 6);
        if (colon < 0) return null;
        int q1 = json.IndexOf('"', colon + 1);
        if (q1 < 0) return null;
        var sb = new System.Text.StringBuilder();
        for (int i = q1 + 1; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '\\')
            {
                char n = json[++i];
                if (n == 'u')
                {
                    string hex = json.Substring(i + 1, 4);
                    sb.Append((char)Convert.ToInt32(hex, 16));
                    i += 4;
                }
                else if (n == 'n') sb.Append('\n');
                else if (n == 't') sb.Append('\t');
                else sb.Append(n);
                continue;
            }
            if (c == '"') break;
            sb.Append(c);
        }
        return sb.ToString();
    }

    static void Emit(bool ok, string note)
    {
        string body = "{\"ok\":" + (ok ? "true" : "false") + ",\"note\":\"" + note.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
        byte[] data = System.Text.Encoding.UTF8.GetBytes(body);
        byte[] header = BitConverter.GetBytes((UInt32)data.Length);
        Stream stdout = Console.OpenStandardOutput();
        stdout.Write(header, 0, 4);
        stdout.Write(data, 0, data.Length);
        stdout.Flush();
    }
}
