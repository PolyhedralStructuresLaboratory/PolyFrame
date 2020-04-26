using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using Rhino.UI;
using System.Diagnostics;
using System.Windows.Forms;
using System;
using System.Drawing;
using System.Runtime.Serialization;
using System.IO;
using System.Web.Script.Serialization;
using static PolyFramework.Util;
using System.Threading;

namespace PolyFramework
{

    [Serializable]
    [DataContract]
    public partial class PFoam
    {
        [DataMember]
        public string Id { get; set; } = "";
        [DataMember]
        public IList<PFVertex> Vertices { get; set; } = new List<PFVertex>();
        [DataMember]
        public IList<PFEdge> Edges { get; set; } = new List<PFEdge>();
        [DataMember]
        public IList<PFFace> Faces { get; set; } = new List<PFFace>();
        [DataMember]
        public IList<PFCell> Cells { get; set; } = new List<PFCell>();
        /// <summary>
        /// The (sub)list of the vertexes outside of the exterior cell - this applies to the dual only
        /// </summary>
        /// 
        [DataMember]
        public IList<PFVertex> ExtVetices { get; set; } = new List<PFVertex>();
        /// <summary>
        /// The (sub)list of edges sticking out of the primer exterior cell - this applies to the dual only
        /// </summary>
        /// 
        [DataMember]
        public IList<PFEdge> ExtEdges { get; set; } = new List<PFEdge>();
        /// <summary>
        /// The (sub)list of incomplete faces sticking outside of the exterior primer cell - this applies to dual only
        /// </summary>
        /// 
        [DataMember]
        public IList<PFFace> ExtFaces { get; set; } = new List<PFFace>();
        [DataMember]
        public Point3d Centroid { get; set; } = Point3d.Unset;
        [DataMember]
        public double MaxDeviation { get; set; } = 0.0;
        public PFoam Dual { get; set; } = null;
        private DrawPFoamConduit DispConduit { get; set; } = null;
        private bool stopRequested = false;

        public PFoam()
        {
            if (new DateTime(year: 2020, month: 6, day: 30).CompareTo(DateTime.Now) < 0)
            {
                Rhino.RhinoApp.WriteLine("This version of PolyFrame has expired. Please download a new version from  https://psl.design.upenn.edu/polyframe/");
                throw new PolyFrameworkException("This version of PolyFrame has expired. Please download a new version from  https://psl.design.upenn.edu/polyframe/");
            }


            Id = Guid.NewGuid().ToString();
        }



        /// <summary>
        /// Create part of the hierarchy of the HalfFace data structure 
        /// </summary>
        /// <param name="bFaces">The input here should be single faced breps</param>
        /// <returns>Face objects </returns>
        public void ProcessFaces(IList<System.Guid> bGuids, double tollerance, double pTollerance)
        {

            var bFaces = Util.GetBrepfromGuids(bGuids);
            // for each item its ID should be the index in the pointCloud 
            if (bFaces.Any(x => x.Faces.Count != 1)) throw new PolyFrameworkException("Single Faces should be inputed");
            List<Brep> nonPlanarInput = new List<Brep>();
            List<System.Guid> nonPlanarGuids = new List<System.Guid>();
            //nonPlanarInput = bFaces.Where(x => x.Faces.Any(y => !y.IsPlanar(tollerance)));

            // make all non planar breps - red 




            for (int b = 0; b < bFaces.Count; b++)
            {
                if (!bFaces[b].Faces[0].IsPlanar(pTollerance))
                {
                    nonPlanarInput.Add(bFaces[b]);
                    nonPlanarGuids.Add(bGuids[b]);
                    // here create the new rhino object with new color 

                }
            }



            if (nonPlanarInput.Count() > 0)
            {

                foreach (var nonPGuid in nonPlanarGuids)
                {
                    var oldBrep = Rhino.RhinoDoc.ActiveDoc.Objects.Find(nonPGuid);
                    oldBrep.Attributes.ObjectColor = System.Drawing.Color.Red;
                    oldBrep.Attributes.ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject;
                    oldBrep.CommitChanges();
                    //oldBrep.Attributes.PlotColor = System.Drawing.Color.Red;
                    //oldBrep.Attributes.PlotColorSource = Rhino.DocObjects.ObjectPlotColorSource.PlotColorFromObject;
                    //Rhino.DocObjects.ObjRef objRef = new Rhino.DocObjects.ObjRef(nonPGuid);
                    //Rhino.RhinoDoc.ActiveDoc.Objects.Replace(objRef, oldBrep);

                }
                Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
                var mess = new PlanarWinForm();
                var result = Dialogs.ShowSemiModal(mess);


                if (result == DialogResult.Cancel)
                {

                    return;
                }


                // show the non planar faces 
                /*
                var brepConduit = new DrawPFBrepConduit(nonPlanarInput.ToList(), System.Drawing.Color.Red, 0.8)
                {
                    Enabled = true
                };
                Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
                //var buttons = MessageBoxButtons.YesNo;
                //var mess = new PlanarWinForm();
                //var result = Dialogs.ShowSemiModal(mess);
                //var result = Dialogs.ShowMessageBox("Some faces in your collection are non planar (shown in red) " +
                //    "Depending on the deviation the results could be compromised." +
                //    " Do you want to proceed anyway ?", "Non Planar Surfaces Detected", buttons, MessageBoxIcon.Error);
                brepConduit.Enabled = false;
                Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

                if (result == DialogResult.Cancel) return; 
                */
            }


            var faceCenterPC = new PointCloud();
            var edgeMidPC = new PointCloud();
            var vertPC = new PointCloud();



            var faces = new List<PFFace>();
            var edges = new List<PFEdge>();
            var verts = new List<PFVertex>();
            foreach (var brp in bFaces)
            {
                brp.Compact();

                // also need to test the created face is not denaturated - eq has more than 2 edges
                int denatCount = 0;
                foreach (var brepEdge in brp.Edges)
                {
                    if (brepEdge.GetLength() < tollerance) denatCount++;
                }
                if (brp.Edges.Count - denatCount < 3) continue;
                var bVerts = brp.DuplicateVertices();
                var centroid = new Point3d(x: bVerts.Select(c => c.X).Average(),
                    y: bVerts.Select(c => c.Y).Average(),
                    z: bVerts.Select(c => c.Z).Average());


                int closestFaceIndex = faceCenterPC.ClosestPoint(centroid);


                if (closestFaceIndex == -1 || faceCenterPC[closestFaceIndex].Location.DistanceTo(centroid) >= tollerance)
                // create new face 
                {
                    var face = new PFFace(faces.Count + 1) // here to avoid face.Id == 0
                    {
                        Centroid = centroid
                    };
                    faceCenterPC.Add(centroid);
                    faces.Add(face);
                    // now add all edges and vertices 
                    foreach (var bEdge in brp.Edges)
                    {
                        var crvEdge = bEdge.DuplicateCurve();
                        var midP = crvEdge.PointAtNormalizedLength(0.5);
                        int closestEdgeIndex = edgeMidPC.ClosestPoint(midP);
                        // test the edge to be new (mid point does not exist) or that the edge is not tiny 
                        if (crvEdge.GetLength() < tollerance) continue;
                        if (closestEdgeIndex == -1 || edgeMidPC[closestEdgeIndex].Location.DistanceTo(midP) >= tollerance)
                        {
                            var edge = new PFEdge(edges.Count + 1); // here to avoid edge.Id == 0
                            edgeMidPC.Add(midP);
                            edges.Add(edge);
                            face.Edges.Add(edge);
                            edge.Faces.Add(face);
                            // now get vertexes for edge
                            // v1 get ends of lines - test 
                            // v2 get vertexes from brep-edge keep score 

                            // v1
                            List<Point3d> ends = new List<Point3d>
                            {
                                crvEdge.PointAtStart,
                                crvEdge.PointAtEnd
                            };


                            foreach (var pt in ends)
                            {
                                int ptPtCloseIndex = vertPC.ClosestPoint(pt);
                                if (ptPtCloseIndex == -1 || vertPC[ptPtCloseIndex].Location.DistanceTo(pt) >= tollerance)
                                {
                                    var vert = new PFVertex(verts.Count, pt);
                                    vertPC.Add(pt);

                                    edge.Vertices.Add(vert);
                                    vert.Edges.Add(edge);

                                    if (!vert.Faces.Contains(face))
                                        vert.Faces.Add(face);
                                    //else Debug.Print($"Face {face.Id} is already contained in vertex {vert.Id}");


                                    if (!face.Vertices.Contains(vert))
                                        face.Vertices.Add(vert);
                                    //else Debug.Print($"Vertex {vert.Id} is already contained in Face {face.Id}");
                                    verts.Add(vert);
                                    // also add the edge and the face to the vertex 
                                }
                                else
                                {
                                    edge.Vertices.Add(verts[ptPtCloseIndex]);
                                    verts[ptPtCloseIndex].Edges.Add(edge);

                                    if (!face.Vertices.Contains(verts[ptPtCloseIndex]))
                                        face.Vertices.Add(verts[ptPtCloseIndex]);
                                    //else Debug.Print($"||||Face {face.Id} is already contained in vertex {verts[ptPtCloseIndex].Id}");
                                    if (!verts[ptPtCloseIndex].Faces.Contains(face))
                                        verts[ptPtCloseIndex].Faces.Add(face);
                                    // else Debug.Print($"|||Vertex {verts[ptPtCloseIndex].Id} is already contained in Face {face.Id}");
                                    // also add the edge and the face to the vertex 
                                }
                            }
                            edge.CreatePair();

                        }
                        else
                        {
                            face.Edges.Add(edges[closestEdgeIndex]);
                            foreach (var vert in edges[closestEdgeIndex].Vertices)
                            {
                                if (!face.Vertices.Contains(vert))
                                    face.Vertices.Add(vert);
                                if (!vert.Faces.Contains(face))
                                    vert.Faces.Add(face);
                            }
                            edges[closestEdgeIndex].Faces.Add(face);
                        }
                    }
                }
                //here 


            }

            //var foam = new PFoam()

            // here test for faces with only 2 edges 




            Vertices = new List<PFVertex>(verts);
            Edges = new List<PFEdge>(edges);


            Faces = new List<PFFace>(faces);


        }

