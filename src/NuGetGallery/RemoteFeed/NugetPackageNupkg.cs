using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;
using NuGetGallery.Packaging;

namespace NuGetGallery.RemoteFeed
{
	public class NugetPackageNupkg : INupkg
	{
		private readonly IPackage _package;

		public NugetPackageNupkg(IPackage package)
		{
			_package = package;
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

		public void Dispose() {}
	}
}