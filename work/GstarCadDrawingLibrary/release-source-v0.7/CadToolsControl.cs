using System;
using System.Drawing;
using System.Windows.Forms;
using Gssoft.Gscad.ApplicationServices;
using GcadApplication = Gssoft.Gscad.ApplicationServices.Application;

namespace GstarCadDrawingLibrary
{
    internal sealed class CadToolsControl : UserControl
    {
        private readonly ToolTip toolTip;
        private readonly Label statusLabel;

        public CadToolsControl()
        {
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(245, 246, 247);
            AutoScroll = true;
            toolTip = new ToolTip();

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Top;
            layout.AutoSize = true;
            layout.Padding = new Padding(10, 8, 10, 8);
            layout.ColumnCount = 1;
            layout.RowCount = 11;

            layout.Controls.Add(CreateHeading("快速选择"), 0, 0);
            layout.Controls.Add(CreateCommandPanel(new CommandButton[]
            {
                new CommandButton("按图层", "DLSELLAYER", "选择与指定对象处于同一图层的对象"),
                new CommandButton("按块名", "DLSELBLOCK", "选择与指定块同名的块参照"),
                new CommandButton("按颜色", "DLSELCOLOR", "选择颜色属性与指定对象相同的对象"),
                new CommandButton("按类型", "DLSELTYPE", "选择与指定对象类型相同的对象")
            }), 0, 1);

            layout.Controls.Add(CreateHeading("图层工具"), 0, 2);
            layout.Controls.Add(CreateCommandPanel(new CommandButton[]
            {
                new CommandButton("隔离", "DLLAYERISOLATE", "只显示指定对象所在的图层"),
                new CommandButton("恢复", "DLLAYERRESTORE", "恢复隔离前的图层显示状态"),
                new CommandButton("冻结", "DLLAYERFREEZE", "冻结指定对象所在的图层"),
                new CommandButton("锁定", "DLLAYERLOCK", "锁定指定对象所在的图层"),
                new CommandButton("全部解锁", "DLLAYERUNLOCKALL", "解锁当前图纸的全部图层")
            }), 0, 3);

            layout.Controls.Add(CreateHeading("文字工具"), 0, 4);
            layout.Controls.Add(CreateCommandPanel(new CommandButton[]
            {
                new CommandButton("加前缀", "DLTEXTPREFIX", "给所选单行或多行文字添加前缀"),
                new CommandButton("加后缀", "DLTEXTSUFFIX", "给所选单行或多行文字添加后缀"),
                new CommandButton("字高", "DLTEXTHEIGHT", "统一修改所选文字高度"),
                new CommandButton("字宽", "DLTEXTWIDTH", "修改所选单行文字宽度系数"),
                new CommandButton("字符互换", "DLTEXTSWAP", "互换所选文字中两个指定位置的等长字符组"),
                new CommandButton("递增编号", "DLTEXTNUMBER", "按位置顺序生成递增文字编号")
            }), 0, 5);

            layout.Controls.Add(CreateHeading("统计工具"), 0, 6);
            layout.Controls.Add(CreateCommandPanel(new CommandButton[]
            {
                new CommandButton("块数量", "DLBLOCKCOUNT", "按块名统计所选块参照数量"),
                new CommandButton("线长", "DLLENGTHSUM", "计算所选曲线的总长度"),
                new CommandButton("面积", "DLAREASUM", "计算封闭曲线、面域或填充的总面积"),
                new CommandButton("文字求和", "DLTEXTSUM", "提取所选文字中的数字并求和")
            }), 0, 7);

            layout.Controls.Add(CreateHeading("图块工具"), 0, 8);
            layout.Controls.Add(CreateCommandPanel(new CommandButton[]
            {
                new CommandButton("快速建块", "DLBLOCKCREATE", "将所选对象转换为一个新块参照"),
                new CommandButton("块改名", "DLBLOCKRENAME", "修改所选块的定义名称"),
                new CommandButton("块替换", "DLBLOCKREPLACE", "替换当前空间同名块并保留同名属性值"),
                new CommandButton("块缩放", "DLBLOCKSCALE", "按倍数批量缩放所选块参照"),
                new CommandButton("块旋转", "DLBLOCKROTATE", "按增量角度批量旋转所选块参照"),
                new CommandButton("置零层", "DLBLOCKZERO", "将所选块定义内的实体设为 0 层"),
                new CommandButton("块内改色", "DLBLOCKCOLOR", "修改所选块定义内实体的颜色"),
                new CommandButton("属性刷", "DLBLOCKBRUSH", "复制块参照的比例、角度、图层和颜色")
            }), 0, 9);

            statusLabel = new Label();
            statusLabel.AutoSize = false;
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.Height = 30;
            statusLabel.Padding = new Padding(3, 6, 3, 3);
            statusLabel.ForeColor = Color.FromArgb(75, 82, 89);
            statusLabel.Text = "就绪";
            layout.Controls.Add(statusLabel, 0, 10);

            Controls.Add(layout);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                toolTip.Dispose();
            }

            base.Dispose(disposing);
        }

        private static Label CreateHeading(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.Height = 30;
            label.Dock = DockStyle.Top;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            label.ForeColor = Color.FromArgb(52, 59, 64);
            return label;
        }

        private FlowLayoutPanel CreateCommandPanel(CommandButton[] commands)
        {
            FlowLayoutPanel panel = new FlowLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.AutoSize = true;
            panel.WrapContents = true;
            panel.Margin = new Padding(0, 0, 0, 10);
            panel.Padding = new Padding(0);

            foreach (CommandButton command in commands)
            {
                Button button = new Button();
                button.Text = command.Text;
                button.Tag = command.Command;
                button.Size = new Size(94, 32);
                button.Margin = new Padding(0, 0, 6, 6);
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Color.FromArgb(180, 186, 190);
                button.BackColor = Color.White;
                button.ForeColor = Color.FromArgb(43, 50, 55);
                button.Click += OnCommandClick;
                toolTip.SetToolTip(button, command.ToolTip);
                panel.Controls.Add(button);
            }

            return panel;
        }

        private void OnCommandClick(object sender, EventArgs e)
        {
            Button button = sender as Button;
            string command = button == null ? null : button.Tag as string;
            Document document = GcadApplication.DocumentManager.MdiActiveDocument;
            if (document == null || string.IsNullOrEmpty(command))
            {
                statusLabel.Text = "当前没有打开的图纸";
                return;
            }

            statusLabel.Text = "已启动：" + button.Text;
            document.SendStringToExecute(command + " ", true, false, false);
        }

        private sealed class CommandButton
        {
            public CommandButton(string text, string command, string toolTip)
            {
                Text = text;
                Command = command;
                ToolTip = toolTip;
            }

            public string Text { get; private set; }
            public string Command { get; private set; }
            public string ToolTip { get; private set; }
        }
    }
}
