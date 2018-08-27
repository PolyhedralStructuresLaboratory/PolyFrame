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
        #region perpMethods
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
        public void EdgeSmoothing(int maxSteps, double conVAngle = 0.017, IList<int> fixedVerts = null, bool type = true, double min = 0.0, double max = double.MaxValue, double lengthConvForce = 0.0)
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
                Dictionary<int, List<PFEdge>> expandedVertEdgeConnection = new Dictionary<int, List<PFEdge>>();
                // this holds the correspondence between expanded vertex and edge 

                foreach (var edge in intEdges)
                {



                    var updatedEdgeVerts = new List<PFVertex>();
                    foreach (var vert in edge.Vertices)
                    {
                        PFVertex exp = new PFVertex(vert.Id, vert.Point);
                        updatedEdgeVerts.Add(exp);
                        var value = new List<PFVertex>(); // the placeholder for the dict value 
                        var edgeCores = new List<PFEdge>(); // placeholder for the edge coresp dict value 
                        if (expandedVertexes.TryGetValue(vert.Id, out value))
                        {
                            edgeCores = expandedVertEdgeConnection[vert.Id]; // if key exists in point dict is should be also in the edge correspondence dict 
                            value.Add(exp);
                            edgeCores.Add(edge);

                        }
                        else
                        {
                            expandedVertexes.Add(vert.Id, new List<PFVertex>() { exp });
                            expandedVertEdgeConnection.Add(vert.Id, new List<PFEdge>() { edge });
                        }
                    }
                    edge.Vertices = updatedEdgeVerts;

                }


                foreach (var edge in intEdges)
                {
                    edge.ScaleToDir(originalEdgeVec[edge], edgeElongFactor[edge]);
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

                // this is the angle weighted average
                // create a list of coefficents based on deviation 
                // just angle dev 1-(max(sin(angle/2 - maxAngle/2), sin(angle/2)) 
                // 1-sin(angle/2) - 
                // angle and lenght of edge 
                /*
                foreach (var keyValPair in expandedVertexes)
                {
                    if (!fixedVertHash.Contains(keyValPair.Key))
                    {
                        var vertexPositions = keyValPair.Value;
                        var vertexPositionEdgeDeviation = expandedVertEdgeConnection[keyValPair.Key].Select(x => currentDeviation[x]);
                        var vertexPositionCoeficient = vertexPositionEdgeDeviation.Select(x => x < conVAngle ? 10.0 : 1.0);
                        originalVertex[keyValPair.Key].Point = PFVertex.AverageVertexes(vertexPositions, vertexPositionCoeficient);

                        // or as one liner - just to show off :)
                        //originalVertex[keyValPair.Key].Point = PFVertex.AverageVertexes(keyValPair.Value, expandedVertEdgeConnection[keyValPair.Key].Select(x => 1 - Math.Sin(currentDeviation[x] /2)).ToList());
                    }
                }
                */


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


                //bool someSwitched = false;
                switched = new List<PFEdge>();

                // set up a dict with all the lengths of the edges 
                var currentLen = intEdges.ToDictionary(e => e, e => e.GetLength());
                double currentMinLength = currentLen.Values.Min();
                double currentMaxLength = currentLen.Values.Max();
                //var currentAverage = currentLen.Values.Average();
                currentDeviation = new Dictionary<PFEdge, double>();

                // here the conduit lists are populated 

                convergent = new List<Line>();
                convergentColors = new List<Color>();
                reSwitched = new List<Line>();

                foreach (var edge in intEdges)
                {
                    var angle = edge.AngleToDir(originalEdgeVec[edge]);
                    currentDeviation.Add(edge, angle);
                    if (angle > maxAngle) maxAngle = angle;


                    // if edge has switched - switch it back 
                    // take .Point from first PFVertex and switch with second PFVertex - this should change 
                    bool neworientation = edge.OrientationToDual();
                    if (neworientation != originalEdgeDir[edge])
                    {
                        //someSwitched = true;
                        switched.Add(edge);
                    }

                    convergent.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));

                    // this is setting the color according to length deviation
                    convergentColors.Add(Util.LengthDeviationBlue(currentLen[edge], minLength, maxLength, originalMin, originalMax));


                    // this is setting the scale factor according to length and set interval (average or absolute length)
                    // the values for minLength and maxLength are set outside the while loop
                    if (currentLen[edge] < minLength)
                    {
                        edgeElongFactor[edge] = (minLength * lengthConvForce + currentLen[edge] * (1 - lengthConvForce)) / currentLen[edge];
                    }
                    if (currentLen[edge] > maxLength)
                    {
                        edgeElongFactor[edge] = (maxLength * lengthConvForce + currentLen[edge] * (1 - lengthConvForce)) / currentLen[edge];
                    }
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

        public void SimpleSmoothing2(int maxSteps, double conVAngle = 0.017, IList<int> fixedVerts = null)
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
            double originalAverage = originalEdgeLen.Values.Average();
            // if smoothing type = absolute length min and max 






            int counter = 0;
            double minLenght = -1.0;
            double maxLength = -1.0;
            double maxDeviation = -1.0;
            while (true)
            {


                this.AlignEdges(intEdges, halfExtEdges, fullExtEdges, originalEdgeVec, conVAngle, originalEdgeLen,
                    100, originalAverage / 5000, fixedVertHash, out minLenght, out maxLength, out maxDeviation, originalAverage,
                    origLineConduit, lineConduitConv, lineConduitFullExt, lineConduitHalfExt);
                this.ScaleEdges(intEdges, halfExtEdges, fullExtEdges, originalEdgeVec, originalEdgeLen,
                    100, originalAverage / 5000, fixedVertHash, out minLenght, out maxLength, originalAverage,
                    origLineConduit, lineConduitConv, lineConduitFullExt, lineConduitHalfExt);


                counter++;   //&& !someSwitched

                if ((maxDeviation < conVAngle && minLenght > originalAverage * .5 && maxLength > originalAverage * 5) || counter > maxSteps)
                {
                    // switch back all the reversed edges ....
                    // update conduits 


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
                        GetString($"Convergence in achieved in {counter} steps. Max deviation is {Math.Round(maxDeviation / Math.PI * 180, 2)} degrees. Minimum length is {minLenght}. Maximum length is {maxLength}. Press enter to proceed.", true, ref refString);
                    MaxDeviation = maxDeviation;

                    lineConduitConv.Enabled = false;
                    lineConduitFullExt.Enabled = false;
                    lineConduitHalfExt.Enabled = false;
                    origLineConduit.Enabled = false;
                    Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
                    break;
                }
            }

        }


        /// <summary>
        /// The method is for internal use. 
        /// It takes care of an internal loop that scales the edges to the average 
        /// </summary>
        /// <param name="intEdges"></param>
        /// <param name="halfExtEdges"></param>
        /// <param name="fullExtEdges"></param>
        /// <param name="maxSteps"></param>
        /// <param name="limTravel"></param>
        /// <param name="fixedVerts"></param>
        /// <param name="minLength"></param>
        /// <param name="maxLength"></param>
        /// <param name="origLineConduit"></param>
        /// <param name="lineConduitConv"></param>
        /// <param name="lineConduitFullExt"></param>
        /// <param name="lineConduitHalfExt"></param>
        public void ScaleEdges(List<PFEdge> intEdges, List<PFEdge> halfExtEdges, List<PFEdge> fullExtEdges,
            Dictionary<PFEdge, Vector3d> originalEdgeVec, Dictionary<PFEdge, double> originalEdgeLen,
            int maxSteps, double limTravel, HashSet<int> fixedVertHash, out double minLength, out double maxLength, double originalAverage,
            DrawPFLineConduit origLineConduit, DrawPFLinesConduit lineConduitConv, DrawPFLineConduit lineConduitFullExt, DrawPFLineConduit lineConduitHalfExt)
        {

            // keeping score of the original/unique points 
            Dictionary<int, PFVertex> originalVertex = Vertices.ToDictionary(x => x.Id, y => y);

            // the lines for the conduits 
            List<Line> origLineList = new List<Line>();
            List<Line> convergent = new List<Line>();
            List<Line> reSwitched = new List<Line>();

            List<Line> fullExtLines = new List<Line>();
            List<Line> halfExtLines = new List<Line>();
            // colors for conduit 
            List<Color> convergentColors = new List<Color>();

            // keeping score of different things 

            Dictionary<PFEdge, double> edgeElongFactor = intEdges.ToDictionary(edge => edge, edge => 1.0);
            //Dictionary<PFEdge, double> currentDeviation = intEdges.ToDictionary(edge => edge, edge => edge.AngleToDual()); ;

            double originalMin = originalEdgeLen.Values.Min();
            double originalMax = originalEdgeLen.Values.Max();
            //double originalAverage = (originalMin + originalMax) / 2;

            // initial conduits for all edges

            Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

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


                //double maxPositiveScaleDeviation = 0.0;
                //double maxNegativeScaleDeviation = 0.0;

                // just set the scale factor to change the edge to the average
                foreach (var edge in intEdges)
                {
                    edgeElongFactor[edge] = originalAverage / currentLen[edge];
                }

                // do the scaling 
                foreach (var edge in intEdges)
                {
                    //edge.ScaleToDir(edge.GetDirectionVector(), edgeElongFactor[edge]);
                    edge.ScaleToDir(originalEdgeVec[edge], edgeElongFactor[edge]);
                }


                // average the location of the original vertex points based on the list of values in the expanded dict

                // this is simple average

                // also calculate travel of each point 

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
                // recalculate the current length
                currentLen = intEdges.ToDictionary(e => e, e => e.GetLength());
                currentMinLength = currentLen.Values.Min();
                currentMaxLength = currentLen.Values.Max();


                // update the conduits 

                convergent = new List<Line>();
                convergentColors = new List<Color>();
                reSwitched = new List<Line>();
                //currentDeviation = new Dictionary<PFEdge, double>();


                // here the conduit lists are populated 


                //maxPositiveScaleDeviation = 0.0;
                //maxNegativeScaleDeviation = 0.0;
                foreach (var edge in intEdges)
                {
                    convergent.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));

                    // this is setting the color according to length deviation
                    // this is measuring the scale deviation of each edge 
                    var currentScaleDev = (currentLen[edge] - originalAverage) / originalAverage;
                    //if (currentScaleDev > maxPositiveScaleDeviation) maxPositiveScaleDeviation = currentScaleDev;
                    //if (currentScaleDev > maxNegativeScaleDeviation) maxNegativeScaleDeviation = currentScaleDev;
                    convergentColors.Add(Util.ScaleDeviationBlue(Math.Abs(currentScaleDev), 0.1, 0.8));

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

                lineConduitFullExt.UpdateLines(fullExtLines);
                lineConduitHalfExt.UpdateLines(halfExtLines);
                Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

                //////////////////////////////////////////////
                if (maxTravel < limTravel || counter > maxSteps)
                {
                    minLength = currentMinLength;
                    maxLength = currentMaxLength;
                    break;
                }
                counter++;
            }

        }


        /// <summary>
        /// The method is for internal use. 
        /// It takes care of an internal loop that aligns the edges to the provided direction
        /// OriginalEdgeVec is the direction 
        /// </summary>
        /// <param name="intEdges"></param>
        /// <param name="halfExtEdges"></param>
        /// <param name="fullExtEdges"></param>
        /// <param name="maxSteps"></param>
        /// <param name="limTravel"></param>
        /// <param name="fixedVerts"></param>
        /// <param name="minLength"></param>
        /// <param name="maxLength"></param>
        /// <param name="origLineConduit"></param>
        /// <param name="lineConduitConv"></param>
        /// <param name="lineConduitFullExt"></param>
        /// <param name="lineConduitHalfExt"></param>
        internal void AlignEdges(List<PFEdge> intEdges, List<PFEdge> halfExtEdges, List<PFEdge> fullExtEdges,
            Dictionary<PFEdge, Vector3d> originalEdgeVec, double imposedDev, Dictionary<PFEdge, double> originalEdgeLen,
            int maxSteps, double limTravel, HashSet<int> fixedVertHash, out double minLength, out double maxLength, out double maxDeviation, double originalAverage,
            DrawPFLineConduit origLineConduit, DrawPFLinesConduit lineConduitConv, DrawPFLineConduit lineConduitFullExt, DrawPFLineConduit lineConduitHalfExt)
        {

            // keeping score of the original/unique points 
            Dictionary<int, PFVertex> originalVertex = Vertices.ToDictionary(x => x.Id, y => y);

            // the lines for the conduits 
            List<Line> origLineList = new List<Line>();
            List<Line> convergent = new List<Line>();
            List<Line> reSwitched = new List<Line>();

            List<Line> fullExtLines = new List<Line>();
            List<Line> halfExtLines = new List<Line>();
            // colors for conduit 
            List<Color> convergentColors = new List<Color>();

            // keeping score of different things 

            Dictionary<PFEdge, double> edgeElongFactor = intEdges.ToDictionary(edge => edge, edge => 1.0);
            Dictionary<PFEdge, double> currentDeviation = intEdges.ToDictionary(edge => edge, edge => edge.AngleToDual()); ;

            double originalMin = originalEdgeLen.Values.Min();
            double originalMax = originalEdgeLen.Values.Max();
            //double originalAverage = (originalMin + originalMax) / 2;


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


                //double maxPositiveScaleDeviation = 0.0;
                //double maxNegativeScaleDeviation = 0.0;

                // just test here if the edge is really small and set the scale factor 
                foreach (var edge in intEdges)
                {
                    if (currentLen[edge] < originalAverage * .02) edgeElongFactor[edge] = originalAverage * .02 / currentLen[edge];
                    else edgeElongFactor[edge] = 1;

                }

                // do the direction setting - no scale here  unless the edges is really small 
                foreach (var edge in intEdges)
                {
                    edge.ScaleToDir(originalEdgeVec[edge], edgeElongFactor[edge]);
                }


                // average the location of the original vertex points based on the list of values in the expanded dict

                // this is simple average

                // also calculate travel of each point 

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
                // recalculate the current length
                currentLen = intEdges.ToDictionary(e => e, e => e.GetLength());
                currentMinLength = currentLen.Values.Min();
                currentMaxLength = currentLen.Values.Max();


                // update the conduits 

                convergent = new List<Line>();
                convergentColors = new List<Color>();
                reSwitched = new List<Line>();
                currentDeviation = new Dictionary<PFEdge, double>();


                // here the conduit lists are populated 


                //maxPositiveScaleDeviation = 0.0;
                //maxNegativeScaleDeviation = 0.0;
                foreach (var edge in intEdges)
                {
                    convergent.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));

                    // this is setting the color according to length deviation
                    // this is measuring the scale deviation of each edge 
                    var currentScaleDev = (currentLen[edge] - originalAverage) / originalAverage;
                    currentDeviation[edge] = edge.AngleToDir(originalEdgeVec[edge]);

                    //if (currentScaleDev > maxPositiveScaleDeviation) maxPositiveScaleDeviation = currentScaleDev;
                    //if (currentScaleDev > maxNegativeScaleDeviation) maxNegativeScaleDeviation = currentScaleDev;
                    //convergentColors.Add(Util.ScaleDeviationBlue(Math.Abs(currentScaleDev), 0.1, 0.8));
                    // the colors are set based on angle deviation here  
                    convergentColors.Add(Util.DeviationToColorList(currentDeviation[edge], Math.PI / 2));

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

                lineConduitFullExt.UpdateLines(fullExtLines);
                lineConduitHalfExt.UpdateLines(halfExtLines);
                Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

                //////////////////////////////////////////////
                if (maxTravel < limTravel || counter > maxSteps)
                {
                    minLength = currentMinLength;
                    maxLength = currentMaxLength;
                    maxDeviation = currentDeviation.Values.Max();
                    break;
                }
                counter++;
            }

        }




        /// <summary>
        /// Perpendicularization of the edges based on the normal stored in the .Dual.Normal (face) 
        /// </summary>
        /// <param name="maxSteps">maximum number of steps in the iteration</param>
        /// <param name="lengthConstrain">constrain factor for edge length 0.0 = no constrain | 1 = max constrain of edges to the length average</param>
        /// <param name="conVAngle">the maximum deviation for the edges from the prescribed direction in radian</param>
        /// <param name="fixedVerts">the list of ids for the vertices that will be kept still during perping</param>
        /// <param name="minLength">minimum length the edges can have during perping</param>
        /// <returns></returns>
        public IList<int> Perp(int maxSteps, double lengthConstrain = 0.0, double conVAngle = 0.017, IList<int> fixedVerts = null, double minLength = 0.0, double dampenCoef = 0.1)
        {
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


            var origLineConduit = new DrawPFLineConduit(origLineList, Color.Gray)
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
            Dictionary<PFEdge, double> currentDeviation = intEdges.ToDictionary(edge => edge, edge => edge.AngleToDual());
            double maxStartDev = currentDeviation.Values.Max();
            double originalAverage = originalEdgeLen.Values.Average();


            int counter = 0;

            while (true)
            {
                Dictionary<int, List<PFVertex>> expandedVertexes = new Dictionary<int, List<PFVertex>>();
                // this is for vertex expansion
                //Dictionary<int, List<PFEdge>> expandedVertEdgeConnection = new Dictionary<int, List<PFEdge>>();
                // this holds the correspondence between expanded vertex and edge 

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
                            //edgeCores = expandedVertEdgeConnection[vert.Id]; // if key exists in point dict is should be also in the edge correspondence dict 
                            value.Add(exp);
                            edgeCores.Add(edge);

                        }
                        else
                        {
                            expandedVertexes.Add(vert.Id, new List<PFVertex>() { exp });
                            // expandedVertEdgeConnection.Add(vert.Id, new List<PFEdge>() { edge });
                        }
                    }
                    edge.Vertices = updatedEdgeVerts;

                }

                // the perping method 
                foreach (var edge in intEdges)
                {
                    edge.InterPerp(originalEdgeDir[edge], originalAverage * dampenCoef, edgeElongFactor[edge]);
                    //edge.PerpEdge(originalEdgeDir[edge], edgeElongFactor[edge]);
                }

                // average the location of the original vertex points based on the list of values in the expanded dict
                // this is simple average

                // all vertices that are in the fixetVert hash are ignored 
                foreach (var keyValPair in expandedVertexes)
                {
                    if (!fixedVertHash.Contains(keyValPair.Key))
                    {
                        originalVertex[keyValPair.Key].Point = PFVertex.AverageVertexes(keyValPair.Value);
                    }
                }

                // this is the angle weighted average
                // create a list of coefficients based on deviation 
                // just angle dev 1-(max(sin(angle/2 - maxAngle/2), sin(angle/2)) 
                // 1-sin(angle/2) - 
                // angle and length of edge 
                /*
                foreach (var keyValPair in expandedVertexes)
                {
                    if (!fixedVertHash.Contains(keyValPair.Key))
                    {
                        var vertexPositions = keyValPair.Value;
                        var vertexPositionEdgeDeviation = expandedVertEdgeConnection[keyValPair.Key].Select(x => currentDeviation[x]);
                        var vertexPositionCoeficient = vertexPositionEdgeDeviation.Select(x => x < conVAngle ? 10.0 : 1.0);
                        originalVertex[keyValPair.Key].Point = PFVertex.AverageVertexes(vertexPositions, vertexPositionCoeficient);

                        // or as one liner - just to show off :)
                        //originalVertex[keyValPair.Key].Point = PFVertex.AverageVertexes(keyValPair.Value, expandedVertEdgeConnection[keyValPair.Key].Select(x => 1 - Math.Sin(currentDeviation[x] /2)).ToList());
                    }
                }
                */


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
                //bool someSwitched = false;
                switched = new List<PFEdge>();

                // set up a dict with all the lengths of the edges 
                var currentLen = intEdges.ToDictionary(e => e, e => e.GetLength());
                var currentAverage = currentLen.Values.Average();
                currentDeviation = new Dictionary<PFEdge, double>();

                // here the conduit lists are populated 

                convergent = new List<Line>();
                convergentColors = new List<Color>();
                reSwitched = new List<Line>();

                foreach (var edge in intEdges)
                {
                    var angle = edge.AngleToDual();
                    currentDeviation.Add(edge, angle);
                    if (angle > maxAngle) maxAngle = angle;


                    // if edge has switched - switch it back 
                    // take .Point from first PFVertex and switch with second PFVertex - this should change 
                    bool neworientation = edge.OrientationToDual();
                    if (neworientation != originalEdgeDir[edge])
                    {
                        //---------- this is the reswitch 
                        //Point3d first = edge.Vertices[0].Point;
                        //Point3d second = edge.Vertices[1].Point;
                        //edge.Vertices[0].Point = second;
                        //edge.Vertices[1].Point = first;
                        // look at the elongation value in the dict - if already > 1 - double that 
                        //edgeElongFactor[edge] *= 2.0;
                        //someSwitched = true;
                        //reSwitched.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));
                        switched.Add(edge);
                    }
                    else
                    {
                        //edgeElongFactor[edge] = 1.0;

                    }
                    convergent.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));
                    convergentColors.Add(Util.AngleDeviationBlue(currentDeviation[edge], conVAngle));
                    /*
                    if (currentLen[edge] < originalAverage / 2)
                    {
                        edgeElongFactor[edge] = 1.4;
                    }
                    if (currentLen[edge] > originalAverage * 2)
                    {
                        edgeElongFactor[edge] = .7;
                    }
                    */

                    // here constrain to average length is enforced - based on coefficient entered
                    edgeElongFactor[edge] = (originalAverage * lengthConstrain + currentLen[edge] * (1 - lengthConstrain)) / currentLen[edge];
                    // here minimum length is enforced 
                    if (currentLen[edge] * edgeElongFactor[edge] < minLength)
                    {
                        edgeElongFactor[edge] = minLength / currentLen[edge];

                    }
                }

                // show all edges in this step 
                // first rebuild the exterior edges - the exterior vertices 
                // the halfExt edges with a positive id should have one internal and one external edge  
                if (((maxAngle > (maxStartDev / 5) && (counter % 5) == 0)) || (counter % 25) == 0) //
                {

                    fullExtLines = new List<Line>();
                    halfExtLines = new List<Line>();

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

                    fullExtLines = fullExtEdges.Select(x => x.CreateLine()).ToList();
                    halfExtLines = halfExtEdges.Select(x => x.CreateLine()).ToList();


                    lineConduitConv.UpdateLines(convergent);
                    lineConduitConv.UpdateColors(convergentColors);
                    lineConduitSwitch.UpdateLines(reSwitched);
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



                    // switch back all the reversed edges ....
                    // update conduits 
                    /*
                    foreach (var edge in switched)
                    {
                        edge.SwitchPoints();
                    }
                    */

                    foreach (var edge in intEdges)
                    {
                        edge.Deviation = currentDeviation[edge];
                    }
                    foreach (var face in Faces)
                    {
                        face.SetNormalToDual();
                    }


                    var elapsed = DateTime.Now - start;

                    string refString = "";
                    Rhino.Input.RhinoGet.GetString($"Convergence in achieved in {counter} steps and {(elapsed.TotalMilliseconds / 1000.0).ToString("####0.00")} seconds. Max deviation is {Math.Round(maxAngle / Math.PI * 180, 2)} degrees. Press enter to proceed.", true, ref refString);
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
            return switched.Select(x => x.Id).ToList();
        }


        /// <summary>
        /// Perpendicularization of the edges based on the normal stored in the .Dual.Normal (face) 
        /// </summary>
        /// <param name="maxSteps">maximum number of steps in the iteration</param>
        /// <param name="lengthConstrain">constrain factor for edge length 0.0 = no constrain | 1 = max constrain of edges to the length average</param>
        /// <param name="conVAngle">the maximum deviation for the edges from the prescribed direction in radian</param>
        /// <param name="fixedVerts">the list of ids for the vertices that will be kept still during perping</param>
        /// <param name="minLength">minimum length the edges can have during perping</param>
        /// <returns></returns>
        public IList<int> PerpSoft(int maxSteps, double conVAngle = 0.017)
        {
            // go through all th edges and disconnect points - create individual points for each edge end 
            // can just use the edges with the positive id and just reverse the information for the pairs 
            // only internal edges will be actively perped 
            // external edges (semi) will just be reconstructed according to normal
            // external edges (full) will be reconstructed according to topology 
            DateTime start = DateTime.Now;

            var escapeHandler = new EscapeKeyEventHandler("Pres ESC to stop");
            
            Dictionary<int, PFVertex> originalVertices = Vertices.ToDictionary(x => x.Id, y => y);

            var intEdges = new List<PFEdge>();
            var halfExtEdges = new List<PFEdge>();
            var fullExtEdges = new List<PFEdge>();
            var conFaces = new List<PFFace>();
            var conVerts = new List<PFVertex>();

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

            // extracting the constrained faces 
            // for now only area constraint
            foreach (var face in Faces)
            {
                if (!double.IsNaN(face.TargetArea)) conFaces.Add(face);
            }
            // extract all constrained points in a list
            // if no delegate declared as property 
            foreach (var vert in Vertices)
            {
                if (vert.RestrictPosition != null) conVerts.Add(vert);
            }


            // declare conduits 

            fullExtLines = fullExtEdges.Select(x => x.CreateLine()).ToList();
            halfExtLines = halfExtEdges.Select(x => x.CreateLine()).ToList();


            var origLineConduit = new DrawPFLineConduit(origLineList, Color.Gray)
            {
                //Enabled = true
            };


            var lineConduitConv = new DrawPFLinesConduit(convergent, convergentColors)
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
            //Dictionary<PFEdge, double> edgeElongFactor = intEdges.ToDictionary(edge => edge, edge => 1.0);
            Dictionary<PFEdge, double> currentDeviation = intEdges.ToDictionary(edge => edge, edge => edge.AngleToDual());
            double maxStartDev = currentDeviation.Values.Count > 0 ? currentDeviation.Values.Max() : 0.0 ;
            double originalAverage = originalEdgeLen.Values.Count > 0 ? originalEdgeLen.Values.Average() : halfExtEdges.Select(x => x.GetLength()).Average() ;


            int counter = 0;


            while (true)
            {
                Dictionary<int, List<PFVertex>> expandedVertexes = new Dictionary<int, List<PFVertex>>();
                // each original vertex gets a set of expanded vertexes - this is a list of vertex transformations (translations = new postions) resulted from element 'constraint'
                // transforming a face, edge or vertex - yields a set of new vertex proxies placed in a new position according to the rule of 'constraint' and tranformation 
                // each new proxy vertex (expanded vertex) is stored together with a weight value (influenceCoef) in the list 
                // at every cycle the list is averaged back into the original point and from there edges/faces 

                
                // go through all constrained faces - apply transformation to point 
                foreach (var face in conFaces)
                {
                    var areaVerts = face.SetArea();
                    foreach (var vrt in areaVerts)
                    {
                        if (expandedVertexes.TryGetValue(vrt.Id, out List<PFVertex> expVert))
                        {
                            expVert.Add(vrt);
                        }
                        else expandedVertexes[vrt.Id] = new List<PFVertex> { vrt };
                    }
                }

                // go through all the constrained modified edges => all edges 
                // the perping method 
                foreach (var edge in intEdges)
                {
                    var edgeVerts = edge.PerpScale_Soft();
                    foreach (var vrt in edgeVerts)
                    {
                        if (expandedVertexes.TryGetValue(vrt.Id, out List<PFVertex> expVert))
                        {
                            expVert.Add(vrt);
                        }
                        else expandedVertexes[vrt.Id] = new List<PFVertex> { vrt };
                    }
                    
                }

                // go through all the constrained vertices and apply the constraints 

                foreach (var vertex in conVerts)
                {
                    var vrt = vertex.RestrictPosition?.Invoke();
                    if (expandedVertexes.TryGetValue(vrt.Id, out List<PFVertex> expVert))
                    {
                        expVert.Add(vrt);
                    }
                    else expandedVertexes[vrt.Id] = new List<PFVertex> { vrt };
                }

                // average the location of the original vertex points based on the list of values in the expanded dict
                // this is weight average based on the influence value stored in the point ~
                var maxTurnTravel = 0.0;
                foreach (var keyValPair in expandedVertexes)
                {
                     
                    var newPoint = PFVertex.WeightAverageVertexes(keyValPair.Value);

                    var travel = newPoint.DistanceTo(originalVertices[keyValPair.Key].Point);
                    originalVertices[keyValPair.Key].Point = newPoint;
                    if (travel > maxTurnTravel)
                        maxTurnTravel = travel;


                }

                // put the vertex back in the edges is not necessary anymore 

                // test for convergence 
                // get all the angles to dual - get max 
                // for convergence max angle should be lower than threshold 


                var edgeAngles = new List<double>();
                double maxAngle = 0.0;
                //bool someSwitched = false;
                switched = new List<PFEdge>();

                // set up a dict with all the lengths of the edges 
                //var currentLen = intEdges.ToDictionary(e => e, e => e.GetLength());
                //var currentAverage = currentLen.Values.Average();
                currentDeviation = new Dictionary<PFEdge, double>();

                // here the conduit lists are populated 

                convergent = new List<Line>();
                convergentColors = new List<Color>();


                foreach (var edge in intEdges)
                {
                    var angle = edge.AngleToDual();
                    currentDeviation.Add(edge, angle);
                    if (angle > maxAngle) maxAngle = angle;


                    // if edge has switched - add to list --- ??

                    bool neworientation = edge.OrientationToDual();
                    if (neworientation != originalEdgeDir[edge])
                    {

                        switched.Add(edge);
                    }
                    convergent.Add(new Line(edge.Vertices[0].Point, edge.Vertices[1].Point));
                    convergentColors.Add(Util.AngleDeviationBlue(currentDeviation[edge], conVAngle));
                    ///////////////////////////////////////////////////////////////////////////////////////////
                    // Minimum, Maximum and average constraint are now enforced through the edge max,min values 
                    ///////////////////////////////////////////////////////////////////////////////////////////
                }

                // show all edges in this step 
                // first rebuild the exterior edges - the exterior vertices 
                // the halfExt edges with a positive id should have one internal and one external edge  
                if (((maxAngle > (maxStartDev / 5) && (counter % 5) == 0)) || (counter % 25) == 0) //
                {

                    fullExtLines = new List<Line>();
                    halfExtLines = new List<Line>();

                    foreach (var edge in halfExtEdges)
                    {
                        Vector3d lineVec = edge.Dual.Normal;
                        if (edge.Vertices[0].External)
                        {
                            edge.Vertices[1] = originalVertices[edge.Vertices[1].Id];

                            lineVec *= originalAverage;
                            edge.Vertices[0].Point = edge.Vertices[1].Point + lineVec;
                        }
                        else
                        {
                            edge.Vertices[0] = originalVertices[edge.Vertices[0].Id];
                            lineVec *= -originalAverage;
                            edge.Vertices[1].Point = edge.Vertices[0].Point + lineVec;

                        }
                    }

                    fullExtLines = fullExtEdges.Select(x => x.CreateLine()).ToList();
                    halfExtLines = halfExtEdges.Select(x => x.CreateLine()).ToList();


                    lineConduitConv.UpdateLines(convergent);
                    lineConduitConv.UpdateColors(convergentColors);

                    lineConduitFullExt.UpdateLines(fullExtLines);
                    lineConduitHalfExt.UpdateLines(halfExtLines);
                    Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
                }

                //System.Threading.Thread.Sleep(100);

                if ((counter % (Math.Floor(maxSteps / 100.0))) == 0)
                {
                    Rhino.RhinoApp.CommandPrompt = $"Perping in progress. Please wait... Or press <ESC> to interrupt. {Math.Round(counter / (double)maxSteps * 100.0)} % Done.Max deviation is {(maxAngle / Math.PI * 180).ToString("####0.0000")} degrees.";

                }

                counter++;  
                // TODO switch here to a stop criteria based movement of the points .... This can be helpful for decisions on strategy swithch 
                if ((maxAngle < conVAngle) || maxTurnTravel < originalAverage / 1e7  ||counter > maxSteps || escapeHandler.EscapeKeyPressed)
                {

                    foreach (var edge in intEdges)
                    {
                        edge.Deviation = currentDeviation[edge];
                    }
                    foreach (var face in Faces)
                    {
                        face.SetNormalToDual();
                    }
                    foreach (var face in Faces)
                    {
                        face.FaceMesh();
                    }

                    var elapsed = DateTime.Now - start;


                    string intro = "";

                    if (counter > maxSteps) intro += "Maximum iteration reached.";
                    else if (maxTurnTravel < originalAverage / 1e7) intro += "Stagnating solution.";
                    else if (maxAngle < conVAngle) intro += "Convergence achieved.";
                    else intro += "User interrupted iteration.";

                    string refString = "";
                    Rhino.Input.RhinoGet.GetString($"{intro} Stopped after {counter} steps and {(elapsed.TotalMilliseconds / 1000.0).ToString("####0.00")} seconds. Max deviation is {Math.Round(maxAngle / Math.PI * 180, 2)} degrees. Press enter to proceed.", true, ref refString);
                    MaxDeviation = maxAngle;


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





        public double Planarize(int maxSteps, double maxDev, IList<int> fixedVerts = null)
        {
            /* Go through all the faces of the foam
             * expand all vertices in a dict and put a surrogate in a new face  
             * Go through all the surrogate faces (with the expanded vertices)
             * Test for planarity - get best fit plane - project points to plane 
             * If face is planar - store plane - store point positions - if nothing moves (or is still within tolerance)
             * Just copy the data in the surrogate points
             * After a complete cycle - merge the data back into the points 
             * In in next cycle no face is planarized - exit loop 
             */

            DateTime start = DateTime.Now;
            var escapeHandler = new EscapeKeyEventHandler("Pres ESC to stop");
            HashSet<int> fixedVertHash = new HashSet<int>();
            if (fixedVerts != null)
            {
                foreach (var vertId in fixedVerts) fixedVertHash.Add(vertId);
            }

            var positiveFaces = Faces.Where(face => face.Id > 0 && face.Vertices.Count > 3).ToList(); // we just need one of the halfFaces
            var originalVertices = Vertices.ToDictionary(x => x.Id, y => y); // some storage for the original Vertices 
            var facePlanes = new Dictionary<int, Plane>(); // storage for the face plane - to see if it is changing
            var faceVertDev = new Dictionary<int, List<double>>(); // storage for plane deviation for each vertex
            var counter = 0;


            double maxAllDev = 0.0;

            /////
            var positiveHash = new HashSet<PFFace>(positiveFaces);

            var vertNonTriangularFaces = Vertices.ToDictionary(x => x.Id, x => x.Faces.Where(f => positiveFaces.Contains(f)).Count()); // number of non triangular faces for each vertex 
            var facePlanVerts = new Dictionary<int, List<int>>();
            foreach (var face in positiveFaces)
            {
                //var faceVerts = 
                var fVerts = face.Vertices.OrderByDescending(x => vertNonTriangularFaces[x.Id]).Select(y => y.Id).ToList(); 
                // order vertices in each face by number of non triangular faces connecting into them 
                // this allows the planarization algorithm to use the face plane that keeps these vertices fixed
                // this should (in theory) decrease the computation time 
                // Because it does not yield better results in practice it is not used right now 
                facePlanVerts[face.Id] = new List<int> { fVerts[0], fVerts[1], fVerts[2] };
                // only the first 3 are required to define the plane 
            }

            foreach (var face in positiveFaces)
            {
                //var fPlane = new Plane(originalVertices[facePlanVerts[face.Id][0]].Point,
                //                      originalVertices[facePlanVerts[face.Id][1]].Point,
                //                       originalVertices[facePlanVerts[face.Id][2]].Point);
                var fPlane = new Plane();

                Plane.FitPlaneToPoints(face.Vertices.Select(x => x.Point), out fPlane);
                facePlanes[face.Id] = fPlane;
                faceVertDev[face.Id] = new List<double>();
                foreach (var vert in face.Vertices)
                {
                    var dist = 0.00;
                    //if (!facePlanVerts[face.Id].Contains(vert.Id))
                    dist = Math.Abs(fPlane.DistanceTo(vert.Point));
                    faceVertDev[face.Id].Add(dist);
                }
                // set the maxAll deviation for later reference in point move  
                double faceMaxDev = faceVertDev[face.Id].Max();
                if (maxAllDev <= faceMaxDev) maxAllDev = faceMaxDev;

            }

            // here we set the false color for the mesh representation in the conduit 
            // deviation from the plane is the defining value 
            var meshes = new List<Mesh>();
            foreach (var face in positiveFaces)
            {
                face.FaceMesh();
                for (int i = 0; i < face.Vertices.Count; i++)
                {
                    var color = Util.DeviationToColorListGreenRed(faceVertDev[face.Id][i], maxAllDev);

                    face.FMesh.VertexColors.SetColor(i, color);
                }
                meshes.Add(face.FMesh);

            }

            var faceConduit = new DrawFalsePFMeshConduit(meshes)
            {
                Enabled = true
            };
            Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

            double maxTurnDev = 0;
            while (true)
            {
                var expandedVertices = new Dictionary<int, List<PFVertex>>();


                foreach (var face in positiveFaces)
                {
                    var updatedFaceVerts = new List<PFVertex>();
                    //var oriPlane = new Plane(face.Vertices[0].Point, face.Vertices[1].Point, face.Vertices[2].Point);
                    //facePlanes[face.Id] = oriPlane;
                    foreach (var vert in face.Vertices)
                    {
                        var expVert = new PFVertex(vert.Id, vert.Point);
                        updatedFaceVerts.Add(expVert);
                        var value = new List<PFVertex>();
                        var corespFaces = new List<PFFace>();

                        if (expandedVertices.TryGetValue(vert.Id, out value))
                        {

                            value.Add(expVert);
                            corespFaces.Add(face);

                        }
                        else
                        {
                            expandedVertices.Add(vert.Id, new List<PFVertex>() { expVert });
                        }
                    }

                    face.Vertices = updatedFaceVerts;
                }
                meshes = new List<Mesh>();


                // planarization or each face 
                bool facePlanarized = false;
                maxTurnDev = 0.0;
                foreach (var face in positiveFaces)
                {

                    //var fPlane = new Plane(originalVertices[facePlanVerts[face.Id][0]].Point,
                    //                  originalVertices[facePlanVerts[face.Id][1]].Point,
                    //                   originalVertices[facePlanVerts[face.Id][2]].Point);
                    var fPlane = new Plane();
                    Plane.FitPlaneToPoints(face.Vertices.Select(x => x.Point), out fPlane);
                    // closest plane to face 
                    var newPoints = new List<Point3d>();
                    for (var v = 0; v < face.Vertices.Count; v++)
                    {
                        // project all vertices to plane 
                        var newVertPoint = fPlane.ClosestPoint(face.Vertices[v].Point);
                        // compute deviation for each vertex/point
                        var vertDev = newVertPoint.DistanceTo(face.Vertices[v].Point);
                        // store deviation 
                        faceVertDev[face.Id][v] = vertDev;
                        // store point 
                        newPoints.Add(newVertPoint);
                        // update max deviation 
                        if (vertDev > maxTurnDev)
                            maxTurnDev = vertDev;
                    }
                    // if any vertex in the face is beyond the max deviation for planarity 
                    if (faceVertDev[face.Id].Any(x => x > maxDev)) 
                    {
                        for (var v = 0; v < face.Vertices.Count; v++)
                        {
                            if (faceVertDev[face.Id][v] > maxAllDev / 10)
                            {
                                // distance move for each vertex 
                                // using the maxAlldev as an transformation limit 
                                // not sure about this .... 
                                var distVert = newPoints[v] - face.Vertices[v].Point;
                                distVert.Unitize();
                                distVert *= maxAllDev / 10;

                                face.Vertices[v].Point += distVert;
                            }
                            else 
                                face.Vertices[v].Point = newPoints[v];
                            //face.FMesh.Vertices.SetVertex(v, newVertPoint);
                            // if face was transformed then also update the colors 
                            face.FMesh.VertexColors.SetColor(v, Util.DeviationToColorListGreenRed(faceVertDev[face.Id][v], maxAllDev));
                        }

                        //face.FaceMesh();
                        facePlanarized = true;
                        facePlanes[face.Id] = fPlane;
                    }
                    meshes.Add(face.FMesh); //.DuplicateMesh()


                }
                // visualization optimization - no need to show all solutions in the conduit
                if (maxTurnDev > maxAllDev / 5 && counter % 5 == 0)
                {
                    faceConduit.UpdateMeshes(meshes);
                    Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
                }




                // average the points back into the original 
                foreach (var keyValPair in expandedVertices)
                {
                    if (!fixedVertHash.Contains(keyValPair.Key))
                    {
                        originalVertices[keyValPair.Key].Point = PFVertex.AverageVertexes(keyValPair.Value);
                    }
                }
                // put the original points now averaged 
                foreach (var face in positiveFaces)
                {
                    for (int i = 0; i < face.Vertices.Count; i++)
                    {
                        face.Vertices[i] = originalVertices[face.Vertices[i].Id];

                        //face.FMesh.Vertices.SetVertex(i, originalVertices[face.Vertices[i].Id].Point);
                    }
                    face.FaceMesh();
                }
                if ((counter % (Math.Floor(maxSteps / 100.0))) == 0)
                {
                    Rhino.RhinoApp.CommandPrompt = $"Planarization in progress. Please wait... Or press <ESC> to interrupt. {Math.Round(counter / (double)maxSteps * 100.0)} % Done. Max deviation is {maxTurnDev.ToString("####0.000000")} units.";

                }


                counter++;
                //System.Threading.Thread.Sleep(100);

                if (counter > maxSteps || !facePlanarized || maxTurnDev < maxDev || escapeHandler.EscapeKeyPressed)
                {
                    var elapsed = DateTime.Now - start;
                    var intro = "";
                    string refString = "";
                    //Rhino.Input.RhinoGet.GetString($"Convergence in achieved in {counter} steps and {(elapsed.TotalMilliseconds / 1000.0).ToString("####0.00")} seconds. Max deviation is {maxTurnDev.ToString("####0.000")} units. Press enter to proceed.", true, ref refString);


                    if (counter > maxSteps) intro += "Maximum iteration reached.";
                    else if (maxTurnDev < maxDev) intro += "Deviation minimized.";
                    else if (!facePlanarized) intro += "Stagnating solution.";
                    else intro += "User interrupted iteration.";

                    Rhino.Input.RhinoGet.GetString($"{intro} Stopped after {counter} steps and {(elapsed.TotalMilliseconds / 1000.0).ToString("####0.00")} seconds. Max deviation is {maxTurnDev.ToString("####0.000000")} units. Press enter to proceed.", true, ref refString);



                    faceConduit.Enabled = false;
                    Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

                    foreach (var face in positiveFaces)
                    {
                        face.ComputeFaceNormal();
                        face.ComputeCentroid();
                        if (face.Pair != null)
                        {
                            face.Pair.Normal = -face.Normal;

                            face.Pair.Centroid = face.Centroid;
                        }
                    }


                    break;
                }


                
            }
            return maxTurnDev;

        }


        public double PlanarizeSoft(int maxSteps, double maxDev)
        {
            /* Go through all the faces of the foam
             * Scale and planarize using the face inbuilt method
             * The method outputs a duplicate set of face vertices (proxies). Those go into a dictionary (int, List<PFVertex))
             * Edges and vertices also can add to the dictionary based on their stored constrains 
             * At the end of the cycle the list for each vertex id is averaged (with weights) back into one value
             * Vertex move uses maxTravel
             * The main loop stops if no face was planarized (no more point movement occurs) or counter > maxSteps 
             */

            DateTime start = DateTime.Now;

            var escapeHandler = new EscapeKeyEventHandler("Pres ESC to stop");

            var positiveFaces = Faces.Where(face => face.Id > 0 && face.Vertices.Count > 3).ToList(); // we just need one of the halfFaces
            var constrainedVerts = Vertices.Where(x => x.RestrictPosition != null);
            var constrainedEdges = Edges.Where(x => !double.IsNaN(x.TargetLength) || x.MinLength > double.Epsilon || x.MaxLength < double.MaxValue);
            var originalVertices = Vertices.ToDictionary(x => x.Id, y => y); // some storage for the original Vertices 
            var facePlanes = new Dictionary<int, Plane>(); // storage for the face plane - to see if it is changing
            var faceVertDev = new Dictionary<int, List<double>>(); // storage for plane deviation for each vertex
            var counter = 0;


            double maxAllDev = 0.0;
            // getting the data for the initial representation in the conduit 
            foreach (var face in positiveFaces)
            {
                var fPlane = new Plane();

                Plane.FitPlaneToPoints(face.Vertices.Select(x => x.Point), out fPlane);
                facePlanes[face.Id] = fPlane;
                faceVertDev[face.Id] = new List<double>();
                foreach (var vert in face.Vertices)
                {
                    var dist = 0.00;
                    //if (!facePlanVerts[face.Id].Contains(vert.Id))
                    dist = Math.Abs(fPlane.DistanceTo(vert.Point));
                    faceVertDev[face.Id].Add(dist);
                }
                // set the maxAll deviation for later reference in point move  
                double faceMaxDev = faceVertDev[face.Id].Max();
                if (maxAllDev <= faceMaxDev) maxAllDev = faceMaxDev;

            }

            // here we set the false color for the mesh representation in the conduit 
            // deviation from the plane is the defining value 
            var meshes = new List<Mesh>();
            foreach (var face in positiveFaces)
            {
                face.FaceMesh();
                for (int i = 0; i < face.Vertices.Count; i++)
                {
                    var color = Util.DeviationToColorListGreenRed(faceVertDev[face.Id][i], maxAllDev);

                    face.FMesh.VertexColors.SetColor(i, color);
                }
                meshes.Add(face.FMesh);

            }

            var faceConduit = new DrawFalsePFMeshConduit(meshes)
            {
                Enabled = true
            };
            Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
            double maxTurnDev = 0;
            while (true)
            {
                // container for the vertices - here each face will add proxy vertices to be averaged 
                var expandedVertexes = new Dictionary<int, List<PFVertex>>();
                
                
                // planarization or each face 
                //bool facePlanarized = false;
                maxTurnDev = 0.0;
                foreach (var face in positiveFaces)
                {
                    var faceVerts = face.PlanarizeAndSetArea(maxDev, false);
                    //if (face.Planarized) facePlanarized = true;
                    //meshes.Add(face.FMesh); //.DuplicateMesh()
                    var maxFaceDev = face.VertPlanDeviations.Max();
                    if (maxFaceDev > maxTurnDev) maxTurnDev = maxFaceDev;

                    foreach (var vrt in faceVerts)
                    {
                        if (expandedVertexes.TryGetValue(vrt.Id, out List<PFVertex> expVert))
                        {
                            expVert.Add(vrt);
                        }
                        else
                            expandedVertexes[vrt.Id] = new List<PFVertex> { vrt };
                    }
                }

                foreach (var edge in constrainedEdges)
                {
                    var edgeVerts = edge.Scale_Soft();
                    foreach (var vrt in edgeVerts)
                    {
                        if (expandedVertexes.TryGetValue(vrt.Id, out List<PFVertex> expVert))
                        {
                            expVert.Add(vrt);
                        }
                        else
                            expandedVertexes[vrt.Id] = new List<PFVertex> { vrt };
                    }
                }

                foreach (var vertex in constrainedVerts)
                {
                    var vrt = vertex.RestrictPosition?.Invoke();
                    if (expandedVertexes.TryGetValue(vrt.Id, out List<PFVertex> expVert))
                    {
                        expVert.Add(vrt);
                    }
                    else
                        expandedVertexes[vrt.Id] = new List<PFVertex> { vrt };
                }



             





                // visualization optimization - no need to show all solutions in the conduit
                if (maxTurnDev > maxAllDev / 5 && counter % 5 == 0)
                {
                    faceConduit.UpdateMeshes(meshes);
                    Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
                }

                meshes = new List<Mesh>();


                // average the location of the original vertex points based on the list of values in the expanded dict
                // this is weight average based on the influence value stored in the point ~
                var maxTurnTravel = 0.0;
                foreach (var keyValPair in expandedVertexes)
                {
                    var newPoint = PFVertex.WeightAverageVertexes(keyValPair.Value);
                    var travel = newPoint.DistanceTo(originalVertices[keyValPair.Key].Point);
                   
                    originalVertices[keyValPair.Key].Point = newPoint;
                    if (travel > maxTurnTravel)
                        maxTurnTravel = travel;
                }
                // put the original points now averaged 

                foreach (var face in positiveFaces)
                {
                    face.FaceMesh();
                    for (int i = 0; i < face.Vertices.Count; i++)
                    {
                        var color = Util.DeviationToColorListGreenRed(face.VertPlanDeviations[i], maxAllDev);

                        face.FMesh.VertexColors.SetColor(i, color);
                    }
                    meshes.Add(face.FMesh);

                }


                //foreach (var face in positiveFaces)
                //{
                // mesh update 
                //     face.FaceMesh();
                //}


                if ((counter % (Math.Floor(maxSteps / 100.0))) == 0)
                {
                    Rhino.RhinoApp.CommandPrompt = $"Planarization in progress. Please wait... Or press <ESC> to interrupt. {Math.Round(counter / (double)maxSteps * 100.0)} % Done.Max deviation is {maxTurnDev.ToString("####0.000000")} units.";

                }

                counter++;
                //System.Threading.Thread.Sleep(100);

                if (counter > maxSteps || maxTurnTravel < maxDev || escapeHandler.EscapeKeyPressed) //|| maxTurnDev < maxDev   
                {
                    var elapsed = DateTime.Now - start;

                    string refString = "";

                    string intro = "";

                    if (counter > maxSteps) intro += "Maximum iteration reached.";
                    else if (maxTurnTravel < maxDev) intro += "Stagnating solution.";
                    else intro += "User interrupted iteration.";

                    Rhino.Input.RhinoGet.GetString($"{intro} Stopped after {counter} steps and {(elapsed.TotalMilliseconds / 1000.0).ToString("####0.00")} seconds. Max deviation is {maxTurnDev.ToString("####0.000000")} units. Press enter to proceed.", true, ref refString);

                    faceConduit.Enabled = false;
                    Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

                    foreach (var face in positiveFaces)
                    {
                        face.ComputeFaceNormal();
                        face.ComputeCentroid();

                        face.FaceMesh();
                        if (face.Pair != null)
                        {
                            face.Pair.Normal = -face.Normal;

                            face.Pair.Centroid = face.Centroid;

                            var meshDup = face.FMesh.DuplicateMesh();
                            meshDup.Flip(true, true, true);

                            face.Pair.FMesh = meshDup;
                        }
                    }


                    break;
                }

                
            }
            return maxTurnDev;

        }

        #endregion
    }
}
