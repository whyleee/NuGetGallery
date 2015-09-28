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
		private IList<Package> _packages = new List<Package>();
		private User _packageOwner;
		private readonly IComponentContext _componentContext;

		public RemoteFeedPackageRepository(IComponentContext componentContext)
		{
			_componentContext = componentContext;
		}

		public IQueryable<Package> GetAll()
		{
			if (_packages.Any())
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
		}

		public void DeleteOnCommit(Package entity)
		{
			_packages.Remove(entity);
		}

		public int InsertOnCommit(Package entity)
		{
			_packages.Add(entity);
			return 1;
		}

		public Package GetEntity(int key)
		{
			throw new NotSupportedException();
		}
	}
}