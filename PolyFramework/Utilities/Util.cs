using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
//using System.Xml.Serialization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Web.Script.Serialization;
using System.Collections;
using System.Net;
using System.Globalization;
using Rhino.DocObjects;

namespace PolyFramework

{
    /// <summary>
    /// Here all purpose methods will be stored 
    /// </summary>
    /// 

    public static partial class Util
    {

        // disassemble Breps into surfaces, points and lines 
        public static IList<Brep> ExtractFaces(IList<Brep> brpList)
        {

            List<Brep> faces = new List<Brep>();
            foreach (Brep brp in brpList)
            {
                for (int f = 0; f < brp.Faces.Count; f++)
                {
                    faces.Add(brp.Faces.ExtractFace(f));
                }
            }
            return faces;
        }

        // Sort breps according to type of input. (closed cells, singular faces, open breps  
        // go through all the breps (maybe test for conformity, planarity proximity etc ?)
        // for each inputed brep cell - get centroid - test centroid against stored cell centroids
        // if centroid is new - create new PFCell  - 



        // start adding the faces of the cell 
        // test face centroid against stored face centroids 
        // if centroid exists - test vertexes - if all vertexes exist - test lines => face exists - get existent face(or face mirror) and store it in the cell (and add cell to the face)
        // if centroid is new - create new face 
        // start adding edges to the face 
        // test edge midpoint against stored edge midpoints
        // if midpoint exists - test vertexes - if vertexes exist too => get existent edge (or edge mirror) and store in the face (and add face to the edge) 
        // if midpoint is new - create new edge
        // test endpoints against stored endpoints 
        // if endpoints exist add endpoints to the edge (and update edge in the endpoint)
        // Populate cell -> face -> edge with the corresponding sub objects. 



        // explode all input ... or get the faces ... 

        public static IList<Brep> Decompose(IList<Brep> inputBreps)
        {
            var faces = new List<Brep>();
            foreach (var brp in inputBreps)
            {
                if (brp.Faces.Count > 1)
                {
                    foreach (BrepFace bf in brp.Faces)
                    {
                        var faceBrep = bf.DuplicateFace(true);
                        faceBrep.UserDictionary.AddContentsFrom(brp.UserDictionary);
                        faces.Add(faceBrep);
                    }
                }
                else faces.Add(brp);

            }
            return faces;
        }

        /// <summary>
        /// Decomposes breps from a list of guids pointing at breps in the document.
        /// Does nothing if they are not breps. 
        /// </summary>
        /// <param name="inputGuids"></param>
        /// <returns></returns>
        public static IList<Brep> DecomposeG(IList<Guid> inputGuids)
        {
            var doc = Rhino.RhinoDoc.ActiveDoc;

            var inputBreps = inputGuids.Select(x => Brep.TryConvertBrep(doc.Objects.Find(x).Geometry));
            var faces = new List<Brep>();
            foreach (var brp in inputBreps)
            {
                if (brp.Faces.Count > 1)
                {
                    foreach (BrepFace bf in brp.Faces)
                    {
                        var faceBrep = bf.DuplicateFace(true);
                        faceBrep.UserDictionary.AddContentsFrom(brp.UserDictionary);
                        faces.Add(faceBrep);
                    }
                }
                else faces.Add(brp);

            }
            return faces;
        }



