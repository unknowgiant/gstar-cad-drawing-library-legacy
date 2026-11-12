using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace GstarCadDrawingLibrary
{
    internal sealed class DrawingLibraryControl : UserControl
    {
        private static readonly string[] ThumbnailExtensions = { ".png", ".jpg", ".jpeg", ".bmp" };

        private readonly DrawingLibraryService libraryService;
        private readonly LibrarySettings settings;
        private readonly List<DrawingItem> allItems;
        private readonly ToolStrip toolStrip;
        private readonly TextBox searchBox;
        private readonly ComboBox categoryBox;
        private readonly ListView drawingList;
        private readonly ImageList imageList;
        private Label selectedLabel;
        private NumericUpDown scaleBox;
        private NumericUpDown rotationBox;
        private Button insertButton;
        private readonly Label statusLabel;

        public DrawingLibraryControl()
        {
            libraryService = new DrawingLibraryService();
            settings = LibrarySettings.Load();
            allItems = new List<DrawingItem>();

            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(245, 246, 247);
            MinimumSize = new Size(300, 420);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Margin = Padding.Empty;
            layout.Padding = Padding.Empty;
            layout.RowCount = 5;
            layout.ColumnCount = 1;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));

            toolStrip = BuildToolStrip();
            layout.Controls.Add(toolStrip, 0, 0);

            TableLayoutPanel filters = new TableLayoutPanel();
            filters.Dock = DockStyle.Fill;
            filters.Padding = new Padding(8, 4, 8, 3);
            filters.ColumnCount = 2;
            filters.RowCount = 2;
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            filters.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            filters.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            filters.Controls.Add(CreateFilterLabel("搜索"), 0, 0);
            searchBox = new TextBox();
            searchBox.Dock = DockStyle.Fill;
            searchBox.Margin = new Padding(0, 1, 0, 2);
            searchBox.TextChanged += delegate { ApplyFilter(); };
            filters.Controls.Add(searchBox, 1, 0);

            filters.Controls.Add(CreateFilterLabel("分类"), 0, 1);
            categoryBox = new ComboBox();
            categoryBox.Dock = DockStyle.Fill;
            categoryBox.DropDownStyle = ComboBoxStyle.DropDownList;
            categoryBox.Margin = new Padding(0, 1, 0, 1);
            categoryBox.SelectedIndexChanged += delegate { ApplyFilter(); };
            filters.Controls.Add(categoryBox, 1, 1);
            layout.Controls.Add(filters, 0, 1);

            imageList = new ImageList();
            imageList.ColorDepth = ColorDepth.Depth32Bit;
            imageList.ImageSize = new Size(112, 78);

            drawingList = new ListView();
            drawingList.Dock = DockStyle.Fill;
            drawingList.Margin = new Padding(8, 2, 8, 3);
            drawingList.View = View.LargeIcon;
            drawingList.LargeImageList = imageList;
            drawingList.MultiSelect = false;
            drawingList.HideSelection = false;
            drawingList.ShowItemToolTips = true;
            drawingList.BorderStyle = BorderStyle.FixedSingle;
            drawingList.BackColor = Color.White;
            drawingList.SelectedIndexChanged += OnSelectionChanged;
            drawingList.DoubleClick += delegate { RequestInsert(); };
            layout.Controls.Add(drawingList, 0, 2);

            TableLayoutPanel insertion = BuildInsertionPanel();
            layout.Controls.Add(insertion, 0, 3);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Padding = new Padding(8, 0, 4, 0);
            statusLabel.ForeColor = Color.FromArgb(75, 82, 89);
            statusLabel.BackColor = Color.FromArgb(232, 235, 237);
            layout.Controls.Add(statusLabel, 0, 4);

            Controls.Add(layout);
            ReloadLibrary();
        }

        public event EventHandler<InsertDrawingEventArgs> InsertRequested;

        public void ReloadLibrary()
        {
            string selectedCategory = GetSelectedCategory();
            statusLabel.Text = "正在扫描图库...";
            drawingList.BeginUpdate();

            try
            {
                allItems.Clear();
                allItems.AddRange(libraryService.Scan(settings.RootDirectory));
                PopulateCategories(selectedCategory);
                ApplyFilter();
            }
            catch (Exception ex)
            {
                drawingList.Items.Clear();
                ClearImages();
                statusLabel.Text = "读取失败：" + ex.Message;
            }
            finally
            {
                drawingList.EndUpdate();
            }
        }

        public void ChooseRootDirectory()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择图纸库根目录";
                dialog.SelectedPath = settings.RootDirectory;
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                settings.RootDirectory = dialog.SelectedPath;
                settings.Save();
                ReloadLibrary();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearImages();
                imageList.Dispose();
            }

            base.Dispose(disposing);
        }

        private ToolStrip BuildToolStrip()
        {
            ToolStrip strip = new ToolStrip();
            strip.Dock = DockStyle.Fill;
            strip.GripStyle = ToolStripGripStyle.Hidden;
            strip.BackColor = Color.FromArgb(236, 239, 241);
            strip.Padding = new Padding(5, 2, 5, 2);
            strip.Items.Add(CreateToolButton("目录", "选择图库根目录", delegate { ChooseRootDirectory(); }));
            strip.Items.Add(CreateToolButton("刷新", "重新扫描图库", delegate { ReloadLibrary(); }));
            strip.Items.Add(new ToolStripSeparator());
            ToolStripDropDownButton categoryMenu = new ToolStripDropDownButton("分类");
            categoryMenu.ToolTipText = "管理图库分类";
            categoryMenu.DropDownItems.Add("新建", null, delegate { CreateCategory(); });
            categoryMenu.DropDownItems.Add("改名", null, delegate { RenameSelectedCategory(); });
            categoryMenu.DropDownItems.Add("删除", null, delegate { DeleteSelectedCategory(); });
            strip.Items.Add(categoryMenu);
            strip.Items.Add(CreateToolButton("添加", "向当前分类添加 DWG", delegate { AddDrawings(); }));
            strip.Items.Add(CreateToolButton("改名", "修改所选图纸名称", delegate { RenameSelectedDrawing(); }));
            strip.Items.Add(CreateToolButton("删除", "从图库删除所选图纸", delegate { DeleteSelectedDrawing(); }));
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(CreateToolButton("打开", "在资源管理器中打开图库", delegate { OpenRootDirectory(); }));
            return strip;
        }

        private TableLayoutPanel BuildInsertionPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(8, 6, 8, 6);
            panel.BackColor = Color.White;
            panel.ColumnCount = 4;
            panel.RowCount = 2;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            panel.Controls.Add(CreateFilterLabel("比例"), 0, 0);
            scaleBox = new NumericUpDown();
            scaleBox.Dock = DockStyle.Fill;
            scaleBox.DecimalPlaces = 3;
            scaleBox.Minimum = 0.001M;
            scaleBox.Maximum = 100000M;
            scaleBox.Value = 1M;
            panel.Controls.Add(scaleBox, 1, 0);

            panel.Controls.Add(CreateFilterLabel("角度"), 2, 0);
            rotationBox = new NumericUpDown();
            rotationBox.Dock = DockStyle.Fill;
            rotationBox.DecimalPlaces = 1;
            rotationBox.Minimum = -360000M;
            rotationBox.Maximum = 360000M;
            rotationBox.Value = 0M;
            panel.Controls.Add(rotationBox, 3, 0);

            selectedLabel = new Label();
            selectedLabel.Dock = DockStyle.Fill;
            selectedLabel.Text = "未选择图纸";
            selectedLabel.TextAlign = ContentAlignment.MiddleLeft;
            selectedLabel.AutoEllipsis = true;
            selectedLabel.ForeColor = Color.FromArgb(52, 59, 64);
            panel.Controls.Add(selectedLabel, 0, 1);
            panel.SetColumnSpan(selectedLabel, 3);

            insertButton = new Button();
            insertButton.Dock = DockStyle.Fill;
            insertButton.Text = "插入";
            insertButton.Enabled = false;
            insertButton.FlatStyle = FlatStyle.Flat;
            insertButton.FlatAppearance.BorderColor = Color.FromArgb(34, 122, 86);
            insertButton.BackColor = Color.FromArgb(38, 139, 98);
            insertButton.ForeColor = Color.White;
            insertButton.Click += delegate { RequestInsert(); };
            panel.Controls.Add(insertButton, 3, 1);
            return panel;
        }

        private static Label CreateFilterLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = Color.FromArgb(64, 70, 75);
            return label;
        }

        private static ToolStripButton CreateToolButton(string text, string toolTip, Action action)
        {
            ToolStripButton button = new ToolStripButton(text);
            button.DisplayStyle = ToolStripItemDisplayStyle.Text;
            button.ToolTipText = toolTip;
            button.Click += delegate { action(); };
            return button;
        }

        private void PopulateCategories(string preferredCategory)
        {
            categoryBox.BeginUpdate();
            categoryBox.Items.Clear();
            categoryBox.Items.Add(new CategoryEntry(string.Empty, "全部分类"));

            foreach (string category in libraryService.GetCategories(settings.RootDirectory))
            {
                categoryBox.Items.Add(new CategoryEntry(category, category.Replace("\\", " / ")));
            }

            int selectedIndex = 0;
            for (int i = 0; i < categoryBox.Items.Count; i++)
            {
                CategoryEntry entry = categoryBox.Items[i] as CategoryEntry;
                if (entry != null && string.Equals(entry.RelativePath, preferredCategory, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                    break;
                }
            }

            categoryBox.SelectedIndex = selectedIndex;
            categoryBox.EndUpdate();
        }

        private void ApplyFilter()
        {
            if (drawingList == null || imageList == null || categoryBox == null)
            {
                return;
            }

            string query = searchBox.Text.Trim();
            string category = GetSelectedCategory();
            IEnumerable<DrawingItem> filtered = allItems.Where(delegate(DrawingItem item)
            {
                bool categoryMatches = string.IsNullOrEmpty(category)
                    || string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase);
                bool queryMatches = string.IsNullOrEmpty(query)
                    || item.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0
                    || item.CategoryDisplay.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
                return categoryMatches && queryMatches;
            });

            drawingList.BeginUpdate();
            drawingList.Items.Clear();
            drawingList.Groups.Clear();
            ClearImages();

            Dictionary<string, ListViewGroup> groups = new Dictionary<string, ListViewGroup>(StringComparer.CurrentCultureIgnoreCase);
            int count = 0;
            foreach (DrawingItem item in filtered)
            {
                string groupName = item.CategoryDisplay;
                ListViewGroup group;
                if (!groups.TryGetValue(groupName, out group))
                {
                    group = new ListViewGroup(groupName, HorizontalAlignment.Left);
                    groups.Add(groupName, group);
                    drawingList.Groups.Add(group);
                }

                int imageIndex = imageList.Images.Count;
                imageList.Images.Add(LoadThumbnail(item));
                ListViewItem listItem = new ListViewItem(item.Name, imageIndex, group);
                listItem.Tag = item;
                listItem.ToolTipText = item.CategoryDisplay + Environment.NewLine + item.FilePath;
                drawingList.Items.Add(listItem);
                count++;
            }

            drawingList.EndUpdate();
            statusLabel.Text = string.Format("{0} 个图纸  |  {1}", count, settings.RootDirectory);
            OnSelectionChanged(this, EventArgs.Empty);
        }

        private Image LoadThumbnail(DrawingItem item)
        {
            if (!string.IsNullOrEmpty(item.ThumbnailPath) && File.Exists(item.ThumbnailPath))
            {
                try
                {
                    using (FileStream stream = File.Open(item.ThumbnailPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (Image original = Image.FromStream(stream))
                    {
                        return new Bitmap(original);
                    }
                }
                catch
                {
                    // A damaged preview should not hide an otherwise valid drawing.
                }
            }

            Bitmap placeholder = new Bitmap(imageList.ImageSize.Width, imageList.ImageSize.Height);
            using (Graphics graphics = Graphics.FromImage(placeholder))
            using (SolidBrush background = new SolidBrush(Color.FromArgb(226, 230, 232)))
            using (SolidBrush foreground = new SolidBrush(Color.FromArgb(79, 88, 94)))
            using (Font titleFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold))
            using (Font extensionFont = new Font("Segoe UI", 8F, FontStyle.Regular))
            {
                graphics.FillRectangle(background, 0, 0, placeholder.Width, placeholder.Height);
                graphics.DrawRectangle(Pens.Silver, 0, 0, placeholder.Width - 1, placeholder.Height - 1);
                string title = item.Name.Length > 8 ? item.Name.Substring(0, 8) : item.Name;
                SizeF titleSize = graphics.MeasureString(title, titleFont);
                graphics.DrawString(title, titleFont, foreground, (placeholder.Width - titleSize.Width) / 2F, 22F);
                graphics.DrawString("DWG", extensionFont, foreground, 6F, placeholder.Height - 20F);
            }

            return placeholder;
        }

        private void ClearImages()
        {
            foreach (Image image in imageList.Images)
            {
                image.Dispose();
            }

            imageList.Images.Clear();
        }

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            DrawingItem item = GetSelectedItem();
            insertButton.Enabled = item != null;
            selectedLabel.Text = item == null
                ? "未选择图纸"
                : item.Name + "  ·  " + item.CategoryDisplay;
        }

        private void RequestInsert()
        {
            DrawingItem item = GetSelectedItem();
            if (item == null)
            {
                return;
            }

            EventHandler<InsertDrawingEventArgs> handler = InsertRequested;
            if (handler != null)
            {
                handler(this, new InsertDrawingEventArgs(
                    item.FilePath,
                    decimal.ToDouble(scaleBox.Value),
                    decimal.ToDouble(rotationBox.Value)));
            }
        }

        private DrawingItem GetSelectedItem()
        {
            return drawingList.SelectedItems.Count == 1
                ? drawingList.SelectedItems[0].Tag as DrawingItem
                : null;
        }

        private string GetSelectedCategory()
        {
            CategoryEntry entry = categoryBox.SelectedItem as CategoryEntry;
            return entry == null ? string.Empty : entry.RelativePath;
        }

        private void CreateCategory()
        {
            string name;
            if (!PromptDialog.TryShow(this, "新建分类", "分类名称", string.Empty, out name))
            {
                return;
            }

            if (!IsValidSimpleName(name))
            {
                MessageBox.Show(this, "分类名称不能为空，且不能包含路径或文件名非法字符。", "新建分类", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string parent = GetSelectedCategory();
            string relative = string.IsNullOrEmpty(parent) ? name : Path.Combine(parent, name);
            Directory.CreateDirectory(Path.Combine(settings.RootDirectory, relative));
            ReloadLibrary();
            SelectCategory(relative);
        }

        private void AddDrawings()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "添加图纸到图库";
                dialog.Filter = "DWG 图纸 (*.dwg)|*.dwg";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                string category = GetSelectedCategory();
                string destinationDirectory = string.IsNullOrEmpty(category)
                    ? settings.RootDirectory
                    : Path.Combine(settings.RootDirectory, category);
                Directory.CreateDirectory(destinationDirectory);

                foreach (string source in dialog.FileNames)
                {
                    string destination = Path.Combine(destinationDirectory, Path.GetFileName(source));
                    if (File.Exists(destination))
                    {
                        DialogResult overwrite = MessageBox.Show(
                            this,
                            Path.GetFileName(source) + " 已存在，是否覆盖？",
                            "添加图纸",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);
                        if (overwrite != DialogResult.Yes)
                        {
                            continue;
                        }
                    }

                    File.Copy(source, destination, true);
                    CopyPairedThumbnail(source, destination);
                }

                ReloadLibrary();
            }
        }

        private void RenameSelectedCategory()
        {
            string category = GetSelectedCategory();
            if (string.IsNullOrEmpty(category))
            {
                MessageBox.Show(this, "请先选择要改名的分类。", "分类改名", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string currentName = Path.GetFileName(category);
            string newName;
            if (!PromptDialog.TryShow(this, "分类改名", "新名称", currentName, out newName))
            {
                return;
            }

            if (!IsValidSimpleName(newName))
            {
                MessageBox.Show(this, "分类名称不能为空，且不能包含路径或文件名非法字符。", "分类改名", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string parent = Path.GetDirectoryName(category);
            string newRelativePath = string.IsNullOrEmpty(parent) ? newName : Path.Combine(parent, newName);
            string sourcePath = Path.Combine(settings.RootDirectory, category);
            string destinationPath = Path.Combine(settings.RootDirectory, newRelativePath);
            if (Directory.Exists(destinationPath))
            {
                MessageBox.Show(this, "目标分类已经存在。", "分类改名", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Directory.Move(sourcePath, destinationPath);
            ReloadLibrary();
            SelectCategory(newRelativePath);
        }

        private void DeleteSelectedCategory()
        {
            string category = GetSelectedCategory();
            if (string.IsNullOrEmpty(category))
            {
                MessageBox.Show(this, "请先选择要删除的分类。", "删除分类", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string directory = Path.Combine(settings.RootDirectory, category);
            int drawingCount = Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*.dwg", SearchOption.AllDirectories).Length
                : 0;
            DialogResult result = MessageBox.Show(
                this,
                "确定删除分类“" + category.Replace("\\", " / ") + "”吗？其中的 " + drawingCount + " 个图纸及其他文件都会被删除。",
                "删除分类",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
            {
                return;
            }

            Directory.Delete(directory, true);
            ReloadLibrary();
        }

        private void RenameSelectedDrawing()
        {
            DrawingItem item = GetSelectedItem();
            if (item == null)
            {
                return;
            }

            string newName;
            if (!PromptDialog.TryShow(this, "图纸改名", "新名称", item.Name, out newName))
            {
                return;
            }

            if (!IsValidSimpleName(newName))
            {
                MessageBox.Show(this, "图纸名称不能为空，且不能包含路径或文件名非法字符。", "图纸改名", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string newPath = Path.Combine(Path.GetDirectoryName(item.FilePath), newName + ".dwg");
            if (File.Exists(newPath))
            {
                MessageBox.Show(this, "目标名称已经存在。", "图纸改名", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            File.Move(item.FilePath, newPath);
            if (!string.IsNullOrEmpty(item.ThumbnailPath) && File.Exists(item.ThumbnailPath))
            {
                string newThumbnail = Path.Combine(
                    Path.GetDirectoryName(item.ThumbnailPath),
                    newName + Path.GetExtension(item.ThumbnailPath));
                File.Move(item.ThumbnailPath, newThumbnail);
            }

            ReloadLibrary();
        }

        private void DeleteSelectedDrawing()
        {
            DrawingItem item = GetSelectedItem();
            if (item == null)
            {
                return;
            }

            DialogResult result = MessageBox.Show(
                this,
                "确定从图库删除“" + item.Name + "”吗？此操作不会删除已经插入图纸的块。",
                "删除图纸",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
            {
                return;
            }

            File.Delete(item.FilePath);
            if (!string.IsNullOrEmpty(item.ThumbnailPath) && File.Exists(item.ThumbnailPath))
            {
                File.Delete(item.ThumbnailPath);
            }

            ReloadLibrary();
        }

        private void OpenRootDirectory()
        {
            Directory.CreateDirectory(settings.RootDirectory);
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = "explorer.exe";
            info.Arguments = "\"" + settings.RootDirectory + "\"";
            info.UseShellExecute = false;
            Process.Start(info);
        }

        private void SelectCategory(string relativePath)
        {
            for (int i = 0; i < categoryBox.Items.Count; i++)
            {
                CategoryEntry entry = categoryBox.Items[i] as CategoryEntry;
                if (entry != null && string.Equals(entry.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
                {
                    categoryBox.SelectedIndex = i;
                    return;
                }
            }
        }

        private static bool IsValidSimpleName(string name)
        {
            return !string.IsNullOrWhiteSpace(name)
                && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && name.IndexOf(Path.DirectorySeparatorChar) < 0
                && name.IndexOf(Path.AltDirectorySeparatorChar) < 0;
        }

        private static void CopyPairedThumbnail(string sourceDrawing, string destinationDrawing)
        {
            string sourceBase = Path.Combine(Path.GetDirectoryName(sourceDrawing), Path.GetFileNameWithoutExtension(sourceDrawing));
            string destinationBase = Path.Combine(Path.GetDirectoryName(destinationDrawing), Path.GetFileNameWithoutExtension(destinationDrawing));
            foreach (string extension in ThumbnailExtensions)
            {
                string sourceThumbnail = sourceBase + extension;
                if (File.Exists(sourceThumbnail))
                {
                    File.Copy(sourceThumbnail, destinationBase + extension, true);
                    return;
                }
            }
        }

        private sealed class CategoryEntry
        {
            public CategoryEntry(string relativePath, string displayName)
            {
                RelativePath = relativePath;
                DisplayName = displayName;
            }

            public string RelativePath { get; private set; }
            public string DisplayName { get; private set; }

            public override string ToString()
            {
                return DisplayName;
            }
        }
    }
}
