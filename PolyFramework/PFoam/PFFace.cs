using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using System.Diagnostics;
using System.Runtime.Serialization;
using System;
using System.Web.Script.Serialization;
using static PolyFramework.Util;
using Rhino.Collections;

namespace PolyFramework

{
    [Serializable]
    [DataContract(IsReference = true)]
    public class PFFace
    {
        /// <summary>
        /// Face Id is an integer positive or negative for the pair (cannot be 0 if face is initialized)
        /// </summary>
        /// 
        [DataMember]
        public int Id { get; set; } = 0;
        [DataMember]
        public IList<PFVertex> Vertices { get; set; } = new List<PFVertex>();
        [DataMember]
        public IList<PFEdge> Edges { get; set; } = new List<PFEdge>();
        [DataMember]
        public IList<PFFace> Neighbors { get; set; } = new List<PFFace>();
        [DataMember]
        public PFCell Cell { get; set; } = null;
        [DataMember]
        public PFFace Pair { get; set; } = null;
        [DataMember]
        public Vector3d Normal { get; set; } = Vector3d.Unset;
        [DataMember]
        public Point3d Centroid { get; set; } = Point3d.Unset;

        public Mesh FMesh { get; set; } = new Mesh();
        [DataMember]
        public PFEdge Dual { get; set; } = null;
        [DataMember]
        public double Area { get; set; } = 0.0;
        public double TargetArea { get; set; } = double.NaN;
        /// <summary>
        /// Refers to the position of the face in the foam/structure - usually applies to the dual 
        /// it will be external if the face is incomplete and sticks outside of the primal external cell. 
        /// </summary>
        /// 
        [DataMember]
        public bool External { get; set; } = false;

        public double InfluenceCoef { get; set; } = 1.0;
        //public System.Drawing.Color Color { get; set; } = System.Drawing.Color.AliceBlue;
        public bool Picked { get; set; } = false;
        public List<double> VertPlanDeviations { get; set; } = new List<double>();
        public Plane FacePlane { get; set; } = Plane.Unset;
        public bool Planarized { get; set; } = false;
        // test for planarity ...
        // test if vertices are neighbors ... form edges - maybe inside the foam object 


        public PFFace()
        {

        }

        public PFFace(int id)
        {
            Id = id;
        }

        public PFFace(int id, IList<PFVertex> vertices)
        {
            Id = id;
            Vertices = new List<PFVertex>(vertices);
        }


        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;
            var otherFace = obj as PFFace;
            return Id == otherFace.Id;
        }

        public override int GetHashCode()
        {
            return Id;

        }


        /// <summary>
        /// creates a mesh patch corresponding to the cell
        /// also sets the area of the polygon 
        /// </summary>
        public void FaceMesh()
        {
            // pick one vertex and create triangles with all the other pairs 
            double area = 0;
            Mesh faceMesh = new Mesh();
            
            faceMesh.Vertices.AddVertices(Vertices.Select(x => x.Point));
            ArchivableDictionary vertDict = new ArchivableDictionary();
            for (int i = 0; i < Vertices.Count; i++) vertDict.Set(i.ToString(), Vertices[i].Id);
            faceMesh.UserDictionary.Set("VertexIds", vertDict);
            faceMesh.UserDictionary.Set("Id", Id);
            //this.FMesh.Vertices.Add(Centroid);
            for (int v = 1; v < faceMesh.Vertices.Count - 1; v++)
            {
                // create faces..
                faceMesh.Faces.AddFace(0, v, v + 1);
                Vector3d v1 = Vertices[v].Point - Vertices[0].Point;
                Vector3d v2 = Vertices[v + 1].Point - Vertices[0].Point;
                double faceArea = System.Math.Abs(Vector3d.CrossProduct(v1, v2).Length / 2);
                area = area + faceArea;
            }


            if (Vertices.Count > 3)
            {
                faceMesh.Ngons.AddNgon(MeshNgon.Create
                    (Enumerable.Range(0, Vertices.Count).ToList(), 
                    Enumerable.Range(0, faceMesh.Faces.Count).ToList()));
            }


            FMesh = faceMesh.DuplicateMesh();
            
            //FMesh.Normals.ComputeNormals();
            //FMesh.FaceNormals.ComputeFaceNormals();

            /*
            for (int n=0; n<FMesh.Vertices.Count; n++)
            {
                FMesh.Normals.SetNormal(n, Normal);
            }
            for (int fn = 0; fn < FMesh.FaceNormals.Count; fn++)
            {
                FMesh.FaceNormals.SetFaceNormal(fn, Normal);
            }
            */


            // check face normal against the PFFace normal and reverse if necessary
            /*
            for (int i=0; i<FMesh.FaceNormals.Count; i++)
            {
                if ((FMesh.FaceNormals[i] - Normal).Length > FMesh.FaceNormals[i].Length)
                {
                    Vector3f reverse = FMesh.FaceNormals[i];
                    reverse.Reverse();
                    FMesh.FaceNormals.SetFaceNormal(i, reverse);
                    
                }
            }
            if ((FMesh.FaceNormals[0] - Normal).Length > FMesh.FaceNormals[0].Length)
            {
                FMesh.Flip(true, true, true);

            }
            */
            Area = area;

        }







