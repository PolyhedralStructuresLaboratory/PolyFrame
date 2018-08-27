using System.Linq;
using Rhino.Geometry;
using System.Drawing;
using System;
using Rhino.Display;
using System.Collections.Generic;

namespace PolyFramework
{
    class DrawFalsePFMeshConduit : DisplayConduit
    {
        IList<Mesh> pfMesh = new List<Mesh>();
        BoundingBox bbox;

        public DrawFalsePFMeshConduit(IList<Mesh> meshes)
        {
            pfMesh = new List<Mesh>(meshes) ?? throw new System.ArgumentNullException(nameof(meshes));
            var allMesh = new Mesh();
            for (int i = 0; i < pfMesh.Count; i++)
            {
              
                allMesh.Append(pfMesh[i]);
            }
            bbox = allMesh.GetBoundingBox(false);
        }

        public void UpdateMeshes(IList<Mesh> newMeshes)
        {
            pfMesh = new List<Mesh>(newMeshes);
            var allMesh = new Mesh();

            for (int i = 0; i < newMeshes.Count; i++)
            {
                allMesh.Append(pfMesh[i]);
            }
            bbox = allMesh.GetBoundingBox(false);

        }

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            base.CalculateBoundingBox(e);
            e.IncludeBoundingBox(bbox);
        }

        protected override void PreDrawObjects(DrawEventArgs e)
        {
            // this could be slow with linq
            base.PreDrawObjects(e);
            foreach (var mesh in pfMesh)
                e.Display.DrawMeshFalseColors(mesh);

        }



    }

    class DrawPFMeshConduit : DisplayConduit
    {
        IList<Mesh> pfMesh = new List<Mesh>();

        IList<Rhino.Display.DisplayMaterial> material = new List<Rhino.Display.DisplayMaterial>();
        BoundingBox bbox;
        private static Random rand = new Random();

        public DrawPFMeshConduit(Mesh mesh, Color color, double transparency)
        {
            pfMesh = new List<Mesh>() { mesh } ?? throw new System.ArgumentNullException(nameof(mesh));
            material = new List<Rhino.Display.DisplayMaterial> { new DisplayMaterial(color, transparency) };
            var allMesh = new Mesh();
            foreach (var msh in pfMesh)
            {
                allMesh.Append(msh);
            }
            bbox = allMesh.GetBoundingBox(false);
        }

        public DrawPFMeshConduit(IList<Mesh> meshes, double transparency)
        {
            pfMesh = new List<Mesh>(meshes) ?? throw new System.ArgumentNullException(nameof(meshes));


            var allMesh = new Mesh();
            for (int i = 0; i < pfMesh.Count; i++)
            {
                Color color = Color.FromArgb(rand.Next(50, 200), rand.Next(50, 200), rand.Next(50, 200));
                DisplayMaterial mat = new DisplayMaterial(color, 0.5);
                material.Add(mat);
                allMesh.Append(pfMesh[i]);
            }
            bbox = allMesh.GetBoundingBox(false);
        }

        public DrawPFMeshConduit(IList<Mesh> meshes, Color color, double transparency)
        {
            pfMesh = new List<Mesh>(meshes) ?? throw new System.ArgumentNullException(nameof(meshes));


            var allMesh = new Mesh();
            for (int i = 0; i < pfMesh.Count; i++)
            {
                DisplayMaterial mat = new DisplayMaterial(color, 0.8);
                material.Add(mat);
                allMesh.Append(pfMesh[i]);
            }
            bbox = allMesh.GetBoundingBox(false);
        }

        public DrawPFMeshConduit(IList<Mesh> meshes, IList<Color> colours, double transparency)
        {
            pfMesh = new List<Mesh>(meshes) ?? throw new System.ArgumentNullException(nameof(meshes));


            var allMesh = new Mesh();
            for (int i = 0; i < pfMesh.Count; i++)
            {
                DisplayMaterial mat = new DisplayMaterial(colours[i], 0.8);
                material.Add(mat);
                allMesh.Append(pfMesh[i]);
            }
            bbox = allMesh.GetBoundingBox(false);
        }


        public DrawPFMeshConduit(IList<Mesh> meshes)
        {
            pfMesh = new List<Mesh>(meshes) ?? throw new System.ArgumentNullException(nameof(meshes));


            var allMesh = new Mesh();
            for (int i = 0; i < pfMesh.Count; i++)
            {

                allMesh.Append(pfMesh[i]);
            }
            bbox = allMesh.GetBoundingBox(false);
        }




        public void UpdateMeshes(IList<Mesh> newMeshes)
        {
            pfMesh = new List<Mesh>(newMeshes);
            var allMesh = new Mesh();
           
            for (int i = 0; i < newMeshes.Count; i++)
            {
                allMesh.Append(pfMesh[i]);
            }
            bbox = allMesh.GetBoundingBox(false);

        }

        public void UpdateColors(IList<Color> newColors)
        {
            for (int i = 0; i < material.Count; i++)
            {
                material[i].Diffuse = newColors[i];
            }
        }

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            base.CalculateBoundingBox(e);
            e.IncludeBoundingBox(bbox);
        }

        protected override void PreDrawObjects(DrawEventArgs e)
        {
            // this could be slow with linq
            base.PreDrawObjects(e);
            foreach (var mesh_mat in pfMesh.Zip(material, (msh, mat) => new { msh, mat }))
                e.Display.DrawMeshShaded(mesh_mat.msh, mesh_mat.mat);

        }

    }
}
