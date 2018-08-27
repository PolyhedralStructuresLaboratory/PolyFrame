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

namespace PolyFramework
{


    public partial class PFoam
    {
        public IList<PFVertex> PickVertex()
        {
            var innerVerts = Vertices.Where(x => !x.External);
            var pickedVerts = new List<PFVertex>();
            var doc = Rhino.RhinoDoc.ActiveDoc;
            var vertexConduit = new DrawPFVertexConduit(innerVerts)
            {
                Enabled = true
            };
            doc.Views.Redraw();
            var gVert = new GetPFVertex(innerVerts);
            //var permOption = new OptionToggle(false, "temp", "perm");
            gVert.AddSnapPoints(innerVerts.Select(x => x.Point).ToArray());
            while (true)
            {
                gVert.SetCommandPrompt("select Vertex points. (<ESC> to exit");
                //gVert.AddOptionToggle("Remember", ref permOption);
                gVert.AcceptNothing(true);
                gVert.AcceptString(true);
                //gVert.SetCursor() // this we can change after switching to Rhino 6 - api

                gVert.SetDefaultString($"({pickedVerts.Count}) vertices selected press <Enter> to accept");
                gVert.Get(true);


                doc.Views.Redraw();
                var result = gVert.CommandResult();
                var strResult = gVert.StringResult();
                pickedVerts = new List<PFVertex>();
                if (gVert.CommandResult() == Rhino.Commands.Result.Cancel)
                {
                    break;
                }
                foreach (var vrt in innerVerts)
                {
                    if (vrt.Picked)
                    {
                        pickedVerts.Add(vrt);
                    }
                }
                if (gVert.GotDefault())
                {
                    // here use a pick mechanism to get the geometry 
                    if (pickedVerts.Count > 0)
                    {
                        var outside = new OptionToggle(true, "true", "false");
                        var fixPoint = new OptionToggle(false, "constrain", "fix");
                        var getRefGeo = new GetObject();
                        getRefGeo.SetCommandPrompt("Pick the constrain object for the selected vertices");

                        getRefGeo.AddOptionToggle("Outside", ref outside);
                        getRefGeo.AddOptionToggle("Fixed", ref fixPoint);
                        getRefGeo.AcceptNothing(true);
                        getRefGeo.DisablePreSelect();



                        if (getRefGeo.Get() == Rhino.Input.GetResult.Object)
                        {
                            var geoGet = getRefGeo.Object(0);
                            var idGet = getRefGeo.Object(0).ObjectId;

                            SetVertexConstraints(pickedVerts, geoGet.Object(), 1, outside.CurrentValue);

                        }

                        else if (getRefGeo.Result() == Rhino.Input.GetResult.Nothing)
                        {
                            foreach (var vert in pickedVerts)
                            {
                                vert.RestrictPosition = null;
                                vert.RestrictSupport = null;
                                vert.SupportGuid = Guid.Empty;
                            }
                        }

                        else if (getRefGeo.Result() == Rhino.Input.GetResult.Option && fixPoint.CurrentValue == true)
                        {
                            foreach (var vert in pickedVerts)
                            {
                                vert.RestrictPosition = vert.ConstrainPoint;
                                vert.RestrictSupport = new Rhino.Geometry.Point(vert.Point); // this is what keeps the vertex in place 
                                vert.InfluenceCoef = 100; // this is what makes it relatively immovable 
                                vert.SupportGuid = Guid.Empty;
                                vert.Fixed = true; // this is just for serilization refference 
                            }
                        }
                        doc.Objects.UnselectAll();
                        pickedVerts.ForEach(x => x.Picked = false);
                        doc.Views.Redraw();
                    }
                    else
                    {
                        break;
                    }
                }
            }
            vertexConduit.Enabled = false;
            doc.Views.Redraw();

            return pickedVerts;

        }

        /// <summary>
        /// Simple version of the above method that only returns a single vertex
        /// Does not add any properties to the vertex. 
        /// </summary>
        /// <returns></returns>
        public IList<PFVertex> PickVertexSingle()
        {
            var innerVerts = Vertices.Where(x => !x.External);
            var pickedVerts = new List<PFVertex>();
            var doc = Rhino.RhinoDoc.ActiveDoc;
            var vertexConduit = new DrawPFVertexConduit(innerVerts)
            {
                Enabled = true
            };
            doc.Views.Redraw();
            var gVert = new GetPFVertex(innerVerts);
            gVert.AddSnapPoints(innerVerts.Select(x => x.Point).ToArray());
            while (true)
            {
                gVert.SetCommandPrompt("select Vertex points. (<ESC> to exit");
                gVert.AcceptNothing(true);
                gVert.AcceptString(true);
                //gVert.SetCursor() // this we can change after switching to Rhino 6 - api

                gVert.SetDefaultString($"({pickedVerts.Count}) vertices selected press <Enter> to accept");
                gVert.Get(true);


                doc.Views.Redraw();
                var result = gVert.CommandResult();
                var strResult = gVert.StringResult();
                pickedVerts = new List<PFVertex>();
                if (gVert.CommandResult() == Rhino.Commands.Result.Cancel)
                {
                    break;
                }
                bool brkFlag = false;
                foreach (var vrt in innerVerts)
                {
                    if (vrt.Picked)
                    {
                        pickedVerts.Add(vrt);
                        brkFlag = true;
                        break; // remove this for multiple picks  
                    }
                }
                if (brkFlag) break;

            }
            vertexConduit.Enabled = false;
            doc.Views.Redraw();

            return pickedVerts;

        }


