using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Helpers;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {

        private async void OnCreateVoicePackClick(object sender, RoutedEventArgs e)
        {
            if (UsesGenericAudioEditor)
            {
                await ShowCreateGenericAudioPackDialogAsync(SettingsGameStyle, ActiveAudioPackKind);
                return;
            }
            if (SettingsGameStyle == GameStyleMode.CustomModule)
            {
                await ShowCreateCustomModuleVoicePackDialogAsync();
            }
            else if (SettingsGameStyle == GameStyleMode.Dagoujiao)
            {
                await ShowCreateDagoujiaoVoicePackDialogAsync();
            }
            else if (SettingsGameStyle == GameStyleMode.Doubao)
            {
                await ShowCreateDoubaoVoicePackDialogAsync();
            }
            else if (SettingsGameStyle == GameStyleMode.Csol)
            {
                await ShowCreateCsolVoicePackDialogAsync();
            }
            else if (SettingsGameStyle == GameStyleMode.Valorant)
            {
                await ShowCreateValorantVoicePackDialogAsync();
            }
            else if (SettingsGameStyle == GameStyleMode.Overwatch)
            {
                await ShowCreateOverwatchVoicePackDialogAsync();
            }
            else if (SettingsGameStyle == GameStyleMode.ModernWarfare2019)
            {
                await ShowCreateModernWarfare2019VoicePackDialogAsync();
            }
            else if (IsEventVoiceGame(SettingsGameStyle))
            {
                await ShowCreateEventVoicePackDialogAsync(SettingsGameStyle);
            }
            else
            {
                await ShowCreateVoicePackDialogAsync();
            }
        }

        private async void OnImportVoiceZipClick(object sender, RoutedEventArgs e)
        {
            await PickAndImportPackFilesAsync(isVoice: true, multiple: false);
        }

        private async Task ImportVoiceZipForCurrentStyleAsync()
        {
            if (await TryImportTypedAudioZipAsync()) return;
            if (SettingsGameStyle == GameStyleMode.Valorant)
            {
                await ImportValorantPackageFromZipAsync(ValorantExternalAssetService.VoicePackageKind);
            }
            else if (SettingsGameStyle == GameStyleMode.CustomModule)
            {
                await ImportPackFromZipAsync(
                    CustomModuleVoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateCustomModuleVoicePackDialogAsync(
                            folder.DisplayName,
                            await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.CustomModuleVoiceSlotMapping),
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (SettingsGameStyle == GameStyleMode.Dagoujiao)
            {
                await ImportPackFromZipAsync(
                    DagoujiaoVoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateDagoujiaoVoicePackDialogAsync(
                            folder.DisplayName,
                            await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.DagoujiaoSlotMapping),
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (SettingsGameStyle == GameStyleMode.Doubao)
            {
                await ImportPackFromZipAsync(
                    DoubaoVoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateDoubaoVoicePackDialogAsync(
                            folder.DisplayName,
                            await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.DoubaoSlotMapping));
                    });
            }
            else if (SettingsGameStyle == GameStyleMode.Csol)
            {
                await ImportPackFromZipAsync(
                    CsolVoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateCsolVoicePackDialogAsync(
                            folder.DisplayName,
                            await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.CsolSlotMapping),
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (SettingsGameStyle == GameStyleMode.Overwatch)
            {
                await ImportPackFromZipAsync(
                    OverwatchVoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateOverwatchVoicePackDialogAsync(
                            folder.DisplayName,
                            await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.OverwatchVoiceSlotMapping),
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (SettingsGameStyle == GameStyleMode.ModernWarfare2019)
            {
                await ImportPackFromZipAsync(
                    ModernWarfare2019VoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateModernWarfare2019VoicePackDialogAsync(
                            folder.DisplayName,
                            await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.ModernWarfare2019VoiceSlotMapping),
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (IsEventVoiceGame(SettingsGameStyle))
            {
                await ImportPackFromZipAsync(
                    EventVoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateEventVoicePackDialogAsync(
                            SettingsGameStyle,
                            folder.DisplayName,
                            await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.EventSlotMapping),
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else
            {
                await ImportPackFromZipAsync(
                    VoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateVoicePackDialogAsync(
                            folder.DisplayName,
                            await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.CrossfireSlotMapping),
                            await TryGetAudioFileAsync(folder, "common_overlay"),
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
        }

        private async void OnImportIconZipClick(object sender, RoutedEventArgs e)
        {
            await PickAndImportPackFilesAsync(isVoice: false, multiple: false);
        }

        private async Task ImportIconZipForCurrentStyleAsync()
        {
            if (SettingsGameStyle == GameStyleMode.Calabiyau) { await ImportCalabiyauPackageFromZipAsync(); return; }
            if (SettingsGameStyle == GameStyleMode.CustomModule) { await ImportCustomModuleAsync(true); return; }
            if (SettingsGameStyle == GameStyleMode.Valorant)
            {
                await ImportValorantPackageFromZipAsync(ValorantExternalAssetService.IconPackageKind);
                return;
            }
            if (SettingsGameStyle == GameStyleMode.Cod2022)
            {
                await ImportCod2022IconZipAsync();
                return;
            }
            if (SettingsGameStyle == GameStyleMode.Titanfall2)
            {
                await ImportTitanfall2IconZipAsync();
                return;
            }
            if (SettingsGameStyle == GameStyleMode.LeagueOfLegends)
            {
                await ImportLeagueOfLegendsIconZipAsync();
                return;
            }
            if (SettingsGameStyle == GameStyleMode.Rdr2)
            {
                await ImportRdr2IconZipAsync();
                return;
            }
            if (SettingsGameStyle == GameStyleMode.WarThunder)
            {
                await ImportWarThunderIconZipAsync();
                return;
            }
            if (await GuardIconPackCreationAsync())
            {
                return;
            }

            if (SettingsGameStyle == GameStyleMode.Dagoujiao)
            {
                await ImportPackFromZipAsync(
                    DagoujiaoIconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateDagoujiaoIconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (SettingsGameStyle == GameStyleMode.Doubao)
            {
                await ImportPackFromZipAsync(
                    DoubaoIconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateDoubaoIconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (SettingsGameStyle == GameStyleMode.Csol)
            {
                await ImportPackFromZipAsync(
                    CsolIconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateCsolIconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (SettingsGameStyle == GameStyleMode.Battlefield1)
            {
                await ImportPackFromZipAsync(
                    Battlefield1IconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateBattlefield1IconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (SettingsGameStyle == GameStyleMode.Battlefield5)
            {
                await ImportPackFromZipAsync(
                    Battlefield5IconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateBattlefield5IconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (SettingsGameStyle == GameStyleMode.Battlefield2042)
            {
                await ImportPackFromZipAsync(
                    Battlefield2042IconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateBattlefield2042IconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (SettingsGameStyle == GameStyleMode.Battlefield6)
            {
                await ImportPackFromZipAsync(
                    Battlefield6IconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateBattlefield6IconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (SettingsGameStyle == GameStyleMode.DeltaForce)
            {
                await ImportPackFromZipAsync(
                    DeltaForceIconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateDeltaForceIconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (SettingsGameStyle == GameStyleMode.Overwatch)
            {
                await ImportPackFromZipAsync(
                    OverwatchIconPackImportFiles,
                    async (folder, files) => await ShowCreateOverwatchIconPackDialogAsync(
                        folder.DisplayName, files, await TryGetCustomPackHeadImageAsync(folder.Path)));
            }
            else if (SettingsGameStyle == GameStyleMode.ModernWarfare2019)
            {
                await ImportPackFromZipAsync(
                    ModernWarfare2019IconPackImportFiles,
                    async (folder, files) => await ShowCreateModernWarfare2019IconPackDialogAsync(
                        folder.DisplayName, files, await TryGetCustomPackHeadImageAsync(folder.Path)));
            }
            else if (SettingsGameStyle == GameStyleMode.Apex)
            {
                await ImportPackFromZipAsync(
                    ApexIconPackImportFiles,
                    async (folder, files) => await ShowCreateApexIconPackDialogAsync(
                        folder.DisplayName, files, await TryGetCustomPackHeadImageAsync(folder.Path)));
            }
            else
            {
                await ImportPackFromZipAsync(
                    IconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateIconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
        }

    }
}
