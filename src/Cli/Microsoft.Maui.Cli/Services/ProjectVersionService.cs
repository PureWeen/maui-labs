// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;
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
	string? ResolvedVersion,
	bool HasControlsReference,
	bool HasCompatibilityReference);

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
	/// Only updates packages already referenced in the project.
	/// </summary>
	Task InstallFromFeedAsync(string projectPath, string version, string feedUrl,
		bool hasCompatibility, CancellationToken cancellationToken = default);

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
		bool hasControls = false;
		bool hasCompat = false;

		if (nodes is not null)
		{
			foreach (XmlNode node in nodes)
			{
				var include = node.Attributes?["Include"]?.Value;
				var version = node.Attributes?["Version"]?.Value;

				if (string.Equals(include, "Microsoft.Maui.Controls", StringComparison.OrdinalIgnoreCase))
				{
					hasControls = true;
					// Version may be null when using implicit versioning (central package management)
					controlsVersion = version;
				}
				else if (string.Equals(include, "Microsoft.Maui.Controls.Compatibility", StringComparison.OrdinalIgnoreCase))
				{
					hasCompat = true;
					compatVersion = version;
				}
			}
		}

		// Resolve $(MauiVersion) or implicit versions via workload info
		string? resolvedVersion = null;
		if (controlsVersion == MauiVersionVariable || compatVersion == MauiVersionVariable
			|| (hasControls && controlsVersion is null))
		{
			resolvedVersion = await ResolveMauiVersionFromWorkloadAsync(cancellationToken);
		}

		return new MauiProjectVersionInfo(projectPath, controlsVersion, compatVersion,
			resolvedVersion, hasControls, hasCompat);
	}

	/// <inheritdoc />
	public async Task UpdateVersionAsync(string projectPath, string version, CancellationToken cancellationToken = default)
	{
		// Read the raw content to preserve original encoding and line endings
		var content = await File.ReadAllTextAsync(projectPath, cancellationToken);
		var lines = content.Split('\n');

		bool anyUpdated = false;
		for (int i = 0; i < lines.Length; i++)
		{
			if (lines[i].Contains("<PackageReference Include=\"Microsoft.Maui.Controls\"", StringComparison.OrdinalIgnoreCase) ||
				lines[i].Contains("<PackageReference Include=\"Microsoft.Maui.Controls.Compatibility\"", StringComparison.OrdinalIgnoreCase))
			{
				var match = VersionRegex().Match(lines[i]);
				if (match.Success)
				{
					lines[i] = lines[i].Replace(match.Groups[1].Value, version);
					anyUpdated = true;
				}
				// If no Version attribute exists, the project uses central package management
				// and the version is controlled by Directory.Packages.props — skip silently
			}
		}

		if (anyUpdated)
		{
			await File.WriteAllTextAsync(projectPath, string.Join('\n', lines), cancellationToken);
		}

		await RunDotnetAsync($"restore \"{projectPath}\"", cancellationToken);
	}

	/// <inheritdoc />
	public async Task InstallFromFeedAsync(string projectPath, string version, string feedUrl,
		bool hasCompatibility, CancellationToken cancellationToken = default)
	{
		// Always update Controls (required for all MAUI projects)
		await RunDotnetAsync(
			$"add \"{projectPath}\" package Microsoft.Maui.Controls -v {version} -s {feedUrl}",
			cancellationToken);

		// Only update Compatibility if the project already references it
		if (hasCompatibility)
		{
			await RunDotnetAsync(
				$"add \"{projectPath}\" package Microsoft.Maui.Controls.Compatibility -v {version} -s {feedUrl}",
				cancellationToken);
		}
	}

	/// <inheritdoc />
	public async Task EnsureNuGetConfigAsync(string projectPath, string feedUrl, string feedName, CancellationToken cancellationToken = default)
	{
		var outputDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty;
		var nugetConfigPath = Path.Combine(outputDirectory, "nuget.config");
		var nugetConfigPathAlt = Path.Combine(outputDirectory, "NuGet.config");

		if (!File.Exists(nugetConfigPath) && !File.Exists(nugetConfigPathAlt))
		{
			await RunDotnetAsync($"new nugetconfig -o \"{outputDirectory}\"", cancellationToken);
		}

		var configPath = File.Exists(nugetConfigPath) ? nugetConfigPath : nugetConfigPathAlt;
		var fullPath = Path.GetFullPath(configPath);

		// Check if the source already exists to make this idempotent
		var (exitCode, output) = await RunDotnetWithOutputAsync(
			$"nuget list source --configfile \"{fullPath}\"", cancellationToken);

		if (exitCode == 0 && output.Contains(feedName, StringComparison.OrdinalIgnoreCase))
		{
			// Source already exists — update it instead
			await RunDotnetAsync(
				$"nuget update source \"{feedName}\" --source {feedUrl} --configfile \"{fullPath}\"",
				cancellationToken);
		}
		else
		{
			await RunDotnetAsync(
				$"nuget add source {feedUrl} -n \"{feedName}\" --configfile \"{fullPath}\"",
				cancellationToken);
		}
	}

	static async Task RunDotnetAsync(string arguments, CancellationToken cancellationToken)
	{
		using var process = new Process();
		process.StartInfo.FileName = "dotnet";
		process.StartInfo.Arguments = arguments;
		process.StartInfo.UseShellExecute = false;
		process.StartInfo.RedirectStandardError = true;
		process.Start();

		var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);

		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException(
				$"'dotnet {arguments}' failed with exit code {process.ExitCode}: {stderr.Trim()}");
		}
	}

	static async Task<(int ExitCode, string Output)> RunDotnetWithOutputAsync(string arguments, CancellationToken cancellationToken)
	{
		using var process = new Process();
		process.StartInfo.FileName = "dotnet";
		process.StartInfo.Arguments = arguments;
		process.StartInfo.UseShellExecute = false;
		process.StartInfo.RedirectStandardOutput = true;
		process.Start();

		var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);

		return (process.ExitCode, output);
	}

	static async Task<string?> ResolveMauiVersionFromWorkloadAsync(CancellationToken cancellationToken)
	{
		var (exitCode, output) = await RunDotnetWithOutputAsync("workload --info", cancellationToken);

		if (exitCode != 0 || !output.Contains("[maui]", StringComparison.OrdinalIgnoreCase))
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
