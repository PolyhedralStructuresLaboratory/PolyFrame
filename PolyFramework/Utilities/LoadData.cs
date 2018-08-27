using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace PolyFramework
{
    public enum ContainerType
    {
        Edge, FaceBrep, CellBrep, FaceMesh, CellMesh
    }
    public static class LoadData
    {
        /// <summary>
        /// Loads a structure from a set of lines and the attached serial string (JSON)
        /// If there is a dual present it links the dual and outputs it too 
        /// </summary>
        /// <param name="edgeLines">The set of line curves with extra data in .UserDictionary</param>
        /// <param name="dual">The dual. If empty structure then no dual was found</param>
        /// <returns></returns>
        public static PFoam LoadFromEdges(IList<Curve> edgeLines, out PFoam dual, bool updateGeometry = false)
        {
            dual = new PFoam();
            var primal = new PFoam();

            var vertLocations = new Dictionary<int, List<PFVertex>>();


            foreach (var edgeLine in edgeLines)
            {
                if (edgeLine.UserDictionary.TryGetInteger("V0", out int v0))
                {
                    if (vertLocations.TryGetValue(v0, out List<PFVertex> vertList))
                    {
                        vertList.Add(new PFVertex(v0, edgeLine.PointAtStart));
                    }
                    else
                    {
                        vertLocations[v0] = new List<PFVertex> { new PFVertex(v0, edgeLine.PointAtStart) };
                    }
                }
                if (edgeLine.UserDictionary.TryGetInteger("V1", out int v1))
                {
                    if (vertLocations.TryGetValue(v1, out List<PFVertex> vertList))
                    {
                        vertList.Add(new PFVertex(v1, edgeLine.PointAtEnd));
                    }
                    else
                    {
                        vertLocations[v1] = new List<PFVertex> { new PFVertex(v1, edgeLine.PointAtEnd) };
                    }
                }
                if (edgeLine.UserDictionary.TryGetString("Primal", out string primalString))
                {
                    primal = Util.DeserializeFoam(primalString);
                }
                if (edgeLine.UserDictionary.TryGetString("Dual", out string dualString))
                {
                    dual = Util.DeserializeFoam(dualString);
                }

            }

            if (primal.Cells.Count < 1) throw new PolyFrameworkException("No serial data stored in the geometry or data has problems!");

            if (updateGeometry)
            {
                foreach (var vertex in primal.Vertices)
                {
                    if (vertLocations.TryGetValue(vertex.Id, out List<PFVertex> vertPostions))
                    {
                        vertex.Point = PFVertex.AverageVertexes(vertPostions);
                    }
                    if (vertex.Fixed) // update the fixed position for the vertex based on new position
                    {
                        vertex.RestrictSupport = new Point(vertex.Point);
                        //vertex.RestrictPosition = vertex.ConstrainPoint;

                    }
                }

            }

            foreach (var face in primal.Faces)
            {
                face.FaceMesh();
                face.ComputeCentroid();
                face.ComputeFaceNormal();
            }
            foreach (var cell in primal.Cells)
            {
                cell.ComputeCentroid();

            }
            primal.Centroid = PFVertex.AverageVertexes(primal.Vertices);


            if (dual.Cells.Count > 1)
            {
                //Util.ConnectDuals(ref primal, ref dual);

                foreach (var face in dual.Faces)
                {
                    face.FaceMesh();
                    face.ComputeCentroid();
                    face.ComputeFaceNormal();
                }
                foreach (var cell in dual.Cells)
                {
                    cell.ComputeCentroid();

                }
                dual.Centroid = PFVertex.AverageVertexes(dual.Vertices);

            }



            return primal;
        }

        /// <summary>
        /// Loads a structure from a set of breps and the attached serial string (JSON)
        /// If there is a dual present it links the dual and outputs it too 
        /// </summary>
        /// <param name="edgeLines">The set of line curves with extra data in .UserDictionary</param>
        /// <param name="dual">The dual. If empty structure then no dual was found</param>
        /// <returns></returns>
        public static PFoam LoadFromFaces(IList<Brep> faceBreps, out PFoam dual, bool updateGeometry = false)
        {
            dual = new PFoam();
            var primal = new PFoam();

            var vertLocations = new Dictionary<int, List<PFVertex>>();


            foreach (var faceBrep in faceBreps)
            {
                //if (faceBrep.Faces.Count > 1) throw new PolyFrameworkException("LoadFromFaces only works with individual faces!");

                foreach (var bVert in faceBrep.Vertices)
                {
                    if (bVert.UserDictionary.TryGetInteger("Id", out int vertId))
                    {
                        if (vertLocations.TryGetValue(vertId, out List<PFVertex> vertList))
                        {
                            vertList.Add(new PFVertex(vertId, bVert.Location));
                        }
                        else
                        {
                            vertLocations[vertId] = new List<PFVertex> { new PFVertex(vertId, bVert.Location) };
                        }
                    }

                }

                if (faceBrep.UserDictionary.TryGetString("Primal", out string primalString))
                {
                    primal = Util.DeserializeFoam(primalString);
                }
                if (faceBrep.UserDictionary.TryGetString("Dual", out string dualString))
                {
                    dual = Util.DeserializeFoam(dualString);
                }

            }

            if (primal.Cells.Count < 1) throw new PolyFrameworkException("No serial data stored in the geometry or data has problems!");

            if (updateGeometry)
            {
                foreach (var vertex in primal.Vertices)
                {
                    if (vertLocations.TryGetValue(vertex.Id, out List<PFVertex> vertPostions))
                    {
                        vertex.Point = PFVertex.AverageVertexes(vertPostions);
                    }
                }

            }

            foreach (var face in primal.Faces)
            {
                face.FaceMesh();
                face.ComputeCentroid();
                face.ComputeFaceNormal();
            }
            foreach (var cell in primal.Cells)
            {
                cell.ComputeCentroid();

            }
            primal.Centroid = PFVertex.AverageVertexes(primal.Vertices);


            if (dual.Cells.Count > 1)
            {
                //Util.ConnectDuals(ref primal, ref dual);

                foreach (var face in dual.Faces)
                {
                    face.FaceMesh();
                    face.ComputeCentroid();
                    face.ComputeFaceNormal();
                }
                foreach (var cell in dual.Cells)
                {
                    cell.ComputeCentroid();

                }
                dual.Centroid = PFVertex.AverageVertexes(dual.Vertices);

            }



            return primal;
        }

        /// <summary>
        /// Loads a structure from a set of breps and the attached serial string (JSON)
        /// If there is a dual present it links the dual and outputs it too 
        /// </summary>
        /// <param name="edgeLines">The set of line curves with extra data in .UserDictionary</param>
        /// <param name="dual">The dual. If empty structure then no dual was found</param>
        /// <returns></returns>
        public static PFoam LoadFromMeshes(IList<Mesh> meshes, out PFoam dual, bool updateGeometry = false)
        {
            dual = new PFoam();
            var primal = new PFoam();

            var vertLocations = new Dictionary<int, List<PFVertex>>();


            foreach (var mesh in meshes)
            {
                //if (faceBrep.Faces.Count > 1) throw new PolyFrameworkException("LoadFromFaces only works with individual faces!");
                if (mesh.UserDictionary.TryGetDictionary("VertexIds", out Rhino.Collections.ArchivableDictionary vertDict))
                {
                    var sortedVerts = vertDict.ToList().OrderBy(x => int.Parse(x.Key)).Select(y => new Tuple<int, int>(int.Parse(y.Key), (int)y.Value));
                    foreach (var indexId in sortedVerts)
                    {
                        if (vertLocations.TryGetValue(indexId.Item2, out List<PFVertex> vertList))
                        {
                            vertList.Add(new PFVertex(indexId.Item2, new Point3d(mesh.Vertices[indexId.Item1])));
                        }
                        else
                        {
                            vertLocations[indexId.Item2] = new List<PFVertex> { new PFVertex(indexId.Item2, new Point3d(mesh.Vertices[indexId.Item1])) };
                        }
                    }
                }

                



                if (mesh.UserDictionary.TryGetString("Primal", out string primalString))
                {
                    primal = Util.DeserializeFoam(primalString);
                }
                if (mesh.UserDictionary.TryGetString("Dual", out string dualString))
                {
                    dual = Util.DeserializeFoam(dualString);
                }

            }

            if (primal.Cells.Count < 1) throw new PolyFrameworkException("No serial data stored in the geometry or data has problems!");

            if (updateGeometry)
            {
                foreach (var vertex in primal.Vertices)
                {
                    if (vertLocations.TryGetValue(vertex.Id, out List<PFVertex> vertPostions))
                    {
                        vertex.Point = PFVertex.AverageVertexes(vertPostions);
                    }
                }

            }

            foreach (var face in primal.Faces)
            {
                face.FaceMesh();
                face.ComputeCentroid();
                face.ComputeFaceNormal();
            }
            foreach (var cell in primal.Cells)
            {
                cell.ComputeCentroid();

            }
            primal.Centroid = PFVertex.AverageVertexes(primal.Vertices);


            if (dual.Cells.Count > 1)
            {
                //Util.ConnectDuals(ref primal, ref dual);

                foreach (var face in dual.Faces)
                {
                    face.FaceMesh();
                    face.ComputeCentroid();
                    face.ComputeFaceNormal();
                }
                foreach (var cell in dual.Cells)
                {
                    cell.ComputeCentroid();

                }
                dual.Centroid = PFVertex.AverageVertexes(dual.Vertices);

            }



            return primal;
        }

        /// <summary>
        /// Loads a structure from a set of breps and the attached serial string (JSON)
        /// If there is a dual present it links the dual and outputs it too 
        /// </summary>
        /// <param name="edgeLines">The set of line curves with extra data in .UserDictionary</param>
        /// <param name="dual">The dual. If empty structure then no dual was found</param>
        /// <returns></returns>
        public static PFoam LoadFromCells(IList<Brep> cellBreps, out PFoam dual, bool updateGeometry = false)
        {
            dual = new PFoam();
            var primal = new PFoam();

            var vertLocations = new Dictionary<int, List<PFVertex>>();


            foreach (var cellBrep in cellBreps)
            {
                if (cellBrep.Faces.Count < 2) throw new PolyFrameworkException("LoadFromCells only works with PolySurfaces!");

                foreach (var bVert in cellBrep.Vertices)
                {
                    if (bVert.UserDictionary.TryGetInteger("Id", out int vertId))
                    {
                        if (vertLocations.TryGetValue(vertId, out List<PFVertex> vertList))
                        {
                            vertList.Add(new PFVertex(vertId, bVert.Location));
                        }
                        else
                        {
                            vertLocations[vertId] = new List<PFVertex> { new PFVertex(vertId, bVert.Location) };
                        }
                    }

                }

                if (cellBrep.UserDictionary.TryGetString("Primal", out string primalString))
                {
                    primal = Util.DeserializeFoam(primalString);
                }
                if (cellBrep.UserDictionary.TryGetString("Dual", out string dualString))
                {
                    dual = Util.DeserializeFoam(dualString);
                }

            }

            if (primal.Cells.Count < 1) throw new PolyFrameworkException("No serial data stored in the geometry or data has problems!");

            if (updateGeometry)
            {
                foreach (var vertex in primal.Vertices)
                {
                    if (vertLocations.TryGetValue(vertex.Id, out List<PFVertex> vertPostions))
                    {
                        vertex.Point = PFVertex.AverageVertexes(vertPostions);
                    }
                }

            }

            foreach (var face in primal.Faces)
            {
                face.FaceMesh();
                face.ComputeCentroid();
                face.ComputeFaceNormal();
            }
            foreach (var cell in primal.Cells)
            {
                cell.ComputeCentroid();

            }
            primal.Centroid = PFVertex.AverageVertexes(primal.Vertices);


            if (dual.Cells.Count > 1)
            {
                //Util.ConnectDuals(ref primal, ref dual);

                foreach (var face in dual.Faces)
                {
                    face.FaceMesh();
                    face.ComputeCentroid();
                    face.ComputeFaceNormal();
                }
                foreach (var cell in dual.Cells)
                {
                    cell.ComputeCentroid();

                }
                dual.Centroid = PFVertex.AverageVertexes(dual.Vertices);

            }



            return primal;
        }




        /// <summary>
        /// Getter method to load primal/dual from a geometry container from the Rhino Document 

        /// </summary>
        /// <param name="primal">provided primal - need to check if it has data after the method ends</param>
        /// <param name="dual">provided primal - just like primal check if it has cells </param>
        public static IList<Guid> LoadPrimalDual(out PFoam primal, out PFoam dual, out ContainerType container,  bool connect = true)
        {
            primal = new PFoam();
            dual = new PFoam();

            container = ContainerType.Edge;

            GetObject getObj = new GetObject();
            var togGeoUpdate = new OptionToggle(true, "no", "yes");
            var togClearConstraints = new OptionToggle(false, "no", "yes");
            //var togOptionReplace = new OptionToggle(true, "Keep", "Replace");

            getObj.SetCommandPrompt("Pick the geometry container (group of lines, surfaces, polysurfaces or meshes)");


            getObj.AddOptionToggle("UpdateGeo", ref togGeoUpdate);
            getObj.AddOptionToggle("ClearConstraints", ref togClearConstraints);
            //getObj.AddOptionToggle("ReplaceGeo", ref togOptionReplace);
            getObj.GroupSelect = true;
            getObj.GeometryFilter = Rhino.DocObjects.ObjectType.Brep | Rhino.DocObjects.ObjectType.Curve | Rhino.DocObjects.ObjectType.Mesh;

            while (true)
            {

                var r = getObj.GetMultiple(1, 0);
                if (r == GetResult.Cancel) return new List<Guid>();
                else if (r == GetResult.Object) break;
            }

            var guids = getObj.Objects().Select(x => x.ObjectId).ToList();

            var geoObj = getObj.Objects().Select(x => x.Geometry());

            if (geoObj.All(x => x.ObjectType == Rhino.DocObjects.ObjectType.Brep))
            {
                
                var brepInput = geoObj.Cast<Brep>().ToList();

                if (brepInput.Any(x => x.Faces.Count > 1)) container = ContainerType.CellBrep;
                else container = ContainerType.FaceBrep;

                primal = LoadData.LoadFromFaces(brepInput, out dual, togGeoUpdate.CurrentValue);


            }
            else if (geoObj.All(x => x.ObjectType == Rhino.DocObjects.ObjectType.Curve))
            {
                container = ContainerType.Edge;
                var curveGeos = geoObj.Cast<Curve>().ToList();
                primal = LoadData.LoadFromEdges(curveGeos, out dual, togGeoUpdate.CurrentValue);
            }

            else if (geoObj.All(x =>x.ObjectType == Rhino.DocObjects.ObjectType.Mesh))
            {
                var meshGeos = geoObj.Cast<Mesh>().ToList();
                if (meshGeos.Any(x => x.Ngons.Count > 1 ||
                    (x.Ngons.Count == 0 && x.Faces.Count > 1) ||
                    (x.Ngons.Count == 1 && x.Faces.Count > x.Ngons[0].FaceCount))) container = ContainerType.CellMesh;
                else container = ContainerType.FaceMesh;



                primal = LoadData.LoadFromMeshes(meshGeos, out dual, togGeoUpdate.CurrentValue);
            }
            else
            {
                Rhino.RhinoApp.WriteLine("Mixed data detected!");
                return new List<Guid>();
            }


            if (primal.Cells.Count < 1)
            {
                Rhino.RhinoApp.WriteLine("Error creating primal from provided data!");
                return new List<Guid>();
            }

            if (connect && dual.Cells.Count > 1 && primal.Dual.Id == dual.Id)
            {
                Util.ConnectDuals(ref primal, ref dual);
            }


            if (togClearConstraints.CurrentValue)
            {
                foreach (var vert in primal.Vertices)
                {
                    vert.RestrictPosition = null;
                    vert.RestrictSupport = null;
                    vert.SupportGuid = Guid.Empty;
                }
                foreach (var edge in primal.Edges)
                {
                    edge.TargetLength = double.NaN;
                    edge.MaxLength = double.MaxValue;
                    edge.MinLength = double.Epsilon;
                }
                foreach (var face in primal.Faces)
                {
                    face.TargetArea = double.NaN;
                    
                }
            }


            Rhino.RhinoDoc.ActiveDoc.Objects.UnselectAll();
            Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
            return guids;

        }

    }
}
