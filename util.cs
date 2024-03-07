using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.Geometry;

namespace PipingSystem
{
    public class util
    {
        public static double DegreeToRadian(double angle)
        {
            return Math.PI * angle / 180.0;
        }

        public static double RadianToDegree(double angle)
        {
            return angle * (180.0 / Math.PI);
        }
        public static Point3d PolarPoint(double r, double zeta,double x=0,double y=0)
        {
            x += r * Math.Cos(DegreeToRadian(zeta));
            y += r * Math.Sin(DegreeToRadian(zeta));
            return new Point3d(x, y, 0);
        }
    }
}
