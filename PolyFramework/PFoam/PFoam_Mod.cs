using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using Rhino.Input.Custom;

namespace PolyFramework
{
    /// <summary>
    /// This takes care of live update for PolyCluster transformation while moving one point 
    /// </summary>
    public class GetPolyTransPoint : GetPoint
    {
        private PFoam _polyFoam;
        private PFVertex _baseVert;
        private bool _consinderExternal;
        private IEnumerable<PFEdge> _singleEdges;

        public GetPolyTransPoint(PFoam polyFoam, PFVertex baseVert, bool considerExternal = false)
        {
            _polyFoam = polyFoam;
            _baseVert = baseVert;
            _consinderExternal = considerExternal;
            _singleEdges = polyFoam.Edges.Where(x => x.Id > 0);
        }

        protected override void OnDynamicDraw(GetPointDrawEventArgs e)
        {
            base.OnDynamicDraw(e);
            var result = _polyFoam.MoveFaceVertices_Form(new List<PFVertex> { _baseVert }, new List<Point3d> { e.CurrentPoint }, 0.001, _consinderExternal);
            //var lines = new Line[result.Edges.Count];
            //var flipped = new System.Drawing.Color[result.Edges.Count];
            foreach (var edg in _singleEdges)
            { 
                // if the line has been moved
                if((result.Edges[edg].StubVertices[0].Position != result.Edges[edg].StubVertices[0].Vertex.Point
                    || result.Edges[edg].StubVertices[1].Position != result.Edges[edg].StubVertices[1].Vertex.Point))
                {
                    e.Display.DrawLine(result.Edges[edg].Line, result.Edges[edg].Flipped ? System.Drawing.Color.Red : System.Drawing.Color.Blue, 2);
                }
                
                //lines[edg.Edge.Id - 1] = edg.Line;
                //flipped[edg.Edge.Id - 1] = edg.Flipped ? System.Drawing.Color.Red : System.Drawing.Color.Blue;
            }


        }
    }
    /// <summary>
    /// Help class for polyCluster transformation.
    /// </summary>
    public class PfoamRef
    {
        public Dictionary<PFVertex, EVertex> Vertices { get; set; } = new Dictionary<PFVertex, EVertex>();
        public Dictionary<PFEdge, EEdge> Edges { get; set; } = new Dictionary<PFEdge, EEdge>();
        public Dictionary<PFFace, EFace> Faces { get; set; } = new Dictionary<PFFace, EFace>();

        /// <summary>
        /// Sorts a set of freshly updated faces into sets of priorities for further intersection processing  
        /// </summary>
        /// <param name="unsorted">input list of fresh faces</param>
        /// <param name="active">output active faces to be processed first</param>
        /// <param name="planar">output planar intersection faces processed second</param>
        /// <param name="nonPlanar">non planar intersections to be processed third</param>
        internal void SortFaces(HashSet<EFace> unsorted, out HashSet<EFace> active, out HashSet<EFace> planar, out HashSet<EFace> nonPlanar)
        {
            active = new HashSet<EFace>();
            planar = new HashSet<EFace>();
            nonPlanar = new HashSet<EFace>();

            foreach (var eFace in unsorted)
            {
                // test for active and then planar (2)
                // go through all original edges (they are ordered ;)
                // if edges[i] is in inPlane & EEdges[i-1] or EEdges[i+1] are in active and edge not in passive (in stubs with 2 everts)
                // then it can go to planar 
                // if EEdge[edge] also in active then face can go to active  
                eFace.ActiQueue = new Dictionary<EVertex, List<EEdge>>(); //clean the face intersection queue
                eFace.PlanarQueue = new Dictionary<EVertex, List<EEdge>>(); //clean the face intersection queue
                if (eFace.Face.Vertices.All(x => eFace.Coresp.Vertices[x].Parsed)) continue;
                // for now the queue needs to be redone every time to find the active intersection anyway
                bool planarInt = false;
                bool activeInt = false;
                for (int i = 0; i < eFace.Face.Edges.Count; i++)
                {

                    bool passive = Edges[eFace.Face.Edges[i]].StubVertices.Count > 1;
                    bool preActive = eFace.Active.Contains(Edges[eFace.Face.Edges[(i - 1) < 0 ? eFace.Face.Edges.Count - 1 : i - 1]]);
                    bool postActive = eFace.Active.Contains(Edges[eFace.Face.Edges[(i + 1) % eFace.Face.Edges.Count]]);

                    if ((preActive || postActive) && !passive)
                    {
                        bool inPlane = eFace.InPlane.Contains(eFace.Face.Edges[i]);
                        bool activeEdge = eFace.Active.Contains(Edges[eFace.Face.Edges[i]]);
                        // have to test for eVert stub sharing here between current eEdge and previous or next  
                        bool evShare = false;
                        if (activeEdge && preActive)
                        {
                            if (Edges[eFace.Face.Edges[i]].StubVertices.Single() ==
                                Edges[eFace.Face.Edges[(i - 1) < 0 ? eFace.Face.Edges.Count - 1 : i - 1]].StubVertices.Single())
                            {
                                evShare = true;
                            }
                        }
                        if (activeEdge && postActive)
                        {
                            if (Edges[eFace.Face.Edges[i]].StubVertices.Single() ==
                                Edges[eFace.Face.Edges[(i + 1) % eFace.Face.Edges.Count]].StubVertices.Single())
                            {
                                evShare = true;
                            }
                        }



                        if (activeEdge && !evShare)
                        {
                            activeInt = true; // set the flag 
                            active.Add(eFace);
                            planarInt = true;
                            if (preActive)
                            {
                                var missingVert = Vertices[eFace.Face.Edges[i].Vertices[0]]; // this is the missing vert
                                eFace.ActiQueue[missingVert]= new List<EEdge> { Edges[eFace.Face.Edges[(i - 1) < 0 ? eFace.Face.Edges.Count - 1 : i - 1]],
                                    Edges[eFace.Face.Edges[i]] }; // both are active here 

                            }
                            if (postActive)
                            {
                                var missingVert = Vertices[eFace.Face.Edges[i].Vertices[1]]; // this is the missing vert
                                eFace.ActiQueue[missingVert]= new List<EEdge> { Edges[eFace.Face.Edges[(i + 1) % eFace.Face.Edges.Count]],
                                    Edges[eFace.Face.Edges[i]] }; // same as above 

                            }
                            if (eFace.ActiQueue.Count * 2 >= eFace.Active.Count) break; //If all active stubs have been processed 



                        }
                        else if (inPlane && !activeEdge)
                        // here we have to wait for loop to end in order to confirm no active intersection is due
                        // so we just set the flag
                        // also add the pair of edges to an in face intersection queue
                        {

                            planarInt = true;
                            if (preActive)
                            {
                                var missingVert = Vertices[eFace.Face.Edges[(i - 1) < 0 ? eFace.Face.Edges.Count - 1 : i - 1].Vertices[1]]; // this is the missing vert
                                eFace.PlanarQueue.Add(missingVert, new List<EEdge> { Edges[eFace.Face.Edges[(i - 1) < 0 ? eFace.Face.Edges.Count - 1 : i - 1]],
                                    Edges[eFace.Face.Edges[i]] }); // always the active one first

                            }
                            if (postActive)
                            {
                                var missingVert = Vertices[eFace.Face.Edges[(i + 1) % eFace.Face.Edges.Count].Vertices[0]]; // this is the missing vert
                                eFace.PlanarQueue.Add(missingVert, new List<EEdge> { Edges[eFace.Face.Edges[(i + 1) % eFace.Face.Edges.Count]],
                                    Edges[eFace.Face.Edges[i]] }); // always the active one first

                            }

                        }
                    }
                }
                if (!planarInt && !activeInt)
                    nonPlanar.Add(eFace);
                else if (planarInt && !activeInt)
                    planar.Add(eFace);
            }
        }
    }

