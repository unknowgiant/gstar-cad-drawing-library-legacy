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
            layout.RowCount = 5;

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

            statusLabel = new Label();
            statusLabel.AutoSize = false;
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.Height = 30;
            statusLabel.Padding = new Padding(3, 6, 3, 3);
            statusLabel.ForeColor = Color.FromArgb(75, 82, 89);
            statusLabel.Text = "就绪";
            layout.Controls.Add(statusLabel, 0, 4);

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
