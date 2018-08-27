using System.Linq;
using Rhino.Geometry;
using System.Drawing;
using Rhino.Display;
using System.Collections.Generic;

namespace PolyFramework

{
    /// <summary>
    /// Pass an array of lines here ... no fancy shit 
    /// </summary>
    public class DrawPFLineConduit : DisplayConduit
    {
        List<Line> pfLines = new List<Line>();
        
        System.Drawing.Color material = new System.Drawing.Color();
        BoundingBox bbox;
        //private static Random rand = new Random();
        Line [] lineArray;
        
        
        
        public DrawPFLineConduit(IList<Line> edges, Color color)
        {
            pfLines = new List<Line>(edges) ?? throw new System.ArgumentNullException(nameof(edges));

            List<Point3d> allEnds = new List<Point3d>();
            for (int i = 0; i < pfLines.Count; i++)
            {
                allEnds.Add(pfLines[i].From);
                allEnds.Add(pfLines[i].To);
            }
            bbox = new BoundingBox(allEnds);
            material = color;
            lineArray = pfLines.ToArray();
        }

        public void UpdateLines(IList<Line> newLines)
        {
            List<Point3d> allEnds = new List<Point3d>();
            for (int i = 0; i < newLines.Count; i++)
            {
                allEnds.Add(newLines[i].From);
                allEnds.Add(newLines[i].To);
            }
            bbox = new BoundingBox(allEnds);
            
            lineArray = newLines.ToArray();
            
        }

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            base.CalculateBoundingBox(e);
            e.IncludeBoundingBox(bbox);
        }

        

        protected override void PreDrawObjects(DrawEventArgs e)
        {
            // this could be slow with linq
            base.PreDrawObjects(e);
            e.Display.DrawLines(lineArray, material, 1);

        }

    }


    // <summary>
    /// This one draws lines with multiple colors using display conduit
    /// </summary>
    public class DrawPFLinesConduit : DisplayConduit
    {


        Color[] lineColors;
        BoundingBox bbox;
        //private static Random rand = new Random();
        Line[] lineArray;



        public DrawPFLinesConduit(IList<Line> edges, IList<Color> colors)
        {
            lineArray = edges.ToArray() ?? throw new System.ArgumentNullException(nameof(edges));
            lineColors = colors.ToArray() ?? throw new System.ArgumentNullException(nameof(colors));
            HashSet<Point3d> allEnds = new HashSet<Point3d>();

            for (int i = 0; i < lineArray.Length; i++)
            {
                allEnds.Add(lineArray[i].From);
                allEnds.Add(lineArray[i].To);
            }
            bbox = new BoundingBox(allEnds);
            
            
        }

        public void UpdateLines(IList<Line> newLines)
        {
            List<Point3d> allEnds = new List<Point3d>();
            for (int i = 0; i < newLines.Count; i++)
            {
                allEnds.Add(newLines[i].From);
                allEnds.Add(newLines[i].To);
            }
            bbox = new BoundingBox(allEnds);
            lineArray = newLines.ToArray();
        }

        public void UpdateColors(IList<Color> newColors)
        {
            lineColors = newColors.ToArray();
        }

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            base.CalculateBoundingBox(e);
            e.IncludeBoundingBox(bbox);
        }

        protected override void PreDrawObjects(DrawEventArgs e)
        {
            // this could be slow with linq
            base.PreDrawObjects(e);
            for(int i=0; i<lineArray.Length; i++)
            {
                e.Display.DrawLine(lineArray[i], lineColors[i], 1);
            }
            

        }

    }
}
