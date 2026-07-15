using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace 醫療訊號期末報告
{
    public partial class InterForm : Form
    {
        public InterForm()
        {
            InitializeComponent();
        }
        private void 美化首頁()
        {
            // ===== 上方漸層區（深綠）=====
            guna2GradientPanel1.FillColor = Color.FromArgb(27, 72, 66);
            guna2GradientPanel1.FillColor2 = Color.FromArgb(45, 122, 104);
            guna2GradientPanel1.BackColor = Color.FromArgb(33, 90, 80);
            // ===== 左上關閉鈕 =====
            guna2CircleButton2.Size = new Size(20, 20);
            guna2CircleButton2.FillColor = Color.FromArgb(45, 212, 160);
            guna2CircleButton2.BackColor = Color.Transparent;
            guna2CircleButton2.Text = "";

            // ===== 圓形進度條（超透明）=====
            guna2CircleProgressBar1.FillThickness = 8;
            guna2CircleProgressBar1.FillColor = Color.FromArgb(20, 255, 255, 255);  // 超透明
            guna2CircleProgressBar1.BackColor = Color.Transparent;

            // ===== 內層圓形按鈕（白色）=====
            guna2CircleButton1.Text = "";
            guna2CircleButton1.Enabled = false;
            guna2CircleButton1.BorderThickness = 0;

            // ===== 大標題 =====
            guna2HtmlLabel30.Font = new Font("Microsoft JhengHei", 26F, FontStyle.Bold);
            guna2HtmlLabel30.ForeColor = Color.White;
            guna2HtmlLabel30.BackColor = Color.Transparent;
            guna2HtmlLabel30.Text = "健康監測系統";
            // ===== 英文小標題 =====
            guna2HtmlLabel1.Font = new Font("Segoe UI", 12F, FontStyle.Regular);
            guna2HtmlLabel1.ForeColor = Color.FromArgb(168, 212, 201);
            guna2HtmlLabel1.BackColor = Color.Transparent;
            guna2HtmlLabel1.Text = "Health Monitoring System";
            // ===== 開始檢測 =====
            guna2GradientButton1.FillColor = Color.FromArgb(33, 140, 111);
            guna2GradientButton1.FillColor2 = Color.FromArgb(45, 181, 115);
            guna2GradientButton1.BorderRadius = 16;
            guna2GradientButton1.Font = new Font("Microsoft JhengHei", 18F, FontStyle.Bold);
            guna2GradientButton1.ForeColor = Color.White;
            guna2GradientButton1.Text = "開始檢測";
            guna2GradientButton1.ShadowDecoration.Enabled = true;
            // ===== 管理者登入 =====
            guna2GradientButton2.FillColor = Color.White;
            guna2GradientButton2.FillColor2 = Color.White;
            guna2GradientButton2.BorderRadius = 16;
            guna2GradientButton2.BorderThickness = 2;
            guna2GradientButton2.BorderColor = Color.FromArgb(45, 122, 104);
            guna2GradientButton2.Font = new Font("Microsoft JhengHei", 18F, FontStyle.Bold);
            guna2GradientButton2.ForeColor = Color.FromArgb(27, 72, 66);
            guna2GradientButton2.Text = "管理者登入";

            // ===== 按鈕提示 =====
            ToolTip tooltip = new ToolTip();
            tooltip.OwnerDraw = true;

            tooltip.Popup += (s, e) =>
            {
                e.ToolTipSize = new Size(80, 40);
            };

            tooltip.Draw += (s, e) =>
            {
                e.DrawBackground();
                e.DrawBorder();

                Font font = new Font("Microsoft JhengHei", 12F, FontStyle.Bold);
                StringFormat format = new StringFormat();
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;

                e.Graphics.DrawString(e.ToolTipText, font, Brushes.Black, e.Bounds, format);
            };

            tooltip.SetToolTip(guna2CircleButton2, "關閉應用");  // 黄色按钮
        }
        private void guna2CircleButton2_Click(object sender, EventArgs e)
        {
            this.Close();

            Application.Exit();
        }
        private void InterForm_Load(object sender, EventArgs e)
        {
            美化首頁();

        }

        private void guna2GradientButton1_Click(object sender, EventArgs e)
        {
            MainForm mainForm = new MainForm(0);  // 0 = Tab1
            mainForm.Show();
            this.Hide();
        }

        private void guna2GradientButton2_Click(object sender, EventArgs e)
        {
            string password = PromptPassword("請輸入管理者密碼：");

            if (password == "123456")
            {
                MainForm mainForm = new MainForm(5);
                mainForm.Show();
                this.Hide();
            }
            else if (password != "")
            {
                MessageBox.Show("密碼錯誤", "登入失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        private string PromptPassword(string prompt)
        {
            Form promptForm = new Form()
            {
                Text = "管理者登入",
                Width = 320,
                Height = 180,  // ⬅️ 改成 180（原本 140）
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label label = new Label()
            {
                Left = 20,
                Top = 15,
                Text = prompt,
                Width = 260,
                Height = 25
            };

            TextBox textBox = new TextBox()
            {
                Left = 20,
                Top = 45,
                Width = 270,
                Height = 30,
                UseSystemPasswordChar = true,
                Font = new System.Drawing.Font("Arial", 12)
            };

            Button okBtn = new Button()
            {
                Text = "確認",
                Left = 120,
                Width = 70,
                Height = 35,
                Top = 90,  // ⬅️ 也改一下
                DialogResult = DialogResult.OK
            };

            Button cancelBtn = new Button()
            {
                Text = "取消",
                Left = 200,
                Width = 70,
                Height = 35,
                Top = 90,
                DialogResult = DialogResult.Cancel
            };

            promptForm.Controls.Add(label);
            promptForm.Controls.Add(textBox);
            promptForm.Controls.Add(okBtn);
            promptForm.Controls.Add(cancelBtn);
            promptForm.AcceptButton = okBtn;
            promptForm.CancelButton = cancelBtn;

            return promptForm.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }
    }
}