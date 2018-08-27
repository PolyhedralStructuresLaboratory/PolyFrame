using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace PolyFramework
{
    public partial class PFoam
    {

        /// <summary>
        /// Edge length smoothing routine. All edges will be resized based on specified parameters.
        /// Orientation will be kept within the supplied tolerance. 
        /// Edges will be resized inside the supplied foam object. 
        /// </summary>
        /// <param name="maxSteps">maximal number of steps for the algorithm</param>
        /// <param name="conVAngle">orientation maximal deviation in Radians</param>
        /// <param name="fixedVerts">set of vertices that will be kept fixed</param>
        /// <param name="type">type length constrain [True] = average with minus and plus fractional deviations, 
        /// [False] = absolute min max values to constrain edge length to </param>
        /// <param name="min">Average minus limit or absolute minimal length</param>
        /// <param name="max">Average plus limit or absolute maximal length</param>
        /// <param name="lengthConvForce">Force of length constrain [0..1] 0 loose no constrain, 1 max full constrain</param>
        public void EdgeSmoothing2(int maxSteps, double conVAngle = 0.017, IList<int> fixedVerts = null, bool type = true, double min = 0.0, double max = double.MaxValue, double lengthConvForce = 0.0)
        {
            // 
            // go through all th edges and disconnect points - create individual points for each edge end 
            // can just use the edges with the positive id and just reverse the information for the pairs 
            // only internal edges will be actively resized - the rest will be constructed 
            // external edges (semi) will just be reconstructed according to normal
            // external edges (full) will be reconstructed according to topology 

            HashSet<int> fixedVertHash = new HashSet<int>();
            if (fixedVerts != null)
            {
                foreach (var vertId in fixedVerts) fixedVertHash.Add(vertId);
            }

            Dictionary<int, PFVertex> originalVertex = Vertices.ToDictionary(x => x.Id, y => y);

            var intEdges = new List<PFEdge>();
            var halfExtEdges = new List<PFEdge>();
            var fullExtEdges = new List<PFEdge>();
            var switched = new List<PFEdge>();

            List<Line> origLineList = new List<Line>();
            List<Line> convergent = new List<Line>();
            List<Line> reSwitched = new List<Line>();

            List<Line> fullExtLines = new List<Line>();
            List<Line> halfExtLines = new List<Line>();
            List<Color> convergentColors = new List<Color>();




            // splitting the edges based on the interior / exterior / half    rule 
            foreach (var edge in Edges)
            {
                if (edge.Id > 0)
                {
                    if (edge.Vertices[0].External && edge.Vertices[1].External) fullExtEdges.Add(edge);
                    else if (edge.Vertices[0].External ^ edge.Vertices[1].External) halfExtEdges.Add(edge);
                    else intEdges.Add(edge);

                    origLineList.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));
                }
            }

            fullExtLines = fullExtEdges.Select(x => x.CreateLine()).ToList();
            halfExtLines = halfExtEdges.Select(x => x.CreateLine()).ToList();




            DrawPFLineConduit origLineConduit = new DrawPFLineConduit(origLineList, Color.Gray)
            {
                Enabled = true
            };


            var lineConduitConv = new DrawPFLinesConduit(convergent, convergentColors)
            {
                Enabled = true
            };
            var lineConduitSwitch = new DrawPFLineConduit(reSwitched, System.Drawing.Color.Red)
            {
                Enabled = true
            };

            var lineConduitFullExt = new DrawPFLineConduit(fullExtLines, System.Drawing.Color.White)
            {
                Enabled = true
            };

            var lineConduitHalfExt = new DrawPFLineConduit(halfExtLines, System.Drawing.Color.SpringGreen)
            {
                Enabled = true
            };


            Rhino.RhinoDoc.ActiveDoc.Views.Redraw();




            // this is for keeping score of the edge direction/length.
            Dictionary<PFEdge, bool> originalEdgeDir = intEdges.ToDictionary(edge => edge, edge => edge.OrientationToDual());
            Dictionary<PFEdge, Vector3d> originalEdgeVec = intEdges.ToDictionary(edge => edge, edge => edge.GetDirectionVector());
            Dictionary<PFEdge, double> originalEdgeLen = intEdges.ToDictionary(edge => edge, edge => edge.GetLength());
            Dictionary<PFEdge, double> edgeElongFactor = intEdges.ToDictionary(edge => edge, edge => 1.0);
            Dictionary<PFEdge, double> currentDeviation = intEdges.ToDictionary(edge => edge, edge => edge.AngleToDual()); ;
            double originalAverage = originalEdgeLen.Values.Average();
            double originalMin = originalEdgeLen.Values.Min();
            double originalMax = originalEdgeLen.Values.Max();

            // if smoothing type = absolute length min and max 

            double minLength = min;
            double maxLength = max;

            if (type) // if smoothing type = average +/- fraction
            {
                minLength = originalAverage - originalAverage * min;
                maxLength = originalAverage + originalAverage * max;
            }




            int counter = 0;

            while (true)
            {
                Dictionary<int, List<PFVertex>> expandedVertexes = new Dictionary<int, List<PFVertex>>();
                // this is for vertex expansion

                foreach (var edge in intEdges)
                {

                    var updatedEdgeVerts = new List<PFVertex>();
                    foreach (var vert in edge.Vertices)
                    {
                        PFVertex exp = new PFVertex(vert.Id, vert.Point);
                        updatedEdgeVerts.Add(exp);
                        var value = new List<PFVertex>(); // the placeholder for the dict value 
                        if (expandedVertexes.TryGetValue(vert.Id, out value))
                        {
                            value.Add(exp);
                        }
                        else
                        {
                            expandedVertexes.Add(vert.Id, new List<PFVertex>() { exp });
                        }
                    }
                    edge.Vertices = updatedEdgeVerts;
                }



                // set up a dict with all the lengths of the edges 
                var currentLen = intEdges.ToDictionary(e => e, e => e.GetLength());
                double currentMinLength = currentLen.Values.Min();
                double currentMaxLength = currentLen.Values.Max();
                var currentAverage = currentLen.Values.Average();




                foreach (var edge in intEdges)
                {

                    if (currentLen[edge] < minLength)
                    {
                        edgeElongFactor[edge] = (minLength * lengthConvForce + currentLen[edge] * (1 - lengthConvForce)) / currentLen[edge];
                    }
                    if (currentLen[edge] > maxLength)
                    {
                        edgeElongFactor[edge] = (maxLength * lengthConvForce + currentLen[edge] * (1 - lengthConvForce)) / currentLen[edge];
                    }


                }


                foreach (var edge in intEdges)
                {
                    edge.ScaleToDir(edge.GetDirectionVector(), edgeElongFactor[edge]);
                }


                // average the location of the original vertex points based on the list of values in the expanded dict

                // this is simple average

                foreach (var keyValPair in expandedVertexes)
                {
                    if (!fixedVertHash.Contains(keyValPair.Key))
                    {
                        originalVertex[keyValPair.Key].Point = PFVertex.AverageVertexes(keyValPair.Value);
                    }
                }

                // put the vertex back in the edges 

                for (int e = 0; e < intEdges.Count; e++)
                {
                    /////////////////////////////////////////////////////////////////////
                    intEdges[e].Vertices[0] = originalVertex[intEdges[e].Vertices[0].Id];
                    intEdges[e].Vertices[1] = originalVertex[intEdges[e].Vertices[1].Id];
                    ////////////////////////////////////////////////////////////////////
                }

                // test for  angle convergence 

                // for convergence max angle should be lower than threshold 



                var edgeAngles = new List<double>();
                double maxAngle = 0;
                int innerCounter = 0;


                while (true)
                {
                    innerCounter++;
                    // decompose again 
                    // rotate 
                    // recompose 
                    // test angle 
                    // if smaller exit while 

                    expandedVertexes = new Dictionary<int, List<PFVertex>>();
                    // this is for vertex expansion

                    foreach (var edge in intEdges)
                    {

                        var updatedEdgeVerts = new List<PFVertex>();
                        foreach (var vert in edge.Vertices)
                        {
                            PFVertex exp = new PFVertex(vert.Id, vert.Point);
                            updatedEdgeVerts.Add(exp);
                            var value = new List<PFVertex>(); // the placeholder for the dict value 
                            if (expandedVertexes.TryGetValue(vert.Id, out value))
                            {
                                value.Add(exp);
                            }
                            else
                            {
                                expandedVertexes.Add(vert.Id, new List<PFVertex>() { exp });
                            }
                        }
                        edge.Vertices = updatedEdgeVerts;
                    }

                    //rotating to original dir 
                    foreach (var edge in intEdges)
                    {
                        edge.ScaleToDir(originalEdgeVec[edge], 1);
                    }

                    // average the location of the original vertex points based on the list of values in the expanded dict

                    // this is simple average

                    foreach (var keyValPair in expandedVertexes)
                    {
                        if (!fixedVertHash.Contains(keyValPair.Key))
                        {
                            originalVertex[keyValPair.Key].Point = PFVertex.AverageVertexes(keyValPair.Value);
                        }
                    }

                    // put the vertex back in the edges 

                    for (int e = 0; e < intEdges.Count; e++)
                    {
                        /////////////////////////////////////////////////////////////////////
                        intEdges[e].Vertices[0] = originalVertex[intEdges[e].Vertices[0].Id];
                        intEdges[e].Vertices[1] = originalVertex[intEdges[e].Vertices[1].Id];
                        ////////////////////////////////////////////////////////////////////
                    }






                    convergent = new List<Line>();
                    convergentColors = new List<Color>();
                    reSwitched = new List<Line>();
                    currentDeviation = new Dictionary<PFEdge, double>();


                    // here the conduit lists are populated 



                    foreach (var edge in intEdges)
                    {
                        var angle = edge.AngleToDir(originalEdgeVec[edge]);
                        currentDeviation.Add(edge, angle);
                        if (angle > maxAngle) maxAngle = angle;




                        convergent.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));

                        // this is setting the color according to length deviation
                        convergentColors.Add(Util.LengthDeviationBlue(currentLen[edge], minLength, maxLength, originalMin, originalMax));


                        // this is setting the scale factor according to length and set interval (average or absolute length)
                        // the values for minLength and maxLength are set outside the while loop


                    }



                    // show all edges in this step 
                    // first rebuild the exterior edges - the exterior vertices 
                    // the halfExt edges with a positive id should have one internal and one external edge  

                    fullExtLines = new List<Line>();
                    halfExtLines = new List<Line>();

                    foreach (var edge in halfExtEdges)
                    {
                        //double len = edge.GetLength();
                        Vector3d lineVec = edge.Dual.Normal;
                        if (edge.Vertices[0].External)
                        {
                            edge.Vertices[1] = originalVertex[edge.Vertices[1].Id];

                            lineVec *= originalAverage;
                            edge.Vertices[0].Point = edge.Vertices[1].Point + lineVec;
                        }
                        else
                        {
                            edge.Vertices[0] = originalVertex[edge.Vertices[0].Id];
                            lineVec *= -originalAverage;
                            edge.Vertices[1].Point = edge.Vertices[0].Point + lineVec;

                        }
                    }

                    fullExtLines = fullExtEdges.Select(x => x.CreateLine()).ToList();
                    halfExtLines = halfExtEdges.Select(x => x.CreateLine()).ToList();


                    lineConduitConv.UpdateLines(convergent);
                    lineConduitConv.UpdateColors(convergentColors);
                    lineConduitSwitch.UpdateLines(reSwitched);
                    lineConduitFullExt.UpdateLines(fullExtLines);
                    lineConduitHalfExt.UpdateLines(halfExtLines);
                    Rhino.RhinoDoc.ActiveDoc.Views.Redraw();


                    if (maxAngle < conVAngle || innerCounter > 50)
                    {
                        break;
                    }

                }





                //System.Threading.Thread.Sleep(100);



                counter++;   //&& !someSwitched

                if ((maxAngle < conVAngle && currentMinLength >= minLength && currentMaxLength <= maxLength) || counter > maxSteps)
                {
                    // switch back all the reversed edges ....
                    // update conduits 

                    // there is no edge switching here .
                    // edges are kept in original direction by the scale operation 

                    foreach (var edge in intEdges)
                    {
                        edge.Deviation = currentDeviation[edge];
                    }


                    foreach (var face in Faces)
                    {
                        face.SetNormalToDual();
                    }


                    string refString = "";
                    Rhino.Input.RhinoGet.
                        GetString($"Convergence in achieved in {counter} steps. Max deviation is {Math.Round(maxAngle / Math.PI * 180, 2)} degrees. Minimum length is {currentMinLength}. Maximum length is {currentMaxLength}. Press enter to proceed.", true, ref refString);
                    MaxDeviation = maxAngle;


                    lineConduitConv.Enabled = false;
                    lineConduitSwitch.Enabled = false;
                    lineConduitFullExt.Enabled = false;
                    lineConduitHalfExt.Enabled = false;
                    origLineConduit.Enabled = false;
                    Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

                    break;
                }
                else
                {

                }



            }

        }

        /// <summary>
        /// Edge length smoothing routine. All edges will be resized based on specified parameters.
        /// Orientation will be kept within the supplied tolerance. 
        /// Edges will be resized inside the supplied foam object. 
        /// </summary>
        /// <param name="maxSteps">maximal number of steps for the algorithm</param>
        /// <param name="conVAngle">orientation maximal deviation in Radians</param>
        /// <param name="fixedVerts">set of vertices that will be kept fixed</param>
        /// <param name="type">type length constrain [True] = average with minus and plus fractional deviations, 
        /// [False] = absolute min max values to constrain edge length to </param>
        /// <param name="min">Average minus limit or absolute minimal length</param>
        /// <param name="max">Average plus limit or absolute maximal length</param>
        /// <param name="lengthConvForce">Force of length constrain [0..1] 0 loose no constrain, 1 max full constrain</param>
        public void SimpleSmoothing(int maxSteps, double conVAngle = 0.017, IList<int> fixedVerts = null)
        {
            // 
            // go through all th edges and disconnect points - create individual points for each edge end 
            // can just use the edges with the positive id and just reverse the information for the pairs 
            // only internal edges will be actively resized - the rest will be constructed 
            // external edges (semi) will just be reconstructed according to normal
            // external edges (full) will be reconstructed according to topology 

            HashSet<int> fixedVertHash = new HashSet<int>();
            if (fixedVerts != null)
            {
                foreach (var vertId in fixedVerts) fixedVertHash.Add(vertId);
            }

            Dictionary<int, PFVertex> originalVertex = Vertices.ToDictionary(x => x.Id, y => y);

            var intEdges = new List<PFEdge>();
            var halfExtEdges = new List<PFEdge>();
            var fullExtEdges = new List<PFEdge>();
            var switched = new List<PFEdge>();

            List<Line> origLineList = new List<Line>();
            List<Line> convergent = new List<Line>();
            List<Line> reSwitched = new List<Line>();

            List<Line> fullExtLines = new List<Line>();
            List<Line> halfExtLines = new List<Line>();
            List<Color> convergentColors = new List<Color>();




            // splitting the edges based on the interior / exterior / half    rule 
            foreach (var edge in Edges)
            {
                if (edge.Id > 0)
                {
                    if (edge.Vertices[0].External && edge.Vertices[1].External) fullExtEdges.Add(edge);
                    else if (edge.Vertices[0].External ^ edge.Vertices[1].External) halfExtEdges.Add(edge);
                    else intEdges.Add(edge);

                    origLineList.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));
                }
            }

            fullExtLines = fullExtEdges.Select(x => x.CreateLine()).ToList();
            halfExtLines = halfExtEdges.Select(x => x.CreateLine()).ToList();




            DrawPFLineConduit origLineConduit = new DrawPFLineConduit(origLineList, Color.Gray)
            {
                Enabled = true
            };


            var lineConduitConv = new DrawPFLinesConduit(convergent, convergentColors)
            {
                Enabled = true
            };
            var lineConduitSwitch = new DrawPFLineConduit(reSwitched, System.Drawing.Color.Red)
            {
                Enabled = true
            };

            var lineConduitFullExt = new DrawPFLineConduit(fullExtLines, System.Drawing.Color.White)
            {
                Enabled = true
            };

            var lineConduitHalfExt = new DrawPFLineConduit(halfExtLines, System.Drawing.Color.SpringGreen)
            {
                Enabled = true
            };


            Rhino.RhinoDoc.ActiveDoc.Views.Redraw();




            // this is for keeping score of the edge direction/length.
            Dictionary<PFEdge, bool> originalEdgeDir = intEdges.ToDictionary(edge => edge, edge => edge.OrientationToDual());
            Dictionary<PFEdge, Vector3d> originalEdgeVec = intEdges.ToDictionary(edge => edge, edge => edge.GetDirectionVector());
            Dictionary<PFEdge, double> originalEdgeLen = intEdges.ToDictionary(edge => edge, edge => edge.GetLength());
            Dictionary<PFEdge, double> edgeElongFactor = intEdges.ToDictionary(edge => edge, edge => 1.0);
            Dictionary<PFEdge, double> currentDeviation = intEdges.ToDictionary(edge => edge, edge => edge.AngleToDual()); ;

            double originalMin = originalEdgeLen.Values.Min();
            double originalMax = originalEdgeLen.Values.Max();
            double originalAverage = (originalMin + originalMax) / 2;
            // if smoothing type = absolute length min and max 






            int counter = 0;

            while (true)
            {
                Dictionary<int, List<PFVertex>> expandedVertexes = new Dictionary<int, List<PFVertex>>();
                // this is for vertex expansion

                foreach (var edge in intEdges)
                {

                    var updatedEdgeVerts = new List<PFVertex>();
                    foreach (var vert in edge.Vertices)
                    {
                        PFVertex exp = new PFVertex(vert.Id, vert.Point);
                        updatedEdgeVerts.Add(exp);
                        var value = new List<PFVertex>(); // the placeholder for the dict value 
                        if (expandedVertexes.TryGetValue(vert.Id, out value))
                        {
                            value.Add(exp);
                        }
                        else
                        {
                            expandedVertexes.Add(vert.Id, new List<PFVertex>() { exp });
                        }
                    }
                    edge.Vertices = updatedEdgeVerts;
                }



                // set up a dict with all the lengths of the edges 
                var currentLen = intEdges.ToDictionary(e => e, e => e.GetLength());
                double currentMinLength = currentLen.Values.Min();
                double currentMaxLength = currentLen.Values.Max();

                //var currentAverage = currentLen.Values.Average();

                double maxPositiveScaleDeviation = 0.0;
                double maxNegativeScaleDeviation = 0.0;

                // just set the scale factor to change the edge to the average
                foreach (var edge in intEdges)
                {
                    edgeElongFactor[edge] = originalAverage / currentLen[edge];
                }

                // do the scaling 
                foreach (var edge in intEdges)
                {
                    edge.ScaleToDir(edge.GetDirectionVector(), edgeElongFactor[edge]);
                }


                // average the location of the original vertex points based on the list of values in the expanded dict

                // this is simple average

                foreach (var keyValPair in expandedVertexes)
                {
                    if (!fixedVertHash.Contains(keyValPair.Key))
                    {
                        originalVertex[keyValPair.Key].Point = PFVertex.AverageVertexes(keyValPair.Value);
                    }
                }

                // put the vertex back in the edges 

                for (int e = 0; e < intEdges.Count; e++)
                {
                    /////////////////////////////////////////////////////////////////////
                    intEdges[e].Vertices[0] = originalVertex[intEdges[e].Vertices[0].Id];
                    intEdges[e].Vertices[1] = originalVertex[intEdges[e].Vertices[1].Id];
                    ////////////////////////////////////////////////////////////////////
                }
                currentLen = intEdges.ToDictionary(e => e, e => e.GetLength());
                currentMinLength = currentLen.Values.Min();
                currentMaxLength = currentLen.Values.Max();

                // test for  angle convergence 

                // for convergence max angle should be lower than threshold 



                var edgeAngles = new List<double>();
                double maxAngle = 0;
                int innerCounter = 0;


                while (true)
                {
                    switched = new List<PFEdge>();
                    innerCounter++;
                    // decompose again 
                    // rotate 
                    // recompose 
                    // test angle 
                    // if smaller exit while 

                    expandedVertexes = new Dictionary<int, List<PFVertex>>();
                    // this is for vertex expansion

                    foreach (var edge in intEdges)
                    {

                        var updatedEdgeVerts = new List<PFVertex>();
                        foreach (var vert in edge.Vertices)
                        {
                            PFVertex exp = new PFVertex(vert.Id, vert.Point);
                            updatedEdgeVerts.Add(exp);
                            var value = new List<PFVertex>(); // the placeholder for the dict value 
                            if (expandedVertexes.TryGetValue(vert.Id, out value))
                            {
                                value.Add(exp);
                            }
                            else
                            {
                                expandedVertexes.Add(vert.Id, new List<PFVertex>() { exp });
                            }
                        }
                        edge.Vertices = updatedEdgeVerts;
                    }

                    //rotating to original dir 
                    foreach (var edge in intEdges)
                    {
                        edge.ScaleToDir(originalEdgeVec[edge], 1);
                    }

                    // average the location of the original vertex points based on the list of values in the expanded dict

                    // this is simple average

                    // here also measure the distance between old point and new average (future point) 

                    double maxTravel = 0.0;

                    foreach (var keyValPair in expandedVertexes)
                    {
                        if (!fixedVertHash.Contains(keyValPair.Key))
                        {
                            var futurePoint = PFVertex.AverageVertexes(keyValPair.Value);
                            double travel = futurePoint.DistanceTo(originalVertex[keyValPair.Key].Point);
                            if (travel > maxTravel) maxTravel = travel;
                            originalVertex[keyValPair.Key].Point = futurePoint;
                        }
                    }

                    // put the vertex back in the edges 

                    for (int e = 0; e < intEdges.Count; e++)
                    {
                        /////////////////////////////////////////////////////////////////////
                        intEdges[e].Vertices[0] = originalVertex[intEdges[e].Vertices[0].Id];
                        intEdges[e].Vertices[1] = originalVertex[intEdges[e].Vertices[1].Id];
                        ////////////////////////////////////////////////////////////////////
                    }
                    currentLen = intEdges.ToDictionary(e => e, e => e.GetLength());
                    currentMinLength = currentLen.Values.Min();
                    currentMaxLength = currentLen.Values.Max();





                    convergent = new List<Line>();
                    convergentColors = new List<Color>();
                    reSwitched = new List<Line>();
                    currentDeviation = new Dictionary<PFEdge, double>();


                    // here the conduit lists are populated 


                    maxPositiveScaleDeviation = 0.0;
                    maxNegativeScaleDeviation = 0.0;
                    foreach (var edge in intEdges)
                    {
                        var angle = edge.AngleToDir(originalEdgeVec[edge]);
                        currentDeviation.Add(edge, angle);
                        if (angle > maxAngle) maxAngle = angle;




                        convergent.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));

                        // this is setting the color according to length deviation
                        var currentScaleDev = (currentLen[edge] - originalAverage) / originalAverage;
                        if (currentScaleDev > maxPositiveScaleDeviation) maxPositiveScaleDeviation = currentScaleDev;
                        if (currentScaleDev > maxNegativeScaleDeviation) maxNegativeScaleDeviation = currentScaleDev;
                        convergentColors.Add(Util.ScaleDeviationBlue(Math.Abs(currentScaleDev), 0.1, 0.8));


                        // this is setting the scale factor according to length and set interval (average or absolute length)
                        // the values for minLength and maxLength are set outside the while loop


                    }



                    // show all edges in this step 
                    // first rebuild the exterior edges - the exterior vertices 
                    // the halfExt edges with a positive id should have one internal and one external edge  

                    fullExtLines = new List<Line>();
                    halfExtLines = new List<Line>();

                    foreach (var edge in halfExtEdges)
                    {
                        //double len = edge.GetLength();
                        Vector3d lineVec = edge.Dual.Normal;
                        if (edge.Vertices[0].External)
                        {
                            edge.Vertices[1] = originalVertex[edge.Vertices[1].Id];

                            lineVec *= originalAverage;
                            edge.Vertices[0].Point = edge.Vertices[1].Point + lineVec;
                        }
                        else
                        {
                            edge.Vertices[0] = originalVertex[edge.Vertices[0].Id];
                            lineVec *= -originalAverage;
                            edge.Vertices[1].Point = edge.Vertices[0].Point + lineVec;

                        }
                    }

                    fullExtLines = fullExtEdges.Select(x => x.CreateLine()).ToList();
                    halfExtLines = halfExtEdges.Select(x => x.CreateLine()).ToList();


                    lineConduitConv.UpdateLines(convergent);
                    lineConduitConv.UpdateColors(convergentColors);
                    lineConduitSwitch.UpdateLines(reSwitched);
                    lineConduitFullExt.UpdateLines(fullExtLines);
                    lineConduitHalfExt.UpdateLines(halfExtLines);
                    Rhino.RhinoDoc.ActiveDoc.Views.Redraw();


                    if (maxAngle < conVAngle || maxTravel < originalAverage / 1000 || innerCounter > 100)
                    {
                        //switched = new List<PFEdge>();
                        foreach (var edge in intEdges)
                        {
                            if (originalEdgeDir[edge] != edge.OrientationToDual()) edge.SwitchPoints();

                        }



                        break;
                    }

                }




                counter++;   //&& !someSwitched

                if ((maxAngle < conVAngle && maxPositiveScaleDeviation < 2 && maxNegativeScaleDeviation > -.5) || counter > maxSteps)
                {
                    // switch back all the reversed edges ....
                    // update conduits 

                    // there is no edge switching here .
                    // edges are kept in original direction by the scale operation 

                    foreach (var edge in intEdges)
                    {
                        edge.Deviation = currentDeviation[edge];
                    }

                    foreach (var face in Faces)
                    {
                        face.SetNormalToDual();
                    }


                    string refString = "";
                    Rhino.Input.RhinoGet.
                        GetString($"Convergence in achieved in {counter} steps. Max deviation is {Math.Round(maxAngle / Math.PI * 180, 2)} degrees. Minimum length is {currentMinLength}. Maximum length is {currentMaxLength}. Press enter to proceed.", true, ref refString);
                    MaxDeviation = maxAngle;


                    lineConduitConv.Enabled = false;
                    lineConduitSwitch.Enabled = false;
                    lineConduitFullExt.Enabled = false;
                    lineConduitHalfExt.Enabled = false;
                    origLineConduit.Enabled = false;
                    Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

                    break;
                }
                else
                {

                }



            }

        }

        public void WavePerp()
        {
            // get the starting cell for the process
            // v1 - the cell closest to the foam centroid 
            // v2 - the exterior cells with reverse list at the end (this can produce multiple centers)
            // start wave segmentation from the start cell 
            // create lists of interior edges based on the cell segments 
            // loop through all the interior edge lists 
            //  - block the vertices that were part of the previous perp loop 
            //  - do perping for each edge in the list (while loop with limit condition for movement and angle)
            //      - if perped exit inner while loop 
            //      - else ??
            // use conduit to show edges - perping = blue; perped = gray; nextWaves = green

            //v1
            var cellCentroidToFoamCentroid = Cells.Select(x => x.Centroid.DistanceTo(Centroid)).ToList();
            int minIndex = cellCentroidToFoamCentroid.IndexOf(cellCentroidToFoamCentroid.Min());

            var cellSegments = CellPartition(new List<PFCell> { Cells[minIndex] });

            var halfExtEdges = new HashSet<PFEdge>();
            var fullExtEdges = new HashSet<PFEdge>();

            var lockedVertices = new List<PFVertex>();

            var originalVertex = Vertices.ToDictionary(x => x.Id, y => y);

            // create the line lists and color list for the conduits 

            List<Line> finishedLines = new List<Line>();
            List<Line> convergentLines = new List<Line>();

            List<Color> convergentColors = new List<Color>();


            // set up the conduits 
            var origLineConduit = new DrawPFLineConduit(finishedLines, Color.Gray)
            {
                Enabled = true
            };

            var lineConduitConv = new DrawPFLinesConduit(convergentLines, convergentColors)
            {
                Enabled = true
            };


            foreach (var cellList in cellSegments)
            {
                // get int edges 
                // first clear the int edges list
                List<PFEdge> intEdges = new List<PFEdge>();
                HashSet<PFEdge> preIntEdges = new HashSet<PFEdge>();
                foreach (var cell in cellList)
                {
                    foreach (var edge in cell.Edges)
                    {
                        if (edge.Id > 0)
                        {
                            if (!edge.Vertices[0].External && !edge.Vertices[1].External)
                            {
                                preIntEdges.Add(edge);
                            }
                            else if (!edge.Vertices[0].External ^ !edge.Vertices[1].External)
                            {
                                halfExtEdges.Add(edge);
                            }
                            else
                            {
                                fullExtEdges.Add(edge);
                            }
                        }
                    }
                }
                intEdges = preIntEdges.ToList();

                // this is for keeping score of the edge direction/length.
                Dictionary<PFEdge, bool> originalEdgeDir = intEdges.ToDictionary(edge => edge, edge => edge.OrientationToDual());
                Dictionary<PFEdge, double> originalEdgeLen = intEdges.ToDictionary(edge => edge, edge => edge.GetLength());
                Dictionary<PFEdge, double> edgeElongFactor = intEdges.ToDictionary(edge => edge, edge => 1.0);
                Dictionary<PFEdge, double> currentDeviation = intEdges.ToDictionary(edge => edge, edge => edge.AngleToDual()); ;
                double originalAverage = originalEdgeLen.Values.Average();
                var fixedVertHash = new HashSet<int>();

                int counter = 0;
                // this the perping loop
                while (true)
                {
                    // keeping score of the expanded vertices 
                    Dictionary<int, List<PFVertex>> expandedVertexes = new Dictionary<int, List<PFVertex>>();
                    // expanding vertices 
                    foreach (var edge in intEdges)
                    {

                        var updatedEdgeVerts = new List<PFVertex>();
                        foreach (var vert in edge.Vertices)
                        {
                            PFVertex exp = new PFVertex(vert.Id, vert.Point);
                            updatedEdgeVerts.Add(exp);
                            var value = new List<PFVertex>(); // the placeholder for the dict value 
                            var edgeCores = new List<PFEdge>(); // placeholder for the edge coreps dict value 
                            if (expandedVertexes.TryGetValue(vert.Id, out value))
                            {
                                edgeCores.Add(edge);
                            }
                            else
                            {
                                expandedVertexes.Add(vert.Id, new List<PFVertex>() { exp });
                            }
                        }
                        edge.Vertices = updatedEdgeVerts;
                    }

                    // the perping method 
                    foreach (var edge in intEdges)
                    {
                        edge.PerpEdge(originalEdgeDir[edge], edgeElongFactor[edge]);
                    }


                    // average the location of the original vertex points based on the list of values in the expanded dict
                    // this is simple average

                    foreach (var keyValPair in expandedVertexes)
                    {
                        if (!fixedVertHash.Contains(keyValPair.Key))
                        {
                            originalVertex[keyValPair.Key].Point = PFVertex.AverageVertexes(keyValPair.Value);
                        }
                    }

                    // put the vertex back in the edges 

                    for (int e = 0; e < intEdges.Count; e++)
                    {
                        /////////////////////////////////////////////////////////////////////
                        intEdges[e].Vertices[0] = originalVertex[intEdges[e].Vertices[0].Id];
                        intEdges[e].Vertices[1] = originalVertex[intEdges[e].Vertices[1].Id];
                        ////////////////////////////////////////////////////////////////////
                    }

                    // test for convergence 
                    // get all the angles to dual - get max 
                    // for convergence max angle should be lower than threshold 


                    var edgeAngles = new List<double>();
                    double maxAngle = 0.0;

                    // set up a dict with all the lengths of the edges 
                    var currentLen = intEdges.ToDictionary(e => e, e => e.GetLength());
                    var currentAverage = currentLen.Values.Average();
                    currentDeviation = new Dictionary<PFEdge, double>();

                    // here the conduit lists are populated 

                    convergentLines = new List<Line>();
                    convergentColors = new List<Color>();

                    foreach (var edge in intEdges)
                    {
                        var angle = edge.AngleToDual();
                        currentDeviation.Add(edge, angle);
                        if (angle > maxAngle) maxAngle = angle;

                        //if (angle > Math.PI / 2) edge.SwitchPoints();

                        convergentLines.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));
                        convergentColors.Add(Util.AngleDeviationBlue(currentDeviation[edge], .017));


                        // here constrain to original or average length is enforced 
                        // this is post-factum so it might not work properly 
                        // a dedicated resize loop might be necessary
                        edgeElongFactor[edge] = (currentLen[edge] + originalEdgeLen[edge]) / (2 * currentLen[edge]);



                    }

                    lineConduitConv.UpdateLines(convergentLines);
                    lineConduitConv.UpdateColors(convergentColors);


                    // test for loop exit conditions 
                    Rhino.RhinoDoc.ActiveDoc.Views.Redraw();


                    counter++;   //&& !someSwitched

                    if ((maxAngle < .017) || counter > 200)
                    {
                        // switch back all the reversed edges ....
                        // update conduits 

                        foreach (var edge in intEdges)
                        {

                            edge.Deviation = currentDeviation[edge];
                            // fix the points 
                            fixedVertHash = new HashSet<int>();
                            fixedVertHash.Add(edge.Vertices[0].Id);
                            fixedVertHash.Add(edge.Vertices[1].Id);
                        }


                        string refString = "";
                        Rhino.Input.RhinoGet.GetString($"Convergence in achieved in {counter} steps. Max deviation is {Math.Round(maxAngle / Math.PI * 180, 2)} degrees. Press enter to proceed.", true, ref refString);
                        MaxDeviation = maxAngle;


                        // clear convergent lines 
                        // add finished lines
                        finishedLines.AddRange(convergentLines);
                        convergentLines = new List<Line>();
                        convergentColors = new List<Color>();

                        lineConduitConv.UpdateLines(convergentLines);
                        origLineConduit.UpdateLines(finishedLines);
                        Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
                        break;
                    }

                }

                // need to rebuild the next wave of inner edges based on original directions .... or perp directions 
            }
            // show all edges in this step 
            // first rebuild the exterior edges - the exterior vertices 
            // the halfExt edges with a positive id should have one internal and one external edge  

            var fullExtLines = new List<Line>();
            var halfExtLines = new List<Line>();

            // now construct and eventually show the half/full exterior edges 
            double finalAverage = Edges.Select(x => x.GetLength()).Average();

            foreach (var edge in halfExtEdges)
            {
                //double len = edge.GetLength();
                Vector3d lineVec = edge.Dual.Normal;
                if (edge.Vertices[0].External)
                {
                    edge.Vertices[1] = originalVertex[edge.Vertices[1].Id];
                    //lineVec.Unitize();
                    lineVec *= finalAverage;
                    edge.Vertices[0].Point = edge.Vertices[1].Point + lineVec;
                }
                else
                {
                    edge.Vertices[0] = originalVertex[edge.Vertices[0].Id];
                    lineVec *= -finalAverage;
                    edge.Vertices[1].Point = edge.Vertices[0].Point + lineVec;

                }
            }

            fullExtLines = fullExtEdges.Select(x => x.CreateLine()).ToList();
            halfExtLines = halfExtEdges.Select(x => x.CreateLine()).ToList();



            lineConduitConv.Enabled = false;

            origLineConduit.Enabled = false;
            Rhino.RhinoDoc.ActiveDoc.Views.Redraw();






        }

        /// <summary>
        /// Makes all the edges of the dual perpendicular on the faces of the primal
        /// Tries to equalize the length of all the edges 
        /// Needs the dual to be stored in the edges
        /// </summary>
        public void RelaxPerp(int maxSteps, double maxImposedDeviation)
        {
            // 
            // go through all th edges and disconnect points - create individual points for each edge end 
            // can just use the edges with the positive id and just reverse the information for the pairs 
            // only internal edges will be actively perped 
            // external edges (semi) will just be reconstructed according to normal
            // external edges (full) will be reconstructed according to topology 



            Dictionary<int, PFVertex> originalVertex = Vertices.ToDictionary(x => x.Id, y => y);

            var intEdges = new List<PFEdge>();
            var halfExtEdges = new List<PFEdge>();
            var fullExtEdges = new List<PFEdge>();

            List<Line> origLineList = new List<Line>();
            List<Line> convergent = new List<Line>();
            List<Line> reSwitched = new List<Line>();

            List<Line> fullExtLines = new List<Line>();
            List<Line> halfExtLines = new List<Line>();
            List<Color> convergentColors = new List<Color>();




            // splitting the edges based on the interior / exterior / half    rule 
            foreach (var edge in Edges)
            {
                if (edge.Id > 0)
                {
                    if (edge.Vertices[0].External && edge.Vertices[1].External) fullExtEdges.Add(edge);
                    else if (edge.Vertices[0].External ^ edge.Vertices[1].External) halfExtEdges.Add(edge);
                    else intEdges.Add(edge);

                    origLineList.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));
                }
            }

            fullExtLines = fullExtEdges.Select(x => x.CreateLine()).ToList();
            halfExtLines = halfExtEdges.Select(x => x.CreateLine()).ToList();




            DrawPFLineConduit origLineConduit = new DrawPFLineConduit(origLineList, Color.Gray)
            {
                Enabled = true
            };


            var lineConduitConv = new DrawPFLinesConduit(convergent, convergentColors)
            {
                Enabled = true
            };
            var lineConduitSwitch = new DrawPFLineConduit(reSwitched, System.Drawing.Color.Red)
            {
                Enabled = true
            };

            var lineConduitFullExt = new DrawPFLineConduit(fullExtLines, System.Drawing.Color.White)
            {
                Enabled = true
            };

            var lineConduitHalfExt = new DrawPFLineConduit(halfExtLines, System.Drawing.Color.SpringGreen)
            {
                Enabled = true
            };


            Rhino.RhinoDoc.ActiveDoc.Views.Redraw();




            // this is for keeping score of the edge direction/length.
            Dictionary<PFEdge, bool> originalEdgeDir = intEdges.ToDictionary(edge => edge, edge => edge.OrientationToDual());
            Dictionary<PFEdge, double> originalEdgeLen = intEdges.ToDictionary(edge => edge, edge => edge.GetLength());
            Dictionary<PFEdge, double> edgeElongFactor = intEdges.ToDictionary(edge => edge, edge => 1.0);

            double originalAverage = originalEdgeLen.Values.Average();
            double originalMax = originalEdgeLen.Values.Max();
            double originalMin = originalEdgeLen.Values.Min();
            double maxOriginalDev = Math.Max((originalAverage - originalMin) / originalAverage, (originalMax - originalAverage) / originalAverage);

            int counter = 0;

            while (true)
            {
                Dictionary<int, List<PFVertex>> expandedVertexes = new Dictionary<int, List<PFVertex>>();
                // this is for vertex expansion
                foreach (var edge in intEdges)
                {



                    var updatedEdgeVerts = new List<PFVertex>();
                    foreach (var vert in edge.Vertices)
                    {
                        PFVertex exp = new PFVertex(vert.Id, vert.Point);
                        updatedEdgeVerts.Add(exp);
                        var value = new List<PFVertex>(); // the placeholder for the dict key 
                        if (expandedVertexes.TryGetValue(vert.Id, out value))
                        {
                            value.Add(exp);
                        }
                        else
                        {
                            expandedVertexes.Add(vert.Id, new List<PFVertex>() { exp });
                        }
                    }
                    edge.Vertices = updatedEdgeVerts;

                }


                foreach (var edge in intEdges)
                {
                    edge.PerpEdge(originalEdgeDir[edge], edgeElongFactor[edge]);
                }


                // average the location of the original vertex points based on the list of values in the expanded dict
                foreach (var keyValPair in expandedVertexes)
                {
                    originalVertex[keyValPair.Key].Point = PFVertex.AverageVertexes(keyValPair.Value);

                }

                // put the vertex back in the edges 

                for (int e = 0; e < intEdges.Count; e++)
                {
                    /////////////////////////////////////////////////////////////////////
                    intEdges[e].Vertices[0] = originalVertex[intEdges[e].Vertices[0].Id];
                    intEdges[e].Vertices[1] = originalVertex[intEdges[e].Vertices[1].Id];
                    ////////////////////////////////////////////////////////////////////
                }

                // test for convergence 
                // get all the angles to dual - get max 
                // for convergence max angle should be lower than threshold 


                var edgeAngles = new List<double>();
                double maxDeviation = 0.0;


                // set up a dict with all the lengths of the edges 
                var currentLen = intEdges.ToDictionary(e => e, e => e.GetLength());
                var currentAverage = currentLen.Values.Average();
                var currentDeviation = new Dictionary<PFEdge, double>();

                // here the conduit lists are populated 

                convergent = new List<Line>();
                convergentColors = new List<Color>();
                reSwitched = new List<Line>();

                foreach (var edge in intEdges)
                {
                    var dev = Math.Abs((currentLen[edge] - originalAverage) / originalAverage);
                    currentDeviation.Add(edge, dev);
                    if (dev > maxDeviation) maxDeviation = dev;


                    // if edge has switched - switch it back 
                    // take .Point from first PFVertex and switch with second PFVertex - this should change 
                    bool neworientation = edge.OrientationToDual();
                    if (neworientation != originalEdgeDir[edge])
                    {


                        reSwitched.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));
                    }
                    else
                    {

                        convergent.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));
                        convergentColors.Add(Util.ScaleDeviationBlue(currentDeviation[edge], maxImposedDeviation, maxOriginalDev));

                        //convergentColors.Add(Util.AngleDeviationBlue(currentDeviation[edge], conVAngle));


                    }

                    if (true)//(currentDeviation[edge] > maxImposedDeviation)
                    {
                        edgeElongFactor[edge] = originalAverage / currentLen[edge];
                    }



                }

                // show all edges in this step 
                // first rebuild the exterior edges - the exterior vertices 
                // the halfExt edges with a positive id should have one internal and one external edge  

                fullExtLines = new List<Line>();
                halfExtLines = new List<Line>();

                foreach (var edge in halfExtEdges)
                {
                    double len = edge.GetLength();
                    Vector3d lineVec = edge.Dual.Normal;
                    if (edge.Vertices[0].External)
                    {
                        edge.Vertices[1] = originalVertex[edge.Vertices[1].Id];
                        //lineVec.Unitize();
                        lineVec *= len;
                        edge.Vertices[0].Point = edge.Vertices[1].Point + lineVec;
                    }
                    else
                    {
                        edge.Vertices[0] = originalVertex[edge.Vertices[0].Id];
                        lineVec *= -len;
                        edge.Vertices[1].Point = edge.Vertices[0].Point + lineVec;

                    }
                }

                fullExtLines = fullExtEdges.Select(x => x.CreateLine()).ToList();
                halfExtLines = halfExtEdges.Select(x => x.CreateLine()).ToList();


                lineConduitConv.UpdateLines(convergent);
                lineConduitConv.UpdateColors(convergentColors);
                lineConduitSwitch.UpdateLines(reSwitched);
                lineConduitFullExt.UpdateLines(fullExtLines);
                lineConduitHalfExt.UpdateLines(halfExtLines);
                Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

                //System.Threading.Thread.Sleep(100);



                counter++;

                if ((maxDeviation <= maxImposedDeviation) || counter > maxSteps)
                {
                    string refString = "";
                    Rhino.Input.RhinoGet.GetString($"Convergence in achieved in {counter} steps. Max deviation is {Math.Round(maxDeviation, 5) * 100} %. Press enter to proceed.", true, ref refString);


                    lineConduitConv.Enabled = false;
                    lineConduitSwitch.Enabled = false;
                    lineConduitFullExt.Enabled = false;
                    lineConduitHalfExt.Enabled = false;
                    origLineConduit.Enabled = false;
                    Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
                    break;
                }
                else
                {

                }

            }

        }

        /// <summary>
        /// Tries to equalize all the edges in the foam by making them equal to the average 
        /// </summary>
        public void Relax(int maxSteps, double maxAllowedDeviation = 0.1)
        {
            // 
            // go through all th edges and disconnect points - create individual points for each edge end 
            // can just use the edges with the positive id and just reverse the information for the pairs 
            // only internal edges will be actively relaxed 
            // external edges (semi) will just be reconstructed according to normal
            // external edges (full) will be reconstructed according to topology 



            Dictionary<int, PFVertex> originalVertex = Vertices.ToDictionary(x => x.Id, y => y);

            var intEdges = new List<PFEdge>();
            var halfExtEdges = new List<PFEdge>();
            var fullExtEdges = new List<PFEdge>();



            List<Line> origLineList = new List<Line>();
            List<Line> convergent = new List<Line>();
            List<Line> reSwitched = new List<Line>();
            List<Line> extLines = new List<Line>();



            // put edges in the appropriate list 
            foreach (var edge in Edges)
            {
                if (edge.Id > 0)
                {
                    if (edge.Vertices[0].External && edge.Vertices[1].External) fullExtEdges.Add(edge);
                    else if (edge.Vertices[0].External ^ edge.Vertices[1].External) halfExtEdges.Add(edge);
                    else intEdges.Add(edge);

                    origLineList.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));
                }
            }



            // setting a visual reference for the user. 
            DrawPFLineConduit origLineConduit = new DrawPFLineConduit(origLineList, System.Drawing.Color.Gray)
            {
                Enabled = true
            };
            Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

            var lineConduitConv = new DrawPFLineConduit(convergent, System.Drawing.Color.Blue)
            {
                Enabled = true
            };
            var lineConduitSwitch = new DrawPFLineConduit(reSwitched, System.Drawing.Color.Red)
            {
                Enabled = true
            };

            var lineConduitExt = new DrawPFLineConduit(extLines, System.Drawing.Color.White)
            {
                Enabled = true
            };

            Rhino.RhinoDoc.ActiveDoc.Views.Redraw();




            // this is for keeping score of the edge direction/length.

            Dictionary<PFEdge, double> originalEdgeLen = intEdges.ToDictionary(edge => edge, edge => edge.GetLength());
            Dictionary<PFEdge, double> edgeElongFactor = intEdges.ToDictionary(edge => edge, edge => 1.0);
            double average = originalEdgeLen.Values.Average();



            int counter = 0;

            while (true)
            {
                Dictionary<int, List<PFVertex>> expandedVertexes = new Dictionary<int, List<PFVertex>>();
                // this is for vertex expansion
                foreach (var edge in intEdges)
                {
                    var updatedEdgeVerts = new List<PFVertex>();
                    foreach (var vert in edge.Vertices)
                    {
                        PFVertex exp = new PFVertex(vert.Id, vert.Point);
                        updatedEdgeVerts.Add(exp);
                        var value = new List<PFVertex>(); // the placeholder for the dict key 
                        if (expandedVertexes.TryGetValue(vert.Id, out value))
                        {
                            value.Add(exp);
                        }
                        else
                        {
                            expandedVertexes.Add(vert.Id, new List<PFVertex>() { exp });
                        }
                    }
                    edge.Vertices = updatedEdgeVerts;

                }


                //double average = originalEdgeLen.Values.Average();

                foreach (var edge in intEdges)
                {
                    double len = edge.GetLength();
                    //originalEdgeLen[edge] = len;
                    edge.ScaleEdge((average + len) / 2);


                }


                // average the location of the original vertex points based on the list of values in the expanded dict
                foreach (var keyValPair in expandedVertexes)
                {
                    originalVertex[keyValPair.Key].Point = PFVertex.AverageVertexes(keyValPair.Value);

                }

                // put the vertex back in the edges 

                for (int e = 0; e < intEdges.Count; e++)
                {
                    /////////////////////////////////////////////////////////////////////
                    intEdges[e].Vertices[0] = originalVertex[intEdges[e].Vertices[0].Id];
                    intEdges[e].Vertices[1] = originalVertex[intEdges[e].Vertices[1].Id];
                    ////////////////////////////////////////////////////////////////////
                }

                // test for convergence 
                // get all the angles to dual - get max 
                // for convergence max angle should be lower than threshold 



                double maxDeviation = 0.0;

                convergent = new List<Line>();
                reSwitched = new List<Line>();


                foreach (var edge in intEdges)
                {
                    var deviation = Math.Abs(edge.GetLength() - average) / average;
                    if (deviation > maxDeviation) maxDeviation = deviation;

                    if (deviation < maxAllowedDeviation)
                    {
                        convergent.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));
                    }
                    else reSwitched.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));

                }

                // show all edges in this step 
                // first rebuild the exterior edges - the exterior vertices 
                // the halfExt edges with a positive id should have one internal and one external edge  

                foreach (var edge in halfExtEdges)
                {
                    double len = edge.GetLength();
                    Vector3d lineVec = edge.Dual.Normal;
                    if (edge.Vertices[0].External)
                    {
                        edge.Vertices[1] = originalVertex[edge.Vertices[1].Id];
                        //lineVec.Unitize();
                        lineVec *= len;
                        edge.Vertices[0].Point = edge.Vertices[1].Point + lineVec;
                    }
                    else
                    {
                        edge.Vertices[0] = originalVertex[edge.Vertices[0].Id];
                        lineVec *= -len;
                        edge.Vertices[1].Point = edge.Vertices[0].Point + lineVec;

                    }
                }

                extLines = halfExtEdges.Concat(fullExtEdges)
                    .Select(x => new Line(x.Vertices[0].Point, x.Vertices[1].Point))
                    .ToList();


                lineConduitConv.UpdateLines(convergent);
                lineConduitSwitch.UpdateLines(reSwitched);
                lineConduitExt.UpdateLines(extLines);

                Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

                //System.Threading.Thread.Sleep(100);

                counter++;

                if (maxDeviation < maxAllowedDeviation || counter > maxSteps)
                {
                    string refString = "";
                    Rhino.Input.RhinoGet.GetString($"Convergence in achieved in {counter} steps. Deviation is now {maxDeviation * 100}% Press enter to proceed.", true, ref refString);


                    lineConduitConv.Enabled = false;
                    lineConduitSwitch.Enabled = false;
                    lineConduitExt.Enabled = false;
                    origLineConduit.Enabled = false;
                    Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
                    break;
                }
                else
                {

                }

            }

        }


        /// <summary>
        /// Perpendicularization of the edges based on the normal stored in the .Dual.Normal (face) 
        /// This is the parallelized version  
        /// </summary>
        /// <param name="maxSteps">maximum number of steps in the iteration</param>
        /// <param name="lengthConstrain">constrain factor for edge length 0.0 = no constrain | 1 = max constrain of edges to the length average</param>
        /// <param name="conVAngle">the maximum deviation for the edges from the prescribed direction in radian</param>
        /// <param name="fixedVerts">the list of ids for the vertices that will be kept still during perping</param>
        /// <param name="minLength">minimum length the edges can have during perping</param>
        /// <returns></returns>
        public IList<int> ParaPerp(int maxSteps, double lengthConstrain = 0.0, double conVAngle = 0.017, IList<int> fixedVerts = null, double minLength = 0.0)
        {
            // 
            // go through all th edges and disconnect points - create individual points for each edge end 
            // can just use the edges with the positive id and just reverse the information for the pairs 
            // only internal edges will be actively perped 
            // external edges (semi) will just be reconstructed according to normal
            // external edges (full) will be reconstructed according to topology 
            DateTime start = DateTime.Now;


            HashSet<int> fixedVertHash = new HashSet<int>();
            if (fixedVerts != null)
            {
                foreach (var vertId in fixedVerts) fixedVertHash.Add(vertId);
            }

            ConcurrentDictionary<int, PFVertex> originalVertex = new ConcurrentDictionary<int, PFVertex>();
            foreach (var vert in Vertices) originalVertex[vert.Id] = vert;

            var switched = new List<PFEdge>();
            var intEdges = new ConcurrentBag<PFEdge>();
            var halfExtEdges = new ConcurrentBag<PFEdge>();
            var fullExtEdges = new ConcurrentBag<PFEdge>();


            List<Line> origLineList = new List<Line>();

            var lineColors = new ConcurrentBag<Tuple<Line, Color>>();
            List<Line> fullExtLines = new List<Line>();
            List<Line> halfExtLines = new List<Line>();

            ConcurrentDictionary<int, ConcurrentBag<PFVertex>> expandedVertexes = new ConcurrentDictionary<int, ConcurrentBag<PFVertex>>();



            // splitting the edges based on the interior / exterior / half    rule 
            foreach (var edge in Edges)
            {
                if (edge.Id > 0)
                {
                    if (edge.Vertices[0].External && edge.Vertices[1].External) fullExtEdges.Add(edge);
                    else if (edge.Vertices[0].External ^ edge.Vertices[1].External) halfExtEdges.Add(edge);
                    else
                    {
                        intEdges.Add(edge);
                        foreach (var vert in edge.Vertices)
                        {
                            expandedVertexes[vert.Id] = new ConcurrentBag<PFVertex>();
                        }
                    }


                    origLineList.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));
                }
            }

            fullExtLines = fullExtEdges.Select(x => x.CreateLine()).ToList();
            halfExtLines = halfExtEdges.Select(x => x.CreateLine()).ToList();




            var origLineConduit = new DrawPFLineConduit(origLineList, Color.Gray)
            {
                Enabled = true
            };


            var lineConduitConv = new DrawPFLinesConduit(lineColors.Select(x => x.Item1).ToList(), lineColors.Select(x => x.Item2).ToList())
            {
                Enabled = true
            };


            var lineConduitFullExt = new DrawPFLineConduit(fullExtLines, System.Drawing.Color.White)
            {
                Enabled = true
            };

            var lineConduitHalfExt = new DrawPFLineConduit(halfExtLines, System.Drawing.Color.SpringGreen)
            {
                Enabled = true
            };


            Rhino.RhinoDoc.ActiveDoc.Views.Redraw();




            // this is for keeping score of the edge direction/length.
            Dictionary<PFEdge, bool> originalEdgeDir = intEdges.ToDictionary(edge => edge, edge => edge.OrientationToDual());
            Dictionary<PFEdge, double> originalEdgeLen = intEdges.ToDictionary(edge => edge, edge => edge.GetLength());
            ConcurrentDictionary<PFEdge, double> edgeElongFactor = new ConcurrentDictionary<PFEdge, double>();
            foreach (var edge in intEdges) edgeElongFactor[edge] = 1.0;
            //intEdges.ToDictionary(edge => edge, edge => 1.0);
            ConcurrentDictionary<PFEdge, double> currentDeviation = new ConcurrentDictionary<PFEdge, double>();
            foreach (var edge in intEdges) currentDeviation[edge] = edge.AngleToDual();


            double maxStartDev = currentDeviation.Values.Max();
            double originalAverage = originalEdgeLen.Values.Average();


            int counter = 0;

            while (true)
            {
                var keys = new List<int>(expandedVertexes.Keys);
                Parallel.ForEach(keys, key =>
                {
                    expandedVertexes[key] = new ConcurrentBag<PFVertex>();
                });
                // this is for vertex expansion
                //Dictionary<int, List<PFEdge>> expandedVertEdgeConnection = new Dictionary<int, List<PFEdge>>();
                // this holds the correspondence between expanded vertex and edge 

                Parallel.ForEach(intEdges, edge =>
                {
                    var updatedEdgeVerts = new List<PFVertex>();
                    foreach (var vert in edge.Vertices)
                    {
                        PFVertex exp = new PFVertex(vert.Id, vert.Point);
                        updatedEdgeVerts.Add(exp);
                        var value = new List<PFVertex>(); // the placeholder for the dict value 
                        expandedVertexes[vert.Id].Add(exp);

                    }
                    edge.Vertices = updatedEdgeVerts;
                });

                // the perping method 
                Parallel.ForEach(intEdges, edge =>
                {
                    edge.InterPerp(originalEdgeDir[edge], originalAverage * .1, edgeElongFactor[edge]);
                });




                // average the location of the original vertex points based on the list of values in the expanded dict
                // this is simple average


                Parallel.ForEach(expandedVertexes, keyValPair =>
                {
                    if (!fixedVertHash.Contains(keyValPair.Key))
                    {
                        originalVertex[keyValPair.Key].Point = PFVertex.AverageVertexes(keyValPair.Value.ToList());
                    }
                });


                // put the vertex back in the edges 
                Parallel.ForEach(intEdges, edge =>
                {
                    /////////////////////////////////////////////////////////////////////
                    edge.Vertices[0] = originalVertex[edge.Vertices[0].Id];
                    edge.Vertices[1] = originalVertex[edge.Vertices[1].Id];
                    ////////////////////////////////////////////////////////////////////
                });



                // test for convergence 
                // get all the angles to dual - get max 
                // for convergence max angle should be lower than threshold 

                double maxAngle = 0.0;


                // set up a dict with all the lengths of the edges 
                var currentLen = intEdges.ToDictionary(e => e, e => e.GetLength());
                var currentAverage = currentLen.Values.Average();
                currentDeviation = new ConcurrentDictionary<PFEdge, double>();

                // here the conduit lists are populated 
                lineColors = new ConcurrentBag<Tuple<Line, Color>>();

                Parallel.ForEach(intEdges, edge =>
                {
                    var angle = edge.AngleToDual();
                    currentDeviation[edge] = angle;
                    if (angle > maxAngle) maxAngle = angle;
                });

                Parallel.ForEach(intEdges, edge =>
                {
                    var lineColor = new Tuple<Line, Color>(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point),
                    Util.AngleDeviationBlue(currentDeviation[edge], conVAngle));
                    lineColors.Add(lineColor);

                    // here constrain to average length is enforced - based on coefficient entered
                    edgeElongFactor[edge] = (originalAverage * lengthConstrain + currentLen[edge] * (1 - lengthConstrain)) / currentLen[edge];
                    // here minimum length is enforced 
                    if (currentLen[edge] * edgeElongFactor[edge] < minLength)
                    {
                        edgeElongFactor[edge] = minLength / currentLen[edge];

                    }
                });



                // show all edges in this step 
                // first rebuild the exterior edges - the exterior vertices 
                // the halfExt edges with a positive id should have one internal and one external edge  
                if (((maxAngle > (maxStartDev / 5) && (counter % 5) == 0)) || (counter % 25) == 0) //
                {

                    fullExtLines = new List<Line>();
                    halfExtLines = new List<Line>();


                    Parallel.ForEach(halfExtEdges, edge =>
                    {
                        Vector3d lineVec = edge.Dual.Normal;
                        if (edge.Vertices[0].External)
                        {
                            edge.Vertices[1] = originalVertex[edge.Vertices[1].Id];
                            //lineVec.Unitize();
                            lineVec *= originalAverage;
                            edge.Vertices[0].Point = edge.Vertices[1].Point + lineVec;
                        }
                        else
                        {
                            edge.Vertices[0] = originalVertex[edge.Vertices[0].Id];
                            lineVec *= -originalAverage;
                            edge.Vertices[1].Point = edge.Vertices[0].Point + lineVec;

                        }
                    });


                    fullExtLines = fullExtEdges.Select(x => x.CreateLine()).ToList();
                    halfExtLines = halfExtEdges.Select(x => x.CreateLine()).ToList();


                    lineConduitConv.UpdateLines(lineColors.Select(x => x.Item1).ToList());
                    lineConduitConv.UpdateColors(lineColors.Select(x => x.Item2).ToList());
                    lineConduitFullExt.UpdateLines(fullExtLines);
                    lineConduitHalfExt.UpdateLines(halfExtLines);
                    Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
                }

                //System.Threading.Thread.Sleep(100);



                counter++;   //&& !someSwitched

                if ((maxAngle < conVAngle) || counter > maxSteps)
                {
                    // rebuild the full ext and half ext - for the end 
                    foreach (var edge in halfExtEdges)
                    {
                        //double len = edge.GetLength();
                        Vector3d lineVec = edge.Dual.Normal;
                        if (edge.Vertices[0].External)
                        {
                            edge.Vertices[1] = originalVertex[edge.Vertices[1].Id];
                            //lineVec.Unitize();
                            lineVec *= originalAverage;
                            edge.Vertices[0].Point = edge.Vertices[1].Point + lineVec;
                        }
                        else
                        {
                            edge.Vertices[0] = originalVertex[edge.Vertices[0].Id];
                            lineVec *= -originalAverage;
                            edge.Vertices[1].Point = edge.Vertices[0].Point + lineVec;

                        }
                    }



                    foreach (var edge in intEdges)
                    {
                        edge.Deviation = currentDeviation[edge];
                        if (edge.Deviation > Math.PI / 2) switched.Add(edge);
                    }
                    foreach (var face in Faces)
                    {
                        face.SetNormalToDual();
                    }


                    var elapsed = DateTime.Now - start;

                    string refString = "";
                    Rhino.Input.RhinoGet.GetString($"Convergence in achieved in {counter} steps and {(elapsed.TotalMilliseconds / 1000.0).ToString("####0.00")} seconds. Max deviation is {Math.Round(maxAngle / Math.PI * 180, 2)} degrees. Press enter to proceed.", true, ref refString);
                    MaxDeviation = maxAngle;


                    // update conduits 
                    lineConduitConv.Enabled = false;
                    lineConduitFullExt.Enabled = false;
                    lineConduitHalfExt.Enabled = false;
                    origLineConduit.Enabled = false;
                    Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
                    break;
                }

            }
            return switched.Select(x => x.Id).ToList();
        }
    }

}
