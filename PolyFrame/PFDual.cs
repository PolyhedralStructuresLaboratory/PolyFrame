using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using PolyFramework;
using System.Linq;

namespace PolyFrame
{
    public class PFDual : Command
    {
        static PFDual _instance;
        public PFDual()
        {
            _instance = this;
        }

        ///<summary>The only instance of the PFDual command.</summary>
        public static PFDual Instance
        {
            get { return _instance; }
        }

        public override string EnglishName
        {
            get { return "PFDual"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var primal = new PFoam();
            var dual = new PFoam();

            // load the foam from the geometry

            try
            {


                var guids = LoadData.LoadPrimalDual(out primal, out dual, out ContainerType container, false);


                if (primal.Cells.Count < 1)
                {
                    Rhino.RhinoApp.WriteLine("Error creating dual from provided data!");
                    return Result.Failure;
                }

                bool usePFDual = true;
                bool validDual = false;
                var form = primal.Edges.Where(e => e.Id > 0).Any(x => x.Faces.Count < 2);
                if (dual.Id == primal.Dual.Id && dual.Cells.Count >= 1)
                {
                    validDual = true;
                    //Rhino.Input.RhinoGet.GetBool("PolyFrame Dual data detected. Use stored geometry?", false, "no", "yes", ref usePFDual);
                }


                // here primal connection to dual is only established if dual is not fully reconstructed. 
                if (validDual && !usePFDual)
                {
                    var tempDualDict = new Dictionary<int, Point3d>();
                    if (form)
                    {
                        tempDualDict = primal.ComputePrimalVertices();
                    }
                    else
                    {
                        tempDualDict = primal.ComputeDualVertices();
                    }

                    foreach (var vert in dual.Vertices)
                    {
                        vert.Point = tempDualDict[vert.Id];
                    }

                    foreach (var face in dual.Faces)
                    {
                        face.FaceMesh();
                        face.ComputeCentroid();
                        face.ComputeFaceNormal();
                    }
                    foreach (var cell in dual.Cells)
                    {
                        cell.ComputeCentroid();

                    }
                    dual.Centroid = PFVertex.AverageVertexes(primal.Vertices);

                    Util.ConnectDuals(ref primal, ref dual);

                }
                else if (!validDual)
                {
                    if (form)
                    {
                        dual = primal.CreatePrimal();
                    }
                    else
                    {
                        dual = primal.CreateDual();
                    }
                }
                else if (validDual && usePFDual)
                {
                    Util.ConnectDuals(ref primal, ref dual);

                }





                //Point3d newCentroid = dual.Centroid;
                var gp = new Rhino.Input.Custom.GetPoint();
                gp.SetCommandPrompt("Place the PolyFrame. Hit <Enter> to replace Primal");
                gp.SetDefaultPoint(primal.Centroid);
                gp.AcceptPoint(true);

                gp.Get();

                if (gp.CommandResult() != Result.Success) return Result.Failure;


                var moveVect = gp.Point() - dual.Centroid;



                dual.Offset(moveVect);

                dual.Show(true, true, false);

                dual.ShowCells();


                if (!dual.SaveToDocument(container))
                {

                    dual.Hide();
                    return Result.Cancel;
                }
                dual.Hide();

                // also need to update the primal to have a connection to the dual 

                if (!gp.GotDefault())
                {
                    var dualJsonString = dual.SerializeJson();
                    var primalJsonString = primal.SerializeJson();
                    bool updatedPrimal = false;

                    foreach (var docGeoObj in guids)
                    {
                        //var geomObj = docGeoObj.Object();
                        var geomObj = doc.Objects.Find(docGeoObj);// docGeoObj.Geometry();
                        // look for the primal but change the dual. 
                        if (geomObj.Geometry.UserDictionary.TryGetString("Primal", out string dummyVal))
                        {
                            //var objData = docGeoObj.Object();
                            geomObj.Geometry.UserDictionary.Set("Primal", primalJsonString);
                            geomObj.Geometry.UserDictionary.Set("Dual", dualJsonString);
                            geomObj.CommitChanges();
                            updatedPrimal = true;



                            break;
                        }

                    }

                    if (updatedPrimal)
                    {
                        Rhino.RhinoApp.WriteLine("Primal geometry was also updated with Dual data.");
                    }
                    else
                    {
                        Rhino.RhinoApp.WriteLine("Failed to update Primal geometry with Dual data.");
                    }
                }
                else
                {

                    doc.Objects.Delete(guids, true);
                    doc.Views.Redraw();

                    Rhino.RhinoApp.WriteLine("Primal deleted. You can restore it using PFDual command on the result.");
                }

                Rhino.RhinoApp.WriteLine($"Constructed a PolyFrame object (dual) with {dual.Cells.Count} cells, {dual.Faces.Count} half-faces, {dual.Edges.Count} half-edges and {dual.Vertices.Count} vertices.");



            }
            catch (PolyFrameworkException pfE)
            {
                RhinoApp.WriteLine(pfE.Message + " Press <Esc> to continue.");
                primal.Hide();
                dual.Hide();
                return Result.Failure;


            }


            return Result.Success;

            // TODO: complete command.

        }
    }
}