        public IList<PFEdge> PickEdges(IList<PFEdge> available = null)
        {
            var pickedEdges = new List<PFEdge>();
            var doc = Rhino.RhinoDoc.ActiveDoc;
            var selection = new HashSet<PFEdge>();
            if (available != null && available.Count > 0)
            {
                selection = new HashSet<PFEdge>(available.Where(x => x.Id > 0));
            }
            else
            {
                selection = new HashSet<PFEdge>(Edges.Where(x => x.Id > 0));
            }
            var intPositiveEdges = Edges.Where(x => selection.Contains(x) && x.Vertices.All(y => !y.External)).ToList();
            var midPoints = intPositiveEdges.Select(x => PFVertex.AverageVertexes(x.Vertices));
            var edgeConduit = new DrawPFEdgeConduit(intPositiveEdges)
            {
                Enabled = true
            };
            doc.Views.Redraw();
            var gEdge = new GetPFEdge(intPositiveEdges);
            //var permOption = new OptionToggle(false, "temp", "perm");
            gEdge.SetCommandPrompt("select Edges by mid-points. (<ESC> to exit");
            //gEdge.AddOptionToggle("Remember", ref permOption);
            gEdge.AcceptNothing(true);
            gEdge.AcceptString(true);
            gEdge.AddSnapPoints(midPoints.ToArray());
            while (true)
            {

                gEdge.SetCursor(CursorStyle.Default); // this we can change after switching to Rhino 6 - api

                gEdge.SetDefaultString($"({pickedEdges.Count}) edges selected press <Enter> to accept");
                gEdge.Get(true);


                doc.Views.Redraw();
                var result = gEdge.CommandResult();
                var strResult = gEdge.StringResult();
                pickedEdges = new List<PFEdge>();
                if (gEdge.CommandResult() == Rhino.Commands.Result.Cancel)
                {
                    break;
                }
                var lastPointClicked = new Point3d();
                foreach (var edge in intPositiveEdges)
                {
                    if (edge.Picked)
                    {
                        //edge.Picked = false;
                        pickedEdges.Add(edge);
                        lastPointClicked = PFVertex.AverageVertexes(edge.Vertices);
                        
                    }
                }
                //edgeConduit.UpdateLines();
                //doc.Views.Redraw();

                if (gEdge.GotDefault())
                {
                    var targetLen = GetData.GetDoubleInViewport(lastPointClicked);
                    foreach (var edge in pickedEdges)
                    {
                        edge.TargetLength = targetLen;
                        edge.InfluenceCoef = 1;
                       
                    }
                    doc.Views.Redraw();
                    break;
                }
            }
            edgeConduit.Enabled = false;
            doc.Views.Redraw();
            return pickedEdges;
        }


