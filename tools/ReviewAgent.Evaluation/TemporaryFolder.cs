namespace ReviewAgent.Evaluation;

/// <summary>Deletes a temporary folder with everything in it, so nothing is left behind on the machine.</summary>
internal static class TemporaryFolder
{
    public static void Delete(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        // Git makes its object files read-only, which would stop the deletion on Windows.
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}
