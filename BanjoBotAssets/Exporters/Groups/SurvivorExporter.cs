﻿using BanjoBotAssets.Artifacts.Models;

// TODO: fix Halloween survivors all getting the same name: they should be separated by rarity (WorkerHalloween_VR_T04 is Lobber, WorkerHalloween_UC_T01 is Husky, etc.)

namespace BanjoBotAssets.Exporters.Groups
{
    internal record SurvivorItemGroupFields(string DisplayName, string? Description, string? SubType,
        string? Personality) : BaseItemGroupFields(DisplayName, Description, SubType)
    {
        public SurvivorItemGroupFields() : this("", null, null, null) { }
    }

    internal sealed class SurvivorExporter : GroupExporter<UFortWorkerType, BaseParsedItemName, SurvivorItemGroupFields, SurvivorItemData>
    {
        protected override string Type => "Worker";

        protected override bool InterestedInAsset(string name) =>
            name.Contains("Workers/Worker", StringComparison.OrdinalIgnoreCase) || name.Contains("Managers/Manager", StringComparison.OrdinalIgnoreCase);

        // regular survivor:    WorkerBasic_SR_T02
        // special survivor:    Worker_Leprechaun_VR_T01, WorkerHalloween_VR_T04
        // mythic survivor:     Worker_Karolina_UR_T02
        // lead:                ManagerEngineer_R_T04
        // mythic lead:         ManagerMartialArtist_SR_samurai_T03
        private static readonly Regex survivorAssetNameRegex = new(@".*/([^/]+)_(C|UC|R|VR|SR|UR)_([a-z]+_)?T(\d+)(?:\..*)?$", RegexOptions.IgnoreCase);

        public SurvivorExporter(IExporterContext services) : base(services) { }

        protected override BaseParsedItemName? ParseAssetName(string name)
        {
            var match = survivorAssetNameRegex.Match(name);

            if (!match.Success)
            {
                logger.LogWarning(Resources.Warning_CannotParseSurvivorName, name);
                return null;
            }

            return new BaseParsedItemName(BaseName: match.Groups[1].Value + match.Groups[3].Value, Rarity: match.Groups[2].Value, Tier: int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture));
        }

        protected override async Task<SurvivorItemGroupFields> ExtractCommonFieldsAsync(UFortWorkerType asset, IGrouping<string?, string> grouping)
        {
            var result = await base.ExtractCommonFieldsAsync(asset, grouping);
            var subType = asset.bIsManager ? GetManagerJob(asset) : null;
            var displayName = asset.DisplayName?.Text ?? MakeSurvivorDisplayName(asset);
            var personality = asset.FixedPersonalityTag.GameplayTags is { Length: 1 }
                ? asset.FixedPersonalityTag.GameplayTags[0].Text.Split('.')[^1]
                : null;
            return result with { SubType = subType, DisplayName = displayName, Personality = personality };
        }

        protected override Task<bool> ExportAssetAsync(BaseParsedItemName parsed, UFortWorkerType primaryAsset, SurvivorItemGroupFields fields, string path, SurvivorItemData itemData)
        {
            itemData.Personality = fields.Personality;
            return Task.FromResult(true);
        }

        protected override EFortRarity GetRarity(BaseParsedItemName parsedName, UFortWorkerType primaryAsset, SurvivorItemGroupFields fields)
        {
            // managers' rarity is displayed as one level above what the asset name would imply
            // i.e. a mythic lead survivor is actually "SR", not "UR"
            var result = base.GetRarity(parsedName, primaryAsset, fields);
            return primaryAsset.bIsManager ? result + 1 : result;
        }

        private static readonly IReadOnlyDictionary<string, string> managerSynergyToJob = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
                { "Homebase.Manager.IsDoctor", Resources.Field_Survivor_Doctor },
                { "Homebase.Manager.IsEngineer", Resources.Field_Survivor_Engineer },
                { "Homebase.Manager.IsExplorer", Resources.Field_Survivor_Explorer },
                { "Homebase.Manager.IsGadgeteer", Resources.Field_Survivor_Gadgeteer },
                { "Homebase.Manager.IsInventor", Resources.Field_Survivor_Inventor },
                { "Homebase.Manager.IsMartialArtist", Resources.Field_Survivor_MartialArtist },
                { "Homebase.Manager.IsSoldier", Resources.Field_Survivor_Marksman },
                { "Homebase.Manager.IsTrainer", Resources.Field_Survivor_Trainer },
        };

        private static string GetManagerJob(UFortWorkerType worker)
        {
            if (!worker.bIsManager)
                throw new AssetFormatException(Resources.Error_NotAManager);

            string synergyTag = worker.ManagerSynergyTag.First().Text;
            if (managerSynergyToJob.TryGetValue(synergyTag, out var job))
                return job;

            throw new AssetFormatException(string.Format(CultureInfo.CurrentCulture, Resources.FormatString_Error_UnexpectedManagerSynergy, synergyTag));
        }

        private static string MakeSurvivorDisplayName(UFortWorkerType worker) =>
            worker.bIsManager ? string.Format(CultureInfo.CurrentCulture, Resources.FormatString_Field_Survivor_LeadNameFormat, GetManagerJob(worker)) : Resources.Field_Survivor_DefaultName;
    }
}
