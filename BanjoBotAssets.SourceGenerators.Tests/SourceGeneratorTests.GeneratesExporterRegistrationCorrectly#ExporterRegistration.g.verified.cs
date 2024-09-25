﻿//HintName: ExporterRegistration.g.cs
namespace BanjoBotAssets.Extensions
{
    using Microsoft.Extensions.DependencyInjection;
    internal static partial class ServiceCollectionExtensions
    {

        /// <summary>
        /// Registers all non-abstract classes that derive from <see cref="global::BanjoBotAssets.Exporters.BaseExporter"/>
        /// as implementations of <see cref="global::BanjoBotAssets.Exporters.IExporter"/> with the specified service lifetime.
        /// </summary>
        /// <remarks>
        /// This method is automatically generated by <see cref="ExporterRegistrationGenerator"/>.
        /// </remarks>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
        /// <param name="lifetime">The <see cref="ServiceLifetime"/> to register the services with.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddExporterServices(this IServiceCollection services, ServiceLifetime lifetime)
        {
            services.Add(new ServiceDescriptor(typeof(global::BanjoBotAssets.Exporters.IExporter), typeof(global::BanjoBotAssets.Exporters.UObjects.TestExporter), lifetime));
            return services;
        }
    }
}