    /// <summary>
    /// Face extension class for polyCluster transformation
    /// </summary>
    public class EFace
    {
        public PFFace Face { get; set; }
        public Plane Plane { get; set; } = Plane.Unset;
        public HashSet<PFEdge> InPlane { get; set; } = new HashSet<PFEdge>();
        public List<EEdge> Stubs { get; set; } = new List<EEdge>();
        public List<EEdge> Active { get; set; } = new List<EEdge>();
        public Dictionary<EVertex, List<EEdge>> ActiQueue { get; set; } = new Dictionary<EVertex, List<EEdge>>();
        public Dictionary<EVertex, List<EEdge>> PlanarQueue { get; set; } = new Dictionary<EVertex, List<EEdge>>();
        public PfoamRef Coresp { get; } = null;

        public bool Moved { get; set; } = false;

        internal EFace(PFFace face, PfoamRef coresp)
        {
            Face = face;
            Plane = face.GetFacePlane();
            Moved = false;
            Coresp = coresp;
        }


        public override int GetHashCode()
        {
            if (Face == null) return 0;
            return Math.Abs(Face.Id);
        }

        public override string ToString()
        {
            return $"EF_{GetHashCode().ToString()}";
        }




        /// <summary>
        /// Adds an EEdge to a EFace 
        /// This assumes the EEdge is a stub (only one EVertex)
        /// </summary>
        /// <param name="fromEdge"></param>
        internal void AddEdge(EEdge eEdge, bool inPlan)
        {

            // if face is 'new'
            if (Stubs.Count < 1)
            {

                if (inPlan)
                {
                    InPlane = new HashSet<PFEdge>(Face.Edges);
                }
                else
                {
                    var newPlane = Plane;
                    newPlane.Origin = eEdge.StubVertices[0].Position;
                    Plane = newPlane;
                    InPlane.Add(eEdge.Edge);
                }
            }
            else // if face is already altered 
            {
                if (!inPlan)
                {
                    bool vertShare = false; // this is for the special case of adding a 2nd consecutive isolated EEdge to EFace with inPlan set to false 
                    foreach (var actEdg in Active)
                    {
                        if (actEdg.StubVertices.Any(x => x == eEdge.StubVertices.Single())) vertShare = true;
                    }
                    double dist = Plane.DistanceTo(eEdge.StubVertices[0].Position);
                    if (!vertShare && dist > Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                    {
                        //Rhino.RhinoDoc.ActiveDoc.Objects.AddLine(eEdge.Line);
                        //Rhino.RhinoDoc.ActiveDoc.Objects.AddTextDot("L_"+ eEdge.Edge.Id.ToString(), eEdge.Line.To);
                        //throw new PolyFrameworkException($"The added edge ({eEdge.Edge.Id.ToString()}) stub is out of plane for face ({this.Face.Id.ToString()})");
                    }
                }
                InPlane.Add(eEdge.Edge);
            }
            Stubs.Add(eEdge);
            Active.Add(eEdge);
        }

        internal void UpdateEdge(EEdge eEdge)
        {
            // just need to remove from active 
            // the other edge of the EVertex in the face is part of another iteration. 
            Active.Remove(eEdge);
        }



    }
    /// <summary>
    /// Edge extension class for polyCluster transformation  
    /// </summary>
    public class EEdge
    {
        public PFEdge Edge { get; set; } = null;
        public Line Line { get; set; } = Line.Unset;
        public List<EVertex> StubVertices { get; set; } = new List<EVertex>();
        public PfoamRef Coresp { get; } = null;
        public bool Moved { get; set; } = false;
        public bool Flipped { get; set; } = false;
        public override int GetHashCode()
        {
            if (Edge == null) return 0;
            return Math.Abs(Edge.Id);
        }

        public override string ToString()
        {
            return $"EE_{GetHashCode().ToString()}";
        }

        internal EEdge(PFEdge edge, PfoamRef coresp)
        {
            Edge = edge;
            Line = edge.CreateLine();
            Coresp = coresp;
        }