        /// <summary>
        /// Create part of the hierarchy of the HalfFace data structure 
        /// </summary>
        /// <param name="bFaces">The input here should be single faced breps</param>
        /// <returns>Face objects </returns>
        public void ProcessBFaces(IList<Brep> breps, double tollerance, double pTollerance)
        {
            //var bFaces = Util.Decompose(breps); // this is no longer needed the translation method also decomposes breps 
            var bFaces = breps.ToList();

            if (bFaces.Any(x => x.Faces.Count != 1)) throw new PolyFrameworkException("Single Faces should be inputed");
            List<Brep> nonPlanarInput = new List<Brep>();
            List<Brep> degenerateInput = new List<Brep>();
            //List<System.Guid> nonPlanarGuids = new List<System.Guid>();
            //nonPlanarInput = bFaces.Where(x => x.Faces.Any(y => !y.IsPlanar(tollerance)));

            // make all non planar breps - red 




            for (int b = 0; b < bFaces.Count; b++)
            {
                if (bFaces[b].Vertices.Count < 3)
                {
                    degenerateInput.Add(bFaces[b]);

                }

                if (!bFaces[b].Faces[0].IsPlanar(pTollerance))
                {
                    double dev = Util.BFacePlanaritiy(bFaces[b]);
                    if (dev > pTollerance)
                        nonPlanarInput.Add(bFaces[b]);

                    //nonPlanarGuids.Add(bGuids[b]);
                    // here create the new rhino object with new color 

                }
            }

            if (degenerateInput.Count > 0)
            {
                BakeErrorGeo(degenerateInput.Select(x => (GeometryBase)x), degenerateInput.Select(x => "Degenerate_Face"));
                throw new PolyFrameworkException("There are degenerate faces in the input ! Less than 3 unique vertices.");
            }



            if (nonPlanarInput.Count() > 0)
            {
                // here non planarity for GH logic will be implemented 
                BakeErrorGeo(nonPlanarInput.Select(x => (GeometryBase)x), nonPlanarInput.Select(x => "NonPlanar_Face"));
                throw new PolyFrameworkException($"{nonPlanarInput.Count} faces are not planar to provided planar tolerance. Fix brep input or increase tolerance ");

            }


            var faceCenterPC = new PointCloud();
            var edgeMidPC = new PointCloud();
            var vertPC = new PointCloud();



            var faces = new List<PFFace>();
            var edges = new List<PFEdge>();
            var verts = new List<PFVertex>();
            foreach (var brp in bFaces)
            {
                //brp.Compact(); // this in not needed I think 

                // also need to test the created face is not denaturated - eq has more than 2 edges
                int denatCount = 0;
                foreach (var brepEdge in brp.Edges)
                {
                    if (brepEdge.GetLength() < tollerance) denatCount++;
                }
                if (brp.Edges.Count - denatCount < 3) continue;
                var bVerts = brp.DuplicateVertices();
                var centroid = new Point3d(x: bVerts.Select(c => c.X).Average(),
                    y: bVerts.Select(c => c.Y).Average(),
                    z: bVerts.Select(c => c.Z).Average());


                int closestFaceIndex = faceCenterPC.ClosestPoint(centroid);


                if (closestFaceIndex == -1 || faceCenterPC[closestFaceIndex].Location.DistanceTo(centroid) >= tollerance)
                // create new face 
                {
                    var face = new PFFace(faces.Count + 1) // here to avoid face.Id == 0
                    {
                        Centroid = centroid
                    };
                    faceCenterPC.Add(centroid);
                    faces.Add(face);
                    // now add all edges and vertices 
                    foreach (var bEdge in brp.Edges)
                    {
                        var crvEdge = bEdge.DuplicateCurve();
                        var midP = crvEdge.PointAtNormalizedLength(0.5);
                        int closestEdgeIndex = edgeMidPC.ClosestPoint(midP);
                        // test the edge to be new (mid point does not exist) or that the edge is not tiny 
                        if (crvEdge.GetLength() < tollerance) continue;
                        if (closestEdgeIndex == -1 || edgeMidPC[closestEdgeIndex].Location.DistanceTo(midP) >= tollerance)
                        {
                            var edge = new PFEdge(edges.Count + 1); // here to avoid edge.Id == 0
                            edgeMidPC.Add(midP);
                            edges.Add(edge);
                            face.Edges.Add(edge);
                            edge.Faces.Add(face);
                            // now get vertexes for edge
                            // v1 get ends of lines - test 
                            // v2 get vertexes from brep-edge keep score 

                            // v1
                            List<Point3d> ends = new List<Point3d>
                            {
                                crvEdge.PointAtStart,
                                crvEdge.PointAtEnd
                            };


                            foreach (var pt in ends)
                            {
                                int ptPtCloseIndex = vertPC.ClosestPoint(pt);
                                if (ptPtCloseIndex == -1 || vertPC[ptPtCloseIndex].Location.DistanceTo(pt) >= tollerance)
                                {
                                    var vert = new PFVertex(verts.Count, pt);
                                    vertPC.Add(pt);

                                    edge.Vertices.Add(vert);
                                    vert.Edges.Add(edge);

                                    if (!vert.Faces.Contains(face))
                                        vert.Faces.Add(face);
                                    //else Debug.Print($"Face {face.Id} is already contained in vertex {vert.Id}");


                                    if (!face.Vertices.Contains(vert))
                                        face.Vertices.Add(vert);
                                    //else Debug.Print($"Vertex {vert.Id} is already contained in Face {face.Id}");
                                    verts.Add(vert);
                                    // also add the edge and the face to the vertex 
                                }
                                else
                                {
                                    edge.Vertices.Add(verts[ptPtCloseIndex]);
                                    verts[ptPtCloseIndex].Edges.Add(edge);

                                    if (!face.Vertices.Contains(verts[ptPtCloseIndex]))
                                        face.Vertices.Add(verts[ptPtCloseIndex]);
                                    //else Debug.Print($"||||Face {face.Id} is already contained in vertex {verts[ptPtCloseIndex].Id}");
                                    if (!verts[ptPtCloseIndex].Faces.Contains(face))
                                        verts[ptPtCloseIndex].Faces.Add(face);
                                    // else Debug.Print($"|||Vertex {verts[ptPtCloseIndex].Id} is already contained in Face {face.Id}");
                                    // also add the edge and the face to the vertex 
                                }
                            }
                            edge.CreatePair();

                        }
                        else
                        {
                            face.Edges.Add(edges[closestEdgeIndex]);
                            foreach (var vert in edges[closestEdgeIndex].Vertices)
                            {
                                if (!face.Vertices.Contains(vert))
                                    face.Vertices.Add(vert);
                                if (!vert.Faces.Contains(face))
                                    vert.Faces.Add(face);
                            }
                            edges[closestEdgeIndex].Faces.Add(face);
                        }
                    }
                }
                //here 


            }

            //var foam = new PFoam()

            // here test for faces with only 2 edges 




            Vertices = new List<PFVertex>(verts);
            Edges = new List<PFEdge>(edges);


            Faces = new List<PFFace>(faces);


        }


        /// <summary>
        /// Adds the edge pairs to the list of the foam object
        /// </summary>
        public void ExtractPairEdges()
        {
            var pairs = new List<PFEdge>();
            for (int e = 0; e < Edges.Count; e++)
            {
                pairs.Add(Edges[e].Pair ?? throw new PolyFrameworkException("You need to set the edge pair first "));
            }
            Edges = Edges.Concat(pairs).ToList();
        }

        /// <summary>
        /// Adds the face pairs to the list of the foam object. 
        /// </summary>
        public void ExtractPairFaces()
        {
            var pairs = new List<PFFace>();
            for (int f = 0; f < Faces.Count; f++)
            {
                pairs.Add(Faces[f].Pair ?? throw new PolyFrameworkException("You need to set the face pair first "));
            }

            Faces = Faces.Concat(pairs).ToList();
        }

        /// <summary>
        /// Extract all the facePairs from the half-edges into one dictionary.
        /// Create all the cells from the face pairs 
        /// </summary>
        public void ConstructCells()
        {
            Dictionary<PFFace, List<PFFace>> fpDict = new Dictionary<PFFace, List<PFFace>>();

            // combine all the possible face pairs from the polyFoam into one dictionary
            foreach (var edge in Edges)
            {
                foreach (var keyValue in edge.FacePairs().ToList())
                {
                    List<PFFace> connectedFaces = new List<PFFace>();
                    if (!fpDict.TryGetValue(keyValue.Key, out connectedFaces))
                    {
                        // if not create the pair face, list<pface>{with the pair face}
                        fpDict.Add(keyValue.Key, keyValue.Value);
                    }
                    else
                    {
                        // get list from the dictionary, add the pair face list 
                        foreach (var face in keyValue.Value)
                        {
                            if (!connectedFaces.Contains(face))
                                connectedFaces.Add(face);
                        }


                    }
                }
                //fpDict = fpDict.Concat(edge.FacePairs()).ToDictionary(x => x.Key, x => x.Value);
            }

            /*
             * this is just for debugging
             * 
             *foreach (var keyValuePair in fpDict)
             *{
             *   string pair = "F" + keyValuePair.Key.Id.ToString() + " = ";
             *   foreach (var face in keyValuePair.Value)
             *   {
             *       pair += " f" + face.Id.ToString();
             *   }
             *
             *   Debug.Print(pair);
             *}
             */

            HashSet<PFFace> usedFaces = new HashSet<PFFace>();
            // 2 nested while loops
            // inner loop iterates until a full cell is found (condition boundary cell list is empty)
            // outer loop iterates until no more face pairs found in dictionary 
            while (fpDict.Count > 0)
            {
                var workFace = fpDict.Keys.ElementAt(0);
                var cell = new PFCell(Cells.Count);
                //cell.Faces.Add(workFace);
                var boundaryFaces = new List<PFFace> { workFace };
                while (boundaryFaces.Count > 0)
                {
                    var boundaryRemove = new List<PFFace>();
                    var boundaryExtention = new HashSet<PFFace>();
                    foreach (var face in boundaryFaces)
                    {
                        cell.Faces.Add(face);
                        // try to get the list of pairs for this face 
                        var nextFaces = new List<PFFace>();
                        if (fpDict.TryGetValue(face, out nextFaces))
                        {
                            // if there is a list get the first face from it
                            // add it to the cell
                            foreach (var nFace in nextFaces)
                            {
                                if (!boundaryFaces.Contains(nFace) && !cell.Faces.Contains(nFace) && !usedFaces.Contains(nFace))
                                {
                                    boundaryExtention.Add(nFace);
                                    usedFaces.Add(nFace);////////////////////////////////////////////////////////////////////////
                                }
                            }
                            usedFaces.Add(face);//////////////////////////////////////////////////////////////////////////////////

                            fpDict.Remove(face);
                        }
                        boundaryRemove.Add(face);

                    }
                    // execute faces removal from boundary faces
                    boundaryRemove.ForEach(x => boundaryFaces.Remove(x));
                    // add new boundary faces
                    boundaryFaces.AddRange(boundaryExtention);
                }
                cell.UpdateAllFromFaces();
                Cells.Add(cell);
            }


            // find the exterior cell .... only if no other exterior cells exist 
            // this can happen if this is a form diagram loaded as geometry.
            // then all the diagram will have multiple external cells - basically all open ones will be external
            if (Cells.All(x => !x.Exterior))
            {
                int extIndex = 0;
                double maxArea = 0.0;

                for (int c = 0; c < Cells.Count; c++)
                {
                    double cellArea = Cells[c].Faces.Sum(x => x.Area);
                    if (cellArea > maxArea)
                    {
                        maxArea = cellArea;
                        extIndex = c;
                    }
                }
                Cells[extIndex].Exterior = true;
                Centroid = Cells[extIndex].Centroid;
            }
            else
            {
                // this could be off 
                Centroid = Util.AveragePoints(Cells.Where(x => x.Exterior).Select(y => y.Centroid));
            }
        }


