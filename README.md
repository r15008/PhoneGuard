# PhoneGuard

PhoneGuard 是一个供个人安装使用的 Windows + Android 双端安全检查工具。Android App 可独立检查已安装的用户应用；Windows 程序使用 Android Debug Bridge（ADB）通过 USB 调试或无线调试发起扫描、读取报告，并在手机上打开选中应用的系统信息页。

当前版本为 **0.1.0 预览版**。它检查应用申请的权限组合和安装来源，给出人工复查线索。它不扫描文件内容、不使用恶意软件签名库，也不能保证发现病毒或证明应用安全。分数不是病毒判定。卸载始终由用户在手机系统界面确认。

## 安装与使用

1. 安装 `PhoneGuard.apk`（Android 10 或更高版本）。在手机上打开 PhoneGuard，点击“扫描已安装应用”即可独立使用。
2. 在 Windows 上把 Google 官方 [SDK Platform Tools](https://developer.android.com/tools/releases/platform-tools) 解压为与 `PhoneGuard.exe` 同目录的 `platform-tools` 文件夹；也可以把 `adb.exe` 加入 PATH。
3. USB：在手机“开发者选项”启用“USB 调试”，连接电脑并在手机上同意授权。打开 EXE，点击“刷新设备”后扫描。
4. Wi-Fi：手机与电脑连接同一局域网，在手机“开发者选项 → 无线调试”选择“使用配对码配对设备”。把配对页面的地址与 6 位配对码输入 EXE 并点击“无线配对”；再把无线调试主页显示的连接地址输入“连接地址”，点击“无线连接”。
5. 选中报告中的应用，点击“在手机上查看选中应用”，可在手机系统界面进一步核实并卸载。

电脑端每次扫描会启动手机 App 并等待带本次请求编号的新报告。报告只保存在手机的 `Android/data/com.r15008.phoneguard/files/report.json`，不发送到云端。清除 App 数据或卸载 App 会删除报告。

## 从源码构建

- Android：JDK 17、Android SDK Platform 35、Build Tools 35、Gradle 8.7。运行 `gradlew.bat :android-app:assembleDebug`，APK 位于 `android-app/build/outputs/apk/debug/`。
- Windows：Windows 自带的 .NET Framework 4.x C# 编译器。运行 `powershell -ExecutionPolicy Bypass -File windows-app/build.ps1`，EXE 位于 `dist/`。

调试 APK 可直接安装；正式分发前应使用自己保管的签名密钥构建 release APK。不要把密钥、扫描报告或个人设备数据提交到仓库。

## 当前范围与限制

- 检查非系统应用（包括更新过的系统应用）的权限申请和安装来源。某些正常应用也会申请这些权限，所以存在误报。
- Android 的包可见性权限 `QUERY_ALL_PACKAGES` 用于安全检查。若提交 Google Play，需符合该权限的审核政策。
- 需要用户主动开启 USB 调试或无线调试，PC 端不绕过手机授权。
- 只在用户点击时扫描，没有后台监控和自动删除。
- 暂未在实体手机上验证；具体 Samsung 系统版本可能影响报告路径读取或无线调试体验。

