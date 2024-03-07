
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;



namespace JsonFileIO
{
    internal class JsonAccess
    {
        internal void Main(string[] args)
        {
            // オプション設定
            var options = new JsonSerializerOptions
            {
                // 日本語を変換するためのエンコード設定
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),

                // （.NET 8以降）プロパティ名をスネークケースに変換
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,

                // （.NET 7以前）プロパティ名をスネークケースに変換するための自作ポリシーを適用
                //PropertyNamingPolicy = new SnakeCaseNamingPolicy(),

                // インデントを付ける
                WriteIndented = true
            };

            /*
            var person = new PipeJson;
            {
                FullName = "田中太郎",
                Age = 20,
                FavoriteThings = "読書",
                Memo = "こんにちは"
            };

            // シリアライズ
            var jsonString = JsonSerializer.Serialize(person, options);
            Console.WriteLine(jsonString);
            */
            using (var sr = new StreamReader(@"C:\Users\windows7\source\repos\PipingSystem\Resources\PIPE_DIM.json", System.Text.Encoding.UTF8))
            {
                // 変数 jsonReadData にファイルの内容を代入 
                var jsonReadData = sr.ReadToEnd();
                // デシリアライズ
                //var person2 = JsonSerializer.Deserialize<PipeJson>(jsonReadData, options);
                //Console.WriteLine($"{person2?.Material} {person2?.Name} {person2?.Sch}");
                //Console.WriteLine($"{person2?.Material} {person2?.Name} {person2?.Sch}");
            }

        }

    }
}