        public IList<PFEdge> PickEdge(IList<PFEdge> available = null)
        {
            var pickedEdges = new List<PFEdge>();
            var doc = Rhino.RhinoDoc.ActiveDoc;
            var selection = new HashSet<PFEdge>();
            if (available != null && available.Count > 0)
            {
                selection = new HashSet<PFEdge>(available.Where(x => x.Id > 0));
            }
            else
            {
                selection = new HashSet<PFEdge>(Edges.Where(x => x.Id > 0));
            }
            var intPositiveEdges = Edges.Where(x => selection.Contains(x) && x.Vertices.All(y => !y.External)).ToList();
            var midPoints = intPositiveEdges.Select(x => PFVertex.AverageVertexes(x.Vertices));
            var edgeConduit = new DrawPFEdgeConduit(intPositiveEdges)
            {
                Enabled = true
            };
            doc.Views.Redraw();
            var gEdge = new GetPFEdge(intPositiveEdges);
            //var permOption = new OptionToggle(false, "temp", "perm");
            gEdge.SetCommandPrompt("select Edges by mid-points. (<ESC> to exit");
            //gEdge.AddOptionToggle("Remember", ref permOption);
            gEdge.AcceptNothing(true);
            gEdge.AcceptString(true);
            gEdge.AddSnapPoints(midPoints.ToArray());
            while (true)
            {

                gEdge.SetCursor(CursorStyle.Hand); // this we can change after switching to Rhino 6 - api

                gEdge.SetDefaultString($"({pickedEdges.Count}) edges selected press <Enter> to accept");
                gEdge.Get(true);


                doc.Views.Redraw();
                var result = gEdge.CommandResult();
                var strResult = gEdge.StringResult();
                pickedEdges = new List<PFEdge>();
                if (gEdge.CommandResult() == Rhino.Commands.Result.Cancel)
                {
                    break;
                }
                foreach (var edge in intPositiveEdges)
                {
                    if (edge.Picked)
                    {
                        edge.Picked = false;
                        pickedEdges.Add(edge);
                        edge.TargetLength = GetData.GetDoubleInViewport(PFVertex.AverageVertexes(edge.Vertices), edge.TargetLength);
                        edge.InfluenceCoef = 1;
                        doc.Views.Redraw();
                        break;
                    }
                }
                //edgeConduit.UpdateLines();
                //doc.Views.Redraw();

                if (gEdge.GotDefault())
                {
                    break;
                }
            }
            edgeConduit.Enabled = false;
            doc.Views.Redraw();
            return pickedEdges;
        }




        public IList<PFEdge> PickEdgeSingle(IList<PFEdge> available = null)
        {
            var pickedEdges = new List<PFEdge>();
            var doc = Rhino.RhinoDoc.ActiveDoc;
            var selection = new HashSet<PFEdge>();
            if (available != null && available.Count > 0)
            {
                selection = new HashSet<PFEdge>(available.Where(x => x.Id > 0));
            }
            else
            {
                selection = new HashSet<PFEdge>(Edges.Where(x => x.Id > 0));
            }
            var intPositiveEdges = Edges.Where(x => selection.Contains(x)).ToList();
            var midPoints = intPositiveEdges.Select(x => PFVertex.AverageVertexes(x.Vertices));
            var edgeConduit = new DrawPFEdgeConduit(intPositiveEdges)
            {
                Enabled = true
            };
            doc.Views.Redraw();
            var gEdge = new GetPFEdge(intPositiveEdges);
            gEdge.AddSnapPoints(midPoints.ToArray());
            while (true)
            {
                gEdge.SetCommandPrompt("select Edge by mid-point. (<ESC> to exit");
                gEdge.AcceptNothing(true);
                gEdge.AcceptString(true);
                //gVert.SetCursor() // this we can change after switching to Rhino 6 - api

                gEdge.SetDefaultString($"({pickedEdges.Count}) edges selected press <Enter> to accept");
                gEdge.Get(true);


                doc.Views.Redraw();
                var result = gEdge.CommandResult();
                var strResult = gEdge.StringResult();
                pickedEdges = new List<PFEdge>();
                if (gEdge.CommandResult() == Rhino.Commands.Result.Cancel)
                {
                    break;
                }
                bool breakFlag = false;
                foreach (var edge in intPositiveEdges)
                {
                    if (edge.Picked)
                    {
                        edge.Picked = false;
                        pickedEdges.Add(edge);
                        breakFlag = true;
                        doc.Views.Redraw();
                        break;
                    }
                }

                if (breakFlag) break;
                //edgeConduit.UpdateLines();
                //doc.Views.Redraw();

                if (gEdge.GotDefault())
                {
                    break;
                }
            }
            edgeConduit.Enabled = false;
            doc.Views.Redraw();
            return pickedEdges;
        }


        public IList<PFFace> PickFaces()
        {
            var pickedFaces = new List<PFFace>();
            var doc = Rhino.RhinoDoc.ActiveDoc;
            var positiveFaces = Faces.Where(x => x.Id > 0).ToList();

            var faceConduit = new DrawPFFaceConduit(positiveFaces)
            {
                Enabled = true
            };
            doc.Views.Redraw();
            var gEdge = new GetPFFace(positiveFaces);
            gEdge.AddSnapPoints(positiveFaces.Select(x => x.Centroid).ToArray());
            while (true)
            {
                gEdge.SetCommandPrompt("select Faces by mid-points. (<ESC> to exit");
                gEdge.AcceptNothing(true);
                gEdge.AcceptString(true);
                //gVert.SetCursor() // this we can change after switching to Rhino 6 - api

                gEdge.SetDefaultString($"({pickedFaces.Count}) faces selected press <Enter> to accept");
                gEdge.Get(true);


                doc.Views.Redraw();
                var result = gEdge.CommandResult();
                var strResult = gEdge.StringResult();
                pickedFaces = new List<PFFace>();
                if (gEdge.CommandResult() == Rhino.Commands.Result.Cancel)
                {
                    break;
                }
                foreach (var face in positiveFaces)
                {
                    if (face.Picked)
                    {
                        face.Picked = false;
                        pickedFaces.Add(face);
                        face.TargetArea = GetData.GetDoubleInViewport(face.Centroid, face.TargetArea);
                        face.InfluenceCoef = 0.1;
                        doc.Views.Redraw();
                        break;
                    }



                }


                if (gEdge.GotDefault())
                {
                    break;
                }
            }
            faceConduit.Enabled = false;
            doc.Views.Redraw();
            return pickedFaces;
        }

