// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Maui.Cli.Output;
using Microsoft.Maui.Cli.Services;

namespace Microsoft.Maui.Cli.Commands;

public static partial class VersionCommands
{
	static Command CreateListCommand()
	{
		var channelOption = new Option<string>("--channel", "-c")
		{
			Description = "Release channel: stable or nightly (default: stable)",
			DefaultValueFactory = _ => "stable"
		};

		var prereleaseOption = new Option<bool>("--prerelease")
		{
			Description = "Include prerelease versions"
		};

		var takeOption = new Option<int>("--take", "-t")
		{
			Description = "Number of versions to display (default: 10)",
			DefaultValueFactory = _ => 10
		};

		var command = new Command("list", "List available .NET MAUI versions")
		{
			channelOption,
			prereleaseOption,
			takeOption
		};

		command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
		{
			var formatter = Program.GetFormatter(parseResult);
			var useJson = parseResult.GetValue(GlobalOptions.JsonOption);
			var channelStr = parseResult.GetValue(channelOption) ?? "stable";
			var includePrerelease = parseResult.GetValue(prereleaseOption);
			var take = parseResult.GetValue(takeOption);

			var channel = channelStr.Equals("nightly", StringComparison.OrdinalIgnoreCase)
				? ReleaseChannel.Nightly
				: ReleaseChannel.Stable;

			var nugetService = new NuGetVersionService();

			formatter.WriteInfo($"Querying {channel} feed for Microsoft.Maui.Controls...");

			try
			{
				var versions = await nugetService.GetAllVersionsAsync(
					"Microsoft.Maui.Controls", channel, includePrerelease, cancellationToken);

				if (versions.Count == 0)
				{
					formatter.WriteWarning("No versions found matching the specified criteria.");
					return 0;
				}

				// Take the most recent N versions
				var displayVersions = versions.TakeLast(take).Reverse().ToList();

				if (useJson)
				{
					formatter.Write(new
					{
						channel = channel.ToString().ToLowerInvariant(),
						feed = nugetService.GetFeedUrl(channel),
						totalAvailable = versions.Count,
						versions = displayVersions.Select(v => new
						{
							version = v.OriginalVersion ?? v.ToString(),
							isPrerelease = v.IsPrerelease
						})
					});
				}
				else
				{
					formatter.WriteTable(displayVersions,
						("Version", v => v.OriginalVersion ?? v.ToString()),
						("Prerelease", v => v.IsPrerelease ? "Yes" : "No"));

					formatter.WriteInfo($"Showing {displayVersions.Count} of {versions.Count} available versions.");
				}
			}
			catch (Exception ex)
			{
				formatter.WriteError(ex);
				return 1;
			}

			return 0;
		});

		return command;
	}
}
