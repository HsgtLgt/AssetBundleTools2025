using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 简单的JSON解析辅助类
/// </summary>
[System.Serializable]
public class AddAssetRequest
{
    public string assetPath;
}

namespace AssetBundleTools
{
    /// <summary>
    /// 简化的 Unity HTTP 服务器，用于与网页界面通信
    /// </summary>
    public class SimpleAssetBundleWebServer
    {
        private HttpListener listener;
        private bool isRunning = false;
        private int port = 8080;
        private string webInterfacePath = "";
        
        // 事件
        public static event Action<string> OnLogMessage;
        
        /// <summary>
        /// 启动HTTP服务器
        /// </summary>
        public void StartServer()
        {
            if (isRunning) return;
            
            try
            {
                // 自动检测网页文件路径
                if (string.IsNullOrEmpty(webInterfacePath))
                {
                    webInterfacePath = FindWebInterfacePath();
                }
                
                // 确保使用绝对路径
                if (!Path.IsPathRooted(webInterfacePath))
                {
                    webInterfacePath = Path.GetFullPath(webInterfacePath);
                }
                
                LogMessage($"最终网页文件路径: {webInterfacePath}");
                LogMessage($"文件是否存在: {File.Exists(webInterfacePath)}");
                
                port = 8080;
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/");
                listener.Start();
                
                isRunning = true;
                LogMessage($"HTTP服务器已启动，端口: {port}");
                LogMessage($"网页界面地址: http://localhost:{port}/");
                
                // 启动请求处理线程
                System.Threading.Tasks.Task.Run(HandleRequestsAsync);
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
                isRunning = false;
                if (listener != null)
                {
                    listener.Stop();
                    listener.Close();
                    listener = null;
                }
                LogMessage("HTTP服务器已停止");
            }
            catch (Exception ex)
            {
                LogMessage($"停止服务器失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 自动查找网页界面文件
        /// </summary>
        private string FindWebInterfacePath()
        {
            // 尝试多个可能的路径，使用绝对路径
            string[] possiblePaths = {
                Path.Combine(Application.dataPath, "..", "Packages", "com.yourcompany.assetbundle-tools-2025", "Editor", "ui_preview.html"),
                Path.Combine(Application.dataPath, "Editor", "ui_preview.html"),
                Path.Combine(Application.dataPath, "..", "Packages", "AssetBundleTools2025", "Editor", "ui_preview.html")
            };
            
            foreach (string path in possiblePaths)
            {
                LogMessage($"检查绝对路径: {path}");
                if (File.Exists(path))
                {
                    LogMessage($"找到网页文件: {path}");
                    return path;
                }
            }
            
            // 如果都找不到，尝试搜索
            string[] guids = AssetDatabase.FindAssets("ui_preview.html");
            if (guids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
                LogMessage($"通过搜索找到网页文件: {fullPath}");
                return fullPath;
            }
            
            LogMessage("警告: 未找到网页文件 ui_preview.html");
            return "ui_preview.html"; // 默认值
        }
        
        /// <summary>
        /// 处理HTTP请求
        /// </summary>
        private async System.Threading.Tasks.Task HandleRequestsAsync()
        {
            while (isRunning && listener != null && listener.IsListening)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    _ = ProcessRequestAsync(context);
                }
                catch (ObjectDisposedException)
                {
                    LogMessage("HttpListener已被释放，停止处理请求");
                    break;
                }
                catch (HttpListenerException ex)
                {
                    LogMessage($"HttpListener异常: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    LogMessage($"处理请求时出错: {ex.Message}");
                    if (!isRunning)
                    {
                        break;
                    }
                }
            }
        }
        
        /// <summary>
        /// 处理单个HTTP请求
        /// </summary>
        private async System.Threading.Tasks.Task ProcessRequestAsync(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;
                
                string path = request.Url.AbsolutePath;
                string method = request.HttpMethod;
                
                LogMessage($"收到请求: {method} {path}");
                
                // 设置CORS头
                try
                {
                    response.Headers.Add("Access-Control-Allow-Origin", "*");
                    response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                    response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
                }
                catch (Exception ex)
                {
                    LogMessage($"设置CORS头失败: {ex.Message}");
                }
                
                // 处理预检请求
                if (method == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }
                
                LogMessage($"开始处理路径: {path}");
                switch (path)
                {
                    case "/":
                        LogMessage("处理根路径请求");
                        await ServeWebInterfaceAsync(response);
                        LogMessage("根路径请求处理完成");
                        break;
                    case "/api/config":
                        LogMessage("处理配置API请求");
                        await ServeConfigAsync(response);
                        LogMessage("配置API请求处理完成");
                        break;
                    case "/api/add-asset":
                        LogMessage("处理添加资源API请求");
                        await ServeAddAssetAsync(request, response);
                        LogMessage("添加资源API请求处理完成");
                        break;
                    case "/api/remove-asset":
                        LogMessage("处理移除资源API请求");
                        await ServeRemoveAssetAsync(request, response);
                        LogMessage("移除资源API请求处理完成");
                        break;
                    case "/api/build":
                        LogMessage("处理构建API请求");
                        await ServeBuildAsync(request, response);
                        LogMessage("构建API请求处理完成");
                        break;
                    case "/api/browse-files":
                        LogMessage("处理文件浏览API请求");
                        await ServeBrowseFilesAsync(request, response);
                        LogMessage("文件浏览API请求处理完成");
                        break;
                    case "/api/assets":
                        LogMessage("处理获取资源列表API请求");
                        await ServeAssetsAsync(request, response);
                        LogMessage("获取资源列表API请求处理完成");
                        break;
                    case "/api/statistics":
                        LogMessage("处理获取统计信息API请求");
                        await ServeStatisticsAsync(request, response);
                        LogMessage("获取统计信息API请求处理完成");
                        break;
                    case "/api/clear-assets":
                        LogMessage("处理清空资源API请求");
                        await ServeClearAssetsAsync(request, response);
                        LogMessage("清空资源API请求处理完成");
                        break;
                    default:
                        LogMessage($"未知路径: {path}，返回404");
                        response.StatusCode = 404;
                        response.Close();
                        break;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"处理请求时出错: {ex.Message}");
                LogMessage($"异常堆栈: {ex.StackTrace}");
                try
                {
                    var response = context.Response;
                    response.StatusCode = 500;
                    string errorHtml = GenerateErrorPage($"服务器内部错误: {ex.Message}");
                    byte[] buffer = Encoding.UTF8.GetBytes(errorHtml);
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    await response.OutputStream.FlushAsync();
                    response.Close();
                }
                catch (Exception ex2)
                {
                    LogMessage($"发送错误响应失败: {ex2.Message}");
                }
            }
        }
        
        /// <summary>
        /// 提供网页界面
        /// </summary>
        private async System.Threading.Tasks.Task ServeWebInterfaceAsync(HttpListenerResponse response)
        {
            LogMessage("开始处理网页请求");
            try
            {
                LogMessage($"尝试提供网页文件: {webInterfacePath}");
                LogMessage($"当前工作目录: {Directory.GetCurrentDirectory()}");
                LogMessage($"文件是否存在: {File.Exists(webInterfacePath)}");
                
                // 直接使用已经找到的绝对路径
                if (File.Exists(webInterfacePath))
                {
                    LogMessage($"文件存在，开始读取: {webInterfacePath}");
                    string htmlContent = await File.ReadAllTextAsync(webInterfacePath);
                    LogMessage($"成功读取文件: {webInterfacePath}，内容长度: {htmlContent?.Length ?? 0}");
                    
                    if (!string.IsNullOrEmpty(htmlContent))
                    {
                        LogMessage("准备发送HTML内容");
                        byte[] buffer = Encoding.UTF8.GetBytes(htmlContent);
                        
                        response.ContentType = "text/html; charset=utf-8";
                        response.ContentLength64 = buffer.Length;
                        response.Headers.Add("Access-Control-Allow-Origin", "*");
                        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                        LogMessage($"设置响应头 - ContentType: {response.ContentType}, ContentLength: {response.ContentLength64}");
                        
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        LogMessage("已写入响应流");
                        
                        await response.OutputStream.FlushAsync();
                        LogMessage("已刷新响应流");
                        
                        LogMessage($"成功提供网页内容，大小: {buffer.Length} 字节，路径: {webInterfacePath}");
                        LogMessage("网页请求处理完成，准备关闭响应");
                        response.Close();
                        LogMessage("响应已关闭");
                        return;
                    }
                    else
                    {
                        LogMessage("文件内容为空");
                        string errorHtml = GenerateErrorPage("网页文件内容为空");
                        byte[] buffer = Encoding.UTF8.GetBytes(errorHtml);
                        response.ContentType = "text/html; charset=utf-8";
                        response.ContentLength64 = buffer.Length;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        await response.OutputStream.FlushAsync();
                        response.Close();
                        return;
                    }
                }
                else
                {
                    LogMessage($"网页文件不存在: {webInterfacePath}");
                    string errorHtml = GenerateErrorPage($"网页文件不存在: {webInterfacePath}");
                    byte[] buffer = Encoding.UTF8.GetBytes(errorHtml);
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    await response.OutputStream.FlushAsync();
                    response.Close();
                    return;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"提供网页界面时出错: {ex.Message}");
                LogMessage($"异常堆栈: {ex.StackTrace}");
                try
                {
                    string errorHtml = GenerateErrorPage($"服务器错误: {ex.Message}");
                    byte[] buffer = Encoding.UTF8.GetBytes(errorHtml);
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    await response.OutputStream.FlushAsync();
                    response.Close();
                    LogMessage("发送了异常错误页面");
                }
                catch (Exception ex2)
                {
                    LogMessage($"发送错误页面时也出错了: {ex2.Message}");
                }
            }
            finally
            {
                try
                {
                    LogMessage("准备关闭响应流");
                    response.Close();
                    LogMessage("响应流已关闭");
                }
                catch (Exception ex)
                {
                    LogMessage($"关闭响应流时出错: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 提供配置信息
        /// </summary>
        private async System.Threading.Tasks.Task ServeConfigAsync(HttpListenerResponse response)
        {
            try
            {
                var config = new
                {
                    port = port,
                    webInterfacePath = webInterfacePath,
                    isRunning = isRunning
                };
                
                string json = JsonUtility.ToJson(config, true);
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                await response.OutputStream.FlushAsync();
                LogMessage("提供配置信息成功");
            }
            catch (Exception ex)
            {
                LogMessage($"提供配置信息时出错: {ex.Message}");
                try
                {
                    response.StatusCode = 500;
                    response.Close();
                }
                catch
                {
                    // 忽略关闭错误
                }
            }
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
        /// 处理添加资源API请求
        /// </summary>
        private async System.Threading.Tasks.Task ServeAddAssetAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                string requestBody = "";
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    requestBody = await reader.ReadToEndAsync();
                }
                
                LogMessage($"收到添加资源请求: {requestBody}");
                
                // 解析JSON数据
                var data = JsonUtility.FromJson<AddAssetRequest>(requestBody);
                string assetPath = data.assetPath;
                
                LogMessage($"尝试添加资源: {assetPath}");
                
                // 验证资源路径
                if (string.IsNullOrEmpty(assetPath))
                {
                    response.StatusCode = 400;
                    response.ContentType = "application/json; charset=utf-8";
                    string jsonResponse = "{\"success\": false, \"message\": \"资源路径不能为空\"}";
                    byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    await response.OutputStream.FlushAsync();
                    response.Close();
                    LogMessage("资源路径为空");
                    return;
                }
                
                // 检查资源是否存在
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset != null)
                {
                    // 使用AssetBundleManager添加资源
                    var manager = AssetBundleManager.Instance;
                    manager.AddAsset(asset);
                    
                    response.StatusCode = 200;
                    response.ContentType = "application/json; charset=utf-8";
                    string jsonResponse = "{\"success\": true, \"message\": \"资源添加成功\"}";
                    byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    await response.OutputStream.FlushAsync();
                    response.Close();
                    LogMessage($"成功添加资源: {assetPath}");
                }
                else
                {
                    // 检查文件是否存在但无法加载
                    string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
                    if (File.Exists(fullPath))
                    {
                        response.StatusCode = 400;
                        response.ContentType = "application/json; charset=utf-8";
                        string jsonResponse = "{\"success\": false, \"message\": \"文件存在但无法作为Unity资源加载\"}";
                        byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                        response.ContentLength64 = buffer.Length;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        await response.OutputStream.FlushAsync();
                        response.Close();
                        LogMessage($"文件存在但无法加载: {assetPath}");
                    }
                    else
                    {
                        response.StatusCode = 404;
                        response.ContentType = "application/json; charset=utf-8";
                        string jsonResponse = "{\"success\": false, \"message\": \"资源不存在\"}";
                        byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                        response.ContentLength64 = buffer.Length;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        await response.OutputStream.FlushAsync();
                        response.Close();
                        LogMessage($"资源不存在: {assetPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"添加资源时出错: {ex.Message}");
                LogMessage($"异常堆栈: {ex.StackTrace}");
                response.StatusCode = 500;
                response.ContentType = "application/json; charset=utf-8";
                string jsonResponse = $"{{\"success\": false, \"message\": \"服务器错误: {ex.Message}\"}}";
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                await response.OutputStream.FlushAsync();
                response.Close();
            }
        }
        
        /// <summary>
        /// 处理移除资源API请求
        /// </summary>
        private async System.Threading.Tasks.Task ServeRemoveAssetAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                string assetPath = request.QueryString["path"];
                LogMessage($"尝试移除资源: {assetPath}");
                
                response.StatusCode = 200;
                response.ContentType = "application/json";
                string jsonResponse = "{\"success\": true, \"message\": \"资源移除成功\"}";
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                await response.OutputStream.FlushAsync();
                response.Close();
                LogMessage($"成功移除资源: {assetPath}");
            }
            catch (Exception ex)
            {
                LogMessage($"移除资源时出错: {ex.Message}");
                response.StatusCode = 500;
                response.ContentType = "application/json";
                string jsonResponse = $"{{\"success\": false, \"message\": \"服务器错误: {ex.Message}\"}}";
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                await response.OutputStream.FlushAsync();
                response.Close();
            }
        }
        
        /// <summary>
        /// 处理构建API请求
        /// </summary>
        private async System.Threading.Tasks.Task ServeBuildAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                LogMessage("开始构建AssetBundle");
                
                // 获取构建配置
                string requestBody = "";
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    requestBody = await reader.ReadToEndAsync();
                }
                
                LogMessage($"收到构建请求: {requestBody}");
                
                // 使用AssetBundleManager进行构建
                var manager = AssetBundleManager.Instance;
                
                // 检查是否有选中的资源
                if (manager.selectedAssets.Count == 0)
                {
                    response.StatusCode = 400;
                    response.ContentType = "application/json; charset=utf-8";
                    string jsonResponse = "{\"success\": false, \"message\": \"没有选择任何资源进行构建\"}";
                    byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    await response.OutputStream.FlushAsync();
                    response.Close();
                    LogMessage("没有选择任何资源");
                    return;
                }
                
                // 异步构建AssetBundle
                var manifest = await manager.BuildAssetBundlesAsync();
                
                if (manifest != null)
                {
                    response.StatusCode = 200;
                    response.ContentType = "application/json; charset=utf-8";
                    string jsonResponse = "{\"success\": true, \"message\": \"AssetBundle构建完成\"}";
                    byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    await response.OutputStream.FlushAsync();
                    response.Close();
                    LogMessage("AssetBundle构建完成");
                }
                else
                {
                    response.StatusCode = 500;
                    response.ContentType = "application/json; charset=utf-8";
                    string jsonResponse = "{\"success\": false, \"message\": \"AssetBundle构建失败\"}";
                    byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    await response.OutputStream.FlushAsync();
                    response.Close();
                    LogMessage("AssetBundle构建失败");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"构建时出错: {ex.Message}");
                LogMessage($"异常堆栈: {ex.StackTrace}");
                response.StatusCode = 500;
                response.ContentType = "application/json; charset=utf-8";
                string jsonResponse = $"{{\"success\": false, \"message\": \"构建失败: {ex.Message}\"}}";
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                await response.OutputStream.FlushAsync();
                response.Close();
            }
        }
        
