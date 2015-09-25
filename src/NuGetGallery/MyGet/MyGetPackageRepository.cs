using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;
using NuGetGallery.Packaging;

namespace NuGetGallery.MyGet
{
	public class MyGetPackageRepository : IEntityRepository<Package>
	{
		public void CommitChanges()
		{
			throw new NotSupportedException();
		}

		public void DeleteOnCommit(Package entity)
		{
			throw new NotSupportedException();
		}

		public int InsertOnCommit(Package entity)
		{
			throw new NotSupportedException();
		}

		public Package GetEntity(int key)
		{
			throw new NotSupportedException();
		}

		private static IList<PackageRegistration> _packageRegistrations = new List<PackageRegistration>();
		private static IList<Package> _packages;

		public IQueryable<Package> GetAll()
		{
			if (_packages != null)
			{
				return _packages.AsQueryable();
			}

			var feedUrl = ConfigurationManager.AppSettings["MyGetFeedUrl"];
			var repo = CreateRemotePackageRepository(feedUrl);
			var nugetPackages = repo.GetPackages().ToList();
			_packages = nugetPackages.Select(ConvertToPackage).ToList();

			return _packages.AsQueryable();
		}

		private IPackageRepository CreateRemotePackageRepository(string feedUrl)
		{
			var httpClient = PackageRepositoryFactory.Default.HttpClientFactory(new Uri(feedUrl));
			return new FixedDataServicePackageRepository(httpClient);
		}

		private Package ConvertToPackage(IPackage nugetPackage)
		{
			ValidateNuGetPackageMetadata(nugetPackage);
			var packageRegistration = CreateOrGetPackageRegistration(nugetPackage);

			var nupkg = new NugetPackageNupkg(nugetPackage);
			var package = CreatePackageFromNuGetPackage(packageRegistration, nupkg);
			packageRegistration.Packages.Add(package);
			UpdateIsLatest(packageRegistration);

			return package;
		}

		private PackageRegistration CreateOrGetPackageRegistration(IPackage nugetPackage)
		{
			var packageRegistration = _packageRegistrations.SingleOrDefault(pr => pr.Id == nugetPackage.Id);

			if (packageRegistration == null)
			{
				packageRegistration = new PackageRegistration
				{
					Id = nugetPackage.Id,
					DownloadCount = nugetPackage.DownloadCount,
					Owners = nugetPackage.Owners.Select(username => new User
					{
						Username = username
					}).ToList()
				};

				_packageRegistrations.Add(packageRegistration);
			}

			return packageRegistration;
		}

