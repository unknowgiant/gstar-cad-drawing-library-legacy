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

如果此前已经加载过旧版插件，请先完全关闭并重新启动浩辰 CAD，再加载新版 DLL。浩辰 CAD 在当前进程中不会卸载已经加载的旧程序集。

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
- `DLSELLAYER`：按指定对象的图层快速选择。
- `DLSELBLOCK`：按指定块的名称快速选择。
- `DLSELCOLOR`：按指定对象的颜色属性快速选择。
- `DLSELTYPE`：按指定对象的实体类型快速选择。
- `DLLAYERISOLATE`：隔离指定对象所在图层。
- `DLLAYERRESTORE`：恢复隔离前的图层显示状态。
- `DLLAYERFREEZE`：冻结指定对象所在图层。
- `DLLAYERLOCK`：锁定指定对象所在图层。
- `DLLAYERUNLOCKALL`：解锁当前图纸全部图层。
- `DLTEXTPREFIX`：给所选单行或多行文字添加前缀。
- `DLTEXTSUFFIX`：给所选单行或多行文字添加后缀。
- `DLTEXTHEIGHT`：统一修改所选文字高度。
- `DLTEXTWIDTH`：修改所选单行文字宽度系数。
- `DLTEXTSWAP`：互换所选文字中两个指定位置的等长字符组，位置从 1 开始。
- `DLTEXTNUMBER`：按从上到下、从左到右的顺序生成递增编号。
- `DLBLOCKCOUNT`：按块名统计所选块参照数量。
- `DLLENGTHSUM`：统计所选曲线总长度。
- `DLAREASUM`：统计封闭曲线、面域及填充总面积。
- `DLTEXTSUM`：提取所选文字中的数字并求和。
- `DLBLOCKCREATE`：将所选对象转换为新块，并在原位置插入块参照。
- `DLBLOCKRENAME`：修改所选块的定义名称。
- `DLBLOCKREPLACE`：替换当前空间中的同名块，并保留同名属性值。
- `DLBLOCKSCALE`：按指定倍数批量缩放块参照。
- `DLBLOCKROTATE`：按增量角度批量旋转块参照。
- `DLBLOCKZERO`：将所选块定义内的实体统一置于 0 层。
- `DLBLOCKCOLOR`：修改所选块定义内实体的颜色。
- `DLBLOCKBRUSH`：复制块参照的比例、角度、图层和颜色。

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

面板中的“批量添加”支持在文件窗口中使用 `Ctrl`、`Shift` 或框选一次选择多张 DWG。同名图纸只会集中询问一次，可选择全部覆盖或全部跳过。

## 图库功能

- 优先使用同名 PNG/JPG/BMP；没有同名图片时自动读取 DWG 内嵌缩略图并缓存。
- 可以把多张 DWG 从资源管理器直接拖到图库面板。
- “移动”可以将所选图纸移动到其他分类，并同步移动同名预览图。
- “最近使用”保留最近插入的 20 张图纸；改名、移动和删除时会自动同步。

## 辅助工具

打开 `DLIB` 后切换到“辅助工具”页签，可以直接使用快速选择、图层、文字、统计和图块工具，不需要记忆命令。快速选择作用于当前空间；图层隔离会记录原有开关状态，使用“恢复”即可还原。统计结果显示在 CAD 命令行中。

“字符互换”依次输入两个起始位置和互换长度。例如文字 `AB-1234` 输入位置 `1`、`4`、长度 `2` 后得到 `12-AB34`。带格式控制码的多行文字会跳过，以免破坏原有格式。

块替换只处理当前空间中的同名块参照，并保留目标块中存在的同名属性值。“置零层”和“块内改色”会修改块定义，因此会同时影响该块的全部实例。
