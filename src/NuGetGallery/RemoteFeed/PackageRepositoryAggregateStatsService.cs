using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery.RemoteFeed
{
	public class PackageRepositoryAggregateStatsService : IAggregateStatsService
	{
		private readonly IEntityRepository<Package> _packageRepository;

		public PackageRepositoryAggregateStatsService(IEntityRepository<Package> packageRepository)
		{
			_packageRepository = packageRepository;
		}

		public Task<AggregateStats> GetAggregateStats()
		{
			var packages = _packageRepository.GetAll();

			return Task.FromResult(new AggregateStats
			{
				TotalPackages = packages.Count(),
				UniquePackages = packages.Count(p => p.IsLatest),
				Downloads = packages.Sum(p => p.DownloadCount),
				LastUpdateDateUtc = DateTime.Now
			});
		}
	}
}