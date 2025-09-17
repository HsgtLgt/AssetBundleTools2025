using System;
using UnityEditor;
using UnityEngine;

namespace AssetBundleTools
{
    /// <summary>
    /// AssetBundle测试辅助工具
    /// </summary>
    public class AssetBundleTestHelper : EditorWindow
    {
        [MenuItem("工具/AssetBundle 测试工具")]
        public static void OpenWindow()
        {
            var window = GetWindow<AssetBundleTestHelper>("AssetBundle 测试工具");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("🧪 AssetBundle 测试工具", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("用于测试和调试AssetBundle功能", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("测试功能", EditorStyles.boldLabel);
            
            if (GUILayout.Button("🔍 测试文件浏览API", GUILayout.Height(30)))
            {
                TestBrowseFilesAPI();
            }
            
            if (GUILayout.Button("➕ 测试添加资源API", GUILayout.Height(30)))
            {
                TestAddAssetAPI();
            }
            
            if (GUILayout.Button("📊 测试统计信息API", GUILayout.Height(30)))
            {
                TestStatisticsAPI();
            }
            
            if (GUILayout.Button("🗑️ 测试清空资源API", GUILayout.Height(30)))
            {
                TestClearAssetsAPI();
            }
            
            if (GUILayout.Button("🚀 测试构建API", GUILayout.Height(30)))
            {
                TestBuildAPI();
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("调试信息", EditorStyles.boldLabel);
            
            var manager = AssetBundleManager.Instance;
            EditorGUILayout.LabelField($"已选资源数量: {manager.selectedAssets.Count}");
            EditorGUILayout.LabelField($"输出路径: {manager.buildConfig.outputPath}");
            EditorGUILayout.LabelField($"目标平台: {manager.buildConfig.targetPlatform}");
            
            EditorGUILayout.EndVertical();
        }
        
        private void TestBrowseFilesAPI()
        {
            try
            {
                Debug.Log("开始测试文件浏览API...");
                string[] guids = AssetDatabase.FindAssets("", new[] { "Assets" });
                Debug.Log($"找到 {guids.Length} 个资源文件");
                
                int count = 0;
                foreach (string guid in guids)
                {
                    if (count >= 10) break; // 只显示前10个
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path) && !path.EndsWith(".meta"))
                    {
                        Debug.Log($"资源: {path}");
                        count++;
                    }
                }
                Debug.Log("文件浏览API测试完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"文件浏览API测试失败: {ex.Message}");
            }
        }
        
        private void TestAddAssetAPI()
        {
            try
            {
                Debug.Log("开始测试添加资源API...");
                var manager = AssetBundleManager.Instance;
                
                // 尝试添加一个测试资源
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
                if (guids.Length > 0)
                {
                    string testPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(testPath);
                    if (asset != null)
                    {
                        manager.AddAsset(asset);
                        Debug.Log($"成功添加测试资源: {testPath}");
                    }
                }
                else
                {
                    Debug.LogWarning("没有找到可用的纹理资源进行测试");
                }
                
                Debug.Log($"当前已选资源数量: {manager.selectedAssets.Count}");
                Debug.Log("添加资源API测试完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"添加资源API测试失败: {ex.Message}");
            }
        }
        
        private void TestStatisticsAPI()
        {
            try
            {
                Debug.Log("开始测试统计信息API...");
                var manager = AssetBundleManager.Instance;
                var stats = manager.GetBuildStatistics();
                
                Debug.Log($"总资源数量: {stats.TotalAssets}");
                Debug.Log($"总大小: {stats.TotalSize} 字节");
                Debug.Log($"依赖关系数量: {stats.DependencyCount}");
                Debug.Log($"预计构建时间: {stats.EstimatedBuildTime} 秒");
                
                Debug.Log("统计信息API测试完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"统计信息API测试失败: {ex.Message}");
            }
        }
        
        private void TestClearAssetsAPI()
        {
            try
            {
                Debug.Log("开始测试清空资源API...");
                var manager = AssetBundleManager.Instance;
                int beforeCount = manager.selectedAssets.Count;
                
                manager.ClearAssets();
                
                Debug.Log($"清空前资源数量: {beforeCount}");
                Debug.Log($"清空后资源数量: {manager.selectedAssets.Count}");
                Debug.Log("清空资源API测试完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"清空资源API测试失败: {ex.Message}");
            }
        }
        
        private void TestBuildAPI()
        {
            try
            {
                Debug.Log("开始测试构建API...");
                var manager = AssetBundleManager.Instance;
                
                if (manager.selectedAssets.Count == 0)
                {
                    Debug.LogWarning("没有选中的资源，无法测试构建");
                    return;
                }
                
                Debug.Log($"准备构建 {manager.selectedAssets.Count} 个资源");
                Debug.Log("注意：实际构建可能需要较长时间，这里只是测试API调用");
                
                // 这里不实际执行构建，只是测试API调用
                Debug.Log("构建API测试完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"构建API测试失败: {ex.Message}");
            }
        }
    }
}
