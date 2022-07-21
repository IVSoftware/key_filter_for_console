using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NoteEditor
{
    public partial class NoteEditorForm : Form
    {
        public NoteEditorForm()
        {
            InitializeComponent();
            textBoxNoteEditor.KeyDown += (sender, e) =>
            {
                switch (e.KeyData)
                {
                    case Keys.Control | Keys.S:
                        e.Handled = e.SuppressKeyPress = true;
                        saveAndClose(textBoxNoteEditor.Rtf);
                        break;
                    case Keys.Escape:
                        e.Handled = e.SuppressKeyPress = true;
                        saveAndClose($"{DialogResult.Cancel}");
                        break;
                    case Keys.Control | Keys.X:
                        e.Handled = e.SuppressKeyPress = true;
                        saveAndClose($"{DialogResult.Abort}");
                        break;
                }
            };
        }
        protected override void OnVisibleChanged(EventArgs e)
        {
            if(Visible)
            {
                TopMost = true;
                Location = MousePosition;
                BeginInvoke((MethodInvoker)delegate 
                {
                    textBoxNoteEditor.Focus();
                });

                var fi = new FileInfo(_notePath);
                _dtInit = fi.LastWriteTime;
                Task.Run(async () =>
                {
                    while (true)
                    {
                        if (new FileInfo(_notePath).LastWriteTime > _dtInit)
                        {
                            var text = await File.ReadAllTextAsync(_notePath);
                            switch (text)
                            {
                                case "Cancel":
                                    Close();
                                    return;
                            }
                        }
                        await Task.Delay(250);
                    }
                });
            }
            base.OnVisibleChanged(e);
        }
        static DateTime _dtInit = DateTime.MinValue;
        private void buttonSave_Click(object sender, EventArgs e) => saveAndClose();
        private void buttonCancel_Click(object sender, EventArgs e) => saveAndClose($"{DialogResult.Cancel}");
        private void saveAndClose(string dialogResult)
        {
            File.WriteAllText(_notePath, dialogResult);
            Close();
        }
        private void saveAndClose()
        {
            File.WriteAllText(_notePath, JsonConvert.SerializeObject(
                new Note
                {
                    Rtf = textBoxNoteEditor.Rtf,
                    Text = textBoxNoteEditor.Text,
                }));
            ;
            Close();
        }

        private static string _notePath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "key_filter_for_console",
                "note.rtf");
    }
    class Note
    {
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();
        public string Rtf { get; set; }
        public string Text { get; set; }
    }
}