        internal bool AddPoint(EVertex eVert, bool inLine)
        {
            if (StubVertices.Count < 1)
            {
                StubVertices.Add(eVert);
                if (!inLine)
                {
                    if (eVert.Vertex == Edge.Vertices[0]) Line = new Line(eVert.Position, eVert.Position - Edge.GetDirectionVector());
                    else Line = new Line(eVert.Position, eVert.Position + Edge.GetDirectionVector());
                    Moved = true;
                }


                return true;
            }
            else
            {
                if (inLine) 
                {
                    StubVertices.Add(eVert);
                    //update the line maybe 
                }
                else
                {
                    double dist = Line.DistanceTo(eVert.Position, false);
                    if (dist > Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                    {
                        // if vertex is part of a naked edge - create it none the less 
                        // but set a flag for all stubs starting from the point not to propagate any further 

                        //Rhino.RhinoDoc.ActiveDoc.Objects.AddPoint(eVert.Position);
                        ////////////////////////////////////////////////////////////
                        //Rhino.RhinoDoc.ActiveDoc.Objects.AddTextDot(new TextDot($"Dev={dist.ToString("0.000")}", (eVert.Position + StubVertices[0].Position) / 2));
                        ////////////////////////////////////////////////////////////
                        //throw new Exception($"Added point_{eVert.Vertex.Id.ToString()} is not in line! {this.Edge.Id.ToString()} distance is {dist}");
                        // add a point here to see what happens 

                    }

                    StubVertices.Add(eVert);

                }
                if ((StubVertices.Count > 2)) throw new PolyFrameworkException("Too many points in line!");
                //////////////////////////////////////
                // test to see if edge is flipped 
                Line = new Line(StubVertices[0].Position, StubVertices[1].Position);
                var movedVec = Coresp.Vertices[Edge.Vertices[0]].Position - Coresp.Vertices[Edge.Vertices[1]].Position;
                movedVec.Unitize();

                var origVec = Edge.Vertices[0].Point - Edge.Vertices[1].Point;
                origVec.Unitize();
                Flipped = Util.Dot(movedVec, origVec) < 0;

                // test for line transformation 
             


                //Rhino.RhinoDoc.ActiveDoc.Objects.AddLine(StubVertices[0].Position, StubVertices[1].Position);
                //string noUsingThat = "";
                //Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
                //Rhino.Input.RhinoGet.GetString("Next!", true, ref noUsingThat);
                ////////////////////////////////////////
                return false;
            }
        }


        internal EVertex NonPlanarIntersect(bool considerExternal = false)
        {
            var otherVert = Coresp.Vertices[Edge.Vertices.Single(x => x != StubVertices.Single().Vertex)];
            var vertFaces = otherVert.Vertex.Faces.Where(x => considerExternal||!x.External);
            var vertEfaces = new HashSet<EFace>(vertFaces.Select(x => Coresp.Faces[x])); // non external
            var edgeEfaces = Edge.Faces.Select(x => Coresp.Faces[x]);
            var capEfaces = vertEfaces.Except(edgeEfaces).ToList();

            if (capEfaces.Count() == 0)
            {

                return otherVert; 
                // for this return a special consideration is necessary 
                // if othervert has no InFace face specified it means it points to an outside face 

            }

            // now intersect line with all face planes 
            double minPara = double.MaxValue;
            int minParaIndex = 0;
            for (int i = 0; i < capEfaces.Count(); i++)
            {
                var facePlane = capEfaces[i].Plane;
                if (Rhino.Geometry.Intersect.Intersection.LinePlane(Line, facePlane, out double param))
                {
                    if (param < minPara)
                    {
                        minPara = param;
                        minParaIndex = i;
                    }
                }
            }



            otherVert.Position = Line.PointAt(minPara);

            //////////////////////////////////////
            //Rhino.RhinoDoc.ActiveDoc.Objects.AddTextDot(new TextDot($"N_{otherVert.Vertex.Id.ToString()}", otherVert.Position));
            //////////////////////////////////////

            //Rhino.RhinoDoc.ActiveDoc.Objects.AddPoint(otherVert.Position);
            //Rhino.RhinoDoc.ActiveDoc.Objects.AddTextDot(new TextDot($"E_{this.Edge.Id.ToString()}+F_{capEfaces[minParaIndex].Face.Id.ToString()}", otherVert.Position));
            //Rhino.RhinoDoc.ActiveDoc.Objects.AddCircle(new Circle(capEfaces[minParaIndex].Plane, 3.00));
            //Rhino.RhinoDoc.ActiveDoc.Objects.AddLine(this.Line);
            //otherVert.InFaces.Add(capEfaces[minParaIndex]); // this will be the only face the new EVertex is in at first UNTRUE - also in all the faces of the edge 

            //capEFace = capEfaces[minParaIndex];
            otherVert.InEdges.Add(this);
            otherVert.InFaces.Add(capEfaces[minParaIndex]);
            return otherVert;
        }

        internal EVertex DetermineUncalculable(EVertex endVert)
        {
            // use the edge direction and original edge length to create the other end 
            Vector3d direction = Line.To - Line.From;
            endVert.Position = StubVertices.Single().Position + direction * Edge.GetLength();

            endVert.InEdges.Add(this);
            return endVert;
        }


    }


    /// <summary>
    /// Vertex extension class for polyCluster transformation
    /// </summary>
    public class EVertex
    {
        public PFVertex Vertex { get; set; } = null;
        public Point3d Position { get; set; } = Point3d.Unset;
        public HashSet<EFace> InFaces { get; set; } = new HashSet<EFace>();
        public HashSet<EEdge> InEdges { get; set; } = new HashSet<EEdge>();
        public PfoamRef Coresp { get; } = null;
        public bool Parsed { get; set; } = false;
        public bool Moved { get; set; } = true; 
        // TODO have to implement point Moved and Line moved. In planar intersections - if point in edge = > edge not moved .....

        public override int GetHashCode()
        {
            if (Vertex == null) return 0;
            return Vertex.Id;
        }

        public override string ToString()
        {
            return $"EV_{Vertex.Id.ToString()}";
        }

        internal EVertex(PFVertex vertex, PfoamRef coresp)
        {
            Vertex = vertex;
            Position = vertex.Point;
            Coresp = coresp;
        }
        /// <summary>
        /// Updates all edges through point (this is a newly moved point)
        /// Adds the new edges to faces that share them.
        /// Or updates the edges inside the face 
        /// </summary>
        internal HashSet<EFace> UpdateStructure(bool propagateExt = true)
        {
            var facesWithNewEdges = new HashSet<EFace>();
            // do the intersection and update 
            // Inside face ....
            // 1. both active edges get another EVert - become passive 
            // 2. No new edges in the face, stubs or in plane 
            // Outside face 
            // 1. Go through all edges of Evert / all faces of EEdge and add/update - if outside face was transformed already test for coplanarity 
            foreach (var edge in Vertex.Edges)
            {
                if (edge.Id > 0 ) // just need to use one of the pair of edges 
                {
                    bool inEdge = InEdges.Contains(Coresp.Edges[edge]);
                    // if inEdge the point is also in face/in plane 
                    if (Coresp.Edges[edge].AddPoint(this, inEdge)) // if true the edge is 'new'
                    {
                        foreach (var face in edge.Faces) // add to all faces the 'new' edge
                        {
                            if (face.External && face.Pair.External && !propagateExt) continue;
                            // because this depends on user input the different stubs might not be coplanar so we have to check
                            // we know if the vert (edge stub) is not in face at the beginning EFace will get a new plane 
                            // second eventual vert (edge stub) if declared in face will be in fact the new face plane . So we can declare the add edge operation as in plane 
                            bool inFace = InFaces.Contains(Coresp.Faces[face]);
                            Coresp.Faces[face].AddEdge(Coresp.Edges[edge], inEdge || inFace);
                            // now we need to add the face to the return list 
                            facesWithNewEdges.Add(Coresp.Faces[face]);
                        }
                    }
                    else // else the edge is just getting the second vertex 
                    {
                        foreach (var face in edge.Faces) // update all the faces as a result of edge completion 
                        {
                            Coresp.Faces[face].UpdateEdge(Coresp.Edges[edge]);
                        }
                    }
                }
            }

            return facesWithNewEdges;
        }

