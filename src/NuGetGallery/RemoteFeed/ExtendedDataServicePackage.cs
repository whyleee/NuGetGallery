using NuGet;

namespace NuGetGallery.RemoteFeed
{
	public class ExtendedDataServicePackage : DataServicePackage
	{
		public int VersionDownloadCount { get; set; }
	}
}