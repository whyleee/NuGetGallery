using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace NuGetGallery.RemoteFeed
{
    public class NullEntitiesContext : IEntitiesContext
    {
        public IDbSet<CuratedFeed> CuratedFeeds { get; set; }
        public IDbSet<CuratedPackage> CuratedPackages { get; set; }
        public IDbSet<PackageRegistration> PackageRegistrations { get; set; }
        public IDbSet<Credential> Credentials { get; set; }
        public IDbSet<User> Users { get; set; }

        public int SaveChanges()
        {
            return 1;
        }

        public IDbSet<T> Set<T>() where T : class
        {
            return new NullDbSet<T>();
        }

        public void DeleteOnCommit<T>(T entity) where T : class
        {
            throw new NotImplementedException();
        }

        public void SetCommandTimeout(int? seconds)
        {
            throw new NotImplementedException();
        }
    }
}