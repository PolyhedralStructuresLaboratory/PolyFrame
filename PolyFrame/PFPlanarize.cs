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
    public class PFPlanarize : Command
    {
        static PFPlanarize _instance;
        public PFPlanarize()
        {
            _instance = this;
        }

        ///<summary>The only instance of the PFDual command.</summary>
        public static PFPlanarize Instance
        {
            get { return _instance; }
        }

        public override string EnglishName
        {
            get { return "PFPlanarize"; }
        }

        // loads or makes a polyframe and planarizes the faces 
        // load a set of geometries 
        // if lines => must have PolyFrame date attached 
        // if breps and data exists => reload topology data and update from geometry
        // if no data exists => make topology without cells 
        // planarize and dump to the file 
        // add topology data if cells are present. 

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            GetObject getObjects = new GetObject();
            getObjects.SetCommandPrompt("Pick the breps or PolyFrame container to planarize ");
            getObjects.GeometryFilter = 
                Rhino.DocObjects.ObjectType.Brep | 
                Rhino.DocObjects.ObjectType.Curve | 
                Rhino.DocObjects.ObjectType.Mesh;
            // set up options 

            getObjects.GroupSelect = true;

            OptionDouble dblOptionCollapse = new OptionDouble(0.1, true, 0.0);

            OptionToggle togOptionReplace = new OptionToggle(true, "Keep", "Replace");

            getObjects.AddOptionDouble("PointCollapseLimit", ref dblOptionCollapse);

            getObjects.AddOptionToggle("ReplaceGeo", ref togOptionReplace);
            getObjects.AcceptNothing(false);



            var primal = new PFoam();
            var dual = new PFoam();


            while (true)
            {

                var r = getObjects.GetMultiple(1, 0);
                if (r == GetResult.Cancel) return getObjects.CommandResult();
                else if (r == GetResult.Object) break;
            }



            List<Guid> ids = new List<Guid>();
            for (int i = 0; i < getObjects.ObjectCount; i++)
            {

                ids.Add(getObjects.Object(i).ObjectId);
            }


            try
            {


                ContainerType container = ContainerType.Edge;
                bool usePFTopo = false;
                var selGeo = getObjects.Objects().Select(x => x.Geometry());
                if (selGeo.All(x => x.ObjectType == Rhino.DocObjects.ObjectType.Brep))
                {
                    if (selGeo.Any(y => y.UserDictionary.TryGetString("Primal", out string primalJson)))
                    {
                        Rhino.Input.RhinoGet.GetBool("PolyFrame data detected. Use topology data from there?", false, "no", "yes", ref usePFTopo);
                    }

                    if (usePFTopo)
                    {
                        //guids = LoadData.LoadPrimalDual(out primal, out dual);

                        var brepGeo = selGeo.Cast<Brep>().ToList();
                        if (brepGeo.Any(x => x.Faces.Count > 1)) container = ContainerType.CellBrep;
                        else container = ContainerType.FaceBrep; 
                            
                        primal = LoadData.LoadFromFaces(brepGeo, out dual, true);
                    }
                    else
                    {
                        primal.ProcessBFaces(Util.DecomposeG(ids), dblOptionCollapse.CurrentValue, double.MaxValue);
                        primal.SortPartsInFaces();

                    }
                }
                else if (selGeo.All(x => x.ObjectType == Rhino.DocObjects.ObjectType.Curve))
                {
                    if (selGeo.Any(y => y.UserDictionary.TryGetString("Primal", out string primalJson)))
                    {
                        primal = LoadData.LoadFromEdges(selGeo.Cast<Curve>().ToList(), out dual, true);
                        //guids = LoadData.LoadPrimalDual(out primal, out dual);
                        container = ContainerType.Edge;
                    }
                    else
                    {
                        RhinoApp.WriteLine("No PolyFrame data detected. Planarization of raw line/curve data is not supported ");
                        return Result.Failure;
                    }
                }
                else if (selGeo.All(x=> x.ObjectType == Rhino.DocObjects.ObjectType.Mesh))
                {
                    if (selGeo.Any(y => y.UserDictionary.TryGetString("Primal", out string primalJson)))
                    {
                        var meshGeos = selGeo.Cast<Mesh>().ToList();
                        if (meshGeos.Any(x => x.Ngons.Count > 1 ||
                    (x.Ngons.Count == 0 && x.Faces.Count > 1) ||
                    (x.Ngons.Count == 1 && x.Faces.Count > x.Ngons[0].FaceCount))) container = ContainerType.CellMesh;
                        else container = ContainerType.FaceMesh;
                        primal = LoadData.LoadFromMeshes(meshGeos, out dual, true);

                    }
                    else
                    {
                        RhinoApp.WriteLine("No PolyFrame data detected. Planarization of raw mesh data is not supported ");
                        return Result.Failure;
                    }

                }
                else
                {
                    RhinoApp.WriteLine("Mixed data detected. You should input only surfaces(breps) or curves(lines)!");
                    return Result.Failure;
                }

                if (dual.Cells.Count > 0 && dual.Id == primal.Dual.Id )
                {
                    Util.ConnectDuals(ref primal, ref dual);
                }


                primal.Centroid = PFVertex.AverageVertexes(primal.Vertices);
                Point3d newCentroid = primal.Centroid;
                if (!togOptionReplace.CurrentValue)
                    Rhino.Input.RhinoGet.GetPoint("Place the PolyFrame", true, out newCentroid);
                else Rhino.RhinoDoc.ActiveDoc.Objects.Delete(ids, true);

                doc.Views.Redraw();
                primal.Offset(newCentroid - primal.Centroid);

                primal.Show(true, true, false);
                

                var minEdgeLen = primal.Edges.Select(x => x.GetLength()).Min();

                var getMaxDev = new Rhino.Input.Custom.GetNumber();
                var stepOption = new OptionInteger(5000, true, 1);
                var typeToggle = new OptionToggle(true, "HType", "SType");

                var optionSetEdge = new OptionToggle(false, "setE", "E_set");
                var optionSetVert = new OptionToggle(false, "setV", "V_set");
                var optionSetFace = new OptionToggle(false, "setF", "F_set");


                var minEdgeOption = new OptionDouble(minEdgeLen/2, true, double.Epsilon);
                getMaxDev.SetCommandPrompt("Set maximum allowed vertex deviation from the plane");
                getMaxDev.SetDefaultNumber(doc.ModelAbsoluteTolerance / 10);
                getMaxDev.AddOptionInteger("MaxIterations", ref stepOption);
                getMaxDev.AddOptionToggle("Algorithm", ref typeToggle);
                getMaxDev.AddOptionDouble("MinEdgeLength", ref minEdgeOption);
                getMaxDev.AddOptionToggle("SetVerPos", ref optionSetVert);
                getMaxDev.AddOptionToggle("SetEdgeLen", ref optionSetEdge);
                getMaxDev.AddOptionToggle("SetFaceArea", ref optionSetFace);

                bool storedVertOpt = false;
                bool storedEdgeOpt = false;
                bool storedFaceOpt = false;

                

                //var resul = getMaxDev.Result();
                while (true)
                {
                    getMaxDev.Get();
                    if (getMaxDev.Result() == GetResult.Number)
                    {
                        //RhinoApp.CommandPrompt = "Planarization in progress. Please wait... Or press <ESC> to interrupt.";
                        //RhinoApp.WriteLine("Planarization in progress. Please wait... Or press <ESC> to interrupt.");
                        if (typeToggle.CurrentValue)
                        {
                            //primal.PickFaces();

                            foreach (var edge in primal.Edges)
                            {
                                edge.MinLength = minEdgeOption.CurrentValue;
                                edge.InfluenceCoef = .2;
                            }
                            primal.PlanarizeSoft(stepOption.CurrentValue, getMaxDev.Number());
                        }
                        else
                        {
                            RhinoApp.WriteLine("HType planarization does not take into consideration any set constraints!");
                            primal.Planarize(stepOption.CurrentValue, getMaxDev.Number());
                        }
                        
                        break;
                    }
                    else if (getMaxDev.Result() == GetResult.Cancel)
                    {
                        primal.Hide();
                        return Result.Cancel;
                    }
                    else if (getMaxDev.Result() == GetResult.Option)
                    {
                        if (storedVertOpt != optionSetVert.CurrentValue)
                        {
                            storedVertOpt = optionSetVert.CurrentValue;
                            primal.PickVertex();
                        }
                        if (storedEdgeOpt != optionSetEdge.CurrentValue)
                        {
                            storedEdgeOpt = optionSetEdge.CurrentValue;
                            primal.PickEdges();
                        }
                        if (storedFaceOpt != optionSetFace.CurrentValue)
                        {
                            storedFaceOpt = optionSetFace.CurrentValue;
                            primal.PickFaces();
                        }

                    }
                }






                //primal.Planarize(1000, 0.00001);




                // saving 

                if (primal.Cells.Count < 1 || primal.Faces.All(x => x.Id > 0))
                {
                    foreach (var face in primal.Faces) if (face.Id > 0)
                            doc.Objects.AddBrep(face.CreateBrep());
                }
                else
                {
                    if (!primal.SaveToDocument(container))
                    {
                        primal.Hide();
                        return Result.Cancel;
                    }
                }

                
                primal.Hide();


                return Result.Success;
            }
            catch (PolyFrameworkException pfE)
            {
                RhinoApp.WriteLine(pfE.Message);
                primal.Hide();
                dual.Hide();
                return Result.Failure;

            }


            // TODO: complete command.

        }
    }
}