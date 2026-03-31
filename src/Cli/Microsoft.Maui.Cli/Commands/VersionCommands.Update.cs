// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Maui.Cli.Output;
using Microsoft.Maui.Cli.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Maui.Cli.Commands;

public static partial class VersionCommands
{
	static Command CreateUpdateCommand()
	{
		var projectOption = new Option<string>("--project", "-p")
		{
			Description = "Path to a .csproj file (auto-discovers in current directory if omitted)"
		};

		var versionOption = new Option<string>("--version")
		{
			Description = "Specific .NET MAUI version to install"
		};

		var latestOption = new Option<bool>("--latest")
		{
			Description = "Update to the latest stable version (uses $(MauiVersion))"
		};

		var latestNightlyOption = new Option<bool>("--latest-nightly")
		{
			Description = "Update to the latest nightly version"
		};

		var nugetConfigOption = new Option<bool>("--nuget-config")
		{
			Description = "Create or update NuGet.config with the nightly feed source"
		};

		var command = new Command("update", "Update .NET MAUI package versions in a project")
		{
			projectOption,
			versionOption,
			latestOption,
			latestNightlyOption,
			nugetConfigOption
		};

		command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
		{
			var formatter = Program.GetFormatter(parseResult);
			var dryRun = parseResult.GetValue(GlobalOptions.DryRunOption);
			var projectPath = parseResult.GetValue(projectOption);
			var specificVersion = parseResult.GetValue(versionOption);
			var latest = parseResult.GetValue(latestOption);
			var latestNightly = parseResult.GetValue(latestNightlyOption);
			var createNuGetConfig = parseResult.GetValue(nugetConfigOption);

			// Validate: exactly one version source must be specified
			var optionCount = (latest ? 1 : 0) + (latestNightly ? 1 : 0) + (!string.IsNullOrEmpty(specificVersion) ? 1 : 0);
			if (optionCount == 0)
			{
				formatter.WriteError(new InvalidOperationException(
					"Specify a version source: --latest, --latest-nightly, or --version <version>"));
				return 1;
			}
			if (optionCount > 1)
			{
				formatter.WriteError(new InvalidOperationException(
					"Cannot combine --latest, --latest-nightly, and --version. Choose one."));
				return 1;
			}

			if (latest && createNuGetConfig)
			{
				formatter.WriteError(new InvalidOperationException(
					"--nuget-config is only supported with --latest-nightly or --version."));
				return 1;
			}

			var projectService = Program.Services.GetService<IProjectVersionService>()
				?? new ProjectVersionService();
			var nugetService = Program.Services.GetService<INuGetVersionService>()
				?? new NuGetVersionService();

			// Discover project file if not specified
			if (string.IsNullOrEmpty(projectPath))
			{
				projectPath = projectService.DiscoverProjectFile();
				if (projectPath is null)
				{
					formatter.WriteError(new InvalidOperationException(
						"No single .csproj file found in current directory. Use --project to specify one."));
					return 1;
				}
			}

			if (!File.Exists(projectPath))
			{
				formatter.WriteError(new FileNotFoundException($"Project file not found: {projectPath}"));
				return 1;
			}

			formatter.WriteInfo($"Found project: {projectPath}");

			// Read current project state to know what packages exist
			var currentInfo = await projectService.GetInstalledVersionAsync(projectPath, cancellationToken);

			string versionToInstall;
			string? feedUrl = null;

			if (latest)
			{
				versionToInstall = "$(MauiVersion)";
				feedUrl = NuGetVersionService.StableFeedUrl;
			}
			else if (latestNightly)
			{
				formatter.WriteInfo("Querying nightly feed for latest version...");
				var nightlyVersion = await nugetService.GetLatestVersionAsync(
					"Microsoft.Maui.Controls", ReleaseChannel.Nightly, true, cancellationToken);

				if (nightlyVersion is null)
				{
					formatter.WriteError(new InvalidOperationException(
						"Could not determine latest nightly version."));
					return 1;
				}

				versionToInstall = nightlyVersion.OriginalVersion ?? nightlyVersion.ToString();
				feedUrl = NuGetVersionService.NightlyFeedUrl;
			}
			else
			{
				versionToInstall = specificVersion!;
			}

			formatter.WriteInfo($"Version: {versionToInstall}");

			if (dryRun)
			{
				formatter.WriteInfo("[dry-run] Would update MAUI packages to: " + versionToInstall);
				if (createNuGetConfig && feedUrl is not null)
					formatter.WriteInfo("[dry-run] Would create/update NuGet.config with feed: " + feedUrl);
				return 0;
			}

			try
			{
				if (latestNightly && feedUrl is not null)
				{
					// Use dotnet add package for nightly (handles feed source)
					formatter.WriteProgress("Installing from nightly feed...");
					await projectService.InstallFromFeedAsync(
						projectPath, versionToInstall, feedUrl,
						currentInfo.HasCompatibilityReference, cancellationToken);
				}
				else
				{
					// Direct csproj version replacement + restore
					formatter.WriteProgress("Updating package versions...");
					await projectService.UpdateVersionAsync(projectPath, versionToInstall, cancellationToken);
				}

				if (createNuGetConfig && feedUrl is not null)
				{
					formatter.WriteProgress("Configuring NuGet feed...");
					await projectService.EnsureNuGetConfigAsync(
						projectPath, feedUrl, ".NET MAUI Nightly", cancellationToken);
					formatter.WriteSuccess("NuGet.config updated with nightly feed.");
				}

				formatter.WriteSuccess($"Updated MAUI packages to {versionToInstall}");
				return 0;
			}
			catch (Exception ex)
			{
				formatter.WriteError(ex);
				return 1;
			}
		});

		return command;
	}
}