        /// <summary>
        /// Extract all the facePairs from the half-edges into one dictionary.
        /// Create all the cells from the face pairs 
        /// </summary>
        public IList<PFoam> ConstructCellsAndSplit()
        {
            List<PFoam> pieces = new List<PFoam>();
            Dictionary<PFFace, List<PFFace>> fpDict = new Dictionary<PFFace, List<PFFace>>();

            // combine all the possible face pairs from the polyFoam into one dictionary
            foreach (var edge in Edges)
            {
                foreach (var keyValue in edge.FacePairs().ToList())
                {
                    List<PFFace> connectedFaces = new List<PFFace>();
                    if (!fpDict.TryGetValue(keyValue.Key, out connectedFaces))
                    {
                        // if not create the pair face, list<pface>{with the pair face}
                        fpDict.Add(keyValue.Key, keyValue.Value);
                    }
                    else
                    {
                        // get list from the dictionary, add the pair face list 
                        foreach (var face in keyValue.Value)
                        {
                            if (!connectedFaces.Contains(face))
                                connectedFaces.Add(face);
                        }


                    }
                }
                //fpDict = fpDict.Concat(edge.FacePairs()).ToDictionary(x => x.Key, x => x.Value);
            }


            HashSet<PFFace> usedFaces = new HashSet<PFFace>();
            // 2 nested while loops
            // inner loop iterates until a full cell is found (condition boundary cell list is empty)
            // outer loop iterates until no more face pairs found in dictionary 
            while (fpDict.Count > 0)
            {
                var workFace = fpDict.Keys.ElementAt(0);
                var cell = new PFCell(Cells.Count);
                //cell.Faces.Add(workFace);
                var boundaryFaces = new List<PFFace> { workFace };
                while (boundaryFaces.Count > 0)
                {
                    var boundaryRemove = new List<PFFace>();
                    var boundaryExtention = new HashSet<PFFace>();
                    foreach (var face in boundaryFaces)
                    {
                        cell.Faces.Add(face);
                        // try to get the list of pairs for this face 
                        var nextFaces = new List<PFFace>();
                        if (fpDict.TryGetValue(face, out nextFaces))
                        {
                            // if there is a list get the first face from it
                            // add it to the cell
                            foreach (var nFace in nextFaces)
                            {
                                if (!boundaryFaces.Contains(nFace) && !cell.Faces.Contains(nFace) && !usedFaces.Contains(nFace))
                                {
                                    boundaryExtention.Add(nFace);
                                    usedFaces.Add(nFace);////////////////////////////////////////////////////////////////////////
                                }
                            }
                            usedFaces.Add(face);//////////////////////////////////////////////////////////////////////////////////

                            fpDict.Remove(face);
                        }
                        boundaryRemove.Add(face);

                    }
                    // execute faces removal from boundary faces
                    boundaryRemove.ForEach(x => boundaryFaces.Remove(x));
                    // add new boundary faces
                    boundaryFaces.AddRange(boundaryExtention);
                }
                cell.UpdateAllFromFaces();
                Cells.Add(cell);
            }

            // here run a BFS on cells to split the results 
            // after repopulate all from the cell sets 

            var parts = FoamPartition();
            List<PFoam> foams = new List<PFoam>();
            foreach (var cellList in parts)
            {
                PFoam part = new PFoam();
                var pVerts = new HashSet<PFVertex>();
                var pEdges = new HashSet<PFEdge>();
                var pFaces = new HashSet<PFFace>();

                foreach (var cell in cellList)
                {
                    foreach (var vert in cell.Vertices)
                    {
                        pVerts.Add(vert);
                    }
                    foreach (var edge in cell.Edges)
                    {
                        pEdges.Add(edge);
                    }
                    foreach (var face in cell.Faces)
                    {
                        pFaces.Add(face);
                    }

                }
                part.Vertices = new List<PFVertex>(pVerts);
                part.Edges = new List<PFEdge>(pEdges);
                part.Faces = new List<PFFace>(pFaces);
                part.Cells = new List<PFCell>(cellList);

                // find the exterior cell
                if (cellList.All(c => !c.Exterior))
                {
                    int extIndex = 0;
                    double maxArea = 0.0;

                    for (int c = 0; c < cellList.Count; c++)
                    {
                        double cellArea = cellList[c].Faces.Sum(x => x.Area);
                        if (cellArea > maxArea)
                        {
                            maxArea = cellArea;
                            extIndex = c;
                        }
                    }
                    cellList[extIndex].Exterior = true;
                    part.Centroid = cellList[extIndex].Centroid;
                }
                else
                {
                    part.Centroid = Util.AveragePoints(cellList.Where(cell => cell.Exterior).Select(cell => cell.Centroid));
                }
                foams.Add(part);
            }

            return foams;

        }


        /// <summary>
        /// Puts all the faces in the corresponding edges.
        /// Also looks to see if the edge is naked and marks it and its face as external 
        /// </summary>
        public void UpdateEdgesWithFaces()
        {
            foreach (var edge in Edges)
            {
                edge.Faces = new List<PFFace>();
            }
            foreach (var face in Faces)
            {
                foreach (var edge in face.Edges)
                {
                    edge.Faces.Add(face);
                }
            }
            // this mostly refers to form diagrams loaded directly from geometry faces
            foreach (var edge in Edges)
            {
                if (edge.Faces.Count == 1)
                {
                    edge.External = true;
                    edge.Faces.Single().External = true;
                }
            }
        }


