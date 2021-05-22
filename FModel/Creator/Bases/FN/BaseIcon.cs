﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.GameplayTags;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse_Conversion.Textures;
using CUE4Parse_Fortnite.Enums;
using FModel.Settings;
using SkiaSharp;

namespace FModel.Creator.Bases.FN
{
    public class BaseIcon : UCreator
    {
        public SKBitmap SeriesBackground { get; protected set; }
        protected string ShortDescription { get; set; }
        protected string CosmeticSource { get; set; }
        protected SKBitmap[] UserFacingFlags { get; set; }

        public BaseIcon(UObject uObject, EIconStyle style) : base(uObject, style)
        {
        }

        public void ParseForReward(bool isUsingDisplayAsset)
        {
            // rarity
            if (Object.TryGetValue(out FPackageIndex series, "Series")) GetSeries(series);
            else GetRarity(Object.GetOrDefault<FName>("Rarity")); // default is uncommon

            // preview
            if (isUsingDisplayAsset && Utils.TryGetDisplayAsset(Object, out var preview))
                Preview = preview;
            else if (Object.TryGetValue(out FPackageIndex itemDefinition, "HeroDefinition", "WeaponDefinition"))
                Preview = Utils.GetBitmap(itemDefinition);
            else if (Object.TryGetValue(out FSoftObjectPath largePreview, "LargePreviewImage", "SidePanelIcon", "EntryListIcon", "SmallPreviewImage", "ItemDisplayAsset", "LargeIcon", "ToastIcon", "SmallIcon"))
                Preview = Utils.GetBitmap(largePreview);
            else if (Object.TryGetValue(out string s, "LargePreviewImage") && !string.IsNullOrEmpty(s))
                Preview = Utils.GetBitmap(s);
            else if (Object.TryGetValue(out FPackageIndex otherPreview, "SmallPreviewImage", "ToastIcon", "access_item"))
                Preview = Utils.GetBitmap(otherPreview);

            // text
            if (Object.TryGetValue(out FText displayName, "DisplayName", "DefaultHeaderText", "UIDisplayName", "EntryName"))
                DisplayName = displayName.Text;
            if (Object.TryGetValue(out FText description, "Description", "GeneralDescription", "DefaultBodyText", "UIDescription", "UIDisplayDescription", "EntryDescription"))
                Description = description.Text;
            else if (Object.TryGetValue(out FText[] descriptions, "Description"))
                Description = string.Join('\n', descriptions.Select(x => x.Text));
            if (Object.TryGetValue(out FText shortDescription, "ShortDescription", "UIDisplaySubName"))
                ShortDescription = shortDescription.Text;
            else if (Object.ExportType.Equals("AthenaItemWrapDefinition", StringComparison.OrdinalIgnoreCase))
                ShortDescription = "Wrap";

            Description = Utils.RemoveHtmlTags(Description);
        }

        public override void ParseForInfo()
        {
            ParseForReward(UserSettings.Default.CosmeticDisplayAsset == EEnabledDisabled.Enabled);

            if (Object.TryGetValue(out FGameplayTagContainer gameplayTags, "GameplayTags"))
                CheckGameplayTags(gameplayTags);
            if (Object.TryGetValue(out FPackageIndex cosmeticItem, "cosmetic_item"))
                CosmeticSource = cosmeticItem.Name;
        }

        protected void Draw(SKCanvas c)
        {
            switch (Style)
            {
                case EIconStyle.NoBackground:
                    DrawPreview(c);
                    break;
                case EIconStyle.NoText:
                    DrawBackground(c);
                    DrawPreview(c);
                    DrawUserFacingFlags(c);
                    break;
                default:
                    DrawBackground(c);
                    DrawPreview(c);
                    DrawTextBackground(c);
                    DrawDisplayName(c);
                    DrawDescription(c);
                    DrawToBottom(c, SKTextAlign.Right, CosmeticSource);
                    if (Description != ShortDescription)
                        DrawToBottom(c, SKTextAlign.Left, ShortDescription);
                    DrawUserFacingFlags(c);
                    break;
            }
        }

        public override SKImage Draw()
        {
            using var ret = new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var c = new SKCanvas(ret);

            Draw(c);

            return SKImage.FromBitmap(ret);
        }

        private void GetSeries(FPackageIndex s)
        {
            if (!Utils.TryGetPackageIndexExport(s, out UObject export)) return;

            GetSeries(export);
        }

