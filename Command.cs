﻿using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Windows;
using Autodesk.AutoCAD.Colors;
using PipingSystem;
using JsonFileIO;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Forms;
using LiteDB;
using acObjectId = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using acApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using OpenFileDialog = Autodesk.AutoCAD.Windows.OpenFileDialog;



namespace PipingSystem
{

    public class Command
    {
        public double Center_buf { get; set; }//中心線のオフセット係数
        public static acObjectId Cur_ObjID { get; set; }//作業用のオブジェクトID
        public static string Insert_blk { get; set; }//Insertするブロックの名前

        public static string[] Blk_suffix { get; set; }//ブロック名の末尾
        public static string Cur_layer { get; set; }//作業用のレイヤー
        public static int Shortcut_menu { get; set; }//コマンド開始前の"SHORTCUMMENU"を格納
        public static Point3d Last_Point { get; set; }//作業中の最後に指定した点を格納
        public static Double Cur_Slope { get; set; }//作業用の傾斜角度
        public static Double Prev_Slope { get; set; }//前回の傾斜角度
        public static bool Mirror_FLG { get; set; }//ブロックの反転を判定

        public static Parts CParts { get; set; }//作業用のDBのデータを格納


        public Command()
        {
            //初期値
            Center_buf = 1.25;
            Blk_suffix = new string[] {"_H","_DW","_UP","_V"};
            //Prev_Slope = 0;

        }
        /// <summary>
        ///右クリックでEnterを発行するように"SHORTCUTMENU"を変更
        /// <summary>
        public static void InitCommand()
        {
            Shortcut_menu = System.Convert.ToInt32(acApplication.GetSystemVariable("SHORTCUTMENU"));
            acApplication.SetSystemVariable("SHORTCUTMENU", 16);
        }
        /// <summary>
        ///コマンドが終わったら"SHORTCUTMENU"の値を戻す
        /// <summary>
        public static void EndCommand()
        {
            acApplication.SetSystemVariable("SHORTCUTMENU", Shortcut_menu);
        }
        /// <summary>
        /// DBからレイヤー情報を読み込み作成
        /// </summary>
        /// <param name="LayerName">レイヤー名</param>
        public static void CreateLayer(string LayerName)
        {
            // Get the current document and database
            Document acDoc = acApplication.DocumentManager.MdiActiveDocument;
            Database acCurDb = acDoc.Database;
            int LineColor;
            string LineTypName ;
            using (LiteDatabase litedb = new LiteDatabase(DataBase.db_dir))
            {
                var col = litedb.GetCollection<Layer>("Layer");

                var result = col.Query()
                    .Where(x => x.Name.Equals(LayerName))
                    .ToList();
                LineColor = result[0].Color;
                LineTypName = result[0].LineType;
            }

            

            using (DocumentLock docLock = acDoc.LockDocument())
            {
                
                // Start a transaction
                using (Transaction acTrans = acCurDb.TransactionManager.StartTransaction())
                {
                    // Open the Layer table for read
                    LayerTable acLyrTbl;
                    acLyrTbl = acTrans.GetObject(acCurDb.LayerTableId,
                                                    OpenMode.ForRead) as LayerTable;

                    if (acLyrTbl.Has(LayerName) == false)
                    {

                        using (LayerTableRecord acLyrTblRec = new LayerTableRecord())
                        {
                            // Assign the layer the ACI color 3 and a name
                            acLyrTblRec.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)LineColor);
                            acLyrTblRec.Name = LayerName;

                            // 線種追加
                            LinetypeTable acLinTbl;
                            acLinTbl = acTrans.GetObject(acCurDb.LinetypeTableId,
                                                            OpenMode.ForRead) as LinetypeTable;
                            
                            if (acLinTbl.Has(LineTypName) == true)
                            {
                                // Set the linetype for the layer
                                acLyrTblRec.LinetypeObjectId = acLinTbl[LineTypName];
                            }
                            else
                            {
                                // Load the Center Linetype
                                acCurDb.LoadLineTypeFile(LineTypName, "acad.lin");
                                // Set the linetype for the layer
                                acLyrTblRec.LinetypeObjectId = acLinTbl[LineTypName];
                            }

                            // Upgrade the Layer table for write
                            acTrans.GetObject(acCurDb.LayerTableId, OpenMode.ForWrite);

                            // Append the new layer to the Layer table and the transaction
                            acLyrTbl.Add(acLyrTblRec);
                            acTrans.AddNewlyCreatedDBObject(acLyrTblRec, true);
                        }
                    }

                    // Save the changes and dispose of the transaction
                    acTrans.Commit();
                }
            }
        }
        /// <summary>
        /// Defpoints画層を作成
        /// </summary>
        public static void CreateDefpoints()
        {
            // Get the current document and database
            Document acDoc = acApplication.DocumentManager.MdiActiveDocument;
            Database acCurDb = acDoc.Database;
            string LayerName = "Defpoints";
            using (DocumentLock docLock = acDoc.LockDocument())
            {

                // Start a transaction
                using (Transaction acTrans = acCurDb.TransactionManager.StartTransaction())
                {
                    // Open the Layer table for read
                    LayerTable acLyrTbl;
                    acLyrTbl = acTrans.GetObject(acCurDb.LayerTableId,
                                                    OpenMode.ForRead) as LayerTable;

                    if (acLyrTbl.Has(LayerName) == false)
                    {

                        using (LayerTableRecord acLyrTblRec = new LayerTableRecord())
                        {

                            acLyrTblRec.Name = LayerName;
                            // Upgrade the Layer table for write
                            acTrans.GetObject(acCurDb.LayerTableId, OpenMode.ForWrite);

                            // Append the new layer to the Layer table and the transaction
                            acLyrTbl.Add(acLyrTblRec);
                            acTrans.AddNewlyCreatedDBObject(acLyrTblRec, true);
                        }
                    }

                    // Save the changes and dispose of the transaction
                    acTrans.Commit();
                }
            }
        }
        /// <summary>
        /// コマンドラインからコマンドをキャンセル
        /// </summary>

        public void Command_Cancel()
        {
            //作業ウィンドウをアクティブ
            acApplication.MainWindow.Focus();
            Document doc = acApplication.DocumentManager.MdiActiveDocument;
            doc.SendStringToExecute("\x03", false, true, false);
            //Editor ed = acApplication.DocumentManager.MdiActiveDocument.Editor;
            //ed.Command("\x03\x03");
        }

        /// <summary>
        /// MainUIを呼び出す
        /// </summary>
        [CommandMethod("Piping_System")]
        public static void Piping_System()
        {
            //DataBase.createLayerDB();
            //var DbData = Properties.Resources.data;

            if (!File.Exists("./data.db"))
            {
                if (!File.Exists("../../Resources/data.db"))
                {
                    OpenFileDialog ofd = new OpenFileDialog("data.dbを選択", "",
                                    "db; *",
                                    "data.dbを選択",
                                    OpenFileDialog.OpenFileDialogFlags.DefaultIsFolder |
                                    OpenFileDialog.OpenFileDialogFlags.ForceDefaultFolder // .AllowMultiple
                                  );
                    DialogResult sdResult = ofd.ShowDialog();
                    if(sdResult== DialogResult.OK)
                    {
                        File.Copy(ofd.Filename, "./data.db", true);
                    }
                    else
                    {

                        return;
                    }
                    
                }
                else
                {
                    File.Copy("../../Resources/data.db", "./data.db", true);
                }
            }
            MainUI w = new MainUI();

                //JsonAccess test = new JsonAccess();
                //DataBase.createPipeDB();
                //DataBase.createElbowDB();
                //test.Main(new string[] { "test"});
                //Debug.WriteLine("test");
            }
        /// <summary>
        /// パイプを描画する
        /// </summary>
        //[CommandMethod("WritePipe")]
        public void WritePipe(string material ,double rad)
        {
            //作業ウィンドウをアクティブ
            acApplication.MainWindow.Focus();

            InitCommand();
            CreateLayer(material);
            CreateLayer("CEN");
            //ドキュメント、データベースオブジェクトの作成
            Document now_doc = acApplication.DocumentManager.MdiActiveDocument;
            Database db = now_doc.Database;

            do {
                //ユーザー入力の結果を代入するポイントの作成
                PromptPointResult pt_res;
                PromptPointOptions pt_option = new PromptPointOptions("");

                //ユーザーへの入力要求
                pt_option.Message = "\n1点目を指定: ";
                pt_option.AllowNone = true;
                pt_res = now_doc.Editor.GetPoint(pt_option);
                Point3d ptStart = pt_res.Value;

                //ESCキーなどでキャンセルしたときの処理
                if (pt_res.Status == PromptStatus.Cancel | pt_res.Status == PromptStatus.None)
                {
                    EndCommand();
                    return;
                }

                //ユーザーへの入力要求
                pt_option.Message = "\n2点目を指定: ";
                pt_option.UseBasePoint = true;
                pt_option.BasePoint = ptStart;
                pt_option.AllowNone = true;
                pt_res = now_doc.Editor.GetPoint(pt_option);
                Point3d ptEnd = pt_res.Value;


                //ESCキーなどでキャンセルしたときの処理
                if (pt_res.Status == PromptStatus.Cancel | pt_res.Status == PromptStatus.None)
                {
                    EndCommand();
                    return;
                }

                //ドキュメントのロックを解除
                using (DocumentLock docLock = now_doc.LockDocument())
                {
                    //トランザクションの開始
                    using (Transaction fir_trans = db.TransactionManager.StartTransaction())
                    {
                        //ブロックテーブルの作成
                        BlockTable b_table;
                        b_table = fir_trans.GetObject(db.BlockTableId,
                                                        OpenMode.ForRead) as BlockTable;

                        BlockTableRecord b_table_rec;
                        b_table_rec = fir_trans.GetObject(b_table[BlockTableRecord.ModelSpace],
                                                        OpenMode.ForWrite) as BlockTableRecord;
                        // Create a line that starts at 5,5 and ends at 12,3
                        using (Line acLine = new Line(ptStart,
                                                      ptEnd))
                        {
                            acLine.Layer = material;
                            //オフセットを作成
                            DBObjectCollection acDbObjColl = acLine.GetOffsetCurves(rad);
                            DBObjectCollection acDbObjColl2 = acLine.GetOffsetCurves(-rad);

                            // Step through the new objects created
                            foreach (Entity acEnt in acDbObjColl)
                            {
                                // Add each offset object
                                b_table_rec.AppendEntity(acEnt);
                                fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                            }
                            // Step through the new objects created
                            foreach (Entity acEnt in acDbObjColl2)
                            {
                                // Add each offset object
                                b_table_rec.AppendEntity(acEnt);
                                fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                            }

                            // Add the new object to the block table record and the transaction
                            b_table_rec.AppendEntity(acLine);
                            fir_trans.AddNewlyCreatedDBObject(acLine, true);
                            acLine.Layer = "CEN";
                        }


                        //トランザクション終了
                        fir_trans.Commit();
                    }
                }
            } while (true);
        }
        /// <summary>
        /// 45度エルボを生成し、ブロックに登録
        /// </summary>

        public void Generate45Elbow(string name, double s_angle = 0)
        {

            Elbow elb = null;
            using (LiteDatabase litedb = new LiteDatabase(DataBase.db_dir))
            {
                var col = litedb.GetCollection<Elbow>("Elbow");

                var result = col.Query()
                    .Where(x => x.BName.Equals(name))
                    .ToList();
                elb = result[0];
            }
            CreateLayer(elb.Material);
            CreateLayer("CEN");

            Document now_doc = acApplication.DocumentManager.MdiActiveDocument;
            Database db = now_doc.Database;
            Point3d origin = new Point3d(0, 0, 0);

            horizontal(elb);
            down(elb);
            up(elb);
            vertical(elb);
            void horizontal(Elbow dim)
            {
                //平面--------------------------------------------------------------------------------
                //ドキュメントのロックを解除
                using (DocumentLock docLock = now_doc.LockDocument())
                {
                    //トランザクションの開始
                    using (Transaction fir_trans = db.TransactionManager.StartTransaction())
                    {

                        //ブロックテーブルの作成
                        BlockTable b_table;
                        b_table = fir_trans.GetObject(db.BlockTableId,
                                                        OpenMode.ForRead) as BlockTable;
                        string blkName = dim.BName + Blk_suffix[0];
                        if (!b_table.Has(blkName))
                        {
                            BlockTableRecord b_table_rec = new BlockTableRecord();
                            //b_table_rec = fir_trans.GetObject(b_table[BlockTableRecord.ModelSpace],
                            //OpenMode.ForWrite) as BlockTableRecord;

                            b_table_rec.Name = blkName;
                            b_table_rec.Origin = new Point3d(0, 0, 0);
                            Point3d target = target = new Point3d(dim.Length,0 , 0);
                            // 中心線を描画---------------------------------------------------------
                            // X
                            using (Line acLine = new Line(origin,
                                                            target))
                            {
                                acLine.Layer = "CEN";

                                b_table_rec.AppendEntity(acLine);

                                //ブロックを定義する場合の最初のオブジェクトはこれが必要
                                //-------------------------------------------------------------
                                fir_trans.GetObject(db.BlockTableId, OpenMode.ForWrite);
                                b_table.Add(b_table_rec);
                                fir_trans.AddNewlyCreatedDBObject(b_table_rec, true);
                                //------------------------------------------------------
                            }

                            // Y
                            target = util.PolarPoint(dim.Length, 225);
                            using (Line acLine = new Line(origin,
                                    target))
                            {
                                acLine.Layer = "CEN";

                                b_table_rec.AppendEntity(acLine);
                            }

                            // R
                            target = util.PolarPoint(dim.Length, 45);
                            double R = target[0] * 2 + dim.Length;
                            target = new Point3d(dim.Length, -R, 0);
                            double ang1 = util.DegreeToRadian(90);
                            double ang2 = util.DegreeToRadian(135);
                            using (Arc acArc = new Arc(target,
                                                R, ang1, ang2))
                            {
                                acArc.Layer = "0";
                                acArc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);
                                //オフセットを作成
                                DBObjectCollection acDbObjColl = acArc.GetOffsetCurves(dim.Orad);
                                DBObjectCollection acDbObjColl2 = acArc.GetOffsetCurves(-dim.Orad);

                                // Step through the new objects created
                                foreach (Entity acEnt in acDbObjColl)
                                {
                                    // Add each offset object
                                    b_table_rec.AppendEntity(acEnt);
                                    fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                }
                                // Step through the new objects created
                                foreach (Entity acEnt in acDbObjColl2)
                                {
                                    // Add each offset object
                                    b_table_rec.AppendEntity(acEnt);
                                    fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                }

                                acArc.Layer = "CEN";
                                acArc.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                                b_table_rec.AppendEntity(acArc);
                            }
                            // アウトラインを描画---------------------------------------------------------
                            // X
                            Point3d target1 = new Point3d(dim.Length, dim.Orad, 0);
                            Point3d target2 = new Point3d(dim.Length, -dim.Orad, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acLine);
                            }
                            // Y
                            target = util.PolarPoint(dim.Length, 225);
                            Point3d m = new Point3d(target[0], target[1], 0);
                            target1 = util.PolarPoint(dim.Orad, 135, m[0], m[1]);
                            target2 = util.PolarPoint(dim.Orad, -45, m[0], m[1]);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acLine);
                            }
                            // サイズ　テキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString = dim.Name;
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 4;
                                acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(dim.Length - acText.Height / 4, acText.Height / 2, 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }
                        }

                        //トランザクション終了
                        fir_trans.Commit();
                    }
                }
            }
            void down(Elbow dim)
            {
                //平面--------------------------------------------------------------------------------
                //ドキュメントのロックを解除
                using (DocumentLock docLock = now_doc.LockDocument())
                {
                    //トランザクションの開始
                    using (Transaction fir_trans = db.TransactionManager.StartTransaction())
                    {

                        //ブロックテーブルの作成
                        BlockTable b_table;
                        b_table = fir_trans.GetObject(db.BlockTableId,
                                                        OpenMode.ForRead) as BlockTable;
                        string blkName = dim.BName + Blk_suffix[1];
                        if (!b_table.Has(blkName))
                        {
                            BlockTableRecord b_table_rec = new BlockTableRecord();
                            //b_table_rec = fir_trans.GetObject(b_table[BlockTableRecord.ModelSpace],
                            //OpenMode.ForWrite) as BlockTableRecord;

                            b_table_rec.Name = blkName;
                            b_table_rec.Origin = new Point3d(0, 0, 0);
                            Command cm = new Command();
                            double buf = cm.Center_buf;
                            Point3d cut = util.PolarPoint(dim.Orad, 45);
                            Point3d Plen = util.PolarPoint(dim.Length, 45);
                            Point3d target1 = new Point3d(-cut[0]*2 * buf, 0, 0);
                            Point3d target2 = new Point3d(dim.Length, 0, 0);
                            // 中心線を描画---------------------------------------------------------
                            // X
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "CEN";

                                b_table_rec.AppendEntity(acLine);

                                //ブロックを定義する場合の最初のオブジェクトはこれが必要
                                //-------------------------------------------------------------
                                fir_trans.GetObject(db.BlockTableId, OpenMode.ForWrite);
                                b_table.Add(b_table_rec);
                                fir_trans.AddNewlyCreatedDBObject(b_table_rec, true);
                                //------------------------------------------------------
                            }

                            // Y
                            target1 = new Point3d(0, -dim.Orad * buf, 0);
                            target2 = new Point3d(0, dim.Orad * buf, 0);
                            using (Line acLine = new Line(target1,
                                    target2))
                            {
                                acLine.Layer = "CEN";
                                //オフセットを作成
                                DBObjectCollection acDbObjColl = acLine.GetOffsetCurves(Plen[0]);

                                // Step through the new objects created
                                foreach (Entity acEnt in acDbObjColl)
                                {
                                    // Add each offset object
                                    b_table_rec.AppendEntity(acEnt);
                                    fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                }
                                b_table_rec.AppendEntity(acLine);
                            }

                            // アウトラインを描画---------------------------------------------------------
                            // X
                            target1 = new Point3d(-Plen[0] , dim.Orad, 0);
                            target2 = new Point3d(dim.Length, dim.Orad, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                //オフセットを作成
                                DBObjectCollection acDbObjColl = acLine.GetOffsetCurves(-dim.Odia);

                                // Step through the new objects created
                                foreach (Entity acEnt in acDbObjColl)
                                {
                                    // Add each offset object
                                    b_table_rec.AppendEntity(acEnt);
                                    fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                }

                                b_table_rec.AppendEntity(acLine);
                            }
                            // EndCap
                            target1 = new Point3d(dim.Length, dim.Orad, 0);
                            target2 = new Point3d(dim.Length, -dim.Orad, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acLine);
                            }

                            // Ellipse
                            Point3d tp = new Point3d(-Plen[0], 0,0);
                            Vector3d normal = Vector3d.ZAxis;
                            Vector3d majorAxis = dim.Orad * Vector3d.YAxis;
                            double radiusRatio = cut[0]/dim.Orad;
                            double startAng = 0 ;
                            double endAng = 180 * Math.Atan(1.0) / 45.0;

                            using (Ellipse acArc = new Ellipse(tp, normal
                                                , majorAxis, radiusRatio, startAng, endAng))
                            {
                                acArc.Layer = "0";
                                acArc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acArc);
                            }
                            // サイズ　テキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString = dim.Name;
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 4;
                                acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(dim.Length - acText.Height / 4, acText.Height / 2, 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }
                        }

                        //トランザクション終了
                        fir_trans.Commit();
                    }
                }
            }
            void up(Elbow dim)
            {
                //平面--------------------------------------------------------------------------------
                //ドキュメントのロックを解除
                using (DocumentLock docLock = now_doc.LockDocument())
                {
                    //トランザクションの開始
                    using (Transaction fir_trans = db.TransactionManager.StartTransaction())
                    {

                        //ブロックテーブルの作成
                        BlockTable b_table;
                        b_table = fir_trans.GetObject(db.BlockTableId,
                                                        OpenMode.ForRead) as BlockTable;
                        string blkName = dim.BName + Blk_suffix[2];
                        if (!b_table.Has(blkName))
                        {
                            BlockTableRecord b_table_rec = new BlockTableRecord();
                            //b_table_rec = fir_trans.GetObject(b_table[BlockTableRecord.ModelSpace],
                            //OpenMode.ForWrite) as BlockTableRecord;

                            b_table_rec.Name = blkName;
                            b_table_rec.Origin = new Point3d(0, 0, 0);
                            Command cm = new Command();
                            double buf = cm.Center_buf;
                            Point3d cut = util.PolarPoint(dim.Orad, 45);
                            Point3d Plen = util.PolarPoint(dim.Length, 45);
                            Point3d target1 = new Point3d(-cut[0] * 2 * buf, 0, 0);
                            Point3d target2 = new Point3d(dim.Length, 0, 0);
                            // 中心線を描画---------------------------------------------------------
                            // X
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "CEN";

                                b_table_rec.AppendEntity(acLine);

                                //ブロックを定義する場合の最初のオブジェクトはこれが必要
                                //-------------------------------------------------------------
                                fir_trans.GetObject(db.BlockTableId, OpenMode.ForWrite);
                                b_table.Add(b_table_rec);
                                fir_trans.AddNewlyCreatedDBObject(b_table_rec, true);
                                //------------------------------------------------------
                            }

                            // Y
                            target1 = new Point3d(0, -dim.Orad * buf, 0);
                            target2 = new Point3d(0, dim.Orad * buf, 0);
                            using (Line acLine = new Line(target1,
                                    target2))
                            {
                                acLine.Layer = "CEN";
                                //オフセットを作成
                                DBObjectCollection acDbObjColl = acLine.GetOffsetCurves(Plen[0]);

                                // Step through the new objects created
                                foreach (Entity acEnt in acDbObjColl)
                                {
                                    // Add each offset object
                                    b_table_rec.AppendEntity(acEnt);
                                    fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                }
                                b_table_rec.AppendEntity(acLine);
                            }

                            // アウトラインを描画---------------------------------------------------------
                            // X
                            target1 = new Point3d(-cut[0], dim.Orad, 0);
                            target2 = new Point3d(dim.Length, dim.Orad, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                //オフセットを作成
                                DBObjectCollection acDbObjColl = acLine.GetOffsetCurves(-dim.Odia);

                                // Step through the new objects created
                                foreach (Entity acEnt in acDbObjColl)
                                {
                                    // Add each offset object
                                    b_table_rec.AppendEntity(acEnt);
                                    fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                }

                                b_table_rec.AppendEntity(acLine);
                            }
                            // EndCap
                            target1 = new Point3d(dim.Length, dim.Orad, 0);
                            target2 = new Point3d(dim.Length, -dim.Orad, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acLine);
                            }

                            // Outer Ellipse
                            Point3d tp = new Point3d(-Plen[0], 0, 0);
                            Vector3d normal = Vector3d.ZAxis;
                            Vector3d majorAxis = dim.Orad * Vector3d.YAxis;
                            double radiusRatio = cut[0] / dim.Orad;
                            double startAng = 0;
                            double endAng = 360 * Math.Atan(1.0) / 45.0;

                            using (Ellipse acArc = new Ellipse(tp, normal
                                                , majorAxis, radiusRatio, startAng, endAng))
                            {
                                acArc.Layer = "0";
                                acArc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acArc);
                            }
                            // Inner Ellipse

                            majorAxis = dim.Irad * Vector3d.YAxis;

                            using (Ellipse acArc = new Ellipse(tp, normal
                                                , majorAxis, radiusRatio, startAng, endAng))
                            {
                                acArc.Layer = "0";
                                acArc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acArc);
                            }
                            // サイズ　テキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString = dim.Name;
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 4;
                                acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(dim.Length - acText.Height / 4, acText.Height / 2, 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }
                            // ハッチング
                            endAng = 90 * Math.Atan(1.0) / 45.0;
                            Matrix3d curUCSMatrix = now_doc.Editor.CurrentUserCoordinateSystem;
                            CoordinateSystem3d curUCS = curUCSMatrix.CoordinateSystem3d;
                            Point3d in_target = util.PolarPoint(dim.Irad, 45);
                            ObjectIdCollection acObjIdColl = new ObjectIdCollection();

                            using (Ellipse acArc = new Ellipse(tp, normal
                                                                , majorAxis, radiusRatio, startAng, endAng))
                            {

                                acArc.Layer = dim.Material;
                                //回転コピー
                                Ellipse acArcClone = acArc.Clone() as Ellipse;
                                Double rotate_angle = util.DegreeToRadian(180);
                                acArcClone.TransformBy(Matrix3d.Rotation(rotate_angle,
                                                                        curUCS.Zaxis,
                                                                        new Point3d(-Plen[0], 0, 0)));
                                b_table_rec.AppendEntity(acArc);
                                b_table_rec.AppendEntity(acArcClone);

                                fir_trans.AddNewlyCreatedDBObject(acArc, true);
                                fir_trans.AddNewlyCreatedDBObject(acArcClone, true);

                                acObjIdColl.Add(acArc.ObjectId);
                                acObjIdColl.Add(acArcClone.ObjectId);

                                Point3d target = util.PolarPoint(dim.Length, 45);
                                    
                                target1 = new Point3d(-Plen[0], dim.Irad, 0);
                                target2 = new Point3d(-Plen[0], -dim.Irad, 0);

                                Line acPoly = new Line(target1, target2);
                                b_table_rec.AppendEntity(acPoly);
                                fir_trans.AddNewlyCreatedDBObject(acPoly, true);

                                acObjIdColl.Add(acPoly.ObjectId);
                                target1 = new Point3d(-Plen[0]-in_target[0], 0, 0);
                                target2 = new Point3d(-Plen[0]+ in_target[0], 0, 0);

                                Line acPoly2 = new Line(target1, target2);
                                b_table_rec.AppendEntity(acPoly2);
                                fir_trans.AddNewlyCreatedDBObject(acPoly2, true);

                                acObjIdColl.Add(acPoly2.ObjectId);

                                // Create the hatch object and append it to the block table record
                                using (Hatch acHatch = new Hatch())
                                {
                                    b_table_rec.AppendEntity(acHatch);
                                    fir_trans.AddNewlyCreatedDBObject(acHatch, true);

                                    // Set the properties of the hatch object
                                    // Associative must be set after the hatch object is appended to the 
                                    // block table record and before AppendLoop
                                    acHatch.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31");
                                    acHatch.Associative = true;
                                    acHatch.AppendLoop(HatchLoopTypes.Default, acObjIdColl);
                                    acHatch.EvaluateHatch(true);
                                    //ハッチングのレイヤー設定
                                    acHatch.Layer = dim.Material;
                                    acHatch.ColorIndex = 1;
                                }
                                //不要なオブジェクトを削除
                                acArc.Erase();
                                acArcClone.Erase();
                                acPoly.Erase();
                                acPoly2.Erase();
                            }
                        }
                        //トランザクション終了
                        fir_trans.Commit();
                    }
                }
            }
            void vertical(Elbow dim)
            {
                //平面--------------------------------------------------------------------------------
                //ドキュメントのロックを解除
                using (DocumentLock docLock = now_doc.LockDocument())
                {
                    //トランザクションの開始
                    using (Transaction fir_trans = db.TransactionManager.StartTransaction())
                    {

                        //ブロックテーブルの作成
                        BlockTable b_table;
                        b_table = fir_trans.GetObject(db.BlockTableId,
                                                        OpenMode.ForRead) as BlockTable;
                        string blkName = dim.BName + Blk_suffix[3];
                        if (!b_table.Has(blkName))
                        {
                            BlockTableRecord b_table_rec = new BlockTableRecord();
                            //b_table_rec = fir_trans.GetObject(b_table[BlockTableRecord.ModelSpace],
                            //OpenMode.ForWrite) as BlockTableRecord;

                            b_table_rec.Name = blkName;
                            b_table_rec.Origin = new Point3d(0, 0, 0);
                            Command cm = new Command();
                            double buf = cm.Center_buf;
                            Point3d cut = util.PolarPoint(dim.Orad, 45);
                            Point3d Plen = util.PolarPoint(dim.Length, 45);
                            Point3d target1 = new Point3d(-cut[0] * 2 * buf, 0, 0);
                            Point3d target2 = new Point3d(dim.Length, 0, 0);
                            // 中心線を描画---------------------------------------------------------
                            // X
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "CEN";

                                b_table_rec.AppendEntity(acLine);

                                //ブロックを定義する場合の最初のオブジェクトはこれが必要
                                //-------------------------------------------------------------
                                fir_trans.GetObject(db.BlockTableId, OpenMode.ForWrite);
                                b_table.Add(b_table_rec);
                                fir_trans.AddNewlyCreatedDBObject(b_table_rec, true);
                                //------------------------------------------------------
                            }

                            // Y
                            target1 = new Point3d(0, -dim.Orad * buf, 0);
                            target2 = new Point3d(0, dim.Orad * buf, 0);
                            using (Line acLine = new Line(target1,
                                    target2))
                            {
                                acLine.Layer = "CEN";
                                //オフセットを作成
                                DBObjectCollection acDbObjColl = acLine.GetOffsetCurves(Plen[0]);

                                // Step through the new objects created
                                foreach (Entity acEnt in acDbObjColl)
                                {
                                    // Add each offset object
                                    b_table_rec.AppendEntity(acEnt);
                                    fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                }
                                b_table_rec.AppendEntity(acLine);
                            }

                            // アウトラインを描画---------------------------------------------------------
                            // X
                            target1 = new Point3d(-Plen[0], dim.Orad, 0);
                            target2 = new Point3d(dim.Length, dim.Orad, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                //オフセットを作成
                                DBObjectCollection acDbObjColl = acLine.GetOffsetCurves(-dim.Odia);

                                // Step through the new objects created
                                foreach (Entity acEnt in acDbObjColl)
                                {
                                    // Add each offset object
                                    b_table_rec.AppendEntity(acEnt);
                                    fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                }

                                b_table_rec.AppendEntity(acLine);
                            }
                            // EndCap
                            target1 = new Point3d(dim.Length, dim.Orad, 0);
                            target2 = new Point3d(dim.Length, -dim.Orad, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acLine);
                            }

                            // Ellipse
                            Point3d tp = new Point3d(-Plen[0], 0, 0);
                            Vector3d normal = Vector3d.ZAxis;
                            Vector3d majorAxis = dim.Orad * Vector3d.YAxis;
                            double radiusRatio = cut[0] / dim.Orad;
                            double startAng = 180 * Math.Atan(1.0) / 45.0;
                            double endAng = 0;

                            using (Ellipse acArc = new Ellipse(tp, normal
                                                , majorAxis, radiusRatio, startAng, endAng))
                            {
                                acArc.Layer = "0";
                                acArc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acArc);
                            }
                            // サイズ　テキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString = dim.Name;
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 4;
                                acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(dim.Length - acText.Height / 4, acText.Height / 2, 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }
                        }

                        //トランザクション終了
                        fir_trans.Commit();
                    }
                }
            }


            //平面--------------------------------------------------------------------------------
        }
        /// <summary>
        /// 90度エルボを生成し、ブロックに登録
        /// </summary>

        public void Generate90Elbow(string name ,double s_angle = 0)
        {

            Elbow elb = null;
            using (LiteDatabase litedb = new LiteDatabase(DataBase.db_dir))
            {
                var col = litedb.GetCollection<Elbow>("Elbow");

                var result = col.Query()
                    .Where(x => x.BName.Equals(name))
                    .ToList();
                elb = result[0];
            }
            CreateLayer(elb.Material);
            CreateLayer("CEN");
            Document now_doc = acApplication.DocumentManager.MdiActiveDocument;
            Database db = now_doc.Database;
            Point3d origin = new Point3d(0, 0, 0);

            if (s_angle != 0)
            {
                slope_up(elb, s_angle);
                slope_down(elb, -s_angle);
                slope_vertical(elb, s_angle);
            }
            horizontal(elb);
            down(elb);
            up(elb);
            vertical(elb);

            void horizontal(Elbow dim)
            {
                //平面--------------------------------------------------------------------------------
                //ドキュメントのロックを解除
                using (DocumentLock docLock = now_doc.LockDocument())
                {
                    //トランザクションの開始
                    using (Transaction fir_trans = db.TransactionManager.StartTransaction())
                    {

                        //ブロックテーブルの作成
                        BlockTable b_table;
                        b_table = fir_trans.GetObject(db.BlockTableId,
                                                        OpenMode.ForRead) as BlockTable;
                        string blkName = dim.BName + Blk_suffix[0];
                        if (!b_table.Has(blkName))
                        {
                            BlockTableRecord b_table_rec = new BlockTableRecord();
                            //b_table_rec = fir_trans.GetObject(b_table[BlockTableRecord.ModelSpace],
                            //OpenMode.ForWrite) as BlockTableRecord;

                            b_table_rec.Name = blkName;
                            b_table_rec.Origin = new Point3d(0, 0, 0);
                            Point3d target = new Point3d(dim.Length, 0, 0);
                            // 中心線を描画---------------------------------------------------------
                            // X
                            using (Line acLine = new Line(origin,
                                                            target))
                            {
                                acLine.Layer = "CEN";

                                b_table_rec.AppendEntity(acLine);

                                //ブロックを定義する場合の最初のオブジェクトはこれが必要
                                //-------------------------------------------------------------
                                fir_trans.GetObject(db.BlockTableId, OpenMode.ForWrite);
                                b_table.Add(b_table_rec);
                                fir_trans.AddNewlyCreatedDBObject(b_table_rec, true);
                                //------------------------------------------------------
                            }

                            // Y
                            target = new Point3d(0, -dim.Length, 0);
                            using (Line acLine = new Line(origin,
                                    target))
                            {
                                acLine.Layer = "CEN";

                                b_table_rec.AppendEntity(acLine);
                            }

                            // R
                            target = new Point3d(dim.Length, -dim.Length, 0);
                            double ang1 = util.DegreeToRadian(90);
                            double ang2 = util.DegreeToRadian(180);
                            using (Arc acArc = new Arc(target,
                                                dim.R, ang1, ang2))
                            {
                                acArc.Layer = "0";
                                acArc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);
                                //オフセットを作成
                                DBObjectCollection acDbObjColl = acArc.GetOffsetCurves(dim.Orad);
                                DBObjectCollection acDbObjColl2 = acArc.GetOffsetCurves(-dim.Orad);

                                // Step through the new objects created
                                foreach (Entity acEnt in acDbObjColl)
                                {
                                    // Add each offset object
                                    b_table_rec.AppendEntity(acEnt);
                                    fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                }
                                // Step through the new objects created
                                foreach (Entity acEnt in acDbObjColl2)
                                {
                                    // Add each offset object
                                    b_table_rec.AppendEntity(acEnt);
                                    fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                }

                                acArc.Layer = "CEN";
                                acArc.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                                b_table_rec.AppendEntity(acArc);
                            }
                            // アウトラインを描画---------------------------------------------------------
                            // X
                            Point3d target1 = new Point3d(dim.Length, dim.Orad, 0);
                            Point3d target2 = new Point3d(dim.Length, -dim.Orad, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acLine);
                            }
                            // Y
                            target1 = new Point3d(dim.Orad, -dim.Length, 0);
                            target2 = new Point3d(-dim.Orad, -dim.Length, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acLine);
                            }
                            // サイズ　テキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString = dim.Name;
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 4;
                                acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(dim.Length- acText.Height / 4, acText.Height / 2, 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }
                        }

                        //トランザクション終了
                        fir_trans.Commit();
                    }
                }
            }
            void down(Elbow dim)
            {
                //平面--------------------------------------------------------------------------------
                //ドキュメントのロックを解除
                using (DocumentLock docLock = now_doc.LockDocument())
                {
                    //トランザクションの開始
                    using (Transaction fir_trans = db.TransactionManager.StartTransaction())
                    {

                        //ブロックテーブルの作成
                        BlockTable b_table;
                        b_table = fir_trans.GetObject(db.BlockTableId,
                                                        OpenMode.ForRead) as BlockTable;
                        string blkName = dim.BName + Blk_suffix[1];
                        if (!b_table.Has(blkName))
                        {
                            BlockTableRecord b_table_rec = new BlockTableRecord();
                            //b_table_rec = fir_trans.GetObject(b_table[BlockTableRecord.ModelSpace],
                            //OpenMode.ForWrite) as BlockTableRecord;

                            b_table_rec.Name = blkName;
                            b_table_rec.Origin = new Point3d(0, 0, 0);
                            Command cm = new Command();
                            double buf = cm.Center_buf;
                            Point3d target1 = new Point3d(-dim.Orad * buf, 0, 0);
                            Point3d target2 = new Point3d(dim.Length, 0, 0);
                            // 中心線を描画---------------------------------------------------------
                            // X
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "CEN";

                                b_table_rec.AppendEntity(acLine);

                                //ブロックを定義する場合の最初のオブジェクトはこれが必要
                                //-------------------------------------------------------------
                                fir_trans.GetObject(db.BlockTableId, OpenMode.ForWrite);
                                b_table.Add(b_table_rec);
                                fir_trans.AddNewlyCreatedDBObject(b_table_rec, true);
                                //------------------------------------------------------
                            }

                            // Y
                            target1 = new Point3d(0, -dim.Orad * buf, 0);
                            target2 = new Point3d(0, dim.Orad * buf, 0);
                            using (Line acLine = new Line(target1,
                                    target2))
                            {
                                acLine.Layer = "CEN";

                                b_table_rec.AppendEntity(acLine);
                            }

                            // アウトラインを描画---------------------------------------------------------
                            // X
                            target1 = new Point3d(0, dim.Orad, 0);
                            target2 = new Point3d(dim.Length, dim.Orad, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                //オフセットを作成
                                DBObjectCollection acDbObjColl = acLine.GetOffsetCurves(-dim.Odia);

                                // Step through the new objects created
                                foreach (Entity acEnt in acDbObjColl)
                                {
                                    // Add each offset object
                                    b_table_rec.AppendEntity(acEnt);
                                    fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                }

                                b_table_rec.AppendEntity(acLine);
                            }
                            // EndCap
                            target1 = new Point3d(dim.Length, dim.Orad, 0);
                            target2 = new Point3d(dim.Length, -dim.Orad, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acLine);
                            }
                            // Ellipse
                            double ang1 = util.DegreeToRadian(90);
                            double ang2 = util.DegreeToRadian(-90);
                            using (Arc acArc = new Arc(origin,
                                                dim.Orad, ang1, ang2))
                            {
                                acArc.Layer = "0";
                                acArc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acArc);
                            }
                            // サイズ　テキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString = dim.Name;
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 4;
                                acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(dim.Length - acText.Height / 4, acText.Height / 2, 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }
                        }

                        //トランザクション終了
                        fir_trans.Commit();
                    }
                }
            }
            void up(Elbow dim)
            {
                //平面--------------------------------------------------------------------------------
                //ドキュメントのロックを解除
                using (DocumentLock docLock = now_doc.LockDocument())
                {
                    //トランザクションの開始
                    using (Transaction fir_trans = db.TransactionManager.StartTransaction())
                    {

                        //ブロックテーブルの作成
                        BlockTable b_table;
                        b_table = fir_trans.GetObject(db.BlockTableId,
                                                        OpenMode.ForRead) as BlockTable;
                        string blkName = dim.BName + Blk_suffix[2];
                        if (!b_table.Has(blkName))
                        {
                            BlockTableRecord b_table_rec = new BlockTableRecord();
                            //b_table_rec = fir_trans.GetObject(b_table[BlockTableRecord.ModelSpace],
                            //OpenMode.ForWrite) as BlockTableRecord;

                            b_table_rec.Name = blkName;
                            b_table_rec.Origin = new Point3d(0, 0, 0);
                            Command cm = new Command();
                            double buf = cm.Center_buf;
                            Point3d target1 = new Point3d(-dim.Orad * buf, 0, 0);
                            Point3d target2 = new Point3d(dim.Length, 0, 0);
                            // 中心線を描画---------------------------------------------------------
                            // X
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "CEN";

                                b_table_rec.AppendEntity(acLine);

                                //ブロックを定義する場合の最初のオブジェクトはこれが必要
                                //-------------------------------------------------------------
                                fir_trans.GetObject(db.BlockTableId, OpenMode.ForWrite);
                                b_table.Add(b_table_rec);
                                fir_trans.AddNewlyCreatedDBObject(b_table_rec, true);
                                //------------------------------------------------------
                            }

                            // Y
                            target1 = new Point3d(0, -dim.Orad * buf, 0);
                            target2 = new Point3d(0, dim.Orad * buf, 0);
                            using (Line acLine = new Line(target1,
                                    target2))
                            {
                                acLine.Layer = "CEN";

                                b_table_rec.AppendEntity(acLine);
                            }

                            // アウトラインを描画---------------------------------------------------------
                            // X
                            target1 = new Point3d(0, dim.Orad, 0);
                            target2 = new Point3d(dim.Length, dim.Orad, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                //オフセットを作成
                                DBObjectCollection acDbObjColl = acLine.GetOffsetCurves(-dim.Odia);

                                // Step through the new objects created
                                foreach (Entity acEnt in acDbObjColl)
                                {
                                    // Add each offset object
                                    b_table_rec.AppendEntity(acEnt);
                                    fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                }

                                b_table_rec.AppendEntity(acLine);
                            }
                            // EndCap
                            target1 = new Point3d(dim.Length, dim.Orad, 0);
                            target2 = new Point3d(dim.Length, -dim.Orad, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acLine);
                            }
                            // Outer_circle
                            using (Circle acCirc = new Circle())
                            {
                                acCirc.Center = origin;
                                acCirc.Radius = dim.Orad;
                                acCirc.Layer = "0";
                                acCirc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acCirc);
                            }
                            // Inner_circle
                            using (Circle acCirc = new Circle())
                            {
                                acCirc.Center = origin;
                                acCirc.Radius = dim.Irad;
                                acCirc.Layer = "0";
                                acCirc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acCirc);
                            }
                            // サイズ　テキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString = dim.Name;
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 4;
                                acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(dim.Length - acText.Height / 4, acText.Height / 2, 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }
                            // ハッチング
                            // Create a circle object for the closed boundary to hatch
                            using (Polyline acPoly = new Polyline())
                            {
                                acPoly.AddVertexAt(0, new Point2d(0, dim.Irad), 0, 0, 0);
                                acPoly.AddVertexAt(1, new Point2d(0, -dim.Irad), -0.414, 0, 0);
                                acPoly.AddVertexAt(2, new Point2d(-dim.Irad, 0), 0, 0, 0);
                                acPoly.AddVertexAt(3, new Point2d(dim.Irad, 0), 0.414, 0, 0);
                                acPoly.Closed = true;

                                // Add the new circle object to the block table record and the transaction
                                b_table_rec.AppendEntity(acPoly);
                                fir_trans.AddNewlyCreatedDBObject(acPoly, true);

                                // Adds the circle to an object id array
                                ObjectIdCollection acObjIdColl = new ObjectIdCollection();
                                acObjIdColl.Add(acPoly.ObjectId);

                                // Create the hatch object and append it to the block table record
                                using (Hatch acHatch = new Hatch())
                                {
                                    b_table_rec.AppendEntity(acHatch);
                                    fir_trans.AddNewlyCreatedDBObject(acHatch, true);

                                    // Set the properties of the hatch object
                                    // Associative must be set after the hatch object is appended to the 
                                    // block table record and before AppendLoop
                                    acHatch.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31");
                                    acHatch.Associative = true;
                                    acHatch.AppendLoop(HatchLoopTypes.Default, acObjIdColl);
                                    acHatch.EvaluateHatch(true);
                                    //ハッチングのレイヤー設定
                                    acHatch.Layer = dim.Material;
                                    acHatch.ColorIndex = 1;
                                }
                                //不要なポリラインを削除
                                Entity ent = (Entity)fir_trans.GetObject(acPoly.ObjectId,
                                                                    OpenMode.ForWrite);
                                ent.Erase();

                            }

                        }

                        //トランザクション終了
                        fir_trans.Commit();
                    }
                }
            }
            void vertical(Elbow dim)
            {
                //平面--------------------------------------------------------------------------------
                //ドキュメントのロックを解除
                using (DocumentLock docLock = now_doc.LockDocument())
                {
                    //トランザクションの開始
                    using (Transaction fir_trans = db.TransactionManager.StartTransaction())
                    {

                        //ブロックテーブルの作成
                        BlockTable b_table;
                        b_table = fir_trans.GetObject(db.BlockTableId,
                                                        OpenMode.ForRead) as BlockTable;
                        string blkName = dim.BName + Blk_suffix[3];
                        if (!b_table.Has(blkName))
                        {
                            BlockTableRecord b_table_rec = new BlockTableRecord();
                            //b_table_rec = fir_trans.GetObject(b_table[BlockTableRecord.ModelSpace],
                            //OpenMode.ForWrite) as BlockTableRecord;

                            b_table_rec.Name = blkName;
                            b_table_rec.Origin = new Point3d(0, 0, 0);
                            Command cm = new Command();
                            double buf = cm.Center_buf;
                            Point3d target1 = new Point3d(-dim.Orad * buf, 0, 0);
                            Point3d target2 = new Point3d(dim.Length, 0, 0);
                            // 中心線を描画---------------------------------------------------------
                            // X
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "CEN";

                                b_table_rec.AppendEntity(acLine);

                                //ブロックを定義する場合の最初のオブジェクトはこれが必要
                                //-------------------------------------------------------------
                                fir_trans.GetObject(db.BlockTableId, OpenMode.ForWrite);
                                b_table.Add(b_table_rec);
                                fir_trans.AddNewlyCreatedDBObject(b_table_rec, true);
                                //------------------------------------------------------
                            }

                            // Y
                            target1 = new Point3d(0, -dim.Orad * buf, 0);
                            target2 = new Point3d(0, dim.Orad * buf, 0);
                            using (Line acLine = new Line(target1,
                                    target2))
                            {
                                acLine.Layer = "CEN";

                                b_table_rec.AppendEntity(acLine);
                            }

                            // アウトラインを描画---------------------------------------------------------
                            // X
                            target1 = new Point3d(dim.Orad * buf, dim.Orad, 0);
                            target2 = new Point3d(dim.Length, dim.Orad, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                //オフセットを作成
                                DBObjectCollection acDbObjColl = acLine.GetOffsetCurves(-dim.Odia);

                                // Step through the new objects created
                                foreach (Entity acEnt in acDbObjColl)
                                {
                                    // Add each offset object
                                    b_table_rec.AppendEntity(acEnt);
                                    fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                }

                                b_table_rec.AppendEntity(acLine);
                            }
                            // EndCap
                            target1 = new Point3d(dim.Length, dim.Orad, 0);
                            target2 = new Point3d(dim.Length, -dim.Orad, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acLine);
                            }
                            // サイズ　テキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString = dim.Name;
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 4;
                                acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(dim.Length - acText.Height / 4, acText.Height / 2, 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }
                        }

                        //トランザクション終了
                        fir_trans.Commit();
                    }
                }
            }
            void slope_down(Elbow dim, Double sl_angle)
            {
                //平面--------------------------------------------------------------------------------
                //ドキュメントのロックを解除
                using (DocumentLock docLock = now_doc.LockDocument())
                {
                    //トランザクションの開始
                    using (Transaction fir_trans = db.TransactionManager.StartTransaction())
                    {

                        //ブロックテーブルの作成
                        BlockTable b_table;
                        b_table = fir_trans.GetObject(db.BlockTableId,
                                                        OpenMode.ForRead) as BlockTable;
                        string blkName = dim.BName + Blk_suffix[1] + Math.Abs(sl_angle).ToString();
                        s_angle = -s_angle;
                        if (!b_table.Has(blkName))
                        {
                            BlockTableRecord b_table_rec = new BlockTableRecord();
                            //b_table_rec = fir_trans.GetObject(b_table[BlockTableRecord.ModelSpace],
                            //OpenMode.ForWrite) as BlockTableRecord;

                            b_table_rec.Name = blkName;
                            b_table_rec.Origin = new Point3d(0, 0, 0);
                            Command cm = new Command();
                            double buf = cm.Center_buf;
                            Point3d cut = util.PolarPoint(dim.Orad, 270 + sl_angle);
                            Point3d Plen = util.PolarPoint(dim.Length, sl_angle);
                            Point3d target1 = new Point3d(0, 0, 0);
                            Point3d target2 = new Point3d(Plen[0] + (-cut[0] * Center_buf), 0, 0);
                            // 中心線を描画---------------------------------------------------------
                            // X
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "CEN";

                                b_table_rec.AppendEntity(acLine);

                                //ブロックを定義する場合の最初のオブジェクトはこれが必要
                                //-------------------------------------------------------------
                                fir_trans.GetObject(db.BlockTableId, OpenMode.ForWrite);
                                b_table.Add(b_table_rec);
                                fir_trans.AddNewlyCreatedDBObject(b_table_rec, true);
                                //------------------------------------------------------
                            }

                            // Y
                            target1 = new Point3d(0, 0, 0);
                            target2 = new Point3d(0, -dim.Length, 0);
                            using (Line acLine = new Line(target1,
                                    target2))
                            {
                                acLine.Layer = "CEN";
                                b_table_rec.AppendEntity(acLine);
                            }
                            target1 = new Point3d(Plen[0], dim.Orad * Center_buf, 0);
                            target2 = new Point3d(Plen[0], -dim.Orad * Center_buf, 0);
                            using (Line acLine = new Line(target1,
                                    target2))
                            {
                                acLine.Layer = "CEN";
                                b_table_rec.AppendEntity(acLine);
                            }
                            // R
                            Point3d tp = new Point3d(Plen[0], -dim.Length, 0);
                            Vector3d normal = Vector3d.ZAxis;
                            Vector3d majorAxis = dim.Length * Vector3d.YAxis;
                            double radiusRatio = Plen[0] / dim.Length;
                            double startAng = 0;
                            double endAng = 90 * Math.Atan(1.0) / 45.0;

                            using (Ellipse acArc = new Ellipse(tp, normal
                                    , majorAxis, radiusRatio, startAng, endAng))
                            {
                                //ターゲット地点が半径以下の場合
                                if (Plen[0] <= dim.Orad)
                                {
                                    acArc.Layer = "0";
                                    acArc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);
                                    //オフセットを作成
                                    DBObjectCollection acDbObjColl = acArc.GetOffsetCurves(dim.Orad);

                                    // Step through the new objects created
                                    foreach (Entity acEnt in acDbObjColl)
                                    {
                                        // Add each offset object
                                        b_table_rec.AppendEntity(acEnt);
                                        fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                    }

                                }
                                acArc.Layer = "CEN";
                                acArc.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                                b_table_rec.AppendEntity(acArc);

                                fir_trans.AddNewlyCreatedDBObject(acArc, true);
                            }
                            // Outer Ellipse
                            tp = new Point3d(Plen[0], 0, 0);
                            majorAxis = dim.Orad * Vector3d.YAxis;
                            radiusRatio = cut[0] / dim.Orad;
                            startAng = 0;
                            endAng = 180 * Math.Atan(1.0) / 45.0;

                            using (Ellipse acArc = new Ellipse(tp, normal
                                                , majorAxis, radiusRatio, startAng, endAng))
                            {
                                acArc.Layer = "0";
                                acArc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acArc);

                                //ターゲット地点が半径より大きいの場合
                                if (Plen[0] > dim.Orad)
                                {
                                    tp = new Point3d(Plen[0], -dim.Length, 0);
                                    endAng = 90 * Math.Atan(1.0) / 45.0;
                                    //アウトライン内側
                                    majorAxis = (dim.Length - dim.Orad) * Vector3d.YAxis;
                                    radiusRatio = (Plen[0] - dim.Orad) / (dim.Length - dim.Orad);
                                    using (Ellipse acArc2 = new Ellipse(tp, normal
                                            , majorAxis, radiusRatio, startAng, endAng))
                                    {
                                        acArc2.Layer = "0";
                                        acArc2.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                        b_table_rec.AppendEntity(acArc2);

                                        fir_trans.AddNewlyCreatedDBObject(acArc2, true);
                                    }
                                    //アウトライン外側
                                    majorAxis = (dim.Length + dim.Orad) * Vector3d.YAxis;
                                    radiusRatio = (Plen[0] + dim.Orad) / (dim.Length + dim.Orad);
                                    using (Ellipse acArc2 = new Ellipse(tp, normal
                                            , majorAxis, radiusRatio, startAng, endAng))
                                    {
                                        acArc2.Layer = "0";
                                        acArc2.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                        b_table_rec.AppendEntity(acArc2);

                                        fir_trans.AddNewlyCreatedDBObject(acArc2, true);
                                    }
                                }
                                else
                                {
                                    target1 = new Point3d(dim.Orad, -dim.Length, 0);
                                    target2 = new Point3d(dim.Orad, 0, 0);
                                    using (Line acLine = new Line(target1,
                                            target2))
                                    {
                                        Point3dCollection intersectPoints = new Point3dCollection();
                                        acLine.IntersectWith(acArc, Intersect.OnBothOperands, intersectPoints,
                                            IntPtr.Zero, IntPtr.Zero);
                                        acLine.EndPoint = intersectPoints[0];
                                        acLine.Layer = "0";
                                        acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);
                                        Vector3d vec = tp.GetVectorTo(intersectPoints[0]);
                                        double m_angle = tp.GetAsVector().GetAngleTo(vec);
                                        acArc.EndAngle = util.DegreeToRadian(90) + m_angle;
                                        b_table_rec.AppendEntity(acLine);
                                    }
                                }
                            }

                            // EndCap
                            target1 = new Point3d(-dim.Orad, -dim.Length, 0);
                            target2 = new Point3d(dim.Orad, -dim.Length, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acLine);
                            }



                            // サイズ　テキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString = dim.Name;
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 4;
                                //acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(acText.Height / 2, -dim.Length + acText.Height / 4, 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }
                            // HORテキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString = "HOR.";
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 6;
                                //acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(acText.Height / 2 - dim.Orad, -dim.Length + acText.Height / 4 + (acText.Height * 1.25), 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }
                            // HORテキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString = Math.Abs(sl_angle).ToString();
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 6;
                                //acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(acText.Height / 2 - dim.Orad, -dim.Length + acText.Height / 4, 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }

                        }
                        //トランザクション終了
                        fir_trans.Commit();
                    }
                }
            }
            void slope_up(Elbow dim,Double sl_angle)
            {
                //平面--------------------------------------------------------------------------------
                //ドキュメントのロックを解除
                using (DocumentLock docLock = now_doc.LockDocument())
                {
                    //トランザクションの開始
                    using (Transaction fir_trans = db.TransactionManager.StartTransaction())
                    {

                        //ブロックテーブルの作成
                        BlockTable b_table;
                        b_table = fir_trans.GetObject(db.BlockTableId,
                                                        OpenMode.ForRead) as BlockTable;
                        string blkName = dim.BName + Blk_suffix[2]+ Math.Abs(sl_angle).ToString();
                        if (!b_table.Has(blkName))
                        {
                            BlockTableRecord b_table_rec = new BlockTableRecord();
                            //b_table_rec = fir_trans.GetObject(b_table[BlockTableRecord.ModelSpace],
                            //OpenMode.ForWrite) as BlockTableRecord;

                            b_table_rec.Name = blkName;
                            b_table_rec.Origin = new Point3d(0, 0, 0);
                            Command cm = new Command();
                            double buf = cm.Center_buf;
                            Point3d cut = util.PolarPoint(dim.Orad, 270+sl_angle);
                            Point3d Plen = util.PolarPoint(dim.Length, sl_angle);
                            Point3d target1 = new Point3d(0,0,0);
                            Point3d target2 = new Point3d(Plen[0]+(cut[0]*Center_buf), 0, 0);
                            // 中心線を描画---------------------------------------------------------
                            // X
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "CEN";

                                b_table_rec.AppendEntity(acLine);

                                //ブロックを定義する場合の最初のオブジェクトはこれが必要
                                //-------------------------------------------------------------
                                fir_trans.GetObject(db.BlockTableId, OpenMode.ForWrite);
                                b_table.Add(b_table_rec);
                                fir_trans.AddNewlyCreatedDBObject(b_table_rec, true);
                                //------------------------------------------------------
                            }

                            // Y
                            target1 = new Point3d(0, 0, 0);
                            target2 = new Point3d(0, -dim.Length, 0);
                            using (Line acLine = new Line(target1,
                                    target2))
                            {
                                acLine.Layer = "CEN";
                                b_table_rec.AppendEntity(acLine);
                            }
                            target1 = new Point3d(Plen[0], dim.Orad*Center_buf, 0);
                            target2 = new Point3d(Plen[0], -dim.Orad * Center_buf, 0);
                            using (Line acLine = new Line(target1,
                                    target2))
                            {
                                acLine.Layer = "CEN";
                                b_table_rec.AppendEntity(acLine);
                            }
                            // R
                            Point3d tp = new Point3d(Plen[0], -dim.Length, 0);
                            Vector3d normal = Vector3d.ZAxis;
                            Vector3d majorAxis = dim.Length * Vector3d.YAxis;
                            double radiusRatio = Plen[0] / dim.Length;
                            double startAng = 0;
                            double endAng = 90 * Math.Atan(1.0) / 45.0;

                            using (Ellipse acArc = new Ellipse(tp, normal
                                    , majorAxis, radiusRatio, startAng, endAng))
                            {
                                //ターゲット地点が半径以下の場合
                                if (Plen[0] <= dim.Orad)
                                {
                                    acArc.Layer = "0";
                                    acArc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);
                                    //オフセットを作成
                                    DBObjectCollection acDbObjColl = acArc.GetOffsetCurves(dim.Orad);
                                    DBObjectCollection acDbObjColl2 = acArc.GetOffsetCurves(-dim.Orad);

                                    // Step through the new objects created
                                    foreach (Entity acEnt in acDbObjColl)
                                    {
                                        // Add each offset object
                                        b_table_rec.AppendEntity(acEnt);
                                        fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                    }
                                    // Step through the new objects created
                                    foreach (Entity acEnt in acDbObjColl2)
                                    {
                                        // Add each offset object
                                        b_table_rec.AppendEntity(acEnt);
                                        fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                    }
                                }

                                acArc.Layer = "CEN";
                                acArc.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                                b_table_rec.AppendEntity(acArc);

                                fir_trans.AddNewlyCreatedDBObject(acArc, true);
                            }
                            //ターゲット地点が半径より大きいの場合
                            if (Plen[0] > dim.Orad)
                            {
                                //アウトライン内側
                                majorAxis = (dim.Length - dim.Orad) * Vector3d.YAxis;
                                radiusRatio = (Plen[0] - dim.Orad) / (dim.Length - dim.Orad);
                                using (Ellipse acArc = new Ellipse(tp, normal
                                        , majorAxis, radiusRatio, startAng, endAng))
                                {
                                    acArc.Layer = "0";
                                    acArc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                    b_table_rec.AppendEntity(acArc);

                                    fir_trans.AddNewlyCreatedDBObject(acArc, true);
                                }
                                //アウトライン外側
                                majorAxis = (dim.Length + dim.Orad) * Vector3d.YAxis;
                                radiusRatio = (Plen[0] + dim.Orad) / (dim.Length + dim.Orad);
                                using (Ellipse acArc = new Ellipse(tp, normal
                                        , majorAxis, radiusRatio, startAng, endAng))
                                {
                                    acArc.Layer = "0";
                                    acArc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                    b_table_rec.AppendEntity(acArc);

                                    fir_trans.AddNewlyCreatedDBObject(acArc, true);
                                }
                            }
                            // EndCap
                            target1 = new Point3d(-dim.Orad, -dim.Length, 0);
                            target2 = new Point3d(dim.Orad, -dim.Length, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acLine);
                            }

                            // Outer Ellipse
                            tp = new Point3d(Plen[0], 0, 0);
                            majorAxis = dim.Orad * Vector3d.YAxis;
                            radiusRatio = cut[0] / dim.Orad;
                            startAng = 0;
                            endAng = 360 * Math.Atan(1.0) / 45.0;

                            using (Ellipse acArc = new Ellipse(tp, normal
                                                , majorAxis, radiusRatio, startAng, endAng))
                            {
                                acArc.Layer = "0";
                                acArc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acArc);
                            }
                            // Inner Ellipse

                            majorAxis = dim.Irad * Vector3d.YAxis;

                            using (Ellipse acArc = new Ellipse(tp, normal
                                                , majorAxis, radiusRatio, startAng, endAng))
                            {
                                acArc.Layer = "0";
                                acArc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acArc);
                            }
                            // サイズ　テキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString = dim.Name;
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 4;
                                //acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(acText.Height / 2, -dim.Length +acText.Height / 4, 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }
                            // HORテキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString = "HOR.";
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 6;
                                //acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(acText.Height / 2 - dim.Orad, -dim.Length + acText.Height / 4+ (acText.Height*1.25), 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }
                            // HORテキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString =Math.Abs(sl_angle).ToString();
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 6;
                                //acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(acText.Height / 2 - dim.Orad, -dim.Length + acText.Height / 4, 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }
                            // ハッチング
                            endAng = 90 * Math.Atan(1.0) / 45.0;
                            Matrix3d curUCSMatrix = now_doc.Editor.CurrentUserCoordinateSystem;
                            CoordinateSystem3d curUCS = curUCSMatrix.CoordinateSystem3d;
                            Point3d in_target = util.PolarPoint(dim.Irad, 270+sl_angle);
                            ObjectIdCollection acObjIdColl = new ObjectIdCollection();
                            
                            using (Ellipse acArc = new Ellipse(tp, normal
                                                                , majorAxis, radiusRatio, startAng, endAng))
                            {

                                acArc.Layer = dim.Material;
                                //回転コピー
                                Ellipse acArcClone = acArc.Clone() as Ellipse;
                                Double rotate_angle = util.DegreeToRadian(180);
                                acArcClone.TransformBy(Matrix3d.Rotation(rotate_angle,
                                                                        curUCS.Zaxis,
                                                                        new Point3d(Plen[0], 0, 0)));
                                b_table_rec.AppendEntity(acArc);
                                b_table_rec.AppendEntity(acArcClone);

                                fir_trans.AddNewlyCreatedDBObject(acArc, true);
                                fir_trans.AddNewlyCreatedDBObject(acArcClone, true);

                                acObjIdColl.Add(acArc.ObjectId);
                                acObjIdColl.Add(acArcClone.ObjectId);

                                Point3d target = util.PolarPoint(dim.Length, 45);

                                target1 = new Point3d(Plen[0], dim.Irad, 0);
                                target2 = new Point3d(Plen[0], -dim.Irad, 0);

                                Line acPoly = new Line(target1, target2);
                                b_table_rec.AppendEntity(acPoly);
                                fir_trans.AddNewlyCreatedDBObject(acPoly, true);

                                acObjIdColl.Add(acPoly.ObjectId);
                                target1 = new Point3d(Plen[0] - in_target[0], 0, 0);
                                target2 = new Point3d(Plen[0] + in_target[0], 0, 0);

                                Line acPoly2 = new Line(target1, target2);
                                b_table_rec.AppendEntity(acPoly2);
                                fir_trans.AddNewlyCreatedDBObject(acPoly2, true);

                                acObjIdColl.Add(acPoly2.ObjectId);
                                
                                // Create the hatch object and append it to the block table record
                                using (Hatch acHatch = new Hatch())
                                {
                                    b_table_rec.AppendEntity(acHatch);
                                    fir_trans.AddNewlyCreatedDBObject(acHatch, true);

                                    // Set the properties of the hatch object
                                    // Associative must be set after the hatch object is appended to the 
                                    // block table record and before AppendLoop
                                    acHatch.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31");
                                    acHatch.Associative = true;
                                    acHatch.AppendLoop(HatchLoopTypes.Default, acObjIdColl);
                                    acHatch.EvaluateHatch(true);
                                    //ハッチングのレイヤー設定
                                    acHatch.Layer = dim.Material;
                                    acHatch.ColorIndex = 1;
                                }
                                //不要なオブジェクトを削除
                                acArc.Erase();
                                acArcClone.Erase();
                                acPoly.Erase();
                                acPoly2.Erase();
                            }
                        }
                        //トランザクション終了
                        fir_trans.Commit();
                    }
                }
            }
            void slope_vertical(Elbow dim, Double sl_angle)
            {
                //平面--------------------------------------------------------------------------------
                //ドキュメントのロックを解除
                using (DocumentLock docLock = now_doc.LockDocument())
                {
                    //トランザクションの開始
                    using (Transaction fir_trans = db.TransactionManager.StartTransaction())
                    {

                        //ブロックテーブルの作成
                        BlockTable b_table;
                        b_table = fir_trans.GetObject(db.BlockTableId,
                                                        OpenMode.ForRead) as BlockTable;
                        string blkName = dim.BName + Blk_suffix[3] + Math.Abs(sl_angle).ToString();
                        s_angle = -s_angle;
                        if (!b_table.Has(blkName))
                        {
                            BlockTableRecord b_table_rec = new BlockTableRecord();
                            //b_table_rec = fir_trans.GetObject(b_table[BlockTableRecord.ModelSpace],
                            //OpenMode.ForWrite) as BlockTableRecord;

                            b_table_rec.Name = blkName;
                            b_table_rec.Origin = new Point3d(0, 0, 0);
                            Command cm = new Command();
                            double buf = cm.Center_buf;
                            Point3d cut = util.PolarPoint(dim.Orad, 270 + sl_angle);
                            Point3d Plen = util.PolarPoint(dim.Length, sl_angle);
                            Point3d target1 = new Point3d(0, 0, 0);
                            Point3d target2 = new Point3d(Plen[0] + (-cut[0] * Center_buf), 0, 0);
                            // 中心線を描画---------------------------------------------------------
                            // X
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "CEN";

                                b_table_rec.AppendEntity(acLine);

                                //ブロックを定義する場合の最初のオブジェクトはこれが必要
                                //-------------------------------------------------------------
                                fir_trans.GetObject(db.BlockTableId, OpenMode.ForWrite);
                                b_table.Add(b_table_rec);
                                fir_trans.AddNewlyCreatedDBObject(b_table_rec, true);
                                //------------------------------------------------------
                            }

                            // Y
                            target1 = new Point3d(0, 0, 0);
                            target2 = new Point3d(0, -dim.Length, 0);
                            using (Line acLine = new Line(target1,
                                    target2))
                            {
                                acLine.Layer = "CEN";
                                b_table_rec.AppendEntity(acLine);
                            }
                            target1 = new Point3d(Plen[0], dim.Orad * Center_buf, 0);
                            target2 = new Point3d(Plen[0], -dim.Orad * Center_buf, 0);
                            using (Line acLine = new Line(target1,
                                    target2))
                            {
                                acLine.Layer = "CEN";
                                b_table_rec.AppendEntity(acLine);
                            }
                            // R
                            Point3d tp = new Point3d(Plen[0], -dim.Length, 0);
                            Vector3d normal = Vector3d.ZAxis;
                            Vector3d majorAxis = dim.Length * Vector3d.YAxis;
                            double radiusRatio = Plen[0] / dim.Length;
                            double startAng = 0;
                            double endAng = 90 * Math.Atan(1.0) / 45.0;

                            using (Ellipse acArc = new Ellipse(tp, normal
                                    , majorAxis, radiusRatio, startAng, endAng))
                            {
                                //ターゲット地点が半径以下の場合
                                if (Plen[0] <= dim.Orad)
                                {
                                    acArc.Layer = "0";
                                    acArc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);
                                    //オフセットを作成
                                    DBObjectCollection acDbObjColl = acArc.GetOffsetCurves(dim.Orad);

                                    // Step through the new objects created
                                    foreach (Entity acEnt in acDbObjColl)
                                    {
                                        // Add each offset object
                                        b_table_rec.AppendEntity(acEnt);
                                        fir_trans.AddNewlyCreatedDBObject(acEnt, true);
                                    }

                                }
                                acArc.Layer = "CEN";
                                acArc.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                                b_table_rec.AppendEntity(acArc);

                                fir_trans.AddNewlyCreatedDBObject(acArc, true);
                            }
                            // Outer Ellipse
                            tp = new Point3d(Plen[0], 0, 0);
                            majorAxis = dim.Orad * Vector3d.YAxis;
                            radiusRatio = cut[0] / dim.Orad;
                            startAng = 180 * Math.Atan(1.0) / 45.0;
                            endAng = 0;

                            using (Ellipse acArc = new Ellipse(tp, normal
                                                , majorAxis, radiusRatio, startAng, endAng))
                            {
                                acArc.Layer = "0";
                                acArc.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acArc);
                                startAng = 0;
                                //ターゲット地点が半径より大きいの場合
                                if (Plen[0] > dim.Orad)
                                {
                                    tp = new Point3d(Plen[0], -dim.Length, 0);
                                    endAng = 90 * Math.Atan(1.0) / 45.0;
                                    //アウトライン内側
                                    majorAxis = (dim.Length - dim.Orad) * Vector3d.YAxis;
                                    radiusRatio = (Plen[0] - dim.Orad) / (dim.Length - dim.Orad);
                                    using (Ellipse acArc2 = new Ellipse(tp, normal
                                            , majorAxis, radiusRatio, startAng, endAng))
                                    {
                                        acArc2.Layer = "0";
                                        acArc2.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                        b_table_rec.AppendEntity(acArc2);

                                        fir_trans.AddNewlyCreatedDBObject(acArc2, true);
                                    }
                                    //アウトライン外側
                                    majorAxis = (dim.Length + dim.Orad) * Vector3d.YAxis;
                                    radiusRatio = (Plen[0] + dim.Orad) / (dim.Length + dim.Orad);
                                    using (Ellipse acArc2 = new Ellipse(tp, normal
                                            , majorAxis, radiusRatio, startAng, endAng))
                                    {
                                        acArc2.Layer = "0";
                                        acArc2.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                        b_table_rec.AppendEntity(acArc2);

                                        fir_trans.AddNewlyCreatedDBObject(acArc2, true);
                                    }
                                }
                                else
                                {
                                    target1 = new Point3d(dim.Orad, -dim.Length, 0);
                                    target2 = new Point3d(dim.Orad, 0, 0);
                                    using (Line acLine = new Line(target1,
                                            target2))
                                    {
                                        Point3dCollection intersectPoints = new Point3dCollection();
                                        acLine.IntersectWith(acArc, Intersect.OnBothOperands, intersectPoints,
                                            IntPtr.Zero, IntPtr.Zero);
                                        acLine.EndPoint = intersectPoints[0];
                                        acLine.Layer = "0";
                                        acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);
                                        Vector3d vec = tp.GetVectorTo(intersectPoints[0]);
                                        double m_angle = tp.GetAsVector().GetAngleTo(vec);
                                        acArc.EndAngle = util.DegreeToRadian(90) + m_angle;
                                        b_table_rec.AppendEntity(acLine);
                                    }
                                }
                            }

                            // EndCap
                            target1 = new Point3d(-dim.Orad, -dim.Length, 0);
                            target2 = new Point3d(dim.Orad, -dim.Length, 0);
                            using (Line acLine = new Line(target1,
                                                            target2))
                            {
                                acLine.Layer = "0";
                                acLine.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);

                                b_table_rec.AppendEntity(acLine);
                            }



                            // サイズ　テキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString = dim.Name;
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 4;
                                //acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(acText.Height / 2, -dim.Length + acText.Height / 4, 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }
                            // HORテキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString = "HOR.";
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 6;
                                //acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(acText.Height / 2 - dim.Orad, -dim.Length + acText.Height / 4 + (acText.Height * 1.25), 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }
                            // HORテキスト
                            using (DBText acText = new DBText())
                            {
                                CreateDefpoints();
                                acText.TextString = Math.Abs(sl_angle).ToString();
                                //acText.HorizontalMode = TextHorizontalMode.TextCenter;
                                //acText.VerticalMode = TextVerticalMode.TextBottom;
                                acText.Height = dim.Orad / 6;
                                //acText.Rotation = util.DegreeToRadian(90);
                                acText.Position = new Point3d(acText.Height / 2 - dim.Orad, -dim.Length + acText.Height / 4, 0);
                                acText.Layer = "Defpoints";
                                acText.ColorIndex = 5;


                                b_table_rec.AppendEntity(acText);
                                fir_trans.AddNewlyCreatedDBObject(acText, true);
                            }

                        }
                        //トランザクション終了
                        fir_trans.Commit();
                    }
                }
            }


            //平面--------------------------------------------------------------------------------
        }


        public void WriteBlocks(Parts DBData, Point3d pt,string layer,int rep = -1)
        {
            //作業ウィンドウをアクティブ
            acApplication.MainWindow.Focus();
            InitCommand();
            CParts = DBData;
            string blkName = DBData.BName;

            //各種情報を格納
            Insert_blk = blkName;
            Cur_layer = layer;

            if (rep == Blk_suffix.Length) rep = 0;
            if (rep>=0)
            {
                string blk = blkName + Blk_suffix[rep];
                if (Cur_Slope != 0 & rep != 0)
                {
                    blk += Cur_Slope.ToString();
                }
                    
                Command.InsertingABlock(blk, pt);
                if (Mirror_FLG)
                {
                    ScaleRefBlock(Cur_ObjID, -1);
                }
                Command c = new Command();
                c.ChangeRotate(false,rep);
            }
            else
            {
                //ドキュメント、データベースオブジェクトの作成
                Document now_doc = acApplication.DocumentManager.MdiActiveDocument;
                Database db = now_doc.Database;
                //ミラーフラグを初期化
                Mirror_FLG = false;
                Cur_Slope = 0;
                //現在の画層を変更
                //ドキュメントのロックを解除
                using (DocumentLock docLock = now_doc.LockDocument())
                {
                    using (Transaction acTrans = db.TransactionManager.StartTransaction())
                    {
                        LayerTable acLyrTbl;
                        acLyrTbl = acTrans.GetObject(db.LayerTableId,
                                                        OpenMode.ForRead) as LayerTable;
                        db.Clayer = acLyrTbl[layer];
                        acTrans.Commit();
                    }
                    
                }
                now_doc.SendStringToExecute("InsertBlocks ", true, false, false);
            }

        }
        [CommandMethod("InsertBlocks")]
        async public  void InsertBlocks()
        {
            //作業ウィンドウをアクティブ
            acApplication.MainWindow.Focus();

            var doc = acApplication.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            //await ed.CommandAsync("_.INSERT", "SUS_100A_90_LONG_H", Editor.PauseToken, 1, 1, 0);


            try {
                CursorMonitor();
                await ed.CommandAsync("_.INSERT", Insert_blk+Blk_suffix[0], "S",1,"R",0, Editor.PauseToken);
                RemoveCursorMonitor();

                //ラスト選択セット
                PromptSelectionResult acSSPrompt = ed.SelectLast();
                SelectionSet acSSet = null;
                Autodesk.AutoCAD.DatabaseServices.ObjectId[] acObjIds = null;
                acSSet = acSSPrompt.Value;
                acObjIds = acSSet.GetObjectIds();
                Cur_ObjID = acObjIds[0];


                Command c = new Command();
                c.ChangeRotate(true);
                //await ed.CommandAsync("_.MOVE", "L", "", "",0);
                //await ed.CommandAsync("_.MOVE", "L", "","0,0", Editor.PauseToken);
            }
            catch
            {
                //await ed.CommandAsync("_.ERASE", "L", "");
                RemoveCursorMonitor();
                //doc.CommandEnded -= new CommandEventHandler(EndRotateHandler);
                EndCommand();
                return;
            }

            //await ed.CommandAsync(1, 1, Editor.PauseToken);
            //ed.WriteMessage("\nWe have inserted our block.");
        }

        public void ChangeLayer(acObjectId objID,string layer)
        {
            Document now_doc = acApplication.DocumentManager.MdiActiveDocument;
            Database db = now_doc.Database;
            //ドキュメントのロックを解除
            using (DocumentLock docLock = now_doc.LockDocument())
            {
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    Entity blk = (Entity)acTrans.GetObject(Cur_ObjID, OpenMode.ForWrite);
                    blk.Layer = layer;
                    acTrans.Commit();
                }
            }
        }
        public void EraseObject(acObjectId objID)
        {
            Document doc = acApplication.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            //ドキュメントのロックを解除
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    Entity obj = (Entity)acTrans.GetObject(objID, OpenMode.ForWrite);

                    obj.Erase();
                    acTrans.Commit();
                }
            }
        }
        public void RotateRefBlock(acObjectId objID,double angle)
        {
            Document doc = acApplication.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            //ドキュメントのロックを解除
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    BlockReference blk = (BlockReference)acTrans.GetObject(Cur_ObjID, OpenMode.ForWrite);
                    blk.Rotation = util.DegreeToRadian(angle);
                    acTrans.Commit();
                }
            }
        }
        public void ScaleRefBlock(acObjectId objID, double x=1, double y = 1, double z = 1)
        {
            Scale3d cs = new Scale3d(x, y, z);
            Document doc = acApplication.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            //ドキュメントのロックを解除
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    BlockReference blk = (BlockReference)acTrans.GetObject(Cur_ObjID, OpenMode.ForWrite);
                    blk.ScaleFactors = cs;
                    acTrans.Commit();
                }
            }
        }
        public double Point2Angle(Point3d src, Point3d target)
        {
            Point2d s = new Point2d(src[0], src[1]);
            Point2d t = new Point2d(target[0], target[1]);
            return util.RadianToDegree(s.GetVectorTo(t).Angle); 
        }
        private void ChangeRotate(bool eve = false,int rep = 0)
        {
            Document doc = acApplication.DocumentManager.MdiActiveDocument;
            Editor ed = acApplication.DocumentManager.MdiActiveDocument.Editor;
            Database db = doc.Database;

            //ユーザー入力の結果を代入するポイントの作成
            PromptPointResult pt_res;
            PromptPointOptions pt_option = new PromptPointOptions("");
            //Point3d lastpoint = (Point3d)acApplication.GetSystemVariable("LASTPOINT");
            Point3d lastpoint = Last_Point;

            //if (eve) doc.CommandEnded -= new CommandEventHandler(EndRotateHandler);


            RotateChangeSet();

            //ユーザーへの入力要求
            pt_option.Message = "\n角度を指定 または [右クリック:向き変更][S:傾斜][M:ミラー]: ";
            pt_option.UseBasePoint = true;
            pt_option.AllowNone = true;
            pt_option.BasePoint = lastpoint;
            pt_option.AllowArbitraryInput =　true;
            pt_res = doc.Editor.GetPoint(pt_option);
            Point3d ptStart = pt_res.Value;
            EndCommand();
            //キーワード入力処理
            if (pt_res.Status == PromptStatus.Keyword) {
                double i_angle;
                if (pt_res.StringResult.ToLower() == "s")
                {
                    PromptStringOptions pStrOpts = new PromptStringOptions("\n角度を入力してください[0<XXX<90][(X):点指定]:\n");
                    //pt_option.Message = "\n点または角度を入力してください[0<XXX<90]: ";
                    pStrOpts.AllowSpaces = false;
                    pStrOpts.UseDefaultValue = true;
                    pStrOpts.DefaultValue = Prev_Slope.ToString();
                    PromptResult pStrRes = doc.Editor.GetString(pStrOpts);
                    //pt_res = doc.Editor.GetPoint(pt_option);
                    ed.PointMonitor -= new PointMonitorEventHandler(RotateChangeHandler);
                    if (pStrRes.StringResult.ToLower() == "x")
                    {
                        pt_option.Message = "\n平面の点を指定してください: ";
                        pt_option.BasePoint = lastpoint;
                        pt_res = doc.Editor.GetPoint(pt_option);
                        double dist;
                        if (double.TryParse(pt_res.StringResult, out i_angle))
                        {
                            dist = i_angle;
                        }
                        else if (pt_res.Status != PromptStatus.OK)
                        {
                            ed.PointMonitor -= new PointMonitorEventHandler(RotateChangeHandler);
                            WriteBlocks(CParts, lastpoint, Cur_layer, rep);
                            return;
                        }
                        dist = lastpoint.DistanceTo(pt_res.Value);
                        pt_option.Message = "\n1点目または高さを入力してください: ";
                        pt_option.UseBasePoint = false;
                        pt_res = doc.Editor.GetPoint(pt_option);
                        double height;
                        if (pt_res.Status == PromptStatus.Keyword)
                        {
                            if (double.TryParse(pt_res.StringResult, out i_angle))
                        {
                            height = i_angle;
                            Cur_Slope = double.Parse(string.Format("{0:F2}", Point2Angle(Point3d.Origin, new Point3d(dist, height, 0))));
                                Cur_Slope = Math.Abs(Cur_Slope);
                                Prev_Slope = Math.Abs(Cur_Slope);
                            }
                        }

                        else if (pt_res.Status == PromptStatus.OK)
                        {
                            pt_option.Message = "\n2点目を入力してください: ";
                            pt_option.UseBasePoint = true;
                            pt_option.BasePoint = pt_res.Value;
                            pt_option.AllowArbitraryInput = false;
                            pt_res = doc.Editor.GetPoint(pt_option);
                            if (pt_res.Status == PromptStatus.OK)
                            {
                                height = pt_option.BasePoint.DistanceTo(pt_res.Value);
                                Cur_Slope = double.Parse(string.Format("{0:F2}", Point2Angle(Point3d.Origin, new Point3d(dist, height, 0))));
                                Cur_Slope = Math.Abs(Cur_Slope);
                                Prev_Slope = Math.Abs(Cur_Slope);
                            }
                            else
                            {
                                ed.PointMonitor -= new PointMonitorEventHandler(RotateChangeHandler);
                                WriteBlocks(CParts, lastpoint, Cur_layer, rep);
                                return;
                            }
                        }
                        else
                        {
                            ed.PointMonitor -= new PointMonitorEventHandler(RotateChangeHandler);
                            EraseObject(Cur_ObjID);
                            WriteBlocks(CParts, lastpoint, Cur_layer, rep);

                            return;
                        }
                    }//数値を入力した場合
                    else if(double.TryParse(pStrRes.StringResult, out i_angle)) 
                    {
                        Cur_Slope = Math.Abs(i_angle);
                        Prev_Slope = Math.Abs(i_angle);
                    }
                    else if (pt_res.Status == PromptStatus.Cancel)
                    {

                        ed.PointMonitor -= new PointMonitorEventHandler(RotateChangeHandler);
                        EraseObject(Cur_ObjID);
                        WriteBlocks(CParts, lastpoint, Cur_layer, rep);
                        return;
                    }

                    ed.PointMonitor -= new PointMonitorEventHandler(RotateChangeHandler);
                    EraseObject(Cur_ObjID);
                    Generate90Elbow(CParts.BName, Cur_Slope);
                    WriteBlocks(CParts, lastpoint, Cur_layer, rep+1);
                    return;
                }else if (pt_res.StringResult.ToLower() == "m")
                {
                    //ミラーフラグを切り替え
                    Mirror_FLG = !Mirror_FLG;

                    EraseObject(Cur_ObjID);
                    WriteBlocks(CParts, lastpoint, Cur_layer, rep);
                    ed.PointMonitor -= new PointMonitorEventHandler(RotateChangeHandler);
                    return;
                }//数値を入力した場合
                else if(double.TryParse(pt_res.StringResult, out i_angle))
                {
                    ed.PointMonitor -= new PointMonitorEventHandler(RotateChangeHandler);
                    RotateRefBlock(Cur_ObjID, i_angle);
                }
                else
                {
                    ed.PointMonitor -= new PointMonitorEventHandler(RotateChangeHandler);
                    EraseObject(Cur_ObjID);
                    return;
                }
            }
                //ESCキーなどでキャンセルしたときの処理
            if (pt_res.Status == PromptStatus.Cancel)
            {
                
                ed.PointMonitor -= new PointMonitorEventHandler(RotateChangeHandler);
                EraseObject(Cur_ObjID);
                return;
            }
            //右クリックでループ
            if (pt_res.Status == PromptStatus.None)
            {
                ed.PointMonitor -= new PointMonitorEventHandler(RotateChangeHandler);
                //string blk_name = Insert_blk+ Blk_suffix[rep];

                EraseObject(Cur_ObjID);
                WriteBlocks(CParts, lastpoint, Cur_layer,rep + 1);
                return;
                //continue;
            }
            //左クリックの場合はループ
            ed.PointMonitor -= new PointMonitorEventHandler(RotateChangeHandler);
            WriteBlocks(CParts, lastpoint, Cur_layer, -1);
        }
        public static void RotateChangeSet()
        {
            var doc = acApplication.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            Editor ed = acApplication.DocumentManager.MdiActiveDocument.Editor;
            ed.PointMonitor += new PointMonitorEventHandler(RotateChangeHandler);
        }
        public static void RotateChangeHandler(object sender, PointMonitorEventArgs e)
        {
            Editor ed = acApplication.DocumentManager.MdiActiveDocument.Editor;

            if (e.Context.History == PointHistoryBits.LastPoint)
                return;

            var ucs = ed.CurrentUserCoordinateSystem.Inverse();
            var snapped = (e.Context.History & PointHistoryBits.ObjectSnapped) > 0;
            var pt = (snapped ? e.Context.ObjectSnappedPoint : e.Context.ComputedPoint).TransformBy(ucs);

            try
            {
                RotateChange(pt);
                //ed.WriteMessage("{0}: {1:F4}\n", snapped ? "Snapped" : "Found", pt);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                if (ex.ErrorStatus != ErrorStatus.NotApplicable)
                    throw;
            }
        }
        public static void RotateChange(Point3d target)
        {
            Document now_doc = acApplication.DocumentManager.MdiActiveDocument;
            Database db = now_doc.Database;

            Point3d lastpoint = (Point3d)acApplication.GetSystemVariable("LASTPOINT");
            Point2d lp = new Point2d(lastpoint[0], lastpoint[1]);
            Point2d tp = new Point2d(target[0], target[1]);
            //ドキュメントのロックを解除
            using (DocumentLock docLock = now_doc.LockDocument())
            {
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    BlockReference blk = (BlockReference)acTrans.GetObject(Cur_ObjID, OpenMode.ForWrite);
                    blk.Rotation = lp.GetVectorTo(tp).Angle;
                    acTrans.Commit();
                }
            }

        }


        async public static  void lastMove()
        {
            //作業ウィンドウをアクティブ
            acApplication.MainWindow.Focus();
            var doc = acApplication.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                await ed.CommandAsync("_.MOVE", "L","","0,0" ,Editor.PauseToken);
            }
            catch
            {
                return;
            }
        }
        public static void wait_point()
        {
            //Command cmd = new Command();
            //cmd.InsertElbow(material, name, angle, type);
            //作業ウィンドウをアクティブ
            acApplication.MainWindow.Focus();

            //ドキュメント、データベースオブジェクトの作成
            Document now_doc = acApplication.DocumentManager.MdiActiveDocument;
            Database db = now_doc.Database;

            //ユーザー入力の結果を代入するポイントの作成
            PromptPointResult pt_res;
            PromptPointOptions pt_option = new PromptPointOptions("");

            //ユーザーへの入力要求
            pt_option.Message = "\n原点を指定: ";
            pt_option.AllowNone = true;
            pt_res = now_doc.Editor.GetPoint(pt_option);
            Point3d ptStart = pt_res.Value;
        }
        public static void InsertingABlock(string blkName,Point3d blkPoint)
        {
            // Get the current database and start a transaction
            Database acCurDb;
            acCurDb = acApplication.DocumentManager.MdiActiveDocument.Database;
            Document doc = acApplication.DocumentManager.MdiActiveDocument;
            //ドキュメントのロックを解除
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction acTrans = acCurDb.TransactionManager.StartTransaction())
                {
                    // Open the Block table for read
                    BlockTable acBlkTbl;
                    acBlkTbl = acTrans.GetObject(acCurDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                    acObjectId blkRecId = acObjectId.Null;

                    if (!acBlkTbl.Has(blkName))
                    {
                        return ;
                        /*
                        using (BlockTableRecord acBlkTblRec = new BlockTableRecord())
                        {
                            acBlkTblRec.Name = "SUS_100A_90_LONG_H";

                            // Set the insertion point for the block
                            acBlkTblRec.Origin = new Point3d(0, 0, 0);

                            // Add a circle to the block
                            using (Circle acCirc = new Circle())
                            {
                                acCirc.Center = new Point3d(0, 0, 0);
                                acCirc.Radius = 2;

                                acBlkTblRec.AppendEntity(acCirc);

                                acTrans.GetObject(acCurDb.BlockTableId, OpenMode.ForWrite);
                                acBlkTbl.Add(acBlkTblRec);
                                acTrans.AddNewlyCreatedDBObject(acBlkTblRec, true);
                            }

                            blkRecId = acBlkTblRec.Id;
                        }
                        */
                    }
                    else
                    {
                        blkRecId = acBlkTbl[blkName];
                    }
                    acObjectId resId = acObjectId.Null;
                    // Insert the block into the current space
                    if (blkRecId != acObjectId.Null)
                    {
                        using (BlockReference acBlkRef = new BlockReference(blkPoint, blkRecId))
                        {
                            BlockTableRecord acCurSpaceBlkTblRec;
                            acCurSpaceBlkTblRec = acTrans.GetObject(acCurDb.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;

                            acCurSpaceBlkTblRec.AppendEntity(acBlkRef);
                            acTrans.AddNewlyCreatedDBObject(acBlkRef, true);
                            resId = acBlkRef.Id;
                        }
                    }

                    // Save the new object to the database
                    acTrans.Commit();
                    Cur_ObjID = resId;
                    
                    // Dispose of the transaction
                }
            }
        }
      
        
        public static void SetMoveObj()
        {
            var doc = acApplication.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            Editor ed = acApplication.DocumentManager.MdiActiveDocument.Editor;
            ed.PointMonitor += new PointMonitorEventHandler(Block_PointHandler);
        }
        [CommandMethod("RemoveMoveObj")]
        public static void RemoveMoveObj()
        {
            var doc = acApplication.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            Editor ed = acApplication.DocumentManager.MdiActiveDocument.Editor;
            ed.PointMonitor -= Block_PointHandler;
        }
        public static void Block_PointHandler(object sender, PointMonitorEventArgs e)
        {
            Editor ed = acApplication.DocumentManager.MdiActiveDocument.Editor;

            if (e.Context.History == PointHistoryBits.LastPoint)
                return;

            var ucs = ed.CurrentUserCoordinateSystem.Inverse();
            var snapped = (e.Context.History & PointHistoryBits.ObjectSnapped) > 0;
            var pt = (snapped ? e.Context.ObjectSnappedPoint : e.Context.ComputedPoint).TransformBy(ucs);

            try
            {
                moveObj(pt);
                //ed.WriteMessage("{0}: {1:F4}\n", snapped ? "Snapped" : "Found", pt);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                if (ex.ErrorStatus != ErrorStatus.NotApplicable)
                    throw;
            }
        }
        public static void moveObj(Point3d target)
        {
            Document now_doc = acApplication.DocumentManager.MdiActiveDocument;
            Database db = now_doc.Database;
            //ドキュメントのロックを解除
            using (DocumentLock docLock = now_doc.LockDocument())
            {
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    BlockReference blk = (BlockReference)acTrans.GetObject(Cur_ObjID, OpenMode.ForWrite);
                    blk.Position = target;
                    acTrans.Commit();
                }
            }

        }
        [CommandMethod("CurMon")]
        public void CursorMonitor()
        {
            var doc = acApplication.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            Editor ed = acApplication.DocumentManager.MdiActiveDocument.Editor;
            ed.PointMonitor += new PointMonitorEventHandler(PointHandler);
        }

        [CommandMethod("RemCurMon")]
        public void RemoveCursorMonitor()
        {
            var doc = acApplication.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            Editor ed = acApplication.DocumentManager.MdiActiveDocument.Editor;
            ed.PointMonitor -= PointHandler;
            ed.PointMonitor -= new PointMonitorEventHandler(RotateChangeHandler);
        }

        public void PointHandler(object sender, PointMonitorEventArgs e)
        {
            Editor ed = acApplication.DocumentManager.MdiActiveDocument.Editor;
            if (e.Context.History == PointHistoryBits.LastPoint)
                return;

            var ucs = ed.CurrentUserCoordinateSystem.Inverse();
            var snapped = (e.Context.History & PointHistoryBits.ObjectSnapped) > 0;
            var pt = (snapped ? e.Context.ObjectSnappedPoint : e.Context.ComputedPoint).TransformBy(ucs);

            try
            {
                Last_Point = pt;
                //ed.WriteMessage("{0}: {1:F4}\n", snapped ? "Snapped" : "Found", pt);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                if (ex.ErrorStatus != ErrorStatus.NotApplicable)
                    throw;
            }
        }
        public static void EndRotateSet()
        {
            Document doc = acApplication.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            Editor ed = acApplication.DocumentManager.MdiActiveDocument.Editor;
            doc.CommandEnded += new CommandEventHandler(EndRotateHandler);
        }
        public static void EndRotateHandler(object sender, CommandEventArgs e)
        {
            Command c = new Command();
            c.ChangeRotate(true);
        }

        /*メモ-----------------------------------------------------------------------------------------
        static int RTSTR = 5005;
        
        [DllImport("accore.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, EntryPoint = "acedCmd")]
        private static extern int acedCmd(System.IntPtr vlist);

        [CommandMethod("MyCommand")]
        public void MyCommand()
        {
            Database oDb = HostacApplicationServices.WorkingDatabase;
            using (Transaction oTr = oDb.TransactionManager.StartTransaction())
            {
                try
                {

                    using (ResultBuffer rb = new ResultBuffer())
                    {
                        rb.Add(new TypedValue(RTSTR, "UNDO"));
                        rb.Add(new TypedValue(RTSTR, "BEGIN"));
                        rb.Add(new TypedValue(RTSTR, "_"));
                        acedCmd(rb.UnmanagedObject);
                    }
                    Line oLine1 = new Line(new Point3d(0.0, 0.0, 0.0), new Point3d(50.0, 100.0, 0.0));
                    Line oLine2 = new Line(new Point3d(50.0, 0.0, 0.0), new Point3d(100.0, 100.0, 0.0));
                    Line oLine3 = new Line(new Point3d(100.0, 0.0, 0.0), new Point3d(150.0, 100.0, 0.0));
                    BlockTable oBt = (BlockTable)oTr.GetObject(oDb.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord oBtr = (BlockTableRecord)oTr.GetObject(oBt["*MODEL_SPACE"], OpenMode.ForWrite);
                    oBtr.AppendEntity(oLine1);
                    oBtr.AppendEntity(oLine2);
                    oBtr.AppendEntity(oLine3);
                    oTr.AddNewlyCreatedDBObject(oLine1, true);
                    oTr.AddNewlyCreatedDBObject(oLine2, true);
                    oTr.AddNewlyCreatedDBObject(oLine3, true);
                    oTr.Commit();
                    using (ResultBuffer rb = new ResultBuffer())
                    {
                        rb.Add(new TypedValue(RTSTR, "UNDO"));
                        rb.Add(new TypedValue(RTSTR, "END"));
                        rb.Add(new TypedValue(RTSTR, ""));
                        acedCmd(rb.UnmanagedObject);
                    }
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.Message);
                }
                finally
                {
                    oTr.Dispose();
                }
            }
        }
        [CommandMethod("IBS")]

        public void InsertBlockSync()

        {

            var doc =

                acApplication.DocumentManager.MdiActiveDocument;

            var ed = doc.Editor;



            // Our insertion point is hardcoded at 10,10



            ed.Command("_.INSERT", "TEST", "10,10", 1, 1, 0);



            ed.WriteMessage("\nWe have inserted our block.");

        }



        [CommandMethod("IBA")]

        async public void InsertBlockAsync()

        {

            var doc =

                acApplication.DocumentManager.MdiActiveDocument;

            var ed = doc.Editor;



            // Let's ask the user to select the insertion point



            await ed.CommandAsync(

                "_.INSERT", "SUS_100A_90_LONG_H", Editor.PauseToken, 1, 1, 0

            );
            ed.WriteMessage("\nWe have inserted our block.");

        }
        
        static int RTSTR = 5005;

        [DllImport("accore.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, EntryPoint = "acedCmd")]
        private static extern int acedCmd(System.IntPtr vlist);
        [CommandMethod("Test7")]

        public void Test7()

        {

            ResultBuffer rb = new ResultBuffer();

            // RTSTR = 5005

            rb.Add(new TypedValue(5005, "_.INSERT"));

            // start the insert command

            acedCmd(rb.UnmanagedObject);



            bool quit = false;

            // loop round while the insert command is active

            while (!quit)

            {

                // see what commands are active

                string cmdNames = (string)Autodesk.AutoCAD.acApplicationServices.acApplication.GetSystemVariable("CMDNAMES");

                // if the INSERT command is active

                if (cmdNames.ToUpper().IndexOf("INSERT") >= 0)

                {

                    // then send a PAUSE to the command line

                    rb = new ResultBuffer();

                    // RTSTR = 5005 - send a user pause to the command line

                    rb.Add(new TypedValue(5005, "\\"));

                    acedCmd(rb.UnmanagedObject);

                }

                else

                    // otherwise quit

                    quit = true;

            }

        }
        */
    }
}