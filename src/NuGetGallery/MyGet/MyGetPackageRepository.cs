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

		private static IList<Package> _packages;

		public IQueryable<Package> GetAll()
		{
			if (_packages != null)
			{
				return _packages.AsQueryable();
			}

			var feedUrl = ConfigurationManager.AppSettings["MyGetFeedUrl"];
			var repo = PackageRepositoryFactory.Default.CreateRepository(feedUrl);
			var nugetPackages = repo.GetPackages().ToList();
			_packages = nugetPackages.Select(ConvertToPackage).ToList();

			return _packages.AsQueryable();
		}

		private Package ConvertToPackage(IPackage nugetPackage)
		{
			ValidateNuGetPackageMetadata(nugetPackage);
			var packageRegistration = CreateOrGetPackageRegistration(nugetPackage);

			var nupkg = new NugetPackageNupkg(nugetPackage);
			var package = CreatePackageFromNuGetPackage(packageRegistration, nupkg);

			return package;
		}

		private PackageRegistration CreateOrGetPackageRegistration(IPackage nugetPackage)
		{
			var packageRegistration = new PackageRegistration
				{
					Id = nugetPackage.Id,
					DownloadCount = nugetPackage.DownloadCount,
					Owners = nugetPackage.Owners.Select(username => new User
					{
						Username = username
					}).ToList()
				};

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
				User = new User("whyleee"),

				DownloadCount = ((IPackage) nugetPackage.Metadata).DownloadCount,
				IsLatest = true,
				IsLatestStable = true
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