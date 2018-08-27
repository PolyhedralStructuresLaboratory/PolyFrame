using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino;
using Rhino.Geometry;
using System.Drawing;
using Rhino.Input.Custom;
using Rhino.Input;

namespace PolyFramework
{
    public partial class PFoam
    {
        /// <summary>
        /// Saves the foam as a group of color-coded lines in the Rhino document 
        /// For each line a user dictionary is saved with the following entries:
        /// EdgeId, StartVertexId, EndVertexId
        /// The first line contains a JSON serialization for the whole foam + 
        /// If available the dual will also be saved  
        /// </summary>
        public IList<Guid> SaveAsEdges()
        {
            // first determine if form or force
            // if foam has naked edges then it is form else it is force 

            var doc = Rhino.RhinoDoc.ActiveDoc;
            doc.Views.RedrawEnabled = false;
            var form = Edges.Where(e => e.Id > 0).Any(x => x.Faces.Count < 2);

            var positiveEdges = Edges.Where(x => x.Id > 0);
            var edgeUserDict = new Dictionary<int, Dictionary<string, int>>();

            bool foamSaved = false;

            var guids = new List<Guid>();

            string primal = SerializeJson();
            string dual = Dual?.SerializeJson() ?? "";


            // pre-make the data for the user dictionary for each edge  
            foreach (var edge in positiveEdges)
            {
                var edgeDict = new Dictionary<string, int>();
                edgeDict.Add("Id", edge.Id);
                edgeDict.Add("V0", edge.Vertices[0].Id);
                edgeDict.Add("V1", edge.Vertices[1].Id);
                edgeUserDict.Add(edge.Id, edgeDict);
            }

            // handling different behaviors for form and force 
            if (form)
            {
                var intEdges = new List<PFEdge>();
                var halfExtEdges = new List<PFEdge>();
                var fullExtEdges = new List<PFEdge>();

                foreach (var edge in positiveEdges)
                {
                    if (edge.Id > 0)
                    {
                        if (edge.Vertices[0].External && edge.Vertices[1].External) fullExtEdges.Add(edge);
                        else if (edge.Vertices[0].External ^ edge.Vertices[1].External) halfExtEdges.Add(edge);
                        else intEdges.Add(edge);
                    }
                }

                var areas = intEdges.Select(x => x.Dual?.Area ?? 0.0);
                var maxArea = areas.Count() > 0 ? areas.Max() : 1.0;
                var minArea = areas.Count() > 0 ? areas.Min() : 0.0;
                // create all layers 
                var intLineLayer = new Rhino.DocObjects.Layer()
                {
                    Name = "_Form_InternalLines"
                };
                if (doc.Layers.All(x => x.Name != intLineLayer.Name)) doc.Layers.Add(intLineLayer);
                intLineLayer = doc.Layers.First(x => x.Name == "_Form_InternalLines");

                var extForceLayer = new Rhino.DocObjects.Layer()
                {
                    Name = "_Form_ExternalForces"
                };
                if (doc.Layers.All(x => x.Name != extForceLayer.Name)) doc.Layers.Add(extForceLayer);
                extForceLayer = doc.Layers.First(x => x.Name == "_Form_ExternalForces");

                var externalPolyLayer = new Rhino.DocObjects.Layer()
                {
                    Name = "_Form_ExternalPoly"
                };
                if (doc.Layers.All(x => x.Name != externalPolyLayer.Name)) doc.Layers.Add(externalPolyLayer);
                externalPolyLayer = doc.Layers.First(x => x.Name == "_Form_ExternalPoly");



                // handle the interior edges 
                foreach (var edge in intEdges)
                {
                    
                    var attributes = new Rhino.DocObjects.ObjectAttributes
                    {

                        ObjectColor = Util.CreateBlue(Util.ValueUnitizer(edge.Dual?.Area ?? 1.0, new List<double> { minArea, maxArea }, new List<double> { 0.0, 1.0 })),
                        PlotWeight = Math.Round(Util.ValueUnitizer(edge.Dual?.Area ?? 1.0, new List<double> { minArea, maxArea }, new List<double> { 0, 11 }) * 0.05 + 0.15),
                        ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                        Name = edge.Id.ToString(),
                        LayerIndex = intLineLayer.LayerIndex,


                    };
                    var lineCrv = edge.CreateLine().ToNurbsCurve();

                    foreach (var keyVal in edgeUserDict[edge.Id])
                    {
                        lineCrv.UserDictionary.Set(keyVal.Key, keyVal.Value);
                    }

                    // the foam will be serialized with the first interior edge 
                    // this will include the dual if present 
                    if (!foamSaved)
                    {
                        lineCrv.UserDictionary.Set("Primal", primal);
                        lineCrv.UserDictionary.Set("Dual", dual);
                        foamSaved = true;

                    }

                    guids.Add(doc.Objects.AddCurve(lineCrv, attributes));

                }

                // handle applied forces - or half ext edges 

                foreach (var edge in halfExtEdges)
                {
                    var attributes = new Rhino.DocObjects.ObjectAttributes
                    {

                        ObjectColor = System.Drawing.Color.FromArgb(0, 100, 0),

                        ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                        Name = edge.Id.ToString(),
                        LayerIndex = extForceLayer.LayerIndex,


                    };
                    if (edge.Vertices[1].External) attributes.ObjectDecoration = Rhino.DocObjects.ObjectDecoration.StartArrowhead;
                    else attributes.ObjectDecoration = Rhino.DocObjects.ObjectDecoration.EndArrowhead;

                    var lineCrv = edge.CreateLine().ToNurbsCurve();

                    foreach (var keyVal in edgeUserDict[edge.Id])
                    {
                        lineCrv.UserDictionary.Set(keyVal.Key, keyVal.Value);
                    }
                    if (!foamSaved)
                    {
                        lineCrv.UserDictionary.Set("Primal", primal);
                        lineCrv.UserDictionary.Set("Dual", dual);
                        foamSaved = true;

                    }

                    guids.Add(doc.Objects.AddCurve(lineCrv, attributes));
                }


                // handle external polyhedron or full external edges  

                foreach (var edge in fullExtEdges)
                {
                    var attributes = new Rhino.DocObjects.ObjectAttributes
                    {

                        ObjectColor = System.Drawing.Color.LightGray,

                        ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                        Name = edge.Id.ToString(),
                        LayerIndex = externalPolyLayer.LayerIndex,


                    };
                    var lineCrv = edge.CreateLine().ToNurbsCurve();
                    foreach (var keyVal in edgeUserDict[edge.Id])
                    {
                        lineCrv.UserDictionary.Set(keyVal.Key, keyVal.Value);
                    }
                    guids.Add(doc.Objects.AddCurve(lineCrv, attributes));
                }
            }
            else
            {
                var forceLineLayer = new Rhino.DocObjects.Layer()
                {
                    Name = "_Force_Lines"
                };
                if (doc.Layers.All(x => x.Name != forceLineLayer.Name)) doc.Layers.Add(forceLineLayer);
                forceLineLayer = doc.Layers.First(x => x.Name == "_Force_Lines");


                foreach (var edge in positiveEdges)
                {
                    var attributes = new Rhino.DocObjects.ObjectAttributes
                    {

                        ObjectColor = System.Drawing.Color.DarkSlateGray,

                        ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                        Name = edge.Id.ToString(),
                        LayerIndex = forceLineLayer.LayerIndex,


                    };

                    var lineCrv = edge.CreateLine().ToNurbsCurve();
                    foreach (var keyVal in edgeUserDict[edge.Id])
                    {
                        lineCrv.UserDictionary.Set(keyVal.Key, keyVal.Value);

                    }

                    // the foam will be serialized with the first interior edge 
                    // this will include the dual if present 
                    if (!foamSaved)
                    {
                        lineCrv.UserDictionary.Set("Primal", primal);
                        lineCrv.UserDictionary.Set("Dual", dual);
                        foamSaved = true;

                    }




                    guids.Add(doc.Objects.AddCurve(lineCrv, attributes));
                }
            }


            doc.Views.RedrawEnabled = true;
            doc.Groups.Add(Id.ToString(), guids);
            return guids;
        }


