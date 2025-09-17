# AssetBundle Tools 2025 - 更新日志

## 版本 1.0.6 (2025-01-27)

### 修复
- 修复了网页显示为黑色的问题
- 改进了 HTTP 响应处理逻辑
- 添加了响应流刷新和错误处理
- 添加了配置 API 端点

### 技术改进
- 在 ServeWebInterfaceAsync 中添加了 FlushAsync() 调用
- 改进了错误处理，确保响应流正确关闭
- 添加了 ServeConfigAsync 方法处理 /api/config 请求
- 增加了详细的日志记录，便于调试

---

## 版本 1.0.5 (2025-01-27)

### 修复
- 彻底修复了所有 GUID 冲突问题
- 解决了 Editor 脚本不能作为组件的问题
- 创建了简化的 HTTP 服务器类
- 删除了有问题的旧服务器文件

### 技术改进
- 为所有 meta 文件生成了真正唯一的 GUID
- 重新设计了服务器架构，不再继承 MonoBehaviour
- 使用异步方法替代协程，避免 Editor 脚本限制
- 简化了 HTTP 服务器实现，提高稳定性

---

## 版本 1.0.4 (2025-01-27)

### 修复
- 添加了自动网页文件路径检测功能
- 修复了 '网页文件不存在' 的错误
- 改进了错误提示，提供详细的路径信息
- 添加了多种路径尝试机制

### 技术改进
- 自动检测多个可能的网页文件路径
- 使用 AssetDatabase.FindAssets 进行文件搜索
- 提供更详细的错误信息和解决建议
- 改进了启动服务器的错误处理逻辑

---

## 版本 1.0.3 (2025-01-27)

### 修复
- 彻底解决了所有 yield 在 try-catch 中的编译错误
- 修复了所有 GUID 冲突问题
- 重构了 HTTP 请求处理逻辑，完全避免在 try-catch 中使用 yield
- 为所有文件生成了唯一的 GUID

### 技术改进
- 重新设计了 HandleRequests() 方法，使用错误标志而不是 try-catch
- 重构了 ProcessRequest() 方法，将 yield 和错误处理分离
- 优化了 BuildAssetBundles() 方法，避免 try-catch 包装 yield
- 为所有 meta 文件生成了唯一的 GUID，解决冲突

---

## 版本 1.0.2 (2025-01-27)

### 修复
- 解决了 yield 在 try-catch 中的编译错误 (CS1626)
- 添加了 System.Linq 命名空间，修复 Sum() 方法错误
- 为所有文件生成了唯一的 GUID，解决冲突问题
- 移除了未使用的变量和事件，消除编译警告

### 技术改进
- 重构了 HTTP 请求处理逻辑，避免在 try-catch 中使用 yield
- 添加了必要的 using System.Linq 引用
- 为所有文件生成了唯一的 GUID
- 清理了未使用的代码，提高代码质量

---

## 版本 1.0.1 (2025-01-27)

### 修复
- 解决了 Editor 脚本无法引用 Runtime 脚本的问题
- 为所有文件添加了必要的 `.meta` 文件
- 将所有脚本移动到 Editor 文件夹，符合 Unity 包管理规范

### 文件结构变更
- 将 `AssetBundleWebServer.cs` 从 Runtime 移动到 Editor 文件夹
- 将 `AssetBundleManager.cs` 从 Runtime 移动到 Editor 文件夹
- 添加了 `修复说明.md` 及其 meta 文件
- 创建了 `CHANGELOG.md` 版本更新日志

---

## 版本 1.0.0 (2025-01-27)

### 首次发布
- 炫酷的 Web UI 界面：2025年科幻风格的用户界面
- 异步构建系统：不阻塞 Unity 编辑器的资源打包
- 实时统计功能：资源数量、大小、依赖关系分析
- 拖拽操作支持：从 Unity Project 窗口直接拖拽资源
- HTTP 服务器：内置 Web 服务器，支持实时通信
- 多平台支持：支持 Windows、Mac、Linux 平台

### 特色功能
- 赛博朋克美学：霓虹色彩、几何线条设计
- 玻璃拟态效果：半透明效果、模糊背景
- 动态交互：微动画、粒子效果
- 响应式布局：适配不同屏幕尺寸