        protected void GetSeries(UObject uObject)
        {
            if (uObject is UTexture2D texture2D)
            {
                SeriesBackground = SKBitmap.Decode(texture2D.Decode()?.Encode());
                return;
            }

            if (uObject.TryGetValue(out FSoftObjectPath backgroundTexture, "BackgroundTexture"))
            {
                SeriesBackground = Utils.GetBitmap(backgroundTexture);
            }

            if (uObject.TryGetValue(out FStructFallback colors, "Colors") &&
                colors.TryGetValue(out FLinearColor color1, "Color1") &&
                colors.TryGetValue(out FLinearColor color2, "Color2") &&
                colors.TryGetValue(out FLinearColor color3, "Color3"))
            {
                Background = new[] {SKColor.Parse(color1.Hex), SKColor.Parse(color3.Hex)};
                Border = new[] {SKColor.Parse(color2.Hex), SKColor.Parse(color1.Hex)};
            }

            if (uObject.Name.Equals("PlatformSeries") &&
                uObject.TryGetValue(out FSoftObjectPath itemCardMaterial, "ItemCardMaterial") &&
                Utils.TryLoadObject(itemCardMaterial.AssetPathName.Text, out UMaterialInstanceConstant material))
            {
                foreach (var vectorParameter in material.VectorParameterValues)
                {
                    if (vectorParameter.ParameterValue == null || !vectorParameter.ParameterInfo.Name.Text.Equals("ColorCircuitBackground"))
                        continue;

                    Background[0] = SKColor.Parse(vectorParameter.ParameterValue.Value.Hex);
                }
            }
        }

        private void GetRarity(FName r)
        {
            if (!Utils.TryLoadObject("FortniteGame/Content/Balance/RarityData.RarityData", out UObject export)) return;

            var rarity = EFortRarity.Uncommon;
            switch (r.Text)
            {
                case "EFortRarity::Common":
                case "EFortRarity::Handmade":
                    rarity = EFortRarity.Common;
                    break;
                case "EFortRarity::Rare":
                case "EFortRarity::Sturdy":
                    rarity = EFortRarity.Rare;
                    break;
                case "EFortRarity::Epic":
                case "EFortRarity::Quality":
                    rarity = EFortRarity.Epic;
                    break;
                case "EFortRarity::Legendary":
                case "EFortRarity::Fine":
                    rarity = EFortRarity.Legendary;
                    break;
                case "EFortRarity::Mythic":
                case "EFortRarity::Elegant":
                    rarity = EFortRarity.Mythic;
                    break;
                case "EFortRarity::Transcendent":
                case "EFortRarity::Masterwork":
                    rarity = EFortRarity.Transcendent;
                    break;
                case "EFortRarity::Unattainable":
                case "EFortRarity::Badass":
                    rarity = EFortRarity.Unattainable;
                    break;
            }

            if (export.GetByIndex<FStructFallback>((int) rarity) is { } data &&
                data.TryGetValue(out FLinearColor color1, "Color1") &&
                data.TryGetValue(out FLinearColor color2, "Color2") &&
                data.TryGetValue(out FLinearColor color3, "Color3"))
            {
                Background = new[] {SKColor.Parse(color1.Hex), SKColor.Parse(color3.Hex)};
                Border = new[] {SKColor.Parse(color2.Hex), SKColor.Parse(color1.Hex)};
            }
        }

        protected string GetCosmeticSet(string setName)
        {
            if (!Utils.TryLoadObject("FortniteGame/Content/Athena/Items/Cosmetics/Metadata/CosmeticSets.CosmeticSets", out UDataTable cosmeticSets))
                return string.Empty;

            if (!cosmeticSets.TryGetDataTableRow(setName, StringComparison.OrdinalIgnoreCase, out var uObject))
                return string.Empty;

            var name = string.Empty;
            if (uObject.TryGetValue(out FText displayName, "DisplayName"))
                name = displayName.Text;

            var format = Utils.GetLocalizedResource("Fort.Cosmetics", "CosmeticItemDescription_SetMembership_NotRich", "\nPart of the {0} set.");
            return string.Format(format, name);
        }

