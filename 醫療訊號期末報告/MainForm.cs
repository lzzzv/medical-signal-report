using System;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using myTools;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using Guna.UI2.WinForms;
using System.Data;

namespace 醫療訊號期末報告
{
    public partial class MainForm : Form
    {
        // 兩個連線：生理板(USB) + 環境板(藍牙)
        SerialPort portPhysio;   // COM7 生理板
        SerialPort portEnv;      // COM8 環境板
        bool 顯示生理 = false;   // Toggle 控制
        bool 顯示環境 = false;
        bool 環境連線中 = false;
        bool 生理連線中 = false;
        bool 忽略開關事件 = false;
        bool 生理曾連線 = false;
        bool 已儲存 = false;
        DateTime 生理最後收到 = DateTime.MinValue;
        DateTime 環境最後收到 = DateTime.MinValue;

        Form 遮罩層;


        // 生理數值
        int hr = 0;
        int spo2 = 0;
        int ecg = 0;
        // ECG 繪圖用
        int[] ecgData = new int[300];   // 存最近 300 個點
        int ecgPos = 0;                  // 目前寫到第幾個
        Image img;

        // 環境數值
        float temp = 0;
        float humidity = 0;
        float objTemp = 0;
        int distance = 0;
        int motion = 0;

        private int 初始TabIndex = 0;
        private int 上一個TabIndex = 0;
        public MainForm(int tabIndex = 0)
        {
            InitializeComponent();
            初始TabIndex = tabIndex;
            this.Scale(new SizeF(1.5f, 1.3f));   // 寬放大1.5倍、長1.3倍
        }


        private void 畫ECG()
        {
            int w = guna2PictureBox1.Width;
            int h = guna2PictureBox1.Height;
            if (w <= 0 || h <= 0) return;

            Bitmap bmp = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(13, 31, 28));

                // 網格
                using (Pen 網格 = new Pen(Color.FromArgb(22, 53, 48)))
                {
                    for (int x = 0; x < w; x += 25)
                        g.DrawLine(網格, x, 0, x, h);
                    for (int y = 0; y < h; y += 25)
                        g.DrawLine(網格, 0, y, w, y);
                }

                // 上下兩條分隔線
                using (Pen 分線 = new Pen(Color.FromArgb(45, 212, 122), 1))
                {
                    g.DrawLine(分線, 0, 40, w, 40);          // 上分線（標題下方）
                    g.DrawLine(分線, 0, h - 40, w, h - 40);  // 下分線（對稱留白）
                }

                // 標題文字
                using (Font 標題字 = new Font("Segoe UI", 12F, FontStyle.Bold))
                using (Brush 綠刷 = new SolidBrush(Color.FromArgb(45, 212, 122)))
                {
                    g.DrawString("ECG 心電圖 (即時)", 標題字, 綠刷, 10, 8);
                }

                // 判斷有沒有資料（最近的值是否都接近0或沒變化）
                bool 有資料 = false;
                for (int i = 0; i < ecgData.Length; i++)
                    if (ecgData[i] > 10) { 有資料 = true; break; }

                if (!有資料)
                {
                    // 沒資料：顯示「未偵測」
                    using (Font f = new Font("Microsoft JhengHei", 14F, FontStyle.Bold))
                    using (Brush 灰刷 = new SolidBrush(Color.FromArgb(100, 130, 120)))
                    {
                        string s = "未偵測";
                        SizeF sz = g.MeasureString(s, f);
                        g.DrawString(s, f, 灰刷, (w - sz.Width) / 2, (h - sz.Height) / 2);
                    }
                }
                else
                {
                    // 畫波形
                    using (Pen 線 = new Pen(Color.FromArgb(45, 212, 122), 2))
                    {
                        int 點數 = ecgData.Length;
                        for (int i = 0; i < 點數 - 1; i++)
                        {
                            int x1 = i * w / 點數;
                            int x2 = (i + 1) * w / 點數;
                            int y1 = h - (ecgData[i] * (h - 50) / 1024) - 10;
                            int y2 = h - (ecgData[i + 1] * (h - 50) / 1024) - 10;
                            g.DrawLine(線, x1, y1, x2, y2);
                        }
                    }

                }
                // 底部資訊文字
                using (Font 小字 = new Font("Segoe UI", 9F))
                using (Brush 綠刷2 = new SolidBrush(Color.FromArgb(100, 160, 140)))
                {
                    g.DrawString("掃描速度 25mm/s    增益 10mm/mV", 小字, 綠刷2, 10, h - 32);
                }
            }

            if (guna2PictureBox1.Image != null)
                guna2PictureBox1.Image.Dispose();

