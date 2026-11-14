using System.Globalization;

namespace AURA.Services;

/// <summary>
/// Vietnamese display formats that do not depend on the host's current culture, so
/// server-rendered amounts match the browser's <c>Intl.NumberFormat('vi-VN')</c> output.
/// </summary>
public static class DisplayFormat
{
    private static readonly NumberFormatInfo VietnameseNumbers = NumberFormatInfo.ReadOnly(new NumberFormatInfo
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
        NumberGroupSizes = new[] { 3 }
    });

    public static string Number(decimal value) => value.ToString("N0", VietnameseNumbers);

    public static string Number(long value) => value.ToString("N0", VietnameseNumbers);

    public static string Vnd(decimal amount) => $"{Number(amount)} VND";

    public static string Date(DateTime value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Date(DateOnly value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Time(DateTime value) => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public static string DateTime(DateTime value) =>
        value.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
}
