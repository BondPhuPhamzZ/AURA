using System.IO.Compression;
using System.Xml;

namespace AURA.Services;

/// <summary>Writes the existing operations report as a small, text-only XLSX workbook.</summary>
internal static class ManagerOperationsWorkbookWriter
{
    private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static byte[] Write(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddXml(archive, "[Content_Types].xml", writer =>
            {
                writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
                WriteContentType(writer, "rels", "application/vnd.openxmlformats-package.relationships+xml");
                WriteContentType(writer, "xml", "application/xml");
                WriteOverride(writer, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
                WriteOverride(writer, "/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                WriteOverride(writer, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
                writer.WriteEndElement();
            });
            AddXml(archive, "_rels/.rels", writer =>
            {
                writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                WriteRelationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml");
                writer.WriteEndElement();
            });
            AddXml(archive, "xl/workbook.xml", writer =>
            {
                writer.WriteStartElement("workbook", MainNs);
                writer.WriteAttributeString("xmlns", "r", null, RelNs);
                writer.WriteStartElement("sheets", MainNs);
                writer.WriteStartElement("sheet", MainNs);
                writer.WriteAttributeString("name", "Bao cao van hanh");
                writer.WriteAttributeString("sheetId", "1");
                writer.WriteAttributeString("r", "id", RelNs, "rId1");
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
            });
            AddXml(archive, "xl/_rels/workbook.xml.rels", writer =>
            {
                writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                WriteRelationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet1.xml");
                WriteRelationship(writer, "rId2", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml");
                writer.WriteEndElement();
            });
            AddXml(archive, "xl/styles.xml", WriteStyles);
            AddXml(archive, "xl/worksheets/sheet1.xml", writer => WriteSheet(writer, headers, rows));
        }
        return output.ToArray();
    }

    private static void WriteSheet(XmlWriter writer, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        writer.WriteStartElement("worksheet", MainNs);
        writer.WriteStartElement("dimension", MainNs);
        writer.WriteAttributeString("ref", $"A1:{ColumnName(headers.Count)}{rows.Count + 1}");
        writer.WriteEndElement();
        writer.WriteStartElement("sheetViews", MainNs);
        writer.WriteStartElement("sheetView", MainNs);
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane", MainNs);
        writer.WriteAttributeString("ySplit", "1");
        writer.WriteAttributeString("topLeftCell", "A2");
        writer.WriteAttributeString("activePane", "bottomLeft");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("sheetFormatPr", MainNs);
        writer.WriteAttributeString("defaultRowHeight", "18");
        writer.WriteEndElement();
        writer.WriteStartElement("cols", MainNs);
        for (var column = 1; column <= headers.Count; column++)
        {
            writer.WriteStartElement("col", MainNs);
            writer.WriteAttributeString("min", column.ToString());
            writer.WriteAttributeString("max", column.ToString());
            writer.WriteAttributeString("width", column is 2 or 3 ? "25" : "21");
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteStartElement("sheetData", MainNs);
        WriteRow(writer, headers, 1, header: true);
        for (var index = 0; index < rows.Count; index++)
            WriteRow(writer, rows[index], index + 2, header: false);
        writer.WriteEndElement();
        writer.WriteStartElement("autoFilter", MainNs);
        writer.WriteAttributeString("ref", $"A1:{ColumnName(headers.Count)}{rows.Count + 1}");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteRow(XmlWriter writer, IReadOnlyList<string> cells, int rowNumber, bool header)
    {
        writer.WriteStartElement("row", MainNs);
        writer.WriteAttributeString("r", rowNumber.ToString());
        if (header) writer.WriteAttributeString("ht", "27");
        if (header) writer.WriteAttributeString("customHeight", "1");
        for (var index = 0; index < cells.Count; index++)
        {
            writer.WriteStartElement("c", MainNs);
            writer.WriteAttributeString("r", ColumnName(index + 1) + rowNumber);
            writer.WriteAttributeString("s", header ? "1" : "0");
            writer.WriteAttributeString("t", "inlineStr");
            writer.WriteStartElement("is", MainNs);
            writer.WriteStartElement("t", MainNs);
            writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
            writer.WriteString(new string((cells[index] ?? string.Empty)
                .Where(XmlConvert.IsXmlChar).Take(32767).ToArray()));
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void WriteStyles(XmlWriter writer)
    {
        writer.WriteStartElement("styleSheet", MainNs);
        writer.WriteStartElement("fonts", MainNs);
        writer.WriteAttributeString("count", "2");
        writer.WriteStartElement("font", MainNs);
        writer.WriteStartElement("sz", MainNs); writer.WriteAttributeString("val", "11"); writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("font", MainNs);
        writer.WriteStartElement("b", MainNs); writer.WriteEndElement();
        writer.WriteStartElement("color", MainNs); writer.WriteAttributeString("rgb", "FF14532D"); writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("fills", MainNs);
        writer.WriteAttributeString("count", "3");
        WriteFill(writer, "none", null);
        WriteFill(writer, "gray125", null);
        WriteFill(writer, "solid", "FFD9F2E3");
        writer.WriteEndElement();
        writer.WriteStartElement("borders", MainNs);
        writer.WriteAttributeString("count", "1");
        writer.WriteStartElement("border", MainNs); writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("cellStyleXfs", MainNs);
        writer.WriteAttributeString("count", "1");
        WriteXf(writer, "0", "0", false);
        writer.WriteEndElement();
        writer.WriteStartElement("cellXfs", MainNs);
        writer.WriteAttributeString("count", "2");
        WriteXf(writer, "0", "0", false);
        WriteXf(writer, "1", "2", true);
        writer.WriteEndElement();
        writer.WriteStartElement("cellStyles", MainNs);
        writer.WriteAttributeString("count", "1");
        writer.WriteStartElement("cellStyle", MainNs);
        writer.WriteAttributeString("name", "Normal");
        writer.WriteAttributeString("xfId", "0");
        writer.WriteAttributeString("builtinId", "0");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteFill(XmlWriter writer, string pattern, string? rgb)
    {
        writer.WriteStartElement("fill", MainNs);
        writer.WriteStartElement("patternFill", MainNs);
        writer.WriteAttributeString("patternType", pattern);
        if (rgb is not null)
        {
            writer.WriteStartElement("fgColor", MainNs);
            writer.WriteAttributeString("rgb", rgb);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteXf(XmlWriter writer, string fontId, string fillId, bool header)
    {
        writer.WriteStartElement("xf", MainNs);
        writer.WriteAttributeString("numFmtId", "0");
        writer.WriteAttributeString("fontId", fontId);
        writer.WriteAttributeString("fillId", fillId);
        writer.WriteAttributeString("borderId", "0");
        writer.WriteAttributeString("xfId", "0");
        if (header)
        {
            writer.WriteAttributeString("applyFont", "1");
            writer.WriteAttributeString("applyFill", "1");
        }
        writer.WriteEndElement();
    }

    private static void AddXml(ZipArchive archive, string path, Action<XmlWriter> write)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = System.Text.Encoding.UTF8 });
        write(writer);
    }

    private static void WriteContentType(XmlWriter writer, string extension, string contentType)
    {
        writer.WriteStartElement("Default");
        writer.WriteAttributeString("Extension", extension);
        writer.WriteAttributeString("ContentType", contentType);
        writer.WriteEndElement();
    }

    private static void WriteOverride(XmlWriter writer, string partName, string contentType)
    {
        writer.WriteStartElement("Override");
        writer.WriteAttributeString("PartName", partName);
        writer.WriteAttributeString("ContentType", contentType);
        writer.WriteEndElement();
    }

    private static void WriteRelationship(XmlWriter writer, string id, string type, string target)
    {
        writer.WriteStartElement("Relationship");
        writer.WriteAttributeString("Id", id);
        writer.WriteAttributeString("Type", type);
        writer.WriteAttributeString("Target", target);
        writer.WriteEndElement();
    }

    private static string ColumnName(int number)
    {
        var name = string.Empty;
        while (number > 0)
        {
            number--;
            name = (char)('A' + number % 26) + name;
            number /= 26;
        }
        return name;
    }
}
