﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityVersion = AssetRipper.VersionUtilities.UnityVersion;
using System.Drawing;
using MelonLoader.Utils;

namespace MelonLoader.InternalUtils
{
    public static class UnityInformationHandler
    {
        private const string DefaultInfo = "UNKNOWN";

        public static string GameName { get; private set; }
        public static string GameDeveloper { get; private set; }
        public static UnityVersion EngineVersion { get; private set; } = UnityVersion.MinVersion;
        public static string GameVersion { get; private set; }

        internal static void Setup()
        {
            string gameDataPath = MelonEnvironment.UnityGameDataDirectory;

            if (!string.IsNullOrEmpty(MelonLaunchOptions.Core.UnityVersion))
            {
                try { EngineVersion = UnityVersion.Parse(MelonLaunchOptions.Core.UnityVersion); }
                catch (Exception ex)
                {
                    if (MelonDebug.IsEnabled())
                        MelonLogger.Error(ex);
                }
            }

            AssetsManager assetsManager = new AssetsManager();
            ReadGameInfo(assetsManager, gameDataPath);
            assetsManager.UnloadAll();

            if (string.IsNullOrEmpty(GameDeveloper)
                || string.IsNullOrEmpty(GameName))
                ReadGameInfoFallback();

            if (EngineVersion == UnityVersion.MinVersion)
            {
                try { EngineVersion = ReadVersionFallback(gameDataPath); }
                catch (Exception ex)
                {
                    if (MelonDebug.IsEnabled())
                        MelonLogger.Error(ex);
                }
            }

            if (string.IsNullOrEmpty(GameDeveloper))
                GameDeveloper = DefaultInfo;
            if (string.IsNullOrEmpty(GameName))
                GameName = DefaultInfo;

            //BootstrapInterop.SetDefaultConsoleTitleWithGameName(GameName, GameVersion);

            if (string.IsNullOrEmpty(GameVersion))
                GameVersion = DefaultInfo;

            MelonLogger.WriteLine(Color.Magenta);
            MelonLogger.Msg($"Game Name: {GameName}");
            MelonLogger.Msg($"Game Developer: {GameDeveloper}");
            MelonLogger.Msg($"Unity Version: {EngineVersion}");
            MelonLogger.Msg($"Game Version: {GameVersion}");
            MelonLogger.WriteLine(Color.Magenta);
            MelonLogger.WriteSpacer();
        }

        private static void ReadGameInfo(AssetsManager assetsManager, string gameDataPath)
        {
            AssetsFileInstance instance = null;
            try
            {
                string bundlePath = Path.Combine(gameDataPath, "globalgamemanagers");
                if (!APKAssetManager.DoesAssetExist(bundlePath))
                    bundlePath = Path.Combine(gameDataPath, "mainData");

                if (!APKAssetManager.DoesAssetExist(bundlePath))
                {
                    bundlePath = Path.Combine(gameDataPath, "data.unity3d");
                    if (!APKAssetManager.DoesAssetExist(bundlePath))
                        return;

                    Stream bundleStream = APKAssetManager.GetAssetStream(bundlePath);
                    BundleFileInstance bundleFile = assetsManager.LoadBundleFile(bundleStream, bundlePath);
                    instance = assetsManager.LoadAssetsFileFromBundle(bundleFile, "globalgamemanagers");
                }
                else
                {
                    Stream bundleStream = APKAssetManager.GetAssetStream(bundlePath);
                    instance = assetsManager.LoadAssetsFile(bundleStream, bundlePath, true);
                }

                if (instance == null)
                    return;

                assetsManager.LoadIncludedClassPackage();
                if (!instance.file.Metadata.TypeTreeEnabled)
                    assetsManager.LoadClassDatabaseFromPackage(instance.file.Metadata.UnityVersion);

                if (EngineVersion == UnityVersion.MinVersion)
                    EngineVersion = UnityVersion.Parse(instance.file.Metadata.UnityVersion);

                List<AssetFileInfo> assetFiles = instance.file.GetAssetsOfType(AssetClassID.PlayerSettings);
                if (assetFiles.Count > 0)
                {
                    AssetFileInfo playerSettings = assetFiles.First();

                    AssetTypeValueField playerSettings_baseField = assetsManager.GetBaseField(instance, playerSettings);
                    if (playerSettings_baseField != null)
                    {
                        AssetTypeValueField bundleVersion = playerSettings_baseField.Get("bundleVersion");
                        if (bundleVersion != null)
                            GameVersion = bundleVersion.AsString;

                        AssetTypeValueField companyName = playerSettings_baseField.Get("companyName");
                        if (companyName != null)
                            GameDeveloper = companyName.AsString;

                        AssetTypeValueField productName = playerSettings_baseField.Get("productName");
                        if (productName != null)
                            GameName = productName.AsString;
                    }
                }
            }
            catch (Exception ex)
            {
                if (MelonDebug.IsEnabled())
                    MelonLogger.Error(ex);
                //MelonLogger.Error("Failed to Initialize Assets Manager!");
            }
            if (instance != null)
                instance.file.Close();
        }

