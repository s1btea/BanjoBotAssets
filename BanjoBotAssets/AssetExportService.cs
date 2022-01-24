﻿using BanjoBotAssets.Exporters;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using CUE4Parse.FileProvider;

namespace BanjoBotAssets
{
    internal sealed class AssetExportService : IHostedService
    {
        private readonly ILogger<AssetExportService> logger;
        private readonly IHostApplicationLifetime lifetime;
        private readonly IEnumerable<IExporter> exporters;
        private readonly IOptions<GameFileOptions> options;

        public AssetExportService(ILogger<AssetExportService> logger,
            IHostApplicationLifetime lifetime,
            IEnumerable<IExporter> exporters,
            IOptions<GameFileOptions> options)
        {
            this.logger = logger;
            this.lifetime = lifetime;
            this.exporters = exporters;
            this.options = options;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Environment.ExitCode = await RunAsync(cancellationToken);
            lifetime.StopApplication();
        }

        private async Task<int> RunAsync(CancellationToken cancellationToken)
        {
            // open PAK files
            var gameDirectories = options.Value.GameDirectories.Cast<string>();

            var gameDirectory = gameDirectories.FirstOrDefault(d => Directory.Exists(d));

            if (gameDirectory == null)
            {
                logger.LogError(Resources.Error_GameNotFound);
                return 1;
            }

            var provider = new DefaultFileProvider(
                gameDirectory,
                SearchOption.TopDirectoryOnly,
                false,
                new VersionContainer(EGame.GAME_UE5_LATEST));
            provider.Initialize();

            // get encryption keys from fortnite-api.com
            AesApiResponse? aes;

            using (var client = new HttpClient())
            {
                aes = await client.GetFromJsonAsync<AesApiResponse>(Settings.Default.AesApiUri, cancellationToken);
            }

            if (aes == null)
            {
                logger.LogError(Resources.Error_AesFetchFailed);
                return 2;
            }

            logger.LogInformation(Resources.Status_SubmittingMainKey);
            provider.SubmitKey(new FGuid(), new FAesKey(aes.Data.MainKey));

            foreach (var dk in aes.Data.DynamicKeys)
            {
                logger.LogInformation(Resources.Status_SubmittingDynamicKey, dk.PakFilename);
                provider.SubmitKey(new FGuid(dk.PakGuid), new FAesKey(dk.Key));
            }

            logger.LogInformation(Resources.Status_LoadingMappings);
            provider.LoadMappings();

            // sometimes the mappings don't load, and then nothing works
            if (provider.MappingsForThisGame is null or { Enums.Count: 0, Types.Count: 0 })
            {
                await Task.Delay(5 * 1000, cancellationToken);
                logger.LogWarning(Resources.Status_RetryingMappings);
                provider.LoadMappings();

                if (provider.MappingsForThisGame is null or { Enums.Count: 0, Types.Count: 0 })
                {
                    logger.LogError(Resources.Error_MappingsFetchFailed);
                    return 3;
                }
            }

            logger.LogInformation(Resources.Status_LoadingLocalization);
            provider.LoadLocalization(GetLocalizationLanguage(), cancellationToken);

            logger.LogInformation(Resources.Status_RegisteringExportTypes);
            ObjectTypeRegistry.RegisterEngine(typeof(UFortItemDefinition).Assembly);
            ObjectTypeRegistry.RegisterClass("FortDefenderItemDefinition", typeof(UFortHeroType));
            ObjectTypeRegistry.RegisterClass("FortTrapItemDefinition", typeof(UFortItemDefinition));
            ObjectTypeRegistry.RegisterClass("FortAlterationItemDefinition", typeof(UFortItemDefinition));
            ObjectTypeRegistry.RegisterClass("FortResourceItemDefinition", typeof(UFortWorldItemDefinition));
            ObjectTypeRegistry.RegisterClass("FortGameplayModifierItemDefinition", typeof(UFortItemDefinition));

            var exportedAssets = new ExportedAssets();
            var exportedRecipes = new List<ExportedRecipe>();
            //using var logFile = new StreamWriter("assets.log");

            // find interesting assets
            foreach (var (name, file) in provider.Files)
            {
                if (name.Contains("/Athena/"))
                {
                    continue;
                }

                foreach (var e in exporters)
                {
                    e.ObserveAsset(name);
                }
            }

            // log all asset names found to file
            //foreach (var (name, assets) in allAssetLists)
            //{
            //    foreach (var asset in assets)
            //    {
            //        logFile.WriteLine("{0}: {1}", name, asset);
            //    }
            //}

            var progress = new Progress<ExportProgress>(_ =>
            {
                // TODO: do something with progress reports
            });

            var assetsLoaded = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var allPrivateExports = new List<IAssetOutput>();

            await Task.WhenAll(exporters.Select(e =>
            {
                // give the exporter its own output object to use,
                // then combine the results when the task completes
                var privateExport = new AssetOutput();
                allPrivateExports.Add(privateExport);
                return e.ExportAssetsAsync(progress, privateExport, cancellationToken);
            }));

            stopwatch.Stop();

            assetsLoaded = exporters.Sum(e => e.AssetsLoaded);

            logger.LogInformation(Resources.Status_LoadedAssets, assetsLoaded, stopwatch.Elapsed, stopwatch.ElapsedMilliseconds / Math.Max(assetsLoaded, 1));

            // collect exported assets
            foreach (var privateExport in allPrivateExports)
            {
                privateExport.CopyTo(exportedAssets, exportedRecipes, cancellationToken);
            }

            allPrivateExports.Clear();

            // send to finalizers
            


            // done!
            return 0;

            static ELanguage GetLocalizationLanguage()
            {
                if (!string.IsNullOrEmpty(Settings.Default.ELanguage) && Enum.TryParse<ELanguage>(Settings.Default.ELanguage, out var result))
                    return result;

                return Enum.Parse<ELanguage>(Resources.ELanguage);
            }

        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}