        /// <summary>
        /// Converts guids to breps.
        /// Also sets the order of geometry pick-up 
        /// If geometry has order in user dictionary it is sorted by that 
        /// If geometry has no order or only partial order an order values is written to the geo
        /// All compound breps are exploded and order is written in each face. 
        /// </summary>
        /// <param name="guids"></param>
        /// <returns></returns>
        public static IList<Brep> GetBrepfromGuids(IList<System.Guid> guids)
        {
            var doc = Rhino.RhinoDoc.ActiveDoc;
            //var gList = new Dictionary<System.Guid>(guids);
            List<Brep> geos = new List<Brep>();
            List<Tuple<int, RhinoObject>> geoWithOrder = new List<Tuple<int, RhinoObject>>();
            bool withOrder = false;
            bool withoutOrder = false;
            for (int i = 0; i < guids.Count; i++)
            {

                var docObjGeo = doc.Objects.Find(guids[i]);
                if (docObjGeo.Geometry.UserDictionary.ContainsKey("Order"))
                {
                    withOrder = true;
                    geoWithOrder.Add(new Tuple<int, RhinoObject>(docObjGeo.Geometry.UserDictionary.GetInteger("Order"), docObjGeo));
                }
                else
                {
                    withoutOrder = true;
                    //docObjGeo.UserDictionary.Set("Order", i);
                    geoWithOrder.Add(new Tuple<int, RhinoObject>(i, docObjGeo));
                }
                //geos.Add(Brep.TryConvertBrep(Rhino.RhinoDoc.ActiveDoc.Objects.Find(guids[i]).Geometry));

            }
            if (withOrder && !withoutOrder)
            {
                geoWithOrder.Sort((x, y) => x.Item1.CompareTo(y.Item1));
                geos = geoWithOrder.Select(x => Brep.TryConvertBrep(x.Item2.Geometry)).ToList();
            }
            else if ((!withOrder && withoutOrder) || (withOrder && withoutOrder))
            {
                int count = 0;
                for (int i = 0; i < guids.Count; i++)
                {
                    var brep = Brep.TryConvertBrep(geoWithOrder[i].Item2.Geometry);
                    var att = geoWithOrder[i].Item2.Attributes;
                    //att.ColorSource = ObjectColorSource.ColorFromObject;
                    //att.ObjectColor = System.Drawing.Color.FromArgb(150, 0, 150);
                    if (brep.Faces.Count > 1)
                    {
                        foreach (BrepFace bf in brep.Faces)
                        {
                            var faceBrep = bf.DuplicateFace(true);
                            faceBrep.UserDictionary.Set("Order", count);
                            geos.Add(faceBrep);
                            // add faces to the document 
                            // remove the connected obj from document
                            doc.Objects.AddBrep(faceBrep, att);
                            count++;
                        }
                        doc.Objects.Delete(guids[i], true);
                    }
                    else
                    {
                        geoWithOrder[i].Item2.Geometry.UserDictionary.Set("Order", count);
                        geoWithOrder[i].Item2.Attributes = att;
                        geoWithOrder[i].Item2.CommitChanges();


                        geos.Add(brep);
                        count++;
                    }

                }



            }

            return geos;
        }

        public static double BFacePlanaritiy(Brep face)
        {

            var fit = Plane.FitPlaneToPoints(face.Vertices.Select(x => x.Location), out Plane fPlane);

            double maxDist = 0.0;
            if (fit == PlaneFitResult.Success)
            {
                foreach (var fPoint in face.Vertices)
                {
                    var dist = fPlane.DistanceTo(fPoint.Location);
                    if (maxDist < dist) maxDist = dist;
                }
            }
            else
                return double.MaxValue;

            return maxDist;

        }
        /// <summary>
        /// Bakes the problem geometry to the document on a special layer 
        /// Geometry is colored -> Orange and will have the a name that is showing what the problem is!
        /// </summary>
        /// <param name="erGeo">List of error geometry</param>
        /// <param name="erName">List of error names</param>
        public static IList<Guid> BakeErrorGeo(IEnumerable<GeometryBase> erGeo, IEnumerable<string> erName)
        {
            var addedGeoIds = new List<Guid>();
            var doc = Rhino.RhinoDoc.ActiveDoc;
            var errLayer = new Rhino.DocObjects.Layer()
            {
                Name = "<<Error_Geometry>>"
            };
            if (!doc.Layers.Any(x => x.Name == errLayer.Name)) doc.Layers.Add(errLayer);
            errLayer = doc.Layers.First(x => x.Name == "<<Error_Geometry>>");

            doc.Views.RedrawEnabled = false;

            foreach (var geoName in erGeo.Zip(erName, (x, y) => new { geo = x, name = y }))
            {
                var attributesError = new Rhino.DocObjects.ObjectAttributes
                {

                    ObjectColor = System.Drawing.Color.OrangeRed,

                    ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                    Name = geoName.name,
                    LayerIndex = errLayer.LayerIndex
                };
                addedGeoIds.Add(doc.Objects.Add(geoName.geo, attributesError));
            }
            return addedGeoIds;
        }
        /// <summary>
        /// True angle between 2 3d vectors 
        /// Using direction of the cross product 
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="reference"></param>
        /// <returns></returns>
        public static double Angle(Vector3d v1, Vector3d v2, Vector3d reference)
        {
            double retVal = 0.0;
            if (Dot(Vector3d.CrossProduct(v1, v2), reference) < 0)
                retVal = 2 * Math.PI - DotAngle(v1, v2);
            else retVal = DotAngle(v1, v2);

            if (retVal == double.NaN)
            {
                throw new PolyFrameworkException("NaN returned");

            }

            return retVal;


        }

        /// <summary>
        /// Simple dot angle - calculates the smallest angle between 2 vectors 
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static double DotAngle(Vector3d a, Vector3d b)
        {
            double dot = Math.Round(Dot(a, b), 10);
            double result = Math.Acos(dot / Math.Round(a.Length, 10) * Math.Round(b.Length, 10));
            return result;
        }

