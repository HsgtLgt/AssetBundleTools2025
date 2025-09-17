using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AssetBundleTools
{
    /// <summary>
    /// Unity编辑器窗口，用于启动HTTP服务器并嵌入网页
    /// </summary>
    public class AssetBundleWebEditorWindow : EditorWindow
    {
        private SimpleAssetBundleWebServer webServer;
        private Process browserProcess;
        private bool isServerRunning = false;
        private string webInterfacePath = "";
        private int serverPort = 8080;
        
        [MenuItem("工具/AssetBundle Web工具")]
        public static void OpenWindow()
        {
            var window = GetWindow<AssetBundleWebEditorWindow>("AssetBundle Web工具");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }
        
        private void OnEnable()
        {
            // 订阅事件
            SimpleAssetBundleWebServer.OnLogMessage += OnLogMessage;
            
            // 自动检测网页文件路径
            if (string.IsNullOrEmpty(webInterfacePath))
            {
                webInterfacePath = FindWebInterfacePath();
            }
        }
        
        /// <summary>
        /// 自动查找网页界面文件
        /// </summary>
        private string FindWebInterfacePath()
        {
            // 尝试多个可能的路径
            string[] possiblePaths = {
                "Packages/com.yourcompany.assetbundle-tools-2025/Editor/ui_preview.html",
                "Assets/Editor/ui_preview.html",
                "Packages/AssetBundleTools2025/Editor/ui_preview.html"
            };
            
            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    AddLog($"找到网页文件: {path}");
                    return path;
                }
            }
            
            // 如果都找不到，尝试搜索
            string[] guids = AssetDatabase.FindAssets("ui_preview.html");
            if (guids.Length > 0)
            {
                string foundPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                AddLog($"通过搜索找到网页文件: {foundPath}");
                return foundPath;
            }
            
            AddLog("警告: 未找到网页文件 ui_preview.html");
            return "ui_preview.html"; // 默认值
        }
        
        private void OnDisable()
        {
            // 取消订阅事件
            SimpleAssetBundleWebServer.OnLogMessage -= OnLogMessage;
            
            // 清理资源
            StopServer();
            CloseBrowser();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            // 标题
            EditorGUILayout.LabelField("🚀 AssetBundle Web工具", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("2025 科幻版 - 下一代资源管理解决方案", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(10);
            
            // 服务器状态
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("服务器状态", EditorStyles.boldLabel);
            
            if (isServerRunning)
            {
                EditorGUILayout.LabelField("✅ 服务器运行中", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"端口: {serverPort}");
                EditorGUILayout.LabelField($"地址: http://localhost:{serverPort}/");
            }
            else
            {
                EditorGUILayout.LabelField("❌ 服务器未运行", EditorStyles.boldLabel);
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            // 控制按钮
            EditorGUILayout.BeginHorizontal();
            
            if (!isServerRunning)
            {
                if (GUILayout.Button("🚀 启动服务器", GUILayout.Height(30)))
                {
                    StartServer();
                }
            }
            else
            {
                if (GUILayout.Button("⏹️ 停止服务器", GUILayout.Height(30)))
                {
                    StopServer();
                }
            }
            
            if (isServerRunning)
            {
                if (GUILayout.Button("🌐 打开网页", GUILayout.Height(30)))
                {
                    OpenBrowser();
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // 设置
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("设置", EditorStyles.boldLabel);
            
            serverPort = EditorGUILayout.IntField("服务器端口", serverPort);
            webInterfacePath = EditorGUILayout.TextField("网页文件路径", webInterfacePath);
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            // 使用说明
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("使用说明", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. 点击'启动服务器'启动HTTP服务器", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("2. 点击'打开网页'在浏览器中打开炫酷界面", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("3. 在网页中拖拽Unity资源进行打包", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("4. 享受现代化的AssetBundle打包体验！", EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            // 日志区域
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("日志", EditorStyles.boldLabel);
            
            if (GUILayout.Button("清除日志"))
            {
                logContent = "";
            }
            
            EditorGUILayout.BeginScrollView(logScrollPosition, GUILayout.Height(100));
            EditorGUILayout.TextArea(logContent, EditorStyles.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.EndVertical();
        }
        
        private Vector2 logScrollPosition;
        private string logContent = "";
        
        /// <summary>
        /// 启动HTTP服务器
        /// </summary>
        private void StartServer()
        {
            try
            {
                // 自动检测网页文件路径
                if (string.IsNullOrEmpty(webInterfacePath) || !File.Exists(webInterfacePath))
                {
                    webInterfacePath = FindWebInterfacePath();
                }
                
                // 再次检查网页文件是否存在
                if (!File.Exists(webInterfacePath))
                {
                    EditorUtility.DisplayDialog("错误", 
                        $"网页文件不存在！\n\n" +
                        $"尝试的路径: {webInterfacePath}\n\n" +
                        $"请确保 ui_preview.html 文件存在于以下位置之一：\n" +
                        $"- Packages/com.yourcompany.assetbundle-tools-2025/Editor/\n" +
                        $"- Assets/Editor/\n" +
                        $"- Packages/AssetBundleTools2025/Editor/", "确定");
                    return;
                }
                
                // 直接创建 WebServer 实例
                webServer = new SimpleAssetBundleWebServer();
                
                // 启动服务器
                webServer.StartServer();
                
                isServerRunning = true;
                AddLog("HTTP服务器已启动");
                AddLog($"网页文件路径: {webInterfacePath}");
                AddLog($"网页地址: http://localhost:{serverPort}/");
                
                // 自动打开浏览器
                OpenBrowser();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("错误", $"启动服务器失败: {ex.Message}", "确定");
                AddLog($"启动服务器失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 停止HTTP服务器
        /// </summary>
        private void StopServer()
        {
            try
            {
                if (webServer != null)
                {
                    webServer.StopServer();
                    webServer = null;
                }
                
                isServerRunning = false;
                AddLog("HTTP服务器已停止");
            }
            catch (Exception ex)
            {
                AddLog($"停止服务器失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 打开浏览器
        /// </summary>
        private void OpenBrowser()
        {
            try
            {
                if (browserProcess != null && !browserProcess.HasExited)
                {
                    browserProcess.Kill();
                }
                
                string url = $"http://localhost:{serverPort}/";
                browserProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                
                AddLog("已打开浏览器");
            }
            catch (Exception ex)
            {
                AddLog($"打开浏览器失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 关闭浏览器
        /// </summary>
        private void CloseBrowser()
        {
            try
            {
                if (browserProcess != null && !browserProcess.HasExited)
                {
                    browserProcess.Kill();
                    browserProcess = null;
                }
            }
            catch (Exception ex)
            {
                AddLog($"关闭浏览器失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 添加日志
        /// </summary>
        private void AddLog(string message)
        {
            logContent += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            Repaint();
        }
        
        /// <summary>
        /// 处理日志消息
        /// </summary>
        private void OnLogMessage(string message)
        {
            AddLog(message);
        }
    }
}
