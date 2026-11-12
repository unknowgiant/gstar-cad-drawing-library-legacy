# 浩辰 CAD 2024 图纸库插件

## 环境

- 浩辰 CAD 2024，64 位
- .NET Framework 4.8
- C#

## 构建

在 PowerShell 中运行：

```powershell
.\build.ps1
```

如果系统提示禁止运行脚本，使用一次性执行方式（不会修改系统策略）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

如果浩辰 CAD 安装目录不同：

```powershell
.\build.ps1 -GstarCadInstallDir "D:\你的浩辰安装目录"
```

生成文件位于 `package` 目录。

## 加载

1. 启动浩辰 CAD 2024。
2. 输入 `FILEDIA`，回车后输入 `1`，确保加载时弹出文件选择窗口。
3. 输入 `NETLOAD`。
4. 在弹出的窗口中选择 `GstarCadDrawingLibrary.dll`。
5. 命令行出现“浩辰图纸库已加载”后，输入 `DLIB` 打开图纸库面板。

注意：`DLIB` 是插件加载成功后执行的命令，不能在“程序集文件名”提示后输入。如果没有弹出文件选择窗口，应输入 DLL 的完整路径，而不是输入 `DLIB`。

## 命令

- `DLIB`：打开图纸库面板。
- `DLREFRESH`：重新扫描图库。
- `DLSETROOT`：选择图库根目录。
- `DLINSERT`：从文件选择窗口直接插入 DWG。
- `DLSELFTEST`：运行面板与宿主连接诊断。

## 图库结构

文件夹即分类。DWG 与同名 PNG、JPG、JPEG 或 BMP 放在同一目录时，插件会显示该图片作为预览。

```text
GstarCAD 图纸库/
  建筑/
    门窗/
      单扇门.dwg
      单扇门.png
  电气/
    灯具/
      吸顶灯.dwg
      吸顶灯.png
```

首次运行默认在“文档”目录创建 `GstarCAD 图纸库`。也可以在面板中选择任意图库目录。
