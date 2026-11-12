using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GstarCadDrawingLibrary
{
    internal sealed class CategorySelectionDialog : Form
    {
        private readonly ComboBox categoryBox;
        private readonly IList<string> categories;

        private CategorySelectionDialog(IList<string> availableCategories, string currentCategory)
        {
            categories = availableCategories;
            Text = "移动图纸";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(400, 126);
            Font = new Font("Microsoft YaHei UI", 9F);

            Label label = new Label();
            label.Text = "目标分类";
            label.Location = new Point(12, 13);
            label.Size = new Size(376, 22);

            categoryBox = new ComboBox();
            categoryBox.DropDownStyle = ComboBoxStyle.DropDownList;
            categoryBox.Location = new Point(12, 39);
            categoryBox.Size = new Size(376, 25);
            categoryBox.Items.Add("未分类（图库根目录）");
            foreach (string category in categories)
            {
                categoryBox.Items.Add(category.Replace("\\", " / "));
            }

            categoryBox.SelectedIndex = 0;
            for (int i = 0; i < categories.Count; i++)
            {
                if (string.Equals(categories[i], currentCategory, System.StringComparison.OrdinalIgnoreCase))
                {
                    categoryBox.SelectedIndex = i + 1;
                    break;
                }
            }

            Button okButton = new Button();
            okButton.Text = "移动";
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new Point(232, 83);
            okButton.Size = new Size(75, 28);

            Button cancelButton = new Button();
            cancelButton.Text = "取消";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(313, 83);
            cancelButton.Size = new Size(75, 28);

            Controls.Add(label);
            Controls.Add(categoryBox);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        public static bool TrySelect(
            IWin32Window owner,
            IList<string> categories,
            string currentCategory,
            out string selectedCategory)
        {
            using (CategorySelectionDialog dialog = new CategorySelectionDialog(categories, currentCategory))
            {
                if (dialog.ShowDialog(owner) == DialogResult.OK)
                {
                    selectedCategory = dialog.categoryBox.SelectedIndex <= 0
                        ? string.Empty
                        : categories[dialog.categoryBox.SelectedIndex - 1];
                    return true;
                }
            }

            selectedCategory = null;
            return false;
        }
    }
}
