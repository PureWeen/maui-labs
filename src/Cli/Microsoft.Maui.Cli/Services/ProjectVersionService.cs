// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;

namespace Microsoft.Maui.Cli.Services;

/// <summary>
/// Represents the MAUI package versions found in a project file.
/// </summary>
public record MauiProjectVersionInfo(
	string ProjectPath,
	string? ControlsVersion,
	string? CompatibilityVersion,
	string? ResolvedVersion);

/// <summary>
/// Service for reading and updating MAUI package versions in project files.
/// </summary>
public interface IProjectVersionService
{
	/// <summary>
	/// Discovers a .csproj file in the specified directory.
	/// </summary>
	string? DiscoverProjectFile(string? directory = null);

	/// <summary>
	/// Gets the installed MAUI version info from a project file.
	/// </summary>
	Task<MauiProjectVersionInfo> GetInstalledVersionAsync(string projectPath, CancellationToken cancellationToken = default);

	/// <summary>
	/// Updates MAUI package versions in the project file.
	/// </summary>
	Task UpdateVersionAsync(string projectPath, string version, CancellationToken cancellationToken = default);

	/// <summary>
	/// Installs a package from a specific feed using dotnet add package.
	/// </summary>
	Task InstallFromFeedAsync(string projectPath, string version, string feedUrl, CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates or updates a NuGet.config with the specified feed.
	/// </summary>
	Task EnsureNuGetConfigAsync(string projectPath, string feedUrl, string feedName, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public partial class ProjectVersionService : IProjectVersionService
{
	const string MauiVersionVariable = "$(MauiVersion)";

	/// <inheritdoc />
	public string? DiscoverProjectFile(string? directory = null)
	{
		directory ??= Environment.CurrentDirectory;
		var csprojFiles = Directory.GetFiles(directory, "*.csproj");

		return csprojFiles.Length switch
		{
			1 => csprojFiles[0],
			_ => null,
		};
	}

	/// <inheritdoc />
	public async Task<MauiProjectVersionInfo> GetInstalledVersionAsync(string projectPath, CancellationToken cancellationToken = default)
	{
		var xml = new XmlDocument();
		xml.LoadXml(await File.ReadAllTextAsync(projectPath, cancellationToken));

		var nodes = xml.SelectNodes(
			"//PackageReference[@Include=\"Microsoft.Maui.Controls\" or @Include=\"Microsoft.Maui.Controls.Compatibility\"]");

		string? controlsVersion = null;
		string? compatVersion = null;

		if (nodes is not null)
		{
			foreach (XmlNode node in nodes)
			{
				var include = node.Attributes?["Include"]?.Value;
				var version = node.Attributes?["Version"]?.Value;

				if (string.Equals(include, "Microsoft.Maui.Controls", StringComparison.OrdinalIgnoreCase))
					controlsVersion = version;
				else if (string.Equals(include, "Microsoft.Maui.Controls.Compatibility", StringComparison.OrdinalIgnoreCase))
					compatVersion = version;
			}
		}

		// Resolve $(MauiVersion) if used
		string? resolvedVersion = null;
		if (controlsVersion == MauiVersionVariable || compatVersion == MauiVersionVariable)
		{
			resolvedVersion = await ResolveMauiVersionFromWorkloadAsync(cancellationToken);
		}

		return new MauiProjectVersionInfo(projectPath, controlsVersion, compatVersion, resolvedVersion);
	}

	/// <inheritdoc />
	public async Task UpdateVersionAsync(string projectPath, string version, CancellationToken cancellationToken = default)
	{
		var lines = await File.ReadAllLinesAsync(projectPath, cancellationToken);

		for (int i = 0; i < lines.Length; i++)
		{
			if (lines[i].Contains("<PackageReference Include=\"Microsoft.Maui.Controls\"", StringComparison.OrdinalIgnoreCase) ||
				lines[i].Contains("<PackageReference Include=\"Microsoft.Maui.Controls.Compatibility\"", StringComparison.OrdinalIgnoreCase))
			{
				var match = VersionRegex().Match(lines[i]);
				if (match.Success)
				{
					lines[i] = lines[i].Replace(match.Groups[1].Value, version);
				}
			}
		}

		await File.WriteAllLinesAsync(projectPath, lines, cancellationToken);

		// Restore after version update
		using var process = new Process();
		process.StartInfo.FileName = "dotnet";
		process.StartInfo.Arguments = $"restore \"{projectPath}\"";
		process.StartInfo.UseShellExecute = false;
		process.Start();
		await process.WaitForExitAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task InstallFromFeedAsync(string projectPath, string version, string feedUrl, CancellationToken cancellationToken = default)
	{
		// Use dotnet add package which handles feed source resolution
		using var controlsProcess = new Process();
		controlsProcess.StartInfo.FileName = "dotnet";
		controlsProcess.StartInfo.Arguments = $"add \"{projectPath}\" package Microsoft.Maui.Controls -v {version} -s {feedUrl}";
		controlsProcess.StartInfo.UseShellExecute = false;
		controlsProcess.Start();
		await controlsProcess.WaitForExitAsync(cancellationToken);

		using var compatProcess = new Process();
		compatProcess.StartInfo.FileName = "dotnet";
		compatProcess.StartInfo.Arguments = $"add \"{projectPath}\" package Microsoft.Maui.Controls.Compatibility -v {version} -s {feedUrl}";
		compatProcess.StartInfo.UseShellExecute = false;
		compatProcess.Start();
		await compatProcess.WaitForExitAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task EnsureNuGetConfigAsync(string projectPath, string feedUrl, string feedName, CancellationToken cancellationToken = default)
	{
		var outputDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty;
		var nugetConfigPath = Path.Combine(outputDirectory, "nuget.config");
		var nugetConfigPathAlt = Path.Combine(outputDirectory, "NuGet.config");

		if (!File.Exists(nugetConfigPath) && !File.Exists(nugetConfigPathAlt))
		{
			using var createProcess = new Process();
			createProcess.StartInfo.FileName = "dotnet";
			createProcess.StartInfo.Arguments = $"new nugetconfig -o \"{outputDirectory}\"";
			createProcess.StartInfo.UseShellExecute = false;
			createProcess.Start();
			await createProcess.WaitForExitAsync(cancellationToken);
		}

		var configPath = File.Exists(nugetConfigPath) ? nugetConfigPath : nugetConfigPathAlt;

		using var addSourceProcess = new Process();
		addSourceProcess.StartInfo.FileName = "dotnet";
		addSourceProcess.StartInfo.Arguments = $"nuget add source {feedUrl} -n \"{feedName}\" --configfile \"{Path.GetFullPath(configPath)}\"";
		addSourceProcess.StartInfo.UseShellExecute = false;
		addSourceProcess.Start();
		await addSourceProcess.WaitForExitAsync(cancellationToken);
	}

	static async Task<string?> ResolveMauiVersionFromWorkloadAsync(CancellationToken cancellationToken)
	{
		using var process = new Process();
		process.StartInfo.FileName = "dotnet";
		process.StartInfo.Arguments = "workload --info";
		process.StartInfo.UseShellExecute = false;
		process.StartInfo.RedirectStandardOutput = true;
		process.Start();

		var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);

		if (!output.Contains("[maui]", StringComparison.OrdinalIgnoreCase))
			return null;

		var mauiSection = output[output.IndexOf("[maui]", StringComparison.OrdinalIgnoreCase)..];
		if (!mauiSection.Contains("Manifest Version:", StringComparison.OrdinalIgnoreCase))
			return null;

		var manifestIndex = mauiSection.IndexOf("Manifest Version:", StringComparison.OrdinalIgnoreCase);
		var afterLabel = mauiSection[(manifestIndex + "Manifest Version:".Length)..];

		// Version ends at '/' or newline
		var slashIndex = afterLabel.IndexOf('/');
		var newlineIndex = afterLabel.IndexOf('\n');
		var endIndex = (slashIndex >= 0 && newlineIndex >= 0) ? Math.Min(slashIndex, newlineIndex) :
			(slashIndex >= 0 ? slashIndex : (newlineIndex >= 0 ? newlineIndex : afterLabel.Length));

		return afterLabel[..endIndex].Trim();
	}

	[GeneratedRegex(@"Version=""(.*?)""")]
	private static partial Regex VersionRegex();
}
