using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Builds Sans's metro map (extracted from his Prisma3D glTF export) at
// runtime from base64 chunk TextAssets in Resources (metro_00, metro_01,
// ...). Geometry was merged per material with world transforms baked and
// converted to Unity space by extract_metro.py; textures are the original
// embedded PNG/JPEG bytes. Blob format "FMM1":
//   magic, u32 materialCount, u32 textureCount
//   per texture: u32 len + raw image bytes
//   per material: i32 textureIndex, 4x f32 baseColor, u32 vertCount,
//                 u32 indexCount, verts (pos3+nrm3+uv2 f32), indices u32
public static class FalconMetroMap
{
    public static GameObject Build()
    {
        var parts = new List<byte[]>();
        for (int i = 0; ; i++)
        {
            TextAsset ta = Resources.Load<TextAsset>("metro_" + i.ToString("00"));
            if (ta == null) break;
            parts.Add(Convert.FromBase64String(ta.text));
        }
        if (parts.Count == 0) return null;
        int total = 0;
        foreach (var p in parts) total += p.Length;
        byte[] blob = new byte[total];
        int off = 0;
        foreach (var p in parts) { Array.Copy(p, 0, blob, off, p.Length); off += p.Length; }

        var root = new GameObject("FalconMetroMap");
        using (var br = new BinaryReader(new MemoryStream(blob)))
        {
            byte[] magic = br.ReadBytes(4);
            if (magic.Length < 4 || magic[0] != 'F' || magic[1] != 'M' || magic[2] != 'M' || magic[3] != '1')
            {
                UnityEngine.Object.Destroy(root);
                return null;
            }
            uint matCount = br.ReadUInt32();
            uint texCount = br.ReadUInt32();
            var textures = new Texture2D[texCount];
            for (int t = 0; t < texCount; t++)
            {
                uint len = br.ReadUInt32();
                byte[] img = br.ReadBytes((int)len);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Repeat;
                tex.LoadImage(img);
                textures[t] = tex;
            }
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            for (int m = 0; m < matCount; m++)
            {
                int texIdx = br.ReadInt32();
                var color = new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                uint vcount = br.ReadUInt32();
                uint icount = br.ReadUInt32();
                var positions = new Vector3[vcount];
                var normals = new Vector3[vcount];
                var uvs = new Vector2[vcount];
                for (int v = 0; v < vcount; v++)
                {
                    positions[v] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    normals[v] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    uvs[v] = new Vector2(br.ReadSingle(), br.ReadSingle());
                }
                var indices = new int[icount];
                for (int ix = 0; ix < icount; ix++) indices[ix] = (int)br.ReadUInt32();

                var mesh = new Mesh();
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = positions;
                mesh.normals = normals;
                mesh.uv = uvs;
                mesh.SetIndices(indices, MeshTopology.Triangles, 0);
                mesh.RecalculateBounds();

                var mat = new Material(shader);
                if (texIdx >= 0 && texIdx < textures.Length) mat.mainTexture = textures[texIdx];
                mat.color = color;
                // Round 68: matte, not plastic. Runtime Standard
                // materials default to 0.5 smoothness - under the
                // loader lights the whole map sheened like molded
                // plastic (build #12: "too much shine"). Rough
                // concrete: gloss near zero, metal zero.
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Glossiness", 0.18f);
                // Round 65: the ground/water sheet spans the whole
                // map flat - it catches the directional full-face
                // and blew out neon blue in build #11, where Sans's
                // render has it dark teal. Dim just that sheet,
                // identified by its bounds (150 m+ across, flat).
                var mb = mesh.bounds;
                if (mb.size.x > 150f && mb.size.z > 150f && mb.size.y < 2f)
                    mat.color = new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, color.a);

                var go = new GameObject("metro_mat_" + m);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; // round 52: loader spotlight throws real shadows (Sans: "no shadows")
                mr.receiveShadows = true;
            }
        }
        return root;
    }
}
