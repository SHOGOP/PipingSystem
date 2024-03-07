using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PipingSystem
{
    //[JsonObject("PipeJson")]
    public class Parts
    {
        public string BName { get; set; }
        public string Material { get; set; }

        public string Name { get; set; }
        public string Sch { get; set; }
    }
    public class Pipe: Parts
    {

        public double Odia { get; set; }
        public double Orad { get; set; }

        public double Idia { get; set; }
        public double Irad { get; set; }

    }
    public class Elbow : Parts
    {

        public string Angle { get; set; }

        public string Type { get; set; }

        public string Pattern { get; set; }
        public double Odia { get; set; }
        public double Orad { get; set; }

        public double Idia { get; set; }
        public double Irad { get; set; }
        public double Length { get; set; }

        public double R { get; set; }

    }
    public class Layer
    {
        public string Name { get; set; }
        public int Color { get; set; }
        public string LineType { get; set; }
    }
}