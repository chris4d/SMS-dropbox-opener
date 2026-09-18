// Throwaway tool: computes the Chrome extension ID from a PEM RSA private key.
// Chrome ID = 16 bytes of SHA256(SPKI DER of public key), each of the 32 hex
// digits mapped to a..p.
using System;
using System.IO;
using System.Security.Cryptography;

class DerId
{
    static int Main(string[] args)
    {
        byte[] pkcs1 = LoadPkcs1(args[0]);

        int pos = HeaderLen(pkcs1, 0);
        if (ReadInt(pkcs1, ref pos).Length != 1) throw new InvalidOperationException("not v0");
        byte[] n = ReadInt(pkcs1, ref pos);
        byte[] x = ReadInt(pkcs1, ref pos);

        byte[] rsaPub = Seq(Cat(Int(n), Int(x)));
        byte[] bits = BitString(rsaPub);
        byte[] alg = new byte[] { 0x30, 0x0d, 0x06, 0x09, 0x2a, 0x86,
                                  0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x01, 0x05, 0x00 };
        byte[] spki = Seq(Cat(alg, bits));

        string hex;
        using (SHA256 h = SHA256.Create())
        {
            var sb = new System.Text.StringBuilder();
            foreach (byte by in h.ComputeHash(spki)) sb.Append(by.ToString("x2"));
            hex = sb.ToString();
        }
        const string ap = "abcdefghijklmnop";
        var id = new char[32];
        for (int i = 0; i < 32; i++) id[i] = ap[Convert.ToInt32(hex[i].ToString(), 16)];
        Console.WriteLine(new string(id));
        if (args.Length > 1 && args[1] == "spki")
        {
            // Also print manifest-style "key" = base64 SPKI DER of the public key
            byte[] spki2 = spki;
            Console.WriteLine(Convert.ToBase64String(spki2));
        }
        return 0;
    }

    // BEGIN PRIVATE KEY (PKCS#8) or BEGIN RSA PRIVATE KEY (PKCS#1) -> PKCS#1 DER
    static byte[] LoadPkcs1(string path)
    {
        string pem = File.ReadAllText(path);
        int b = pem.IndexOf("-----BEGIN", StringComparison.Ordinal);
        int lineEnd = pem.IndexOf('\n', b);
        int e = pem.IndexOf("-----END", StringComparison.Ordinal);
        byte[] der = Convert.FromBase64String(
            pem.Substring(lineEnd, e - lineEnd).Replace("\r", "").Replace("\n", "").Trim());

        int pos = HeaderLen(der, 0);

        // PKCS#8: body starts with INTEGER 0
        if (der[pos] == 0x02 && der[pos + 1] == 0x01 && der[pos + 2] == 0x00)
        {
            pos += 3;       // version
            Skip(der, ref pos); // SEQ { alg }
            int l2 = SkipLen(der, ref pos); // OCTET STRING { PKCS#1 key }
            byte[] inner = new byte[l2];
            Buffer.BlockCopy(der, pos, inner, 0, l2);
            pos += l2;
            return inner;
        }
        return der;
    }

    static int SkipLen(byte[] der, ref int pos)
    {
        byte tag = der[pos++];
        if (tag != 0x30 && tag != 0x04)
            throw new InvalidOperationException("bad tag 0x" + tag.ToString("x2"));
        int len = ReadLen(der, ref pos);
        return len;
    }

    static int HeaderLen(byte[] der, int start)
    {
        if (der[start] != 0x30 && der[start] != 0x04)
            throw new InvalidOperationException("bad tag 0x" + der[start].ToString("x2"));
        int p = start + 1, first = der[p++];
        if ((first & 0x80) == 0) return p;
        int nb = first & 0x7f, len = 0;
        for (int i = 0; i < nb; i++) len = (len << 8) | der[p++];
        return p; // index of first content byte (length was <=> nb bytes)
    }

    static int Skip(byte[] der, ref int pos)
    {
        int tag = der[pos++];
        if (tag != 0x30 && tag != 0x04)
            throw new InvalidOperationException("bad tag 0x" + tag.ToString("x2"));
        int len = ReadLen(der, ref pos);
        pos += len;
        return len;
    }

    static byte[] ReadInt(byte[] der, ref int pos)
    {
        if (der[pos++] != 0x02) throw new InvalidOperationException("expect int");
        int len = ReadLen(der, ref pos);
        byte[] r = new byte[len];
        Buffer.BlockCopy(der, pos, r, 0, len);
        pos += len;
        return r;
    }
    static int ReadLen(byte[] der, ref int pos)
    {
        int first = der[pos++];
        if ((first & 0x80) == 0) return first;
        int nb = first & 0x7f, len = 0;
        for (int i = 0; i < nb; i++) len = (len << 8) | der[pos++];
        return len;
    }
    static byte[] Seq(byte[] body) { return Cat(new byte[] { 0x30 }, Len(body.Length), body); }
    static byte[] BitString(byte[] body) { return Cat(new byte[] { 0x03 }, Len(body.Length + 1), new byte[] { 0 }, body); }
    static byte[] Int(byte[] v)
    {
        int s = 0;
        while (s < v.Length - 1 && v[s] == 0) s++;
        int body = v.Length - s;
        bool pad = (v[s] & 0x80) != 0;
        // DER INTEGER: length is the CONTENT length (value bytes incl. sign pad),
        // always multi-byte encoded via Len() - a 256-byte modulus needs 82 01 01.
        byte[] head = Cat(new byte[] { 0x02 }, Len(body + (pad ? 1 : 0)));
        if (pad) head = Cat(head, new byte[] { 0 });
        byte[] content = new byte[body];
        Buffer.BlockCopy(v, s, content, 0, body);
        return Cat(head, content);
    }
    static byte[] Len(int len)
    {
        if (len < 0x80) return new byte[] { (byte)len };
        if (len < 0x100) return new byte[] { 0x81, (byte)len };
        return new byte[] { 0x82, (byte)(len >> 8), (byte)(len & 0xff) };
    }
    static byte[] Cat(params byte[][] parts)
    {
        int total = 0; foreach (byte[] p in parts) total += p.Length;
        byte[] r = new byte[total]; int o = 0;
        foreach (byte[] p in parts) { Buffer.BlockCopy(p, 0, r, o, p.Length); o += p.Length; }
        return r;
    }
}
