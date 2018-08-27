using Rhino.Collections;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using static PolyFramework.Util;

namespace PolyFramework
{
    [Serializable]
    [DataContract(IsReference = true)]
    public class PFCell
    {
        [DataMember]
        public int Id { get; set; }

        /// <summary>
        /// The HalfFaces of the polyhedral cell
        /// </summary> 
        [DataMember]
        public List<PFFace> Faces { get; set; } = new List<PFFace>();
        /// <summary>
        /// The vertexes that make up the cell
        /// </summary>
        [DataMember]
        public List<PFVertex> Vertices { get; set; } = new List<PFVertex>();
        /// <summary>
        /// The half edges that are present in the cell 
        /// </summary>
        [DataMember]
        public IList<PFEdge> Edges { get; set; } = new List<PFEdge>();
        public Mesh CellMesh { get; private set; } = new Mesh();
        [DataMember]
        public Point3d Centroid { get; set; } = Point3d.Unset;
        [DataMember]
        public IList<PFVertex> Dual { get; set; } = new List<PFVertex>();
        [DataMember]
        public bool Exterior { get; set; } = false;

        // To do - implement dual - the position of the dual cell->vertex 

        public PFCell()
        {

        }

        public PFCell(int id)
        {
            Id = id;
        }
        /// <summary>
        /// Updates the cell from the data in its faces 
        /// Sets the cell as exterior if it has any naked edges/faces
        /// </summary>
        public void UpdateAllFromFaces()
        {
            HashSet<PFVertex> verts = new HashSet<PFVertex>();
            HashSet<PFEdge> edgs = new HashSet<PFEdge>();
            foreach (var face in Faces)
            {
                face.Cell = this;
                if (face.External)
                {
                    Exterior = true; // this sets the cell as exterior if it has any naked faces

                }

                foreach (var vert in face.Vertices) verts.Add(vert);
                foreach (var edg in face.Edges) edgs.Add(edg);
            }
            if (Exterior)
            {
                foreach (var face in Faces) face.External = true;
                foreach (var edg in edgs)
                {
                    if (edg.External)
                    {
                        foreach (var vert in edg.Vertices) vert.External = true;
                    }
                }

            }
            Vertices = new List<PFVertex>(verts);
            Edges = new List<PFEdge>(edgs);
            Centroid = new Point3d(Vertices.Average(x => x.Point.X), Vertices.Average(y => y.Point.Y), Vertices.Average(z => z.Point.Z));

        }

        public void ComputeCentroid()
        {
            Centroid = new Point3d(Vertices.Average(x => x.Point.X), Vertices.Average(y => y.Point.Y), Vertices.Average(z => z.Point.Z));
        }

        public void DualUpdateAllFromFaces()
        {
            HashSet<PFVertex> verts = new HashSet<PFVertex>();
            HashSet<PFEdge> edgs = new HashSet<PFEdge>();
            foreach (var face in Faces)
            {
                face.Cell = this;
                foreach (var vert in face.Vertices) verts.Add(vert);
                foreach (var edg in face.Edges) edgs.Add(edg);
            }
            Vertices = new List<PFVertex>(verts);
            Edges = new List<PFEdge>(edgs);
            Centroid = this.Dual[0].Point;

        }


