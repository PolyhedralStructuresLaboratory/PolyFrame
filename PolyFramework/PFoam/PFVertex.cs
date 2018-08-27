using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using System.Linq;
using static PolyFramework.Util;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;

namespace PolyFramework

{
    /// <summary>
    /// PolyFrame vertex.
    /// A point in space with added properties
    /// 
    /// </summary>
    [Serializable]
    [DataContract(IsReference = true)]
    public class PFVertex
    {
        /// <summary>
        /// Id the id of the vertex 
        /// </summary>
        [DataMember]
        public int Id { get; set; } = -1;
        //public double X { get; set; }
        //public double Y { get; set; }
        //public double Z { get; set; }
        /// <summary>
        /// Refers to the position of the point in the foam/structure - usually applies to the dual 
        /// it will be external if the point is outside of the primal external cell. 
        /// </summary>
        [DataMember] public bool External { get; set; } = false;
        [DataMember] public Point3d Point { get; set; } // Rhino ref
        public IList<PFVertex> Adjacent { get; set; } = new List<PFVertex>();
        [DataMember] public IList<PFEdge> Edges { get; set; } = new List<PFEdge>();
        [DataMember] public IList<PFFace> Faces { get; set; } = new List<PFFace>();
        [DataMember] public IList<PFCell> Cells { get; set; } = new List<PFCell>();
        [DataMember] public PFCell Dual { get; set; } = null;
        /// <summary>
        /// Use for conduit view
        /// </summary>
        //public System.Drawing.Color Color { get; set; } = System.Drawing.Color.AntiqueWhite;
        public bool Picked { get; set; } = false;
        public GeometryBase RestrictSupport { get; set; } = null;
        public Func<PFVertex> RestrictPosition { get; set; } = null;
        public double MaxTravel { get; set; } = double.MaxValue;


        public Guid SupportGuid { get; set; } = Guid.Empty;
        public bool Fixed { get; set; } = false;
        public bool OnGeo { get; set; } = true;
        public double InfluenceCoef { get; set; } = 1.0;

        public PFVertex()
        {

        }
        public PFVertex(int id, double x, double y, double z)
        {
            Id = id;

            Point = new Point3d(x, y, z);

            External = false;
        }

        public PFVertex(int id, Point3d location)
        {
            Id = id;
            Point = location;



        }
        /// <summary>
        /// The PFVertex equality comparer ID and Point 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is PFVertex otherVert)
            {
                return Id == otherVert.Id; //&& Point == otherVert.Point;
            }
            return false;
        }

        public override int GetHashCode()
        {
            //string sHash = Id.ToString() + (Point.X*Point.Y*Point.Z*10e8).ToString().Remove(7);
            //if (sHash.Length > 9) sHash.Remove(9);
            //return Int32.Parse(sHash);
            return Id;
        }

        public override string ToString()
        {
            return $"Vertex Id = {Id} | X = {Point.X:0.0000} | Y = {Point.Y:0.0000} | Z = {Point.Z:0.0000} |";
        }