        /// <summary>
        /// Saves the foam object as a set of trimmed surfaces (one faced breps)
        /// For each brep it saves an entry in the user dictionary with the faceId
        /// For each vertex in the brep as Point an entry in the user dictionary is saved with the PFvertex Id
        /// </summary>
        /// <returns></returns>
        public IList<Guid> SaveAsFaces(bool asMesh = false)
        {
            // first determine if form or force
            // if foam has naked edges then it is form else it is force 

            var doc = Rhino.RhinoDoc.ActiveDoc;
            doc.Views.RedrawEnabled = false;
            var form = Edges.Where(e => e.Id > 0).Any(x => x.Faces.Count < 2);

            var positiveFaces = Faces.Where(x => x.Id > 0);
            //var faceUserDict = new Dictionary<int, List<int>>();

            bool foamSaved = false;

            var guids = new List<Guid>();

            string primal = SerializeJson();
            string dual = Dual?.SerializeJson() ?? "";



            if (form)
            {
                // if the foam is a form then just save everything with the same color
                // except the exterior faces (applied forces and supports that will be printed with a different color

                var intFaces = new List<PFFace>();
                var extFaces = new List<PFFace>();

                foreach (var face in positiveFaces)
                {
                    if (face.External) extFaces.Add(face);
                    else intFaces.Add(face);
                }

                var intFaceLayer = new Rhino.DocObjects.Layer()
                {
                    Name = "_Form_InternalFaces"
                };
                if (doc.Layers.All(x => x.Name != intFaceLayer.Name)) doc.Layers.Add(intFaceLayer);
                intFaceLayer = doc.Layers.First(x => x.Name == "_Form_InternalFaces");

                var extFaceLayer = new Rhino.DocObjects.Layer()
                {
                    Name = "_Form_ExternalFaces"
                };
                if (doc.Layers.All(x => x.Name != extFaceLayer.Name)) doc.Layers.Add(extFaceLayer);
                extFaceLayer = doc.Layers.First(x => x.Name == "_Form_ExternalFaces");




                // go through internal faces 
                foreach (var face in intFaces)
                {
                    var attributes = new Rhino.DocObjects.ObjectAttributes
                    {

                        ObjectColor = System.Drawing.Color.SlateGray,

                        ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                        Name = face.Id.ToString(),
                        LayerIndex = intFaceLayer.LayerIndex,


                    };

                    GeometryBase faceGeo;
                    if (!asMesh) faceGeo = face.CreateBrepMatched();
                    else faceGeo = face.FMesh;
                    // the foam will be serialized with the first interior face
                    // this will include the dual if present 
                    if (!foamSaved)
                    {
                        faceGeo.UserDictionary.Set("Primal", primal);
                        faceGeo.UserDictionary.Set("Dual", dual);
                        foamSaved = true;

                    }

                    guids.Add(doc.Objects.Add(faceGeo, attributes));
                }

                // go through the ext faces
                foreach (var face in extFaces)
                {
                    var attributes = new Rhino.DocObjects.ObjectAttributes
                    {

                        ObjectColor = System.Drawing.Color.LightSlateGray,

                        ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                        Name = face.Id.ToString(),
                        LayerIndex = extFaceLayer.LayerIndex,


                    };

                    GeometryBase faceGeo;
                    if (!asMesh) faceGeo = face.CreateBrepMatched();
                    else faceGeo = face.FMesh;

                    if (!foamSaved)
                    {
                        faceGeo.UserDictionary.Set("Primal", primal);
                        faceGeo.UserDictionary.Set("Dual", dual);
                        foamSaved = true;

                    }


                    guids.Add(doc.Objects.Add(faceGeo, attributes));
                }



            }
            else // if force 
            {

                var maxArea = Faces.Select(x => x.Area).Max();
                var minArea = Faces.Select(x => x.Area).Min();
                var forceFaceLayer = new Rhino.DocObjects.Layer()
                {
                    Name = "_Force_Faces"
                };
                if (doc.Layers.All(x => x.Name != forceFaceLayer.Name)) doc.Layers.Add(forceFaceLayer);
                forceFaceLayer = doc.Layers.First(x => x.Name == "_Force_Faces");

                foreach (var face in positiveFaces)
                {
                    var attributes = new Rhino.DocObjects.ObjectAttributes
                    {

                        ObjectColor = Util.CreateBlue(Util.ValueUnitizer(face.Area,
                        new List<double> { minArea, maxArea }, new List<double> { 0.0, 1.0 })),

                        ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                        Name = face.Id.ToString(),
                        LayerIndex = forceFaceLayer.LayerIndex,


                    };



                    GeometryBase faceGeo;
                    if (!asMesh) faceGeo = face.CreateBrepMatched();
                    else faceGeo = face.FMesh;
                    // the foam will be serialized with the first interior face
                    // this will include the dual if present 
                    if (!foamSaved)
                    {
                        faceGeo.UserDictionary.Set("Primal", primal);
                        faceGeo.UserDictionary.Set("Dual", dual);
                        foamSaved = true;
                    }
                    guids.Add(doc.Objects.Add(faceGeo, attributes));

                }


            }

            doc.Views.RedrawEnabled = true;
            doc.Groups.Add(Id.ToString(), guids);


            return guids;
        }


