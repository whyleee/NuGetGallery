using System;
using System.Linq;
using System.Reflection;
using NuGet;

namespace NuGetGallery.RemoteFeed
{
	public class ExtendedDataServicePackageRepository : DataServicePackageRepository
	{
		public ExtendedDataServicePackageRepository(Uri serviceRoot) : base(serviceRoot) {}
		public ExtendedDataServicePackageRepository(IHttpClient client) : base(client) {}
		public ExtendedDataServicePackageRepository(IHttpClient client, PackageDownloader packageDownloader) : base(client, packageDownloader) {}

		public override IQueryable<IPackage> GetPackages()
		{
			var context = RegisterExtendedDataServicePackageType();
			return new SmartDataServiceQuery<ExtendedDataServicePackage>(context, "Packages");
		}

		private IDataServiceContext RegisterExtendedDataServicePackageType()
		{
			var context = (IDataServiceContext) typeof (DataServicePackageRepository)
				.GetProperty("Context", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(this);
			var innerContext = (dynamic) typeof (DataServiceContextWrapper)
				.GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(context);
			innerContext.ResolveType = new Func<string, Type>(ResolveTypeFunction);

			return context;
		}

		private Type ResolveTypeFunction(string wireName)
		{
			if (wireName.EndsWith("V2FeedPackage", StringComparison.OrdinalIgnoreCase))
			{
				return typeof(ExtendedDataServicePackage);
			}
			return null;
		}
	}
}