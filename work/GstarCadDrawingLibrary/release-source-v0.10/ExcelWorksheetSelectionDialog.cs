using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GstarCadDrawingLibrary
{
    internal sealed class ExcelWorksheetSelectionDialog : Form
    {
        private readonly ComboBox sheetBox;
        private readonly IList<ExcelSheetData> sheets;

        private ExcelWorksheetSelectionDialog(IList<ExcelSheetData> availableSheets)
        {
            sheets = availableSheets;
            Text = "选择 Excel 工作表";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(420, 126);
            Font = new Font("Microsoft YaHei UI", 9F);

            Label label = new Label();
            label.Text = "工作表";
            label.Location = new Point(12, 13);
            label.Size = new Size(396, 22);

            sheetBox = new ComboBox();
            sheetBox.DropDownStyle = ComboBoxStyle.DropDownList;
            sheetBox.Location = new Point(12, 39);
            sheetBox.Size = new Size(396, 25);
            foreach (ExcelSheetData sheet in sheets)
            {
                sheetBox.Items.Add(sheet.Name + "  (" + sheet.RowCount + " 行 × " + sheet.ColumnCount + " 列)");
            }
            sheetBox.SelectedIndex = 0;

            Button okButton = new Button();
            okButton.Text = "确定";
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new Point(252, 83);
            okButton.Size = new Size(75, 28);

            Button cancelButton = new Button();
            cancelButton.Text = "取消";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(333, 83);
            cancelButton.Size = new Size(75, 28);

            Controls.Add(label);
            Controls.Add(sheetBox);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        public static bool TrySelect(
            IWin32Window owner,
            IList<ExcelSheetData> sheets,
            out ExcelSheetData selectedSheet)
        {
            if (sheets.Count == 1)
            {
                selectedSheet = sheets[0];
                return true;
            }

            using (ExcelWorksheetSelectionDialog dialog = new ExcelWorksheetSelectionDialog(sheets))
            {
                if (dialog.ShowDialog(owner) == DialogResult.OK)
                {
                    selectedSheet = sheets[dialog.sheetBox.SelectedIndex];
                    return true;
                }
            }

            selectedSheet = null;
            return false;
        }
    }
}
