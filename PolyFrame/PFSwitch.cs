using System;
using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using PolyFramework;
using Rhino.Input;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

namespace PolyFrame
{
    public class PFSwitch : Command
    {
        static PFSwitch _instance;
        public PFSwitch()
        {
            _instance = this;
        }

        ///<summary>The only instance of the PFSwitch command.</summary>
        public static PFSwitch Instance
        {
            get { return _instance; }
        }

        public override string EnglishName
        {
            get { return "PFSwitch"; }
        }


        // Switches the container representation of a PolyFrame 

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            



            var primal = new PFoam();
            var dual = new PFoam();

            
            try
            {

                var guids = LoadData.LoadPrimalDual(out primal, out dual, out ContainerType container);

                

                //primal.Show(true, true, true);


                // saving 


                if (!primal.SaveToDocument(out bool replace, container))
                {
                    primal.Hide();
                    return Result.Cancel;
                }

               

                primal.Hide();


                if (replace) Rhino.RhinoDoc.ActiveDoc.Objects.Delete(guids, true);

                doc.Views.Redraw();
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