        public void SortPartsInFaces()
        {
            bool sortingSuccessful = true;
            foreach (var face in Faces)
            {
                if (!face.SortParts())
                {
                    sortingSuccessful = false;
                }

            }
            if (!sortingSuccessful)
            {
                throw new PolyFrameworkException(@"Some faces could not be processed. 
Some elements might be smaller than the specified tolerance and thus disappear or some faces need to be further subdivided!
The problematic geometry was baked in Orange in the <<Error_Geometry>> layer");
            }
        }

        public void MakeCells()
        {
            bool sortingSuccessful = true;
            foreach (var face in Faces)
            {
                if (!face.SortParts())
                {
                    sortingSuccessful = false;
                }

            }
            if (!sortingSuccessful)
            {
                throw new PolyFrameworkException(@"Some faces could not be processed. 
Some elements might be smaller than the specified tolerance and thus disappear or some faces need to be further subdivided!
The problematic geometry was baked in Orange in the <<Error_Geometry>> layer");
            }
            ExtractPairEdges();
            ExtractPairFaces();
            UpdateEdgesWithFaces();
            foreach (var edge in Edges) edge.SortFacesByAngle();
            foreach (var face in Faces) face.FaceMesh();
            ConstructCells();


        }

        public IList<PFoam> MakeMultipleCells()
        {
            foreach (var face in Faces) face.SortParts();
            ExtractPairEdges();
            ExtractPairFaces();
            UpdateEdgesWithFaces();
            foreach (var edge in Edges) edge.SortFacesByAngle();
            foreach (var face in Faces) face.FaceMesh();

            return ConstructCellsAndSplit();
        }




        public void ShowCells()
        {
            string in_str = "";
            var result = Rhino.Input.RhinoGet.GetString("press <Enter> to start showing cell geometry", true, ref in_str);
            //var fullMesh = new Mesh();
            foreach (var cell in Cells)
            {
                cell.AgregateCellMesh();
                //fullMesh.Append(cell.CellMesh);
            }
            if (result == Rhino.Commands.Result.Nothing)
            {


                var meshConduit = new DrawPFMeshConduit(Cells.Select(x => x.CellMesh).ToList(), 0.5)
                {
                    Enabled = true
                };

                Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
                Rhino.Input.RhinoGet.GetString("press <Enter> to stop showing cell geometry", true, ref in_str);
                meshConduit.Enabled = false;
                Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

            }
            // else branch is for baking the mesh geometry 
            /*
            else
            {
                var attributes = new Rhino.DocObjects.ObjectAttributes
                {

                    ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                    PlotColorSource = Rhino.DocObjects.ObjectPlotColorSource.PlotColorFromObject,
                    ObjectColor = System.Drawing.Color.Green 
                };
                foreach (var cell in Cells)
                    Rhino.RhinoDoc.ActiveDoc.Objects.AddMesh(cell.CellMesh, attributes);
            }
            */

        }

        /// <summary>
        /// Offsets a foam in 3d space 
        /// </summary>
        /// <param name="distance"></param>
        public void Offset(Vector3d distance)
        {
            Transform offset = Transform.Translation(distance);
            foreach (PFVertex vert in Vertices)
            {
                vert.Point += distance;
            }
            foreach (PFFace face in Faces)
            {
                face.Centroid += distance;
                face.FMesh?.Transform(offset);
            }
            foreach (PFCell cell in Cells)
            {
                cell.Centroid += distance;
                cell.CellMesh?.Transform(offset);
            }
            Centroid += distance;

            // offset all the points - including Centroids 
            // offset all the meshes - face and cell 
        }

        //______________________ Put the offset foam here 

        public IList<Brep> BakeCellsAsBreps()
        {
            foreach (var cell in Cells)
            {

            }

            return new List<Brep>();
        }


        /// <summary>
        /// Creates another polyhedral foam, the dual of the object that invokes the method
        /// </summary>
        /// <returns>The dual of the PFoam </returns>
        public PFoam CreateDual()
        {
            foreach (var vert in Vertices) vert.Dual = null;
            foreach (var edge in Edges) edge.Dual = null;
            foreach (var face in Faces) face.Dual = null;
            foreach (var cell in Cells) cell.Dual = new List<PFVertex>();


            // create an average value for the edges sticking out of 
            var edgesLen = Edges.Select(x => x.GetLength());
            var averageLen = edgesLen.Average();
            PFoam dual = new PFoam();
            Dictionary<PFFace, PFVertex> exteriorDualCoresp = new Dictionary<PFFace, PFVertex>();

            // first create dual vertexes
            // the vertex will have the same id like the cell dual it comes from. ?????
            foreach (var cell in Cells)
            {
                //cell.Dual = new List<PFVertex>();
                // for non exterior cells
                if (!cell.Exterior) //the exterior cell does not get a dual vertex
                {
                    var dualVert = new PFVertex(cell.Id, cell.Centroid); // create vertex
                    cell.Dual.Add(dualVert); // add it as dual to the primer cell
                    dualVert.Dual = cell; // make the cell the dual of the created vertex
                    dual.Vertices.Add(dualVert);  // add the vertex to the list of the dual foam/structure 
                }
                else // for the exterior cell create a dual vertex for each face 
                {
                    int dualVertCounter = Cells.Count;
                    foreach (var face in cell.Faces)
                    {
                        PFVertex extVert = new PFVertex(dualVertCounter, face.Centroid + averageLen * face.Normal)
                        {
                            External = true
                        };
                        cell.Dual.Add(extVert); // the cell is going to have multiple duals
                        dualVertCounter++;
                        extVert.Dual = cell;
                        exteriorDualCoresp.Add(face, extVert); // this is for later to get the vertex faster;
                        dual.Vertices.Add(extVert);
                        dual.ExtVetices.Add(extVert);
                    }

                }
            }


            // dual edges 
            // dual edge = primer face 
            // look at half faces in original foam - get their cell
            // a dual edge will connect a 2dual vertexes => a face and its half pair will connect 2 cells 


            // 1) go through every cell - 
            // get cell faces - get their pairs - get the other cell 
            // get the dual vertex of the cell - test if exterior or not  



            foreach (var cell in Cells)
            {


                if (!cell.Exterior) // if we start from an interior cell
                {
                    foreach (var face in cell.Faces)
                    {
                        PFVertex otherVert = new PFVertex();
                        if (!face.Pair.Cell.Exterior)
                        {
                            // this branch applies if the new edge is not sticking out of the primer ext cell
                            otherVert = face.Pair.Cell.Dual.Single() ?? throw new PolyFrameworkException("Cell has no dual vertex set!");
                            var dualEdge = new PFEdge(face.Id)
                            {
                                Vertices = new List<PFVertex>() { cell.Dual.Single(), otherVert },
                                Dual = face,


                            };
                            cell.Dual.Single().Edges.Add(dualEdge); // add edge to the vertex
                            otherVert.Edges.Add(dualEdge); // add edge to the vertex
                            dual.Edges.Add(dualEdge); // add edge to dual foam edges 
                            face.Dual = dualEdge; // add edge as the dual of the primer face 
                        }
                        else
                        {
                            // this branch applies when the edge sticks out of the primer ext cell
                            // this results in the new vertex and the new edge being added to the exterior (sub)lists
                            // the new vertex in this case is created from the face centroid + the face normal
                            //otherVert = face.Pair.Cell.Dual[face.Pair.Cell.Faces.IndexOf(face.Pair)];  //------------------- need to use dict here 
                            otherVert = exteriorDualCoresp[face.Pair]; // faster alternative to above;


                            var dualEdge = new PFEdge(face.Id)
                            {
                                Vertices = new List<PFVertex>() { cell.Dual.Single(), otherVert },
                                Dual = face,
                                External = true

                            };
                            cell.Dual.Single().Edges.Add(dualEdge); // add edge to the vertex
                            otherVert.Edges.Add(dualEdge); // add edge to the vertex
                            dual.Edges.Add(dualEdge); // add edge to dual foam edges 
                            dual.ExtEdges.Add(dualEdge); // add edge to dual foam external edges subset
                            face.Dual = dualEdge; // add edge as the dual of the primer face 
                        }


                    }
                }
                else
                { // get the dual edges of the exterior cell face
                    // just go through all the faces/duals of the face 
                    for (int f = 0; f < cell.Faces.Count; f++)
                    {
                        PFVertex firstVert = cell.Dual[f];
                        PFVertex otherVert = cell.Faces[f].Pair.Cell.Dual.Single();
                        firstVert.Point = otherVert.Point + cell.Faces[f].Normal * averageLen;  // this is a patch - recalculating this verterx position is not elengant. 
                        var dualEdge = new PFEdge(cell.Faces[f].Id)
                        {
                            Vertices = new List<PFVertex>() { firstVert, otherVert },
                            Dual = cell.Faces[f],
                            External = true
                        };
                        firstVert.Edges.Add(dualEdge); // add edge to the vertex
                        otherVert.Edges.Add(dualEdge); // add edge to the vertex
                        cell.Faces[f].Dual = dualEdge; // add edge as the dual of the primer face 
                        dual.Edges.Add(dualEdge); // add edge to dual foam edges 
                        dual.ExtEdges.Add(dualEdge); // add edge to dual foam external edges subset
                    }
                }
            }
            foreach (var dualEdge in dual.Edges)
            {
                // complete all the pairing of the dual edges
                dualEdge.Pair = dualEdge.Dual.Pair.Dual;
            }


            // faces
            // primer edges = dual faces.
            // go through each primer edge - make face with same id 
            // get all primer faces from around primer edge - they should all have duals now (dual edges)
            // get the dual edges in the same order in the dual face 
            // get dual vertexes in the face via the dual 

            foreach (var edge in Edges)
            {
                bool external = false;
                var dualFace = new PFFace(edge.Id);
                foreach (var face in edge.Faces)
                {
                    dualFace.Edges.Add(face.Dual);
                    // the face to the edge will be added at the end 
                    // they need to be added in order



                    // dualFace.Vertices.Add(face.Dual.Vertices[0]);
                    // adding also the face to vertex  
                    //face.Dual.Vertices[0].Faces.Add(dualFace);
                    // set the face as external if necessary
                    if (face.Dual.External) external = true;
                }
                dualFace.Edges = dualFace.Edges.Reverse().ToList();

                // add vertexes to the face and the face to the vertex
                // go through all the edges in the dual face (they should be sorted)
                // add points to the face list if the last element in the list is not identical
                foreach (var faceDualedge in dualFace.Edges)
                {
                    foreach (var dualEdgeVert in faceDualedge.Vertices)
                    {
                        if (dualFace.Vertices.Count == 0 || (dualFace.Vertices[dualFace.Vertices.Count - 1] != dualEdgeVert && dualFace.Vertices[0] != dualEdgeVert))
                        {
                            dualFace.Vertices.Add(dualEdgeVert);
                            dualEdgeVert.Faces.Add(dualFace);
                        }
                    }
                }



                dualFace.Dual = edge;
                edge.Dual = dualFace;
                dual.Faces.Add(dualFace);
                if (external)
                {
                    dual.ExtFaces.Add(dualFace);
                    dualFace.External = true;
                }
            }
            // now to get the pairs for all dual faces
            foreach (var face in dual.Faces)
            {
                face.Pair = face.Dual.Pair.Dual;

            }
            // now set all dual faces in the dual edges as connections 
            foreach (var edge in dual.Edges)
            {
                // dual edge dual -> primer face -> has sorted primer edges -> each edge has a dual face 
                edge.Faces = edge.Dual.Edges.Select(x => x.Dual).ToList();
            }
            // also set all dual points to know what face they belong to 


            // the dual cells are next. 
            // dual cell = primal point
            // to get the faces of the cell get the outgoing primal edges from primal point  

            // keep score of the exterior cells to use later to the dual shell calculation

            //var marginalDualCells = new List<PFCell>(); // this is not used now -

            foreach (var primeVert in Vertices)
            {
                bool marginal = false;
                // create dual cell 
                var dualCell = new PFCell(primeVert.Id);
                foreach (var priEdge in primeVert.Edges)
                {
                    if (priEdge.Vertices[0] == primeVert)
                    {
                        dualCell.Faces.Add(priEdge.Dual);
                        priEdge.Dual.Cell = dualCell; // add the cell to the faces in it 
                        if (priEdge.Dual.External) marginal = true;

                    }
                }
                dualCell.Dual.Add(primeVert);
                dual.Cells.Add(dualCell);
                primeVert.Dual = dualCell;
                if (marginal) dualCell.Exterior = true; //marginalDualCells.Add(dualCell);  // this is not used 
            }

            // add the missing edge for the exterior faces 
            /**/
            Dictionary<string, PFEdge> extEdgePairs = new Dictionary<string, PFEdge>();
            // a dict to edges hashed by their vertex pairs for fast lookup of edge pairs 
            // when an edge is create also the pair is created and added to the dict 
            // every edge creation looks first if the dict has the edge 

            //also use a dict of type <cell, list of edges> with the new created edges 
            //this is already computed and ready to be used to create the exterior face of the marginal cell
            //and after that the exterior cell of the dual foam 
            Dictionary<PFCell, List<PFEdge>> cellExtEdges = new Dictionary<PFCell, List<PFEdge>>();

            foreach (var face in dual.ExtFaces)
            {
                //the edges should be sorted and oriented 
                //go through the nodes and check if consecutive edges share end nodes
                //if not add edge 
                //keep track of the fully exterior edges in the cell and the foam/structure
                PFVertex lastVert = face.Edges[0].Vertices[1];
                int insertIndex = -1;
                PFEdge extEd = new PFEdge(-1) { External = true };
                for (int e = 0; e < face.Edges.Count; e++)
                {
                    if (face.Edges[e].Vertices[1] != face.Edges[(e + 1) % face.Edges.Count].Vertices[0])
                    {
                        extEd = new PFEdge(dual.Edges.Count, new List<PFVertex>()
                        {   face.Edges[e].Vertices[1],
                            face.Edges[(e + 1) % face.Edges.Count].Vertices[0],
                        })
                        {

                            External = true

                        };

                        //This was adding the edge to the vertices even though the edge was not used afterwards
                        //face.Edges[e].Vertices[1].Edges.Add(extEd);
                        //face.Edges[(e + 1) % face.Edges.Count].Vertices[0].Edges.Add(extEd);

                        insertIndex = e + 1;
                        break;
                    }
                }
                if (insertIndex != -1)
                {
                    var value = new PFEdge(-10);
                    // using LINQ and string aggregation to create the dictionary key 

                    if (!extEdgePairs.TryGetValue(extEd.Vertices.Select(x => x.Id.ToString()).Aggregate((x, y) => x + y), out value))
                    {
                        // if a missing edge is found then add it to the face at the right place to maintain the order
                        // also add the face to the new edge.
                        // for now the edge is not connected to another face.
                        // it will be after the exterior cell of the dual is computed. 

                        // also now create the Pair of the edge and add it to the dict 

                        // if the pair is found in the dict just use it instead of the computed value 
                        foreach (var vert in extEd.Vertices)
                        {
                            vert.Edges.Add(extEd);
                        }
                        extEd.CreatePair();

                        var pKey = extEd.Pair.Vertices.Select(x => x.Id.ToString()).Aggregate((x, y) => x + y);
                        extEdgePairs.Add(pKey, extEd.Pair);
                    }
                    else
                        extEd = value;


                    face.Edges.Insert(insertIndex, extEd); // add to the face 
                    extEd.Faces.Add(face); // add the face to the edge 
                    dual.Edges.Add(extEd); // add to the dual 
                    dual.ExtEdges.Add(extEd); // add to the exterior edges in the dual 
                    // also add to the corresponding marginal cell
                    List<PFEdge> marginalCellEdges = new List<PFEdge>();

                    if (cellExtEdges.TryGetValue(face.Cell, out marginalCellEdges))
                    {
                        marginalCellEdges.Add(extEd);
                    }
                    else
                    {
                        cellExtEdges.Add(face.Cell, new List<PFEdge>() { extEd });
                    }


                }


            }
            /**/
            // update the cell structure based on the faces
            foreach (var cell in dual.Cells) cell.DualUpdateAllFromFaces();


            /*   _______________________________________________

            // add the missing face for all the exterior cells. 
            // use all the exterior vertices of a cell to get the missing face
            // get all edges from the cell that have both points outside of the primer cell
            // first create the exterior cell of the dual 

            var exteriorDualCell = new PFCell(dual.Cells.Count)
            {
                Exterior = true
            };

            
            // in the cellExtEdges dict there should be all the edges of the 
            // missing face for each marginal cell 

            foreach (var cell in cellExtEdges.Keys)
            {
                var outFace = new PFFace(dual.Faces.Count)
                {
                    Edges = new List<PFEdge>(cellExtEdges[cell])
                };

                // need to sort the parts of the face
                outFace.SortEdgesDual();

                var vertHash = new HashSet<PFVertex>();
                 
                foreach (var edge in outFace.Edges)
                {                 
                    vertHash.Add(edge.Vertices[0]);
                    vertHash.Add(edge.Vertices[1]);            
                }

                // add vertexes to the face 
                outFace.Vertices = new List<PFVertex>(vertHash);
                // add face to the vertex 
                foreach (var vert in outFace.Vertices) vert.Faces.Add(outFace);
                                
                
                outFace.ComputeCentroid();
                outFace.ComputeFaceNormal();

                var referenceVector = cell.Centroid - outFace.Centroid;
                outFace.CreatePair();

                dual.Faces.Add(outFace);
                dual.Faces.Add(outFace.Pair);

                // test if the normal of the face is facing like the reference vector add it to the cell
                // else add the dual and add the face to the outer dual cell. 
                if (referenceVector - outFace.Normal < referenceVector-outFace.Pair.Normal)
                {
                    cell.Faces.Add(outFace);
                    outFace.Cell = cell;
                    exteriorDualCell.Faces.Add(outFace.Pair);
                    outFace.Pair.Cell = exteriorDualCell;
                }
                else
                {
                    cell.Faces.Add(outFace.Pair);
                    outFace.Pair.Cell = cell;
                    exteriorDualCell.Faces.Add(outFace);
                    outFace.Cell = exteriorDualCell;
                }
                // there is no need to update the marginal interior cell with edges and vertices 

            }
            // update the vertices and edges in the exterior dual cell
            exteriorDualCell.UpdateAllFromFaces();
            dual.Cells.Add(exteriorDualCell);


            _______________________________________________________________________*/



            // create the mesh for each dual face 
            foreach (var face in dual.Faces)
            {
                face.ComputeFaceNormal();
                face.ComputeCentroid();
                face.FaceMesh();
            }

            dual.Centroid = Centroid;

            // making sure the dual/primal know of each other.

            dual.Dual = this;
            Dual = dual;




            return dual;
        }


        /// <summary>
        /// Alternative method used for refreshing the dual when there is no need to created the topology structure.
        /// Just the new positions for the vertices are computed. 
        /// </summary>
        /// <returns></returns>
        public Dictionary<int, Point3d> ComputeDualVertices()
        {

            var dualVertCoresp = new Dictionary<int, Point3d>();

            // create an average value for the edges sticking out of 
            var edgesLen = Edges.Select(x => x.GetLength());
            var averageLen = edgesLen.Average();
            PFoam dual = new PFoam();
            Dictionary<PFFace, PFVertex> exteriorDualCoresp = new Dictionary<PFFace, PFVertex>();

            // first create dual vertexes
            // the vertex will have the same id like the cell dual it comes from. ?????
            foreach (var cell in Cells)
            {
                // for non exterior cells
                if (!cell.Exterior) //the exterior cell does not get a dual vertex
                {
                    var dualVert = new PFVertex(cell.Id, cell.Centroid); // create vertex
                    dualVertCoresp.Add(dualVert.Id, dualVert.Point);

                }
                else // for the exterior cell create a dual vertex for each face 
                {
                    int dualVertCounter = Cells.Count - 1;
                    foreach (var face in cell.Faces)
                    {
                        PFVertex extVert = new PFVertex(dualVertCounter, face.Centroid + averageLen * face.Normal)
                        {
                            External = true
                        };
                        cell.Dual.Add(extVert); // the cell is going to have multiple duals
                        dualVertCoresp.Add(extVert.Id, extVert.Point);
                        dualVertCounter++;
                    }

                }
            }
            return dualVertCoresp;
        }


        /// <summary>
        /// Creates another polyhedral foam, the dual of the object that invokes the method
        /// This version is made to create the dual of the dual - so the primal
        /// </summary>
        /// <returns>The dual of the PFoam </returns>
        public PFoam CreatePrimal()
        {
            foreach (var vert in Vertices) vert.Dual = null;
            foreach (var edge in Edges) edge.Dual = null;
            foreach (var face in Faces) face.Dual = null;
            foreach (var cell in Cells) cell.Dual = new List<PFVertex>();

            // create an average value for the edges sticking out of 
            var edgesLen = Edges.Select(x => x.GetLength());
            var averageLen = edgesLen.Average();
            PFoam dual = new PFoam();
            Dictionary<PFFace, PFVertex> exteriorDualCoresp = new Dictionary<PFFace, PFVertex>();

            // first create dual vertexes
            // the vertex will have the same id like the cell dual it comes from. ?????
            foreach (var cell in Cells)
            {
                // we have no "exterior" big cell here so all cells will be treated equal
                //cell.Dual = new List<PFVertex>();
                var dualVert = new PFVertex(cell.Id, cell.Centroid); // create vertex
                cell.Dual.Add(dualVert); // add it as dual to the primer cell
                dualVert.Dual = cell; // make the cell the dual of the created vertex
                dual.Vertices.Add(dualVert);  // add the vertex to the list of the dual foam/structure 

            }


            // dual edges 
            // dual edge = primer face 
            // look at half faces in original foam - get their cell
            // a dual edge will connect a 2dual vertexes => a face and its half pair will connect 2 cells 


            // 1) go through every cell - 
            // get cell faces - get their pairs - get the other cell 
            // get the dual vertex of the cell - test if exterior or not  



            foreach (var cell in Cells)
            {

                // again all cells are treated equal - no big exterior cell 

                foreach (var face in cell.Faces)
                {
                    PFVertex otherVert = new PFVertex();

                    // all edges here should be between cell centroids - no exterior cell exists per se 
                    otherVert = face.Pair.Cell.Dual.Single() ?? throw new PolyFrameworkException("Cell has no dual vertex set!");
                    var dualEdge = new PFEdge(face.Id)
                    {
                        Vertices = new List<PFVertex>() { cell.Dual.Single(), otherVert },
                        Dual = face,
                        //External = cell.Exterior

                    };
                    cell.Dual.Single().Edges.Add(dualEdge); // add edge to the vertex
                    otherVert.Edges.Add(dualEdge); // add edge to the vertex
                    dual.Edges.Add(dualEdge); // add edge to dual foam edges 
                    face.Dual = dualEdge; // add edge as the dual of the primer face 

                }


            }
            foreach (var dualEdge in dual.Edges)
            {
                // complete all the pairing of the dual edges
                dualEdge.Pair = dualEdge.Dual.Pair.Dual;
            }


            // faces
            // primer edges = dual faces.
            // go through each primer edge - make face with same id 
            // get all primer faces from around primer edge - they should all have duals now (dual edges)
            // get the dual edges in the same order in the dual face 
            // get dual vertexes in the face via the dual 

            foreach (var edge in Edges)
            {
                // only interior or half exterior edges are processed because full ext edges don't have duals 
                if (edge.Vertices.Any(x => !x.External))
                {


                    bool external = false;
                    var dualFace = new PFFace(edge.Id)
                    {
                        External = edge.Vertices[0].External

                    };
                    foreach (var face in edge.Faces)
                    {
                        dualFace.Edges.Add(face.Dual);
                        // the face to the edge will be added at the end 
                        // they need to be added in order



                        // dualFace.Vertices.Add(face.Dual.Vertices[0]);
                        // adding also the face to vertex  
                        //face.Dual.Vertices[0].Faces.Add(dualFace);
                        // set the face as external if necessary
                        //if (face.Dual.External) external = true;
                    }
                    // no reversing of the edges here 
                    dualFace.Edges = dualFace.Edges.Reverse().ToList();

                    // add vertexes to the face and the face to the vertex
                    // go through all the edges in the dual face (they should be sorted)
                    // add points to the face list if the last element in the list is not identical
                    foreach (var faceDualedge in dualFace.Edges)
                    {
                        foreach (var dualEdgeVert in faceDualedge.Vertices)
                        {
                            if (dualFace.Vertices.Count == 0 || (dualFace.Vertices[dualFace.Vertices.Count - 1] != dualEdgeVert && dualFace.Vertices[0] != dualEdgeVert))
                            {
                                dualFace.Vertices.Add(dualEdgeVert);
                                dualEdgeVert.Faces.Add(dualFace);
                            }
                        }
                    }



                    dualFace.Dual = edge;
                    edge.Dual = dualFace;
                    dual.Faces.Add(dualFace);
                    if (dualFace.External)
                    {
                        dual.ExtFaces.Add(dualFace);
                        //dualFace.External = true;
                    }


                }
            }
            // now to get the pairs for all dual faces
            foreach (var face in dual.Faces)
            {
                face.Pair = face.Dual.Pair.Dual;

            }
            // now set all dual faces in the dual edges as connections 
            foreach (var edge in dual.Edges)
            {
                // dual edge dual -> primer face -> has sorted primer edges -> each edge has a dual face 
                // edge.Faces = edge.Dual.Edges.Select(x => x.Dual).ToList();
                // Here we have a special case where we need to account for the full exterior edges that have no dual 

                edge.Faces = edge.Dual.Edges.Where(x => x.Dual != null && x.Dual.Id != 0).Select(y => y.Dual).ToList();
            }
            // also set all dual points to know what face they belong to 


            // the dual cells are next. 
            // dual cell = primal point
            // to get the faces of the cell get the outgoing primal edges from primal point  

            // keep score of the exterior cells to use later to the dual shell calculation

            //var marginalDualCells = new List<PFCell>(); // this is not used now -
            var extVerts = new List<PFVertex>();

            foreach (var primeVert in Vertices)
            {
                if (primeVert.External)
                {
                    extVerts.Add(primeVert);
                    continue;
                }
                bool marginal = false;
                // create dual cell 
                var dualCell = new PFCell(primeVert.Id);
                foreach (var priEdge in primeVert.Edges)
                {
                    if (priEdge.Vertices[0] == primeVert)
                    {
                        dualCell.Faces.Add(priEdge.Dual);
                        priEdge.Dual.Cell = dualCell; // add the cell to the faces in it 
                        if (priEdge.Dual.External)
                            marginal = true; // this should not hit for create primal from dual

                    }
                }
                dualCell.Dual.Add(primeVert);
                if (dualCell.Dual.Count > 1) throw new PolyFrameworkException("More than one Dual for the cell does not work for inner cells!");
                dual.Cells.Add(dualCell);
                primeVert.Dual = dualCell;
                //if (marginal) dualCell.Exterior = true; //marginalDualCells.Add(dualCell);  // this is not used 
            }

            // create the exterior cell 
            var dualExtCell = new PFCell(dual.Cells.Count)
            {
                Faces = dual.ExtFaces.ToList(),
                Dual = extVerts,
                Exterior = true,
                Centroid = Centroid
            };
            dual.Cells.Add(dualExtCell);


            /**/
            // update the cell structure based on the faces
            foreach (var cell in dual.Cells) cell.DualUpdateAllFromFaces();





            // create the mesh for each dual face 
            foreach (var face in dual.Faces)
            {
                face.ComputeFaceNormal();
                face.FaceMesh();
            }

            dual.Centroid = Centroid;

            // making sure the dual/primal know of each other.

            dual.Dual = this;
            Dual = dual;




            return dual;
        }

        /// <summary>
        /// Alternative method used for refreshing the dual when there is no need to created the topology structure.
        /// Just the new positions for the vertices are computed. 
        /// </summary>
        /// <returns></returns>
        public Dictionary<int, Point3d> ComputePrimalVertices()
        {
            var dualVertCoresp = new Dictionary<int, Point3d>();



            foreach (var cell in Cells)
            {
                // we have no "exterior" big cell here so all cells will be treated equal

                var dualVert = new PFVertex(cell.Id, cell.Centroid); // create vertex
                dualVertCoresp.Add(dualVert.Id, dualVert.Point);

            }


            return dualVertCoresp;

        }



        /// <summary>
        /// Creates "ripples" of cells originating from the input list of cells
        /// The input cells can be disjoint "ripples" will be created from each cluster.
        /// </summary>
        /// <param name="startCells">The cells to start the calculation from.</param>
        /// <returns>A list of ripples. Each ripple is a list of cells.</returns>
        public IList<IList<PFCell>> CellPartition(List<PFCell> startCells)
        {
            var partitions = new List<List<PFCell>>();

            List<PFCell> workCells = new List<PFCell>(startCells);
            HashSet<PFCell> usedCells = new HashSet<PFCell>();

            while (true)
            {
                partitions.Add(workCells);
                workCells.ForEach(x => usedCells.Add(x));
                var nextCells = new HashSet<PFCell>();
                foreach (var cell in workCells)
                {
                    foreach (var face in cell.Faces)
                    {
                        if (!nextCells.Contains(face.Pair.Cell) && !usedCells.Contains(face.Pair.Cell) && !face.Pair.Cell.Exterior)
                        {
                            nextCells.Add(face.Pair.Cell);
                        }
                    }
                }
                if (nextCells.Count == 0) break;
                else
                {
                    workCells = new List<PFCell>(nextCells);
                    nextCells = new HashSet<PFCell>();
                }

            }
            var result = partitions.Select(x => (IList<PFCell>)x).ToList();

            return (IList<IList<PFCell>>)result;
        }

        /// <summary>
        /// Creates a list of cell lists based on the cell connection graph
        /// Cell->Face->FacePair->NeighborCell
        /// Used mostly for splitting separate parts of the foam
        /// </summary>
        /// <returns></returns>
        public IList<IList<PFCell>> FoamPartition()
        {
            var partitions = new List<List<PFCell>>();
            var allCellsHash = new HashSet<PFCell>(Cells);

            HashSet<PFCell> usedCells = new HashSet<PFCell>();


            while (allCellsHash.Count > 0)
            {
                List<PFCell> workCells = new List<PFCell>() { allCellsHash.First() };
                var subFoamCells = new List<PFCell>();

                while (true)
                {
                    subFoamCells.AddRange(workCells);
                    workCells.ForEach(x => allCellsHash.Remove(x));
                    workCells.ForEach(x => usedCells.Add(x));
                    var nextCells = new HashSet<PFCell>();
                    foreach (var cell in workCells)
                    {
                        foreach (var face in cell.Faces)
                        {
                            if (!nextCells.Contains(face.Pair.Cell) && !usedCells.Contains(face.Pair.Cell))
                            {
                                nextCells.Add(face.Pair.Cell);
                            }
                        }
                    }
                    if (nextCells.Count == 0) break;
                    else
                    {
                        workCells = new List<PFCell>(nextCells);
                        nextCells = new HashSet<PFCell>();
                    }

                }

                partitions.Add(subFoamCells);
            }
            var result = partitions.Select(x => (IList<PFCell>)x).ToList();

            return (IList<IList<PFCell>>)result;
        }

        /// <summary>
        /// Creates outward waves from a preexisting specified set of sets of cells.
        /// Each set creates its own waves 
        /// </summary>
        /// <param name="originalContaminants"></param>
        /// <returns>A list of all waves(lists) of cells for each inputed set</returns>
        public List<List<List<PFCell>>> Contamination(List<List<PFCell>> originalContaminants)
        {
            //Outer While loop - exit condition = no more cells to contaminate 
            //Create border cells to contaminate - go through all contaminated 
            //In border cells test each cell to see the nature of the neighbors 
            //Contaminate based on the largest number of contaminated neighbors
            //Keep tabs on each contamination spread with dicts or values in the cells ... 
            //Create next border from present border neighbors -> condition = uncontaminated
            //Dump present border in the list of contaminant waves based on contaminant type
            //List[Cont1 = List[wave 1 = list , wave 2 ...], Cont2 = List[wave 1 = list , wave 2 ...], Cont3 = ...]
            //Return list of list of list... :)

            List<List<List<PFCell>>> contaminationWaves = new List<List<List<PFCell>>>();
            // the output list 
            List<PFCell> workCells = new List<PFCell>(); // the first wave from the input 
            HashSet<PFCell> usedCells = new HashSet<PFCell>();
            Dictionary<PFCell, int> cellContamination = new Dictionary<PFCell, int>();
            // set original contamination
            for (int l = 0; l < originalContaminants.Count; l++)
            {
                contaminationWaves.Add(new List<List<PFCell>>());
                workCells.AddRange(originalContaminants[l]);
                foreach (var cell in originalContaminants[l])
                {
                    if (cellContamination.ContainsKey(cell)) throw new PolyFrameworkException(@"The preexisting cells need to be unique. Make sure you have unique cell in all the provided lists!");
                    cellContamination.Add(cell, l); // each cell will point to the type of contamination
                }
            }




            while (true)
            {
                // first add another wave to each contamination 
                foreach (var contamination in contaminationWaves)
                {
                    contamination.Add(new List<PFCell>()); // add an empty container list (wave) to each contamination
                }
                foreach (var cell in workCells)
                {
                    // put the cell in the corresponding contamination in the last empty wave (just created above)
                    contaminationWaves[cellContamination[cell]].Last().Add(cell);
                }

                //workCells.ForEach(x => usedCells.Add(x));
                // get the next general wave of expansion - this is not separated on contaminations yet.
                var nextCells = new HashSet<PFCell>();
                foreach (var cell in workCells)
                {
                    foreach (var face in cell.Faces)
                    {
                        if (!nextCells.Contains(face.Pair.Cell) && !cellContamination.ContainsKey(face.Pair.Cell) && !face.Pair.Cell.Exterior)
                        {
                            nextCells.Add(face.Pair.Cell);
                        }
                    }
                }
                if (nextCells.Count == 0) break;
                else
                {
                    // here place each cell in the next wave also in the contamination dictionary 
                    List<int> nextCellConta = new List<int>(); // this is the list specifying the contamination for each cell
                    foreach (var cell in nextCells)
                    {
                        // get all cell neighbors as ints from the cellContamination dictionary if they exist 
                        // if not use -1
                        Dictionary<int, int> neigborContId = new Dictionary<int, int>();
                        foreach (var face in cell.Faces)
                        {
                            int contId = -1;
                            if (cellContamination.TryGetValue(face.Pair.Cell, out contId))
                            {
                                if (neigborContId.ContainsKey(contId)) neigborContId[contId]++;
                                else neigborContId[contId] = 1;
                            }
                        }
                        var keyValList = neigborContId.ToList().OrderByDescending(x => x.Value).ThenBy(x => x.Key);
                        nextCellConta.Add(keyValList.First().Key);

                    }
                    // add the each nextCell in cellConatmination dictionary with contamination value
                    foreach (var compoundVal in nextCells.Zip(nextCellConta, (x, y) => new { cell = x, conta = y }))
                    {
                        cellContamination.Add(compoundVal.cell, compoundVal.conta);
                    }
                    workCells = new List<PFCell>(nextCells);
                    nextCells = new HashSet<PFCell>();
                }

            }


            return contaminationWaves;
        }


        /// <summary>
        /// This is not used atm
        /// Creates a set of edge lists based on BFS
        /// </summary>
        /// <returns></returns>
        public List<List<PFEdge>> Partition()
        {
            List<List<PFVertex>> vertPartition = new List<List<PFVertex>>();
            List<List<PFEdge>> edgePartition = new List<List<PFEdge>>();

            List<PFVertex> workVerts = Vertices.Where(x => x.External).ToList();

            HashSet<PFVertex> usedVerts = new HashSet<PFVertex>();
            HashSet<PFEdge> usedEdges = new HashSet<PFEdge>();

            while (true)
            {
                List<PFEdge> workEdges = new List<PFEdge>();  // clear work edges
                vertPartition.Add(workVerts); // add the working verts to the partition list
                workVerts.ForEach(x => usedVerts.Add(x)); // also add the to the used list 
                var nextVerts = new HashSet<PFVertex>(); // this is for building the next part of verts 
                foreach (var vert in workVerts)
                {
                    foreach (var edge in vert.Edges)
                    {
                        if (!usedEdges.Contains(edge))
                        {
                            workEdges.Add(edge);
                            nextVerts.Add(edge.Vertices.Single(x => !usedVerts.Contains(x) && !nextVerts.Contains(x))); // there can be only one

                        }
                    }
                }
                if (workEdges.Count == 0) break; // if no more edges break 
                else
                {
                    edgePartition.Add(new List<PFEdge>(workEdges)); // add to partition
                    workEdges.ForEach(x => usedEdges.Add(x)); // 
                    workEdges = new List<PFEdge>();
                }

                if (nextVerts.Count == 0)
                {
                    break;
                }
                else
                {
                    workVerts = new List<PFVertex>(nextVerts);
                    nextVerts = new HashSet<PFVertex>();
                }
            }


            return edgePartition;
        }
        /// <summary>
        /// Create a the pipe system for the Edges of the Pfoam 
        /// Also output the colors for the edges/pipes 
        /// This is mostly for Grasshopper use.
        /// </summary>
        /// <param name="minRadius"></param>
        /// <param name="maxRadius"></param>
        /// <returns></returns>
        public IList<Tuple<Brep, Color>> PipeGeoDual(double minRadius, double maxRadius, IList<int> appliedId)
        {
            var doc = Rhino.RhinoDoc.ActiveDoc;
            // split edges based on int/halfExt/fullExt
            // make layers for all pipes internal, external, 

            var intEdges = new List<PFEdge>();
            var halfExtEdges = new List<PFEdge>();
            var fullExtEdges = new List<PFEdge>();


            foreach (var edge in Edges)
            {
                if (edge.Id > 0)
                {
                    if (edge.Vertices[0].External && edge.Vertices[1].External) fullExtEdges.Add(edge);
                    else if (edge.Vertices[0].External ^ edge.Vertices[1].External) halfExtEdges.Add(edge);
                    else intEdges.Add(edge);
                }
            }
            IList<Tuple<Brep, Color>> coloredPipes = new List<Tuple<Brep, Color>>();
            var areas = intEdges.Select(x => x.Dual.Area);
            var maxArea = areas.Max();
            var minArea = areas.Min();
            var minColNum = 0.0;
            var maxColNum = 1.0;

            if (maxArea - minArea < doc.ModelAbsoluteTolerance)
            {
                var medRadius = (maxRadius + minRadius) / 2;
                maxRadius = medRadius;
                minRadius = medRadius;
                minColNum = 0.5;
                maxColNum = 0.5;
            }

            //var bbox = new BoundingBox(Vertices.Select(x => x.Point));
            //var boxSize = bbox.Diagonal.Length;
            var radiuses = intEdges.Concat(halfExtEdges).ToDictionary(x => x, x => Util.ValueUnitizer(x.Dual.Area, new List<double> { minArea, maxArea }, new List<double> { minRadius, maxRadius }));

            //var pipes = new List<Brep>();
            var colors = new List<Color>();
            foreach (var edge in intEdges)
            {
                var colPipe = new Tuple<Brep, Color>(Brep.CreatePipe(edge.CreateLine().ToNurbsCurve(), radiuses[edge], false, PipeCapMode.Round, true, doc.ModelAbsoluteTolerance, doc.ModelAngleToleranceRadians)[0],
                    CreateBlue(Util.ValueUnitizer(edge.Dual.Area, new List<double> { minArea, maxArea }, new List<double> { minColNum, maxColNum })));
                coloredPipes.Add(colPipe);
                //pipes.AddRange( Brep.CreatePipe(edge.CreateLine().ToNurbsCurve(), radiuses[edge], false, PipeCapMode.Round, true, doc.ModelAbsoluteTolerance, doc.ModelAngleToleranceRadians));
                //colors.Add(CreateBlue(Util.ValueUnitizer(edge.Dual.Area, new List<double> { minArea, maxArea }, new List<double> { 0.0, 1.0 })));
            }
            foreach (var edge in halfExtEdges)
            {
                if (!appliedId.Contains(edge.Id))
                {
                    var colPipe = new Tuple<Brep, Color>(Brep.CreatePipe(edge.CreateLine().ToNurbsCurve(), radiuses[edge], false, PipeCapMode.Round, true, doc.ModelAbsoluteTolerance, doc.ModelAngleToleranceRadians)[0],
                   Color.FromArgb(0, 100, 0));
                    coloredPipes.Add(colPipe);
                }

            }


            return coloredPipes;
        }
        /// <summary>
        /// Builds a pipe representation of the PolyFrame 
        /// It bakes it to the document together with a line representation for all elements. 
        /// </summary>
        /// <param name="minRadius"></param>
        /// <param name="maxRadius"></param>
        public void PipeDual(double minRadius, double maxRadius)
        {
            var doc = Rhino.RhinoDoc.ActiveDoc;
            // split edges based on int/halfExt/fullExt
            // make layers for all pipes internal, external, 

            var intEdges = new List<PFEdge>();
            var halfExtEdges = new List<PFEdge>();
            var fullExtEdges = new List<PFEdge>();


            foreach (var edge in Edges)
            {
                if (edge.Id > 0)
                {
                    if (edge.Vertices[0].External && edge.Vertices[1].External) fullExtEdges.Add(edge);
                    else if (edge.Vertices[0].External ^ edge.Vertices[1].External) halfExtEdges.Add(edge);
                    else intEdges.Add(edge);
                }
            }

            var areas = intEdges.Select(x => x.Dual.Area);
            var maxArea = areas.Max();
            var minArea = areas.Min();

            var bbox = new BoundingBox(Vertices.Select(x => x.Point));
            var boxSize = bbox.Diagonal.Length;
            var radiuses = intEdges.ToDictionary(x => x, x => Util.ValueUnitizer(x.Dual.Area, new List<double> { minArea, maxArea }, new List<double> { minRadius, maxRadius }));


            var intPipeLayer = new Rhino.DocObjects.Layer()
            {
                Name = "_InternalPipes"
            };
            if (doc.Layers.All(x => x.Name != intPipeLayer.Name)) doc.Layers.Add(intPipeLayer);
            intPipeLayer = doc.Layers.First(x => x.Name == "_InternalPipes");

            var intLineLayer = new Rhino.DocObjects.Layer()
            {
                Name = "_InternalLines"
            };
            if (doc.Layers.All(x => x.Name != intLineLayer.Name)) doc.Layers.Add(intLineLayer);
            intLineLayer = doc.Layers.First(x => x.Name == "_InternalLines");

            var forceLayer = new Rhino.DocObjects.Layer()
            {
                Name = "_ExternalForces"
            };
            if (doc.Layers.All(x => x.Name != forceLayer.Name)) doc.Layers.Add(forceLayer);
            forceLayer = doc.Layers.First(x => x.Name == "_ExternalForces");

            var externalPolyLayer = new Rhino.DocObjects.Layer()
            {
                Name = "_ExternalPoly"
            };
            if (doc.Layers.All(x => x.Name != externalPolyLayer.Name)) doc.Layers.Add(externalPolyLayer);
            externalPolyLayer = doc.Layers.First(x => x.Name == "_ExternalPoly");


            doc.Views.RedrawEnabled = false;

            foreach (var edge in intEdges)
            {
                var attributesPipe = new Rhino.DocObjects.ObjectAttributes
                {

                    ObjectColor = Util.CreateBlue(Util.ValueUnitizer(edge.Dual.Area, new List<double> { minArea, maxArea }, new List<double> { 0.0, 1.0 })),

                    ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                    Name = "intPipe_" + edge.Id.ToString(),
                    LayerIndex = intPipeLayer.LayerIndex
                };
                var attributesLine = new Rhino.DocObjects.ObjectAttributes
                {

                    ObjectColor = Util.CreateBlue(Util.ValueUnitizer(edge.Dual.Area, new List<double> { minArea, maxArea }, new List<double> { 0.0, 1.0 })),
                    PlotWeight = Math.Round(Util.ValueUnitizer(edge.Dual.Area, new List<double> { minArea, maxArea }, new List<double> { 0, 11 }) * 0.05 + 0.15),
                    ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                    Name = "intPipe_" + edge.Id.ToString(),
                    LayerIndex = intLineLayer.LayerIndex
                };


                var pipe = Brep.CreatePipe(edge.CreateLine().ToNurbsCurve(), radiuses[edge], false, PipeCapMode.Round, true, doc.ModelAbsoluteTolerance, doc.ModelAngleToleranceRadians);
                doc.Objects.AddBrep(pipe[0], attributesPipe);
                doc.Objects.AddLine(edge.CreateLine(), attributesLine);
            }

            foreach (var edge in halfExtEdges)
            {
                var attributes = new Rhino.DocObjects.ObjectAttributes
                {

                    ObjectColor = System.Drawing.Color.FromArgb(0, 100, 0),

                    ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                    Name = "extForce_" + edge.Id.ToString(),
                    LayerIndex = forceLayer.LayerIndex,


                };
                if (edge.Vertices[1].External) attributes.ObjectDecoration = Rhino.DocObjects.ObjectDecoration.StartArrowhead;
                else attributes.ObjectDecoration = Rhino.DocObjects.ObjectDecoration.EndArrowhead;



                doc.Objects.AddLine(edge.CreateLine(), attributes);

            }

            foreach (var edge in fullExtEdges)
            {
                var attributes = new Rhino.DocObjects.ObjectAttributes
                {

                    ObjectColor = System.Drawing.Color.LightGray,

                    ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                    Name = "extPoly_" + edge.Id.ToString(),
                    LayerIndex = externalPolyLayer.LayerIndex,


                };
                doc.Objects.AddLine(edge.CreateLine(), attributes);
            }

            doc.Views.RedrawEnabled = true;




        }

        public void BlueBrepFaces(Vector3d translate)
        {
            var doc = Rhino.RhinoDoc.ActiveDoc;
            var _forceFaceLayer = new Rhino.DocObjects.Layer()
            {
                Name = "_Force_Cells"
            };
            if (doc.Layers.All(x => x.Name != _forceFaceLayer.Name)) doc.Layers.Add(_forceFaceLayer);
            _forceFaceLayer = doc.Layers.First(x => x.Name == "_Force_Cells");


            double minArea = Faces.Select(x => x.Area).Min();
            double maxArea = Faces.Select(x => x.Area).Max();
            foreach (var cell in Cells)
            {
                var faceGuids = new List<Guid>();
                foreach (var face in cell.Faces)
                {
                    var attributes = new Rhino.DocObjects.ObjectAttributes
                    {
                        ObjectColor = Util.CreateBlue(Util.ValueUnitizer(face.Area, new List<double> { minArea, maxArea }, new List<double> { 0.0, 1.0 })),

                        ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                        Name = "force_face_" + face.Id.ToString(),
                        LayerIndex = _forceFaceLayer.LayerIndex
                    };
                    var brepFace = face.CreateBrep();
                    brepFace.UserDictionary.Set("Id", face.Id);
                    brepFace.Translate(translate);
                    faceGuids.Add(doc.Objects.Add(brepFace, attributes));
                }
                doc.Groups.Add("cell_" + cell.Id.ToString(), faceGuids);
            }
        }



        public double VolumeSphCyl(double minRadius, double maxRadius, 
            out IList<Mesh> geometry, out Dictionary<int, double> edgeRadiusesOut,
            IList<int> excludedEdges, bool createGeo = false )
        {

            var intEdges = new List<PFEdge>();
            var halfExtEdges = new List<PFEdge>();
            var fullExtEdges = new List<PFEdge>();
            var excluded = new HashSet<int>(excludedEdges);

            foreach (var edge in Edges)
            {
                if (edge.Id > 0 && !excluded.Contains(edge.Id) )
                {
                    if (edge.Vertices[0].External && edge.Vertices[1].External) fullExtEdges.Add(edge);
                    else if (edge.Vertices[0].External ^ edge.Vertices[1].External) halfExtEdges.Add(edge);
                    // here the half ext edges are also considered as int edges
                    else intEdges.Add(edge);
                }
            }

            var intVertices = Vertices.Where(x => !x.External);

            var areas = intEdges.Select(x => x.Dual.Area);
            var maxArea = areas.Max();
            var minArea = areas.Min();

            var bbox = new BoundingBox(Vertices.Select(x => x.Point));
            var boxSize = bbox.Diagonal.Length;
            var edgeRadiuses = intEdges.ToDictionary(x => x.Id, x => Util.ValueUnitizer(x.Dual.Area, new List<double> { minArea, maxArea }, new List<double> { minRadius, maxRadius }));

            // vertex radius is the largest radius of the incoming edges
            var vertRadiuses = intVertices.ToDictionary(x => x, x => x.Edges.Select(e =>edgeRadiuses.ContainsKey(e.Id) ? edgeRadiuses[Math.Abs(e.Id)] : 0.0).Max());

            // edge length - the radius of the vertex. Ext vertex will be considered 0.0 radius)
            var edgeLengths = intEdges.ToDictionary(x => x.Id, x => x.GetLength() - x.Vertices.Select(v => vertRadiuses.ContainsKey(v) ? vertRadiuses[v] : 0.0).Sum());

            double volume = 0.0;

            foreach (var vert in intVertices)
            {
                volume += Math.Pow(vertRadiuses[vert], 3) * Math.PI * 4 / 3;
            }
            foreach (var edge in intEdges)
            {
                if (edgeLengths[edge.Id] > 0)
                    volume += Math.Pow(edgeRadiuses[edge.Id], 2) * Math.PI * edgeLengths[edge.Id];



            }
            geometry = new List<Mesh>();

            edgeRadiusesOut = edgeRadiuses;
            // create the geometry too 
            // cylinders - 
            if (createGeo)
            {
                foreach (var edge in intEdges)
                {
                    if (edgeLengths[edge.Id] > 0)
                    {
                        var edgeVec = edge.GetDirectionVector(); // from V1 to V0
                                                                 // make circle at V1 + V1 radius 
                        var circleCenter = edge.Vertices[1].Point + edgeVec * vertRadiuses[edge.Vertices[1]];
                        var circlePlane = new Plane(circleCenter, edgeVec);
                        var circle = new Circle(circlePlane, edgeRadiuses[edge.Id]);
                        var cylinder = new Cylinder(circle, edgeLengths[edge.Id]);
                        geometry.Add(Mesh.CreateFromCylinder(cylinder, 1, 24));
                    }
                        
                }

                foreach (var vert in intVertices)
                {
                    if (vertRadiuses[vert] > double.Epsilon)
                        geometry.Add(Mesh.CreateFromSphere(new Sphere(vert.Point, vertRadiuses[vert]), 24, 24));
                }

            }



            return volume;

        }


        /// <summary>
        /// Serializes the whole foam as a JSON string
        /// </summary>
        /// <returns></returns>
        public string SerializeJson()
        {


            JavaScriptSerializer json = new JavaScriptSerializer();
            json.MaxJsonLength = 2147483647;
            string jsonData = json.Serialize(SerializeDict());
            //var len = jsonData.Length;
            return jsonData;
        }

        public Dictionary<string, object> SerializeDict()
        {
            //serialize Vertices, Edges, Faces and Cell using their own SerializeDict

            Dictionary<string, object> props = new Dictionary<string, object>
            {
                { "Id", Id },
                { "Vertices", Vertices.ToDictionary(x => x.Id.ToString(), x => x.SerializeDict()) },
                { "Edges", Edges.ToDictionary(x => x.Id.ToString(), x => x.SerializeDict()) },
                { "Faces", Faces.ToDictionary(x => x.Id.ToString(), x => x.SerializeDict()) },
                { "Cells", Cells.ToDictionary(x => x.Id.ToString(), x => x.SerializeDict()) },
                { "ExtVertices", ExtVetices.Select(x => x.Id).ToList() },
                { "ExtEdges", ExtEdges.Select(x => x.Id).ToList() },
                { "ExtFaces", ExtFaces.Select(x => x.Id).ToList() },
                { "Centroid", PVToDict(Centroid) },
                { "Dual", Dual?.Id ?? "" },
                { "MaxDeviation", MaxDeviation }

            };



            return props;
        }

        public void SerializeFoamToFile()
        {
            var fd = new Rhino.UI.SaveFileDialog { Filter = "Json Files (*.json)|*.json" };
            if (fd.ShowSaveDialog()) File.WriteAllText(fd.FileName, SerializeJson());


        }





    }


}