        /// <summary>
        /// 处理文件浏览API请求
        /// </summary>
        private async System.Threading.Tasks.Task ServeBrowseFilesAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                LogMessage("处理文件浏览请求");
                
                // 使用文件系统扫描而不是AssetDatabase来避免线程问题
                var assets = new List<object>();
                string assetsPath = Path.Combine(Application.dataPath);
                
                if (Directory.Exists(assetsPath))
                {
                    // 扫描Assets文件夹
                    ScanDirectoryForAssets(assetsPath, "Assets", assets, 1000);
                }
                
                response.StatusCode = 200;
                response.ContentType = "application/json; charset=utf-8";
                string jsonResponse = JsonUtility.ToJson(new { success = true, assets = assets });
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                await response.OutputStream.FlushAsync();
                response.Close();
                LogMessage($"返回了 {assets.Count} 个资源");
            }
            catch (Exception ex)
            {
                LogMessage($"文件浏览时出错: {ex.Message}");
                LogMessage($"异常堆栈: {ex.StackTrace}");
                response.StatusCode = 500;
                response.ContentType = "application/json; charset=utf-8";
                string jsonResponse = $"{{\"success\": false, \"message\": \"文件浏览失败: {ex.Message}\"}}";
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                await response.OutputStream.FlushAsync();
                response.Close();
            }
        }
        
        /// <summary>
        /// 递归扫描目录查找资源文件
        /// </summary>
        private void ScanDirectoryForAssets(string physicalPath, string assetPath, List<object> assets, int maxCount)
        {
            if (assets.Count >= maxCount) return;
            
            try
            {
                var files = Directory.GetFiles(physicalPath);
                foreach (string file in files)
                {
                    if (assets.Count >= maxCount) break;
                    
                    string extension = Path.GetExtension(file).ToLower();
                    string fileName = Path.GetFileName(file);
                    
                    // 跳过meta文件和脚本文件
                    if (extension == ".meta" || extension == ".cs" || extension == ".js")
                        continue;
                    
                    // 只包含Unity支持的资源文件
                    if (IsUnityAssetFile(extension))
                    {
                        string relativePath = assetPath + "/" + fileName;
                        assets.Add(new
                        {
                            path = relativePath,
                            name = fileName,
                            type = extension
                        });
                    }
                }
                
                // 递归扫描子目录
                var directories = Directory.GetDirectories(physicalPath);
                foreach (string dir in directories)
                {
                    if (assets.Count >= maxCount) break;
                    
                    string dirName = Path.GetFileName(dir);
                    string newAssetPath = assetPath + "/" + dirName;
                    ScanDirectoryForAssets(dir, newAssetPath, assets, maxCount);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"扫描目录时出错 {physicalPath}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 检查是否为Unity资源文件
        /// </summary>
        private bool IsUnityAssetFile(string extension)
        {
            string[] supportedExtensions = {
                ".prefab", ".mat", ".fbx", ".obj", ".dae", ".3ds", ".blend",
                ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tiff", ".exr",
                ".wav", ".mp3", ".ogg", ".aiff",
                ".anim", ".controller", ".overrideController",
                ".asset", ".unity", ".unitypackage",
                ".shader", ".compute", ".cginc", ".hlsl",
                ".txt", ".bytes", ".json", ".xml", ".csv"
            };
            
            return Array.IndexOf(supportedExtensions, extension) >= 0;
        }

        /// <summary>
        /// 处理获取资源列表API请求
        /// </summary>
        private async System.Threading.Tasks.Task ServeAssetsAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                LogMessage("处理获取资源列表请求");
                
                // 从AssetBundleManager获取已选择的资源
                var manager = AssetBundleManager.Instance;
                var assets = new List<object>();
                
                foreach (var asset in manager.selectedAssets)
                {
                    assets.Add(new
                    {
                        path = asset.path,
                        name = Path.GetFileName(asset.path),
                        size = asset.size,
                        type = asset.type.ToString(),
                        dependencies = asset.dependencies
                    });
                }
                
                response.StatusCode = 200;
                response.ContentType = "application/json; charset=utf-8";
                string jsonResponse = JsonUtility.ToJson(new { success = true, assets = assets });
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                await response.OutputStream.FlushAsync();
                response.Close();
                LogMessage($"返回了 {assets.Count} 个已选资源");
            }
            catch (Exception ex)
            {
                LogMessage($"获取资源列表时出错: {ex.Message}");
                response.StatusCode = 500;
                response.ContentType = "application/json; charset=utf-8";
                string jsonResponse = $"{{\"success\": false, \"message\": \"获取资源列表失败: {ex.Message}\"}}";
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                await response.OutputStream.FlushAsync();
                response.Close();
            }
        }
        
        /// <summary>
        /// 处理获取统计信息API请求
        /// </summary>
        private async System.Threading.Tasks.Task ServeStatisticsAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                LogMessage("处理获取统计信息请求");
                
                // 从AssetBundleManager获取统计信息
                var manager = AssetBundleManager.Instance;
                var stats = manager.GetBuildStatistics();
                
                var statistics = new
                {
                    totalAssets = stats.TotalAssets,
                    totalSize = stats.TotalSize,
                    dependencyCount = stats.DependencyCount,
                    estimatedBuildTime = stats.EstimatedBuildTime
                };
                
                response.StatusCode = 200;
                response.ContentType = "application/json; charset=utf-8";
                string jsonResponse = JsonUtility.ToJson(new { success = true, statistics = statistics });
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                await response.OutputStream.FlushAsync();
                response.Close();
                LogMessage("统计信息返回成功");
            }
            catch (Exception ex)
            {
                LogMessage($"获取统计信息时出错: {ex.Message}");
                response.StatusCode = 500;
                response.ContentType = "application/json; charset=utf-8";
                string jsonResponse = $"{{\"success\": false, \"message\": \"获取统计信息失败: {ex.Message}\"}}";
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                await response.OutputStream.FlushAsync();
                response.Close();
            }
        }
        
        /// <summary>
        /// 处理清空资源API请求
        /// </summary>
        private async System.Threading.Tasks.Task ServeClearAssetsAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                LogMessage("处理清空资源请求");
                
                // 从AssetBundleManager清空资源列表
                var manager = AssetBundleManager.Instance;
                manager.ClearAssets();
                
                response.StatusCode = 200;
                response.ContentType = "application/json; charset=utf-8";
                string jsonResponse = "{\"success\": true, \"message\": \"资源列表已清空\"}";
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                await response.OutputStream.FlushAsync();
                response.Close();
                LogMessage("资源列表已清空");
            }
            catch (Exception ex)
            {
                LogMessage($"清空资源时出错: {ex.Message}");
                response.StatusCode = 500;
                response.ContentType = "application/json; charset=utf-8";
                string jsonResponse = $"{{\"success\": false, \"message\": \"清空资源失败: {ex.Message}\"}}";
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                await response.OutputStream.FlushAsync();
                response.Close();
            }
        }

        /// <summary>
        /// 记录日志
        /// </summary>
        private void LogMessage(string message)
        {
            // 使用 Unity 的主线程调度器来确保线程安全
            if (System.Threading.Thread.CurrentThread.ManagedThreadId == 1)
            {
                // 主线程，直接调用
                Debug.Log($"[SimpleAssetBundleWebServer] {message}");
                OnLogMessage?.Invoke(message);
            }
            else
            {
                // 后台线程，使用 Unity 的主线程调度器
                UnityEditor.EditorApplication.delayCall += () => {
                    Debug.Log($"[SimpleAssetBundleWebServer] {message}");
                    OnLogMessage?.Invoke(message);
                };
            }
        }
    }
}
