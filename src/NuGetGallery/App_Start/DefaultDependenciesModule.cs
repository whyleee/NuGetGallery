// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Mail;
using System.Security.Principal;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using AnglicanGeek.MarkdownMailer;
using Autofac;
using Elmah;
using Microsoft.WindowsAzure.ServiceRuntime;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Infrastructure;
using NuGetGallery.Infrastructure.Lucene;
using NuGetGallery.RemoteFeed;

namespace NuGetGallery
{
    public class DefaultDependenciesModule : Module
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:CyclomaticComplexity", Justification = "This code is more maintainable in the same function.")]
        protected override void Load(ContainerBuilder builder)
        {
            var configuration = new ConfigurationService();
            builder.RegisterInstance(configuration)
                .AsSelf()
                .As<PoliteCaptcha.IConfigurationSource>();
            builder.Register(c => configuration.Current)
                .AsSelf()
                .As<IAppConfiguration>();

            builder.RegisterInstance(LuceneCommon.GetDirectory(configuration.Current.LuceneIndexLocation))
                .As<Lucene.Net.Store.Directory>()
                .SingleInstance();

            ConfigureSearch(builder, configuration);

            /*
            if (!string.IsNullOrEmpty(configuration.Current.AzureStorageConnectionString))
            {
                builder.RegisterInstance(new TableErrorLog(configuration.Current.AzureStorageConnectionString))
                    .As<ErrorLog>()
                    .SingleInstance();
            }
            else
            {
                builder.RegisterInstance(new SqlErrorLog(configuration.Current.SqlConnectionString))
                    .As<ErrorLog>()
                    .SingleInstance();
            }
            */
            builder.RegisterInstance(new MemoryErrorLog())
                .As<ErrorLog>()
                .SingleInstance();

            builder.RegisterType<HttpContextCacheService>()
                .AsSelf()
                .As<ICacheService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<ContentService>()
                .AsSelf()
                .As<IContentService>()
                .SingleInstance();

            //builder.Register(c => new EntitiesContext(configuration.Current.SqlConnectionString, readOnly: configuration.Current.ReadOnlyMode))
            builder.RegisterType<NullEntitiesContext>()
                .AsSelf()
                .As<IEntitiesContext>()
                .SingleInstance();

            builder.RegisterType<InMemoryRepository<User>>()
                .AsSelf()
                .As<IEntityRepository<User>>()
                .SingleInstance();

            builder.RegisterType<InMemoryRepository<CuratedFeed>>()
                .AsSelf()
                .As<IEntityRepository<CuratedFeed>>()
                .SingleInstance();

            builder.RegisterType<InMemoryRepository<CuratedPackage>>()
                .AsSelf()
                .As<IEntityRepository<CuratedPackage>>()
                .SingleInstance();

            builder.RegisterType<InMemoryRepository<PackageRegistration>>()
                .AsSelf()
                .As<IEntityRepository<PackageRegistration>>()
                .SingleInstance();

            builder.RegisterType<RemoteFeedPackageRepository>()
                .AsSelf()
                .As<IEntityRepository<Package>>()
                .SingleInstance();

            builder.RegisterType<InMemoryRepository<PackageDependency>>()
                .AsSelf()
                .As<IEntityRepository<PackageDependency>>()
                .SingleInstance();

            builder.RegisterType<InMemoryRepository<PackageStatistics>>()
                .AsSelf()
                .As<IEntityRepository<PackageStatistics>>()
                .SingleInstance();

            builder.RegisterType<InMemoryRepository<Credential>>()
                .AsSelf()
                .As<IEntityRepository<Credential>>()
                .SingleInstance();

            builder.RegisterType<InMemoryRepository<PackageOwnerRequest>>()
                .AsSelf()
                .As<IEntityRepository<PackageOwnerRequest>>()
                .SingleInstance();

            builder.RegisterType<CuratedFeedService>()
                .AsSelf()
                .As<ICuratedFeedService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<UserService>()
                .AsSelf()
                .As<IUserService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageService>()
                .AsSelf()
                .As<IPackageService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EditPackageService>()
                .AsSelf()
                .InstancePerLifetimeScope();

            builder.RegisterType<FormsAuthenticationService>()
                .As<IFormsAuthenticationService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<CookieTempDataProvider>()
                .As<ITempDataProvider>()
                .InstancePerLifetimeScope();

            builder.RegisterType<NuGetExeDownloaderService>()
                .AsSelf()
                .As<INuGetExeDownloaderService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<StatusService>()
                .AsSelf()
                .As<IStatusService>()
                .InstancePerLifetimeScope();
            
            var mailSenderThunk = new Lazy<IMailSender>(
                () =>
                {
                    var settings = configuration;
                    if (settings.Current.SmtpUri != null && settings.Current.SmtpUri.IsAbsoluteUri)
                    {
                        var smtpUri = new SmtpUri(settings.Current.SmtpUri);

                        var mailSenderConfiguration = new MailSenderConfiguration
                            {
                                DeliveryMethod = SmtpDeliveryMethod.Network,
                                Host = smtpUri.Host,
                                Port = smtpUri.Port,
                                EnableSsl = smtpUri.Secure
                            };

                        if (!string.IsNullOrWhiteSpace(smtpUri.UserName))
                        {
                            mailSenderConfiguration.UseDefaultCredentials = false;
                            mailSenderConfiguration.Credentials = new NetworkCredential(
                                smtpUri.UserName,
                                smtpUri.Password);
                        }

                        return new MailSender(mailSenderConfiguration);
                    }
                    else
                    {
                        var mailSenderConfiguration = new MailSenderConfiguration
                            {
                                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                                PickupDirectoryLocation = HostingEnvironment.MapPath("~/App_Data/Mail")
                            };

                        return new MailSender(mailSenderConfiguration);
                    }
                });



            builder.Register(c => mailSenderThunk.Value)
                .AsSelf()
                .As<IMailSender>()
                .InstancePerLifetimeScope();

            builder.RegisterType<MessageService>()
                .AsSelf()
                .As<IMessageService>()
                .InstancePerLifetimeScope();

            builder.Register(c => HttpContext.Current.User)
                .AsSelf()
                .As<IPrincipal>()
                .InstancePerLifetimeScope();
            
            switch (configuration.Current.StorageType)
            {
                case StorageType.FileSystem:
                case StorageType.NotSpecified:
                    ConfigureForLocalFileSystem(builder);
                    break;
                case StorageType.AzureStorage:
                    ConfigureForAzureStorage(builder, configuration);
                    break;
            }

            builder.RegisterType<FileSystemService>()
                .AsSelf()
                .As<IFileSystemService>()
                .SingleInstance();

            builder.RegisterType<PackageFileService>()
                .AsSelf()
                .As<IPackageFileService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<UploadFileService>()
                .AsSelf()
                .As<IUploadFileService>()
                .InstancePerLifetimeScope();

            // todo: bind all package curators by convention
            builder.RegisterType<WebMatrixPackageCurator>()
                .AsSelf()
                .As<IAutomaticPackageCurator>()
                .InstancePerLifetimeScope();

            builder.RegisterType<Windows8PackageCurator>()
                .AsSelf()
                .As<IAutomaticPackageCurator>()
                .InstancePerLifetimeScope();

            // todo: bind all commands by convention
            builder.RegisterType<AutomaticallyCuratePackageCommand>()
                .AsSelf()
                .As<IAutomaticallyCuratePackageCommand>()
                .InstancePerLifetimeScope();

            ConfigureAutocomplete(builder, configuration);

            builder.RegisterType<DiagnosticsService>()
                .AsSelf()
                .As<IDiagnosticsService>()
                .SingleInstance();
        }
        
