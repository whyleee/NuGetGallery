using NuGet;

namespace NuGetGallery.MyGet
{
	public class FixedDataServicePackage : DataServicePackage
	{
		public int VersionDownloadCount { get; set; }
	}
}