        /// <summary>
        /// Computes the face normal using the right hand rule and the
        /// order of the sorted edges 
        /// </summary>
        public void ComputeFaceNormal()
        {
            Vector3d v1 = new Vector3d(Edges[0].Vertices[0].Point - Edges[0].Vertices[1].Point);
            Vector3d v2 = new Vector3d(Edges[1].Vertices[0].Point - Edges[1].Vertices[1].Point);

            Vector3d normal = Vector3d.CrossProduct(v1, v2);
            normal.Unitize();
            this.Normal = normal;

        }
        /// <summary>
        /// Sorts the edges and vertexes inside of a face.
        /// Exchanges all ill-aligned edges with their pair  
        /// </summary>
        public bool SortParts()
        {
            List<PFVertex> sortedVerts = new List<PFVertex>();
            List<PFEdge> sortedEdges = new List<PFEdge>();
            var workEdge = Edges[0];
            var workVert = Edges[0].Vertices[0];

            while (true)
            {
                //this;
                //Debug.Print(this.Id.ToString());
                sortedVerts.Add(workVert);
                sortedEdges.Add(workEdge);
                if (sortedVerts.Count == Vertices.Count)
                    break;

                //workVert = workEdge.Vertices.Single(x => x != workVert);
                if (workVert == workEdge.Vertices[1])
                {
                    BakeErrorGeo(new List<GeometryBase> { new Point(workVert.Point) }, new List<String> { "ErrorEdge" });
                    //throw new PolyFrameworkException("Edge is ill-aligned !!!");
                    return false;
                    
                }

                workVert = workEdge.Vertices[1];

                //workEdge = workVert.Edges.Single(x => Edges.Contains(x) && x != workEdge);

                // condition edge should be in the face (both vertexes in the face) and should have the work-vertex as first vertex
                // also in should not be equal to the current workedge. 
                try
                {
                    workEdge = workVert.Edges.Single(x => x.Vertices[0] == workVert && Vertices.Contains(x.Vertices[1]) && x != workEdge.Pair && x != workEdge);
                }
                catch (System.InvalidOperationException)
                {
                    BakeErrorGeo(Edges.Select(x => x.CreateLine().ToNurbsCurve()), Edges.Select(y => $"Edge_{y.Id.ToString()}_Face_{Id.ToString()}"));
                    return false;
                    //throw new PolyFrameworkException(@"Could not sort elements inside face. Some elements might be smaller than the specified tolerance and thus disappear or some faces have subdivisions", e);
                }



                workEdge = workEdge.Vertices[0] == workVert ? workEdge : workEdge.Pair;

                // look at the equality comparer of PFVertes and PFEdge 
                // 
                // take last vert from sortedVerts - get first edge from .Edges if sorted edge list is empty 
                // take vert from edge.Vertexes that not equal to last vert from list and add to sorted list ...
                //Debug.Print("Looping");
            }
            // now use sort to sort points in edges 

            Vertices = new List<PFVertex>(sortedVerts);
            Edges = new List<PFEdge>(sortedEdges);
            ComputeFaceNormal();
            //FaceMesh();
            CreatePair();

            return true;

        }
        /// <summary>
        /// This is not used !!!!!!!!1
        /// </summary>
        public void SortEdgesDual()
        {
            var sortedEdges = new List<PFEdge>
            {
                Edges[0]
            };
            Edges.RemoveAt(0);
            while (Edges.Count > 0)
            {

                int removal = -1;
                for (int e = 0; e < Edges.Count; e++)
                {
                    if (Edges[e].Vertices[0] == sortedEdges[sortedEdges.Count - 1].Vertices[1])
                    {
                        sortedEdges.Add(Edges[e]);
                        removal = e;
                        break;
                    }
                }
                if (removal > -1)
                {
                    Edges.RemoveAt(removal);
                }



            }

            Edges = new List<PFEdge>(sortedEdges);
        }


