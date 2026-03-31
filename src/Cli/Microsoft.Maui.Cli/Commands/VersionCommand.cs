// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Reflection;
using Microsoft.Maui.Cli.Output;

namespace Microsoft.Maui.Cli.Commands;

/// <summary>
/// Implementation of 'maui version' command group.
/// Sub-commands are in partial class files: Check, List, Update.
/// </summary>
public static partial class VersionCommands
{
	public static Command Create()
	{
		var command = new Command("version", "MAUI version information and management");

		// Default action: show CLI version (existing behavior)
		command.SetAction((ParseResult parseResult) =>
		{
			var assembly = Assembly.GetExecutingAssembly();
			var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
				?? assembly.GetName().Version?.ToString()
				?? "0.0.0";

			var formatter = Program.GetFormatter(parseResult);
			var useJson = parseResult.GetValue(GlobalOptions.JsonOption);

			if (useJson)
			{
				formatter.Write(new
				{
					version,
					runtime = Environment.Version.ToString(),
					os = Environment.OSVersion.ToString()
				});
			}
			else
			{
				formatter.WriteVersion(version, $".NET {Environment.Version}", Environment.OSVersion.ToString());
			}
		});

		// Subcommands
		command.Add(CreateCheckCommand());
		command.Add(CreateListCommand());
		command.Add(CreateUpdateCommand());

		return command;
	}
}
