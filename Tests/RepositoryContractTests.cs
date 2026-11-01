using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

// Portable repository-contract checks. ProjectNexus may execute these only
// on a trusted, permitted source, never as an unreviewed PR host process.
internal static class RepositoryContractTests
{
    internal static void Verify()
    {
        string root = FindRoot();
        XElement about = XDocument.Load(Path.Combine(root, "About", "About.xml")).Root
            ?? throw new InvalidOperationException("About.xml is missing its root.");
        string[] gameVersions = about.Element("supportedVersions")?.Elements("li")
            .Select(li => li.Value.Trim()).ToArray() ?? Array.Empty<string>();
        Require(gameVersions.Length == 1 && gameVersions[0] == "1.7",
            "The package must support RimWorld 1.7 exclusively.");
        string version = about.Element("modVersion")?.Value.Trim() ?? "";
        Require(Version.TryParse(version, out _) && version.Split('.').Length == 3,
            "About.xml requires a three-part mod version.");
        string info = Read(root, "Source", "AssemblyInfo.cs");
        Require(info.Contains("AssemblyVersion(\"" + version + ".0\")", StringComparison.Ordinal) &&
                info.Contains("AssemblyFileVersion(\"" + version + ".0\")", StringComparison.Ordinal),
            "AssemblyInfo and About.xml versions differ.");
        Require(!Directory.Exists(Path.Combine(root, "1.6")),
            "The retired 1.6 package folder is still present.");
        Require(File.Exists(Path.Combine(root, "1.7", "Languages", "English",
            "Keyed", "InsightCanvas.xml")), "1.7 keyed translations are missing.");

        foreach (string relative in new[] {
            "Source/InsightCanvas.csproj",
            "DevTools/BridgeTools/InsightCanvas.BridgeTools.csproj",
            "Examples/MinimalConsumer/InsightCanvas.MinimalConsumer.csproj" })
        {
            string project = File.ReadAllText(Path.Combine(root, relative));
            Require(project.Contains("<TargetFramework>net10.0</TargetFramework>",
                StringComparison.Ordinal), relative + " must target net10.0.");
            foreach (string gameDll in new[] {
                "Assembly-CSharp.dll", "Waylith.dll", "UnityStubs.dll" })
                Require(project.Contains(gameDll, StringComparison.Ordinal),
                    relative + " lacks a RimWorld 1.7 reference to " + gameDll + ".");
            foreach (string oldDll in new[] {
                "UnityEngine.CoreModule.dll", "UnityEngine.IMGUIModule.dll",
                "UnityEngine.TextRenderingModule.dll" })
                Require(!project.Contains(oldDll, StringComparison.Ordinal),
                    relative + " still references removed Unity module " + oldDll + ".");
        }
        Require(Read(root, "Tests", "InsightCanvas.CoreTests.csproj")
                .Contains("<TargetFramework>net10.0</TargetFramework>", StringComparison.Ordinal),
            "The portable harness must target net10.0.");

        using (JsonDocument stack = JsonDocument.Parse(Read(root, ".rimdev", "stack.json")))
        {
            JsonElement properties = stack.RootElement;
            Require(properties.GetProperty("deploymentTarget").GetString() ==
                "1.7/Assemblies/InsightCanvas.dll", "Wrong 1.7 runtime deployment target.");
            string[] included = properties.GetProperty("runtimePackage")
                .GetProperty("include").EnumerateArray()
                .Select(li => li.GetString() ?? "").ToArray();
            Require(included.Contains("1.7/**", StringComparer.Ordinal),
                "Runtime package does not include the 1.7 mod files.");
            Require(!included.Any(pattern => pattern == "1.*/**" ||
                    pattern.StartsWith("1.6/", StringComparison.Ordinal)),
                "Runtime package still includes 1.6 or unbounded game versions.");
        }

        using (JsonDocument manifest = JsonDocument.Parse(
                   Read(root, ".projectnexus", "project.json")))
        {
            JsonElement rootElement = manifest.RootElement;
            Require(rootElement.GetProperty("projectId").GetString() == "insightcanvas",
                "Incorrect ProjectNexus project identity.");
            HashSet<string> declared = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement check in rootElement.GetProperty("capabilities")
                         .GetProperty("checks").EnumerateArray())
                declared.Add(check.GetProperty("capabilityId").GetString() ?? "");
            foreach (string required in new[] {
                "validation.portable-core", "validation.waylith17-build",
                "validation.waylith17-consumer" })
                Require(declared.Contains(required),
                    "ProjectNexus lacks the mandatory check: " + required + ".");
            JsonElement pack = rootElement.GetProperty("validationPack");
            Require(pack.GetProperty("releaseTargets").EnumerateArray()
                .Any(item => item.GetString() == "win32"),
                "The Waylith release gate must target Windows.");
            bool found = false;
            foreach (JsonElement profile in pack.GetProperty("profiles").EnumerateArray())
            {
                if (profile.GetProperty("profileId").GetString() != "waylith17") continue;
                found = true;
                HashSet<string> selected = new HashSet<string>(
                    profile.GetProperty("checkIds").EnumerateArray()
                        .Select(item => item.GetString() ?? ""), StringComparer.Ordinal);
                Require(selected.Contains("validation.waylith17-build") &&
                        selected.Contains("validation.waylith17-consumer"),
                    "Waylith profile must cover the framework and independent consumer.");
            }
            Require(found, "The ProjectNexus Waylith release profile is missing.");
        }
    }

    private static string FindRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo directory = new DirectoryInfo(Path.GetFullPath(start));
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "About", "About.xml")) &&
                    File.Exists(Path.Combine(directory.FullName, "Source", "InsightCanvas.csproj")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }
        throw new InvalidOperationException("InsightCanvas project root is missing.");
    }

    private static string Read(string root, params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
    }

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
