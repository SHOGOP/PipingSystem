using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using LiteDB;
using Csv;

namespace PipingSystem
{
    
    public class DataBase
    {
        public const string db_dir = "./data.db";
        public DataBase()
        {
        }
        public static void createLiteDB()
        {
            using (LiteDatabase litedb = new LiteDatabase(db_dir))
            {
                var col = litedb.GetCollection<Pipe>("Pipe");

                Pipe cus = new Pipe
                {
                    Material = "SUS",
                    Name = "8A",
                    Sch = "SGP",
                    Odia = 10.5,
                    Orad = 5.25,
                    Idia = 6.5,
                    Irad = 3.25,

                };

                col.Insert(cus);
            }
        }
        public static void createLayerDB()
        {
            using (LiteDatabase litedb = new LiteDatabase(db_dir))
            {
                var col = litedb.GetCollection<Layer>("Layer");
                //OpenFileDialogクラスのインスタンスを作成
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.FileName = "default.csv";
                //ダイアログを表示する
                if (ofd.ShowDialog() == DialogResult.No)
                {
                    return;
                }
                // CSVファイルを文字列で取得
                var csvText = File.ReadAllText(ofd.FileName);

                // CSVファイルの各行ごとに処理をする
                foreach (var line in CsvReader.ReadFromText(csvText))
                {
                    Layer cus = new Layer
                    {
                        Name = line["Name"],
                        Color = int.Parse(line["Color"]),
                        LineType = line["LineType"],
                    };

                    col.Insert(cus);
                }
            }
        }
        public static void createPipeDB()
        {
            using (LiteDatabase litedb = new LiteDatabase(db_dir))
            {
                var col = litedb.GetCollection<Pipe>("Pipe");
                // CSVファイルを文字列で取得
                var csvText = File.ReadAllText(@"C:\Users\windows7\source\repos\PipingSystem\Resources\SUS_PIPE_DIM.csv");

                // CSVファイルの各行ごとに処理をする
                foreach (var line in CsvReader.ReadFromText(csvText))
                {
                    Pipe cus = new Pipe
                    {
                        Material = line["Material"],
                        Name = line["Name"],
                        Sch = line["Sch"],
                        Odia = double.Parse(line["Odia"]),
                        Orad = double.Parse(line["Orad"]),
                        Idia = double.Parse(line["Idia"]),
                        Irad = double.Parse(line["Irad"]),

                    };

                    col.Insert(cus);
                }
            }
        }
        public static List<Pipe> readPipeDB()
        {
            using (LiteDatabase litedb = new LiteDatabase(db_dir))
            {
                var col = litedb.GetCollection<Pipe>("Pipe");

                var result = col.Query()
                    .ToList();
                return result;
            }
        }
        public static void createElbowDB()
        {
            using (LiteDatabase litedb = new LiteDatabase(db_dir))
            {
                var col = litedb.GetCollection<Elbow>("Elbow");
                //OpenFileDialogクラスのインスタンスを作成
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.FileName = "default.csv";
                //ダイアログを表示する
                if (ofd.ShowDialog() == DialogResult.No)
                {
                    return;
                }
                // CSVファイルを文字列で取得
                var csvText = File.ReadAllText(ofd.FileName);

                // CSVファイルの各行ごとに処理をする
                foreach (var line in CsvReader.ReadFromText(csvText))
                {
                    Elbow cus = new Elbow
                    {
                        BName = line["BName"],
                        Material = line["Material"],
                        Name = line["Name"],
                        Sch = line["Sch"],
                        Angle = line["Angle"],
                        Type = line["Type"],
                        Pattern = line["Pattern"],
                        Odia = double.Parse(line["Odia"]),
                        Orad = double.Parse(line["Orad"]),
                        Idia = double.Parse(line["Idia"]),
                        Irad = double.Parse(line["Irad"]),
                        Length = double.Parse(line["Length"]),
                        R = double.Parse(line["R"]),

                    };

                    col.Insert(cus);
                }
            }
        }
        public static List<Elbow> readElbowDB()
        {
            using (LiteDatabase litedb = new LiteDatabase(db_dir))
            {
                var col = litedb.GetCollection<Elbow>("Elbow");

                var result = col.Query()
                    .ToList();
                return result;
            }
        }
    }

}
