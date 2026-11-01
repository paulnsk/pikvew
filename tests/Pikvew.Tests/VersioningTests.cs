using System.Diagnostics;
using System.Xml.Linq;
using Xunit;

namespace Pikvew.Tests;

public sealed class VersioningTests
{
    [Fact]
    public void BuildCounterIncrementsAndResetsForMinorAndMajorChanges()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "Version.props"))) repository = repository.Parent;
        Assert.NotNull(repository);
        var directory = Path.Combine(Path.GetTempPath(), "PikvewVersion-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var config = new XDocument(new XElement("Project", new XElement("PropertyGroup",
            new XElement("VersionMajor", 0), new XElement("VersionMinor", 1), new XElement("MinorVersionMajor", 0), new XElement("BuildFloor", 0))));
        string Build()
        {
            config.Save(Path.Combine(directory, "Version.props"));
            var output = Path.Combine(directory, "version.txt");
            var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(repository.FullName, "scripts", "build-version.ps1"), "-Repository", directory, "-Output", output }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var error = process.StandardError.ReadToEnd();
            Assert.True(process.WaitForExit(15000));
            Assert.True(process.ExitCode == 0, error);
            return File.ReadAllText(output);
        }
        try
        {
            Assert.Equal("0.1.1", Build());
            Assert.Equal("0.1.2", Build());
            config.Root!.Element("PropertyGroup")!.Element("VersionMinor")!.Value = "2";
            Assert.Equal("0.2.1", Build());
            config.Root.Element("PropertyGroup")!.Element("VersionMajor")!.Value = "1";
            Assert.Equal("1.1.1", Build());
            Assert.Equal("1.1.2", Build());
        }
        finally { Directory.Delete(directory, true); }
    }
}