        private static Random rand = new Random();

        /// <summary>
        /// Creates the cell geometry as polySurfaces 
        /// </summary>
        /// <param name="randomCol">If true each group of faces will be with a random colour</param>
        /// <returns>The guids for each cell</returns>
        public IList<Guid> SaveAsCells(bool randomCol = false, bool asMesh = false)
        {
            var doc = Rhino.RhinoDoc.ActiveDoc;
            var foamCells = new List<Guid>();
            string primal = SerializeJson();
            string dual = Dual?.SerializeJson() ?? "";
            bool foamSaved = false;

            foreach (var cell in Cells)
            {
                Color color = Color.FromArgb(rand.Next(50, 200), rand.Next(50, 200), rand.Next(50, 200));


                var attributes = new Rhino.DocObjects.ObjectAttributes
                {
                    ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                    Name = cell.Id.ToString()
                };
                if (randomCol) attributes.ObjectColor = color;

                GeometryBase cellGeo;

                if (!asMesh) cellGeo = cell.CreateBrepMatched();
                else cellGeo = cell.CreateCellMesh();
                if (!foamSaved && !cell.Exterior)
                {
                    cellGeo.UserDictionary.Set("Primal", primal);
                    cellGeo.UserDictionary.Set("Dual", dual);
                    foamSaved = true;
                }
                foamCells.Add(doc.Objects.Add(cellGeo, attributes));
            }
            doc.Groups.Add(Id.ToString(), foamCells);

            return foamCells;
        }





