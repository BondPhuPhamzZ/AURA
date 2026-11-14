using AURA.Models;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class EvidenceRegionMatcherTests
{
    // Word boxes laid out like Tesseract's output for the synthetic R3-01 VAT invoice (1154x1536):
    // labels and amounts sit in separate columns and Vietnamese marks are partly misread.
    private static OcrPage R3Page(string totalText = "241.920", string subtotalText = "224.000") => new(1154, 1536,
        new List<OcrWord>
        {
            W("HÓA", 320, 112, 400, 156, 1), W("ĐƠN", 410, 112, 500, 156, 1), W("GIÁ", 510, 112, 580, 156, 1),
            W("CONG", 132, 172, 230, 210, 2), W("TY", 240, 172, 280, 210, 2), W("TNHH", 290, 172, 380, 210, 2),
            W("VĂN", 390, 172, 450, 210, 2), W("PHÒNG", 460, 172, 560, 210, 2), W("NAM", 570, 172, 610, 210, 2),
            W("VIỆT", 615, 172, 653, 210, 2),
            W("Số", 129, 333, 160, 356, 3), W("hóa", 165, 333, 210, 356, 3), W("đơn:", 215, 333, 260, 356, 3),
            W("000142", 265, 333, 324, 356, 3),
            W("Ngày", 129, 374, 180, 395, 4), W("hóa", 185, 374, 225, 395, 4), W("đơn:", 230, 374, 280, 395, 4),
            W("05/10/2026", 285, 374, 384, 395, 4),
            W("2", 651, 582, 660, 602, 5), W("89.000", 700, 582, 780, 602, 5), W("đ", 785, 582, 795, 602, 5),
            W("178.000", 880, 582, 960, 602, 5), W("đ", 965, 582, 980, 602, 5),
            W("TẠM", 116, 1134, 160, 1156, 6), W("TÍNH", 165, 1134, 216, 1156, 6),
            W(subtotalText, 876, 1149, 955, 1167, 7), W("đ", 960, 1149, 971, 1167, 7),
            W("THUÉ", 115, 1173, 170, 1194, 8), W("GTGT", 175, 1173, 241, 1194, 8),
            W("17.920", 889, 1189, 955, 1206, 9), W("đ", 960, 1189, 971, 1206, 9),
            W("TỎNG", 115, 1220, 200, 1252, 10), W("THANH", 205, 1220, 300, 1252, 10),
            W("TOÁN", 305, 1220, 398, 1252, 10),
            W(totalText, 831, 1239, 950, 1263, 11), W("đ", 955, 1239, 969, 1263, 11)
        });

    private static ReceiptExtractionDto R3Facts() => new()
    {
        MerchantName = "CÔNG TY TNHH VĂN PHÒNG NAM VIỆT",
        InvoiceNumber = "000142",
        InvoiceDateEvidence = "Ngày hóa đơn: 05/10/2026",
        TotalAmount = 241920m,
        TotalAmountEvidence = "TỔNG THANH TOÁN 241.920 đ"
    };

    [Fact]
    public void Locates_the_four_reviewer_fields_on_a_clean_synthetic_invoice()
    {
        var (regions, unlocated) = EvidenceRegionMatcher.Match(R3Page(), R3Facts());

        Assert.Empty(unlocated);
        Assert.Equal(new[] { "merchantName", "identifier", "documentDate", "totalAmount" },
            regions.Select(region => region.Field));
        Assert.Contains("000142", Region(regions, "identifier").Text);
        Assert.StartsWith("Số hóa đơn:", Region(regions, "identifier").Text);
        Assert.Contains("05/10/2026", Region(regions, "documentDate").Text);
        foreach (var box in regions.Select(region => region.Box))
            Assert.True(box.X >= 0 && box.Y >= 0 && box.Width > 0 && box.Height > 0 &&
                box.X + box.Width <= 1000 && box.Y + box.Height <= 1000);
    }

    [Fact]
    public void Total_box_covers_the_final_amount_and_its_label_but_never_the_subtotal()
    {
        var (regions, _) = EvidenceRegionMatcher.Match(R3Page(), R3Facts());
        var total = Region(regions, "totalAmount");

        Assert.Contains("241.920", total.Text);
        Assert.Contains("TỎNG", total.Text);
        Assert.DoesNotContain("224.000", total.Text);
        // The subtotal row (y≈1149..1167 px → 748..760) must lie outside the total box.
        Assert.True(total.Box.Y > 1167 * 1000 / 1536);
    }

    [Fact]
    public void Equal_subtotal_and_total_are_resolved_only_by_the_printed_total_label()
    {
        var page = R3Page(totalText: "224.000", subtotalText: "224.000");
        var facts = R3Facts();
        facts.TotalAmount = 224000m;
        facts.TotalAmountEvidence = "TỔNG THANH TOÁN 224.000 đ";

        var (regions, _) = EvidenceRegionMatcher.Match(page, facts);
        Assert.True(Region(regions, "totalAmount").Box.Y > 1167 * 1000 / 1536);

        facts.TotalAmountEvidence = null;
        var (withoutLabel, unlocated) = EvidenceRegionMatcher.Match(page, facts);
        Assert.DoesNotContain(withoutLabel, region => region.Field == "totalAmount");
        Assert.Contains(unlocated, item => item.Field == "totalAmount");
    }

    [Fact]
    public void A_total_that_survives_only_as_a_subtotal_is_not_highlighted()
    {
        // The printed final-total row is unreadable; the same value is visible only beside "TẠM TÍNH".
        var words = R3Page(totalText: "###", subtotalText: "241.920").Words.ToList();

        var (regions, unlocated) = EvidenceRegionMatcher.Match(new OcrPage(1154, 1536, words), R3Facts());

        Assert.DoesNotContain(regions, region => region.Field == "totalAmount");
        Assert.Contains(unlocated, item => item.Field == "totalAmount" && item.Reason.Contains("nhãn tổng"));
    }

    [Fact]
    public void Two_rows_with_the_total_label_and_value_are_ambiguous()
    {
        var words = R3Page().Words.ToList();
        words.Add(W("TỔNG", 115, 1400, 200, 1430, 20));
        words.Add(W("THANH", 205, 1400, 300, 1430, 20));
        words.Add(W("TOÁN", 305, 1400, 398, 1430, 20));
        words.Add(W("241.920", 831, 1405, 950, 1430, 21));

        var (regions, unlocated) = EvidenceRegionMatcher.Match(new OcrPage(1154, 1536, words), R3Facts());

        Assert.DoesNotContain(regions, region => region.Field == "totalAmount");
        Assert.Contains(unlocated, item => item.Field == "totalAmount" && item.Reason.Contains("nhiều chỗ"));
    }

    [Fact]
    public void Ambiguous_missing_or_unusable_values_are_never_drawn()
    {
        var facts = R3Facts();
        facts.MerchantName = "CỬA HÀNG KHÁC HOÀN TOÀN";
        facts.InvoiceNumber = "12";
        facts.InvoiceDateEvidence = null;
        facts.TotalAmount = 241920.5m;

        var (regions, unlocated) = EvidenceRegionMatcher.Match(R3Page(), facts);

        Assert.Empty(regions);
        Assert.Equal(4, unlocated.Count);
        Assert.All(unlocated, item => Assert.False(string.IsNullOrWhiteSpace(item.Reason)));
    }

    [Fact]
    public void A_slightly_tilted_photo_still_pairs_the_total_label_with_its_amount()
    {
        // About 2° of tilt drops the right-hand amount ~30 px below its left-hand label.
        var tilted = R3Page().Words
            .Select(word => word with
            {
                Y1 = word.Y1 + (int)Math.Round(0.035 * word.X1), Y2 = word.Y2 + (int)Math.Round(0.035 * word.X2)
            })
            .ToList();

        var (regions, _) = EvidenceRegionMatcher.Match(new OcrPage(1154, 1600, tilted), R3Facts());

        var total = Region(regions, "totalAmount");
        Assert.Contains("TỎNG", total.Text);
        Assert.Contains("241.920", total.Text);
        Assert.DoesNotContain("224.000", total.Text);
    }

    [Fact]
    public void A_date_split_inside_a_number_by_ocr_is_still_found()
    {
        var words = R3Page().Words.Where(word => word.Text != "05/10/2026").ToList();
        words.Add(W("05/1", 285, 374, 330, 395, 4));
        words.Add(W("0/2026", 334, 374, 384, 395, 4));

        var (regions, _) = EvidenceRegionMatcher.Match(new OcrPage(1154, 1536, words), R3Facts());

        Assert.StartsWith("Ngày hóa đơn:", Region(regions, "documentDate").Text);
    }

    [Fact]
    public void A_value_printed_twice_is_not_guessed()
    {
        var words = R3Page().Words.ToList();
        words.Add(W("000142", 600, 1355, 660, 1370, 12));
        var (regions, unlocated) = EvidenceRegionMatcher.Match(new OcrPage(1154, 1536, words), R3Facts());

        Assert.DoesNotContain(regions, region => region.Field == "identifier");
        Assert.Contains(unlocated, item => item.Field == "identifier");
    }

    [Theory]
    [InlineData("241.920", "241920")]
    [InlineData("241.920 đ", "241920")]
    [InlineData("241,920đ", "241920")]
    [InlineData("1.241.920 VND", "1241920")]
    [InlineData("2 89.000", null)]
    [InlineData("TỔNG 241.920", null)]
    [InlineData("05/10/2026", null)]
    public void Amount_digits_accept_only_a_number_with_an_optional_vnd_unit(string text, string? expected)
    {
        Assert.Equal(expected, EvidenceRegionMatcher.AmountDigits(text));
    }

    [Fact]
    public void Folding_removes_vietnamese_marks_so_misread_diacritics_still_match()
    {
        Assert.Equal("tong thanh toan", EvidenceRegionMatcher.Fold("TỔNG THANH TOÁN:"));
        Assert.Equal("tong thanh toan", EvidenceRegionMatcher.Fold("TỎNG THANH TOÁN"));
        Assert.Equal("cong ty van phong", EvidenceRegionMatcher.Fold("Công ty Văn-Phòng"));
        Assert.Equal("dong", EvidenceRegionMatcher.Fold("Đồng"));
        Assert.Equal(1, EvidenceRegionMatcher.Similarity("abc", "abc"));
        Assert.Equal(0, EvidenceRegionMatcher.Similarity("", "abc"));
    }

    private static EvidenceRegion Region(IEnumerable<EvidenceRegion> regions, string field) =>
        Assert.Single(regions, region => region.Field == field);

    private static OcrWord W(string text, int x1, int y1, int x2, int y2, int line) =>
        new(text, x1, y1, x2, y2, line);
}
