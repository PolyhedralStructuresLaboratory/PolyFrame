using System;
using Rhino;
using Rhino.Commands;
using PolyFramework;
using Rhino.Geometry;
using System.Collections.Generic;

namespace PolyFrame
{
    public class PFTransformConstrained : Command
    {
        static PFTransformConstrained _instance;
        public PFTransformConstrained()
        {
            _instance = this;
        }

        ///<summary>The only instance of the PFTransformConstrained command.</summary>
        public static PFTransformConstrained Instance
        {
            get { return _instance; }
        }

        public override string EnglishName
        {
            get { return "PFTransformConstrained"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // get PolyFrame - 
            // Get the transformation vertex with options 
            // Restrict transformation to (edge/line, face/plane)
            // Transform constrained with preview  
            // Bake transformation to document. 

            var primal = new PFoam();
            var dual = new PFoam();

            try
            {
                // load 
                var guids = LoadData.LoadPrimalDual(out primal, out dual, out ContainerType container);


                var vertices = primal.PickVertexSingle();

                var edge = primal.PickEdgeSingle(vertices[0].Edges);

                // 
                var pSel = new GetPolyTransPoint(primal, vertices[0], false);
                pSel.SetBasePoint(vertices[0].Point, true);
                
                if (edge.Count > 0)
                {
                    pSel.Constrain(edge[0].CreateLine());
                }
                else
                {
                    pSel.EnableSnapToCurves(true);
                }
                pSel.Get();


                if (pSel.CommandResult() == Result.Success)
                {
                    var result = primal.MoveFaceVertices_Form(vertices, 
                        new List<Point3d> { pSel.Point() }, doc.ModelAbsoluteTolerance, false);

                    foreach (var vert in primal.Vertices)
                    {
                        vert.Point = result.Vertices[vert].Position;
                    }

                    foreach (var face in primal.Faces)
                    {
                        face.FaceMesh();
                        face.ComputeCentroid();
                        face.ComputeFaceNormal();
                    }
                    foreach (var cell in primal.Cells)
                    {
                        cell.ComputeCentroid();

                    }
                    primal.Centroid = PFVertex.AverageVertexes(primal.Vertices);


                    if (!primal.SaveToDocument(out bool replace, container))
                    {
                        //primal.Hide();
                        return Result.Cancel;
                    }
                    else
                    {
                        if (replace)
                        {
                            Rhino.RhinoDoc.ActiveDoc.Objects.Delete(guids, true); 
                        }
                        doc.Views.Redraw();
                        return Result.Success;
                    }
                }
                else
                {
                    return Result.Failure;
                }
               
            }
            catch (PolyFrameworkException pfE)
            {
                RhinoApp.WriteLine(pfE.Message);
                primal.Hide();
                dual.Hide();
                return Result.Failure;

            }



           
        }
    }
}