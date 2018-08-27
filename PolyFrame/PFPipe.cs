using System;
using Rhino;
using Rhino.Commands;
using PolyFramework;
using System.Linq;
using System.Collections.Generic;

namespace PolyFrame
{
    public class PFPipe : Command
    {
        static PFPipe _instance;
        public PFPipe()
        {
            _instance = this;
        }

        ///<summary>The only instance of the PFPipe command.</summary>
        public static PFPipe Instance
        {
            get { return _instance; }
        }

        public override string EnglishName
        {
            get { return "PFPipe"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Get a polyframe 
            // Get a new point 
            // Create the pipes and lines in the new location 
            // the lines will be from an edge container of the PolyFrame 
            // The pipes will be added extra. This will allow for the pipes to be used as a PolyFrame 


            var primal = new PFoam();
            var dual = new PFoam();

            try
            {


                var guids = LoadData.LoadPrimalDual(out primal, out dual, out ContainerType container);


                if (primal.Cells.Count < 1)
                {
                    Rhino.RhinoApp.WriteLine("Error loading PolyFrame from provided data!");
                    return Result.Failure;
                }

                var form = primal.Edges.Where(e => e.Id > 0).Any(x => x.Faces.Count < 2);



                if (dual.Cells.Count < 1)
                {
                    RhinoApp.WriteLine("Could not load dual from the selected PolyFrame.");
                    return Result.Failure;
                }

                double minLen;
                double maxLen;

                var pipePolyFrame = new PFoam();
                if (form)
                {

                    pipePolyFrame = primal;
                }
                else
                {

                    pipePolyFrame = dual;
                }
                var edgeLengths = pipePolyFrame.Edges.Select(x => x.GetLength());

                minLen = edgeLengths.Min();
                maxLen = edgeLengths.Max();
                //var medLen = edgeLengths.Average();



                var gp = new Rhino.Input.Custom.GetPoint();
                var minRadius = new Rhino.Input.Custom.OptionDouble(minLen/10, true, double.Epsilon);
                var maxRadius = new Rhino.Input.Custom.OptionDouble(minLen/2, true, double.Epsilon);
                gp.SetCommandPrompt("Place the Pipes. Hit <Enter> to replace Input");
                gp.SetDefaultPoint(primal.Centroid);
                gp.AcceptPoint(true);
                gp.AddOptionDouble("MinRadius", ref minRadius);
                gp.AddOptionDouble("MaxRadius", ref maxRadius);


                while (true)
                {
                    gp.Get();
                    if (gp.Result() == Rhino.Input.GetResult.Point)
                    {
                        break;
                    }
                    else if (gp.Result() == Rhino.Input.GetResult.Cancel)
                    {
                        return Result.Failure;
                    }      
                }

                var moveVect = gp.Point() - pipePolyFrame.Centroid;
                pipePolyFrame.Offset(moveVect);

                if (gp.GotDefault())
                {
                    if (!form)
                    {
                        moveVect = primal.Centroid - dual.Centroid;
                    }
                    doc.Objects.Delete(guids, true);
                    doc.Views.Redraw();
                }


                var pipeLayer = new Rhino.DocObjects.Layer()
                {
                    Name = "_Pipes"
                };
                if (doc.Layers.All(x => x.Name != pipeLayer.Name)) doc.Layers.Add(pipeLayer);
                pipeLayer = doc.Layers.First(x => x.Name == "_Pipes");

                var pipeCol = pipePolyFrame.PipeGeoDual(minRadius.CurrentValue, maxRadius.CurrentValue, pipePolyFrame.Edges.Select(x => x.Id).ToList());
                var pipeGuids = new List<Guid>();
                foreach (var pc in pipeCol)
                {
                    var att = new Rhino.DocObjects.ObjectAttributes
                    {
                        
                        LayerIndex = pipeLayer.Index,
                        ObjectColor = pc.Item2,
                        ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject
                    };
                    pipeGuids.Add(doc.Objects.AddBrep(pc.Item1, att));
                }
                doc.Groups.Add(pipePolyFrame.Id.ToString() + "_Pipes", pipeGuids);

                pipePolyFrame.SaveAsEdges();

                doc.Views.Redraw();

            }

            catch (PolyFrameworkException pfE)
            {
                RhinoApp.WriteLine(pfE.Message);
                primal.Hide();
                dual.Hide();
                return Result.Failure;


            }


            return Result.Success;
        }
    }
}