        public void AgregateCellMesh(double shrinkFactor = 1.00)
        {
            if (shrinkFactor > 1 || shrinkFactor < 0.01) throw new PolyFrameworkException("shrink factor must be between 0.01 and 1.00");
            // The standard rhino mesh offset is not good for sharp corners 
            // need to write a new offset algorithm
            foreach (var face in Faces)
            {
                if (face.FMesh.Faces.Count == 0 || face.FMesh == null) face.FaceMesh();
                CellMesh.Append(face.FMesh);
            }
            CellMesh.Weld(Math.PI);
            CellMesh.UnifyNormals();

            CellMesh.Normals.ComputeNormals();
            CellMesh.Normals.UnitizeNormals();
            //CellMesh.FaceNormals.ComputeFaceNormals();


            CellMesh.Flip(true, true, true);
            if (Exterior) CellMesh.Transform(Transform.Scale(Centroid, 1 + (1 - shrinkFactor) / 2));
            else CellMesh.Transform(Transform.Scale(Centroid, shrinkFactor));

            //CellMesh = CellMesh.Offset(0.05);


        }
        /// <summary>
        /// Builds the cell mesh in a clean way.
        /// This lends the cell mesh to being referenced for PolyFrame use.
        /// This needs the face meshes stored in the faces
        /// </summary>
        public Mesh CreateCellMesh()
        {
            // create a dict for quick find of the vertices 
            // take the faces from each faceMesh and reconstruct them in the cell 
            var cellMesh = new Mesh();
            ArchivableDictionary vertDict = new ArchivableDictionary();
            for (int i = 0; i < Vertices.Count; i++) vertDict.Set(i.ToString(), Vertices[i].Id);
            cellMesh.UserDictionary.Set("VertexIds", vertDict);
            cellMesh.UserDictionary.Set("Id", Id);
            var cellVerts = new Dictionary<int, int>();
            foreach (var vert in Vertices)
            { 
                cellVerts.Add(vert.Id, cellMesh.Vertices.Add(vert.Point)); // vert id : index in cell 
            }

            foreach (var face in Faces)
            {
                //cellMesh.Ngons.AddNgon(new MeshNgon(new List<MeshFace>()));
                var mFaceList = new List<int>();
                foreach(var mFace in face.FMesh.Faces)
                {
                    var cellMFace = new MeshFace(
                        cellVerts[face.Vertices[mFace.A].Id],
                        cellVerts[face.Vertices[mFace.B].Id],
                        cellVerts[face.Vertices[mFace.C].Id]
                        );

                    mFaceList.Add(cellMesh.Faces.AddFace(cellMFace));
                    
                }
                cellMesh.Ngons.AddNgon(MeshNgon.Create(face.Vertices.Select(x => cellVerts[x.Id]).ToList(), mFaceList));
            }

           

            cellMesh.UnifyNormals();

            cellMesh.Normals.ComputeNormals();
            cellMesh.Normals.UnitizeNormals();
            


            cellMesh.Flip(true, true, true);


            return cellMesh;
        }

        public Brep CreateBrep()
        {
            Brep cellBrep = new Brep();
            foreach (var face in Faces)
            {
                var faceBrep = face.CreateBrep();
                if (faceBrep != null)
                {
                    cellBrep.Append(faceBrep);
                }
            }
            cellBrep.JoinNakedEdges(Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
            return cellBrep;
        }

        /// <summary>
        /// Uses CreateBrep then tries to match each vertex to a brepVertex in the PolySurface 
        /// </summary>
        /// <returns></returns>
        public Brep CreateBrepMatched()
        {
            var cellBrep = CreateBrep();

            var cellBPC = new PointCloud(cellBrep.Vertices.Select(x => x.Location));

            foreach (var vert in Vertices)
            {
                var cellBrVertIndex = cellBPC.ClosestPoint(vert.Point);
                var dist = vert.Point.DistanceTo(cellBPC[cellBrVertIndex].Location);

                if (dist > Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                {
                    cellBrep.Vertices.Add(vert.Point, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                    cellBrVertIndex = cellBrep.Vertices.Count - 1;
                    //BakeErrorGeo(new List<GeometryBase> { cellBrep, new Point(vert.Point) }, new List<String> { $"cell_{Id}", $"vert_{vert.Id}" });
                    //throw new PolyFrameworkException($"Cannot match a CellBrep Vertex to Cell Vertex {vert.Id} for cell {Id}.");   
                }

                cellBrep.Vertices[cellBrVertIndex].UserDictionary.Set("Id", vert.Id);

            }
            cellBrep.UserDictionary.Set("Id", Id);

            return cellBrep;

        }

        public IList<Brep> CreateBrepsFromFaces()
        {
            return Faces.Select(x => x.CreateBrep()).ToList();
        }


        public override string ToString()
        {
            return "Cell Id = " + Id.ToString();
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
                { "Faces", Faces.Select(x => x.Id).ToList() },
                // neighbors is not used and not serialized 
                { "Centroid", PVToDict(Centroid) },
                // FMesh is not serialized - it will be recomputed
                { "Dual", Dual.Select(x => x.Id).ToList() },
                { "Exterior", Exterior }

            };



            return props;
        }
    }








}