        internal void PlanarIntersect(List<EEdge> eEdges)
        {
            //bool allMoved = keyVal.Value.All(x => x.Moved);
            bool oneMoved = eEdges.Any(x => x.Moved);
            if (oneMoved) // if any edge is moved - calculate intersection
            {
                Rhino.Geometry.Intersect.Intersection.LineLine(eEdges[0].Line, eEdges[1].Line, out double i1, out double i2);
                Position = eEdges[0].Line.PointAt(i1);

            }
            // else add all the edges in the point if !oneMoved
            else
            {
                foreach (var ed in Vertex.Edges)
                {
                    if (ed.Id > 0)
                    {
                        InEdges.Add(Coresp.Edges[ed]);
                    }
                }
            }



            // the new Vertex Position is in the planes of the faces the 2 edges are part of.
            // So they will not need moving
            foreach (var eEdg in eEdges)
            {
                InEdges.Add(eEdg); // add the 2 edges 
                // This below is not needed anymore 
                //foreach (var face in eEdg.Edge.Faces)
                //{
                //    keyVal.Key.InFaces.Add(coresp.Faces[face]);
                //}
            }
            /////////
            //Rhino.RhinoDoc.ActiveDoc.Objects.AddTextDot(new TextDot($"A_{Vertex.Id.ToString()}", Position));
            /////////
        }


    }


    


    public partial class PFoam
    {

        /* General algorithm work-flow - updated
             * first test the starting vertices to see if they are in the planes going through the topoVertex linked 
             * if within tolerance to 1 plane - use projection to that plane instead (set plane in dictionary) 
             * if within tolerance to 2 planes - use projection to common edge (set edge in dictionary)
             * in within tolerance to 3 or more planes - remove point from list 
             * if not close to any plane then it is a free point
             * 
             * For each initial vertex/point create the moved faces and the moved edge stubs inside of them 
             * 
             * From the set of input vertices/positions 
             * The inputs are assumed coherent - all keep parallelism intact of edges/faces
             * For each input vertex/point create the new offset face plans and offset edge directions
             * Create this for unmoved faces too - 
             * (may need to create 2 hashsets for each face .... one for original and one for moved )
             * Keep a list of affected faces to cycle through 
             * Keep for each face a set of in plan edges that are ready to intersect
             * While (still edges to intersect)
             *      While (still in_face coplanar edges to intersect)
             *      for each face in the list of coplanar ready faces 
             *      If topological intersection other edge is coplanar 
             *      (here prioritize the intersections between the newly offset faces... new x new - over new x old)  
             *      Intersect edges - create point
             *      Use point to move (offset in space) other edges
             *      Based on the initial edge intersection that created the point - 
             *      Add new edge lines as coplanar in faces that also contain one initial edge 
             *          for each face in created point 
             *              if face has old edge and new edge - add new edge in coplanar - also add edge to be computed -  
             *              if face has only new edges and no other coplanar edges previously created create face coplanar hashset - send face to wait list 
             *              if face has only new edges but also already has new coplanar edges and new plane - test for coplanarity before adding - raise exception if not coplanar 
             *              if face has only old initial edges - do nothing 
             *              add new edges to the faces shared with existing initial edges 
             *              (this is to speed the algorithm as there is no need for a co-planarity check)
             *      Add all faces that received new offset edges to the list of faces to be parsed for in plane intersections
             *      (this is to speed the algorithm as there is no need for a co-planarity check)
             *      If there are no more faces with coplanar edges to intersect - break 
             * If no more planar intersections to be made - 
             *      do a free intersection line to plane to complete a face
             *      If no more intersections to be made break main while loop 
             *      At this points all faces should  either have all edges in the offset list 
             * 
             */