		private Package CreatePackageFromNuGetPackage(PackageRegistration packageRegistration, INupkg nugetPackage)
		{
			var package = packageRegistration.Packages.SingleOrDefault(pv => pv.Version == nugetPackage.Metadata.Version.ToString());

			if (package != null)
			{
				throw new EntityException(
					"A package with identifier '{0}' and version '{1}' already exists.", packageRegistration.Id, package.Version);
			}

			var now = DateTime.UtcNow;
			var packageFileStream = nugetPackage.GetStream();

			package = new Package
			{
				// Version must always be the exact string from the nuspec, which ToString will return to us. 
				// However, we do also store a normalized copy for looking up later.
				Version = nugetPackage.Metadata.Version.ToString(),
				NormalizedVersion = nugetPackage.Metadata.Version.ToNormalizedString(),

				Description = nugetPackage.Metadata.Description,
				ReleaseNotes = nugetPackage.Metadata.ReleaseNotes,
				HashAlgorithm = Constants.Sha512HashAlgorithmId,
				Hash = CryptographyService.GenerateHash(packageFileStream.ReadAllBytes()),
				PackageFileSize = packageFileStream.Length,
				Created = now,
				Language = nugetPackage.Metadata.Language,
				LastUpdated = now,
				Published = now,
				Copyright = nugetPackage.Metadata.Copyright,
				FlattenedAuthors = nugetPackage.Metadata.Authors.Flatten(),
				IsPrerelease = !nugetPackage.Metadata.IsReleaseVersion(),
				Listed = true,
				PackageRegistration = packageRegistration,
				RequiresLicenseAcceptance = nugetPackage.Metadata.RequireLicenseAcceptance,
				Summary = nugetPackage.Metadata.Summary,
				Tags = PackageHelper.ParseTags(nugetPackage.Metadata.Tags),
				Title = nugetPackage.Metadata.Title,

				DownloadCount = ((FixedDataServicePackage) nugetPackage.Metadata).VersionDownloadCount
			};

			package.IconUrl = nugetPackage.Metadata.IconUrl.ToEncodedUrlStringOrNull();
			package.LicenseUrl = nugetPackage.Metadata.LicenseUrl.ToEncodedUrlStringOrNull();
			package.ProjectUrl = nugetPackage.Metadata.ProjectUrl.ToEncodedUrlStringOrNull();
			package.MinClientVersion = nugetPackage.Metadata.MinClientVersion.ToStringOrNull();

#pragma warning disable 618 // TODO: remove Package.Authors completely once prodution services definitely no longer need it
			foreach (var author in nugetPackage.Metadata.Authors)
			{
				package.Authors.Add(new PackageAuthor { Name = author });
			}
#pragma warning restore 618

			var supportedFrameworks = nugetPackage.GetSupportedFrameworks().Select(fn => fn.ToShortNameOrNull()).ToArray();
			if (!supportedFrameworks.AnySafe(sf => sf == null))
			{
				foreach (var supportedFramework in supportedFrameworks)
				{
					package.SupportedFrameworks.Add(new PackageFramework { TargetFramework = supportedFramework });
				}
			}

			foreach (var dependencySet in nugetPackage.Metadata.DependencySets)
			{
				if (dependencySet.Dependencies.Count == 0)
				{
					package.Dependencies.Add(
						new PackageDependency
						{
							Id = null,
							VersionSpec = null,
							TargetFramework = dependencySet.TargetFramework.ToShortNameOrNull()
						});
				}
				else
				{
					foreach (var dependency in dependencySet.Dependencies.Select(d => new { d.Id, d.VersionSpec, dependencySet.TargetFramework }))
					{
						package.Dependencies.Add(
							new PackageDependency
							{
								Id = dependency.Id,
								VersionSpec = dependency.VersionSpec == null ? null : dependency.VersionSpec.ToString(),
								TargetFramework = dependency.TargetFramework.ToShortNameOrNull()
							});
					}
				}
			}

			package.FlattenedDependencies = package.Dependencies.Flatten();

			return package;
		}

		private static void UpdateIsLatest(PackageRegistration packageRegistration)
		{
			if (!packageRegistration.Packages.Any())
			{
				return;
			}

			// TODO: improve setting the latest bit; this is horrible. Trigger maybe? 
			foreach (var pv in packageRegistration.Packages.Where(p => p.IsLatest || p.IsLatestStable))
			{
				pv.IsLatest = false;
				pv.IsLatestStable = false;
				pv.LastUpdated = DateTime.UtcNow;
			}

			// If the last listed package was just unlisted, then we won't find another one
			var latestPackage = FindPackage(packageRegistration.Packages, p => p.Listed);

			if (latestPackage != null)
			{
				latestPackage.IsLatest = true;
				latestPackage.LastUpdated = DateTime.UtcNow;

				if (latestPackage.IsPrerelease)
				{
					// If the newest uploaded package is a prerelease package, we need to find an older package that is 
					// a release version and set it to IsLatest.
					var latestReleasePackage = FindPackage(packageRegistration.Packages.Where(p => !p.IsPrerelease && p.Listed));
					if (latestReleasePackage != null)
					{
						// We could have no release packages
						latestReleasePackage.IsLatestStable = true;
						latestReleasePackage.LastUpdated = DateTime.UtcNow;
					}
				}
				else
				{
					// Only release versions are marked as IsLatestStable. 
					latestPackage.IsLatestStable = true;
				}
			}
		}