        private static void ConfigureSearch(ContainerBuilder builder, ConfigurationService configuration)
        {
            if (configuration.Current.ServiceDiscoveryUri == null)
            {
                builder.RegisterType<LuceneSearchService>()
                    .AsSelf()
                    .As<ISearchService>()
                    .InstancePerLifetimeScope();
                builder.RegisterType<LuceneIndexingService>()
                    .AsSelf()
                    .As<IIndexingService>()
                    .InstancePerLifetimeScope();
            }
            else
            {
                builder.RegisterType<ExternalSearchService>()
                    .AsSelf()
                    .As<ISearchService>()
                    .As<IIndexingService>()
                    .InstancePerLifetimeScope();
            }
        }
        private static void ConfigureAutocomplete(ContainerBuilder builder, ConfigurationService configuration)
        {
            if (configuration.Current.ServiceDiscoveryUri != null &&
                !string.IsNullOrEmpty(configuration.Current.AutocompleteServiceResourceType))
            {
                builder.RegisterType<AutocompleteServicePackageIdsQuery>()
                    .AsSelf()
                    .As<IPackageIdsQuery>()
                    .SingleInstance();

                builder.RegisterType<AutocompleteServicePackageVersionsQuery>()
                    .AsSelf()
                    .As<IPackageVersionsQuery>()
                    .InstancePerLifetimeScope();
            }
            else
            {
                builder.RegisterType<PackageIdsQuery>()
                    .AsSelf()
                    .As<IPackageIdsQuery>()
                    .InstancePerLifetimeScope();

                builder.RegisterType<PackageVersionsQuery>()
                    .AsSelf()
                    .As<IPackageVersionsQuery>()
                    .InstancePerLifetimeScope();
            }
        }