            // 製造圓角效果：四角塗背景色
            using (Graphics g2 = Graphics.FromImage(bmp))
            {

                int r = 15;  // 圓角半徑
                Color 背景 = Color.White;  // PictureBox 外面的底色
                using (Brush b = new SolidBrush(背景))
                {
                    // 左上角
                    g2.FillRectangle(b, 0, 0, r, r);
                    g2.FillRectangle(b, w - r, 0, r, r);      // 右上
                    g2.FillRectangle(b, 0, h - r, r, r);      // 左下
                    g2.FillRectangle(b, w - r, h - r, r, r);  // 右下
                }
                // 再用背景色畫圓角的圓弧補回去
                using (Brush bk = new SolidBrush(Color.FromArgb(13, 31, 28)))
                {
                    g2.FillPie(bk, 0, 0, r * 2, r * 2, 180, 90);
                    g2.FillPie(bk, w - r * 2, 0, r * 2, r * 2, 270, 90);
                    g2.FillPie(bk, 0, h - r * 2, r * 2, r * 2, 90, 90);
                    g2.FillPie(bk, w - r * 2, h - r * 2, r * 2, r * 2, 0, 90);
                }
            }
            guna2PictureBox1.Image = bmp;
        }
        // 輸入管理者密碼的對話框
        private string PromptPassword(string prompt)
        {
            Form promptForm = new Form()
            {
                Text = "管理者登入",
                Width = 320,
                Height = 180,
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
                Top = 90,
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
        private void MainForm_Load(object sender, EventArgs e)
        {
            guna2TabControl1.Selecting -= guna2TabControl1_Selecting;

            SQLiteService.初始化();
            美化卡片();
            畫ECG();

            if (初始TabIndex >= 3)
            {
                if (!guna2TabControl1.TabPages.Contains(tabPage6))
                {
                    guna2TabControl1.TabPages.Add(tabPage6);
                }
            }
            else
            {
                if (guna2TabControl1.TabPages.Contains(tabPage6))
                {
                    guna2TabControl1.TabPages.Remove(tabPage6);
                }
            }

            int targetIndex = 初始TabIndex;
            if (targetIndex >= guna2TabControl1.TabCount)
            {
                targetIndex = guna2TabControl1.TabCount - 1;
            }

            guna2TabControl1.SelectedIndex = targetIndex;
            上一個TabIndex = targetIndex;

            顯示資料紀錄();
        }

        private void 顯示資料紀錄()
        {
            try
            {
                DataTable dtPhysio = SQLiteService.查詢生理數據();

                dataGridView生理.DataSource = null;
                dataGridView生理.DataSource = dtPhysio;

                dataGridView生理.ReadOnly = true;
                dataGridView生理.AllowUserToAddRows = false;
                dataGridView生理.AllowUserToDeleteRows = false;
                dataGridView生理.Font = new Font("Microsoft JhengHei", 11F);
                dataGridView生理.RowTemplate.Height = 25;
                dataGridView生理.ColumnHeadersHeight = 35;
                dataGridView生理.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft JhengHei", 11F, FontStyle.Bold);
                dataGridView生理.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(33, 92, 81);
                dataGridView生理.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;

                // 自動調整欄寬
                dataGridView生理.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);

                // 異常資料整列標紅
                foreach (DataGridViewRow row in dataGridView生理.Rows)
                {
                    try
                    {
                        int spo2 = int.Parse(row.Cells["血氧"].Value?.ToString() ?? "0");
                        int hr = int.Parse(row.Cells["心率"].Value?.ToString() ?? "0");

                        if ((spo2 < 95 && spo2 > 0) || (hr < 50 || hr > 70))
                        {
                            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 200, 200);
                            row.DefaultCellStyle.ForeColor = Color.Red;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("错误：" + ex.Message);
            }
        }
        private void 美化卡片()
        {

            // 時鐘樣式
            guna2HtmlLabel27.Font = new Font("Segoe UI", 15F);
            guna2HtmlLabel27.ForeColor = Color.White;  // 深綠配淺綠導覽列
            guna2HtmlLabel27.BackColor = Color.Transparent;
            // ===== 頂部導覽列 =====
            // 背景橫條：深綠、加圓角
            guna2Panel8.FillColor = Color.FromArgb(205, 224, 218);   // 柔和一點的深綠
            guna2Panel8.BorderRadius = 12;                         // 圓角

            // 兩個連線狀態燈：圓形，預設紅色（未連線）
            void 設定燈(Guna.UI2.WinForms.Guna2Panel 燈)
            {
                燈.Size = new Size(14, 14);
                燈.BorderRadius = 7;
                燈.BorderThickness = 0;
                燈.ShadowDecoration.Enabled = false;
                燈.FillColor = Color.FromArgb(255, 60, 60);  // 預設紅（未連線）
                燈.BackColor = Color.FromArgb(205, 224, 218);  // 角落色與導覽列同色，隱藏方角
            }
            設定燈(guna2Panel9);   // 環境板燈
            設定燈(guna2Panel10);  // 生理板燈

            void 設定開關(Guna.UI2.WinForms.Guna2ToggleSwitch sw)
            {
                sw.CheckedState.FillColor = Color.FromArgb(34, 181, 115);   // 開：綠
                sw.UncheckedState.FillColor = Color.FromArgb(255, 60, 60);  // 關：紅
                sw.BackColor = Color.FromArgb(205, 224, 218);  // 角落色與導覽列同色，隱藏方角
            }
            設定開關(guna2ToggleSwitch1);
            設定開關(guna2ToggleSwitch2);

            // 開關旁的文字
            void 設定開關文字(Guna.UI2.WinForms.Guna2HtmlLabel lbl)
            {
                lbl.Font = new Font("Microsoft JhengHei", 11F, FontStyle.Bold);
                lbl.ForeColor = Color.FromArgb(33, 92, 81);  // 深綠，配淺綠導覽列
                lbl.BackColor = Color.Transparent;
            }
            設定開關文字(guna2HtmlLabel24);  // 生理板
            設定開關文字(guna2HtmlLabel25);  // 環境板

            void 設定標題(Guna.UI2.WinForms.Guna2HtmlLabel lbl)
            {
                lbl.Font = new Font("Segoe UI", 24F, FontStyle.Bold);   // 加粗、大一點
                lbl.ForeColor = Color.FromArgb(70, 70, 70);             // 深灰，明顯
                lbl.BackColor = Color.Transparent;
                lbl.AutoSize = true;
            }

            // 導覽列文字：深綠色
            void 設定導覽文字(Guna.UI2.WinForms.Guna2HtmlLabel lbl)
            {
                lbl.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
                lbl.ForeColor = Color.FromArgb(33, 92, 81);  // 深綠字
                lbl.BackColor = Color.Transparent;
            }
            設定導覽文字(guna2HtmlLabel15);  // 環境文字
            設定導覽文字(guna2HtmlLabel16);  // 生理文字

            // ===== 按鈕_清空數據庫 =====
            按鈕_清空數據庫.FillColor = Color.FromArgb(226, 59, 59);       // 紅
            按鈕_清空數據庫.FillColor2 = Color.FromArgb(255, 100, 100);    // 淡紅
            按鈕_清空數據庫.BorderRadius = 10;
            按鈕_清空數據庫.Font = new Font("Microsoft JhengHei", 12F, FontStyle.Bold);
            按鈕_清空數據庫.ForeColor = Color.White;
            按鈕_清空數據庫.BorderThickness = 0;
            按鈕_清空數據庫.Text = "刪除單筆資料";
            按鈕_清空數據庫.ShadowDecoration.Enabled = true;
            按鈕_清空數據庫.ShadowDecoration.Depth = 5;

            // ===== 美化 TabControl =====
            guna2TabControl1.TabButtonSize = new Size(160, 44);

            // 選中：深綠底、白字、粗大
            guna2TabControl1.TabButtonSelectedState.FillColor = Color.FromArgb(33, 92, 81);
            guna2TabControl1.TabButtonSelectedState.ForeColor = Color.White;
            guna2TabControl1.TabButtonSelectedState.Font = new Font("Microsoft JhengHei", 16F, FontStyle.Bold);

            // 沒選中：透明底、深色粗字
            guna2TabControl1.TabButtonIdleState.FillColor = Color.Transparent;
            guna2TabControl1.TabButtonIdleState.ForeColor = Color.FromArgb(80, 80, 80);
            guna2TabControl1.TabButtonIdleState.Font = new Font("Microsoft JhengHei", 16F, FontStyle.Bold);

            // 滑鼠移上去：淺綠
            guna2TabControl1.TabButtonHoverState.FillColor = Color.FromArgb(205, 224, 218);
            guna2TabControl1.TabButtonHoverState.ForeColor = Color.FromArgb(33, 92, 81);
            guna2TabControl1.TabButtonHoverState.Font = new Font("Microsoft JhengHei", 16F, FontStyle.Bold);

            // 滑鼠移上去：中間調的淺綠
            guna2TabControl1.TabButtonHoverState.FillColor = Color.FromArgb(205, 224, 218);
            guna2TabControl1.TabButtonHoverState.ForeColor = Color.FromArgb(33, 92, 81);

            // ===== tabPage6 美化 =====
            tabPage6.BackColor = Color.FromArgb(240, 243, 245);

            // 加兩個 Label
            Guna.UI2.WinForms.Guna2HtmlLabel lbl1 = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lbl1.Text = "生理數據";
            lbl1.Font = new Font("Microsoft JhengHei", 14F, FontStyle.Bold);
            lbl1.Location = new Point(10, 10);
            lbl1.BackColor = Color.Transparent;
            tabPage6.Controls.Add(lbl1);

            Guna.UI2.WinForms.Guna2HtmlLabel lbl2 = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lbl2.Text = "環境數據";
            lbl2.Font = new Font("Microsoft JhengHei", 14F, FontStyle.Bold);
            lbl2.Location = new Point(10, 280);
            lbl2.BackColor = Color.Transparent;
            tabPage6.Controls.Add(lbl2);

            // 通用卡片樣式設定函式
            void 設定卡片(Guna.UI2.WinForms.Guna2Panel panel, Color 邊框色, Color 底色)
            {
                panel.FillColor = 底色;
                panel.BorderRadius = 15;
                panel.BorderColor = 邊框色;
                panel.BorderThickness = 1;
                panel.ShadowDecoration.Enabled = true;
                panel.ShadowDecoration.Depth = 8;
            }
            // ===== 生理頁 =====
            設定卡片(guna2Panel7, Color.FromArgb(226, 59, 59), Color.FromArgb(253, 245, 245));  // 心率 紅框+淺紅底
            設定卡片(guna2Panel6, Color.FromArgb(42, 123, 216), Color.FromArgb(243, 248, 253)); // 血氧 藍框+淺藍底

            // 通用數值文字樣式
            void 設定數值(Guna.UI2.WinForms.Guna2HtmlLabel lbl, Color 顏色)
            {
                lbl.Font = new Font("Segoe UI", 48F, FontStyle.Bold);
                lbl.ForeColor = 顏色;
                lbl.TextRenderingHint =
                    System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;  // 抗鋸齒
            }
            // ===== 生理頁 =====
            設定數值(guna2HtmlLabel4, Color.FromArgb(226, 59, 59));  // 心率 紅字
            設定數值(guna2HtmlLabel2, Color.FromArgb(42, 123, 216)); // 血氧 藍字
            guna2HtmlLabel2.Font = new Font("Segoe UI", 48F, FontStyle.Bold);  // 血氧放大
            guna2HtmlLabel4.Font = new Font("Segoe UI", 48F, FontStyle.Bold);  // 心率放大
            // ===== 環境頁 =====
            設定卡片(guna2Panel1, Color.FromArgb(200, 135, 26), Color.FromArgb(253, 250, 243));  // 溫度 橘框+淺橘底
            設定卡片(guna2Panel2, Color.FromArgb(19, 163, 122), Color.FromArgb(243, 252, 249));  // 溼度 綠框+淺綠底
            設定卡片(guna2Panel3, Color.FromArgb(226, 59, 59), Color.FromArgb(253, 245, 245));   // 體溫 紅框+淺紅底
            設定卡片(guna2Panel4, Color.FromArgb(90, 79, 199), Color.FromArgb(248, 247, 253));   // 距離 紫框+淺紫底
            設定卡片(guna2Panel5, Color.FromArgb(26, 155, 84), Color.FromArgb(244, 252, 247));   // 人體 綠框+淺綠底
            // 狀態燈設成圓形（卡片底是白色，所以角落色設白）
            void 設定狀態燈(Guna.UI2.WinForms.Guna2Panel 燈)
            {
                燈.Size = new Size(16, 16);
                燈.BorderRadius = 8;
                燈.BorderThickness = 0;
                燈.ShadowDecoration.Enabled = false;
                燈.BackColor = Color.White;   // 卡片底色是白，角落融進白色
                燈.FillColor = Color.FromArgb(120, 120, 120);  // 預設灰
            }
            設定狀態燈(guna2Panel11);  // 血氧
            設定狀態燈(guna2Panel12);  // 心率
            設定狀態燈(guna2Panel13);  // 體溫

            // 狀態文字樣式
            void 設定狀態字(Guna.UI2.WinForms.Guna2HtmlLabel lbl)
            {
                lbl.Font = new Font("Microsoft JhengHei", 11F, FontStyle.Bold);
                lbl.BackColor = Color.Transparent;
            }
            設定狀態字(guna2HtmlLabel23);
            設定狀態字(guna2HtmlLabel26);
            設定狀態字(guna2HtmlLabel29);

            設定標題(guna2HtmlLabel1);   // 血氧標題
            guna2HtmlLabel1.Font = new Font("Segoe UI", 24F, FontStyle.Bold);  // 血氧標題放大
            設定標題(guna2HtmlLabel3);   // 心率標題
            guna2HtmlLabel3.Font = new Font("Segoe UI", 24F, FontStyle.Bold);  // 心率標題放大
            設定標題(guna2HtmlLabel5);   // 溫度標題
            設定標題(guna2HtmlLabel7);   // 溼度標題
            設定標題(guna2HtmlLabel9);   // 體溫標題
            設定標題(guna2HtmlLabel11);  // 距離標題
            設定標題(guna2HtmlLabel13);  // 人體標題



            設定數值(guna2HtmlLabel6, Color.FromArgb(200, 135, 26));  // 溫度 橘
            設定數值(guna2HtmlLabel8, Color.FromArgb(19, 163, 122));  // 溼度 綠
            設定數值(guna2HtmlLabel10, Color.FromArgb(226, 59, 59));  // 體溫 紅
            設定數值(guna2HtmlLabel12, Color.FromArgb(90, 79, 199));  // 距離 紫
            設定數值(guna2HtmlLabel14, Color.FromArgb(26, 155, 84));  // 人體 綠
            guna2HtmlLabel14.Font = new Font("Segoe UI", 48F, FontStyle.Bold);  // 人體單獨放大

            // 自動排列：標題放左上、數值放中間
            void 排版(Guna.UI2.WinForms.Guna2Panel panel,
                      Guna.UI2.WinForms.Guna2HtmlLabel 標題,
                      Guna.UI2.WinForms.Guna2HtmlLabel 數值)
            {
                // 標題：左上角
                標題.Location = new Point(20, 18);

                // 數值：水平置中、垂直偏下
                int x = (panel.Width - 數值.Width) / 2;
                int y = panel.Height - 數值.Height - 10;
                數值.Location = new Point(x, y);
            }

            // 套用到每張卡片（標題Label, 數值Label）
            排版(guna2Panel6, guna2HtmlLabel1, guna2HtmlLabel2);   // 血氧
            排版(guna2Panel7, guna2HtmlLabel3, guna2HtmlLabel4);   // 心率
            排版(guna2Panel1, guna2HtmlLabel5, guna2HtmlLabel6);   // 溫度
            排版(guna2Panel2, guna2HtmlLabel7, guna2HtmlLabel8);   // 溼度
            排版(guna2Panel3, guna2HtmlLabel9, guna2HtmlLabel10);  // 體溫
            排版(guna2Panel4, guna2HtmlLabel11, guna2HtmlLabel12); // 距離
            排版(guna2Panel5, guna2HtmlLabel13, guna2HtmlLabel14); // 人體

            guna2HtmlLabel9.Location = new Point(45, 18);

            // 用真實文字寬度來置中
            guna2HtmlLabel14.AutoSize = true;

            // 測量文字實際像素寬度
            Size 文字大小 = TextRenderer.MeasureText(
                guna2HtmlLabel14.Text,
                guna2HtmlLabel14.Font
            );

            int 置中X = (guna2Panel5.Width - 文字大小.Width) / 2;
            guna2HtmlLabel14.Location = new Point(置中X, guna2HtmlLabel14.Location.Y);

            // ===== 頂部標題板 =====
            guna2Panel14.FillColor = Color.FromArgb(33, 92, 81);   // 深綠
            guna2Panel14.BorderRadius = 0;

            // App 標題
            guna2HtmlLabel30.Font = new Font("Microsoft JhengHei", 24F, FontStyle.Bold);
            guna2HtmlLabel30.ForeColor = Color.White;
            guna2HtmlLabel30.BackColor = Color.Transparent;
            guna2HtmlLabel30.Text = "HealthMange";
            // 環境監測頁背景：暖米色/淺棕
            tabPage4.BackColor = Color.FromArgb(245, 240, 232);  // 淺米棕
            tabPage2.BackColor = Color.FromArgb(240, 243, 245);
            // ===== 儲存按鈕（漸層，融入深綠主題）=====
            guna2GradientButton1.FillColor = Color.FromArgb(33, 92, 81);       // 起點：主題深綠（跟標題板同色）
            guna2GradientButton1.FillColor2 = Color.FromArgb(45, 130, 110);    // 終點：稍亮的綠
            guna2GradientButton1.BorderRadius = 10;
            guna2GradientButton1.Font = new Font("Microsoft JhengHei", 11F, FontStyle.Bold);
            guna2GradientButton1.ForeColor = Color.White;
            guna2GradientButton1.Text = "儲存資料";
            guna2GradientButton1.ShadowDecoration.Enabled = true;
            guna2GradientButton1.ShadowDecoration.Depth = 5;
            // ===== 關閉圓圈 =====
            guna2CircleButton1.Size = new Size(24, 24);
            guna2CircleButton1.FillColor = Color.FromArgb(34, 181, 115);  // 預設綠
            guna2CircleButton1.BackColor = Color.FromArgb(33, 92, 81);  // 角落色與標題板同色
            guna2CircleButton1.Text = "";
            guna2CircleButton1.ShadowDecoration.Enabled = false;

            // ===== 關閉按鈕（綠） =====
            guna2CircleButton1.Size = new Size(24, 24);
            guna2CircleButton1.FillColor = Color.FromArgb(34, 181, 115);
            guna2CircleButton1.BackColor = Color.FromArgb(33, 92, 81);
            guna2CircleButton1.Text = "";
            guna2CircleButton1.BorderThickness = 0;  // 無邊框
            guna2CircleButton1.ShadowDecoration.Enabled = false;

            // ===== 返回按鈕（黃） =====
            guna2CircleButton2.Size = new Size(24, 24);
            guna2CircleButton2.FillColor = Color.FromArgb(255, 193, 7);
            guna2CircleButton2.BackColor = Color.FromArgb(33, 92, 81);  // 角落色與標題板同色
            guna2CircleButton2.Text = "";
            guna2CircleButton2.BorderThickness = 0;  // 無邊框
            guna2CircleButton2.ShadowDecoration.Enabled = false;

            // ===== 最小化按鈕（藍） =====
            guna2CircleButton3.Size = new Size(24, 24);
            guna2CircleButton3.FillColor = Color.FromArgb(94, 148, 255);
            guna2CircleButton3.BackColor = Color.FromArgb(33, 92, 81);  // 角落色與標題板同色
            guna2CircleButton3.Text = "";
            guna2CircleButton3.BorderThickness = 0;  // 無邊框
            guna2CircleButton3.ShadowDecoration.Enabled = false;

            // ===== tabPage6 標題 =====
            lbl1.Text = "生理數據";
            lbl1.Font = new Font("Microsoft JhengHei", 14F, FontStyle.Bold);
            lbl1.Location = new Point(10, 10);
            lbl1.BackColor = Color.Transparent;
            tabPage6.Controls.Add(lbl1);

            lbl2.Text = "環境數據";
            lbl2.Font = new Font("Microsoft JhengHei", 14F, FontStyle.Bold);
            lbl2.Location = new Point(10, 280);
            lbl2.BackColor = Color.Transparent;
            tabPage6.Controls.Add(lbl2);

            tabPage6.BackColor = Color.FromArgb(240, 243, 245);

            // ===== 按鈕提示 =====
            ToolTip tooltip = new ToolTip();
            tooltip.OwnerDraw = true;

            // 提示框大小
            tooltip.Popup += (s, e) =>
            {
                e.ToolTipSize = new Size(120, 40);
            };

            tooltip.Draw += (s, e) =>
            {
                e.DrawBackground();
                e.DrawBorder();

                // 放大字體並置中
                Font font = new Font("Microsoft JhengHei", 14F, FontStyle.Bold);
                StringFormat format = new StringFormat();
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;

                e.Graphics.DrawString(e.ToolTipText, font, Brushes.Black, e.Bounds, format);
            };

            tooltip.SetToolTip(guna2CircleButton1, "關閉應用");
            tooltip.SetToolTip(guna2CircleButton2, "返回上一頁");
            tooltip.SetToolTip(guna2CircleButton3, "最小化");

        }

        private void guna2CircleButton2_MouseEnter(object sender, EventArgs e)
        {
            guna2CircleButton2.FillColor = Color.FromArgb(255, 160, 0);  // 滑入變深橘
        }

        private void guna2CircleButton2_MouseLeave(object sender, EventArgs e)
        {
            guna2CircleButton2.FillColor = Color.FromArgb(255, 193, 7);  // 離開變黃
        }

        private void guna2CircleButton3_MouseEnter(object sender, EventArgs e)
        {
            guna2CircleButton3.FillColor = Color.FromArgb(70, 130, 255);  // 滑入變亮藍
        }

        private void guna2CircleButton3_MouseLeave(object sender, EventArgs e)
        {
            guna2CircleButton3.FillColor = Color.FromArgb(94, 148, 255);  // 離開變藍
        }

        private void guna2CircleButton1_MouseEnter(object sender, EventArgs e)
        {
            guna2CircleButton1.FillColor = Color.FromArgb(255, 60, 60);  // 滑入變紅
        }

        private void guna2CircleButton1_MouseLeave(object sender, EventArgs e)
        {
            guna2CircleButton1.FillColor = Color.FromArgb(34, 181, 115);  // 離開變綠
        }

        private void guna2CircleButton1_Click(object sender, EventArgs e)
        {
            // 檢測中 且 還沒儲存 → 提醒先存
            if ((顯示生理 || 顯示環境) && !已儲存)
            {
                var 結果 = MessageBox.Show(
                    "尚未儲存資料，確定要關閉嗎？\n建議先儲存再關閉。",
                    "提醒", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (結果 != DialogResult.OK) return;  // 取消就不關
            }
            // 沒檢測、或已儲存、或使用者堅持關 → 關閉
            this.Close();
        }
        // 更新狀態燈 + 文字：正常綠、異常紅
        private void 更新狀態(Guna.UI2.WinForms.Guna2Panel 燈,
                              Guna.UI2.WinForms.Guna2HtmlLabel 文字,
                              bool 正常, bool 有資料)
        {
            if (!有資料)
            {
                燈.FillColor = Color.FromArgb(120, 120, 120);  // 灰：沒資料
                文字.Text = "未偵測";
                文字.ForeColor = Color.Gray;
            }
            else if (正常)
            {
                燈.FillColor = Color.FromArgb(34, 181, 115);   // 綠：正常
                文字.Text = "正常";
                文字.ForeColor = Color.FromArgb(34, 181, 115);
            }
            else
            {
                燈.FillColor = Color.Red;                       // 紅：異常
                文字.Text = "異常";
                文字.ForeColor = Color.Red;
            }
        }

        // ===== 生理板資料 =====
        private void Physio_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string line = portPhysio.ReadLine().Trim();
                生理最後收到 = DateTime.Now;
                if (line.StartsWith("E:"))
                {
                    int.TryParse(line.Substring(2), out ecg);

                    // 存進陣列（像跑馬燈一樣往後推）
                    ecgData[ecgPos] = ecg;
                    ecgPos = (ecgPos + 1) % ecgData.Length;

                    this.BeginInvoke(new Action(() =>
                    {
                        if (!顯示生理) return;  // 開關關閉時不更新
                        畫ECG();
                    }));
                }
                else if (line.StartsWith("V:"))
                {
                    string[] parts = line.Substring(2).Split(',');
                    if (parts.Length == 2)
                    {
                        int.TryParse(parts[0], out hr);
                        int.TryParse(parts[1], out spo2);
                    }
                    this.BeginInvoke(new Action(() =>
                    {
                        if (!顯示生理) return;
                        guna2HtmlLabel2.Text = spo2 + " %";
                        guna2HtmlLabel4.Text = hr + " bpm";
                        // 血氧狀態：≥95 正常
                        更新狀態(guna2Panel11, guna2HtmlLabel23, spo2 >= 95, spo2 > 0);
                        // 心率狀態：50~70 正常
                        更新狀態(guna2Panel12, guna2HtmlLabel26, hr >= 50 && hr <= 70, hr > 0);
                    }));
                }
            }
            catch
            {
                try { if (portPhysio != null) portPhysio.Close(); } catch { }
                this.Invoke(new Action(() =>
                {
                    USB斷線處理();
                }));
            }
        }

        // ===== 環境板資料 =====
        private void Env_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string line = portEnv.ReadLine().Trim();

                if (line.StartsWith("ENV:"))
                {
                    string[] parts = line.Substring(4).Split(',');
                    if (parts.Length == 5)
                    {
                        float.TryParse(parts[0], out temp);
                        float.TryParse(parts[1], out humidity);
                        float.TryParse(parts[2], out objTemp);
                        int.TryParse(parts[3], out distance);
                        int.TryParse(parts[4], out motion);

                        this.BeginInvoke(new Action(() =>
                        {
                            if (!顯示環境) return;  // 開關關閉時不更新
                            guna2HtmlLabel6.Text = temp + " °C";        // 環境溫度
                            guna2HtmlLabel8.Text = humidity + " %";     // 溼度
                            guna2HtmlLabel10.Text = objTemp + " °C";

                            // 體溫：<37.5 正常
                            更新狀態(guna2Panel13, guna2HtmlLabel29, objTemp >= 30 && objTemp <= 33, objTemp > 0);
                            guna2HtmlLabel10.ForeColor = Color.FromArgb(226, 59, 59);
                            guna2HtmlLabel12.Text = distance + " mm";   // 距離
                            guna2HtmlLabel14.Text = (motion == 1) ? "偵測到有人" : "無人";  // 人體感應
                            Size sz = TextRenderer.MeasureText(guna2HtmlLabel14.Text, guna2HtmlLabel14.Font);
                            guna2HtmlLabel14.Left = guna2Panel5.Left + (guna2Panel5.Width - sz.Width) / 2;
                            if (motion == 1)
                            {
                                // 偵測到有人：淡綠背景 + 粗綠邊框
                                guna2Panel5.FillColor = Color.FromArgb(230, 248, 238);   // 淡綠
                                guna2Panel5.BorderColor = Color.FromArgb(26, 155, 84);   // 綠
                                guna2Panel5.BorderThickness = 3;                          // 加粗
                            }
                            else
                            {
                                // 沒人：恢復白底細框
                                guna2Panel5.FillColor = Color.White;
                                guna2Panel5.BorderColor = Color.FromArgb(26, 155, 84);
                                guna2Panel5.BorderThickness = 1;
                            }
                        }));
                    }
                }
            }
            catch
            {
                // 讀取失敗 = 可能斷線了
                try { if (portEnv != null) portEnv.Close(); } catch { }
                this.BeginInvoke(new Action(() =>
                {
                    guna2Panel9.FillColor = Color.FromArgb(255, 60, 60);
                    guna2HtmlLabel15.Text = "環境板 USB (嘗試連線中)";
                }));
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (portPhysio != null && portPhysio.IsOpen) portPhysio.Close();
            if (portEnv != null && portEnv.IsOpen) portEnv.Close();
            Application.Exit();
        }

        private void guna2ToggleSwitch1_CheckedChanged(object sender, EventArgs e)
        {
            if (忽略開關事件) return;
            if (guna2ToggleSwitch1.Checked)
            {
                // 沒連 USB 不能開
                if (portPhysio == null || !portPhysio.IsOpen)
                {
                    MessageBox.Show("尚未連接 USB（生理板），請先確認裝置連線");
                    忽略開關事件 = true;
                    guna2ToggleSwitch1.Checked = false;  // 彈回
                    忽略開關事件 = false;
                    return;
                }
                顯示生理 = true;  // 開：允許顯示
            }
            else
            {
                // 關：確認清空
                var 結果 = MessageBox.Show("資料將清空重置，確認結束偵測？", "確認",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (結果 == DialogResult.OK)
                {
                    顯示生理 = false;
                    guna2HtmlLabel2.Text = "(血氧)";
                    guna2HtmlLabel4.Text = "(心率)";
                    更新狀態(guna2Panel11, guna2HtmlLabel23, false, false);  // 血氧
                    更新狀態(guna2Panel12, guna2HtmlLabel26, false, false);  // 心率
                    for (int i = 0; i < ecgData.Length; i++) ecgData[i] = 0;
                    畫ECG();
                }
                else
                {
                    忽略開關事件 = true;
                    guna2ToggleSwitch1.Checked = true;  // 取消：彈回
                    忽略開關事件 = false;
                }
            }
        }

        private void guna2ToggleSwitch2_CheckedChanged(object sender, EventArgs e)
        {
            if (忽略開關事件) return;
            if (guna2ToggleSwitch2.Checked)
            {
                // 先顯示遮罩
                顯示遮罩("藍牙連接中，請稍後...");

                // 連線丟背景，不卡 UI（遮罩才顯示得出來）
                Task.Run(() =>
                {
                    try
                    {
                        var p = new SerialPort("COM8", 9600);
                        p.DataReceived += Env_DataReceived;
                        p.Open();   // 開啟連線較慢，放在背景執行
                        portEnv = p;
                        環境最後收到 = DateTime.Now;

                        this.Invoke(new Action(() =>
                        {
                            顯示環境 = true;
                            guna2Panel9.FillColor = Color.FromArgb(34, 181, 115);  // 綠
                            guna2HtmlLabel15.Text = "環境板" +
                            " 藍牙";
                            隱藏遮罩();   // 連上，遮罩消失
                        }));
                    }
                    catch (Exception ex)
                    {
                        this.Invoke(new Action(() =>
                        {
                            隱藏遮罩();
                            guna2Panel9.FillColor = Color.FromArgb(255, 60, 60);   // 紅
                            MessageBox.Show("環境板連線失敗：" + ex.Message);
                            忽略開關事件 = true;
                            guna2ToggleSwitch2.Checked = false;  // 失敗彈回
                            忽略開關事件 = false;
                        }));
                    }
                });
            }
            else
            {
                // 關：確認結束
                var 結果 = MessageBox.Show("資料將清空重置，確認結束偵測？", "確認",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (結果 != DialogResult.OK)
                {
                    忽略開關事件 = true;
                    guna2ToggleSwitch2.Checked = true;  // 取消，彈回
                    忽略開關事件 = false;
                    return;
                }

                // 確認後：斷藍牙 + 清空
                顯示環境 = false;
                if (portEnv != null && portEnv.IsOpen) portEnv.Close();
                guna2Panel9.FillColor = Color.FromArgb(255, 60, 60);  // 紅
                guna2HtmlLabel15.Text = "環境板 藍牙";

                temp = 0;
                humidity = 0;
                objTemp = 0;

                guna2HtmlLabel6.Text = "(環境溫度)";
                guna2HtmlLabel8.Text = "(濕度)";
                guna2HtmlLabel10.Text = "(人體體溫)";
                guna2HtmlLabel12.Text = "(距離)";
                guna2HtmlLabel14.Text = "(人體感應)";
                更新狀態(guna2Panel13, guna2HtmlLabel29, false, false);  // 體溫燈變灰
            }
        }

        private void 顯示遮罩(string 訊息)
        {
            if (遮罩層 != null) { 隱藏遮罩(); }

            遮罩層 = new Form();
            遮罩層.FormBorderStyle = FormBorderStyle.None;     // 無邊框
            遮罩層.StartPosition = FormStartPosition.Manual;
            遮罩層.ShowInTaskbar = false;
            遮罩層.BackColor = Color.Black;
            遮罩層.Opacity = 0.55;                              // 半透明（0~1，0.55=透出後面）
            遮罩層.Size = this.Size;
            遮罩層.Location = this.Location;

            // 提示文字
            Label lbl = new Label();
            lbl.Text = 訊息;
            lbl.ForeColor = Color.White;
            lbl.Font = new Font("Microsoft JhengHei", 18F, FontStyle.Bold);
            lbl.TextAlign = ContentAlignment.MiddleCenter;
            lbl.Dock = DockStyle.Fill;
            lbl.BackColor = Color.Transparent;
            遮罩層.Controls.Add(lbl);

            遮罩層.Show(this);   // 蓋在主視窗上
        }

        private void 隱藏遮罩()
        {
            if (遮罩層 != null)
            {
                遮罩層.Close();
                遮罩層.Dispose();
                遮罩層 = null;
            }
        }

        private void USB斷線處理()
        {
            顯示生理 = false;
            忽略開關事件 = true;
            guna2ToggleSwitch1.Checked = false;
            忽略開關事件 = false;

            guna2Panel10.FillColor = Color.FromArgb(255, 60, 60);
            guna2HtmlLabel16.Text = "生理板 USB (連線中斷)";

            guna2HtmlLabel2.Text = "(血氧)";
            guna2HtmlLabel4.Text = "(心率)";
            更新狀態(guna2Panel11, guna2HtmlLabel23, false, false);
            更新狀態(guna2Panel12, guna2HtmlLabel26, false, false);
            for (int i = 0; i < ecgData.Length; i++) ecgData[i] = 0;
            畫ECG();

            顯示遮罩("USB 連線中斷，資料遺失\n請重新連接後再測試");
        }

        private void timer1_Tick(object sender, EventArgs e)
        {

            // ===== 生理板 =====
            if (portPhysio != null && portPhysio.IsOpen)
            {
                生理曾連線 = true;  // 記住現在連著
            }
            else
            {
                if (生理曾連線)
                {
                    生理曾連線 = false;
                    if (顯示生理)   // 而且使用者正在監測中
                    {
                        USB斷線處理();   // 做遮罩、關toggle、清空
                    }
                }

                // 嘗試重連
                if (!生理連線中)
                {
                    生理連線中 = true;
                    Task.Run(() =>
                    {
                        try
                        {
                            var p = new SerialPort("COM7", 115200);
                            p.DataReceived += Physio_DataReceived;
                            p.Open();
                            portPhysio = p;
                            this.Invoke(new Action(() =>
                            {
                                guna2Panel10.FillColor = Color.FromArgb(34, 181, 115);
                                guna2HtmlLabel16.Text = "生理板 USB";
                                隱藏遮罩();
                            }));
                        }
                        catch
                        {
                            this.Invoke(new Action(() =>
                            {
                                guna2Panel10.FillColor = Color.FromArgb(255, 60, 60);
                                guna2HtmlLabel16.Text = "生理板 USB (嘗試連線中)";
                            }));
                        }
                        finally { 生理連線中 = false; }
                    });
                }
            }
        }

        private void MainForm_Move(object sender, EventArgs e)
        {
            if (遮罩層 != null)
            {
                遮罩層.Location = this.Location;
                遮罩層.Size = this.Size;
            }
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            guna2HtmlLabel27.Text = DateTime.Now.ToString("yyyy/MM/dd  dddd  HH:mm:ss");
        }

        private void guna2GradientButton1_Click(object sender, EventArgs e)
        {
            if (!顯示生理)
            {
                MessageBox.Show("目前沒有生理監測資料可儲存");
                return;
            }

            // 檢查環境板（藍牙）是否已連線
            if (objTemp == 0 && !顯示環境)
            {
                MessageBox.Show("體溫資料為空，請啟動藍牙連接環境板", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 彈出對話框輸入姓名和學號
            Form form = new Form()
            {
                Text = "儲存資料",
                Width = 380,
                Height = 220,
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label lbl1 = new Label() { Left = 20, Top = 20, Text = "姓名：", Width = 60, Height = 25 };
            TextBox txt1 = new TextBox() { Left = 90, Top = 20, Width = 250, Height = 25 };

            Label lbl2 = new Label() { Left = 20, Top = 60, Text = "學號：", Width = 60, Height = 25 };
            TextBox txt2 = new TextBox() { Left = 90, Top = 60, Width = 250, Height = 25 };

            Button okBtn = new Button() { Text = "確認", Left = 160, Width = 80, Height = 35, Top = 110, DialogResult = DialogResult.OK };
            Button cancelBtn = new Button() { Text = "取消", Left = 250, Width = 80, Height = 35, Top = 110, DialogResult = DialogResult.Cancel };

            form.Controls.Add(lbl1);
            form.Controls.Add(txt1);
            form.Controls.Add(lbl2);
            form.Controls.Add(txt2);
            form.Controls.Add(okBtn);
            form.Controls.Add(cancelBtn);
            form.AcceptButton = okBtn;
            form.CancelButton = cancelBtn;

            if (form.ShowDialog() == DialogResult.OK)
            {
                string 姓名 = txt1.Text;
                string 學號 = txt2.Text;

                if (string.IsNullOrEmpty(姓名) || string.IsNullOrEmpty(學號))
                {
                    MessageBox.Show("請填入姓名和學號");
                    return;
                }

                SQLiteService.存生理數據_帶姓名(姓名, 學號, hr, spo2, objTemp);

                已儲存 = true;
                MessageBox.Show("資料已儲存！");

                顯示資料紀錄();
            }
        }

        private void guna2TabControl1_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (e.TabPage == tabPage6)  // 切到資料紀錄頁需輸入管理者密碼
            {
                string password = PromptPassword("請輸入管理者密碼：");

                if (password != "123456")
                {
                    MessageBox.Show("密碼錯誤，無法進入資料紀錄", "權限不足", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    e.Cancel = true;
                    return;
                }

                // 通過驗證後顯示資料
                顯示資料紀錄();
            }
        }

        private void guna2CircleButton2_Click(object sender, EventArgs e)
        {
            // 檢測中 且 還沒儲存 → 提醒先存
            if ((顯示生理 || 顯示環境) && !已儲存)
            {
                var 結果 = MessageBox.Show(
                    "尚未儲存資料，確定要返回嗎？\n建議先儲存再返回。",
                    "提醒", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (結果 != DialogResult.OK) return;
            }

            InterForm interForm = new InterForm();
            interForm.Show();
            this.Hide();
        }

        private void guna2CircleButton3_Click(object sender, EventArgs e)
        {
            Timer 動畫 = new Timer();
            int 步數 = 0;
            int 總步數 = 20;  // 步數越多越平滑

            int 原始寬 = this.Width;
            int 原始高 = this.Height;
            int 原始X = this.Left;
            int 原始Y = this.Top;

            動畫.Interval = 30;  // 30ms
            動畫.Tick += (s, e2) =>
            {
                步數++;

                // 進度比例
                float 進度 = (float)步數 / 總步數;

                // 淡出
                this.Opacity = 1 - 進度;

                // 縮小並往右下角移動
                float 縮放 = 1 - 進度;
                int 新寬 = (int)(原始寬 * 縮放);
                int 新高 = (int)(原始高 * 縮放);

                this.Left = 原始X + (原始寬 - 新寬) / 2;
                this.Top = 原始Y + (原始高 - 新高) / 2;
                this.Width = 新寬;
                this.Height = 新高;

                if (步數 >= 總步數)
                {
                    動畫.Stop();
                    動畫.Dispose();

                    // 恢復原始大小再最小化
                    this.Opacity = 1;
                    this.Width = 原始寬;
                    this.Height = 原始高;
                    this.Left = 原始X;
                    this.Top = 原始Y;

                    this.WindowState = FormWindowState.Minimized;
                }
            };

            動畫.Start();
        }

        private void 按鈕_清空數據庫_Click(object sender, EventArgs e)
        {
            // 檢查是否選取資料列
            if (dataGridView生理.SelectedRows.Count == 0)
            {
                MessageBox.Show("請先選取要刪除的資料", "提醒", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var 結果 = MessageBox.Show("確定刪除此筆資料？", "確認刪除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (結果 == DialogResult.Yes)
            {
                try
                {
                    // 取得選取資料列的 ID（第一欄）
                    int id = Convert.ToInt32(dataGridView生理.SelectedRows[0].Cells[0].Value);
                    SQLiteService.刪除生理數據(id);
                    MessageBox.Show("資料已刪除！");
                    顯示資料紀錄();  // 重新整理
                }
                catch
                {
                    MessageBox.Show("刪除失敗");
                }
            }
        }
    }
}