PhoneGuard 0.1.0 是第一个可安装的双端预览版。

- Android App 可独立检查已安装的用户应用，列出权限申请和安装来源的风险线索。
- Windows EXE 通过 USB 调试或无线调试启动扫描、读取报告，并可在手机上打开选中应用的系统信息页。
- 下载 EXE 和 APK 后，请按仓库 [README](https://github.com/r15008/PhoneGuard#readme) 的步骤准备 Google 官方 ADB。

这不是完整的病毒查杀引擎：当前没有病毒签名库、文件内容扫描或后台监控。APK 使用调试签名，EXE 未做代码签名；尚未在实体 Samsung 手机上验证。复查或卸载应用时，请结合应用用途判断，卸载操作由手机系统确认。

下载后可使用随附的 `SHA256SUMS.txt` 核对文件。