        protected string GetCosmeticSeason(string seasonNumber)
        {
            var s = seasonNumber["Cosmetics.Filter.Season.".Length..];
            var number = int.Parse(s);
            if (number == 10)
                s = "X";

            var season = Utils.GetLocalizedResource("AthenaSeasonItemDefinitionInternal", "SeasonTextFormat", "Season {0}");
            var introduced = Utils.GetLocalizedResource("Fort.Cosmetics", "CosmeticItemDescription_Season", "\nIntroduced in <SeasonText>{0}</>.");
            if (number <= 10) return Utils.RemoveHtmlTags(string.Format(introduced, string.Format(season, s)));

            var chapter = Utils.GetLocalizedResource("AthenaSeasonItemDefinitionInternal", "ChapterTextFormat", "Chapter {0}");
            var chapterFormat = Utils.GetLocalizedResource("AthenaSeasonItemDefinitionInternal", "ChapterSeasonTextFormat", "{0}, {1}");
            var d = string.Format(chapterFormat, string.Format(chapter, number / 10 + 1), string.Format(season, s[^1..]));
            return Utils.RemoveHtmlTags(string.Format(introduced, d));
        }

        private void CheckGameplayTags(FGameplayTagContainer gameplayTags)
        {
            if (gameplayTags.TryGetGameplayTag("Cosmetics.Source.", out var source))
                CosmeticSource = source.Text["Cosmetics.Source.".Length..];
            else if (gameplayTags.TryGetGameplayTag("Athena.ItemAction.", out var action))
                CosmeticSource = action.Text["Athena.ItemAction.".Length..];

            if (gameplayTags.TryGetGameplayTag("Cosmetics.Set.", out var set))
                Description += GetCosmeticSet(set.Text);
            if (gameplayTags.TryGetGameplayTag("Cosmetics.Filter.Season.", out var season))
                Description += GetCosmeticSeason(season.Text);

            GetUserFacingFlags(gameplayTags.GetAllGameplayTags(
                "Cosmetics.UserFacingFlags.", "Homebase.Class.", "NPC.CharacterType.Survivor.Defender."));
        }

        protected void GetUserFacingFlags(IList<string> userFacingFlags)
        {
            if (userFacingFlags.Count < 1 || !Utils.TryLoadObject("FortniteGame/Content/Items/ItemCategories.ItemCategories", out UObject itemCategories))
                return;

            if (!itemCategories.TryGetValue(out FStructFallback[] tertiaryCategories, "TertiaryCategories"))
                return;

            UserFacingFlags = new SKBitmap[userFacingFlags.Count];
            for (var i = 0; i < UserFacingFlags.Length; i++)
            {
                if (userFacingFlags[i].Equals("Cosmetics.UserFacingFlags.HasUpgradeQuests", StringComparison.OrdinalIgnoreCase))
                {
                    if (Object.ExportType.Equals("AthenaPetCarrierItemDefinition", StringComparison.OrdinalIgnoreCase))
                        UserFacingFlags[i] = SKBitmap.Decode(Application.GetResourceStream(new Uri("pack://application:,,,/Resources/T-Icon-Pets-64.png"))?.Stream);
                    else UserFacingFlags[i] = SKBitmap.Decode(Application.GetResourceStream(new Uri("pack://application:,,,/Resources/T-Icon-Quests-64.png"))?.Stream);
                }
                else
                {
                    foreach (var category in tertiaryCategories)
                    {
                        if (category.TryGetValue(out FGameplayTagContainer tagContainer, "TagContainer") && tagContainer.TryGetGameplayTag(userFacingFlags[i], out _) &&
                            category.TryGetValue(out FStructFallback categoryBrush, "CategoryBrush") && categoryBrush.TryGetValue(out FStructFallback brushXxs, "Brush_XXS") &&
                            brushXxs.TryGetValue(out FPackageIndex resourceObject, "ResourceObject") && Utils.TryGetPackageIndexExport(resourceObject, out UTexture2D texture))
                        {
                            UserFacingFlags[i] = Utils.GetBitmap(texture);
                        }
                    }
                }
            }
        }

        private void DrawUserFacingFlags(SKCanvas c)
        {
            if (UserFacingFlags == null) return;

            const int size = 25;
            var x = Margin * (int) 2.5;
            foreach (var flag in UserFacingFlags)
            {
                if (flag == null) continue;

                c.DrawBitmap(flag.Resize(size), new SKPoint(x, Margin * (int) 2.5), ImagePaint);
                x += size;
            }
        }
    }
}