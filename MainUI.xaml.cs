
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;

using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.DatabaseServices;
using LiteDB;
using acObjectId = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using acApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace PipingSystem
{
    /// <summary>
    /// MainUI.xaml の相互作用ロジック
    /// </summary>
    public partial class MainUI : Window
    {
        public class ItemSet
        {
            // DisplayMemberとValueMemberにはプロパティで指定する仕組み
            public String ItemDisp { get; set; }
            public int ItemValue { get; set; }

            // プロパティをコンストラクタでセット
            public ItemSet(int v, String s)
            {
                ItemDisp = s;
                ItemValue = v;
            }
        }

        public MainUI()
        {
            InitializeComponent();
            Show();
            //Activate_Window();
            Editor ed = acApplication.DocumentManager.MdiActiveDocument.Editor;

            //var process = Process.GetProcessesByName("acad");
            // ComboBox用データ作成 //ListでOK //IList インターフェイスまたは IListSource インターフェイスを実装する、DataSet または Array などのオブジェクト。
            List<string> src = new List<string>();
            var pip = DataBase.readPipeDB();
            var mat = new HashSet<string>();
            var nam = new HashSet<string>();
            foreach (var item in pip)
            {
                mat.Add(item.Material);
                nam.Add(item.Name);
            }
            
            //src.Add("ESLON");

            // ComboBoxに表示と値をセット
            MaterialBox.ItemsSource = mat;
            MaterialBox.SelectedIndex = 0;
            Size1Box.ItemsSource = nam;
            Size1Box.SelectedIndex = 12;
            //MaterialBox.DisplayMember = "ItemDisp";
            //MaterialBox.ValueMember = "ItemValue";

            // 初期値セット
            //MaterialBox.SelectedIndex = 0;
            //comboBox1_SelectedIndexChanged(null, null);
        }



        private void MaterialBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void button_Click(object sender, RoutedEventArgs e)
        {

            
        }
        public Elbow GetDBData(string material, string name, string angle, string type,string table)
        {
            using (LiteDatabase litedb = new LiteDatabase(DataBase.db_dir))
            {
                if (table == "Elbow")
                {
                    var col = litedb.GetCollection<Elbow>("Elbow");
                    var result = col.Query()
                        .Where(x => x.Material.Equals(material))
                        .Where(x => x.Name.Equals(name))
                        .Where(x => x.Angle.Equals(angle))
                        .Where(x => x.Type.Equals(type))
                        .ToList();
                    return result[0];
                }
                return null;
            }
        }
        /*
        private void _90EL_L_button_Click(object sender, RoutedEventArgs e)
        {
            string material = MaterialBox.SelectedItem.ToString();
            string name = Size1Box.SelectedItem.ToString();
            string angle = "90";
            string type = "LONG";

            var DBData = GetDBData(material, name, angle, type, "Elbow");
            Command c = new Command();
            c.Generatr90Elbow(material, name, angle, type);
            

            c.WriteBlocks(blkName, new Point3d(0, 0, 0), material);
        }*/
        public void CheckActivate()
        {
            string angle = "";
            string type = "";
            string material = MaterialBox.SelectedItem.ToString();
            string name = Size1Box.SelectedItem.ToString();

            angle = "90";
            type = "LONG";
            using (LiteDatabase litedb = new LiteDatabase(DataBase.db_dir))
            {
                var col = litedb.GetCollection<Elbow>("Elbow");

                var result = col.Query()
                    .Where(x => x.Material.Equals(material))
                    .Where(x => x.Name.Equals(name))
                    .Where(x => x.Angle.Equals(angle))
                    .Where(x => x.Type.Equals(type))
                    .ToList();
                if (result.Count == 0)
                {
                    _90EL_L_button.IsEnabled = false;
                }
                else
                {
                    _90EL_L_button.IsEnabled = true;
                }
            }
            //---------------------------------------------------------------
            angle = "90";
            type = "SHORT";
            using (LiteDatabase litedb = new LiteDatabase(DataBase.db_dir))
            {
                var col = litedb.GetCollection<Elbow>("Elbow");

                var result = col.Query()
                    .Where(x => x.Material.Equals(material))
                    .Where(x => x.Name.Equals(name))
                    .Where(x => x.Angle.Equals(angle))
                    .Where(x => x.Type.Equals(type))
                    .ToList();
                if (result.Count == 0)
                {
                    _90EL_S_button.IsEnabled = false;
                }
                else
                {
                    _90EL_S_button.IsEnabled = true;
                }
            }
            //---------------------------------------------------------------
            angle = "45";
            type = "LONG";
            using (LiteDatabase litedb = new LiteDatabase(DataBase.db_dir))
            {
                var col = litedb.GetCollection<Elbow>("Elbow");

                var result = col.Query()
                    .Where(x => x.Material.Equals(material))
                    .Where(x => x.Name.Equals(name))
                    .Where(x => x.Angle.Equals(angle))
                    .Where(x => x.Type.Equals(type))
                    .ToList();
                if (result.Count == 0)
                {
                    _45EL_L_button.IsEnabled = false;
                }
                else
                {
                    _45EL_L_button.IsEnabled = true;
                }
            }
            //---------------------------------------------------------------
            angle = "45";
            type = "SHORT";
            using (LiteDatabase litedb = new LiteDatabase(DataBase.db_dir))
            {
                var col = litedb.GetCollection<Elbow>("Elbow");

                var result = col.Query()
                    .Where(x => x.Material.Equals(material))
                    .Where(x => x.Name.Equals(name))
                    .Where(x => x.Angle.Equals(angle))
                    .Where(x => x.Type.Equals(type))
                    .ToList();
                if (result.Count == 0)
                {
                    _45EL_S_button.IsEnabled = false;
                }
                else
                {
                    _45EL_S_button.IsEnabled = true;
                }
            }
        }
        private void Size1Box_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CheckActivate();
        }
        /*
        private void _90EL_S_button_Click(object sender, RoutedEventArgs e)
        {
            string material = MaterialBox.SelectedItem.ToString();
            string name = Size1Box.SelectedItem.ToString();
            string angle = "90";
            string type = "SHORT";

            var DBData = GetDBData(material, name, angle, type, "Elbow");

            Command c = new Command();
            c.Generatr90Elbow(material, name, angle, type);

            

            c.WriteBlocks(blkName, new Point3d(0, 0, 0), material);
        }
        
        private void _45EL_L_button_Click(object sender, RoutedEventArgs e)
        {
            string material = MaterialBox.SelectedItem.ToString();
            string name = Size1Box.SelectedItem.ToString();
            string angle = "45";
            string type = "LONG";

            Command.Generatr45Elbow(material, name, angle, type);
            var blkName = GetDBData(material, name, angle, type, "Elbow");

            Command c = new Command();
            c.WriteBlocks(blkName, new Point3d(0, 0, 0), material);
        }

        private void _45EL_S_button_Click(object sender, RoutedEventArgs e)
        {
            string material = MaterialBox.SelectedItem.ToString();
            string name = Size1Box.SelectedItem.ToString();
            string angle = "45";
            string type = "SHORT";

            Command.Generatr45Elbow(material, name, angle, type);
            var blkName = GetDBData(material, name, angle, type, "Elbow");

            Command c = new Command();
            c.WriteBlocks(blkName, new Point3d(0, 0, 0), material);
        }
        */
        private void Pipe_button_Click(object sender, RoutedEventArgs e)
        {

            Document doc = acApplication.DocumentManager.MdiActiveDocument;
            if(doc.CommandInProgress != "")
            {
                doc.CommandCancelled += new CommandEventHandler(PreCancel_WritePipe);
                
                Command c = new Command();
                c.Command_Cancel();
            }
            else
            {
                Call_WritePipe();
            }

        }
        public void PreCancel_WritePipe(object sender, CommandEventArgs e)
        {
            Document doc = acApplication.DocumentManager.MdiActiveDocument;
            Call_WritePipe();
            doc.CommandCancelled -= new CommandEventHandler(PreCancel_WritePipe);
        }

        public void Call_WritePipe()
        {
            var mat = MaterialBox.SelectedItem.ToString();
            var nam = Size1Box.SelectedItem.ToString();
            double rad;
            using (LiteDatabase litedb = new LiteDatabase(DataBase.db_dir))
            {
                var col = litedb.GetCollection<Pipe>("Pipe");

                var result = col.Query()
                    .Where(x => x.Material.Equals(mat))
                    .Where(x => x.Name.Equals(nam))
                    .ToList();
                rad = result[0].Orad;
            }
            Document doc = acApplication.DocumentManager.MdiActiveDocument;
            Command c = new Command();
            c.WritePipe(mat, rad);
        }
        private void Elbow_button_Click(object sender, RoutedEventArgs e)
        {
            string material = MaterialBox.SelectedItem.ToString();
            string name = Size1Box.SelectedItem.ToString();
            string[] AngTyp()
            { 
            switch (((Button)sender).Content){
                case "90EL(L)":return new string[]{ "90","LONG"};
                    case "90EL(S)": return new string[] { "90", "SHORT" };
                    case "45EL(L)": return new string[] { "45", "LONG" };
                    case "45EL(S)": return new string[] { "45", "SHORT" };
                    default: throw new InvalidOperationException();
                }
            }
            string angle = AngTyp()[0];
            string type = AngTyp()[1];
            var DBData = GetDBData(material, name, angle, type, "Elbow");

            Command c = new Command();
            switch (angle)
            {
                case "90": 
                    c.Generate90Elbow(DBData.BName);
                    break;
                case "45":
                    c.Generate45Elbow(DBData.BName);
                    break;
            }

            c.Command_Cancel();
            c.WriteBlocks(DBData, new Point3d(0, 0, 0), material);
        }

    }
}
