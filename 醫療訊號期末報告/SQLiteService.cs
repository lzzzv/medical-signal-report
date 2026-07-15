using System;
using System.Data;
using Microsoft.Data.Sqlite;   
using System.IO;

namespace 醫療訊號期末報告
{
    public static class SQLiteService
    {
        static string 連線字串 = "Data Source=health.db";

        public static void 初始化()
        {
            SQLitePCL.Batteries_V2.Init();
            using (var conn = new SqliteConnection(連線字串))
            {
                conn.Open();

                string 建表3 = @"
            CREATE TABLE IF NOT EXISTS PhysioData (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                [姓名] TEXT,
                [學號] TEXT,
                [Timestamp] TEXT NOT NULL,
                [HeartRate] INTEGER,
                [SpO2] INTEGER,
                [ObjectTemp] REAL
            );";

                using (var cmd = new SqliteCommand(建表3, conn)) cmd.ExecuteNonQuery();

                // 原有的表
                string 建表1 = @"
                    CREATE TABLE IF NOT EXISTS records (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        [姓名] TEXT, [年齡] INTEGER, [心率] INTEGER,
                        [血氧] INTEGER, [體溫] REAL, [檢測時間] TEXT
                    );";

                string 建表2 = @"
                    CREATE TABLE IF NOT EXISTS admins (
                        [帳號] TEXT PRIMARY KEY, [密碼] TEXT
                    );";



                // 新表：环境数据
                string 建表4 = @"
                    CREATE TABLE IF NOT EXISTS EnvironmentData (
                        ID INTEGER PRIMARY KEY AUTOINCREMENT,
                        [姓名] TEXT,
                        [學號] TEXT,
                        [Timestamp] TEXT NOT NULL,
                        [Temperature] REAL,
                        [Humidity] REAL,
                        [ObjectTemp] REAL
                    );";

                using (var cmd = new SqliteCommand(建表1, conn)) cmd.ExecuteNonQuery();
                using (var cmd = new SqliteCommand(建表2, conn)) cmd.ExecuteNonQuery();
                using (var cmd = new SqliteCommand(建表3, conn)) cmd.ExecuteNonQuery();
                using (var cmd = new SqliteCommand(建表4, conn)) cmd.ExecuteNonQuery();

                string 檢查 = "SELECT COUNT(*) FROM admins WHERE [帳號]='admin';";
                using (var cmd = new SqliteCommand(檢查, conn))
                {
                    long 數量 = (long)cmd.ExecuteScalar();
                    if (數量 == 0)
                    {
                        using (var c2 = new SqliteCommand("INSERT INTO admins ([帳號],[密碼]) VALUES ('admin','1234');", conn))
                            c2.ExecuteNonQuery();
                    }
                }
            }
        }

