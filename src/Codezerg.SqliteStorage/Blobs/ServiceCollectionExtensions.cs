using System;
using Codezerg.SqliteStorage.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Codezerg.SqliteStorage.Blobs
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add ContentStore with default configuration.
        /// Uses FileChunkStorage in "./chunks".
        /// </summary>
        public static IServiceCollection AddContentStore(
            this IServiceCollection services,
            Action<ContentStoreOptions> configure)
        {
            var options = new ContentStoreOptions();
            configure(options);
            options.Validate();

            services.AddSingleton(Options.Create(options));
            services.AddSingleton<IContentStore, SqliteContentStore>();

            return services;
        }

        /// <summary>
        /// Add ContentStore with custom chunk storage.
        /// </summary>
        public static IServiceCollection AddContentStore(
            this IServiceCollection services,
            IChunkStorage chunkStorage,
            Action<ContentStoreOptions>? configure = null)
        {
            var options = new ContentStoreOptions
            {
                ChunkStorage = chunkStorage
            };

            configure?.Invoke(options);
            options.Validate();

            services.AddSingleton(Options.Create(options));
            services.AddSingleton<IContentStore, SqliteContentStore>();

            return services;
        }
    }
}
