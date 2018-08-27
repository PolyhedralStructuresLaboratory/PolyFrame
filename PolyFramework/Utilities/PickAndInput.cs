using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using Rhino.UI;
using System.Diagnostics;
using System.Windows.Forms;
using System;
using System.Drawing;
using System.Runtime.Serialization;
using System.IO;
using System.Web.Script.Serialization;
using static PolyFramework.Util;
using Rhino.Input.Custom;
using RhinoWindows;


namespace PolyFramework
{
    public class GetPFVertex : GetPoint
    {
        private readonly IEnumerable<PFVertex> m_conduit_vertices;

        public GetPFVertex(IEnumerable<PFVertex> conduitVertices)
        {
            m_conduit_vertices = conduitVertices;
            foreach (var vert in m_conduit_vertices) vert.Picked = false;
        }

        protected override void OnMouseDown(GetPointMouseEventArgs e)
        {
            base.OnMouseDown(e);
            var picker = new PickContext
            {
                View = e.Viewport.ParentView,

                PickStyle = PickStyle.PointPick
            };


            var xform = e.Viewport.GetPickTransform(e.WindowPoint);
            picker.SetPickTransform(xform);

            foreach (var vrt in m_conduit_vertices)
            {

                if (picker.PickFrustumTest(vrt.Point, out double depth, out double distance))
                {
                    vrt.Picked = !vrt.Picked;
                }

            }
        }
    }

    public class GetPFEdge : GetPoint
    {
        private readonly IEnumerable<PFEdge> m_conduit_edges;
        private readonly List<Line> m_conduit_lines = new List<Line>();

        public GetPFEdge(List<PFEdge> conduitEdges)
        {
            m_conduit_edges = conduitEdges;
            foreach (var edge in m_conduit_edges)
            {
                edge.Picked = false;
                m_conduit_lines.Add(edge.CreateLine());
            }

        }

        protected override void OnMouseDown(GetPointMouseEventArgs e)
        {
            base.OnMouseDown(e);
            var picker = new PickContext
            {
                View = e.Viewport.ParentView,

                PickStyle = PickStyle.PointPick
            };


            var xform = e.Viewport.GetPickTransform(e.WindowPoint);
            picker.SetPickTransform(xform);

            foreach (var edgeLine in m_conduit_edges.Zip(m_conduit_lines, (edge, line) => new { edge, line }))
            {

                if (picker.PickFrustumTest(edgeLine.line, out double t, out double depth, out double distance))
                {
                    edgeLine.edge.Picked = !edgeLine.edge.Picked;
                }

            }
        }
    }

    public class GetPFFace : GetPoint
    {
        private readonly IEnumerable<PFFace> m_conduit_faces;


        public GetPFFace(List<PFFace> conduitFaces)
        {
            m_conduit_faces = conduitFaces;
            foreach (var face in m_conduit_faces)
            {
                face.Picked = false;
            }

        }

        protected override void OnMouseDown(GetPointMouseEventArgs e)
        {
            base.OnMouseDown(e);
            var picker = new PickContext
            {
                View = e.Viewport.ParentView,

                PickStyle = PickStyle.None
            };


            var xform = e.Viewport.GetPickTransform(e.WindowPoint);
            picker.SetPickTransform(xform);

            foreach (var face in m_conduit_faces)
            {

                if (picker.PickFrustumTest(face.Centroid, out double depth, out double distance))
                {
                    face.Picked = !face.Picked;
                }

            }
        }
    }

    public static class GetData
    {
        

        /// <summary>
        /// Gets a double by popping a dialog in the viewport where a 3d point is 
        /// </summary>
        /// <param name="atPoint"></param>
        public static double GetDoubleInViewport(Point3d atPoint, double startValue = double.NaN)
        {
            var scaleOff = RhinoWindows.Forms.Dpi.ScaleInt(10);
            var testDiag = new ValueInput()
            {
                resValue = startValue
            };
            testDiag.StartPosition = FormStartPosition.Manual;
            
            var screenPoint_R = Rhino.RhinoDoc.ActiveDoc.Views.ActiveView.ActiveViewport.WorldToClient(atPoint);
            Rhino.RhinoDoc.ActiveDoc.Views.ActiveView.ActiveViewport.GetScreenPort(out int portLeft, out int porright, out int portBottom, out int portTop, out int portNear, out int portFar);
            var screenPoint = new System.Drawing.Point((int)screenPoint_R.X, (int)screenPoint_R.Y+ scaleOff);
            var scrPnt = Rhino.RhinoDoc.ActiveDoc.Views.ActiveView.ClientToScreen(screenPoint);
            testDiag.Location = scrPnt;
            testDiag.WindowState = FormWindowState.Normal;
            

            var dr = testDiag.ShowDialog();

            var resVal = testDiag.resValue;

            return resVal;
        }
    }
}