        public bool SaveToDocument(ContainerType container = ContainerType.Edge)
        {
            var getOption = new GetOption();
            getOption.SetCommandPrompt("What type of geometry container?");
            string[] listValues = new string[] { "Edges", "BrepFaces", "BrepCells", "MeshFaces", "MeshCells" };
            int opList = getOption.AddOptionList("Type", listValues, (int)container);
            getOption.SetDefaultString("_");

            getOption.Get();

            var getOp = (int)container;

            if (getOption.Result() == GetResult.Option) getOp = getOption.Option().CurrentListOptionIndex; 

            if (getOption.Result() == GetResult.Option || getOption.GotDefault())
            {
                if (getOp == 0)
                {
                    SaveAsEdges();
                }
                else if (getOp == 1)
                {
                    SaveAsFaces();
                }
                else if (getOp == 2)
                {
                    if (Cells.Count > 0)
                    {
                        SaveAsCells(true);
                    }
                    else
                    {
                        Rhino.RhinoApp.WriteLine("No cells in the PolyFrame, faces will be saved instead.");
                        SaveAsFaces();
                    }

                }
                
                else if (getOp == 3)
                {
                    SaveAsFaces(true);
                }
                else if (getOp == 4)
                {
                    if (Cells.Count > 0)
                    {
                        SaveAsCells(true, true);
                    }
                    else
                    {
                        Rhino.RhinoApp.WriteLine("No cells in the PolyFrame, faces will be saved instead.");
                        SaveAsFaces(true);
                    }
                }
            }
            
            else
            {
                return false;
            }

            return true;
        }

        public bool SaveToDocument(out bool replace, ContainerType container = ContainerType.Edge)
        {
            replace = false;
            var getPointPlace = new Rhino.Input.Custom.GetPoint();
            getPointPlace.SetCommandPrompt("Place the output. Select the type of geometry container?");
            var listValues = new List<string> { "Edges", "Faces", "Cells", "MeshFaces", "MeshCells" };
            //getPointPlace.AddOptionEnumSelectionList("saveAs", listValues, 0);
            int opList = getPointPlace.AddOptionList("Type", listValues, (int)container);
            getPointPlace.SetDefaultPoint(Centroid);

            var opRes = (int)container;
            while (true)
            {
                getPointPlace.Get();
                if (getPointPlace.Result() == GetResult.Point)
                {
                    break;
                }
                else if (getPointPlace.Result() == GetResult.Cancel)
                {
                    return false;
                }
                
                if (getPointPlace.Result() == GetResult.Option)
                    opRes = getPointPlace.Option().CurrentListOptionIndex;
                

            }

            
            var placePoint = getPointPlace.Point();

            if (getPointPlace.GotDefault())
            {
                replace = true;
            }
            else
            {
                replace = false;
                this.Offset(placePoint - Centroid);
            }

            if (opRes == 0)
            {
                SaveAsEdges();
            }
            else if (opRes == 1)
            {
                SaveAsFaces();
            }
            else if (opRes == 2)
            {
                if (Cells.Count > 0)
                {
                    SaveAsCells(true);
                }
                else
                {
                    Rhino.RhinoApp.WriteLine("No cells in the PolyFrame, faces will be saved instead.");
                    SaveAsFaces();
                }

            }
           
            else if (opRes == 3)
            {
                SaveAsFaces(true);
            }
            else if (opRes == 4)
            {
                if (Cells.Count > 0)
                {
                    SaveAsCells(true, true);
                }
                else
                {
                    Rhino.RhinoApp.WriteLine("No cells in the PolyFrame, faces will be saved instead.");
                    SaveAsFaces(true);
                }
            }


            return true;
        }
    }
}
