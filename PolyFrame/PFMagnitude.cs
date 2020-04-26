using System;
using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using PolyFramework;
using Rhino.Input;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using Rhino.DocObjects;

namespace PolyFrame
{
    public class PFMagnitude : Command
    {
        static PFMagnitude _instance;
        public PFMagnitude()
        {
            _instance = this;
        }

        ///<summary>The only instance of the PFSwitch command.</summary>
        public static PFMagnitude Instance
        {
            get { return _instance; }
        }

        public override string EnglishName
        {
            get { return "PFMagnitude"; }
        }


        // Switches the container representation of a PolyFrame 

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {




            var primal = new PFoam();
            var dual = new PFoam();


            try
            {

                var guids = LoadData.LoadPrimalDual(out primal, out dual, out ContainerType container);

                if (container != ContainerType.Edge)
                {
                    Rhino.RhinoApp.WriteLine("This command only works on Line Form Diagrams!");
                    return Result.Failure;
                }

                if (primal.IsForm())
                {
                    var magDict = new Dictionary<int, double>();

                    foreach (var edge in primal.Edges)
                    {
                        if (edge.Dual != null)
                            magDict.Add(edge.Id, edge.Dual.Area);

                    }

                    foreach (var guid in guids)
                    {
                        var docLine = RhinoDoc.ActiveDoc.Objects.FindGeometry(guid);
                        if (magDict.ContainsKey(docLine.UserDictionary.GetInteger("Id")))
                        {
                            ObjectAttributes attr = RhinoDoc.ActiveDoc.Objects.FindId(guid).Attributes;
                            attr.Name = magDict[key: docLine.UserDictionary.GetInteger("Id")].ToString();

                            RhinoDoc.ActiveDoc.Objects.ModifyAttributes(guid, attr, true);
                           
                        }
                    }
                }
                else
                {
                    RhinoApp.WriteLine("This command only works on Line Form Diagrams!");
                    return Result.Failure;
                }


                doc.Views.Redraw();

                RhinoApp.WriteLine("The names of the edges of the provided form diagram have been updated to match the size of the force magnitude (face area) in the force diagram. ");


                return Result.Success;
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