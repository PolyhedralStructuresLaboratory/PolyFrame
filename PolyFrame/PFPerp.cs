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
    public class PFPerp : Command
    {
        static PFPerp _instance;
        public PFPerp()
        {
            _instance = this;
        }

        ///<summary>The only instance of the PFDual command.</summary>
        public static PFPerp Instance
        {
            get { return _instance; }
        }

        public override string EnglishName
        {
            get { return "PFPerp"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var primal = new PFoam();
            var dual = new PFoam();
            ContainerType container;

            try
            {



                var guids = LoadData.LoadPrimalDual(out primal, out dual, out container);

                if (dual.Cells.Count < 1)
                {
                    RhinoApp.WriteLine("No dual data retrieved from the container!");
                    return Result.Failure;
                }

                // Primal ( the perping geometry )
                // load the foam from the geometry option to update from the geometry if any changes occured 
                // select the structure to be perped - if dual not present raise error 

                // show a set of options 
                // Max steps 10 000
                // maximum deviation in degrees from dual direction
                // Update dual data from geometry 
                // set edge legth constraint
                // set point position constraint 

                var averageLen = primal.Edges.Select(x => x.GetLength()).Average();

                var getOptions = new GetOption();
                getOptions.SetDefaultString("enter");

                getOptions.SetCommandPrompt("Set max number of steps and perping options");

                var optionSteps = new OptionInteger(1000, true, 0);
                var optionMaxDev = new OptionDouble(0.1, 0.0, 10.0);
                var optionMinLen = new OptionDouble(averageLen / 4, true, averageLen / 100);
                var optionSetEdge = new OptionToggle(false, "setE", "E_set");
                var optionSetVert = new OptionToggle(false, "setV", "V_set");
                var optionSetDualGeo = new OptionToggle(false, "setG", "G_set");
                var storedSteps = 1000.0;
                var storedMaxDev = 0.1;
                var storedMinLen = averageLen / 4;
                bool storedEdgeSet = false;
                bool storedVertSet = false;
                bool storedDualGeoSet = false;

                getOptions.AddOptionInteger("MaxIterations", ref optionSteps);
                getOptions.AddOptionDouble("MaxDeviation", ref optionMaxDev);
                getOptions.AddOptionDouble("MinEdgeLenght", ref optionMinLen);
                getOptions.AddOptionToggle("SetEdgeLen", ref optionSetEdge);
                getOptions.AddOptionToggle("SetVertPos", ref optionSetVert);
                getOptions.AddOptionToggle("UpdatePrimalGeo", ref optionSetDualGeo);

                while (true)
                {
                    var preres = getOptions.Get();
                    var res = getOptions.CommandResult();
                    //var numres = getOptions.

                    if (res == Result.Success)
                    {
                        if (optionSetEdge.CurrentValue != storedEdgeSet)
                        {

                            primal.PickEdges();

                        }
                        if (optionSetVert.CurrentValue != storedVertSet)
                        {
                            primal.PickVertex();

                        }
                        if (optionSetDualGeo.CurrentValue != storedDualGeoSet)
                        {
                            var updatedDual = new PFoam();



                            LoadData.LoadPrimalDual(out updatedDual, out PFoam notUsedPrimal, out ContainerType dummyContainer ,false);
                            // get the container geometry from a dual from the document 
                            // check to see if the id matches 
                            // load the point geometry, centroids, meshes and update data in the dual 

                            // transform the primal/dual getter into a method to reuse it more easily

                            if (updatedDual.Dual.Id == primal.Id)
                            {


                                dual = updatedDual;
                                Util.ConnectDuals(ref primal, ref updatedDual);
                            }

                        }



                        if (optionSetEdge.CurrentValue == storedEdgeSet &&
                            optionSetVert.CurrentValue == storedVertSet &&
                            optionSetDualGeo.CurrentValue == storedDualGeoSet &&
                            optionSteps.CurrentValue == storedSteps &&
                            optionMaxDev.CurrentValue == storedMaxDev &&
                            optionMinLen.CurrentValue == storedMinLen
                            )
                        {
                            break;
                        }


                        storedEdgeSet = optionSetEdge.CurrentValue;
                        storedVertSet = optionSetVert.CurrentValue;
                        storedDualGeoSet = optionSetDualGeo.CurrentValue;
                        storedSteps = optionSteps.CurrentValue;
                        storedMaxDev = optionMaxDev.CurrentValue;
                        storedMinLen = optionMinLen.CurrentValue;


                    }
                    else
                    {
                        return Result.Cancel;
                    }
                }
                //Util.ConnectDuals(ref primal, ref dual);
                foreach (var edge in primal.Edges)
                {
                    edge.MinLength = optionMinLen.CurrentValue;
                }

                // delete the input objects 
                doc.Objects.Delete(guids, true);

                primal.PerpSoft(optionSteps.CurrentValue, optionMaxDev.CurrentValue / 180 * Math.PI);

                //primal.ParaPerp(optionSteps.CurrentValue, 0, optionMaxDev.CurrentValue / 180 * Math.PI, null, 1);


                primal.Show(true, true, false);



                var pepedPrimalJsonString = primal.SerializeJson();

                if (!primal.SaveToDocument(container))
                {
                    primal.Hide();
                    return Result.Cancel;
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