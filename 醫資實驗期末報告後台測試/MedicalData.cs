using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace 醫資實驗期末報告後台測試
{
    class Program
    {
        static SerialPort port;

        static float temp = 0;
        static float humidity = 0;
        static float objTemp = 0;
        static int distance = 0;
        static int motion = 0;

        static void Main(string[] args)
        {
            port = new SerialPort("COM8", 9600);  // 藍牙連出是 COM8，速度 9600
            port.DataReceived += Port_DataReceived;

            try
            {
                port.Open();
                Console.WriteLine("已連接 COM8（藍牙），開始接收環境板資料...");
                Console.WriteLine("(按任意鍵結束)");
            }
            catch (Exception ex)
            {
                Console.WriteLine("無法開啟 COM8：" + ex.Message);
                Console.ReadKey();
                return;
            }

            Console.ReadKey();
            port.Close();
        }

        static void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string line = port.ReadLine().Trim();

                // ENV:27.9,69.5,28.1,90,1
                if (line.StartsWith("ENV:"))
                {
                    string val = line.Substring(4);   // 去掉 "ENV:"
                    string[] parts = val.Split(',');
                    if (parts.Length == 5)
                    {
                        float.TryParse(parts[0], out temp);
                        float.TryParse(parts[1], out humidity);
                        float.TryParse(parts[2], out objTemp);
                        int.TryParse(parts[3], out distance);
                        int.TryParse(parts[4], out motion);

                        string human = (motion == 1) ? "有人" : "沒人";
                        Console.WriteLine($"溫度={temp}°C, 溼度={humidity}%, " +
                            $"體溫={objTemp}°C, 距離={distance}mm, {human}");
                    }
                }
            }
            catch
            {
            }
        }
    }
}
