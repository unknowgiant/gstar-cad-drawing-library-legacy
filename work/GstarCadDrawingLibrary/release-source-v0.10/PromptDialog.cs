using System.Drawing;
using System.Windows.Forms;

namespace GstarCadDrawingLibrary
{
    internal sealed class PromptDialog : Form
    {
        private readonly TextBox valueBox;

        private PromptDialog(string title, string prompt, string initialValue)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(380, 118);
            Font = new Font("Microsoft YaHei UI", 9F);

            Label promptLabel = new Label();
            promptLabel.Text = prompt;
            promptLabel.AutoSize = false;
            promptLabel.Location = new Point(12, 12);
            promptLabel.Size = new Size(356, 22);

            valueBox = new TextBox();
            valueBox.Text = initialValue ?? string.Empty;
            valueBox.Location = new Point(12, 38);
            valueBox.Size = new Size(356, 23);

            Button okButton = new Button();
            okButton.Text = "确定";
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new Point(212, 76);
            okButton.Size = new Size(75, 28);

            Button cancelButton = new Button();
            cancelButton.Text = "取消";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(293, 76);
            cancelButton.Size = new Size(75, 28);

            Controls.Add(promptLabel);
            Controls.Add(valueBox);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        public static bool TryShow(IWin32Window owner, string title, string prompt, string initialValue, out string value)
        {
            using (PromptDialog dialog = new PromptDialog(title, prompt, initialValue))
            {
                if (dialog.ShowDialog(owner) == DialogResult.OK)
                {
                    value = dialog.valueBox.Text.Trim();
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}
