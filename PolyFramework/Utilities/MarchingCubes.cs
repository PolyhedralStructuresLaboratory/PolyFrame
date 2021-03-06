﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;
using System.Collections.Concurrent;


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

        Mesh Generate(Voxels voxels);
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
        /// <summary>
        /// This generates the cubes and uses an appropriate method to march them
        /// Method assumes all the voxels are populated with values (NaN is considered a value outside the search)
        /// </summary>
        /// <param name="voxels"></param>
        /// <param name="cubeTriangles"></param>
        public virtual Mesh Generate(Voxels voxels)
        {
            // local method for cube making from 1 voxel 
            Cube CubeFromVoxel(Voxel voxel)
            {
                Cube cube = new Cube()
                {
                    Vertices = new Voxel[]
                    {
                        voxel,
                        voxels[new Tuple<int, int, int>(voxel.Ix+1, voxel.Iy, voxel.Iz)],
                        voxels[new Tuple<int, int, int>(voxel.Ix+1, voxel.Iy+1, voxel.Iz)],
                        voxels[new Tuple<int, int, int>(voxel.Ix, voxel.Iy+1, voxel.Iz)],
                        voxels[new Tuple<int, int, int>(voxel.Ix, voxel.Iy, voxel.Iz+1)],
                        voxels[new Tuple<int, int, int>(voxel.Ix+1, voxel.Iy, voxel.Iz+1)],
                        voxels[new Tuple<int, int, int>(voxel.Ix+1, voxel.Iy+1, voxel.Iz+1)],
                        voxels[new Tuple<int, int, int>(voxel.Ix, voxel.Iy+1, voxel.Iz+1)],
                    }
                };

                return cube;
            }


            // make cubes 
            var meshFaces = new List<Tuple<Voxel, Voxel>[]>();
            var mesh = new Mesh();
            var vertexDict = new Dictionary<Tuple<Voxel, Voxel>, int>();
            for (int x = 0; x < voxels.XCount - 1; x++)
            {
                for (int y = 0; y < voxels.YCount - 1; y++)
                {
                    for (int z = 0; z < voxels.ZCount - 1; z++)
                    {
                        var cb = CubeFromVoxel(voxels[new Tuple<int, int, int>(x, y, z)]);
                        // here march the cube 
                        March(cb, mesh, vertexDict);
                    }
                }
            }

            //mesh.Faces.AddFaces(meshFaces);
            return mesh;


        }

        /// <summary>
        /// This generates the cubes and uses an appropriate method to march them
        /// Method assumes all the voxels are populated with values (NaN is considered a value outside the search)
        /// </summary>
        /// <param name="voxels"></param>
        public virtual Mesh Generate_Para(Voxels voxels)
        {
            // local method for cube making from 1 voxel 
            Cube CubeFromVoxel(Voxel voxel)
            {
                Cube cube = new Cube()
                {
                    Vertices = new Voxel[]
                    {
                        voxel,
                        voxels[new Tuple<int, int, int>(voxel.Ix+1, voxel.Iy, voxel.Iz)],
                        voxels[new Tuple<int, int, int>(voxel.Ix+1, voxel.Iy+1, voxel.Iz)],
                        voxels[new Tuple<int, int, int>(voxel.Ix, voxel.Iy+1, voxel.Iz)],
                        voxels[new Tuple<int, int, int>(voxel.Ix, voxel.Iy, voxel.Iz+1)],
                        voxels[new Tuple<int, int, int>(voxel.Ix+1, voxel.Iy, voxel.Iz+1)],
                        voxels[new Tuple<int, int, int>(voxel.Ix+1, voxel.Iy+1, voxel.Iz+1)],
                        voxels[new Tuple<int, int, int>(voxel.Ix, voxel.Iy+1, voxel.Iz+1)],
                    }
                };

                return cube;
            }


            // make cubes 
            var meshFaces = new ConcurrentStack<Tuple<Voxel, Voxel>[]>();
            var mesh = new Mesh();
            var vertexDict = new ConcurrentDictionary<Tuple<Voxel, Voxel>, Point3d>();
            Parallel.For(0, voxels.XCount - 1, x =>
              {
                  for (int y = 0; y < voxels.YCount - 1; y++)
                  {
                      for (int z = 0; z < voxels.ZCount - 1; z++)
                      {
                          var cb = CubeFromVoxel(voxels[new Tuple<int, int, int>(x, y, z)]);
                          // here march the cube 
                          March_Para(cb, meshFaces, vertexDict);
                      }
                  }
              });


            // make the mesh from the vertices and face arrays - this is single thread 
            var meshVertDict = new Dictionary<Tuple<Voxel, Voxel>, int>();
            foreach (var mFAce in meshFaces)
            {
                var faceVertIndexes = new int[3];
                for (int i = 0; i < 3; i++)
                {
                    if (meshVertDict.ContainsKey(mFAce[i]))
                    {
                        faceVertIndexes[i] = meshVertDict[mFAce[i]];
                    }
                    else if (meshVertDict.ContainsKey(new Tuple<Voxel, Voxel>(mFAce[i].Item2, mFAce[i].Item1)))
                    {
                        faceVertIndexes[i] = meshVertDict[new Tuple<Voxel, Voxel>(mFAce[i].Item2, mFAce[i].Item1)];
                    }
                    else
                    {
                        mesh.Vertices.Add(vertexDict[mFAce[i]]);
                        meshVertDict.Add(mFAce[i], mesh.Vertices.Count - 1);
                        faceVertIndexes[i] = mesh.Vertices.Count - 1;
                    }
                }
                if (Orientation) mesh.Faces.AddFace(faceVertIndexes[0], faceVertIndexes[1], faceVertIndexes[2]);
                else mesh.Faces.AddFace(faceVertIndexes[2], faceVertIndexes[1], faceVertIndexes[0]);

            }

            return mesh;


        }


        /// <summary>
        /// This is the abstract march method to be implemented in the particularizations of the class.
        /// This is the single thread version 
        /// </summary>
        /// <param name="cube">individual cube of voxels</param>
        /// <param name="mesh">the gradually built mesh</param>
        /// <param name="vertDict">dictionary to keep score of the interpolated cube edge vertices as mesh vertices</param>
        protected abstract void March(Cube cube, Mesh mesh, IDictionary<Tuple<Voxel, Voxel>, int> vertDict);

        /// <summary>
        /// This is the abstract march method to be implemented in the particularizations of the class.
        /// This is the multiThread one 
        /// </summary>
        /// <param name="cube">individual cube of voxels</param>
        /// <param name="meshFaceVerts">a thread safe collection to hold the faces as arrays of voxels</param>
        /// <param name="vertDict">dictionary to keep score of the interpolated cube edge vertices as Points in 3d space</param>
        protected abstract void March_Para(Cube cube, ConcurrentStack<Tuple<Voxel, Voxel>[]> meshFaceVerts, ConcurrentDictionary<Tuple<Voxel, Voxel>, Point3d> vertDict);

        public virtual double InterpolateValue(double v0, double v1)
        {
            var diff = v1 - v0;
            if (diff == 0.0) return IsoVal;
            else return (IsoVal - v0) / diff;
        }

    }


    public class MarchingCubes : Marching
    {
        //private Point3d[] EdgeVert { get; set; }

        public MarchingCubes(double isoVal = 0) : base(isoVal)
        {
            //EdgeVert = new Point3d[12];
        }

        protected override void March(Cube cube, Mesh mesh, IDictionary<Tuple<Voxel, Voxel>, int> vertDict)
        {
            // flag for bitwise storing of cube vertex position above or below isoVal
            // 0000 0000 = all vertices below isoVal = no intersections 
            // 0000 0111 = 4 vertices below isoVal = some intersections  
            int flagIndex = 0;

            // find the vertices inside the surface (smaller than the isoVal)
            // for each vertex the for loop moves a 1 value one step to the left 00000001 becomes 00000010 
            // if the vertex voxel value is smaller than isoVal a bitwise or is performed 
            // 00001000 |=
            // 00010000 in any bit is 1 the result is 1
            // 00011000 this means that vertex 3 and 4 are below the isoVal 
            // the int equiv of this is 24 or hex 0x18

            for (int i = 0; i < 8; i++) if (cube.Vertices[i].Value <= IsoVal) flagIndex |= 1 << i;

            // edgeFlags is retrieved from a table 
            // edgeFlags is a 12 bit flag similar to the above vertex flag for the cube 
            // 0000 0001 0011 means that edge 0,1 and 4 are intersected

            int edgeFlags = CubeEdgeFlags[flagIndex];

            // edgeFlags == 0 means no intersection 

            if (edgeFlags == 0) return;

            // else test all edges and determine the intersection point through value average between the cube vertices 

            for (int i = 0; i < 12; i++)
            {
                if ((edgeFlags & (1 << i)) != 0) // this tests the bit nr i if is not 0. Test is performed through bitwise or
                {
                    // make a unique topological identifier for each edge (edgeVertex) of the cube system. 
                    var edgeKey = new Tuple<Voxel, Voxel>(cube.Vertices[Cube.EdgeConnection[i, 0]], cube.Vertices[Cube.EdgeConnection[i, 1]]);
                    // test to see if edge (edgeVertex) is in the vertexDict
                    // this ensures each edgeVertex is computed only once and reused in constant time
                    if (!vertDict.ContainsKey(edgeKey))
                    {
                        // interpolate point from values 
                        var param = InterpolateValue(cube.Vertices[Cube.EdgeConnection[i, 0]].Value, cube.Vertices[Cube.EdgeConnection[i, 1]].Value);
                        // add to mesh vertices 
                        mesh.Vertices.Add((cube.Vertices[Cube.EdgeConnection[i, 1]].Location - cube.Vertices[Cube.EdgeConnection[i, 0]].Location) * param +
                        cube.Vertices[Cube.EdgeConnection[i, 0]].Location);
                        // hash it in the vertDict 
                        vertDict[edgeKey] = mesh.Vertices.Count - 1;
                    }
                }
            }

            // create the triangles - max 5 per cube 

            for (int i = 0; i < 5; i++)
            {
                if (TriangleConnectionTable[flagIndex, i * 3] < 0) break; // if the next value for index is -1 in the table, break 
                // face list of vertices 
                var faceVertIndexes = new int[3];
                for (int j = 0; j < 3; j++)
                {
                    // get the edgeVert index from the table - it should be there 
                    var vertIndex = TriangleConnectionTable[flagIndex, i * 3 + j];
                    // recompute the unique topological voxel key
                    // all cubes have the same topology -> get the cube edge(edgeVert) index from the TriangleConnectionTable - get end voxels for the key  
                    var edgeKey = new Tuple<Voxel, Voxel>(cube.Vertices[Cube.EdgeConnection[vertIndex, 0]], cube.Vertices[Cube.EdgeConnection[vertIndex, 1]]);
                    // the key should be in the vertDict - if not something is wrong.
                    faceVertIndexes[j] = vertDict[edgeKey];
                }

                // this should keep faces oriented consistently 
                if (Orientation) mesh.Faces.AddFace(faceVertIndexes[0], faceVertIndexes[1], faceVertIndexes[2]);
                else mesh.Faces.AddFace(faceVertIndexes[2], faceVertIndexes[1], faceVertIndexes[0]);

            }





        }

        protected override void March_Para(Cube cube, ConcurrentStack<Tuple<Voxel, Voxel>[]> meshFaceVerts, ConcurrentDictionary<Tuple<Voxel, Voxel>, Point3d> vertDict)
        {
            // flag for bitwise storing of cube vertex position above or below isoVal
            // 0000 0000 = all vertices below isoVal = no intersections 
            // 0000 0111 = 4 vertices below isoVal = some intersections  
            int flagIndex = 0;

            // find the vertices inside the surface (smaller than the isoVal)
            // for each vertex the for loop moves a 1 value one step to the left 00000001 becomes 00000010 
            // if the vertex voxel value is smaller than isoVal a bitwise or is performed 
            // 00001000 |=
            // 00010000 in any bit is 1 the result is 1
            // 00011000 this means that vertex 3 and 4 are below the isoVal 
            // the int equiv of this is 24 or hex 0x18

            for (int i = 0; i < 8; i++) if (cube.Vertices[i].Value <= IsoVal) flagIndex |= 1 << i;

            // edgeFlags is retrieved from a table 
            // edgeFlags is a 12 bit flag similar to the above vertex flag for the cube 
            // 0000 0001 0011 means that edge 0,1 and 4 are intersected

            int edgeFlags = CubeEdgeFlags[flagIndex];

            // edgeFlags == 0 means no intersection 

            if (edgeFlags == 0) return;

            // else test all edges and determine the intersection point through value average between the cube vertices 

            for (int i = 0; i < 12; i++)
            {
                if ((edgeFlags & (1 << i)) != 0) // this tests the bit nr i if is not 0. Test is performed through bitwise or
                {
                    // make a unique topological identifier for each edge (edgeVertex) of the cube system. 
                    var edgeKey = new Tuple<Voxel, Voxel>(cube.Vertices[Cube.EdgeConnection[i, 0]], cube.Vertices[Cube.EdgeConnection[i, 1]]);
                    // test to see if edge (edgeVertex) is in the vertexDict
                    // this ensures each edgeVertex is computed only once and reused in constant time
                    if (!vertDict.ContainsKey(edgeKey))
                    {
                        // interpolate point from values 
                        var param = InterpolateValue(cube.Vertices[Cube.EdgeConnection[i, 0]].Value, cube.Vertices[Cube.EdgeConnection[i, 1]].Value);

                        vertDict[edgeKey] = ((cube.Vertices[Cube.EdgeConnection[i, 1]].Location - cube.Vertices[Cube.EdgeConnection[i, 0]].Location) * param +
                        cube.Vertices[Cube.EdgeConnection[i, 0]].Location);
                        // hash it in the vertDict 

                    }
                }
            }

            // create the triangles - max 5 per cube 

            for (int i = 0; i < 5; i++)
            {
                if (TriangleConnectionTable[flagIndex, i * 3] < 0) break; // if the next value for index is -1 in the table, break 
                // face list of vertices 
                var faceVertIndexes = new Tuple<Voxel, Voxel>[3];
                for (int j = 0; j < 3; j++)
                {
                    // get the edgeVert index from the table - it should be there 
                    var vertIndex = TriangleConnectionTable[flagIndex, i * 3 + j];
                    // recompute the unique topological voxel key
                    // all cubes have the same topology -> get the cube edge(edgeVert) index from the TriangleConnectionTable - get end voxels for the key  
                    var edgeKey = new Tuple<Voxel, Voxel>(cube.Vertices[Cube.EdgeConnection[vertIndex, 0]], cube.Vertices[Cube.EdgeConnection[vertIndex, 1]]);
                    // the key should be in the vertDict - if not something is wrong.
                    faceVertIndexes[j] = edgeKey;
                }

                // orientation now takes place in create method 
                meshFaceVerts.Push(faceVertIndexes);



            }
        }


        /// <summary>
        /// For any edge, if one vertex is inside of the surface and the other 
        /// is outside of the surface then the edge intersects the surface.
        /// For each of the 8 vertices of the cube can be two possible states,
        /// either inside or outside of the surface.
        /// For any cube the are 2^8=256 possible sets of vertex states.
        /// This table lists the edges intersected by the surface for all 256 
        /// possible vertex states. There are 12 edges.  
        /// For each entry in the table, if edge #n is intersected, then bit #n is set to 1.
        /// cubeEdgeFlags[256]
        /// </summary>
        private static readonly int[] CubeEdgeFlags = new int[]
        {
        0x000, 0x109, 0x203, 0x30a, 0x406, 0x50f, 0x605, 0x70c, 0x80c, 0x905, 0xa0f, 0xb06, 0xc0a, 0xd03, 0xe09, 0xf00,
        0x190, 0x099, 0x393, 0x29a, 0x596, 0x49f, 0x795, 0x69c, 0x99c, 0x895, 0xb9f, 0xa96, 0xd9a, 0xc93, 0xf99, 0xe90,
        0x230, 0x339, 0x033, 0x13a, 0x636, 0x73f, 0x435, 0x53c, 0xa3c, 0xb35, 0x83f, 0x936, 0xe3a, 0xf33, 0xc39, 0xd30,
        0x3a0, 0x2a9, 0x1a3, 0x0aa, 0x7a6, 0x6af, 0x5a5, 0x4ac, 0xbac, 0xaa5, 0x9af, 0x8a6, 0xfaa, 0xea3, 0xda9, 0xca0,
        0x460, 0x569, 0x663, 0x76a, 0x066, 0x16f, 0x265, 0x36c, 0xc6c, 0xd65, 0xe6f, 0xf66, 0x86a, 0x963, 0xa69, 0xb60,
        0x5f0, 0x4f9, 0x7f3, 0x6fa, 0x1f6, 0x0ff, 0x3f5, 0x2fc, 0xdfc, 0xcf5, 0xfff, 0xef6, 0x9fa, 0x8f3, 0xbf9, 0xaf0,
        0x650, 0x759, 0x453, 0x55a, 0x256, 0x35f, 0x055, 0x15c, 0xe5c, 0xf55, 0xc5f, 0xd56, 0xa5a, 0xb53, 0x859, 0x950,
        0x7c0, 0x6c9, 0x5c3, 0x4ca, 0x3c6, 0x2cf, 0x1c5, 0x0cc, 0xfcc, 0xec5, 0xdcf, 0xcc6, 0xbca, 0xac3, 0x9c9, 0x8c0,
        0x8c0, 0x9c9, 0xac3, 0xbca, 0xcc6, 0xdcf, 0xec5, 0xfcc, 0x0cc, 0x1c5, 0x2cf, 0x3c6, 0x4ca, 0x5c3, 0x6c9, 0x7c0,
        0x950, 0x859, 0xb53, 0xa5a, 0xd56, 0xc5f, 0xf55, 0xe5c, 0x15c, 0x055, 0x35f, 0x256, 0x55a, 0x453, 0x759, 0x650,
        0xaf0, 0xbf9, 0x8f3, 0x9fa, 0xef6, 0xfff, 0xcf5, 0xdfc, 0x2fc, 0x3f5, 0x0ff, 0x1f6, 0x6fa, 0x7f3, 0x4f9, 0x5f0,
        0xb60, 0xa69, 0x963, 0x86a, 0xf66, 0xe6f, 0xd65, 0xc6c, 0x36c, 0x265, 0x16f, 0x066, 0x76a, 0x663, 0x569, 0x460,
        0xca0, 0xda9, 0xea3, 0xfaa, 0x8a6, 0x9af, 0xaa5, 0xbac, 0x4ac, 0x5a5, 0x6af, 0x7a6, 0x0aa, 0x1a3, 0x2a9, 0x3a0,
        0xd30, 0xc39, 0xf33, 0xe3a, 0x936, 0x83f, 0xb35, 0xa3c, 0x53c, 0x435, 0x73f, 0x636, 0x13a, 0x033, 0x339, 0x230,
        0xe90, 0xf99, 0xc93, 0xd9a, 0xa96, 0xb9f, 0x895, 0x99c, 0x69c, 0x795, 0x49f, 0x596, 0x29a, 0x393, 0x099, 0x190,
        0xf00, 0xe09, 0xd03, 0xc0a, 0xb06, 0xa0f, 0x905, 0x80c, 0x70c, 0x605, 0x50f, 0x406, 0x30a, 0x203, 0x109, 0x000
        };

        /// <summary>
        /// For each of the possible vertex states listed in cubeEdgeFlags there is a specific triangulation
        /// of the edge intersection points.  triangleConnectionTable lists all of them in the form of
        /// 0-5 edge triples with the list terminated by the invalid value -1.
        /// For example: triangleConnectionTable[3] list the 2 triangles formed when corner[0] 
        /// and corner[1] are inside of the surface, but the rest of the cube is not.
        /// triangleConnectionTable[256][16]
        /// </summary>
        private static readonly int[,] TriangleConnectionTable = new int[,]
        {
        {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1},
        {3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1},
        {3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1},
        {3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1},
        {9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1},
        {9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
        {2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1},
        {8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1},
        {9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
        {4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1},
        {3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1},
        {1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1},
        {4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1},
        {4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
        {5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1},
        {2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1},
        {9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
        {0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
        {2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1},
        {10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1},
        {5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1},
        {5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1},
        {9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1},
        {0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1},
        {1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1},
        {10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1},
        {8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1},
        {2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1},
        {7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1},
        {2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1},
        {11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1},
        {5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1},
        {11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1},
        {11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
        {1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1},
        {9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1},
        {5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1},
        {2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
        {5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1},
        {6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1},
        {3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1},
        {6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1},
        {5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1},
        {1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
        {10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1},
        {6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1},
        {8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1},
        {7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1},
        {3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
        {5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1},
        {0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1},
        {9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1},
        {8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1},
        {5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1},
        {0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1},
        {6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1},
        {10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1},
        {10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1},
        {8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1},
        {1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1},
        {0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1},
        {10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1},
        {3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1},
        {6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1},
        {9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1},
        {8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1},
        {3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1},
        {6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1},
        {0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1},
        {10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1},
        {10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1},
        {2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1},
        {7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1},
        {7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1},
        {2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1},
        {1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1},
        {11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1},
        {8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1},
        {0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1},
        {7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
        {10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
        {2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
        {6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1},
        {7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1},
        {2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1},
        {1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1},
        {10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1},
        {10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1},
        {0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1},
        {7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1},
        {6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1},
        {8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1},
        {9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1},
        {6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1},
        {4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1},
        {10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1},
        {8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1},
        {0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1},
        {1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1},
        {8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1},
        {10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1},
        {4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1},
        {10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
        {5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
        {11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1},
        {9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
        {6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1},
        {7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1},
        {3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1},
        {7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1},
        {3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1},
        {6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1},
        {9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1},
        {1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1},
        {4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1},
        {7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1},
        {6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1},
        {3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1},
        {0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1},
        {6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1},
        {0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1},
        {11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1},
        {6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1},
        {5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1},
        {9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1},
        {1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1},
        {1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1},
        {10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1},
        {0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1},
        {5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1},
        {10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1},
        {11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1},
        {9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1},
        {7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1},
        {2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1},
        {8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1},
        {9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1},
        {9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1},
        {1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1},
        {9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1},
        {9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1},
        {5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1},
        {0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1},
        {10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1},
        {2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1},
        {0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1},
        {0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1},
        {9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1},
        {5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1},
        {3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1},
        {5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1},
        {8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1},
        {0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1},
        {9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1},
        {1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1},
        {3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1},
        {4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1},
        {9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1},
        {11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1},
        {11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1},
        {2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1},
        {9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1},
        {3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1},
        {1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1},
        {4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1},
        {3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1},
        {0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1},
        {9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1},
        {1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}
        };





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
        public double Value { get; set; }
        public int Ix { get; set; }
        public int Iy { get; set; }
        public int Iz { get; set; }

        public Voxel(Point3d location, double value, int ix, int iy, int iz)
        {
            Location = location;
            Value = value;
            Ix = ix;
            Iy = iy;
            Iz = iz;

        }

        public Voxel(Point3d point3D, int ix, int iy, int iz)
        {
            Location = point3D;
            Value = double.NaN;
            Ix = ix;
            Iy = iy;
            Iz = iz;
        }


        public double Factor { get; set; } = 1.0;

        public override string ToString()
        {
            return $"X={Ix}, Y={Iy}, Z={Iz}, Value={Value}, Factor={Factor}";
        }
        /*
        public override bool Equals(object obj)
        {
            if (obj is Voxel)
            {
                var otherVox = obj as Voxel;
                return this.Ix == otherVox.Ix && this.Iy == otherVox.Iy && this.Iy == otherVox.Iz;
            }

            else return false;
            
        }

        public override int GetHashCode()
        {
            return Ix * 100000000 + Iy * 10000 + Iz;
            //return base.GetHashCode();
        }
        */
    }

    public class Voxels
    {
        public IDictionary<Tuple<int, int, int>, Voxel> _voxelDict = new ConcurrentDictionary<Tuple<int, int, int>, Voxel>();
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

        public Voxels(Plane plane, double x_size, double y_size, double z_size, int x_count, int y_count, int z_count, bool para = false)
        {
            Plane = plane;
            XSize = x_size;
            YSize = y_size;
            ZSize = z_size;
            XCount = x_count;
            YCount = y_count;
            ZCount = z_count;

            if (para)
            {
                Parallel.For(0, x_count, ix =>
                {
                    for (int iy = 0; iy < y_count; iy++)
                    {
                        for (int iz = 0; iz < z_count; iz++)
                        {
                            _voxelDict[new Tuple<int, int, int>(ix, iy, iz)] = new Voxel(plane.Origin +
                                plane.XAxis * x_size * ix + plane.YAxis * y_size * iy + plane.ZAxis * z_size * iz, ix, iy, iz);
                        }
                    }
                });
            }
            else
            {
                for (int ix = 0; ix < x_count; ix++)
                {
                    for (int iy = 0; iy < y_count; iy++)
                    {
                        for (int iz = 0; iz < z_count; iz++)
                        {
                            _voxelDict[new Tuple<int, int, int>(ix, iy, iz)] = new Voxel(plane.Origin +
                                plane.XAxis * x_size * ix + plane.YAxis * y_size * iy + plane.ZAxis * z_size * iz, ix, iy, iz);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Set voxel values based on a geometry set and distance values for each geometry piece 
        /// </summary>
        /// <param name="geos"></param>
        /// <param name="distances"></param>
        public void SetValuesFromGeos(IList<GeometryBase> geos, IList<double> distances, bool squared = false)
        {
            // For each geopiece - find min/max point or box with some wiggle room for each geometry
            // Box aligned - get corners - add wiggle value - get all points inside the box using coordinate system 
            // For each point in box calculate value - if existent value is closer to 0 keep existent value else or if NAN write new

            foreach (var geoPiece in geos.Zip(distances, (x, y) => new { geo = x, dist = y }))
            {
                Curve crv = null;
                if (geoPiece.geo.ObjectType == Rhino.DocObjects.ObjectType.Curve)
                {
                    crv = geoPiece.geo as Curve;
                }
                else
                {
                    throw new PolyFrameworkException("For now we can only work with curves");
                }
                var alBox = geoPiece.geo.GetBoundingBox(Plane);


                var voxelX_start = (int)Math.Floor((alBox.Min.X - geoPiece.dist) / XSize) - 1; voxelX_start = voxelX_start < 0 ? 0 : voxelX_start;
                var voxelY_start = (int)Math.Floor((alBox.Min.Y - geoPiece.dist) / YSize) - 1; voxelY_start = voxelY_start < 0 ? 0 : voxelY_start;
                var voxelZ_start = (int)Math.Floor((alBox.Min.Z - geoPiece.dist) / ZSize) - 1; voxelZ_start = voxelZ_start < 0 ? 0 : voxelZ_start;
                var voxelX_end = (int)Math.Ceiling((alBox.Max.X + geoPiece.dist) / XSize) + 1; voxelX_end = voxelX_end > XCount ? XCount : voxelX_end;
                var voxelY_end = (int)Math.Ceiling((alBox.Max.Y + geoPiece.dist) / YSize) + 1; voxelY_end = voxelY_end > YCount ? YCount : voxelY_end;
                var voxelZ_end = (int)Math.Ceiling((alBox.Max.Z + geoPiece.dist) / ZSize) + 1; voxelZ_end = voxelZ_end > ZCount ? ZCount : voxelZ_end;

                for (int ix = voxelX_start; ix < voxelX_end; ix++)
                {
                    for (int iy = voxelY_start; iy < voxelY_end; iy++)
                    {
                        for (int iz = voxelZ_start; iz < voxelZ_end; iz++)
                        {
                            var ind = new Tuple<int, int, int>(ix, iy, iz);

                            if (crv.ClosestPoint(this[ind].Location, out double t, geoPiece.dist * 10))
                            {

                                var val = squared ? this[ind].Location.DistanceToSquared(crv.PointAt(t)) - Math.Pow(geoPiece.dist, 2) : this[ind].Location.DistanceTo(crv.PointAt(t)) - geoPiece.dist;

                                if (double.IsNaN(this[ind].Value) || this[ind].Value > val)
                                    this[ind].Value = val;
                            }



                        }
                    }
                }


                // voxel y start z start 
                // voxel x,y,z end 
                //alBox. // modify the the coordinates to ge the plane aligned coordinates for the box 
                //use the coordingates to determine the box of voxels that containes the geometry  

            }

        }



        public void SetValuesFromGeos_Para(IList<GeometryBase> geos, IList<double> distances, bool squared = false)
        {
            // For each geopiece - find min/max point or box with some wiggle room for each geometry
            // Box aligned - get corners - add wiggle value - get all points inside the box using coordinate system 
            // For each point in box calculate value - if existent value is closer to 0 keep existent value else or if NAN write new

            Parallel.ForEach(geos.Zip(distances, (x, y) => new { geo = x, dist = y }), geoPiece =>// (var geoPiece in )
            {
                Curve crv = null;
                if (geoPiece.geo.ObjectType == Rhino.DocObjects.ObjectType.Curve)
                {
                    crv = geoPiece.geo as Curve;
                }
                else
                {
                    throw new PolyFrameworkException("For now we can only work with curves");
                }
                var alBox = geoPiece.geo.GetBoundingBox(Plane);


                var voxelX_start = (int)Math.Floor((alBox.Min.X - geoPiece.dist) / XSize) - 1; voxelX_start = voxelX_start < 0 ? 0 : voxelX_start;
                var voxelY_start = (int)Math.Floor((alBox.Min.Y - geoPiece.dist) / YSize) - 1; voxelY_start = voxelY_start < 0 ? 0 : voxelY_start;
                var voxelZ_start = (int)Math.Floor((alBox.Min.Z - geoPiece.dist) / ZSize) - 1; voxelZ_start = voxelZ_start < 0 ? 0 : voxelZ_start;
                var voxelX_end = (int)Math.Ceiling((alBox.Max.X + geoPiece.dist) / XSize) + 1; voxelX_end = voxelX_end > XCount ? XCount : voxelX_end;
                var voxelY_end = (int)Math.Ceiling((alBox.Max.Y + geoPiece.dist) / YSize) + 1; voxelY_end = voxelY_end > YCount ? YCount : voxelY_end;
                var voxelZ_end = (int)Math.Ceiling((alBox.Max.Z + geoPiece.dist) / ZSize) + 1; voxelZ_end = voxelZ_end > ZCount ? ZCount : voxelZ_end;

                for (int ix = voxelX_start; ix < voxelX_end; ix++)
                {
                    for (int iy = voxelY_start; iy < voxelY_end; iy++)
                    {
                        for (int iz = voxelZ_start; iz < voxelZ_end; iz++)
                        {
                            var ind = new Tuple<int, int, int>(ix, iy, iz);

                            if (crv.ClosestPoint(this[ind].Location, out double t, geoPiece.dist * 10))
                            {
                                //here method for eval gradually
                                var val = squared ? this[ind].Location.DistanceToSquared(crv.PointAt(t)) -
                                Math.Pow(geoPiece.dist, 2) : this[ind].Location.DistanceTo(crv.PointAt(t)) - geoPiece.dist;

                                if (double.IsNaN(this[ind].Value) || this[ind].Value > val)
                                    this[ind].Value = val;
                            }



                        }
                    }
                }
            });


            // voxel y start z start 
            // voxel x,y,z end 
            //alBox. // modify the the coordinates to ge the plane aligned coordinates for the box 
            //use the coordingates to determine the box of voxels that containes the geometry  



        }

        public void SetValuesFromEquation()
        {
            for (int i = 0; i < this.XCount; i++)
            {
                for (int j = 0; j < this.YCount; j++)
                {
                    for (int k = 0; k < this.ZCount; k++)
                    {
                        var key = new Tuple<int, int, int>(i, j, k);
                        var vx = this[key];
                        var fact = (0.5 * k / (ZCount - 1) + 0.1 * ((ZCount - 1) - k) / (ZCount - 1));
                        var x = vx.Location.X * fact;
                        var y = vx.Location.Y * fact;
                        var z = vx.Location.Z * fact;

                        //Rhino.RhinoApp.WriteLine(vx.ToString());
                        double val1 = (Math.Sin(x) * Math.Cos(y) +
                                   Math.Sin(y) * Math.Cos(z) +
                                   Math.Sin(z) * Math.Cos(x));

                        


                        double val2 = Math.Cos(x) + Math.Cos(y) + Math.Cos(z);
                        vx.Value = (val1 * i / (XCount - 1) + val2 * ((XCount - 1) - i) / (XCount - 1));
                           
                        // size interpolation 
                        //if (double.IsNaN(val)) throw new Exception("Val is nan!!");
                        this[key] = vx;
                        //Rhino.RhinoApp.WriteLine(val.ToString());
                        // 

                    }
                }
            }
        }

        /*
         * double val2 = (Math.Sin(x) * Math.Sin(y) * Math.Sin(z) +
                                        Math.Sin(x) * Math.Cos(y) * Math.Cos(z) +
                                        Math.Cos(x) * Math.Sin(y) * Math.Cos(z) +
                                        Math.Cos(x) * Math.Cos(y) * Math.Sin(z));
         * 
         * 
          //dd = 0.1 * Math.Cos(pi2 * nx) * Math.Cos(2.0 * pi2 * (nz - ny)) - 0.1 * Math.Cos(pi2 * (3.0 * ny - 2.0 * nz)) + 0.2 * Math.Sin(2.0 * pi2 * (nz + ny));
          //Sherk surface
          // dd = Math.Exp(pz) * Math.Cos(px) - Math.Cos(py);
          //dd = pz * Math.Cos(px) - Math.Cos(py);
          //dd = pz * pz * Math.Cos(px) - Math.Cos(py);
          //Swartz P
          //dd = Math.Cos(px) + Math.Cos(py) + Math.Cos(pz);

          //Diamond
          dd = Math.Sin(px) * Math.Sin(py) * Math.Sin(pz) +
            Math.Sin(px) * Math.Cos(py) * Math.Cos(pz) +
            Math.Cos(px) * Math.Sin(py) * Math.Cos(pz) +
            Math.Cos(px) * Math.Cos(py) * Math.Sin(pz);

          //Gyroid
          dd = Math.Cos(px) * Math.Sin(py) +
          Math.Cos(py) * Math.Sin(pz) +
          Math.Cos(pz) * Math.Sin(px);

          //Neovius

          dd = 3.0 * (Math.Cos(px) + Math.Cos(py) + Math.Cos(pz)) +
          4.0 * (Math.Cos(px) * Math.Cos(py) * Math.Cos(pz));

          //Math.Cos(px) * Math.Cos(py) - Math.Cos(pz) * Math.Cos(py)
         */



        /// <summary>
        /// This needs to be updated ... does not work 
        /// </summary>
        /// <param name="line"></param>
        /// <param name="distance"></param>
        private void SetValuesFromLine(Curve line, double distance)
        {
            var alBox = line.GetBoundingBox(Plane);



            var voxelX_start = (int)Math.Ceiling(alBox.Min.X + distance / XSize) - 1;
            var voxelY_start = (int)Math.Ceiling(alBox.Min.Y + distance / YSize) - 1;
            var voxelZ_start = (int)Math.Ceiling(alBox.Min.Z + distance / ZSize) - 1;
            var voxelX_end = (int)Math.Floor(alBox.Max.X - distance / XSize) + 1;
            var voxelY_end = (int)Math.Floor(alBox.Max.Y - distance / YSize) + 1;
            var voxelZ_end = (int)Math.Floor(alBox.Max.Z - distance / ZSize) + 1;

            for (int ix = voxelX_start; ix < voxelX_start; ix++)
            {
                for (int iy = voxelY_start; iy < voxelY_end; iy++)
                {
                    for (int iz = voxelZ_start; iz < voxelZ_end; ix++)
                    {
                        var ind = new Tuple<int, int, int>(ix, iy, iz);

                        line.ClosestPoint(this[ind].Location, out double t, distance);
                        this[ind].Value = this[ind].Location.DistanceToSquared(line.PointAt(t)) - Math.Pow(distance, 2);

                    }
                }
            }

        }

        public static Voxels MakeFromGeo(IList<GeometryBase> geoList, IList<Double> distList, Plane startPlane, int xDiv, int yDiv, int zDiv, bool para = false)
        {
            var maxDist = distList.Max();
            var allBox = new BoundingBox();
            foreach (var geo in geoList)
            {
                var itemBox = geo.GetBoundingBox(startPlane);
                allBox.Union(itemBox);
            }

            allBox.Inflate(maxDist);
            var originNew = startPlane.PointAt(allBox.Min.X, allBox.Min.Y, allBox.Min.Z);

            var voxPlane = startPlane; voxPlane.Origin = originNew;

            var voxels = new Voxels(voxPlane, allBox.Diagonal.X / xDiv, allBox.Diagonal.Y / yDiv, allBox.Diagonal.Z / zDiv, xDiv, yDiv, zDiv, para);

            return voxels;
        }

        /// <summary>
        /// Uses a box and divisions to make the voxels
        /// </summary>
        /// <param name="box"></param>
        /// <param name="xDiv"></param>
        /// <param name="yDiv"></param>
        /// <param name="zDiv"></param>
        /// <param name="para"></param>
        /// <returns></returns>
        public static Voxels MakeFromBox(Box box, int xDiv, int yDiv, int zDiv, bool para = false)
        {
            var x_step = (box.X.Max - box.X.Min) / xDiv;
            var y_step = (box.Y.Max - box.Y.Min) / yDiv;
            var z_step = (box.Z.Max - box.Z.Min) / zDiv;
            var voxels = new Voxels(box.Plane, x_step, y_step, z_step, xDiv, yDiv, zDiv, para);
            return voxels;
        }

        public IDictionary<Tuple<int, int, int>, Voxel> ExtractVoxels()
        {
            return _voxelDict;

        }

    }

    public class Cube
    {
        public Voxel[] Vertices { get; set; }



        /// <summary>
        /// EdgeConnection lists the index of the endpoint vertices for each 
        /// of the 12 edges of the cube.
        /// edgeConnection[12][2]
        /// </summary>
        internal static readonly int[,] EdgeConnection = new int[,]
       {
            {0,1}, {1,2}, {2,3}, {3,0},
            {4,5}, {5,6}, {6,7}, {7,4},
            {0,4}, {1,5}, {2,6}, {3,7}
       };

        /// <summary>
        /// edgeDirection lists the direction vector (vertex1-vertex0) for each edge in the cube.
        /// </summary>
        internal static readonly Vector3d[] EdgeDirection = new Vector3d[]
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
