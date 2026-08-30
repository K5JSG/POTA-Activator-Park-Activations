using System.Diagnostics;

string exePath = @"C:\Users\jsgay\Documents\Ham Radio\POTA Activator Park Activations\bin\Release\net10.0-windows\POTA Activator Park Activations.exe";
string msiFolder = @"C:\Users\jsgay\Documents\Ham Radio\POTA Activator Park Activations\Installer\POTA Activator Park Activations Installer\Release";
string projectName = "POTA Activator Park Activations";

if (!File.Exists(exePath))
{
    Console.WriteLine("Executable not found - skipping rename.");
    Console.WriteLine($"Looked for: {exePath}");
    return 0;
}

// Fetch the version from the actual compiled .exe application file
string? fullVersion = FileVersionInfo.GetVersionInfo(exePath).FileVersion;

string version;
if (string.IsNullOrEmpty(fullVersion))
{
    Console.WriteLine("Warning: Version string returned blank from file properties.");
    version = "1.0.0"; // Fallback version if properties are blank
}
else
{
    // Splits 1.0.0.0 down to 1.0.0
    version = string.Join(".", fullVersion.Split('.').Take(3));
}

string sourceMsi = Path.Combine(msiFolder, $"{projectName}.msi");
string targetMsi = Path.Combine(msiFolder, $"{projectName} Setup {version}.msi");

if (File.Exists(targetMsi))
{
    File.Delete(targetMsi);
}

if (File.Exists(sourceMsi))
{
    File.Move(sourceMsi, targetMsi);
    Console.WriteLine($"Renamed MSI to: {Path.GetFileName(targetMsi)}");
}
else
{
    Console.WriteLine($"{sourceMsi} not found - already renamed or build name doesn't match.");
}

return 0;
