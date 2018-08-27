using Microsoft.Win32;
using Rhino.Display;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.UI;
using RhinoWindows;

namespace PolyFramework
{
    class DrawPFVertexConduit : Rhino.Display.DisplayConduit
    {

        private readonly IEnumerable<PFVertex> m_conduit_vertices;
        System.Drawing.Color clrNP = System.Drawing.Color.DarkBlue;
        System.Drawing.Color clrP = System.Drawing.Color.DarkRed;
        System.Drawing.Color clrSet = System.Drawing.Color.AliceBlue;
        int scaleSmall = RhinoWindows.Forms.Dpi.ScaleInt(8);
        int scaleBig = RhinoWindows.Forms.Dpi.ScaleInt(12);
        int scaleMove = RhinoWindows.Forms.Dpi.ScaleInt(5);
        //float scale = 96 / (float)(int)Registry.GetValue("HKEY_CURRENT_USER\\Control Panel\\Desktop", "LogPixels", 96);

        public DrawPFVertexConduit(IEnumerable<PFVertex> vertices)
        {
            m_conduit_vertices = vertices;
        }

        protected override void DrawForeground(Rhino.Display.DrawEventArgs e)
        {
            if (m_conduit_vertices != null)
            {
                foreach (var vrt in m_conduit_vertices)
                {
                    var pnt2d = e.Viewport.WorldToClient(vrt.Point);
                    pnt2d.X += scaleMove;
                    pnt2d.Y -= scaleMove;

                    if (vrt.Picked)
                    {
                        e.Display.DrawPoint(vrt.Point, PointStyle.Simple, 3, clrP);
                        e.Display.Draw2dText("V_" + vrt.Id.ToString(), clrP, pnt2d, false, scaleBig);
                    }
                    else
                    {
                        if (vrt.RestrictSupport != null) e.Display.DrawPoint(vrt.Point, PointStyle.Simple, 4, clrSet);
                        else e.Display.DrawPoint(vrt.Point, PointStyle.Simple, 3, clrNP);

                        e.Display.Draw2dText("V_" + vrt.Id.ToString(), clrNP, pnt2d, false, scaleSmall);
                    }


                }
            }
        }
    }

    class DrawPFEdgeConduit : Rhino.Display.DisplayConduit
    {

        private List<Line> m_conduit_edgeLines;
        private readonly List<PFEdge> m_conduit_edges;
        private readonly List<Point3d> m_conduit_midPoints;
        System.Drawing.Color clrNP = System.Drawing.Color.DarkBlue;
        System.Drawing.Color clrP = System.Drawing.Color.DarkRed;
        System.Drawing.Color clrSet = System.Drawing.Color.AliceBlue;
        int scaleSmall = RhinoWindows.Forms.Dpi.ScaleInt(8);
        int scaleBig = RhinoWindows.Forms.Dpi.ScaleInt(12);
        int scaleMove = RhinoWindows.Forms.Dpi.ScaleInt(5);
        //float scale = 96 / (float)(int)Registry.GetValue("HKEY_CURRENT_USER\\Control Panel\\Desktop", "LogPixels", 96);

        public DrawPFEdgeConduit(List<PFEdge> edges)
        {
            m_conduit_edges = edges;
            m_conduit_edgeLines = edges.Select(x => x.CreateLine()).ToList();
            m_conduit_midPoints = edges.Select(x => PFVertex.AverageVertexes(x.Vertices)).ToList();
        }

        public void UpdateLines()
        {
            m_conduit_edgeLines = m_conduit_edges.Where(e => e.Picked).Select(x => x.CreateLine()).ToList();
        }

        protected override void DrawForeground(Rhino.Display.DrawEventArgs e)
        {
            if (m_conduit_edges != null)
            {

                for (int i = 0; i < m_conduit_edges.Count(); i++)
                {
                    var pnt2d = e.Viewport.WorldToClient(m_conduit_midPoints[i]);
                    pnt2d.X += scaleMove;
                    pnt2d.Y -= scaleMove;
                    
                    //e.Display.DrawLine(m_conduit_edgeLines[l], m_conduit_edges[l].Color, 2);
                    if (!m_conduit_edges[i].Picked)
                    {
                        e.Display.Draw2dText("E_" + m_conduit_edges[i].Id.ToString(), clrNP, pnt2d, false, scaleSmall);

                        if (double.IsNaN(m_conduit_edges[i].TargetLength))
                        {
                           
                            e.Display.DrawPoint(m_conduit_midPoints[i], PointStyle.Simple, 3, clrNP);
                        }
                        else
                        {
                            e.Display.DrawPoint(m_conduit_midPoints[i], PointStyle.Simple, 4, clrSet);
                        }

                        
                    }
                    else
                    {
                        string targetL = "";
                        if (double.IsNaN(m_conduit_edges[i].TargetLength)) targetL = "not set";
                        else targetL = Math.Round(m_conduit_edges[i].TargetLength, 3).ToString();

                        e.Display.DrawPoint(m_conduit_midPoints[i], PointStyle.Simple, 3, clrP);
                        e.Display.Draw2dText($"length = {Math.Round(m_conduit_edgeLines[i].Length , 3).ToString()}" , clrP, pnt2d, false, scaleBig);
                        e.Display.DrawLine(m_conduit_edgeLines[i], clrP, 2);
                    }
                }
            }
        }
    }