        private static void ReadGameInfoFallback()
        {
            // i don't think any android apps have app.info and i don't know any other way to get game info (unless i just parse the package name, but that's kinda dumb)
            /*try
            {
                string appInfoFilePath = Path.Combine(MelonEnvironment.UnityGameDataDirectory, "app.info");
                if (!File.Exists(appInfoFilePath))
                    return;

                string[] filestr = File.ReadAllLines(appInfoFilePath);
                if ((filestr == null) || (filestr.Length < 2))
                    return;

                if (string.IsNullOrEmpty(GameDeveloper) && !string.IsNullOrEmpty(filestr[0]))
                    GameDeveloper = filestr[0];

                if (string.IsNullOrEmpty(GameName) && !string.IsNullOrEmpty(filestr[1]))
                    GameName = filestr[1];

            }
            catch (Exception ex)
            {
                if (MelonDebug.IsEnabled())
                    MelonLogger.Error(ex);
            }*/
        }

        private static UnityVersion ReadVersionFallback(string gameDataPath)
        {
            try
            {
                var globalgamemanagersPath = Path.Combine(gameDataPath, "globalgamemanagers");
                if (APKAssetManager.DoesAssetExist(globalgamemanagersPath))
                    return GetVersionFromGlobalGameManagers(APKAssetManager.GetAssetBytes(globalgamemanagersPath));
            }
            catch (Exception ex)
            {
                if (MelonDebug.IsEnabled())
                    MelonLogger.Error(ex);
            }

            try
            {
                var dataPath = Path.Combine(gameDataPath, "data.unity3d");
                if (APKAssetManager.DoesAssetExist(dataPath))
                    return GetVersionFromDataUnity3D(APKAssetManager.GetAssetStream(dataPath));
            }
            catch (Exception ex)
            {
                if (MelonDebug.IsEnabled())
                    MelonLogger.Error(ex);
            }

            return default;
        }

        private static UnityVersion GetVersionFromGlobalGameManagers(byte[] ggmBytes)
        {
            var verString = new StringBuilder();
            var idx = 0x14;
            while (ggmBytes[idx] != 0)
            {
                verString.Append(Convert.ToChar(ggmBytes[idx]));
                idx++;
            }

            Regex UnityVersionRegex = new Regex(@"^[0-9]+\.[0-9]+\.[0-9]+[abcfx][0-9]+$", RegexOptions.Compiled);
            string unityVer = verString.ToString();
            if (!UnityVersionRegex.IsMatch(unityVer))
            {
                idx = 0x30;
                verString = new StringBuilder();
                while (ggmBytes[idx] != 0)
                {
                    verString.Append(Convert.ToChar(ggmBytes[idx]));
                    idx++;
                }

                unityVer = verString.ToString().Trim();
            }

            return UnityVersion.Parse(unityVer);
        }

        private static UnityVersion GetVersionFromDataUnity3D(Stream fileStream)
        {
            var verString = new StringBuilder();

            if (fileStream.CanSeek)
                fileStream.Seek(0x12, SeekOrigin.Begin);
            else
            {
                if (fileStream.Read(new byte[0x12], 0, 0x12) != 0x12)
                    throw new("Failed to seek to 0x12 in data.unity3d");
            }

            while (true)
            {
                var read = fileStream.ReadByte();
                if (read == 0)
                    break;
                verString.Append(Convert.ToChar(read));
            }

            return UnityVersion.Parse(verString.ToString().Trim());
        }
    }
}
