using System;
using System.Linq;
using System.Reflection;
using NuGet;

namespace NuGetGallery.MyGet
{
	public class FixedDataServicePackageRepository : DataServicePackageRepository
	{
		private readonly Uri _serviceRoot;

		public FixedDataServicePackageRepository(Uri serviceRoot) : base(serviceRoot)
		{
			_serviceRoot = serviceRoot;
		}

		public FixedDataServicePackageRepository(IHttpClient client) : base(client)
		{
			_serviceRoot = client.Uri;
		}

		public FixedDataServicePackageRepository(IHttpClient client, PackageDownloader packageDownloader) : base(client, packageDownloader)
		{
			_serviceRoot = client.Uri;
		}

		public override IQueryable<IPackage> GetPackages()
		{
			var context = (IDataServiceContext) typeof (DataServicePackageRepository)
				.GetProperty("Context", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(this);
			var innerContext = (dynamic) typeof (DataServiceContextWrapper)
				.GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(context);
			innerContext.ResolveType = new Func<string, Type>(ResolveTypeFunction);

			return new SmartDataServiceQuery<FixedDataServicePackage>(context, "Packages");
		}

		private Type ResolveTypeFunction(string wireName)
		{
			if (wireName.EndsWith("V2FeedPackage", StringComparison.OrdinalIgnoreCase))
			{
				return typeof(FixedDataServicePackage);
			}
			return null;
		}
	}
}