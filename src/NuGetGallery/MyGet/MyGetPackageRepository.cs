using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Autofac;
using NuGet;
using NuGetGallery.Packaging;

namespace NuGetGallery.MyGet
{
	public class MyGetPackageRepository : IEntityRepository<Package>
	{
		private readonly IComponentContext _componentContext;
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

		public MyGetPackageRepository(IComponentContext componentContext)
		{
			_componentContext = componentContext;
		}

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

			foreach (var package in _packages)
			{
				package.PackageRegistration.Owners.Remove(null);
			}

			return _packages.AsQueryable();
		}

		private IPackageRepository CreateRemotePackageRepository(string feedUrl)
		{
			var httpClient = PackageRepositoryFactory.Default.HttpClientFactory(new Uri(feedUrl));
			return new FixedDataServicePackageRepository(httpClient);
		}

		private Package ConvertToPackage(IPackage nugetPackage)
		{
			var packageService = _componentContext.Resolve<IPackageService>();
			var nupkg = new NugetPackageNupkg(nugetPackage);
			var package = packageService.CreatePackage(nupkg, null, commitChanges: false);

			package.DownloadCount = ((FixedDataServicePackage) nugetPackage).VersionDownloadCount;
			package.PackageRegistration.DownloadCount = nugetPackage.DownloadCount;
			package.PackageRegistration.Owners.AddRange(nugetPackage.Owners
				.Select(username => new User
				{
					Username = username
				}));

			return package;
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