        public override string ToString()
        {
            string face =
                $"\nFace Id = {Id.ToString()}"; //\nVertexes =\n{Vertices.Select(x => x.ToString()).Aggregate((i, j) => (i +"\n"+ j)) }\nEdges =\n{Edges.Select(x => x.ToString()).Aggregate((i, j) => (i + "\n" + j)) }";


            return face;
        }
        /// <summary>
        /// Creates the pair for the face
        /// Reverse the edge list and replace all edges with their pair 
        /// Reverse vertex list.  
        /// Add pair face to the edges and to the vertexes
        /// </summary>
        public void CreatePair()
        {
            PFFace pair = new PFFace(-Id)
            {
                Vertices = this.Vertices.Reverse().ToList()
            };
            foreach (var vert in pair.Vertices) vert.Faces.Add(pair);
            pair.Edges = this.Edges.Select(x => x.Pair).Reverse().ToList();
            //foreach (var edge in pair.Edges) edge.Faces.Add(pair); // this needs to be done separately after 
            if (Normal != Vector3d.Unset) pair.Normal = -Normal;
            if (Centroid != Point3d.Unset) pair.Centroid = Centroid;
            //pair.FMesh = this.FMesh.DuplicateMesh();

            //pair.FMesh?.Flip(true, true, true);
            pair.Pair = this;

            Pair = pair;

        }

        public void ComputeCentroid()
        {
            var centroid = new Point3d(x: Vertices.Select(c => c.Point.X).Average(),
                    y: Vertices.Select(c => c.Point.Y).Average(),
                    z: Vertices.Select(c => c.Point.Z).Average());

            Centroid = centroid;
        }

        /// <summary>
        /// Computes the approximate area of a face
        /// Does not need a planar face. Not the same algorithm like the fMesh computing one 
        /// </summary>
        public void ComputeArea()
        {
            double totalArea = 0.0;
            List<Vector3d> vecList = Vertices.Select(x => x.Point - Centroid).ToList();
            for (int v = 0; v < vecList.Count; v++)
            {
                double faceArea = System.Math.Abs(Vector3d.CrossProduct(vecList[v], vecList[(v + 1) % vecList.Count]).Length / 2);
                totalArea += faceArea;
            }
            Area = totalArea;
        }

