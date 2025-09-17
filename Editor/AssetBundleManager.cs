using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AssetBundleTools
{
    /// <summary>
    /// 资源信息类
    /// </summary>
    [System.Serializable]
    public class AssetInfo
    {
        public string path;
        public long size;
        public string[] dependencies;
        public DateTime lastModified;
        public AssetType type;
        public string bundleName;
    }

    /// <summary>
    /// 资源类型枚举
    /// </summary>
    public enum AssetType
    {
        Texture,
        Material,
        Mesh,
        Audio,
        Animation,
        Prefab,
        Script,
        Other
    }

    /// <summary>
    /// 构建配置类
    /// </summary>
    [System.Serializable]
    public class BuildConfig
    {
        [Header("基本设置")]
        public string outputPath = "AssetBundles";
        public BuildTarget targetPlatform = BuildTarget.StandaloneWindows64;
        public bool enableIncrementalBuild = true;
        public bool enableDependencyAnalysis = true;
        public bool enableVersionControl = false;

        [Header("压缩设置")]
        public CompressionType compressionType = CompressionType.LZ4;
        public bool enableCompression = true;

        [Header("高级选项")]
        public BuildAssetBundleOptions buildOptions = BuildAssetBundleOptions.None;
        public bool strictMode = false;
        public bool forceRebuild = false;
        public bool dryRunBuild = false;
    }

    /// <summary>
    /// 压缩类型枚举
    /// </summary>
    public enum CompressionType
    {
        None,
        LZMA,
        LZ4
    }

    /// <summary>
    /// 构建进度事件
    /// </summary>
    public class BuildProgressEventArgs : EventArgs
    {
        public float Progress { get; set; }
        public string Message { get; set; }
        public BuildProgressType Type { get; set; }
    }

    public enum BuildProgressType
    {
        Info,
        Warning,
        Error,
        Success
    }

    /// <summary>
    /// 优化的AssetBundle管理器
    /// </summary>
    public class AssetBundleManager : ScriptableObject
    {
        private static AssetBundleManager _instance;
        public static AssetBundleManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = CreateInstance<AssetBundleManager>();
                }
                return _instance;
            }
        }

        [Header("配置")]
        public BuildConfig buildConfig = new BuildConfig();

        [Header("资源管理")]
        public List<AssetInfo> selectedAssets = new List<AssetInfo>();
        public Dictionary<string, AssetInfo> assetCache = new Dictionary<string, AssetInfo>();

        // 事件
        public static event EventHandler<BuildProgressEventArgs> OnBuildProgress;
        public static event EventHandler<string> OnBuildComplete;
        public static event EventHandler<string> OnBuildError;

        // 构建状态
        private bool isBuilding = false;
        private AssetBundleManifest lastManifest;

        /// <summary>
        /// 添加资源到打包列表
        /// </summary>
        public void AddAsset(UnityEngine.Object asset)
        {
            if (asset == null) return;

            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath)) return;

            // 检查是否已存在
            if (selectedAssets.Exists(a => a.path == assetPath)) return;

            AssetInfo assetInfo = GetOrCreateAssetInfo(assetPath);
            selectedAssets.Add(assetInfo);

            // 如果启用依赖分析，添加依赖资源
            if (buildConfig.enableDependencyAnalysis)
            {
                AddDependencies(assetPath);
            }

            OnBuildProgress?.Invoke(this, new BuildProgressEventArgs
            {
                Progress = 0,
                Message = $"已添加资源: {assetPath}",
                Type = BuildProgressType.Info
            });
        }

        /// <summary>
        /// 移除资源
        /// </summary>
        public void RemoveAsset(string assetPath)
        {
            selectedAssets.RemoveAll(a => a.path == assetPath);
        }

        /// <summary>
        /// 清空资源列表
        /// </summary>
        public void ClearAssets()
        {
            selectedAssets.Clear();
        }

        /// <summary>
        /// 获取或创建资源信息
        /// </summary>
        private AssetInfo GetOrCreateAssetInfo(string assetPath)
        {
            if (assetCache.TryGetValue(assetPath, out AssetInfo cachedInfo))
            {
                // 检查文件是否已修改
                FileInfo fileInfo = new FileInfo(assetPath);
                if (fileInfo.Exists && fileInfo.LastWriteTime > cachedInfo.lastModified)
                {
                    // 文件已修改，重新计算信息
                    cachedInfo = CreateAssetInfo(assetPath);
                    assetCache[assetPath] = cachedInfo;
                }
                return cachedInfo;
            }

            AssetInfo newInfo = CreateAssetInfo(assetPath);
            assetCache[assetPath] = newInfo;
            return newInfo;
        }

        /// <summary>
        /// 创建资源信息
        /// </summary>
        private AssetInfo CreateAssetInfo(string assetPath)
        {
            FileInfo fileInfo = new FileInfo(assetPath);
            string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);

            return new AssetInfo
            {
                path = assetPath,
                size = fileInfo.Exists ? fileInfo.Length : 0,
                dependencies = dependencies,
                lastModified = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue,
                type = GetAssetType(assetPath),
                bundleName = ""
            };
        }

        /// <summary>
        /// 获取资源类型
        /// </summary>
        private AssetType GetAssetType(string assetPath)
        {
            string extension = Path.GetExtension(assetPath).ToLower();
            
            switch (extension)
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".tga":
                case ".psd":
                case ".tiff":
                    return AssetType.Texture;
                case ".mat":
                    return AssetType.Material;
                case ".fbx":
                case ".obj":
                case ".dae":
                    return AssetType.Mesh;
                case ".wav":
                case ".mp3":
                case ".ogg":
                    return AssetType.Audio;
                case ".anim":
                case ".controller":
                    return AssetType.Animation;
                case ".prefab":
                    return AssetType.Prefab;
                case ".cs":
                case ".js":
                    return AssetType.Script;
                default:
                    return AssetType.Other;
            }
        }

        /// <summary>
        /// 添加依赖资源
        /// </summary>
        private void AddDependencies(string assetPath)
        {
            string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
            
            foreach (string dependency in dependencies)
            {
                // 跳过脚本文件
                if (dependency.EndsWith(".cs") || dependency.EndsWith(".js"))
                    continue;

                // 检查是否已存在
                if (selectedAssets.Exists(a => a.path == dependency))
                    continue;

                AssetInfo depInfo = GetOrCreateAssetInfo(dependency);
                selectedAssets.Add(depInfo);
            }
        }

        /// <summary>
        /// 异步构建AssetBundle
        /// </summary>
        public async Task<AssetBundleManifest> BuildAssetBundlesAsync()
        {
            if (isBuilding)
            {
                OnBuildError?.Invoke(this, "构建正在进行中，请等待完成");
                return null;
            }

            if (selectedAssets.Count == 0)
            {
                OnBuildError?.Invoke(this, "没有选择任何资源");
                return null;
            }

            isBuilding = true;

            try
            {
                OnBuildProgress?.Invoke(this, new BuildProgressEventArgs
                {
                    Progress = 0,
                    Message = "开始构建 AssetBundle...",
                    Type = BuildProgressType.Info
                });

                // 创建输出目录
                await CreateOutputDirectoryAsync();

                // 准备构建数据
                List<AssetBundleBuild> builds = await PrepareBuildDataAsync();

                // 设置构建选项
                BuildAssetBundleOptions options = GetBuildOptions();

                OnBuildProgress?.Invoke(this, new BuildProgressEventArgs
                {
                    Progress = 30,
                    Message = "开始构建...",
                    Type = BuildProgressType.Info
                });

                // 异步构建
                AssetBundleManifest manifest = await Task.Run(() =>
                {
                    return BuildPipeline.BuildAssetBundles(
                        buildConfig.outputPath,
                        builds.ToArray(),
                        options,
                        buildConfig.targetPlatform
                    );
                });

                OnBuildProgress?.Invoke(this, new BuildProgressEventArgs
                {
                    Progress = 80,
                    Message = "构建完成，正在生成报告...",
                    Type = BuildProgressType.Info
                });

                // 生成构建报告
                await GenerateBuildReportAsync(manifest);

                OnBuildProgress?.Invoke(this, new BuildProgressEventArgs
                {
                    Progress = 100,
                    Message = "构建完成！",
                    Type = BuildProgressType.Success
                });

                lastManifest = manifest;
                OnBuildComplete?.Invoke(this, "AssetBundle 构建完成");

                return manifest;
            }
            catch (Exception ex)
            {
                OnBuildError?.Invoke(this, $"构建失败: {ex.Message}");
                return null;
            }
            finally
            {
                isBuilding = false;
            }
        }

        /// <summary>
        /// 创建输出目录
        /// </summary>
        private async Task CreateOutputDirectoryAsync()
        {
            await Task.Run(() =>
            {
                if (!Directory.Exists(buildConfig.outputPath))
                {
                    Directory.CreateDirectory(buildConfig.outputPath);
                }
            });

            OnBuildProgress?.Invoke(this, new BuildProgressEventArgs
            {
                Progress = 10,
                Message = "输出目录已创建",
                Type = BuildProgressType.Info
            });
        }

        /// <summary>
        /// 准备构建数据
        /// </summary>
        private async Task<List<AssetBundleBuild>> PrepareBuildDataAsync()
        {
            return await Task.Run(() =>
            {
                List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
                
                // 按类型分组资源
                Dictionary<string, List<string>> bundleGroups = new Dictionary<string, List<string>>();
                
                foreach (var asset in selectedAssets)
                {
                    string bundleName = GetBundleName(asset);
                    
                    if (!bundleGroups.ContainsKey(bundleName))
                    {
                        bundleGroups[bundleName] = new List<string>();
                    }
                    
                    bundleGroups[bundleName].Add(asset.path);
                }

                // 创建构建数据
                foreach (var group in bundleGroups)
                {
                    AssetBundleBuild build = new AssetBundleBuild
                    {
                        assetBundleName = group.Key,
                        assetNames = group.Value.ToArray()
                    };
                    builds.Add(build);
                }

                return builds;
            });
        }

        /// <summary>
        /// 获取Bundle名称
        /// </summary>
        private string GetBundleName(AssetInfo asset)
        {
            if (!string.IsNullOrEmpty(asset.bundleName))
                return asset.bundleName;

            // 根据资源类型自动命名
            switch (asset.type)
            {
                case AssetType.Texture:
                    return "textures";
                case AssetType.Material:
                    return "materials";
                case AssetType.Mesh:
                    return "meshes";
                case AssetType.Audio:
                    return "audio";
                case AssetType.Animation:
                    return "animations";
                case AssetType.Prefab:
                    return "prefabs";
                default:
                    return "misc";
            }
        }

        /// <summary>
        /// 获取构建选项
        /// </summary>
        private BuildAssetBundleOptions GetBuildOptions()
        {
            BuildAssetBundleOptions options = buildConfig.buildOptions;

            if (buildConfig.forceRebuild)
                options |= BuildAssetBundleOptions.ForceRebuildAssetBundle;

            if (buildConfig.strictMode)
                options |= BuildAssetBundleOptions.StrictMode;

            if (buildConfig.dryRunBuild)
                options |= BuildAssetBundleOptions.DryRunBuild;

            if (buildConfig.enableCompression)
            {
                switch (buildConfig.compressionType)
                {
                    case CompressionType.LZMA:
                        options |= BuildAssetBundleOptions.ChunkBasedCompression;
                        break;
                    case CompressionType.LZ4:
                        options |= BuildAssetBundleOptions.ChunkBasedCompression;
                        break;
                }
            }
            else
            {
                options |= BuildAssetBundleOptions.UncompressedAssetBundle;
            }

            return options;
        }

        /// <summary>
        /// 生成构建报告
        /// </summary>
        private async Task GenerateBuildReportAsync(AssetBundleManifest manifest)
        {
            await Task.Run(() =>
            {
                if (manifest == null) return;

                string reportPath = Path.Combine(buildConfig.outputPath, "build_report.txt");
                using (StreamWriter writer = new StreamWriter(reportPath))
                {
                    writer.WriteLine("=== AssetBundle 构建报告 ===");
                    writer.WriteLine($"构建时间: {DateTime.Now}");
                    writer.WriteLine($"目标平台: {buildConfig.targetPlatform}");
                    writer.WriteLine($"输出路径: {buildConfig.outputPath}");
                    writer.WriteLine();

                    writer.WriteLine("=== 构建的 AssetBundle ===");
                    string[] bundles = manifest.GetAllAssetBundles();
                    foreach (string bundleName in bundles)
                    {
                        writer.WriteLine($"- {bundleName}");
                        string[] dependencies = manifest.GetAllDependencies(bundleName);
                        if (dependencies.Length > 0)
                        {
                            writer.WriteLine("  依赖项:");
                            foreach (string dep in dependencies)
                            {
                                writer.WriteLine($"    - {dep}");
                            }
                        }
                        writer.WriteLine();
                    }
                }
            });
        }

        /// <summary>
        /// 获取构建统计信息
        /// </summary>
        public BuildStatistics GetBuildStatistics()
        {
            long totalSize = 0;
            int totalAssets = selectedAssets.Count;
            int dependencyCount = 0;

            foreach (var asset in selectedAssets)
            {
                totalSize += asset.size;
                dependencyCount += asset.dependencies.Length;
            }

            return new BuildStatistics
            {
                TotalAssets = totalAssets,
                TotalSize = totalSize,
                DependencyCount = dependencyCount,
                EstimatedBuildTime = CalculateEstimatedBuildTime()
            };
        }

        /// <summary>
        /// 计算预计构建时间
        /// </summary>
        private float CalculateEstimatedBuildTime()
        {
            // 基于资源数量和大小估算
            float baseTime = selectedAssets.Count * 0.1f;
            float sizeFactor = (selectedAssets.Sum(a => a.size) / (1024 * 1024)) * 0.5f; // MB * 0.5秒
            return baseTime + sizeFactor;
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public void SaveConfig()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        public void LoadConfig()
        {
            string configPath = "Assets/Editor/AssetBundleConfig.asset";
            if (File.Exists(configPath))
            {
                AssetBundleManager loaded = AssetDatabase.LoadAssetAtPath<AssetBundleManager>(configPath);
                if (loaded != null)
                {
                    buildConfig = loaded.buildConfig;
                }
            }
        }
    }

    /// <summary>
    /// 构建统计信息
    /// </summary>
    [System.Serializable]
    public class BuildStatistics
    {
        public int TotalAssets;
        public long TotalSize;
        public int DependencyCount;
        public float EstimatedBuildTime;
    }
}