        public Point3d GetPoint()
        {
            return Point;
        }
        /// <summary>
        /// Culls the PFVertex duplicates based on their position in 3d space
        /// MAKE SURE TO REMOVE also the corresponding edges 
        /// </summary>
        /// <param name="pottentialDuplicates"></param>
        /// <param name="tollerance"></param>
        /// <returns></returns>
        public static IList<PFVertex> RemoveDuplicates(IList<PFVertex> pottentialDuplicates, double tollerance)
        {
            PointCloud pc = new PointCloud();
            List<PFVertex> singulars = new List<PFVertex>();
            pc.Add(pottentialDuplicates[0].Point);
            singulars.Add(pottentialDuplicates[0]);
            for (int p = 1; p < pottentialDuplicates.Count; p++)
            {
                int closeIndex = pc.ClosestPoint(pottentialDuplicates[p].Point);
                if (pottentialDuplicates[p].Point.DistanceTo(pc[closeIndex].Location) > tollerance)
                {
                    pc.Add(pottentialDuplicates[p].Point);
                    singulars.Add(pottentialDuplicates[p]);
                }
            }

            return singulars;
        }
        /// <summary>
        /// Use this to create adjacency table inside each vertex.
        /// ATTENTION this changes the vertex in place 
        /// Goes through the edges and puts neighbors in vertexes
        /// This assumes the lines are unique .... No duplicates 
        /// </summary>
        /// <param name="points"></param>
        /// <param name="lines"></param>
        /// 
        public static void GetAdjacent(IList<PFVertex> points, IList<PFEdge> lines)
        {
            var vertDict = new Dictionary<int, PFVertex>();
            foreach (PFVertex pv in points)
            {
                pv.Adjacent = new List<PFVertex>();
                vertDict.Add(pv.Id, pv);
            }

            foreach (PFEdge ln in lines)
            {
                if (ln.Vertices.Count == 2)
                {
                    // first make sure the lines point to the vertexes in the list 
                    ln.Vertices[0] = vertDict[ln.Vertices[0].Id];
                    ln.Vertices[1] = vertDict[ln.Vertices[1].Id];
                    // second use pointers to store the adjacency in the vertex object
                    ln.Vertices[0].Adjacent.Add(ln.Vertices[1]);
                    ln.Vertices[1].Adjacent.Add(ln.Vertices[0]);
                }
                else throw new PolyFrameworkException("Edges should have 2 vertexes");
            }
        }

        /// <summary>
        /// Gets the average point for a set of vertices
        /// </summary>
        /// <param name="verts"></param>
        /// <returns></returns>
        public static Point3d AverageVertexes(IList<PFVertex> verts)
        {
            return new Point3d(verts.Average(x => x.Point.X), verts.Average(y => y.Point.Y), verts.Average(z => z.Point.Z));
        }

        /// <summary>
        /// Gets the weighted average for a set of vertices
        /// Uses the influence value from each vertex in the average
        /// </summary>
        /// <param name="verts"></param>
        /// <returns>Average location </returns>
        public static Point3d WeightAverageVertexes(IList<PFVertex> verts)
        {
            var coefs = verts.Select(v => v.InfluenceCoef);
            double avX = WeightedAverage(verts.Select(x => x.Point.X), coefs);
            double avY = WeightedAverage(verts.Select(y => y.Point.Y), coefs);
            double avZ = WeightedAverage(verts.Select(z => z.Point.Z), coefs);

            return new Point3d(avX, avY, avZ);
        }





        /// <summary>
        /// Puts all the cells the vertex is part of in the Cell list property of the vertex.
        /// </summary>
        public void PopulateVertexCells()
        {
            var uniqueCells = new HashSet<PFCell>();
            foreach (var face in Faces)
            {
                uniqueCells.Add(face.Cell);
            }
            Cells = uniqueCells.ToList();
        }

        /// <summary>
        /// Finds the cell some vertex moved into.
        /// The cell is part of the list of cells the vertex is connected to. 
        /// </summary>
        /// <param name="position">new vertex position</param>
        /// <returns>the cell the vertex moves into</returns>
        public PFCell PointInCell(Point3d position)
        {
            // get all cells containing the vert 
            // get all faces from cell containing the vert
            // see if vector from vert to point has the same direction as all the normals of those faces 
            // if yes break 
            if (Cells.Count == 0) PopulateVertexCells();


            var cellTrihedrals = Cells.ToDictionary(cell => cell, cell => new HashSet<Vector3d>());
            //new Dictionary<PFCell, HashSet<Vector3d>>();

            foreach (var face in Faces)
            {
                cellTrihedrals[face.Cell].Add(face.Normal);
            }

            PFCell result = new PFCell();

            foreach (var keyValue in cellTrihedrals)
            {
                if (!keyValue.Key.Exterior)
                {
                    if (Util.InsideHedra(Point, position, keyValue.Value.ToList()))
                    {
                        result = keyValue.Key;
                        break;

                    }
                }

            }
            return result;

        }

