﻿using ABI_RC.Core.IO;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.BetterCache;

public class BetterCache : MelonMod {

    internal static Action OnDownloadsStart;
    internal static Action OnDownloadsFinish;

    public override void OnInitializeMelon() {

        ModConfig.InitializeMelonPrefs();
        ModConfig.InitializeBTKUI();

        // Initialize folder
        ModConfig.MeCacheDirectory.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (oldValue == newValue) return;
            InitializeCacheFolder();
        });
        InitializeCacheFolder();

        // Initialize Cache Manager
        CacheManager.Initialize();

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging...");
        #endif
    }

    private static void InitializeCacheFolder() {
        try {
            if (!Directory.Exists(ModConfig.MeCacheDirectory.Value)) {
                Directory.CreateDirectory(ModConfig.MeCacheDirectory.Value);
            }
            DeleteOriginalCacheFolders();
        }
        catch (Exception e) {
            ModConfig.MeCacheDirectory.Value = ModConfig.DefaultDirectory;
            MelonLogger.Error(e);
            MelonLogger.Error($"Error trying to initialize the Cache Folder. " +
                              $"Resetting the cache folder to the default value, {ModConfig.DefaultDirectory}");
        }
    }

    private static void DeleteOriginalCacheFolders() {
        // Ignore deleting if our cache folder is the original cvr folder >.>
        if (Application.dataPath == ModConfig.MeCacheDirectory.Value) return;
        DeleteCacheFolders(Application.dataPath);
    }

    internal static void DeleteCacheFolders(string cachePath) {
        void DeleteFolderIfExists(string path) {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        Task.Run(() => {
            DeleteFolderIfExists(Path.Combine(cachePath, "Avatars"));
            DeleteFolderIfExists(Path.Combine(cachePath, "Worlds"));
            DeleteFolderIfExists(Path.Combine(cachePath, "Spawnables"));
        });
    }

    public static string CacheSizeToReadable(long size) {
        return $"{size / 1024.0 / 1024.0 / 1024.0:F2} GB";
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DownloadManagerHelperFunctions), nameof(DownloadManagerHelperFunctions.GetAppDatapath))]
        public static void After_DownloadManagerHelperFunctions_GetAppDatapath(ref string __result) {
            // Override the cache folder
            try {
                __result = ModConfig.MeCacheDirectory.Value;
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patch: {nameof(After_DownloadManagerHelperFunctions_GetAppDatapath)}");
                MelonLogger.Error(e);
            }
        }

        private static int _previousDownloadingCount;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRDownloadManager), nameof(CVRDownloadManager.CheckAvailability))]
        public static void After_CVRDownloadManager_CheckAvailability(CVRDownloadManager __instance) {
            // Detect when the download queue finishes
            try {
                var currentDownloadingCount = __instance.GetActiveDownloads();
                if (currentDownloadingCount == 0 && _previousDownloadingCount > 0) OnDownloadsFinish?.Invoke();
                if (currentDownloadingCount > 0 && _previousDownloadingCount == 0) OnDownloadsStart?.Invoke();
                _previousDownloadingCount = currentDownloadingCount;
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patch: {nameof(After_CVRDownloadManager_CheckAvailability)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DownloadManagerHelperFunctions), nameof(DownloadManagerHelperFunctions.CalculateMD5Async))]
        public static void After_DownloadManagerHelperFunctions_CalculateMD5Async(string filename) {
            // I OWN THIS SOLUTION, ME KAFEIJAO IS THE ONE TO BLAME, ITS MINE I TAKE FULL RESPONSIBILITY FOR IT
            // As NAK said, it gives the file a lil tickle
            try {
                #if DEBUG
                MelonLogger.Msg($"Tickling {filename}");
                #endif
                File.SetLastAccessTime(filename, DateTime.Now);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patch: {nameof(After_DownloadManagerHelperFunctions_CalculateMD5Async)}");
                MelonLogger.Error(e);
            }
        }

    }
}
