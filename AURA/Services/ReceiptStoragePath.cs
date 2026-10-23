namespace AURA.Services;

/// <summary>Keep receipt bytes outside the ASP.NET static-file root.</summary>
public static class ReceiptStoragePath
{
    public static string ResolveRoot(IWebHostEnvironment environment, string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("ReceiptStorage:Directory không được để trống.");

        var root = Path.GetFullPath(Path.IsPathRooted(directory)
            ? directory : Path.Combine(environment.ContentRootPath, directory));
        var webRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath);
        var relative = Path.GetRelativePath(webRoot, root);
        if (relative == "." || (!Path.IsPathRooted(relative) && relative != ".." &&
            !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)))
            throw new InvalidOperationException("ReceiptStorage:Directory phải nằm ngoài wwwroot.");
        return root;
    }

    public static string ResolveFile(string root, string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName) ||
            storedFileName.IndexOfAny(['/', '\\', ':']) >= 0 ||
            !string.Equals(Path.GetFileName(storedFileName), storedFileName, StringComparison.Ordinal) ||
            storedFileName is "." or "..")
            throw new InvalidOperationException("Tên file hóa đơn đã lưu không hợp lệ.");

        return Path.Combine(root, storedFileName);
    }
}
