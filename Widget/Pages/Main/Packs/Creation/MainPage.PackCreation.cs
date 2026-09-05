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
            if (GameStyleService.Current == GameStyleMode.CustomModule)
            {
                await ShowCreateCustomModuleVoicePackDialogAsync();
            }
            else if (GameStyleService.Current == GameStyleMode.Dagoujiao)
            {
                await ShowCreateDagoujiaoVoicePackDialogAsync();
            }
            else if (GameStyleService.Current == GameStyleMode.Doubao)
            {
                await ShowCreateDoubaoVoicePackDialogAsync();
            }
            else if (GameStyleService.Current == GameStyleMode.Csol)
            {
                await ShowCreateCsolVoicePackDialogAsync();
            }
            else if (GameStyleService.Current == GameStyleMode.Valorant)
            {
                await ShowCreateValorantVoicePackDialogAsync();
            }
            else if (GameStyleService.Current == GameStyleMode.Overwatch)
            {
                await ShowCreateOverwatchVoicePackDialogAsync();
            }
            else if (GameStyleService.Current == GameStyleMode.ModernWarfare2019)
            {
                await ShowCreateModernWarfare2019VoicePackDialogAsync();
            }
            else if (IsEventVoiceGame(GameStyleService.Current))
            {
                await ShowCreateEventVoicePackDialogAsync(GameStyleService.Current);
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
            if (GameStyleService.Current == GameStyleMode.Valorant)
            {
                await ImportValorantPackageFromZipAsync(ValorantExternalAssetService.VoicePackageKind);
            }
            else if (GameStyleService.Current == GameStyleMode.CustomModule)
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
            else if (GameStyleService.Current == GameStyleMode.Dagoujiao)
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
            else if (GameStyleService.Current == GameStyleMode.Doubao)
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
            else if (GameStyleService.Current == GameStyleMode.Csol)
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
            else if (GameStyleService.Current == GameStyleMode.Overwatch)
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
            else if (GameStyleService.Current == GameStyleMode.ModernWarfare2019)
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
            else if (IsEventVoiceGame(GameStyleService.Current))
            {
                await ImportPackFromZipAsync(
                    EventVoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateEventVoicePackDialogAsync(
                            GameStyleService.Current,
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
            if (GameStyleService.Current == GameStyleMode.CustomModule) { await ImportCustomModuleAsync(true); return; }
            if (GameStyleService.Current == GameStyleMode.Valorant)
            {
                await ImportValorantPackageFromZipAsync(ValorantExternalAssetService.IconPackageKind);
                return;
            }
            if (await GuardIconPackCreationAsync())
            {
                return;
            }

            if (GameStyleService.Current == GameStyleMode.Dagoujiao)
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
            else if (GameStyleService.Current == GameStyleMode.Doubao)
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
            else if (GameStyleService.Current == GameStyleMode.Csol)
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
            else if (GameStyleService.Current == GameStyleMode.Battlefield1)
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
            else if (GameStyleService.Current == GameStyleMode.Battlefield5)
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
            else if (GameStyleService.Current == GameStyleMode.Battlefield2042)
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
            else if (GameStyleService.Current == GameStyleMode.DeltaForce)
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
            else if (GameStyleService.Current == GameStyleMode.Overwatch)
            {
                await ImportPackFromZipAsync(
                    OverwatchIconPackImportFiles,
                    async (folder, files) => await ShowCreateOverwatchIconPackDialogAsync(
                        folder.DisplayName, files, await TryGetCustomPackHeadImageAsync(folder.Path)));
            }
            else if (GameStyleService.Current == GameStyleMode.ModernWarfare2019)
            {
                await ImportPackFromZipAsync(
                    ModernWarfare2019IconPackImportFiles,
                    async (folder, files) => await ShowCreateModernWarfare2019IconPackDialogAsync(
                        folder.DisplayName, files, await TryGetCustomPackHeadImageAsync(folder.Path)));
            }
            else if (GameStyleService.Current == GameStyleMode.Apex)
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
