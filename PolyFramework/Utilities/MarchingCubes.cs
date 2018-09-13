using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace PolyFramework.Utilities
{
    public enum MarchType
    {
        Cubes,
        Tetra
    }

    public enum VoxelPos
    {
        Undef,
        Over,
        Under,
        On

    }

    public interface IMarching
    {
        double IsoVal { get; set; }
        MarchType MarchType { get; set; }

        void Generate(IList<float> voxels, int width, int height, int depth, IList<Point3d> verts, IList<int> indices);
    }


    public abstract class Marching : IMarching
    {
        public double IsoVal { get; set; }
        public Cube Cube { get; set; }
        public MarchType MarchType { get; set; }
        public bool Orientation { get; private set; }

        public Marching(double isoVal = 0.0)
        {
            IsoVal = isoVal;
            if (isoVal > 0.0) Orientation = true;
        }

        public virtual void Generate(IList<Voxel> voxels, IList<Mesh> cubeTriangles)
        {

        }

    }

    public class Voxel
    {
        /// <summary>
        /// Location of the voxel in 3d space
        /// </summary>
        public Point3d Location { get; private set; }
        /// <summary>
        /// Iso value
        /// </summary>
        public double Value { get; private set; }

        public Voxel(Point3d location, double value)
        {
            Location = location;
            Value = value;
        }

        public Voxel(Point3d point3D)
        {
            Location = Location;
            Value = 0.0;
        }
    }

    public class Voxels
    {
        private Dictionary<Tuple<int, int, int>, Voxel> _voxelDict = new Dictionary<Tuple<int, int, int>, Voxel>();
        public Voxel this[Tuple<int, int, int> key
            ]
        {
            get { return _voxelDict[key]; }
            set { _voxelDict[key] = value; }
        }
        public Plane Plane { get; set; }
        public double XSize { get; set; }
        public double YSize { get; set; }
        public double ZSize { get; set; }
        public int XCount { get; set; }
        public int YCount { get; set; }
        public int ZCount { get; set; }





    }

    public class Cube
    {
        public Voxel[] Vertices { get; private set; }



        /// <summary>
        /// EdgeConnection lists the index of the endpoint vertices for each 
        /// of the 12 edges of the cube.
        /// edgeConnection[12][2]
        /// </summary>
        private static readonly int[,] EdgeConnection = new int[,]
       {
            {0,1}, {1,2}, {2,3}, {3,0},
            {4,5}, {5,6}, {6,7}, {7,4},
            {0,4}, {1,5}, {2,6}, {3,7}
       };

        /// <summary>
        /// edgeDirection lists the direction vector (vertex1-vertex0) for each edge in the cube.
        /// </summary>
        private static readonly Vector3d[] EdgeDirection = new Vector3d[]
        {
            Vector3d.XAxis, Vector3d.YAxis, -Vector3d.XAxis, -Vector3d.YAxis,
            Vector3d.XAxis, Vector3d.YAxis, -Vector3d.XAxis, -Vector3d.YAxis,
            Vector3d.ZAxis,  Vector3d.ZAxis, Vector3d.ZAxis, Vector3d.ZAxis
            /*
            {1.0f, 0.0f, 0.0f},{0.0f, 1.0f, 0.0f},{-1.0f, 0.0f, 0.0f},{0.0f, -1.0f, 0.0f},
            {1.0f, 0.0f, 0.0f},{0.0f, 1.0f, 0.0f},{-1.0f, 0.0f, 0.0f},{0.0f, -1.0f, 0.0f},
            {0.0f, 0.0f, 1.0f},{0.0f, 0.0f, 1.0f},{ 0.0f, 0.0f, 1.0f},{0.0f,  0.0f, 1.0f}
            */
        };



    }
}
