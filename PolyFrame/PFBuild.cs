using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using PolyFramework;
using System.Linq;
using System.Threading;

namespace PolyFrame
{
    public class PFBuild : Command
    {
        public PFBuild()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a reference in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static PFBuild Instance
        {
            get; private set;
        }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName
        {
            get { return "PFBuild"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // TODO: start here modifying the behaviour of your command.
            // ---
            //RhinoApp.WriteLine("The {0} command will add a line right now.", EnglishName);

            



            GetObject getBreps = new GetObject();
            getBreps.SetCommandPrompt("Pick the breps to create a PolyhedralFrame");
            getBreps.GeometryFilter = Rhino.DocObjects.ObjectType.Brep;
            // set up options 

            OptionDouble dblOptionCollapse = new Rhino.Input.Custom.OptionDouble(0.1, true, 0.0);
            OptionDouble dblOptionPlanar = new Rhino.Input.Custom.OptionDouble(0.1, true, 0.0);
            OptionToggle togOptionReplace = new OptionToggle(true, "Keep", "Replace");

            getBreps.AddOptionDouble("PointCollapseLimit", ref dblOptionCollapse);
            getBreps.AddOptionDouble("PlanarityMaxDeviation", ref dblOptionPlanar);

            getBreps.AddOptionToggle("ReplaceGeo", ref togOptionReplace);
            getBreps.AcceptNothing(false);

            while (true)
            {

                var r = getBreps.GetMultiple(1, 0);
                if (r == GetResult.Cancel) return getBreps.CommandResult();
                else if (r == GetResult.Object) break;
            }

            List<Guid> ids = new List<Guid>();
            for (int i = 0; i < getBreps.ObjectCount; i++)
            {
                ids.Add(getBreps.Object(i).ObjectId);
            }

            var foam = new PFoam();
            var deleteOriginal = false;
            try
            {



                foam.ProcessBFaces(Util.DecomposeG(ids), dblOptionCollapse.CurrentValue, dblOptionPlanar.CurrentValue);
                doc.Objects.UnselectAll();
                if (foam.Faces.Count > 2)
                {
                    foam.MakeCells();
                    Point3d newCentroid = foam.Centroid;
                    if (!togOptionReplace.CurrentValue)
                        Rhino.Input.RhinoGet.GetPoint("Place the PolyFrame", true, out newCentroid);
                    else deleteOriginal = true;
                        //Rhino.RhinoDoc.ActiveDoc.Objects.Delete(ids, true);
                    
                    
                    foam.Offset(newCentroid - foam.Centroid);
                    foam.Show(true, true, false);
                    foam.ShowCells();


                    if (!foam.SaveToDocument())
                    {
                        foam.Hide();
                        return Result.Cancel;
                    }
                 

                    foam.Hide();

                    if (deleteOriginal) Rhino.RhinoDoc.ActiveDoc.Objects.Delete(ids, true);
                    doc.Views.Redraw();
                    return Result.Success;

                }
                else
                {
                    Rhino.RhinoApp.WriteLine("Not enough faces!");
                    return Result.Failure;
                }

            }
            catch (PolyFrameworkException pfE)
            {
                RhinoApp.WriteLine(pfE.Message);
                foam.Hide();
                return Result.Failure;
            }




        }
    }
}