        public void Show(bool verts = true, bool edges = true, bool faces = true)
        {
            var doc = Rhino.RhinoDoc.ActiveDoc;
            if (DispConduit == null)
            {
                DispConduit = new DrawPFoamConduit(this);
            }
            DispConduit.showVerts = verts;
            DispConduit.showEdges = edges;
            DispConduit.showFaces = faces;
            DispConduit.Enabled = true;
            doc.Views.Redraw();
        }

        public void Hide()
        {
            var doc = Rhino.RhinoDoc.ActiveDoc;
            if (DispConduit != null) DispConduit.Enabled = false;
            doc.Views.Redraw();

        }




        public void SetVertexConstraints(IList<PFVertex> constr, Rhino.DocObjects.RhinoObject cBase, double coef = 1.0, bool on = true)
        {
            foreach (var vrt in constr)
            {
                vrt.RestrictSupport = cBase.Geometry;
                vrt.InfluenceCoef = coef;

                vrt.SupportGuid = cBase.Id;

            }

            var type = cBase.Geometry.GetType();
            
            if (cBase.Geometry is Rhino.Geometry.Point) //(cBase.Geometry.GetType() == typeof(Rhino.Geometry.Point))
            {
                foreach (var vert in constr)
                {
                    vert.RestrictPosition = vert.ConstrainPoint;
                }
            }
            else if (cBase.Geometry is Curve) //(cBase.Geometry.GetType() == typeof(Curve) || cBase.Geometry.GetType() == typeof(NurbsCurve) || cBase.Geometry.GetType() == typeof(LineCurve))
            {
                foreach (var vert in constr)
                {
                    vert.RestrictPosition = vert.ConstrainCurve;
                }
            }
            else if (cBase.Geometry is Brep) //(cBase.Geometry.GetType() == typeof(Brep))
            {
                foreach (var vert in constr)
                {
                    if (on)
                    {
                        vert.RestrictPosition = vert.ConstrainOnBrep;
                    }
                    else
                    {
                        vert.RestrictPosition = vert.ConstrainInBrep;
                    }
                }
            }
            else if (cBase.Geometry is Mesh)// (cBase.Geometry.GetType() == typeof(Mesh))
            {
                foreach (var vert in constr)
                {
                    if (on)
                    {
                        vert.RestrictPosition = vert.ConstrainOnMesh;
                    }
                    else
                    {
                        vert.RestrictPosition = vert.ConstrainInMesh;
                    }
                }
            }
        }


        public void SetVertexConstraints(IList<PFVertex> constr, GeometryBase cBase, double coef = 1.0, bool on = true)
        {
            foreach (var vrt in constr)
            {
                vrt.RestrictSupport = cBase;
                vrt.InfluenceCoef = coef;

                //vrt.SupportGuid = cBase.Id;

            }
            if (cBase.GetType() == typeof(Rhino.Geometry.Point))
            {
                foreach (var vert in constr)
                {
                    vert.RestrictPosition = vert.ConstrainPoint;
                }
            }
            else if (cBase.GetType() == typeof(Curve) || cBase.GetType() == typeof(NurbsCurve))
            {
                foreach (var vert in constr)
                {
                    vert.RestrictPosition = vert.ConstrainCurve;
                }
            }
            else if (cBase.GetType() == typeof(Brep))
            {
                foreach (var vert in constr)
                {
                    if (on)
                    {
                        vert.RestrictPosition = vert.ConstrainOnBrep;
                    }
                    else
                    {
                        vert.RestrictPosition = vert.ConstrainInBrep;
                    }
                }
            }
            else if (cBase.GetType() == typeof(Mesh))
            {
                foreach (var vert in constr)
                {
                    if (on)
                    {
                        vert.RestrictPosition = vert.ConstrainOnMesh;
                    }
                    else
                    {
                        vert.RestrictPosition = vert.ConstrainInMesh;
                    }
                }
            }
        }


    }


}