        public PfoamRef MoveFaceVertices(IList<PFVertex> verts, IList<Point3d> positions, double tolerance, bool considerExternal = false)
        {
            // first make the lists for full/half ext edges 
            // also keep score of ext vertices

            


            if (verts.Count != positions.Count) throw new PolyFrameworkException("Each vertex needs a position!");
            // this is for keeping score of the transformations in the network without altering it 
            var coresp = new PfoamRef();
            // create empty references for all edges and faces and vertices in coresp (first and pair all point to same entry) 
            foreach (var edge in Edges)
            {
                if (!coresp.Edges.ContainsKey(edge))
                {
                    EEdge eEdge = new EEdge(edge, coresp);
                    coresp.Edges[edge] = eEdge;
                    coresp.Edges[edge.Pair] = eEdge;
                }

            }

            
            foreach (var face in Faces)
            {
                if (!coresp.Faces.ContainsKey(face))
                {
                    EFace eFace = new EFace(face, coresp);
                    coresp.Faces[face] = eFace;
                    coresp.Faces[face.Pair] = eFace;
                }

            }
            foreach (var vert in Vertices)
            {
                EVertex eVert = new EVertex(vert, coresp);
                coresp.Vertices[vert] = eVert;
            }

            // this is for sorting the input vertex/position 
            // first see how close the corresponding topological faces it is to the input position
            // modify position in the coresp dict to be exactly in face/edge/vert if close (see about the updating planes !)
            // each point can modify the face planes of the corresponding faces if it is not in plane. 
            // So each subsequent point will have to be in a potentially updated plane  
            // update all EEdges from the EVert - (new, or update)
            // update all EFace from each EEdge - (test co-planarity for the new EEdges if EFace gets more than 1 Vert/point as input ) 
            // send all modified EFaces to a preWork HashSet 
            // sort preWork faces into 3 categories - Active (1) Planar(2) Non-Planar(3) based on intersection priority 

            var preWorkFaces = new HashSet<EFace>();

            for (int i = 0; i < verts.Count; i++) // for each vertex 
            {
                var closeEFaces = new HashSet<EFace>();
                var closeEEdges = new HashSet<EEdge>();
                foreach (var face in verts[i].Faces)
                {
                    if (face.Id > 0)
                    {
                        // go through only the positive Id faces / half of them 
                        var plDist = Math.Abs(coresp.Faces[face].Plane.DistanceTo(positions[i]));
                        if (plDist < tolerance)
                        {
                            closeEFaces.Add(coresp.Faces[face]);
                        }
                    }
                }
                if (closeEFaces.Count == 1) // test if in plane of a face 
                {
                    coresp.Vertices[verts[i]].Position = closeEFaces.Single().Plane.ClosestPoint(positions[i]);
                }
                else if (closeEFaces.Count == 2) // test if in edge
                {
                    // replace the position with the one of the common edge for the two planes (faces)
                    var closeFacesList = closeEFaces.ToList(); // so it can be looped through
                    var firstEdges = new HashSet<PFEdge>(closeFacesList[0].Face.Edges);
                    bool connectionFound = false;
                    foreach (var edge in closeFacesList[1].Face.Edges)
                    {
                        if (firstEdges.Contains(edge) || firstEdges.Contains(edge.Pair))
                        {
                            coresp.Vertices[verts[i]].Position = edge.CreateLine().ClosestPoint(positions[i], false);
                            closeEEdges.Add(coresp.Edges[edge]);
                            connectionFound = true;
                            break;
                        }
                    }
                    if (!connectionFound) throw new PolyFrameworkException("No common edge found for the two close faces!");
                }
                else if (closeEFaces.Count >= 3) // for sure in line - test number of lines to see if in vertex 
                {
                    
                    foreach (var edge in verts[i].Edges)
                    {
                        if (edge.Id > 0)
                        {
                            var ln = edge.CreateLine();
                            var lnDist = ln.DistanceTo(positions[i], false);
                            if (lnDist < tolerance) closeEEdges.Add(coresp.Edges[edge]);
                        }
                        
                    }
                    // now see if more than 1 eEdge is close to the point
                    if (closeEEdges.Count == 1)
                    {
                        coresp.Vertices[verts[i]].Position = closeEEdges.Single().Line.ClosestPoint(positions[i], false);
                    }
                    else if (closeEEdges.Count > 1) //point == vertexPoint
                    {
                        coresp.Vertices[verts[i]].Position = verts[i].Point;
                        foreach (var face in verts[i].Faces) closeEFaces.Add(coresp.Faces[face]);
                        foreach (var edge in verts[i].Edges) closeEEdges.Add(coresp.Edges[edge]);
                    }


                   
                    
                }
                else // else it is free  
                {
                    coresp.Vertices[verts[i]].Position = positions[i];
                    //nothing to add here 
                }

                coresp.Vertices[verts[i]].InFaces = new HashSet<EFace>(closeEFaces);
                coresp.Vertices[verts[i]].InEdges = new HashSet<EEdge>(closeEEdges);
                coresp.Vertices[verts[i]].Parsed = true;


                // now update the edges and faces for the point 
                foreach (var updatedEFace in coresp.Vertices[verts[i]].UpdateStructure())
                    preWorkFaces.Add(updatedEFace);


            }



            // Now separate the EFaces based on what is going on inside 
            // 1) Active - at least 2 active EEdges (stubs with only one EVertex) that share the missing vertex 
            // 2) Planar - (all active + ?) at least 1 active member (stub with one EVertex) and the intersecting next edge in the InPlan edges 
            // 3) NonPlanar - no active edge has a coplanar edge to intersect (this is the else part) 
            coresp.SortFaces(preWorkFaces, out var activeWf, out var planarWf, out var nonPlanarWf);



            // now that all starting conditions have been set we need to loop through the faces
            // loop through faces with intersecting active stubs _ while still active intersecting stubs exist
            // after each intersection - update affected faces - 
            // look in each affected face if active intersection status changes (update active, planar, non-planar) all active are also planar  
            // if yes add it to active list 
            // remove from active list the completed face 
            // in no more active faces in list - do a planar intersection - and update (update active, planar, non-planar)
            // if no more planar do an non-planar intersection and update (update active, planar, non-planar)
            // when no more faces in non-planar, planar and active  - exit main loop 




            while (true) // main loop - will have exit condition based on lack of faces to loop
            {
                while (true) // active loop - while still active faces to work with 
                {
                    var vertIntDict = new Dictionary<EVertex, List<EEdge>>();
                    foreach (var eFace in activeWf) // this means that some active edges do intersect 
                    {
                        foreach (var keyVal in eFace.ActiQueue)
                        {
                            vertIntDict[keyVal.Key] = keyVal.Value;
                            // this makes sure that duplicate active points will be calculated only once 
                            // if same point from multiple active faces it will be overwritten 
                        }

                    }
                    // Do the intersection for the eventual pairs of active edges 
                    // Put them in the nextFaces 
                    var nextFaces = new HashSet<EFace>();
                    foreach (var keyVal in vertIntDict)
                    {
                        keyVal.Key.PlanarIntersect(keyVal.Value); // this updates the EVert 
                        keyVal.Key.Parsed = true;
                        foreach (var eFace in keyVal.Key.UpdateStructure()) nextFaces.Add(eFace);

                    }
                    // after the previous loop all active faces have been addressed. 
                    activeWf = new HashSet<EFace>();
                    // any other active EFaces will have to come from nextFaces
                    coresp.SortFaces(nextFaces, out var nextActive, out var nextPlanar, out var nextNonPlanar);
                    // merge faces with wfPlanar and wfNonPlanar - some might repeat 

                    foreach (var ef in nextNonPlanar)
                    {
                        // if the planarity condition for the face has changed from !!!

                        nonPlanarWf.Add(ef);
                    }
                    foreach (var ef in nextPlanar)
                    {
                        // if the planarity condition for the face has changed !!!
                        if (nonPlanarWf.Contains(ef)) nonPlanarWf.Remove(ef);
                        planarWf.Add(ef);
                    }
                    if (nextActive.Count == 0)
                    {
                        break; // if no more active we need to try a planar intersection 
                    }
                    else
                    {
                        foreach (var ef in nextActive)
                        {
                            // if the face used to be in planar or nonPlanar and now it is active 
                            if (nonPlanarWf.Contains(ef)) nonPlanarWf.Remove(ef);
                            if (planarWf.Contains(ef)) planarWf.Remove(ef);
                            activeWf.Add(ef);
                        }
                    }
                }
                // do first planar intersection and - continue 
                bool doOther = true;
                while (doOther) // planar loop. After every planar intersection check for new active faces and break if active appears or no more planar faces in list 
                {
                    var tempPlanar = new HashSet<EFace>();
                    var remPlanar = new List<EFace>();
                    foreach (var eFace in planarWf)
                    {
                        remPlanar.Add(eFace);
                        // find intersection 
                        // get and active edge - missing vert - if other edge is in planar - do intersection 
                        var nextFaces = new HashSet<EFace>();
                        foreach (var keyVal in eFace.PlanarQueue)
                        {
                            keyVal.Key.PlanarIntersect(keyVal.Value); // this updates the vertex 
                            keyVal.Key.Parsed = true;
                            foreach (var proEface in keyVal.Key.UpdateStructure()) nextFaces.Add(proEface);

                        }
                        coresp.SortFaces(nextFaces, out var nextActive, out var nextPlanar, out var nextNonPlanar);
                        foreach (var ef in nextNonPlanar)
                        {
                            nonPlanarWf.Add(ef);
                        }
                        foreach (var ef in nextPlanar)
                        {
                            // if the planarity condition for the face has changed !!!
                            if (nonPlanarWf.Contains(ef)) nonPlanarWf.Remove(ef);
                            tempPlanar.Add(ef);
                        }
                        foreach (var ef in nextActive)
                        {
                            // if the face used to be in planar or nonPlanar and now it is active 
                            if (nonPlanarWf.Contains(ef)) nonPlanarWf.Remove(ef);
                            if (tempPlanar.Contains(ef)) tempPlanar.Remove(ef);

                            activeWf.Add(ef);
                        }
                        if (nextActive.Count > 0)
                        {
                            doOther = false;
                            break;
                            // break from forLoop and from planar whileLoop 
                        }
                    }
                    // if loop broken early by presence of active - clean the parsed planars and add the temp planar before leaving 
                    if (!doOther) planarWf.RemoveWhere(x => remPlanar.Contains(x));
                    // if loop finishes normally clean all planarWf before adding the tempPlanar faces
                    else planarWf = new HashSet<EFace>();
                    // replenish planarWF with temp ones 
                    foreach (var ef in tempPlanar) planarWf.Add(ef);
                    // remove any active ones 
                    foreach (var ef in activeWf) if (planarWf.Contains(ef)) planarWf.Remove(ef);
                    // see if no more planars to work with 
                    if (planarWf.Count == 0) break;
                }

                // do first non planar intersection and - continue 
                // if we got here both actvieWf and planaWf are empty
                // first set up a dictionary for the un-calculable stubs 
                // vertex , edge 
                // at the end go through all dict and if vertex is still not parsed - calculate from dict

                var unCalculable = new Dictionary<EVertex, EEdge>();

                while (doOther)
                {
                    var tempNonPlanar = new HashSet<EFace>();
                    var remNonPlanar = new HashSet<EFace>();

                    // here depending on the type of polyhedra the order might be important 
                    foreach (var eFace in nonPlanarWf)
                    {
                        remNonPlanar.Add(eFace);
                        foreach (var eEdge in eFace.Active)
                        {
                            var eVrt = eEdge.NonPlanarIntersect(considerExternal);
                            if (eVrt.InFaces.Count == 0)
                            {
                                // if there is no capFace to get the point from
                                unCalculable[eVrt] = eEdge; 
                                continue;
                            }
                            eVrt.Parsed = true;

                            
                            // before updating add cap face and edge faces to the  inFaces list

                            //foreach (var face in eEdge.Edge.Faces) eVrt.InFaces.Add(coresp.Faces[face]);
                            //eVrt.InFaces.Add(capFace);
                            var nextFaces = eVrt.UpdateStructure();
                            coresp.SortFaces(nextFaces, out var nextActive, out var nextPlanar, out var nextNonPlanar);
                            // there should be at least 3 EFaces with planar intersections resulting from here 

                            foreach (var ef in nextNonPlanar)
                            {
                                tempNonPlanar.Add(ef);
                            }
                            foreach (var ef in nextPlanar)
                            {
                                // if the planarity condition for the face has changed !!!
                                if (nonPlanarWf.Contains(ef)) remNonPlanar.Add(ef);
                                planarWf.Add(ef);
                            }
                            foreach (var ef in nextActive)
                            {
                                // if the face used to be in planar or nonPlanar and now it is active 
                                if (nonPlanarWf.Contains(ef)) remNonPlanar.Add(ef);
                                if (planarWf.Contains(ef)) planarWf.Remove(ef);

                                activeWf.Add(ef);
                            }
                            if (nextPlanar.Count > 0 || nextActive.Count > 0)
                            {
                                // exit up to the while loop 
                                doOther = false;
                                break;

                            }
                            if (nextPlanar.Count == 0) break;
                        }
                        if (!doOther) break;
                    }
                    // remove nonPlanars these will include here the faces that changed from nonPlana to planar or active 
                    nonPlanarWf.RemoveWhere(x => remNonPlanar.Contains(x));

                    foreach (var ef in tempNonPlanar)
                    {
                        nonPlanarWf.Add(ef);
                    }

                    if (nonPlanarWf.Count == 0) break;
                }
                // add try to resolve the uncalculable vertices if any remain 
                if (nonPlanarWf.Count + planarWf.Count + activeWf.Count == 0)
                {
                    foreach (var keyVal in unCalculable)
                    {
                        if (!keyVal.Key.Parsed)
                        {
                            keyVal.Value.DetermineUncalculable(keyVal.Key);
                            keyVal.Key.UpdateStructure();
                        }
                       
                    }
                    break;
                }
                    



            }
            //Line[] result = new Line[coresp.Edges.Values.Count];
            //bool[] flipped = new bool[coresp.Edges.Values.Count];
            //result = coresp.Edges.Values.Select(x => new Line(x.StubVertices[0].Position, x.StubVertices[1].Position)).ToArray();

            return coresp;


        }


