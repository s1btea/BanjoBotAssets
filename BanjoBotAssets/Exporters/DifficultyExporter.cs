﻿namespace BanjoBotAssets.Exporters
{
    internal sealed class DifficultyExporter : BaseExporter
    {
        public DifficultyExporter(DefaultFileProvider provider) : base(provider) { }

        public override async Task ExportAssetsAsync(IProgress<ExportProgress> progress, IAssetOutput output)
        {
            var growthBoundsPath = assetPaths.First(p => Path.GetFileNameWithoutExtension(p) == "GameDifficultyGrowthBounds");

            if (growthBoundsPath == null)
            {
                Console.WriteLine(Resources.Warning_SpecificAssetNotFound, "GameDifficultyGrowthBounds");
                return;
            }

            var file = provider[growthBoundsPath];

            Interlocked.Increment(ref assetsLoaded);
            var dataTable = await provider.LoadObjectAsync<UDataTable>(file.PathWithoutExtension);

            if (dataTable == null)
            {
                Console.WriteLine(Resources.Warning_CouldNotLoadAsset, growthBoundsPath);
                return;
            }

            foreach (var (rowKey, data) in dataTable.RowMap)
            {
                var requiredRating = data.GetOrDefault<int>("RequiredRating");
                var maximumRating = data.GetOrDefault<int>("MaximumRating");
                var recommendedRating = data.GetOrDefault<int>("RecommendedRating");
                var displayName = data.GetOrDefault<FText>("ThreatDisplayName")?.Text ?? $"<{recommendedRating}>";

                output.AddDifficultyInfo(rowKey.Text, new DifficultyInfo
                {
                    RequiredRating = requiredRating,
                    MaximumRating = maximumRating,
                    RecommendedRating = recommendedRating,
                    DisplayName = displayName.Trim(),
                });
            }

        }

        protected override bool InterestedInAsset(string name) => name.EndsWith("GameDifficultyGrowthBounds.uasset", StringComparison.OrdinalIgnoreCase);
    }
}