        /// <summary>
        /// Dot product a.b vectors 
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static double Dot(Vector3d a, Vector3d b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        /// <summary>
        /// calculates if a point is inside a uni di, or tri
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="point"></param>
        /// <param name="normals"></param>
        /// <returns>true if point inside</returns>
        public static bool InsideHedra(Point3d origin, Point3d point, List<Vector3d> normals)
        {
            if (normals.Count > 3) throw new PolyFrameworkException("This only works with up to 3 normals.");
            bool result = true;

            foreach (var normal in normals)
            {
                if (Dot(point - origin, normal) < 0)
                {
                    result = false;
                }
            }

            return result;
        }



        // calculate color 

        public static System.Drawing.Color CreateBlue(double inValue)
        {

            if (inValue <= 1.00 && inValue >= 0.75)
            {
                int R = (Int32)ValueUnitizer(inValue, new List<double> { 1, 0.75 }, new List<double> { 7, 13 });
                int G = (Int32)ValueUnitizer(inValue, new List<double> { 1, 0.75 }, new List<double> { 37, 62 });
                int B = (Int32)ValueUnitizer(inValue, new List<double> { 1, 0.75 }, new List<double> { 61, 111 });

                /*int R = (Int32)ValueUnitizer(inValue, new List<double> { 1, 0.75 }, new List<double> { 0, 20 });
                int G = (Int32)ValueUnitizer(inValue, new List<double> { 1, 0.75 }, new List<double> { 20, 80 });
                int B = (Int32)ValueUnitizer(inValue, new List<double> { 1, 0.75 }, new List<double> { 50, 110 });*/
                return System.Drawing.Color.FromArgb(R, G, B);
            }
            if (inValue < 0.75 && inValue >= 0.5)
            {
                int R = (Int32)ValueUnitizer(inValue, new List<double> { 0.75, 0.5 }, new List<double> { 13, 14 });
                int G = (Int32)ValueUnitizer(inValue, new List<double> { 0.75, 0.5 }, new List<double> { 62, 81 });
                int B = (Int32)ValueUnitizer(inValue, new List<double> { 0.75, 0.5 }, new List<double> { 111, 150 });


                /*int R = (Int32)ValueUnitizer(inValue, new List<double> { 0.75, 0.5 }, new List<double> { 20, 70 });
                int G = (Int32)ValueUnitizer(inValue, new List<double> { 0.75, 0.5 }, new List<double> { 80, 140 });
                int B = (Int32)ValueUnitizer(inValue, new List<double> { 0.75, 0.5 }, new List<double> { 110, 176 });*/
                return System.Drawing.Color.FromArgb(R, G, B);
            }
            if (inValue < 0.5 && inValue >= 0.25)
            {
                int R = (Int32)ValueUnitizer(inValue, new List<double> { 0.5, 0.25 }, new List<double> { 14, 39 });
                int G = (Int32)ValueUnitizer(inValue, new List<double> { 0.5, 0.25 }, new List<double> { 81, 114 });
                int B = (Int32)ValueUnitizer(inValue, new List<double> { 0.5, 0.25 }, new List<double> { 150, 169 });

                /*int R = (Int32)ValueUnitizer(inValue, new List<double> { 0.5, 0.25 }, new List<double> { 70, 120 });
                int G = (Int32)ValueUnitizer(inValue, new List<double> { 0.5, 0.25 }, new List<double> { 140, 200 });
                int B = (Int32)ValueUnitizer(inValue, new List<double> { 0.5, 0.25 }, new List<double> { 176, 230 });*/
                return System.Drawing.Color.FromArgb(R, G, B);

            }
            if (inValue < 0.25 && inValue >= 0.0)
            {
                int R = (Int32)ValueUnitizer(inValue, new List<double> { 0.25, 0.0 }, new List<double> { 39, 139 });
                int G = (Int32)ValueUnitizer(inValue, new List<double> { 0.25, 0.0 }, new List<double> { 114, 164 });
                int B = (Int32)ValueUnitizer(inValue, new List<double> { 0.25, 0.0 }, new List<double> { 169, 204 });


                /*int R = (Int32)ValueUnitizer(inValue, new List<double> { 0.25, 0.0 }, new List<double> { 120, 170 });
                int G = (Int32)ValueUnitizer(inValue, new List<double> { 0.25, 0.0 }, new List<double> { 200, 255 });
                int B = (Int32)ValueUnitizer(inValue, new List<double> { 0.25, 0.0 }, new List<double> { 230, 255 });*/
                return System.Drawing.Color.FromArgb(R, G, B);
            }
            

            return System.Drawing.Color.White;

        }



        public static System.Drawing.Color AngleDeviationBlue(double inValue, double max)
        {
            if (inValue <= max) { return System.Drawing.Color.Blue; }
            if (inValue >= Math.PI / 2) { return System.Drawing.Color.Red; }
            int R = (Int32)ValueUnitizer(inValue, new List<double> { max, Math.PI / 2 }, new List<double> { 50, 200 });
            int G = 0;
            int B = (Int32)ValueUnitizer(inValue, new List<double> { max, Math.PI / 2 }, new List<double> { 200, 50 });
            return System.Drawing.Color.FromArgb(R, G, B);
        }

        public static System.Drawing.Color LengthDeviationBlue(double inValue, double specifiedMin, double specifiedMax, double originalMin, double originalMax)
        {
            int R = 255, G = 255, B = 255;
            if (inValue < originalMin || inValue > originalMax) return System.Drawing.Color.OrangeRed;
            if (inValue >= specifiedMin && inValue <= specifiedMax) { return System.Drawing.Color.Blue; }
            if (inValue >= originalMin && inValue < specifiedMin)
            {
                R = (Int32)ValueUnitizer(inValue, new List<double> { originalMin, specifiedMin }, new List<double> { 50, 200 });
                G = 0;
                B = (Int32)ValueUnitizer(inValue, new List<double> { originalMin, specifiedMin }, new List<double> { 200, 50 });
            }
            else if (inValue >= specifiedMax && inValue < originalMax)
            {
                R = (Int32)ValueUnitizer(inValue, new List<double> { specifiedMax, originalMax }, new List<double> { 50, 200 });
                G = 0;
                B = (Int32)ValueUnitizer(inValue, new List<double> { specifiedMax, originalMax }, new List<double> { 200, 50 });
            }


            return System.Drawing.Color.FromArgb(R, G, B);
        }





        public static System.Drawing.Color DeviationToColorList(double inValue, double max)
        {
            if (inValue > max) return System.Drawing.Color.Red;
            int R = (Int32)ValueUnitizer(inValue, new List<double> { 0, max }, new List<double> { 0, 200 });
            int G = 0;
            int B = (Int32)ValueUnitizer(inValue, new List<double> { 0, max }, new List<double> { 200, 0 });
            return System.Drawing.Color.FromArgb(R, G, B);
        }

        public static System.Drawing.Color DeviationToColorListGreenRed(double inValue, double max)
        {
            inValue = Math.Abs(inValue);
            if (inValue > max) return System.Drawing.Color.Red;
            int R = (Int32)ValueUnitizer(inValue, new List<double> { 0, max }, new List<double> { 0, 200 });
            int G = (Int32)ValueUnitizer(inValue, new List<double> { 0, max }, new List<double> { 200, 0 });
            int B = 0;
            return System.Drawing.Color.FromArgb(60, R, G, B);
        }


        public static System.Drawing.Color ScaleDeviationBlue(double inValue, double imposed, double maxLimit)
        {
            if (inValue <= imposed) { return System.Drawing.Color.Blue; }
            else if (inValue > maxLimit) { return System.Drawing.Color.DarkRed; }

            else if (inValue <= maxLimit && inValue > imposed)
            {
                int R = (Int32)ValueUnitizer(inValue, new List<double> { imposed, imposed + maxLimit }, new List<double> { 50, 200 });
                int G = 0;
                int B = 150;
                return System.Drawing.Color.FromArgb(R, G, B);
            }

            else return System.Drawing.Color.Pink;


        }

        public static double ValueUnitizer(double origVal, List<double> origList, List<double> unitList)
        {
            if (origList.Count == 1 || origList[0] == origList[1]) return unitList[0];
            return unitList[0] + (origVal - origList[0]) * (unitList[1] - unitList[0]) / (origList[1] - origList[0]);
        }


        public static double WeightedAverage(IEnumerable<double> values, IEnumerable<double> weights)
        {
            if (weights.Count() != values.Count()) throw new PolyFrameworkException("You should input the same number of weights and values for weighted average");
            var sumWeight = weights.Sum();
            double average = 0.0;
            foreach (var valWeight in values.Zip(weights, (value, weight) => new { value, weight }))
            {
                average += valWeight.value * (valWeight.weight / sumWeight);
            }

            return average;

        }

        /*
        public static void XmlSerializeFoamToFile(PFoam foam, string fileName)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(PFoam));
            TextWriter writer = new StreamWriter(fileName);
            serializer.Serialize(writer, foam);
        }

        public static PFoam XmlDeserializeFoamFromFile(string fileName)
        {
            // Creates an instance of the XmlSerializer class;  
            // specifies the type of object to be deserialized.  
            XmlSerializer serializer = new XmlSerializer(typeof(PFoam));
            FileStream fs = new FileStream(fileName, FileMode.Open);
            PFoam foam = new PFoam();

            foam = (PFoam)serializer.Deserialize(fs);

            return foam;

        }
        */

        /*
        public static void Serialize(PFoam obj)
        {
            using (FileStream fStream = File.Create("serialized.bin"))
            
            {
                //BinarySerializer serializer = new DataContractSerializer(obj.GetType());
                IFormatter formatter = new BinaryFormatter();
                formatter.Serialize(fStream, obj);
                //memoryStream.Position = 0;
                //return reader.ReadToEnd();
            }
        }
        
        public static string Serialize(object obj)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                DataContractSerializer serializer = new DataContractSerializer(obj.GetType());
                serializer.WriteObject(memoryStream, obj);
                string result = Encoding.UTF8.GetString(memoryStream.ToArray());
                memoryStream.Close();
                return result;
            }
        }

        public static object Deserialize(string xml, Type toType)
        {
            using (Stream stream = new MemoryStream())
            {
                byte[] data = System.Text.Encoding.UTF8.GetBytes(xml);
                stream.Write(data, 0, data.Length);
                stream.Position = 0;
                DataContractSerializer deserializer = new DataContractSerializer(toType);
                return deserializer.ReadObject(stream);
            }
        }
        */


        public static PFoam DeserializeFoamFromJsonFile()
        {
            string jsonData = "";
            var fr = new Rhino.UI.OpenFileDialog { Filter = "Json Files (*.json)|*.json" };
            if (fr.ShowOpenDialog()) jsonData = File.ReadAllText(fr.FileName);

            return DeserializeFoam(jsonData);
        }

        /// <summary>
        /// This deserializes a whole foam object from a Json string.
        /// Since it only looks at a singular object the dual cannot be deserialized here.
        /// Instead placeholder object will be used in the properties for the dual. 
        /// 
        /// </summary>
        /// <param name="jsonFoam"></param>
        /// <returns></returns>
        public static PFoam DeserializeFoam(string jsonFoam)
        {
            if (jsonFoam == "") return new PFoam();
            // get the foam dict
            JavaScriptSerializer json = new JavaScriptSerializer();
            json.MaxJsonLength = 2147483647;

            var foamPropDict = json.Deserialize<Dictionary<string, object>>(jsonFoam);
            // create the foam and add its ID
            PFoam foam = new PFoam()
            {
                Id = (string)foamPropDict["Id"]
            };


            // extract the vertices dictionary and partially extract the vertices into the foam
            // create dicts for all properties that cannot be deserialized directly into the object. 
            // also a new dict (Id, vertex)
            var vertObjDict = foamPropDict["Vertices"] as Dictionary<string, object>;
            var vertDict = new Dictionary<int, PFVertex>();
            var vertEdgesId = new Dictionary<PFVertex, List<int>>();
            var vertFacesId = new Dictionary<PFVertex, List<int>>();
            var vertCellsId = new Dictionary<PFVertex, List<int>>();
            // since there is no real object from another foam to deserialize as dual 
            // a place holder object is to be replaced as soon as the dual is available 
            // var vertDualId = new Dictionary<PFVertex, int>();
            foreach (var vertKeyVal in vertObjDict)
            {
                var vertPropDict = vertKeyVal.Value as Dictionary<string, object>;

                PFVertex vert = new PFVertex();
                vert.Id = (int)vertPropDict["Id"];
                //Dictionary<string, double> coordList = vertPropDict["Point"] as Dictionary<string, double>;
                vert.Point = PointFromDict(vertPropDict["Point"] as Dictionary<string, object>);
                vert.External = (bool)vertPropDict["External"];

                // this will make new versions compatible with older data json strings  
                if (vertPropDict.ContainsKey("SupportGuid"))
                {
                    vert.SupportGuid = Guid.Parse(vertPropDict["SupportGuid"] as string); // this will throw an exception if there is no guid 
                    vert.InfluenceCoef = Convert.ToDouble(vertPropDict["InfluenceCoef"]); // idem
                    vert.Fixed = (bool)vertPropDict["Fixed"]; // idem
                    vert.OnGeo = (bool)vertPropDict["OnGeo"];
                    if (vert.SupportGuid != Guid.Empty) // now try and find the object in the document 
                    {

                        var restrGeo = Rhino.RhinoDoc.ActiveDoc.Objects.FindId(vert.SupportGuid);
                        if (restrGeo != null)
                        {
                            foam.SetVertexConstraints(new List<PFVertex> { vert }, restrGeo, vert.InfluenceCoef, vert.OnGeo);
                        }
                    }
                    if (vert.Fixed)
                    {
                        foam.SetVertexConstraints(new List<PFVertex> { vert }, new Point(vert.Point), 100, vert.OnGeo);
                        // this needs to be update after the offset part... based on the geometry 
                    }
                }



                vertEdgesId[vert] = (vertPropDict["Edges"] as ArrayList).Cast<int>().ToList();
                vertFacesId[vert] = (vertPropDict["Faces"] as ArrayList).Cast<int>().ToList();
                vertCellsId[vert] = (vertPropDict["Cells"] as ArrayList).Cast<int>().ToList();
                vert.Dual = new PFCell((int)vertPropDict["Dual"]); // dummy cell for dual
                //vertDualId[vert] = (int)vertPropDict["Dual"];
                foam.Vertices.Add(vert);
                vertDict.Add(vert.Id, vert);
            }


            // extract the edges from the dictionary and put the vertices in the edges.
            // after also put the pairs in the edges 
            // create dictionaries for all properties not immediately fillable
            var edgeObjDict = foamPropDict["Edges"] as Dictionary<string, object>;
            var edgeDict = new Dictionary<int, PFEdge>();
            var edgePair = new Dictionary<PFEdge, int>();
            var edgeFaces = new Dictionary<PFEdge, List<int>>();
            var edgeFaceAngle = new Dictionary<PFEdge, List<double>>();
            var edgeDual = new Dictionary<PFEdge, int>();
            foreach (var edgeKeyVal in edgeObjDict)
            {

                var edgePropDict = edgeKeyVal.Value as Dictionary<string, object>;
                PFEdge edge = new PFEdge(Int32.Parse(edgeKeyVal.Key));
                var edgeVertsIds = (edgePropDict["Vertices"] as ArrayList).Cast<int>();
                foreach (int vertId in edgeVertsIds)
                {
                    edge.Vertices.Add(vertDict[vertId]); // add the vertex in the edge
                }
                //edgeDict[edge.Id] = edge;
                edge.External = (bool)edgePropDict["External"];
                edge.Deviation = Convert.ToDouble(edgePropDict["Deviation"]);

                // this will make new versions compatible with older data json strings  
                if (edgePropDict.ContainsKey("TargetLength"))
                {
                    edge.TargetLength = Convert.ToDouble(edgePropDict["TargetLength"]);
                    edge.MinLength = Convert.ToDouble(edgePropDict["MinLength"]);
                    edge.MaxLength = Convert.ToDouble(edgePropDict["MaxLength"]);
                    edge.InfluenceCoef = Convert.ToDouble(edgePropDict["InfluenceCoef"]);
                }


                // put all properties that can't be filled right now in their dictionaries
                edgePair[edge] = (int)edgePropDict["Pair"]; // for keeping score of the pair easier 
                edgeFaces[edge] = (edgePropDict["Faces"] as ArrayList).Cast<int>().ToList();
                edgeFaceAngle[edge] = (edgePropDict["FaceAngle"] as ArrayList).ToArray().Select(x => Convert.ToDouble(x)).ToList();
                edge.Dual = new PFFace((int)edgePropDict["Dual"]); // dummy face for dual just placeholder
                // add edge to foam and edgeDict;
                foam.Edges.Add(edge);
                edgeDict.Add(edge.Id, edge);
            }
            // now that all the edges have been extracted we can populate edge.Pair
            foreach (var edge in foam.Edges)
            {
                edge.Pair = edgeDict[edgePair[edge]];
            }

            // extract the faces from the dictionary 

            var faceObjDict = foamPropDict["Faces"] as Dictionary<string, object>;
            var faceDict = new Dictionary<int, PFFace>();
            var faceCell = new Dictionary<PFFace, int>();
            var facePair = new Dictionary<PFFace, int>();
            foreach (var faceKeyVal in faceObjDict)
            {
                var facePropDict = faceKeyVal.Value as Dictionary<string, object>;
                PFFace face = new PFFace(Int32.Parse(faceKeyVal.Key));
                var faceVertsIds = (facePropDict["Vertices"] as ArrayList).Cast<int>().ToList();
                foreach (var vertId in faceVertsIds)
                {
                    face.Vertices.Add(vertDict[vertId]);
                }
                var faceEdgesIds = (facePropDict["Edges"] as ArrayList).Cast<int>().ToList();
                face.Edges = faceEdgesIds.Select(x => edgeDict[x]).ToList();
                faceCell[face] = (int)facePropDict["Cell"];
                facePair[face] = (int)facePropDict["Pair"];
                face.Normal = VectorFromDict(facePropDict["Normal"] as Dictionary<string, object>);
                face.Centroid = PointFromDict(facePropDict["Centroid"] as Dictionary<string, object>);
                face.Dual = new PFEdge((int)facePropDict["Dual"]); // create a dummy edge as dual - replace later 
                face.External = (bool)facePropDict["External"];
                face.Area = Convert.ToDouble(facePropDict["Area"]);

                if (facePropDict.ContainsKey("TargetArea"))
                {
                    face.TargetArea = Convert.ToDouble(facePropDict["TargetArea"]);
                    face.InfluenceCoef = Convert.ToDouble(facePropDict["InfluenceCoef"]);
                }


                // add face to foam.faces and faceDict
                foam.Faces.Add(face);
                faceDict.Add(face.Id, face);
            }
            foreach (var face in foam.Faces)
            {
                face.Pair = faceDict[facePair[face]];
            }

            // extract cells from the dictionary 
            var cellObjDict = foamPropDict["Cells"] as Dictionary<string, object>;
            var cellDict = new Dictionary<int, PFCell>();
            foreach (var cellKeyVal in cellObjDict)
            {
                var cellPropDict = cellKeyVal.Value as Dictionary<string, object>;
                PFCell cell = new PFCell(Int32.Parse(cellKeyVal.Key));
                var cellVertsIds = (cellPropDict["Vertices"] as ArrayList).Cast<int>();
                cell.Vertices = cellVertsIds.Select(x => vertDict[x]).ToList();
                var cellEdgesIds = (cellPropDict["Edges"] as ArrayList).Cast<int>();
                cell.Edges = cellEdgesIds.Select(x => edgeDict[x]).ToList();
                var cellFacesIds = (cellPropDict["Faces"] as ArrayList).Cast<int>();
                cell.Faces = cellFacesIds.Select(x => faceDict[x]).ToList();
                cell.Centroid = PointFromDict(cellPropDict["Centroid"] as Dictionary<string, object>);
                cell.Dual = (cellPropDict["Dual"] as ArrayList).Cast<int>().Select(x => new PFVertex(x, Point3d.Unset)).ToList(); // list of dummy points to be changed later 
                cell.Exterior = (bool)cellPropDict["Exterior"];
                // add cell to foam.cells and cellDict 
                foam.Cells.Add(cell);
                cellDict.Add(cell.Id, cell);
            }

            //populate properties of vertices 
            foreach (var vert in foam.Vertices)
            {
                vert.Edges = vertEdgesId[vert].Select(x => edgeDict[x]).ToList();
                vert.Faces = vertFacesId[vert].Select(x => faceDict[x]).ToList();
                vert.Cells = vertCellsId[vert].Select(x => cellDict[x]).ToList();
            }
            //populate properties of edges 
            foreach (var edge in foam.Edges)
            {
                edge.Faces = edgeFaces[edge].Select(x => faceDict[x]).ToList();
                edge.FaceAngle = edgeFaceAngle[edge];
            }
            // populate the properties of faces
            foreach (var face in foam.Faces)
            {
                face.Cell = cellDict[faceCell[face]];
            }

            // now deserialize all other properties 
            foam.ExtVetices = (foamPropDict["ExtVertices"] as ArrayList).Cast<int>().Select(x => vertDict[x]).ToList();
            foam.ExtEdges = (foamPropDict["ExtEdges"] as ArrayList).Cast<int>().Select(x => edgeDict[x]).ToList();
            foam.ExtFaces = (foamPropDict["ExtFaces"] as ArrayList).Cast<int>().Select(x => faceDict[x]).ToList();
            foam.Centroid = PointFromDict(foamPropDict["Centroid"] as Dictionary<string, object>);
            foam.Dual = new PFoam() // this is a dummy dual just a placeholder with Id 
            {
                Id = foamPropDict["Dual"] as string
            };
            foam.MaxDeviation = Convert.ToDouble(foamPropDict["MaxDeviation"]);

            // put also the edges in the vertices - use the vertObjDict for that 


            // populate the edges and vertices in the faces 


            return foam;

        }

        /// <summary>
        /// goes through all members of both foam objects and replaces dummy objects with 
        /// real dual references from the other foam
        /// works only if the foam objects have the dummy duals set
        /// will throw exception if the two are not dual of each other
        /// </summary>
        /// <param name="primal">primal foam</param>
        /// <param name="dual">dual foam</param>
        public static void ConnectDuals(ref PFoam primal, ref PFoam dual)
        {
            if (primal.Dual.Id != dual.Id && dual.Dual.Id != primal.Id)
            {
                throw new PolyFrameworkException("The two foam objects are not dual of each other!");
            }
            // first save all sub-objects in dictionaries 
            var primalVerts = primal.Vertices.ToDictionary(vert => vert.Id, vert => vert);
            var primalEdges = primal.Edges.ToDictionary(edge => edge.Id, edge => edge);
            var primalFaces = primal.Faces.ToDictionary(face => face.Id, face => face);
            var primalCells = primal.Cells.ToDictionary(cell => cell.Id, cell => cell);

            var dualVerts = dual.Vertices.ToDictionary(vert => vert.Id, vert => vert);
            var dualEdges = dual.Edges.ToDictionary(edge => edge.Id, edge => edge);
            var dualFaces = dual.Faces.ToDictionary(face => face.Id, face => face);
            var dualCells = dual.Cells.ToDictionary(cell => cell.Id, cell => cell);

            // go through each sub-object and reference its dual from the other foam
            // test for nulls 

            // primal vertices - dual cells 
            foreach (var vert in primal.Vertices)
            {

                vert.Dual = vert.Dual.Id != -1 ? dualCells[vert.Dual.Id] : null;
            }
            // dual vertices - primal cells 
            foreach (var vert in dual.Vertices)
            {

                vert.Dual = vert.Dual.Id != -1 ? primalCells[vert.Dual.Id] : null;
            }
            // primal edges - dual faces 
            foreach (var edge in primal.Edges)
            {
                if (edge.Dual != null)
                    edge.Dual = edge.Dual.Id != 0 ? dualFaces[edge.Dual.Id] : null;
            }
            // dual edges - primal faces 
            foreach (var edge in dual.Edges)
            {
                if (edge.Dual != null)
                    edge.Dual = edge.Dual.Id != 0 ? primalFaces[edge.Dual.Id] : null;
            }
            // primal faces - dual edges 
            foreach (var face in primal.Faces)
            {
                if (face.Dual != null)
                    face.Dual = face.Dual.Id != 0 ? dualEdges[face.Dual.Id] : null;
            }
            // dual faces - primal edges 
            foreach (var face in dual.Faces)
            {
                if (face.Dual != null)
                    face.Dual = face.Dual.Id != 0 ? primalEdges[face.Dual.Id] : null;
            }
            // primal cells - dual vertices
            foreach (var cell in primal.Cells)
            {
                List<PFVertex> realDuals = new List<PFVertex>();
                foreach (var dVert in cell.Dual)
                {
                    realDuals.Add(dualVerts[dVert.Id]);
                }
                cell.Dual = new List<PFVertex>(realDuals);
            }
            // dual cells - primal vertices 
            foreach (var cell in dual.Cells)
            {
                List<PFVertex> realDuals = new List<PFVertex>();
                foreach (var dVert in cell.Dual)
                {
                    realDuals.Add(primalVerts[dVert.Id]);
                }
                cell.Dual = new List<PFVertex>(realDuals);
            }

            primal.Dual = dual;
            dual.Dual = primal;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public static Dictionary<string, double> PVToDict(Point3d point)
        {
            var pvDict = new Dictionary<string, double>()
            {
                {"X", point.X },
                {"Y", point.Y },
                {"Z", point.Z }
            };
            return pvDict;
        }
        public static Dictionary<string, double> PVToDict(Vector3d vect)
        {
            var pvDict = new Dictionary<string, double>()
            {
                {"X", vect.X },
                {"Y", vect.Y },
                {"Z", vect.Z }
            };
            return pvDict;
        }
        public static Point3d PointFromDict(Dictionary<string, object> dict)
        {
            return new Point3d(Convert.ToDouble(dict["X"]), Convert.ToDouble(dict["Y"]), Convert.ToDouble(dict["Z"]));
        }
        public static Vector3d VectorFromDict(Dictionary<string, object> dict)
        {
            return new Vector3d(Convert.ToDouble(dict["X"]), Convert.ToDouble(dict["Y"]), Convert.ToDouble(dict["Z"]));
        }

        public static DateTime GetNistTime()
        {
            try
            {
                var myHttpWebRequest = (HttpWebRequest)WebRequest.Create("http://www.google.com");
                var response = myHttpWebRequest.GetResponse();
                string todaysDates = response.Headers["date"];
                return DateTime.ParseExact(todaysDates,
                                           "ddd, dd MMM yyyy HH:mm:ss 'GMT'",
                                           CultureInfo.InvariantCulture.DateTimeFormat,
                                           DateTimeStyles.AssumeUniversal);
            }
            catch
            {
                return DateTime.Now;
            }

        }

        public static Point3d AveragePoints(IEnumerable<Point3d> points)
        {
            return new Point3d(points.Average(x => x.X), points.Average(y => y.Y), points.Average(z => z.Z));

        }


    }


}
