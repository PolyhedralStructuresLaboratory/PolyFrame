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
    public class PFEdge
    {
        /// <summary>
        /// Edge Id is an integer positive or negative for the pair (cannot be 0 if face is initialized)
        /// </summary>
        /// 
        [DataMember]
        public int Id { get; set; } = 0;
        [DataMember]
        public IList<PFVertex> Vertices { get; set; } = new List<PFVertex>();
        [DataMember]
        public PFEdge Pair { get; set; } = null;
        [DataMember]
        public IList<PFFace> Faces { get; set; } = new List<PFFace>();
        [DataMember]
        public List<double> FaceAngle { get; set; } = new List<double>();
        [DataMember]
        public PFFace Dual { get; set; } = null;
        /// <summary>
        /// Refers to the position of the edge in the foam/structure - usually applies to the dual 
        /// it will be external if the edge sticks outside of the primal external cell. 
        /// </summary>
        /// 
        [DataMember]
        public bool External { get; set; } = false;
        [DataMember]
        public double Deviation { get; set; } = -1000.0;

        //public System.Drawing.Color Color { get; set; } = System.Drawing.Color.WhiteSmoke;
        public bool Picked { get; set; } = false;

        //lengths 
        public double TargetLength { get; set; } = double.NaN;
        public double MinLength { get; set; } = double.Epsilon;
        public double MaxLength { get; set; } = double.MaxValue;
        public double InfluenceCoef { get; set; } = 1.0;
        //save lengths in serial form 


        // should the edge know about the cell ??

        public PFEdge()
        {

        }

        public PFEdge(int id)
        {
            Id = id;
        }
        public PFEdge(int id, IList<PFVertex> vertices)
        {
            Id = id;
            Vertices = new List<PFVertex>(vertices);
            Pair = null;
            Faces = new List<PFFace>();
        }
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;
            var otherEdge = obj as PFEdge;
            return Id == otherEdge.Id && Vertices == otherEdge.Vertices;

        }
        public override int GetHashCode()
        {
            string sHash = Id.ToString();// + Vertices.Select(x => x.Id.ToString()).Aggregate((i, j) => (i + j));
            if (sHash.Length > 9) sHash = sHash.Remove(9);

            return Int32.Parse(sHash);
        }

        public override string ToString()
        {
            return $"Edge Id = {Id.ToString()} | V0 = {Vertices[0].Id.ToString()} | V1 = {Vertices[1].Id.ToString()}";
        }

        /// <summary>
        /// Creates the reversed edge and stores it in the original edge as the pair.
        /// The pair will not have a face for now. Make sure to assign one in the sorting process. 
        /// </summary>
        public void CreatePair()
        {
            if (Vertices.Count != 2) throw new PolyFrameworkException("Edge cannot be reversed... Does not have all vertexes");
            Pair = new PFEdge(-Id, new List<PFVertex>(Vertices.Reverse()));
            foreach (var vert in Pair.Vertices)
            {
                vert.Edges.Add(Pair);
            }
            Pair.Pair = this;
            Pair.External = External;
        }
        /// <summary>
        /// Sorts all the faces around an edge based on the angle 
        /// The angle is measured using the first face normal in the list. 
        /// </summary>
        public void SortFacesByAngle()
        {

            var normals = Faces.Select(x => x.Normal).ToList();
            var refVect = new Vector3d(Vertices[1].Point - Vertices[0].Point);
            var angles = normals.Select(x => Angle(normals[0], x, refVect));
            FaceAngle = new List<double>(angles);
            var joined = angles.Zip(Faces, (angle, face) => new { angle, face }).OrderBy(x => x.angle);
            Faces = joined.Select(x => x.face).ToList();
            FaceAngle = joined.Select(x => x.angle).ToList();
        }


        /// <summary>
        /// Returns a dictionary of all the consecutive half-face pairs around a half-edge
        /// </summary>
        /// <returns></returns>
        public Dictionary<PFFace, List<PFFace>> FacePairs()
        {
            Dictionary<PFFace, List<PFFace>> facePairs = new Dictionary<PFFace, List<PFFace>>();
            if (Faces.Count == 1) return facePairs;
            for (int f = 0; f < Faces.Count; f++)
            {
                // try to see if the dict has the pair 
                List<PFFace> connectedFaces = new List<PFFace>();
                if (!facePairs.TryGetValue(Faces[f], out connectedFaces))
                {
                    // if not create the pair face, list<pface>{with the pair face}
                    facePairs.Add(Faces[f], new List<PFFace>() { Faces[(f + 1) % Faces.Count].Pair });
                }
                else
                {
                    ///////////// This branch should never be reached 
                    // get list from the dict, add the pair face 
                    if (!connectedFaces.Contains(Faces[(f + 1) % Faces.Count].Pair))
                        connectedFaces.Add(Faces[(f + 1) % Faces.Count].Pair);
                    //facePairs.Add(Faces[f], new List<PFFace>(connectedFaces));
                }

                // also add the symmetrical pairs  
                // first clear the out list 
                connectedFaces = new List<PFFace>();

                if (!facePairs.TryGetValue(Faces[(f + 1) % Faces.Count].Pair, out connectedFaces))
                {
                    // if not create the pair face, list<pface>{with the pair face}
                    facePairs.Add(Faces[(f + 1) % Faces.Count].Pair, new List<PFFace>() { Faces[f] });
                }
                else
                {
                    ///////////// This branch should never be reached
                    // get list from the dict, add the pair face
                    if (!connectedFaces.Contains(Faces[f]))
                        connectedFaces.Add(Faces[f]);
                    //facePairs.Add(Faces[(f + 1) % Faces.Count], new List<PFFace>(connectedFaces));
                }
            }
            return facePairs;
        }
        /// <summary>
        /// Gets the length of the edge.
        /// </summary>
        /// <returns></returns>
        public double GetLength()
        {
            return Vertices[0].Point.DistanceTo(Vertices[1].Point);
        }

        /// <summary>
        /// Gets the orientation of the edge in rapport to its dual face 
        /// </summary>
        /// <returns>True if vector (v1-v0) has same general orientation like the dual face normal</returns>
        public bool OrientationToDual()
        {
            if (Dual == null || Dual.Normal == Vector3d.Unset) throw new PolyFrameworkException("Edge dual is null or Dual.Normal is unset");
            Point3d midPoint = PFVertex.AverageVertexes(Vertices);
            Vector3d midToStart = Vertices[0].Point - midPoint;
            Vector3d midToEnd = Vertices[1].Point - midPoint;
            midToStart.Unitize();
            midToEnd.Unitize();
            return (Dual.Normal - midToStart).Length > (Dual.Normal - midToEnd).Length;
        }
        /// <summary>
        /// Calculates the angle to the dual face.
        /// </summary>
        /// <returns></returns>
        public double AngleToDual()
        {
            //Vector3d lineVec = Vertices[0].Point - Vertices[1].Point;
            //lineVec.Unitize();
            return DotAngle(Dual.Normal, GetDirectionVector());
        }

        /// <summary>
        /// Calculates the angle to a specified direction (vector3d)
        /// </summary>
        /// <param name="dir">the custom direction</param>
        /// <returns></returns>
        public double AngleToDir(Vector3d dir)
        {
            return DotAngle(dir, GetDirectionVector());
        }





        /// <summary>
        /// 1 Step perpendincularization of the edge
        /// Will change the locations of the vertexes at the ends 
        /// This should be used on a clone of the edge 
        /// Requires dual of the edge to be set 
        /// </summary>
        /// <returns>the length of the edge after the process </returns>
        public void PerpEdge(bool orientation, double lAdjCoef = 1.0)
        {
            if (lAdjCoef < 0) lAdjCoef *= -1;

            double originaLength = GetLength();
            if (orientation) lAdjCoef *= -1;
            Point3d midPoint = PFVertex.AverageVertexes(Vertices);

            Point3d startNew = midPoint + Dual.Normal * originaLength / 2 * lAdjCoef;
            Point3d endNew = midPoint + Dual.Normal * originaLength / 2 * -lAdjCoef;
            Vertices[0].Point = startNew;
            Vertices[1].Point = endNew;


        }
        /// <summary>
        /// Rotates the edge towards a target vector making sure the ends do not travel beyond a max distance
        /// 
        /// </summary>
        /// <param name="orientation"></param>
        /// <param name="maxTravel"></param>
        /// <param name="lAdjCoef"></param>
        public void InterPerp(bool orientation, double maxTravel, double lAdjCoef = 1.0)
        {
            // test angle against maxAngle (deducted from max travel via asin)
            // if angle bigger than max angle get new vector at max travel angle 
            // get vector by rotating original line vector based on cross product vector 
            // max travel assumes there is no length change in the edge 

            double maxAngle = 2 * (Math.Asin(Math.Round(maxTravel / GetLength(), 9)));
            if (maxAngle < AngleToDual())
            {
                // get new direction 
                Vector3d dir = GetDirectionVector();
                Vector3d axis = Vector3d.CrossProduct(dir, Dual.Normal);
                Transform rot = Transform.Rotation(maxAngle, axis, Point3d.Origin);
                dir.Transform(rot);
                ScaleToDir(dir, lAdjCoef);
            }
            else
            {
                ScaleToDir(Dual.Normal, lAdjCoef);
            }

        }

        /// <summary>
        /// Perps and scales the edge in one step
        /// If target length is set - scales the edge to target
        /// If not set it makes sure  minLen smaller that length and length is smaller than maxLegth  
        /// Uses the constraint move of the point to limit move 
        /// </summary>
        /// <returns>List of 2 new vertices after the constrained move. They have the influence coefficient of the edge </returns>
        public List<PFVertex> PerpScale_Soft()
        {
           

            double originaLength = GetLength();
            double setLength;

            if (!double.IsNaN(TargetLength)) setLength = TargetLength;
            else if (originaLength < MinLength) setLength = MinLength;
            else if (originaLength > MaxLength) setLength = MaxLength;
            else setLength = originaLength;
            

            Point3d midPoint = PFVertex.AverageVertexes(Vertices);

            Point3d startNew = midPoint + Dual.Normal * setLength / 2;
            Point3d endNew = midPoint + Dual.Normal * -setLength / 2;
            var nVertStart = Vertices[0].Move(startNew);
            nVertStart.InfluenceCoef = this.InfluenceCoef;
            var nVertEnd = Vertices[1].Move(endNew);
            nVertEnd.InfluenceCoef = this.InfluenceCoef;
          


            return new List<PFVertex> { nVertStart, nVertEnd };


        }

        /// <summary>
        /// Scales the edge in one step
        /// If target length is set - scales the edge to target
        /// If not set it makes sure  minLen smaller that length and length is smaller than maxLegth  
        /// Uses the constraint move of the point to limit move 
        /// </summary>
        /// <returns>List of 2 new vertices after the constrained move. They have the influence coefficient of the edge </returns>
        public List<PFVertex> Scale_Soft()
        {


            double originaLength = GetLength();

            var direction = GetDirectionVector();
            direction.Unitize();
            double setLength;

            if (!double.IsNaN(TargetLength)) setLength = TargetLength;
            else if (originaLength < MinLength)
                setLength = MinLength;
            else if (originaLength > MaxLength) setLength = MaxLength;
            else setLength = originaLength;


            Point3d midPoint = PFVertex.AverageVertexes(Vertices);

            Point3d startNew = midPoint + direction * setLength / 2;
            Point3d endNew = midPoint + direction * -setLength / 2;
            var nVertStart = Vertices[0].Move(startNew);
            nVertStart.InfluenceCoef = this.InfluenceCoef;
            var nVertEnd = Vertices[1].Move(endNew);
            nVertEnd.InfluenceCoef = this.InfluenceCoef;



            return new List<PFVertex> { nVertStart, nVertEnd };


        }

        /// <summary>
        /// Scales an edge to a desired length.
        /// Changes the edge in place.
        /// </summary>
        /// <param name="setLengh"></param>
        public void ScaleEdge(double setLengh)
        {
            Point3d midPoint = PFVertex.AverageVertexes(Vertices);
            Vector3d midToStart = Vertices[0].Point - midPoint;
            Vector3d midToEnd = Vertices[1].Point - midPoint;
            midToEnd.Unitize(); midToStart.Unitize();

            Vertices[0].Point = midPoint + midToStart * setLengh / 2;
            Vertices[1].Point = midPoint + midToEnd * setLengh / 2;


        }

        public Line CreateLine()
        {
            return new Line(Vertices[0].Point, Vertices[1].Point);
        }

        /// <summary>
        /// switches the geometry of an edge 
        /// only the 3d points in the vertexes will be switched
        /// </summary>
        public void SwitchPoints()
        {
            Point3d temp = Vertices[0].Point;
            Vertices[0].Point = Vertices[1].Point;
            Vertices[1].Point = temp;
        }

        public Vector3d GetDirectionVector()
        {
            Vector3d lineVec = Vertices[0].Point - Vertices[1].Point;
            lineVec.Unitize();
            return lineVec;
        }

        /// <summary>
        /// This scales an edge while keeping a given direction
        /// If edge is reversed it will be un-reversed by reconstruction to the new direction.
        /// </summary>
        /// <param name="dir">imposed direction</param>
        /// <param name="factor">scale factor</param>
        public void ScaleToDir(Vector3d dir, double factor)
        {
            factor = Math.Abs(factor);

            double originaLength = GetLength();

            Point3d midPoint = PFVertex.AverageVertexes(Vertices);

            Point3d startNew = midPoint + dir * originaLength / 2 * factor;
            Point3d endNew = midPoint + dir * -originaLength / 2 * factor;
            Vertices[0].Point = startNew;
            Vertices[1].Point = endNew;
        }


        /// <summary>
        /// Finds the cell a point is in relative to an edge
        /// The cell is part of the list of cells the edge is connected to. 
        /// </summary>
        /// <param name="position">new vertex position</param>
        /// <returns>the cell the point is in</returns>
        public PFCell PointInCell(Point3d position)
        {
            // get all faces containing the edge 
            // create dictionary with face normals pairs for each cell
            // see if vector from vert to point has the same direction as all the normals of those faces 
            // if yes break 

            var cellDihedrals = new Dictionary<PFCell, HashSet<Vector3d>>();
            //new Dictionary<PFCell, HashSet<Vector3d>>();

            foreach (var face in Faces)
            {
                if (cellDihedrals.TryGetValue(face.Cell, out HashSet<Vector3d> dihedral))
                {
                    dihedral.Add(face.Normal);
                }
                else
                {

                    cellDihedrals[face.Cell] = new HashSet<Vector3d> { face.Normal };
                }

            }

            PFCell result = new PFCell();

            foreach (var keyValue in cellDihedrals)
            {
                if (Util.InsideHedra(Vertices[0].Point, position, keyValue.Value.ToList()))
                {
                    result = keyValue.Key;
                    break;

                }
            }
            return result;

        }

        /// <summary>
        /// Finds the intersecting face and the resulting point for an offset edge
        /// Based on a point where the edge will be. Edge stays parallel to itself.
        /// </summary>
        /// <param name="topoLink">The moved vertex from the edge</param>
        /// <param name="position">New position for the moved edge</param>
        /// <param name="capFace">The face intersected with the offset. </param>
        /// <returns>The point of intersection</returns>
        internal Point3d OffsetIntersection(PFVertex topoLink, PfoamRef coresp, out PFVertex otherVert, out PFFace capFace)
        {

            Point3d position = coresp.Vertices[topoLink].Position;
            // for this I need to build overloads to see for special cases - when starting point is in face, or in edge
            otherVert = Vertices.Single(x => x != topoLink);
            var edgeDirection = otherVert.Point - topoLink.Point;
            edgeDirection.Unitize();
            // find all the unique faces of the other point to intersect with 
            // get plane for face 
            // find intersection closest to position -> get that face (use normal to find it and its cell)
            //-----------------------------------------------------------
            // for each face test to see if coresp object contains an updated plane for the face 
            //-----------------------------------------------------------
            // this is the otherPosition - I should output also the point and the corresponding face (cell) 
            // if some faces(or their pair) are part of open cells exclude those faces from the intersections 
            var edgeFaces = new HashSet<PFFace>(Faces);
            edgeFaces.UnionWith(Pair.Faces);
            var otherVertFaces = new HashSet<PFFace>(otherVert.Faces);

            var edgeCapFaces = otherVertFaces.Except(edgeFaces);

            var facingCapFaces = edgeFaces.Where(x => !x.External && Dot(x.Normal, edgeDirection) > 0).ToList();
            if (edgeCapFaces.Count() < 1)
            {
                capFace = null;
                return position + edgeDirection * GetLength();
            }
            var offsetLine = new Line(position, position + edgeDirection);

            // now intersect line with all face planes 
            double minPara = double.MaxValue;
            int minParaIndex = 0;
            for (int i = 0; i < facingCapFaces.Count(); i++)
            {
                var facePlane = coresp.Faces[facingCapFaces[i]].Plane;
                if (Rhino.Geometry.Intersect.Intersection.LinePlane(offsetLine, facePlane, out double param))
                {
                    if (param < minPara)
                    {
                        minPara = param;
                        minParaIndex = i;
                    }
                }
            }

            capFace = facingCapFaces[minParaIndex];

            return offsetLine.PointAt(minPara);
        }

        /// <summary>
        /// Intersects an offset edge with another edge from the same face
        /// The second edge is at the end of the point 
        /// </summary>
        /// <param name="topoLink"></param>
        /// <param name="position"></param>
        /// <param name="inFace"></param>
        /// <returns></returns>
        internal Point3d OffsetIntersection(PFVertex topoLink, PfoamRef coresp, PFFace inFace, out PFVertex otherVert, out PFEdge capEdge)
        {
            // test if position is in the inFace
            // intersect an edge with another edge - edges are part of the same face plane
            //-----------------------------------------------------------
            // for each edge test to see if coresp object contains an updated line for the edge 
            //-----------------------------------------------------------
            // there will be an line/line intersection
            // if other point has more than 4 connections some connections will require type 1 calculations 
            Point3d position = coresp.Vertices[topoLink].Position;
            otherVert = Vertices.Single(x => x != topoLink);
            var edgeDirection = otherVert.Point - topoLink.Point;
            var offsetLine = new Line(position, position + edgeDirection);
            capEdge = otherVert.Edges.Single(x => inFace.Edges.Contains(x));
            var otherLine = coresp.Edges[capEdge].Line;
            var intersection = Rhino.Geometry.Intersect.Intersection.LineLine(offsetLine, otherLine, out double a, out double b);

            return offsetLine.PointAt(a);
        }

        public string SerializeJson()
        {
            //TODO see what happens for nulls 

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
                { "Pair", Pair?.Id ?? 0 },
                { "Faces", Faces.Select(x => x.Id).ToList() },
                { "FaceAngle", FaceAngle },
                { "Dual", Dual?.Id ?? 0 },
                { "External", External },
                { "Deviation", Deviation },


                { "TargetLength", TargetLength },
                { "MinLength", MinLength },
                { "MaxLength", MaxLength },
                { "InfluenceCoef", InfluenceCoef }
            };

            return props;
        }


    }
}
