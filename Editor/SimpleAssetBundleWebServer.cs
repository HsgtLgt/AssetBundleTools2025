using System;
using System.IO;
using System.Net;
using System.Text;
using UnityEditor;
using UnityEngine;

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
                    LogMessage($"找到网页文件: {path}");
                    return path;
                }
            }
            
            // 如果都找不到，尝试搜索
            string[] guids = AssetDatabase.FindAssets("ui_preview.html");
            if (guids.Length > 0)
            {
                string foundPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                LogMessage($"通过搜索找到网页文件: {foundPath}");
                return foundPath;
            }
            
            LogMessage("警告: 未找到网页文件 ui_preview.html");
            return "ui_preview.html"; // 默认值
        }
        
        /// <summary>
        /// 处理HTTP请求
        /// </summary>
        private async System.Threading.Tasks.Task HandleRequestsAsync()
        {
            while (isRunning && listener.IsListening)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    _ = ProcessRequestAsync(context);
                }
                catch (Exception ex)
                {
                    LogMessage($"处理请求时出错: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 处理单个HTTP请求
        /// </summary>
        private async System.Threading.Tasks.Task ProcessRequestAsync(HttpListenerContext context)
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
                return;
            }
            
            try
            {
                switch (path)
                {
                    case "/":
                        await ServeWebInterfaceAsync(response);
                        break;
                    default:
                        response.StatusCode = 404;
                        response.Close();
                        break;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"处理请求时出错: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
        }
        
        /// <summary>
        /// 提供网页界面
        /// </summary>
        private async System.Threading.Tasks.Task ServeWebInterfaceAsync(HttpListenerResponse response)
        {
            try
            {
                string htmlPath = Path.GetFullPath(webInterfacePath);
                if (File.Exists(htmlPath))
                {
                    string htmlContent = await File.ReadAllTextAsync(htmlPath);
                    byte[] buffer = Encoding.UTF8.GetBytes(htmlContent);
                    
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                else
                {
                    string errorHtml = GenerateErrorPage("网页文件不存在");
                    byte[] buffer = Encoding.UTF8.GetBytes(errorHtml);
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
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
            Debug.Log($"[SimpleAssetBundleWebServer] {message}");
            OnLogMessage?.Invoke(message);
        }
    }
}
