// Removes files that used to ship with this app but no longer do, so
// upgrading over an existing install doesn't leave dead weight behind.
//
// This is a supplement to, not a replacement for, the installer's own
// major-upgrade cleanup: RemovePreviousVersions in the .vdproj already
// removes every file the previous version's own install knew about, as long
// as ProductVersion/ProductCode get bumped for each release. This list only
// needs specific, individually-confirmed-safe entries added to it - it's
// deliberately not a general "delete anything unrecognized" sweep, since some
// files are allowed to exist here even though the installer itself doesn't
// place them (e.g. a KffCrossReference.csv dropped in manually - see
// Form1.GetKffCsvReadPath).
//
// Takes the folder to clean as its first argument (the installer passes
// [TARGETDIR]), defaulting to its own folder if none is given - which is
// also where the installer places it, so it can be run by hand at any time
// as a manual cleanup pass.

string[] obsoleteRelativePaths =
[
    "elevation.bin.gz", // superseded by ParkElevations.csv - see ElevationLookupService
];

string targetDir = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? args[0]
    : AppContext.BaseDirectory;

foreach (string relativePath in obsoleteRelativePaths)
{
    string fullPath = Path.Combine(targetDir, relativePath);
    try
    {
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            Console.WriteLine($"Removed obsolete file: {fullPath}");
        }
    }
    catch (Exception ex)
    {
        // Cleanup is a best-effort convenience, not something that should
        // ever fail the install itself.
        Console.Error.WriteLine($"Could not remove {fullPath}: {ex.Message}");
    }
}

return 0;