        private static void ConfigureForLocalFileSystem(ContainerBuilder builder)
        {
            builder.RegisterType<FileSystemFileStorageService>()
                .AsSelf()
                .As<IFileStorageService>()
                .SingleInstance();

            builder.RegisterInstance(NullReportService.Instance)
                .AsSelf()
                .As<IReportService>()
                .SingleInstance();

            builder.RegisterInstance(NullStatisticsService.Instance)
                .AsSelf()
                .As<IStatisticsService>()
                .SingleInstance();

            builder.RegisterInstance(AuditingService.None)
                .AsSelf()
                .As<AuditingService>()
                .SingleInstance();

            // If we're not using azure storage, then aggregate stats comes from package repository
            builder.RegisterType<PackageRepositoryAggregateStatsService>()
                .AsSelf()
                .As<IAggregateStatsService>()
                .InstancePerLifetimeScope();
        }

        private static void ConfigureForAzureStorage(ContainerBuilder builder, ConfigurationService configuration)
        {
            builder.RegisterInstance(new CloudBlobClientWrapper(configuration.Current.AzureStorageConnectionString, configuration.Current.AzureStorageReadAccessGeoRedundant))
                .AsSelf()
                .As<ICloudBlobClient>()
                .SingleInstance();

            builder.RegisterType<CloudBlobFileStorageService>()
                .AsSelf()
                .As<IFileStorageService>()
                .SingleInstance();
            
            // when running on Windows Azure, we use a back-end job to calculate stats totals and store in the blobs
            builder.RegisterInstance(new JsonAggregateStatsService(configuration.Current.AzureStorageConnectionString, configuration.Current.AzureStorageReadAccessGeoRedundant))
                .AsSelf()
                .As<IAggregateStatsService>()
                .SingleInstance();

            // when running on Windows Azure, pull the statistics from the warehouse via storage
            builder.RegisterInstance(new CloudReportService(configuration.Current.AzureStorageConnectionString, configuration.Current.AzureStorageReadAccessGeoRedundant))
                .AsSelf()
                .As<IReportService>()
                .SingleInstance();

            // when running on Windows Azure, download counts come from the downloads.v1.json blob
            var downloadCountService = new CloudDownloadCountService(configuration.Current.AzureStorageConnectionString, configuration.Current.AzureStorageReadAccessGeoRedundant);
            builder.RegisterInstance(downloadCountService)
                .AsSelf()
                .As<IDownloadCountService>()
                .SingleInstance();
            EntityInterception.AddInterceptor(new DownloadCountEntityInterceptor(downloadCountService));

            builder.RegisterType<JsonStatisticsService>()
                .AsSelf()
                .As<IStatisticsService>()
                .SingleInstance();

            string instanceId;
            try
            {
                instanceId = RoleEnvironment.CurrentRoleInstance.Id;
            }
            catch
            {
                instanceId = Environment.MachineName;
            }

            var localIp = AuditActor.GetLocalIP().Result;

            builder.RegisterInstance(new CloudAuditingService(instanceId, localIp, configuration.Current.AzureStorageConnectionString, CloudAuditingService.AspNetActorThunk))
                .AsSelf()
                .As<AuditingService>()
                .SingleInstance();
        }
    }
}
