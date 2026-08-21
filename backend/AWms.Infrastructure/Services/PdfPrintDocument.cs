using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using QRCoder;

namespace AWms.Infrastructure.Services;

internal static class PdfPrintDocument
{
    private static readonly Encoding Ascii = Encoding.ASCII;

    public static async Task WriteAsync(
        string path,
        IReadOnlyList<(string Content, string ReadableText)> items,
        CancellationToken ct)
    {
        var objects = BuildObjects(items);
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);

        await stream.WriteAsync(Ascii.GetBytes("%PDF-1.7\n%\u00e2\u00e3\u00cf\u00d3\n"), ct);
        var offsets = new long[objects.Count + 1];
        for (var i = 0; i < objects.Count; i++)
        {
            offsets[i + 1] = stream.Position;
            await stream.WriteAsync(Ascii.GetBytes($"{i + 1} 0 obj\n"), ct);
            await stream.WriteAsync(objects[i], ct);
            await stream.WriteAsync(Ascii.GetBytes("\nendobj\n"), ct);
        }

        var xrefOffset = stream.Position;
        await stream.WriteAsync(Ascii.GetBytes($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"), ct);
        for (var i = 1; i <= objects.Count; i++)
            await stream.WriteAsync(Ascii.GetBytes($"{offsets[i]:D10} 00000 n \n"), ct);
        await stream.WriteAsync(
            Ascii.GetBytes($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n"),
            ct);
        await stream.FlushAsync(ct);
        stream.Flush(flushToDisk: true);
    }

    private static List<byte[]> BuildObjects(IReadOnlyList<(string Content, string ReadableText)> items)
    {
        var objects = new List<byte[]>();
        var pageObjectNumbers = Enumerable.Range(0, items.Count).Select(i => 5 + i * 3).ToList();
        objects.Add(Ascii.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"));
        objects.Add(Ascii.GetBytes($"<< /Type /Pages /Count {items.Count} /Kids [{string.Join(' ', pageObjectNumbers.Select(x => $"{x} 0 R"))}] >>"));
        objects.Add(Ascii.GetBytes("<< /Type /Font /Subtype /Type0 /BaseFont /STSong-Light /Encoding /UniGB-UCS2-H /DescendantFonts [4 0 R] >>"));
        objects.Add(Ascii.GetBytes("<< /Type /Font /Subtype /CIDFontType0 /BaseFont /STSong-Light /CIDSystemInfo << /Registry (Adobe) /Ordering (GB1) /Supplement 5 >> >>"));

        for (var i = 0; i < items.Count; i++)
        {
            var pageNo = 5 + i * 3;
            var imageNo = pageNo + 1;
            var contentNo = pageNo + 2;
            objects.Add(Ascii.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> /XObject << /QR {imageNo} 0 R >> >> /Contents {contentNo} 0 R >>"));

            var qr = CreateQrImage(items[i].Content);
            objects.Add(StreamObject(
                $"/Type /XObject /Subtype /Image /Width {qr.Size} /Height {qr.Size} /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode",
                qr.Data));

            var pageCommands = BuildPageCommands(items[i].ReadableText, items[i].Content);
            objects.Add(StreamObject(string.Empty, Ascii.GetBytes(pageCommands)));
        }

        return objects;
    }

    private static string BuildPageCommands(string readableText, string content)
    {
        var builder = new StringBuilder("q 210 0 0 210 40 590 cm /QR Do Q\n");
        var lines = WrapLines(readableText, 42).Concat(WrapLines(content, 56)).Take(24).ToList();
        var y = 555;
        foreach (var line in lines)
        {
            builder.Append("BT /F1 10 Tf 40 ")
                .Append(y.ToString(CultureInfo.InvariantCulture))
                .Append(" Td <")
                .Append(Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(line)))
                .Append("> Tj ET\n");
            y -= 15;
        }
        return builder.ToString();
    }

    private static IEnumerable<string> WrapLines(string value, int maxTextElements)
    {
        foreach (var rawLine in value.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            if (rawLine.Length == 0)
            {
                yield return string.Empty;
                continue;
            }

            var elements = StringInfo.ParseCombiningCharacters(rawLine);
            for (var i = 0; i < elements.Length; i += maxTextElements)
            {
                var start = elements[i];
                var endIndex = Math.Min(i + maxTextElements, elements.Length);
                var end = endIndex == elements.Length ? rawLine.Length : elements[endIndex];
                yield return rawLine[start..end];
            }
        }
    }

    private static (int Size, byte[] Data) CreateQrImage(string content)
    {
        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M, forceUtf8: true);
        var matrix = qrData.ModuleMatrix;
        var size = matrix.Count;
        var pixels = new byte[size * size];
        for (var y = 0; y < size; y++)
        {
            BitArray row = matrix[y];
            for (var x = 0; x < size; x++)
                pixels[y * size + x] = row[x] ? (byte)0 : (byte)255;
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(pixels);
        return (size, compressed.ToArray());
    }

    private static byte[] StreamObject(string dictionaryEntries, byte[] data)
    {
        using var stream = new MemoryStream();
        var dictionary = string.IsNullOrWhiteSpace(dictionaryEntries)
            ? $"<< /Length {data.Length} >>\nstream\n"
            : $"<< {dictionaryEntries} /Length {data.Length} >>\nstream\n";
        stream.Write(Ascii.GetBytes(dictionary));
        stream.Write(data);
        stream.Write(Ascii.GetBytes("\nendstream"));
        return stream.ToArray();
    }
}
