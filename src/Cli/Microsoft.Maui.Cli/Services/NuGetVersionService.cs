// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.Maui.Cli.Services;

/// <summary>
/// Release channel for querying MAUI package versions.
/// </summary>
public enum ReleaseChannel
{
	Stable,
	Nightly,
}

/// <summary>
/// Service for querying .NET MAUI package versions from NuGet feeds.
/// </summary>
public interface INuGetVersionService
{
	/// <summary>
	/// Gets the latest version of a package from the specified channel.
	/// </summary>
	Task<NuGetVersion?> GetLatestVersionAsync(string packageId, ReleaseChannel channel,
		bool includePrereleases, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets all available versions of a package from the specified channel.
	/// </summary>
	Task<IReadOnlyList<NuGetVersion>> GetAllVersionsAsync(string packageId, ReleaseChannel channel,
		bool includePrereleases, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the feed URL for the specified channel.
	/// </summary>
	string GetFeedUrl(ReleaseChannel channel);
}

/// <inheritdoc />
public class NuGetVersionService : INuGetVersionService
{
	internal const string StableFeedUrl = "https://api.nuget.org/v3/index.json";
	internal const string NightlyFeedUrl = "https://pkgs.dev.azure.com/xamarin/public/_packaging/maui-nightly/nuget/v3/index.json";

	/// <inheritdoc />
	public string GetFeedUrl(ReleaseChannel channel) => channel switch
	{
		ReleaseChannel.Nightly => NightlyFeedUrl,
		_ => StableFeedUrl,
	};

	/// <inheritdoc />
	public async Task<NuGetVersion?> GetLatestVersionAsync(string packageId, ReleaseChannel channel,
		bool includePrereleases, CancellationToken cancellationToken = default)
	{
		var versions = await GetAllVersionsAsync(packageId, channel, includePrereleases, cancellationToken);
		return versions.LastOrDefault();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<NuGetVersion>> GetAllVersionsAsync(string packageId, ReleaseChannel channel,
		bool includePrereleases, CancellationToken cancellationToken = default)
	{
		var feedUrl = GetFeedUrl(channel);
		using var cache = new SourceCacheContext();
		var repository = Repository.Factory.GetCoreV3(feedUrl);
		var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);

		var allVersions = await resource.GetAllVersionsAsync(
			packageId, cache, NullLogger.Instance, cancellationToken);

		IEnumerable<NuGetVersion> results = allVersions;

		if (channel == ReleaseChannel.Nightly)
		{
			// Nightly feed contains many version types; filter to actual nightlies
			results = results.Where(v => v.OriginalVersion is not null &&
				v.OriginalVersion.Contains("nightly", StringComparison.OrdinalIgnoreCase));
			// Nightlies are always prerelease
			includePrereleases = true;
		}

		if (!includePrereleases)
		{
			results = results.Where(v => !v.IsPrerelease);
		}

		return results.OrderBy(v => v).ToList();
	}
}
