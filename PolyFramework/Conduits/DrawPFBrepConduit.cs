using System.Linq;
using Rhino.Geometry;
using System.Drawing;
using System;
using Rhino.Display;
using System.Collections.Generic;

namespace PolyFramework

{
    class DrawPFBrepConduit : DisplayConduit
    {
        readonly IList<Brep> pfBrep = new List<Brep>();

        readonly IList<Rhino.Display.DisplayMaterial> material = new List<Rhino.Display.DisplayMaterial>();
        readonly BoundingBox bbox;
        private static Random rand = new Random();

        
        public DrawPFBrepConduit(IList<Brep> breps, Color color, double transparency)
        {
            pfBrep = new List<Brep>(breps) ?? throw new System.ArgumentNullException(nameof(breps));


            var allBrep = new Brep();
            for (int i = 0; i < pfBrep.Count; i++)
            {
                DisplayMaterial mat = new DisplayMaterial(color, 0.8);
                material.Add(mat);
                allBrep.Append(pfBrep[i]);
            }
            bbox = allBrep.GetBoundingBox(false);
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
            foreach (var brep_mat in pfBrep.Zip(material, (brp, mat) => new { brp, mat }))
                e.Display.DrawBrepWires(brep_mat.brp, System.Drawing.Color.Red, 1);

        }

    }
}