        public Brep CreateBrep()
        {
            List<Point3d> curvePts = Vertices.Select(x => x.Point).ToList();
            //var brpPoints = curvePts.Select(x => new Point(x)).ToList();
            curvePts.Add(curvePts[0]);


            Polyline pl = new Polyline(curvePts);
            Curve plcrv = pl.ToNurbsCurve();
            var result = Brep.CreatePlanarBreps(plcrv, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

            if (result != null && result.Length == 1)
            {
                //result[0].SetVertices();
                return result[0];
            } 
            if (Vertices.Count == 4)
            {
                var bFace = Brep.CreateFromCornerPoints(Vertices[0].Point,
                Vertices[1].Point, Vertices[2].Point, Vertices[3].Point, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                //bFace.SetVertices();
                return bFace;
            }
                
            if (Vertices.Count == 3)
            {
                var bFace = Brep.CreateFromCornerPoints(Vertices[0].Point,
                Vertices[1].Point, Vertices[2].Point, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                //bFace.SetVertices();
                return bFace;
            }
                
            ComputeCentroid();

            var brepFace = Brep.CreatePatch(new List<GeometryBase>() { plcrv, new Point(Centroid) }, 5, 5, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance/1000);
            //var brepFace = Brep.CreateFromMesh(FMesh, true);

            
            //brpPoints.Add(new Point(Centroid));
            
            //brepFace.SetVertices();
            return brepFace;

            /*
            else
            {
                Brep faceBrep = new Brep();
                ComputeCentroid();
                for (int i = 0; i < Vertices.Count; i++)
                {
                    faceBrep.Append(Brep.CreateFromCornerPoints(Centroid, Vertices[i].Point, Vertices[(i + 1) % Vertices.Count].Point,
                        Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance));
                }
                faceBrep.JoinNakedEdges(Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                return faceBrep;
            }
            
            
            */

        }


        public Brep CreateBrepMatched()
        {
            var faceBrep = CreateBrep();
            var faceBPC = new PointCloud(faceBrep.Vertices.Select(x => x.Location));

            

            foreach (var vert in Vertices)
            {
                var faceBrVertIndex = faceBPC.ClosestPoint(vert.Point);
                var dist = vert.Point.DistanceTo(faceBPC[faceBrVertIndex].Location);

                if (dist > Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                {
                    faceBrep.Vertices.Add(vert.Point, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                    faceBrVertIndex = faceBrep.Vertices.Count - 1;
                    //BakeErrorGeo(new List<GeometryBase> { faceBrep, new Point(vert.Point) }, new List<String> { $"face_{Id}", $"vert_{vert.Id}" });
                    //throw new PolyFrameworkException($"Cannot match a FaceBrep Vertex to Face Vertex {vert.Id} for face {Id}.");   
                }

                faceBrep.Vertices[faceBrVertIndex].UserDictionary.Set("Id", vert.Id);

            }
            faceBrep.UserDictionary.Set("Id", Id);



            return faceBrep;
        }


        /// <summary>
        /// Extracts the vertex push directions for the face.
        /// The face needs to be part of a cell.
        /// The directions are calculated from the edges connecting
        /// into the face that are also part of the cell.
        /// </summary>
        /// <returns></returns>
        public Dictionary<PFVertex, List<PFEdge>> GetVertexPushDirections()
        {
            if (Cell == null) throw new PolyFrameworkException("To get face face/vertex push directions you need a cell to be set in the face!");

            // find the edges in the cell that have exactly only one point in the face 
            // test for duplicate edges - pairs ?? This is not really necessary 
            // get edge vectors 
            // test vectors for loose alignment with the face normal 
            // reverse vectors if necessary 

            var faceVertHash = new HashSet<PFVertex>(Vertices);
            var cellEdgeHash = new HashSet<PFEdge>(Cell.Edges);
            var vertPushdirDict = new Dictionary<PFVertex, Vector3d>();
            var vertDirEdges = new Dictionary<PFVertex, List<PFEdge>>();

            foreach (var vert in Vertices)
            {
                vertDirEdges[vert] = new List<PFEdge>();
                foreach (var edge in vert.Edges)
                {
                    if (edge.Id > 0 && !faceVertHash.Contains(edge.Vertices.Single(x => x != vert)))
                    {
                        vertDirEdges[vert].Add(edge);
                    }

                }
            }

            /*
            foreach(var edge in Cell.Edges)
            {
                // need to use to individual test instead of xor 
                // to get the vertex in the face from the edge and the edge 
                if (faceVertHash.Contains(edge.Vertices[0]) && !faceVertHash.Contains(edge.Vertices[1]))
                {
                    vertPushdirDict.Add(edge.Vertices[0], edge.GetDirectionVector());
                }
                if (faceVertHash.Contains(edge.Vertices[1]) && !faceVertHash.Contains(edge.Vertices[0]))
                {
                    vertPushdirDict.Add(edge.Vertices[1], -edge.GetDirectionVector());
                }
            }
            */
            return vertDirEdges;

        }


        public Plane GetFacePlane()
        {
            return new Plane(Centroid, Normal);
        }

        public void SetNormalToDual()
        {
            Normal = Dual.GetDirectionVector();
        }


        /// <summary>
        /// Gets all the connected faces to the face.
        /// It will get all faces primal and pair using edge connections 
        /// </summary>
        /// <returns>an enumerable of PFFace with all connected faces excluding self and pair</returns>
        public IEnumerable<PFFace> GetAllConnectedFaces()
        {
            HashSet<PFFace> neighbors = new HashSet<PFFace>();
            foreach (var edge in Edges)
            {
                foreach (var face in edge.Faces.Concat(edge.Pair.Faces))
                {
                    neighbors.Add(face);
                }

            }
            neighbors.Remove(this);
            neighbors.Remove(Pair);

            return neighbors;
        }

        /// <summary>
        /// Gets the connected faces to the face.
        /// It will get all faces only to the specified face
        /// Excludes pair and pair connections 
        /// </summary>
        /// <returns>an enumerable of PFFace with all connected self </returns>
        public IEnumerable<PFFace> GetConnectedFaces()
        {
            HashSet<PFFace> neighbors = new HashSet<PFFace>();
            foreach (var edge in Edges)
            {
                foreach (var face in edge.Faces)
                {
                    neighbors.Add(face);
                }

            }
            neighbors.Remove(this);


            return neighbors;
        }

        /// <summary>
        /// Scales a face vertices towards a set target face area.
        /// Target area should already be set in the face.
        /// </summary>
        /// <returns></returns>
        public List<PFVertex> SetArea()
        {
            
            var movedVerts = new List<PFVertex>();
            if (double.IsNaN(TargetArea)) return movedVerts;
            ComputeCentroid();
            double totalArea = 0.0;
            List<Vector3d> vecList = Vertices.Select(x => x.Point - Centroid).ToList();
            for (int v = 0; v < vecList.Count; v++)
            {
                double faceArea = System.Math.Abs(Vector3d.CrossProduct(vecList[v], vecList[(v + 1) % vecList.Count]).Length / 2);
                totalArea += faceArea;
            }


            var scaleCoef = Math.Sqrt(TargetArea / totalArea);

            for (int i = 0; i < Vertices.Count; i++)
            {
                var newVert = Vertices[i].Move(Vertices[i].Point + vecList[i] * scaleCoef);
                newVert.InfluenceCoef = this.InfluenceCoef;
                movedVerts.Add(newVert);
            }
            return movedVerts;
        }

        public List<PFVertex> PlanarizeAndSetArea(double maxDev, bool keepNormal = true)
        {
            Planarized = false;
            var movedVerts = new List<PFVertex>();
            

            var fPlane = new Plane();
            if (keepNormal)
                fPlane = new Plane(Centroid, Normal);
            else
                Plane.FitPlaneToPoints(Vertices.Select(x => x.Point), out fPlane);
            // closest plane to face 
            VertPlanDeviations = new List<double>();

            var newPoints = new List<Point3d>();
            //var deviations = new List<double>();
            foreach (var vert in Vertices)
            {
                // project all vertices to plane 
                var newVertPoint = fPlane.ClosestPoint(vert.Point);
                // compute deviation for each vertex/point
                var vertDev = newVertPoint.DistanceTo(vert.Point);
                // store deviation 
                VertPlanDeviations.Add(vertDev);
                // store point 
                newPoints.Add(newVertPoint);
                // update max deviation 
            }

            ComputeCentroid();
            List<Vector3d> vecList = newPoints.Select(x => x - Centroid).ToList();
            var scaleCoef = 1.0;
            

            if (!double.IsNaN(TargetArea))
            {
                
                double totalArea = 0.0;
                
                for (int v = 0; v < vecList.Count; v++)
                {
                    double faceArea = System.Math.Abs(Vector3d.CrossProduct(vecList[v], vecList[(v + 1) % vecList.Count]).Length / 2);
                    totalArea += faceArea;
                }
                scaleCoef = Math.Sqrt(TargetArea / totalArea);

            }

            // if not planar to tollerance or needs to be scaled
            if (VertPlanDeviations.Any(x => x > maxDev) || scaleCoef  != 1.0)
            {
                for (var v = 0; v < Vertices.Count; v++)
                {
                    var movedVert = Vertices[v].Move(Centroid + vecList[v] * scaleCoef);
                    movedVert.InfluenceCoef = InfluenceCoef;
                    movedVerts.Add(movedVert);
                    //FMesh.Vertices.SetVertex(v, movedVert.Point); // this must happen at the end after the average - so not here 
                    // this sets the color according to the last step transformation and new deviation computed above 
                    // so mesh is one step behind the points .... We need to update the mesh again after the main cycle using this method exits 
                    //FMesh.VertexColors.SetColor(v, Util.DeviationToColorListGreenRed(VertPlanDeviations[v], Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance));
                }

                // 

                Planarized = true;
                FacePlane = fPlane;
            }

            // add mesh to big mesh for representation
            // look at max deviation in the big loop 
            //meshes.Add(face.FMesh); //.DuplicateMesh()




            return movedVerts;
        }





        public string SerializeJson()
        {


            JavaScriptSerializer json = new JavaScriptSerializer();

            string jsonData = json.Serialize(SerializeDict());

            return jsonData;
        }

        public Dictionary<string, object> SerializeDict()
        {
            //TODO see what happens for nulls 
            Dictionary<string, object> props = new Dictionary<string, object>
            {
                { "Id", Id },
                { "Vertices", Vertices.Select(x => x.Id).ToList() },
                { "Edges", Edges.Select(x => x.Id).ToList() },
                // neighbors is not used and not serialized 
                { "Cell", Cell?.Id ?? -1 },
                { "Pair", Pair?.Id ?? 0 },
                { "Normal", PVToDict(Normal) },
                { "Centroid", PVToDict(Centroid) },
                // FMesh is not serialized - it will be recomputed
                { "Dual", Dual?.Id ?? 0 },
                { "External", External },
                { "Area", Area },

                {"TargetArea", TargetArea },
                {"InfluenceCoef", InfluenceCoef }
            };



            return props;
        }



    }
}
