package com.r15008.phoneguard;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.provider.Settings;
import android.view.Gravity;
import android.view.View;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.TextView;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.File;
import java.io.FileOutputStream;
import java.nio.charset.StandardCharsets;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.Date;
import java.util.List;
import java.util.Locale;
import java.util.TimeZone;

public class MainActivity extends Activity {
    private final List<AppResult> results = new ArrayList<>();
    private ArrayAdapter<String> adapter;
    private TextView status;
    private Button scanButton;
    private boolean scanning;

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        buildUi();
        maybeScan(getIntent());
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        maybeScan(intent);
    }

    private void maybeScan(Intent intent) {
        if (intent != null && intent.getBooleanExtra("scan", false)) {
            startScan(intent.getStringExtra("requestId"));
            intent.removeExtra("scan");
        }
    }

    private void buildUi() {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(24, 28, 24, 20);
        root.setBackgroundColor(Color.rgb(247, 249, 252));

        TextView title = new TextView(this);
        title.setText("PhoneGuard");
        title.setTextSize(28);
        title.setTextColor(Color.rgb(25, 43, 65));
        root.addView(title);

        TextView subtitle = new TextView(this);
        subtitle.setText("检查应用权限与安装来源。结果是风险线索，需要人工核实。\\n点击条目可查看应用信息或发起系统卸载确认。");
        subtitle.setTextSize(14);
        subtitle.setPadding(0, 8, 0, 18);
        root.addView(subtitle);

        scanButton = new Button(this);
        scanButton.setText("扫描已安装应用");
        scanButton.setOnClickListener(v -> startScan(null));
        root.addView(scanButton);

        status = new TextView(this);
        status.setText("准备就绪。无需连接电脑即可扫描。");
        status.setTextSize(14);
        status.setPadding(0, 14, 0, 14);
        root.addView(status);

        ListView list = new ListView(this);
        adapter = new ArrayAdapter<>(this, android.R.layout.simple_list_item_1, new ArrayList<>());
        list.setAdapter(adapter);
        list.setOnItemClickListener((parent, view, position, id) -> showApp(results.get(position)));
        root.addView(list, new LinearLayout.LayoutParams(-1, 0, 1));

        TextView foot = new TextView(this);
        foot.setText("报告保存在本应用的外部专属目录，供电脑端通过 USB 或无线调试读取。");
        foot.setTextSize(12);
        foot.setGravity(Gravity.CENTER);
        root.addView(foot);
        setContentView(root);
    }

    private void startScan(String requestId) {
        if (scanning) return;
        scanning = true;
        scanButton.setEnabled(false);
        status.setText("正在扫描…");
        new Thread(() -> {
            try {
                List<AppResult> scanned = scanPackages();
                String json = makeReport(scanned, requestId);
                File directory = getExternalFilesDir(null);
                if (directory == null) throw new IllegalStateException("无法访问应用报告目录");
                File report = new File(directory, "report.json");
                try (FileOutputStream out = new FileOutputStream(report)) {
                    out.write(json.getBytes(StandardCharsets.UTF_8));
                }
                runOnUiThread(() -> {
                    results.clear();
                    results.addAll(scanned);
                    adapter.clear();
                    int review = 0;
                    for (AppResult item : results) {
                        if (item.score >= 30) review++;
                        adapter.add(item.label + "  ·  " + level(item.score) + " (" + item.score + ")\\n" + item.packageName);
                    }
                    adapter.notifyDataSetChanged();
                    status.setText("已检查 " + scanned.size() + " 个用户应用；" + review + " 个建议复查。报告已保存。");
                    scanning = false;
                    scanButton.setEnabled(true);
                });
            } catch (Exception e) {
                runOnUiThread(() -> {
                    status.setText("扫描失败：" + e.getMessage());
                    scanning = false;
                    scanButton.setEnabled(true);
                });
            }
        }, "phoneguard-scan").start();
    }

    private List<AppResult> scanPackages() {
        PackageManager pm = getPackageManager();
        List<PackageInfo> packages = pm.getInstalledPackages(PackageManager.GET_PERMISSIONS);
        List<AppResult> out = new ArrayList<>();
        for (PackageInfo pkg : packages) {
            ApplicationInfo info = pkg.applicationInfo;
            if (info == null || getPackageName().equals(pkg.packageName)) continue;
            if ((info.flags & ApplicationInfo.FLAG_SYSTEM) != 0 &&
                (info.flags & ApplicationInfo.FLAG_UPDATED_SYSTEM_APP) == 0) continue;

            AppResult app = new AppResult();
            app.packageName = pkg.packageName;
            app.label = pm.getApplicationLabel(info).toString();
            String installer = pm.getInstallerPackageName(pkg.packageName);
            app.installer = installer == null ? "未知/手动安装" : installer;
            String[] perms = pkg.requestedPermissions == null ? new String[0] : pkg.requestedPermissions;
            boolean sms = has(perms, "READ_SMS") || has(perms, "RECEIVE_SMS") || has(perms, "SEND_SMS");
            boolean internet = has(perms, "INTERNET");
            if (sms && internet) {
                app.score += 35;
                app.reasons.add("申请短信权限并可联网");
            }
            if (has(perms, "REQUEST_INSTALL_PACKAGES")) {
                app.score += 25;
                app.reasons.add("可请求安装其他应用");
            }
            if (has(perms, "SYSTEM_ALERT_WINDOW")) {
                app.score += 20;
                app.reasons.add("申请悬浮窗权限");
            }
            if (has(perms, "RECEIVE_BOOT_COMPLETED")) {
                app.score += 10;
                app.reasons.add("可在开机时启动");
            }
            if (installer == null || !(installer.equals("com.android.vending") ||
                    installer.equals("com.sec.android.app.samsungapps"))) {
                app.score += 15;
                app.reasons.add("安装来源不是 Google Play 或 Galaxy Store");
            }
            if (app.score == 0) app.reasons.add("未发现当前规则覆盖的风险组合");
            app.score = Math.min(app.score, 100);
            out.add(app);
        }
        Collections.sort(out, Comparator.comparingInt((AppResult x) -> x.score).reversed()
                .thenComparing(x -> x.label.toLowerCase(Locale.ROOT)));
        return out;
    }

    private boolean has(String[] permissions, String suffix) {
        String full = "android.permission." + suffix;
        for (String permission : permissions) if (full.equals(permission)) return true;
        return false;
    }

    private String makeReport(List<AppResult> apps, String requestId) throws Exception {
        JSONObject report = new JSONObject();
        report.put("schemaVersion", 1);
        report.put("requestId", requestId == null ? JSONObject.NULL : requestId);
        SimpleDateFormat date = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", Locale.US);
        date.setTimeZone(TimeZone.getTimeZone("UTC"));
        report.put("generatedAt", date.format(new Date()));
        report.put("device", Build.MANUFACTURER + " " + Build.MODEL);
        JSONArray entries = new JSONArray();
        for (AppResult app : apps) {
            JSONObject item = new JSONObject();
            item.put("name", app.label);
            item.put("packageName", app.packageName);
            item.put("riskScore", app.score);
            item.put("level", level(app.score));
            item.put("installer", app.installer);
            item.put("reasons", new JSONArray(app.reasons));
            entries.put(item);
        }
        report.put("packages", entries);
        return report.toString();
    }

    private String level(int score) {
        return score >= 60 ? "优先复查" : score >= 30 ? "建议复查" : "低线索";
    }

    private void showApp(AppResult app) {
        StringBuilder message = new StringBuilder(app.packageName + "\n安装来源：" + app.installer + "\n\n");
        for (String reason : app.reasons) message.append("• ").append(reason).append("\n");
        new AlertDialog.Builder(this)
                .setTitle(app.label + " · " + level(app.score))
                .setMessage(message.toString())
                .setPositiveButton("应用信息", (d, w) -> {
                    Intent intent = new Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                            Uri.parse("package:" + app.packageName));
                    startActivity(intent);
                })
                .setNeutralButton("卸载…", (d, w) -> {
                    Intent intent = new Intent(Intent.ACTION_DELETE,
                            Uri.parse("package:" + app.packageName));
                    startActivity(intent);
                })
                .setNegativeButton("关闭", null)
                .show();
    }

    private static class AppResult {
        String label;
        String packageName;
        String installer;
        int score;
        List<String> reasons = new ArrayList<>();
    }
}

