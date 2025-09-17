using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AssetBundleTools
{
    /// <summary>
    /// Unity HTTP服务器，用于与网页界面通信
    /// </summary>
    public class AssetBundleWebServer : MonoBehaviour
    {
        private HttpListener listener;
        private AssetBundleManager manager;
        private bool isRunning = false;
        private int port = 8080;
        
        [Header("服务器设置")]
        public bool autoStart = true;
        public int serverPort = 8080;
        public string webInterfacePath = "Assets/Editor/ui_preview.html";
        
        // 事件
        public static event Action<string> OnLogMessage;
        
        private void Start()
        {
            if (autoStart)
            {
                StartServer();
            }
        }
        
        private void OnDestroy()
        {
            StopServer();
        }
        
        /// <summary>
        /// 启动HTTP服务器
        /// </summary>
        public void StartServer()
        {
            if (isRunning) return;
            
            try
            {
                port = serverPort;
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/");
                listener.Start();
                
                isRunning = true;
                LogMessage($"HTTP服务器已启动，端口: {port}");
                LogMessage($"网页界面地址: http://localhost:{port}/");
                
                // 启动请求处理协程
                StartCoroutine(HandleRequests());
            }
            catch (Exception ex)
            {
                LogMessage($"启动服务器失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 停止HTTP服务器
        /// </summary>
        public void StopServer()
        {
            if (!isRunning) return;
            
            try
            {
                listener?.Stop();
                listener?.Close();
                isRunning = false;
                LogMessage("HTTP服务器已停止");
            }
            catch (Exception ex)
            {
                LogMessage($"停止服务器失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 处理HTTP请求
        /// </summary>
        private IEnumerator HandleRequests()
        {
            while (isRunning && listener.IsListening)
            {
                HttpListenerContext context = null;
                bool hasError = false;
                
                // 获取请求上下文
                try
                {
                    context = listener.GetContext();
                }
                catch (Exception ex)
                {
                    LogMessage($"获取请求时出错: {ex.Message}");
                    hasError = true;
                }
                
                if (hasError)
                {
                    yield return null;
                    continue;
                }
                
                if (context != null)
                {
                    yield return StartCoroutine(ProcessRequest(context));
                }
            }
        }
        
        /// <summary>
        /// 处理单个HTTP请求
        /// </summary>
        private IEnumerator ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            
            string path = request.Url.AbsolutePath;
            string method = request.HttpMethod;
            
            LogMessage($"收到请求: {method} {path}");
            
            // 设置CORS头
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
            
            // 处理预检请求
            if (method == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                yield break;
            }
            
            // 路由处理 - 不使用 try-catch 包装 yield
            bool hasError = false;
            string errorMessage = "";
            
            switch (path)
            {
                case "/":
                    yield return StartCoroutine(ServeWebInterface(response));
                    break;
                case "/api/assets":
                    yield return StartCoroutine(GetAssets(response));
                    break;
                case "/api/add-asset":
                    yield return StartCoroutine(AddAsset(request, response));
                    break;
                case "/api/remove-asset":
                    yield return StartCoroutine(RemoveAsset(request, response));
                    break;
                case "/api/clear-assets":
                    yield return StartCoroutine(ClearAssets(response));
                    break;
                case "/api/build":
                    yield return StartCoroutine(BuildAssetBundles(request, response));
                    break;
                case "/api/config":
                    if (method == "GET")
                        yield return StartCoroutine(GetConfig(response));
                    else if (method == "POST")
                        yield return StartCoroutine(SetConfig(request, response));
                    break;
                case "/api/statistics":
                    yield return StartCoroutine(GetStatistics(response));
                    break;
                default:
                    response.StatusCode = 404;
                    response.Close();
                    break;
            }
            
            // 错误处理在 yield 之后
            if (hasError)
            {
                LogMessage($"处理请求时出错: {errorMessage}");
                response.StatusCode = 500;
                response.Close();
            }
        }
        
        /// <summary>
        /// 提供网页界面
        /// </summary>
        private IEnumerator ServeWebInterface(HttpListenerResponse response)
        {
            try
            {
                string htmlPath = Path.GetFullPath(webInterfacePath);
                if (File.Exists(htmlPath))
                {
                    string htmlContent = File.ReadAllText(htmlPath);
                    byte[] buffer = Encoding.UTF8.GetBytes(htmlContent);
                    
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    string errorHtml = GenerateErrorPage("网页文件不存在");
                    byte[] buffer = Encoding.UTF8.GetBytes(errorHtml);
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"提供网页界面时出错: {ex.Message}");
            }
            finally
            {
                response.Close();
            }
            
            yield return null;
        }
        
        /// <summary>
        /// 获取资源列表
        /// </summary>
        private IEnumerator GetAssets(HttpListenerResponse response)
        {
            try
            {
                if (manager == null)
                    manager = AssetBundleManager.Instance;
                
                var assets = manager.selectedAssets;
                var assetData = new List<object>();
                
                foreach (var asset in assets)
                {
                    assetData.Add(new
                    {
                        path = asset.path,
                        size = asset.size,
                        type = asset.type.ToString(),
                        dependencies = asset.dependencies,
                        lastModified = asset.lastModified.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }
                
                string json = JsonUtility.ToJson(new { assets = assetData });
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                LogMessage($"获取资源列表时出错: {ex.Message}");
            }
            finally
            {
                response.Close();
            }
            
            yield return null;
        }
        
        /// <summary>
        /// 添加资源
        /// </summary>
        private IEnumerator AddAsset(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                if (manager == null)
                    manager = AssetBundleManager.Instance;
                
                // 读取请求体
                string requestBody = "";
                if (request.HasEntityBody)
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        requestBody = reader.ReadToEnd();
                    }
                }
                
                var data = JsonUtility.FromJson<AddAssetRequest>(requestBody);
                
                // 根据路径查找Unity对象
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(data.assetPath);
                if (asset != null)
                {
                    manager.AddAsset(asset);
                    LogMessage($"已添加资源: {data.assetPath}");
                }
                
                response.StatusCode = 200;
                response.Close();
            }
            catch (Exception ex)
            {
                LogMessage($"添加资源时出错: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
            
            yield return null;
        }
        
        /// <summary>
        /// 移除资源
        /// </summary>
        private IEnumerator RemoveAsset(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                if (manager == null)
                    manager = AssetBundleManager.Instance;
                
                string assetPath = request.QueryString["path"];
                if (!string.IsNullOrEmpty(assetPath))
                {
                    manager.RemoveAsset(assetPath);
                    LogMessage($"已移除资源: {assetPath}");
                }
                
                response.StatusCode = 200;
                response.Close();
            }
            catch (Exception ex)
            {
                LogMessage($"移除资源时出错: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
            
            yield return null;
        }
        
        /// <summary>
        /// 清空资源列表
        /// </summary>
        private IEnumerator ClearAssets(HttpListenerResponse response)
        {
            try
            {
                if (manager == null)
                    manager = AssetBundleManager.Instance;
                
                manager.ClearAssets();
                LogMessage("已清空资源列表");
                
                response.StatusCode = 200;
                response.Close();
            }
            catch (Exception ex)
            {
                LogMessage($"清空资源列表时出错: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
            
            yield return null;
        }
        
        /// <summary>
        /// 构建AssetBundle
        /// </summary>
        private IEnumerator BuildAssetBundles(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (manager == null)
                manager = AssetBundleManager.Instance;
            
            // 读取配置
            string requestBody = "";
            if (request.HasEntityBody)
            {
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    requestBody = reader.ReadToEnd();
                }
            }
            
            var buildConfig = JsonUtility.FromJson<BuildConfigRequest>(requestBody);
            
            // 更新管理器配置
            manager.buildConfig.outputPath = buildConfig.outputPath;
            manager.buildConfig.targetPlatform = buildConfig.targetPlatform;
            manager.buildConfig.compressionType = buildConfig.compressionType;
            manager.buildConfig.enableIncrementalBuild = buildConfig.enableIncrementalBuild;
            manager.buildConfig.enableDependencyAnalysis = buildConfig.enableDependencyAnalysis;
            
            // 开始构建
            LogMessage("开始构建AssetBundle...");
            
            bool hasError = false;
            string errorMessage = "";
            
            // 异步构建 - 不使用 try-catch 包装 yield
            var buildTask = manager.BuildAssetBundlesAsync();
            yield return new WaitUntil(() => buildTask.IsCompleted);
            
            if (buildTask.Result != null)
            {
                LogMessage("AssetBundle构建完成！");
                response.StatusCode = 200;
            }
            else
            {
                LogMessage("AssetBundle构建失败！");
                response.StatusCode = 500;
            }
            
            // 错误处理
            if (hasError)
            {
                LogMessage($"构建AssetBundle时出错: {errorMessage}");
                response.StatusCode = 500;
            }
            
            response.Close();
        }
        
        /// <summary>
        /// 获取配置
        /// </summary>
        private IEnumerator GetConfig(HttpListenerResponse response)
        {
            try
            {
                if (manager == null)
                    manager = AssetBundleManager.Instance;
                
                var config = new
                {
                    outputPath = manager.buildConfig.outputPath,
                    targetPlatform = manager.buildConfig.targetPlatform.ToString(),
                    compressionType = manager.buildConfig.compressionType.ToString(),
                    enableIncrementalBuild = manager.buildConfig.enableIncrementalBuild,
                    enableDependencyAnalysis = manager.buildConfig.enableDependencyAnalysis,
                    enableVersionControl = manager.buildConfig.enableVersionControl,
                    strictMode = manager.buildConfig.strictMode,
                    forceRebuild = manager.buildConfig.forceRebuild,
                    dryRunBuild = manager.buildConfig.dryRunBuild
                };
                
                string json = JsonUtility.ToJson(config);
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                LogMessage($"获取配置时出错: {ex.Message}");
            }
            finally
            {
                response.Close();
            }
            
            yield return null;
        }
        
        /// <summary>
        /// 设置配置
        /// </summary>
        private IEnumerator SetConfig(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                if (manager == null)
                    manager = AssetBundleManager.Instance;
                
                string requestBody = "";
                if (request.HasEntityBody)
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        requestBody = reader.ReadToEnd();
                    }
                }
                
                var config = JsonUtility.FromJson<BuildConfigRequest>(requestBody);
                
                // 更新配置
                manager.buildConfig.outputPath = config.outputPath;
                manager.buildConfig.targetPlatform = config.targetPlatform;
                manager.buildConfig.compressionType = config.compressionType;
                manager.buildConfig.enableIncrementalBuild = config.enableIncrementalBuild;
                manager.buildConfig.enableDependencyAnalysis = config.enableDependencyAnalysis;
                
                LogMessage("配置已更新");
                response.StatusCode = 200;
                response.Close();
            }
            catch (Exception ex)
            {
                LogMessage($"设置配置时出错: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
            
            yield return null;
        }
        
        /// <summary>
        /// 获取统计信息
        /// </summary>
        private IEnumerator GetStatistics(HttpListenerResponse response)
        {
            try
            {
                if (manager == null)
                    manager = AssetBundleManager.Instance;
                
                var stats = manager.GetBuildStatistics();
                var statistics = new
                {
                    totalAssets = stats.TotalAssets,
                    totalSize = stats.TotalSize,
                    dependencyCount = stats.DependencyCount,
                    estimatedBuildTime = stats.EstimatedBuildTime
                };
                
                string json = JsonUtility.ToJson(statistics);
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                LogMessage($"获取统计信息时出错: {ex.Message}");
            }
            finally
            {
                response.Close();
            }
            
            yield return null;
        }
        
        /// <summary>
        /// 生成错误页面
        /// </summary>
        private string GenerateErrorPage(string message)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>错误</title>
    <style>
        body {{ font-family: Arial, sans-serif; text-align: center; padding: 50px; }}
        .error {{ color: #ff0000; font-size: 18px; }}
    </style>
</head>
<body>
    <div class=""error"">{message}</div>
</body>
</html>";
        }
        
        /// <summary>
        /// 记录日志
        /// </summary>
        private void LogMessage(string message)
        {
            Debug.Log($"[AssetBundleWebServer] {message}");
            OnLogMessage?.Invoke(message);
        }
    }
    
    // 请求数据类
    [System.Serializable]
    public class AddAssetRequest
    {
        public string assetPath;
    }
    
    [System.Serializable]
    public class BuildConfigRequest
    {
        public string outputPath;
        public BuildTarget targetPlatform;
        public CompressionType compressionType;
        public bool enableIncrementalBuild;
        public bool enableDependencyAnalysis;
        public bool enableVersionControl;
        public bool strictMode;
        public bool forceRebuild;
        public bool dryRunBuild;
    }
}