		private static Package FindPackage(IEnumerable<Package> packages, Func<Package, bool> predicate = null)
		{
			if (predicate != null)
			{
				packages = packages.Where(predicate);
			}
			SemanticVersion version = packages.Max(p => new SemanticVersion(p.Version));

			if (version == null)
			{
				return null;
			}
			return packages.First(pv => pv.Version.Equals(version.ToString(), StringComparison.OrdinalIgnoreCase));
		}

		private static void ValidateNuGetPackageMetadata(IPackageMetadata nugetPackage)
		{
			// TODO: Change this to use DataAnnotations
			if (nugetPackage.Id.Length > 100)
			{
				throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Id", "100");
			}
			if (nugetPackage.Authors != null && nugetPackage.Authors.Flatten().Length > 4000)
			{
				throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Authors", "4000");
			}
			if (nugetPackage.Copyright != null && nugetPackage.Copyright.Length > 4000)
			{
				throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Copyright", "4000");
			}
			if (nugetPackage.Description != null && nugetPackage.Description.Length > 4000)
			{
				throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Description", "4000");
			}
			if (nugetPackage.IconUrl != null && nugetPackage.IconUrl.AbsoluteUri.Length > 4000)
			{
				throw new EntityException(Strings.NuGetPackagePropertyTooLong, "IconUrl", "4000");
			}
			if (nugetPackage.LicenseUrl != null && nugetPackage.LicenseUrl.AbsoluteUri.Length > 4000)
			{
				throw new EntityException(Strings.NuGetPackagePropertyTooLong, "LicenseUrl", "4000");
			}
			if (nugetPackage.ProjectUrl != null && nugetPackage.ProjectUrl.AbsoluteUri.Length > 4000)
			{
				throw new EntityException(Strings.NuGetPackagePropertyTooLong, "ProjectUrl", "4000");
			}
			if (nugetPackage.Summary != null && nugetPackage.Summary.Length > 4000)
			{
				throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Summary", "4000");
			}
			if (nugetPackage.Tags != null && nugetPackage.Tags.Length > 4000)
			{
				throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Tags", "4000");
			}
			if (nugetPackage.Title != null && nugetPackage.Title.Length > 256)
			{
				throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Title", "256");
			}

			if (nugetPackage.Version != null && nugetPackage.Version.ToString().Length > 64)
			{
				throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Version", "64");
			}

			if (nugetPackage.Language != null && nugetPackage.Language.Length > 20)
			{
				throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Language", "20");
			}

			foreach (var dependency in nugetPackage.DependencySets.SelectMany(s => s.Dependencies))
			{
				if (dependency.Id != null && dependency.Id.Length > 128)
				{
					throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Dependency.Id", "128");
				}

				if (dependency.VersionSpec != null && dependency.VersionSpec.ToString().Length > 256)
				{
					throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Dependency.VersionSpec", "256");
				}
			}

			if (nugetPackage.DependencySets != null && nugetPackage.DependencySets.Flatten().Length > Int16.MaxValue)
			{
				throw new EntityException(Strings.NuGetPackagePropertyTooLong, "Dependencies", Int16.MaxValue);
			}
		}

		public class NugetPackageNupkg : INupkg
		{
			private readonly IPackage _package;

			public NugetPackageNupkg(IPackage package)
			{
				_package = package;
			}

			public void Dispose()
			{
			}

			public IPackageMetadata Metadata
			{
				get { return _package; }
			}

			public IEnumerable<string> Parts
			{
				get { return new List<string>(); }
			}
			public IEnumerable<string> GetFiles()
			{
				return _package.GetFiles().Select(f => f.Path);
			}

			public Stream GetSizeVerifiedFileStream(string filePath, int maxSize)
			{
				throw new NotSupportedException();
			}

			public Stream GetStream()
			{
				return _package.GetStream();
			}

			public IEnumerable<FrameworkName> GetSupportedFrameworks()
			{
				return _package.GetSupportedFrameworks();
			}
		}
	}
}