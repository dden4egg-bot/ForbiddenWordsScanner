using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ForbiddenWordsScanner
{
    public class MainForm : Form
    {
        private ScannerEngine _engine = new ScannerEngine();

        // UI Elements
        private TextBox txtFolderPath;
        private Button btnBrowse;
        private TextBox txtWordInput;
        private Button btnAddWord;
        private ListBox lstWords;
        private ProgressBar progressBar;
        private Label lblProgressPercent;
        private Button btnStart, btnPause, btnStop;
        private RichTextBox rtbLog;
        private DataGridView dgvTop10;

        public MainForm()
        {
            InitializeUI();

            _engine.ProgressChanged += (val) => this.Invoke(new Action(() => {
                progressBar.Value = val;
                lblProgressPercent.Text = $"{val}%";
            }));

            _engine.LogChanged += (msg) => this.Invoke(new Action(() => {
                rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            }));

            _engine.ScanCompleted += (stats) => this.Invoke(new Action(() => ShowReport(stats)));

            rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Application started.{Environment.NewLine}");
        }

        private void InitializeUI()
        {
            this.Text = "Forbidden Words Scanner";
            this.Size = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            // --- Left Side ---
            Label lblFolder = new Label { Text = "Folder Path:", Location = new Point(20, 20), AutoSize = true };
            txtFolderPath = new TextBox { Location = new Point(20, 45), Width = 300 };
            btnBrowse = new Button { Text = "Browse", Location = new Point(330, 43), Width = 80 };
            btnBrowse.Click += btnBrowse_Click;

            Label lblWord = new Label { Text = "Word:", Location = new Point(20, 80), AutoSize = true };
            txtWordInput = new TextBox { Location = new Point(20, 105), Width = 300 };
            btnAddWord = new Button { Text = "Add Word", Location = new Point(330, 103), Width = 80 };
            btnAddWord.Click += btnAddWord_Click;

            Label lblProgress = new Label { Text = "Progress:", Location = new Point(20, 150), AutoSize = true };
            progressBar = new ProgressBar { Location = new Point(80, 148), Width = 250 };
            lblProgressPercent = new Label { Text = "0%", Location = new Point(340, 150), AutoSize = true };

            btnStart = new Button { Text = "▶ Start", Location = new Point(20, 190), Width = 100, Height = 35, BackColor = Color.LightGreen };
            btnStart.Click += btnStart_Click;

            btnPause = new Button { Text = "⏸ Pause", Location = new Point(130, 190), Width = 100, Height = 35 };
            btnPause.Click += btnPause_Click;

            btnStop = new Button { Text = "⏹ Stop", Location = new Point(240, 190), Width = 100, Height = 35, BackColor = Color.LightCoral };
            btnStop.Click += btnStop_Click;

            Label lblLog = new Label { Text = "Log:", Location = new Point(20, 240), AutoSize = true };
            rtbLog = new RichTextBox { Location = new Point(20, 265), Width = 400, Height = 250, ReadOnly = true };

            // --- Right Side ---
            Label lblWordsList = new Label { Text = "Words List:", Location = new Point(450, 20), AutoSize = true };
            lstWords = new ListBox { Location = new Point(450, 45), Width = 400, Height = 150 };

            Label lblTop10 = new Label { Text = "Top 10 Results:", Location = new Point(450, 210), AutoSize = true };
            dgvTop10 = new DataGridView { Location = new Point(450, 235), Width = 400, Height = 280 };

            dgvTop10.AutoGenerateColumns = false;
            dgvTop10.Columns.Add(new DataGridViewTextBoxColumn { Name = "Index", HeaderText = "#", Width = 40 });
            dgvTop10.Columns.Add(new DataGridViewTextBoxColumn { Name = "Word", HeaderText = "Word", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvTop10.Columns.Add(new DataGridViewTextBoxColumn { Name = "Count", HeaderText = "Count", Width = 80 });
            dgvTop10.AllowUserToAddRows = false;
            dgvTop10.ReadOnly = true;

            this.Controls.Add(lblFolder);
            this.Controls.Add(txtFolderPath);
            this.Controls.Add(btnBrowse);
            this.Controls.Add(lblWord);
            this.Controls.Add(txtWordInput);
            this.Controls.Add(btnAddWord);
            this.Controls.Add(lblProgress);
            this.Controls.Add(progressBar);
            this.Controls.Add(lblProgressPercent);
            this.Controls.Add(btnStart);
            this.Controls.Add(btnPause);
            this.Controls.Add(btnStop);
            this.Controls.Add(lblLog);
            this.Controls.Add(rtbLog);
            this.Controls.Add(lblWordsList);
            this.Controls.Add(lstWords);
            this.Controls.Add(lblTop10);
            this.Controls.Add(dgvTop10);
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                txtFolderPath.Text = dialog.SelectedPath;
                _engine.Log($"Folder selected: {dialog.SelectedPath}");
            }
        }

        private void btnAddWord_Click(object sender, EventArgs e)
        {
            string word = txtWordInput.Text.Trim();
            if (!string.IsNullOrEmpty(word) && !lstWords.Items.Contains(word))
            {
                lstWords.Items.Add(word);
                txtWordInput.Clear();
            }
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFolderPath.Text) || lstWords.Items.Count == 0)
            {
                MessageBox.Show("Please select a folder and add forbidden words.", "Error");
                return;
            }

            rtbLog.Clear();
            dgvTop10.Rows.Clear();
            progressBar.Value = 0;
            lblProgressPercent.Text = "0%";

            _engine.Log($"Words list loaded ({lstWords.Items.Count} words).");
            _engine.Log("Ready to start scanning...");

            string outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ScanResults");

            string[] words = new string[lstWords.Items.Count];
            lstWords.Items.CopyTo(words, 0);

            await _engine.StartScanAsync(txtFolderPath.Text, words, outputFolder);
        }

        private void btnPause_Click(object sender, EventArgs e)
        {
            _engine.Pause();
            _engine.Log("Scan paused.");
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _engine.Stop();
            _engine.Log("Scan stopped by user.");
        }

        private void ShowReport(Dictionary<string, int> top10)
        {
            dgvTop10.Rows.Clear();
            int index = 1;
            foreach (var kv in top10)
            {
                dgvTop10.Rows.Add(index++, kv.Key, kv.Value);
            }
            MessageBox.Show("Scan completed! Check Desktop for 'ScanResults' folder.", "Success");
        }
    }
}