using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace NuGetGallery.RemoteFeed
{
    public class WebHooksController : AppController
    {
        private readonly IEntityRepository<Package> _packageRepository;
        private readonly IEntityRepository<PackageRegistration> _packageRegistrationRepository;
        private readonly IIndexingService _indexingService;

        public WebHooksController(
            IEntityRepository<Package> packageRepository,
            IEntityRepository<PackageRegistration> packageRegistrationRepository,
            IIndexingService indexingService)
        {
            _packageRepository = packageRepository;
            _packageRegistrationRepository = packageRegistrationRepository;
            _indexingService = indexingService;
        }

        [HttpPost]
        public ActionResult ReindexPackages()
        {
            DeleteAll(_packageRepository);
            DeleteAll(_packageRegistrationRepository);
            _indexingService.UpdateIndex(forceRefresh: true);
            HttpContext.Cache.Remove("DefaultSearchResults");

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        private void DeleteAll<T>(IEntityRepository<T> repo) where T : class, IEntity, new()
        {
            var allEntities = repo.GetAll().ToList();

            foreach (var entity in allEntities)
            {
                repo.DeleteOnCommit(entity);
            }

            repo.CommitChanges();
        }
    }
}