    class DrawPFFaceConduit : Rhino.Display.DisplayConduit
    {

        private readonly List<PFFace> m_conduit_faces;
        private BoundingBox bbox = BoundingBox.Empty;
        DisplayMaterial materialNP = new DisplayMaterial(System.Drawing.Color.DimGray, .9);
        DisplayMaterial materialP = new DisplayMaterial(System.Drawing.Color.DarkRed, .5);
        System.Drawing.Color clrNP = System.Drawing.Color.DarkBlue;
        System.Drawing.Color clrP = System.Drawing.Color.DarkRed;
        System.Drawing.Color clrSet = System.Drawing.Color.AliceBlue;
        //float scale = 96 / (float)(int)Registry.GetValue("HKEY_CURRENT_USER\\Control Panel\\Desktop", "LogPixels", 96);
        int scaleSmall = RhinoWindows.Forms.Dpi.ScaleInt(8);
        int scaleBig = RhinoWindows.Forms.Dpi.ScaleInt(12);
        int scaleMove = RhinoWindows.Forms.Dpi.ScaleInt(5);


        public DrawPFFaceConduit(List<PFFace> faces)
        {
            m_conduit_faces = faces;
            foreach (var face in faces)
            {
                bbox.Union(face.FMesh.GetBoundingBox(false));
            }
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
            foreach (var face in m_conduit_faces)
            {
                if (face.Picked)
                {
                    e.Display.DrawMeshShaded(face.FMesh, materialP);
                }
                else if (!double.IsNaN(face.TargetArea))
                {
                    e.Display.DrawMeshShaded(face.FMesh, materialNP);
                }
            }


        }

        protected override void DrawForeground(Rhino.Display.DrawEventArgs e)
        {
            if (m_conduit_faces != null)
            {

                foreach (var face in m_conduit_faces)
                {
                    
                    var pnt2d = e.Viewport.WorldToClient(face.Centroid);
                    pnt2d.X += scaleMove;
                    pnt2d.Y -= scaleMove;

                    //e.Display.DrawLine(m_conduit_edgeLines[l], m_conduit_edges[l].Color, 2);
                    if (!face.Picked)
                    {
                        e.Display.Draw2dText("F_" + face.Id.ToString(), clrNP, pnt2d, false, scaleSmall);
                        if (double.IsNaN(face.TargetArea))
                        {
                            e.Display.DrawPoint(face.Centroid, PointStyle.Simple, 3, clrNP);
                        }
                        else
                        {
                            e.Display.DrawPoint(face.Centroid, PointStyle.Simple, 4, clrSet);
                        }
                            
                        
                    }
                    else
                    {
                        var clr = System.Drawing.Color.DarkRed;
                        e.Display.DrawPoint(face.Centroid, PointStyle.Simple, 3, clrP);
                        e.Display.Draw2dText("area=" + Math.Round(face.Area, 3).ToString(), clrP, pnt2d, false, scaleBig);
                    }
                }
            }
        }
    }

    class DrawPFoamConduit : DisplayConduit
    {
        private System.Drawing.Color lnColor = System.Drawing.Color.SlateGray;
        private PFoam conduit_foam;
        private BoundingBox bbox = BoundingBox.Empty;
        private DisplayMaterial faceMaterial = new DisplayMaterial(System.Drawing.Color.SlateGray, .9);

        public bool showVerts = true;
        public bool showEdges = true;
        public bool showFaces = true;

        internal DrawPFoamConduit(PFoam foam)
        {
            this.conduit_foam = foam;
            var vPC = new PointCloud(foam.Vertices.Select(x => x.Point));
            bbox = vPC.GetBoundingBox(false);
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
            if (showFaces)
                foreach (var face in conduit_foam.Faces.Where(x => x.Id > 0))
                {
                    e.Display.DrawMeshShaded(face.FMesh, faceMaterial);
                }
            if (showEdges)
                foreach (var edge in conduit_foam.Edges.Where(x => x.Id > 0))
                {
                    e.Display.DrawLine(edge.Vertices[0].Point, edge.Vertices[1].Point, lnColor, 1);
                }
            if (showVerts)
                foreach (var vert in conduit_foam.Vertices)
                {
                    e.Display.DrawPoint(vert.Point, PointStyle.ControlPoint, 2, lnColor);
                }


        }



    }





}
