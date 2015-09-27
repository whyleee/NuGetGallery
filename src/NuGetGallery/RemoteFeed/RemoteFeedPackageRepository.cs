using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Autofac;
using NuGet;

namespace NuGetGallery.RemoteFeed
{
	public class RemoteFeedPackageRepository : IEntityRepository<Package>
	{
		private static IList<Package> _packages;
		private static User _packageOwner;
		private readonly IComponentContext _componentContext;

		public RemoteFeedPackageRepository(IComponentContext componentContext)
		{
			_componentContext = componentContext;
		}

		public IQueryable<Package> GetAll()
		{
			if (_packages != null)
			{
				return _packages.AsQueryable();
			}

			var feedUrl = ConfigurationManager.AppSettings["RemoteFeedUrl"];
			_packageOwner = new User { Username = ConfigurationManager.AppSettings["RemoteFeedPackageOwner"] };
			var repo = CreateRemotePackageRepository(feedUrl);
			_packages = repo.GetPackages().Select(ConvertToPackage).ToList();

			return _packages.AsQueryable();
		}

		private IPackageRepository CreateRemotePackageRepository(string feedUrl)
		{
			var httpClient = PackageRepositoryFactory.Default.HttpClientFactory(new Uri(feedUrl));
			return new ExtendedDataServicePackageRepository(httpClient);
		}

		private Package ConvertToPackage(IPackage nugetPackage)
		{
			var packageService = _componentContext.Resolve<IPackageService>();
			var nupkg = new NugetPackageNupkg(nugetPackage);
			
			var package = packageService.CreatePackage(nupkg, _packageOwner, commitChanges: false);

			package.DownloadCount = ((ExtendedDataServicePackage) nugetPackage).VersionDownloadCount;
			package.PackageRegistration.DownloadCount = nugetPackage.DownloadCount;
			package.PackageRegistration.Owners.Add(_packageOwner);

			return package;
		}

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
	}
}