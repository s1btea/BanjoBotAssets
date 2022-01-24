﻿using BanjoBotAssets.Extensions;
using BanjoBotAssets.Models;
using CUE4Parse.UE4.Objects.Engine;

namespace BanjoBotAssets.Exporters.Impl
{
    internal abstract class BlueprintExporter : BaseExporter
    {
        private int numToProcess;
        private int processedSoFar;

        protected BlueprintExporter(IExporterContext services) : base(services)
        {
        }

        protected abstract string Type { get; }
        protected abstract string DisplayNameProperty { get; }
        protected abstract string? DescriptionProperty { get; }

        protected virtual Task<bool> ExportAssetAsync(UBlueprintGeneratedClass bpClass, UObject classDefaultObject, NamedItemData namedItemData)
        {
            return Task.FromResult(true);
        }

        public override Task ExportAssetsAsync(IProgress<ExportProgress> progress, IAssetOutput output, CancellationToken cancellationToken)
        {
            numToProcess = assetPaths.Count;

            return Parallel.ForEachAsync(assetPaths, async (path, _) =>
            {
                var file = provider![path];

                var num = Interlocked.Increment(ref processedSoFar);
                logger.LogInformation(Resources.Status_ProcessingTypeNumOfNum, Type, num, numToProcess);

                //logger.LogInformation("Loading {0}", file.PathWithoutExtension);
                Interlocked.Increment(ref assetsLoaded);
                var pkg = await provider.LoadPackageAsync(file.PathWithoutExtension);

                if (pkg.GetExports().First() is not UBlueprintGeneratedClass bpClass)
                {
                    logger.LogWarning(Resources.Warning_FailedToLoadFile, file.PathWithoutExtension);
                    return;
                }

                var bpClassPath = bpClass.GetPathName();

                Interlocked.Increment(ref assetsLoaded);
                var cdo = await bpClass.ClassDefaultObject.LoadAsync();

                var displayName = (await cdo.GetInheritedOrDefaultAsync<FText>(DisplayNameProperty, this))?.Text ?? $"<{bpClass.Name}>";
                var description = DescriptionProperty == null ? null : (await cdo.GetInheritedOrDefaultAsync<FText>(DescriptionProperty, this))?.Text;

                var namedItemData = new NamedItemData
                {
                    AssetPath = file.PathWithoutExtension,
                    Description = description,
                    DisplayName = displayName.Trim(),
                    Name = bpClass.Name,
                    Type = Type,
                };

                if (!await ExportAssetAsync(bpClass, cdo, namedItemData))
                {
                    return;
                }

                output.AddNamedItem(bpClassPath, namedItemData);
            });
        }
    }
}