        public static void 新增紀錄(string 姓名, int 年齡, int 心率, int 血氧, double 體溫)
        {
            using (var conn = new SqliteConnection(連線字串))
            {
                conn.Open();
                string sql = @"INSERT INTO records ([姓名],[年齡],[心率],[血氧],[體溫],[檢測時間])
                               VALUES (@姓名,@年齡,@心率,@血氧,@體溫,@時間);";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@姓名", 姓名);
                    cmd.Parameters.AddWithValue("@年齡", 年齡);
                    cmd.Parameters.AddWithValue("@心率", 心率);
                    cmd.Parameters.AddWithValue("@血氧", 血氧);
                    cmd.Parameters.AddWithValue("@體溫", 體溫);
                    cmd.Parameters.AddWithValue("@時間", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static DataTable 讀取所有紀錄()
        {
            using (var conn = new SqliteConnection(連線字串))
            {
                conn.Open();
                string sql = "SELECT id AS 編號, [姓名], [年齡], [心率], [血氧], [體溫], [檢測時間] FROM records ORDER BY id DESC;";
                using (var cmd = new SqliteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    DataTable dt = new DataTable();
                    dt.Load(reader);
                    return dt;
                }
            }
        }

        public static bool 驗證登入(string 帳號, string 密碼)
        {
            using (var conn = new SqliteConnection(連線字串))
            {
                conn.Open();
                string sql = "SELECT COUNT(*) FROM admins WHERE [帳號]=@帳號 AND [密碼]=@密碼;";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@帳號", 帳號);
                    cmd.Parameters.AddWithValue("@密碼", 密碼);
                    long 數量 = (long)cmd.ExecuteScalar();
                    return 數量 > 0;
                }
            }
        }

        // ===== 生理数据 =====
        public static void 存生理數據(int hr, int spo2, int ecg)
        {
            using (var conn = new SqliteConnection(連線字串))
            {
                conn.Open();
                string sql = @"
                    INSERT INTO PhysioData ([Timestamp], [HeartRate], [SpO2], [ECG]) 
                    VALUES (@time, @hr, @spo2, @ecg)
                ";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@hr", hr);
                    cmd.Parameters.AddWithValue("@spo2", spo2);
                    cmd.Parameters.AddWithValue("@ecg", ecg);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void 存生理數據_帶姓名(string 姓名, string 學號, int hr, int spo2, int ecg)
        {
            using (var conn = new SqliteConnection(連線字串))
            {
                conn.Open();
                string sql = @"
                    INSERT INTO PhysioData ([姓名], [學號], [Timestamp], [HeartRate], [SpO2], [ECG]) 
                    VALUES (@name, @id, @time, @hr, @spo2, @ecg)
                ";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@name", 姓名);
                    cmd.Parameters.AddWithValue("@id", 學號);
                    cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@hr", hr);
                    cmd.Parameters.AddWithValue("@spo2", spo2);
                    cmd.Parameters.AddWithValue("@ecg", ecg);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ===== 环境数据 =====
        public static void 存環境數據(float temp, float humidity, float objTemp)
        {
            using (var conn = new SqliteConnection(連線字串))
            {
                conn.Open();
                string sql = @"
                    INSERT INTO EnvironmentData ([Timestamp], [Temperature], [Humidity], [ObjectTemp]) 
                    VALUES (@time, @temp, @humid, @objTemp)
                ";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@temp", temp);
                    cmd.Parameters.AddWithValue("@humid", humidity);
                    cmd.Parameters.AddWithValue("@objTemp", objTemp);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void 存生理數據_帶姓名(string 姓名, string 學號, int hr, int spo2, float objTemp)  // ⬅️ 改成 float objTemp
        {
            using (var conn = new SqliteConnection(連線字串))
            {
                conn.Open();
                string sql = @"
            INSERT INTO PhysioData ([姓名], [學號], [Timestamp], [HeartRate], [SpO2], [ObjectTemp]) 
            VALUES (@name, @id, @time, @hr, @spo2, @objTemp)
        ";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@name", 姓名);
                    cmd.Parameters.AddWithValue("@id", 學號);
                    cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@hr", hr);
                    cmd.Parameters.AddWithValue("@spo2", spo2);
                    cmd.Parameters.AddWithValue("@objTemp", objTemp);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ===== 查询生理数据 =====
        public static DataTable 查詢生理數據()
        {
            using (var conn = new SqliteConnection(連線字串))
            {
                conn.Open();
                string sql = @"
            SELECT 
                ID,
                [姓名], 
                [學號], 
                [Timestamp] AS '時間', 
                [HeartRate] AS '心率', 
                [SpO2] AS '血氧', 
                ROUND([ObjectTemp], 2) AS '體溫'
            FROM PhysioData 
            ORDER BY ID DESC
        ";
                using (var cmd = new SqliteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    DataTable dt = new DataTable();
                    dt.Load(reader);
                    return dt;
                }
            }
        }

        public static void 刪除生理數據(int id)
        {
            using (var conn = new SqliteConnection(連線字串))
            {
                conn.Open();
                string sql = "DELETE FROM PhysioData WHERE ID = @id";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static DataTable 查詢環境數據()
        {
            using (var conn = new SqliteConnection(連線字串))
            {
                conn.Open();
                string sql = @"
            SELECT 
                [姓名], 
                [學號], 
                [Timestamp] AS '時間', 
                [Temperature] AS '環境溫度', 
                [Humidity] AS '濕度', 
                [ObjectTemp] AS '體溫'
            FROM EnvironmentData 
            ORDER BY ID DESC
        ";
                using (var cmd = new SqliteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    DataTable dt = new DataTable();
                    dt.Load(reader);
                    return dt;
                }
            }
        }
    }
}