        public PfoamRef MoveFaceVertices_Form(IList<PFVertex> verts, IList<Point3d> positions, double tolerance, bool considerExternal = false)
        {
            // first make the lists for full/half ext edges 
            // also keep score of ext vertices

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


            if (verts.Count != positions.Count) throw new PolyFrameworkException("Each vertex needs a position!");
            // this is for keeping score of the transformations in the network without altering it 
            var coresp = new PfoamRef();
            // create empty references for all edges and faces and vertices in coresp (first and pair all point to same entry) 
            foreach (var edge in Edges)
            {
                if (!coresp.Edges.ContainsKey(edge))
                {
                    EEdge eEdge = new EEdge(edge, coresp);
                    coresp.Edges[edge] = eEdge;
                    coresp.Edges[edge.Pair] = eEdge;
                }

            }


            foreach (var face in Faces)
            {
                if (!coresp.Faces.ContainsKey(face))
                {
                    EFace eFace = new EFace(face, coresp);
                    coresp.Faces[face] = eFace;
                    coresp.Faces[face.Pair] = eFace;
                }

            }
            foreach (var vert in Vertices)
            {
                EVertex eVert = new EVertex(vert, coresp);
                coresp.Vertices[vert] = eVert;
            }

            // this is for sorting the input vertex/position 
            // first see how close the corresponding topological faces it is to the input position
            // modify position in the coresp dict to be exactly in face/edge/vert if close (see about the updating planes !)
            // each point can modify the face planes of the corresponding faces if it is not in plane. 
            // So each subsequent point will have to be in a potentially updated plane  
            // update all EEdges from the EVert - (new, or update)
            // update all EFace from each EEdge - (test co-planarity for the new EEdges if EFace gets more than 1 Vert/point as input ) 
            // send all modified EFaces to a preWork HashSet 
            // sort preWork faces into 3 categories - Active (1) Planar(2) Non-Planar(3) based on intersection priority 

            var preWorkFaces = new HashSet<EFace>();

            for (int i = 0; i < verts.Count; i++) // for each vertex 
            {
                var closeEFaces = new HashSet<EFace>();
                var closeEEdges = new HashSet<EEdge>();
                foreach (var face in verts[i].Faces)
                {
                    if (face.Id > 0)
                    {
                        // go through only the positive Id faces / half of them 
                        var plDist = Math.Abs(coresp.Faces[face].Plane.DistanceTo(positions[i]));
                        if (plDist < tolerance)
                        {
                            closeEFaces.Add(coresp.Faces[face]);
                        }
                    }
                }
                if (closeEFaces.Count == 1) // test if in plane of a face 
                {
                    coresp.Vertices[verts[i]].Position = closeEFaces.Single().Plane.ClosestPoint(positions[i]);
                }
                else if (closeEFaces.Count == 2) // test if in edge
                {
                    // replace the position with the one of the common edge for the two planes (faces)
                    var closeFacesList = closeEFaces.ToList(); // so it can be looped through
                    var firstEdges = new HashSet<PFEdge>(closeFacesList[0].Face.Edges);
                    bool connectionFound = false;
                    foreach (var edge in closeFacesList[1].Face.Edges)
                    {
                        if (firstEdges.Contains(edge) || firstEdges.Contains(edge.Pair))
                        {
                            coresp.Vertices[verts[i]].Position = edge.CreateLine().ClosestPoint(positions[i], false);
                            closeEEdges.Add(coresp.Edges[edge]);
                            connectionFound = true;
                            break;
                        }
                    }
                    if (!connectionFound) throw new PolyFrameworkException("No common edge found for the two close faces!");
                }
                else if (closeEFaces.Count >= 3) // for sure in line - test number of lines to see if in vertex 
                {

                    foreach (var edge in verts[i].Edges)
                    {
                        if (edge.Id > 0)
                        {
                            var ln = edge.CreateLine();
                            var lnDist = ln.DistanceTo(positions[i], false);
                            if (lnDist < tolerance) closeEEdges.Add(coresp.Edges[edge]);
                        }

                    }
                    // now see if more than 1 eEdge is close to the point
                    if (closeEEdges.Count == 1)
                    {
                        coresp.Vertices[verts[i]].Position = closeEEdges.Single().Line.ClosestPoint(positions[i], false);
                    }
                    else if (closeEEdges.Count > 1) //point == vertexPoint
                    {
                        coresp.Vertices[verts[i]].Position = verts[i].Point;
                        foreach (var face in verts[i].Faces) closeEFaces.Add(coresp.Faces[face]);
                        foreach (var edge in verts[i].Edges) closeEEdges.Add(coresp.Edges[edge]);
                    }




                }
                else // else it is free  
                {
                    coresp.Vertices[verts[i]].Position = positions[i];
                    //nothing to add here 
                }

                coresp.Vertices[verts[i]].InFaces = new HashSet<EFace>(closeEFaces);
                coresp.Vertices[verts[i]].InEdges = new HashSet<EEdge>(closeEEdges);
                coresp.Vertices[verts[i]].Parsed = true;


                // now update the edges and faces for the point 
                foreach (var updatedEFace in coresp.Vertices[verts[i]].UpdateStructure(false))
                    preWorkFaces.Add(updatedEFace);


            }



            // Now separate the EFaces based on what is going on inside 
            // 1) Active - at least 2 active EEdges (stubs with only one EVertex) that share the missing vertex 
            // 2) Planar - (all active + ?) at least 1 active member (stub with one EVertex) and the intersecting next edge in the InPlan edges 
            // 3) NonPlanar - no active edge has a coplanar edge to intersect (this is the else part) 
            coresp.SortFaces(preWorkFaces, out var activeWf, out var planarWf, out var nonPlanarWf);



            // now that all starting conditions have been set we need to loop through the faces
            // loop through faces with intersecting active stubs _ while still active intersecting stubs exist
            // after each intersection - update affected faces - 
            // look in each affected face if active intersection status changes (update active, planar, non-planar) all active are also planar  
            // if yes add it to active list 
            // remove from active list the completed face 
            // in no more active faces in list - do a planar intersection - and update (update active, planar, non-planar)
            // if no more planar do an non-planar intersection and update (update active, planar, non-planar)
            // when no more faces in non-planar, planar and active  - exit main loop 




            while (true) // main loop - will have exit condition based on lack of faces to loop
            {
                while (true) // active loop - while still active faces to work with 
                {
                    var vertIntDict = new Dictionary<EVertex, List<EEdge>>();
                    foreach (var eFace in activeWf) // this means that some active edges do intersect 
                    {
                        foreach (var keyVal in eFace.ActiQueue)
                        {
                            vertIntDict[keyVal.Key] = keyVal.Value;
                            // this makes sure that duplicate active points will be calculated only once 
                            // if same point from multiple active faces it will be overwritten 
                        }

                    }
                    // Do the intersection for the eventual pairs of active edges 
                    // Put them in the nextFaces 
                    var nextFaces = new HashSet<EFace>();
                    foreach (var keyVal in vertIntDict)
                    {
                        keyVal.Key.PlanarIntersect(keyVal.Value); // this updates the EVert 
                        keyVal.Key.Parsed = true;
                        foreach (var eFace in keyVal.Key.UpdateStructure(false)) nextFaces.Add(eFace); // here propagation to external vertices is not permitted 

                    }
                    // after the previous loop all active faces have been addressed. 
                    activeWf = new HashSet<EFace>();
                    // any other active EFaces will have to come from nextFaces
                    coresp.SortFaces(nextFaces, out var nextActive, out var nextPlanar, out var nextNonPlanar);
                    // merge faces with wfPlanar and wfNonPlanar - some might repeat 

                    foreach (var ef in nextNonPlanar)
                    {
                        // if the planarity condition for the face has changed from !!!

                        nonPlanarWf.Add(ef);
                    }
                    foreach (var ef in nextPlanar)
                    {
                        // if the planarity condition for the face has changed !!!
                        if (nonPlanarWf.Contains(ef)) nonPlanarWf.Remove(ef);
                        planarWf.Add(ef);
                    }
                    if (nextActive.Count == 0)
                    {
                        break; // if no more active we need to try a planar intersection 
                    }
                    else
                    {
                        foreach (var ef in nextActive)
                        {
                            // if the face used to be in planar or nonPlanar and now it is active 
                            if (nonPlanarWf.Contains(ef)) nonPlanarWf.Remove(ef);
                            if (planarWf.Contains(ef)) planarWf.Remove(ef);
                            activeWf.Add(ef);
                        }
                    }
                }
                // do first planar intersection and - continue 
                bool doOther = true;
                while (doOther) // planar loop. After every planar intersection check for new active faces and break if active appears or no more planar faces in list 
                {
                    var tempPlanar = new HashSet<EFace>();
                    var remPlanar = new List<EFace>();
                    foreach (var eFace in planarWf)
                    {
                        remPlanar.Add(eFace);
                        // find intersection 
                        // get and active edge - missing vert - if other edge is in planar - do intersection 
                        var nextFaces = new HashSet<EFace>();
                        foreach (var keyVal in eFace.PlanarQueue)
                        {
                            keyVal.Key.PlanarIntersect(keyVal.Value); // this updates the vertex 
                            keyVal.Key.Parsed = true;
                            foreach (var proEface in keyVal.Key.UpdateStructure(false)) nextFaces.Add(proEface); //here propagation to external vertices is not permitted 

                        }
                        coresp.SortFaces(nextFaces, out var nextActive, out var nextPlanar, out var nextNonPlanar);
                        foreach (var ef in nextNonPlanar)
                        {
                            nonPlanarWf.Add(ef);
                        }
                        foreach (var ef in nextPlanar)
                        {
                            // if the planarity condition for the face has changed !!!
                            if (nonPlanarWf.Contains(ef)) nonPlanarWf.Remove(ef);
                            tempPlanar.Add(ef);
                        }
                        foreach (var ef in nextActive)
                        {
                            // if the face used to be in planar or nonPlanar and now it is active 
                            if (nonPlanarWf.Contains(ef)) nonPlanarWf.Remove(ef);
                            if (tempPlanar.Contains(ef)) tempPlanar.Remove(ef);

                            activeWf.Add(ef);
                        }
                        if (nextActive.Count > 0)
                        {
                            doOther = false;
                            break;
                            // break from forLoop and from planar whileLoop 
                        }
                    }
                    // if loop broken early by presence of active - clean the parsed planars and add the temp planar before leaving 
                    if (!doOther) planarWf.RemoveWhere(x => remPlanar.Contains(x));
                    // if loop finishes normally clean all planarWf before adding the tempPlanar faces
                    else planarWf = new HashSet<EFace>();
                    // replenish planarWF with temp ones 
                    foreach (var ef in tempPlanar) planarWf.Add(ef);
                    // remove any active ones 
                    foreach (var ef in activeWf) if (planarWf.Contains(ef)) planarWf.Remove(ef);
                    // see if no more planars to work with 
                    if (planarWf.Count == 0) break;
                }

                // do first non planar intersection and - continue 
                // if we got here both actvieWf and planaWf are empty
                // first set up a dictionary for the un-calculable stubs 
                // vertex , edge 
                // at the end go through all dict and if vertex is still not parsed - calculate from dict

                var unCalculable = new Dictionary<EVertex, EEdge>();

                while (doOther)
                {
                    var tempNonPlanar = new HashSet<EFace>();
                    var remNonPlanar = new HashSet<EFace>();

                    // here depending on the type of polyhedra the order might be important 
                    foreach (var eFace in nonPlanarWf)
                    {
                        remNonPlanar.Add(eFace);
                        foreach (var eEdge in eFace.Active)
                        {
                            var eVrt = eEdge.NonPlanarIntersect(considerExternal);
                            if (eVrt.InFaces.Count == 0)
                            {
                                // if there is no capFace to get the point from
                                unCalculable[eVrt] = eEdge;
                                continue;
                            }
                            eVrt.Parsed = true;


                            // before updating add cap face and edge faces to the  inFaces list

                            //foreach (var face in eEdge.Edge.Faces) eVrt.InFaces.Add(coresp.Faces[face]);
                            //eVrt.InFaces.Add(capFace);
                            var nextFaces = eVrt.UpdateStructure(false); // here propagation to external vertices is not permitted 
                            coresp.SortFaces(nextFaces, out var nextActive, out var nextPlanar, out var nextNonPlanar);
                            // there should be at least 3 EFaces with planar intersections resulting from here 

                            foreach (var ef in nextNonPlanar)
                            {
                                tempNonPlanar.Add(ef);
                            }
                            foreach (var ef in nextPlanar)
                            {
                                // if the planarity condition for the face has changed !!!
                                if (nonPlanarWf.Contains(ef)) remNonPlanar.Add(ef);
                                planarWf.Add(ef);
                            }
                            foreach (var ef in nextActive)
                            {
                                // if the face used to be in planar or nonPlanar and now it is active 
                                if (nonPlanarWf.Contains(ef)) remNonPlanar.Add(ef);
                                if (planarWf.Contains(ef)) planarWf.Remove(ef);

                                activeWf.Add(ef);
                            }
                            if (nextPlanar.Count > 0 || nextActive.Count > 0)
                            {
                                // exit up to the while loop 
                                doOther = false;
                                break;

                            }
                            if (nextPlanar.Count == 0) break;
                        }
                        if (!doOther) break;
                    }
                    // remove nonPlanars these will include here the faces that changed from nonPlana to planar or active 
                    nonPlanarWf.RemoveWhere(x => remNonPlanar.Contains(x));

                    foreach (var ef in tempNonPlanar)
                    {
                        nonPlanarWf.Add(ef);
                    }

                    if (nonPlanarWf.Count == 0) break;
                }
                // add resolve all exterior vertices  - just need to update them in the coresp dicts 
                if (nonPlanarWf.Count + planarWf.Count + activeWf.Count == 0)
                {
                    foreach (var edge in halfExtEdges)
                    {
                        
                        Vector3d lineVec = edge.Vertices[1].Point - edge.Vertices[0].Point;
                        if (edge.Vertices[0].External)
                        {
                            // get the point that is internal - find its moved value from the coresp
                            coresp.Vertices[edge.Vertices[0]].Position = coresp.Vertices[edge.Vertices[1]].Position - lineVec;
                        }
                        else
                        {
                            coresp.Vertices[edge.Vertices[1]].Position = coresp.Vertices[edge.Vertices[0]].Position + lineVec;

                        }
                        coresp.Edges[edge].StubVertices = new List<EVertex> { coresp.Vertices[edge.Vertices[0]], coresp.Vertices[edge.Vertices[1]] };

                        coresp.Edges[edge].Line = new Line(coresp.Vertices[edge.Vertices[0]].Position, coresp.Vertices[edge.Vertices[1]].Position);
                    }
                    foreach (var edge in fullExtEdges)
                    {
                        coresp.Edges[edge].Line = new Line(coresp.Vertices[edge.Vertices[0]].Position, coresp.Vertices[edge.Vertices[1]].Position);
                        coresp.Edges[edge].StubVertices = new List<EVertex> { coresp.Vertices[edge.Vertices[0]], coresp.Vertices[edge.Vertices[1]] };
                    }




                    break;
                }




            }
            //Line[] result = new Line[coresp.Edges.Values.Count];
            //bool[] flipped = new bool[coresp.Edges.Values.Count];
            //result = coresp.Edges.Values.Select(x => new Line(x.StubVertices[0].Position, x.StubVertices[1].Position)).ToArray();

            return coresp;


        }
    }
}