        public PFVertex Move(Point3d toPoint)
        {
            if (toPoint.DistanceTo(Point) > MaxTravel)
            {
                var moveVect = toPoint - Point;
                moveVect.Unitize();
                return new PFVertex(Id, Point + moveVect * MaxTravel);
            }
            else return new PFVertex(Id, toPoint);


        }





        public PFVertex ConstrainPoint()
        {
            var tp = RestrictSupport as Point;
            var toPoint = Point + (tp.Location - Point);
            var newV = Move(toPoint);
            newV.InfluenceCoef = InfluenceCoef;
            return newV;

        }

        public PFVertex ConstrainCurve()
        {
            var tCurve = RestrictSupport as Curve;
            if (tCurve.ClosestPoint(Point, out double cParameter))
            {
                var target = tCurve.PointAt(cParameter);
                var toPoint = Point + (target - Point);
                var newV = Move(toPoint);
                newV.InfluenceCoef = InfluenceCoef;
                return newV;

            }
            var oldV = new PFVertex(Id, Point);
            oldV.InfluenceCoef = InfluenceCoef;
            return oldV;

        }
        public PFVertex ConstrainOnBrep()
        {
            var tBrep = RestrictSupport as Brep;
            var target = tBrep.ClosestPoint(Point);
            var toPoint = Point + (target - Point);
            var newV = Move(toPoint);
            newV.InfluenceCoef = InfluenceCoef;
            return newV;

        }

        public PFVertex ConstrainInBrep()
        {
            var tBrep = RestrictSupport as Brep;
            if (tBrep.IsSolid && tBrep.IsPointInside(Point, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, false))
            {
                var oldV = new PFVertex(Id, Point);
                oldV.InfluenceCoef = InfluenceCoef;
                return oldV;
            }
            var target = tBrep.ClosestPoint(Point);
            var toPoint = Point + (target - Point);
            var newV = Move(toPoint);
            newV.InfluenceCoef = InfluenceCoef;
            return newV;
        }

        public PFVertex ConstrainOnMesh()
        {
            var tMesh = RestrictSupport as Mesh;
            var target = tMesh.ClosestPoint(Point);
            var toPoint = Point + (target - Point);
            var newV = Move(toPoint);
            newV.InfluenceCoef = InfluenceCoef;
            return newV;
        }

        public PFVertex ConstrainInMesh()
        {
            var tMesh = RestrictSupport as Brep;
            if (tMesh.IsSolid && tMesh.IsPointInside(Point, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, false))
            {
                var oldV = new PFVertex(Id, Point);
                oldV.InfluenceCoef = InfluenceCoef;
                return oldV;

            }
            var target = tMesh.ClosestPoint(Point);
            var toPoint = Point + (target - Point);
            var newV = Move(toPoint);
            newV.InfluenceCoef = InfluenceCoef;
            return newV;
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
                { "Point", PVToDict(Point) },
                { "External", External },
                { "Edges", Edges.Select(x => x.Id).ToList() },
                { "Faces", Faces.Select(x => x.Id).ToList() },
                { "Cells", Cells.Select(x => x.Id).ToList() },
                { "Dual", Dual?.Id ?? -1 },

                { "SupportGuid", SupportGuid },
                { "InfluenceCoef", InfluenceCoef },
                { "Fixed", Fixed },
                { "OnGeo", OnGeo }

            };



            return props;
        }


        public static PFVertex PreDeserialization(Dictionary<string, object> vertexDict)
        {
            PFVertex newVert = new PFVertex();
            if (vertexDict.TryGetValue("Id", out object id))
            {
                if (id is int)
                {
                    newVert.Id = (int)id;
                }
            }
            else throw new PolyFrameworkException("Id data not valid or non existent");
            Dictionary<string, double> coordList = vertexDict["Point"] as Dictionary<string, double>;

            newVert.Point = new Point3d(coordList["X"], coordList["Y"], coordList["Z"]);
            newVert.External = (bool)vertexDict["External"];

            // only these values are stored in the dicts - the rest are placeholders 
            // they will be restored after all the objects are predeserialized. 

            return newVert;

        }

    }
}
