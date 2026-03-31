// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Maui.Cli.Output;
using Microsoft.Maui.Cli.Services;

namespace Microsoft.Maui.Cli.Commands;

public static partial class VersionCommands
{
	static Command CreateCheckCommand()
	{
		var projectOption = new Option<string>("--project", "-p")
		{
			Description = "Path to a .csproj file (auto-discovers in current directory if omitted)"
		};

		var command = new Command("check", "Check installed MAUI version in a project")
		{
			projectOption
		};

		command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
		{
			var formatter = Program.GetFormatter(parseResult);
			var useJson = parseResult.GetValue(GlobalOptions.JsonOption);
			var projectPath = parseResult.GetValue(projectOption);

			var projectService = new ProjectVersionService();

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

			formatter.WriteInfo($"Checking {Path.GetFileName(projectPath)}...");

			var versionInfo = await projectService.GetInstalledVersionAsync(projectPath, cancellationToken);

			if (versionInfo.ControlsVersion is null && versionInfo.CompatibilityVersion is null)
			{
				formatter.WriteError(new InvalidOperationException(
					$"No .NET MAUI package references found in {Path.GetFileName(projectPath)}. Is this a .NET MAUI project?"));
				return 1;
			}

			if (useJson)
			{
				formatter.Write(new
				{
					project = Path.GetFileName(projectPath),
					controlsVersion = versionInfo.ControlsVersion,
					compatibilityVersion = versionInfo.CompatibilityVersion,
					resolvedVersion = versionInfo.ResolvedVersion,
					versionMismatch = versionInfo.ControlsVersion != versionInfo.CompatibilityVersion
						&& versionInfo.CompatibilityVersion is not null
				});
			}
			else
			{
				formatter.WriteInfo($"Microsoft.Maui.Controls: {versionInfo.ControlsVersion ?? "(not found)"}");

				if (versionInfo.CompatibilityVersion is not null)
					formatter.WriteInfo($"Microsoft.Maui.Controls.Compatibility: {versionInfo.CompatibilityVersion}");

				if (versionInfo.ResolvedVersion is not null)
					formatter.WriteInfo($"$(MauiVersion) resolves to: {versionInfo.ResolvedVersion}");

				if (versionInfo.ControlsVersion != versionInfo.CompatibilityVersion
					&& versionInfo.CompatibilityVersion is not null)
				{
					formatter.WriteWarning(
						"Mixed versions detected for .NET MAUI packages. This could cause unexpected results.");
				}
			}

			return 0;
		});

		return command;
	}
}
