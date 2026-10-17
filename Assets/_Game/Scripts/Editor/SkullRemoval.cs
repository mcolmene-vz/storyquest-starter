using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Editor.Import;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// ARCHITECTURE §15.16.8 item 1 (producer ruling "Troll" item 3): Character_Troll_01 without its belt skull, as a mesh
    /// asset the troll prefab references, so the FBX's own body mesh never ships. The copy keeps every vertex, bone weight
    /// and bindpose byte for byte; only the index buffers lose the skull's triangles. The skull is found as one connected
    /// piece inside SkullBox, so a changed asset fails the generator instead of losing the wrong triangles.
    public static class SkullRemoval
    {
        public const string SourcePart = "Character_Troll_01";
        public const string DerivedPath = "Assets/_Game/Art/Generated/Meshes/Character_Troll_01_NoSkull.asset";

        const string Tag = "[SkullRemoval] ";
        const string DerivedName = "Character_Troll_01_NoSkull";
        // Vertices this close are one point, so UV and normal seams do not split a piece.
        const float WeldMetres = 0.0001f;
        // The belt and the body are far larger, so they can never qualify.
        const float MaxSkullSize = 0.25f;
        // Mesh-local metres (the mesh faces +z, so the troll's left is -x). Measured 2026-10-17 from the piece list this
        // class prints when nothing matches: one piece of 140 triangles centred at (-0.2805, 0.8221, 0.0532), 0.126 m on
        // each axis, at belt height on the left hip, where d6b-troll-front-* show the skull. The skull sinks into the hip:
        // the nearest other triangle centroid is 0.042 m from its centre on the widest axis, so the half-extents stay
        // inside that and no hip or belt triangle has its centroid in the box.
        static readonly Vector3 SkullCentre = new Vector3(-0.2805f, 0.8221f, 0.0532f);
        static readonly Vector3 SkullHalfExtents = new Vector3(0.035f, 0.035f, 0.035f);
        static readonly Bounds SkullBox = new Bounds(SkullCentre, 2f * SkullHalfExtents);

        /// Writes the derived mesh, or updates it in place so an unchanged source leaves the file byte-identical, and
        /// returns the asset. Throws, listing every candidate piece, unless exactly one piece is the skull.
        public static Mesh Derive(Mesh source)
        {
            if (source.name != SourcePart)
                throw new InvalidOperationException($"{Tag}expected the mesh {SourcePart}, got {source.name}");

            var vertices = new List<Vector3>(source.vertexCount);
            source.GetVertices(vertices);
            var submeshes = new List<int[]>();
            for (int s = 0; s < source.subMeshCount; s++)
            {
                if (source.GetTopology(s) != MeshTopology.Triangles)
                    throw new InvalidOperationException($"{Tag}{source.name} sub-mesh {s} is {source.GetTopology(s)}, not triangles");
                submeshes.Add(source.GetIndices(s, false));
            }

            var pieces = Pieces(vertices, submeshes, source, out var pieceOfTriangle);
            var candidates = pieces.Where(p => p.Bounds.size.x < MaxSkullSize && p.Bounds.size.y < MaxSkullSize && p.Bounds.size.z < MaxSkullSize).ToList();
            var matches = candidates.Where(p => SkullBox.Contains(p.Bounds.center)).ToList();
            if (matches.Count != 1)
                throw new InvalidOperationException(Tag + string.Format(CultureInfo.InvariantCulture,
                    "{0} piece(s) of {1} match SkullBox centre {2} size {3}; expected exactly 1. {4} piece(s) in all, mesh bounds centre {5} size {6}. " +
                    "Candidates under {7} m on every axis (centre, size, triangles):\n - {8}",
                    matches.Count, source.name, V(SkullBox.center), V(SkullBox.size), pieces.Count, V(source.bounds.center), V(source.bounds.size),
                    MaxSkullSize, string.Join("\n - ", candidates.OrderBy(p => p.Bounds.center.x).Select(p =>
                        $"{(SkullBox.Contains(p.Bounds.center) ? "IN BOX " : "")}piece {p.Index}: {V(p.Bounds.center)} {V(p.Bounds.size)} {p.Triangles}"))));
            var skull = matches[0];

            var derived = Object.Instantiate(source);
            derived.name = DerivedName;
            int before = 0, after = 0;
            for (int s = 0; s < submeshes.Count; s++)
            {
                var indices = submeshes[s];
                var kept = new List<int>(indices.Length);
                for (int t = 0; t < indices.Length / 3; t++)
                {
                    before++;
                    if (pieceOfTriangle[s][t] == skull.Index)
                        continue;
                    kept.Add(indices[3 * t]);
                    kept.Add(indices[3 * t + 1]);
                    kept.Add(indices[3 * t + 2]);
                    after++;
                }
                derived.SetIndices(kept.ToArray(), MeshTopology.Triangles, s, false, (int)source.GetBaseVertex(s));
            }
            derived.bounds = source.bounds;
            using (var serialized = new SerializedObject(derived))
            {
                serialized.FindProperty("m_IsReadable").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Assert(source, derived, vertices, skull, before, after);
            var asset = Save(derived);
            Object.DestroyImmediate(derived);
            HeroGenerator.LogDerived(DerivedPath, new[] { SyntyPostprocessor.BigRigModel });
            return asset;
        }

        sealed class Piece
        {
            public int Index;
            public int Triangles;
            public Bounds Bounds;
        }

        /// Triangles grouped by connectivity: vertices are joined when a triangle shares them and when their positions
        /// agree to WeldMetres. pieceOfTriangle[s][t] is the piece of sub-mesh s's triangle t.
        static List<Piece> Pieces(List<Vector3> vertices, List<int[]> submeshes, Mesh source, out List<int[]> pieceOfTriangle)
        {
            var parent = Enumerable.Range(0, vertices.Count).ToArray();
            int Find(int i)
            {
                while (parent[i] != i)
                    i = parent[i] = parent[parent[i]];
                return i;
            }
            void Union(int a, int b)
            {
                a = Find(a);
                b = Find(b);
                if (a != b)
                    parent[Math.Max(a, b)] = Math.Min(a, b);
            }

            var firstAt = new Dictionary<(long, long, long), int>();
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                var key = ((long)Math.Round(v.x / WeldMetres), (long)Math.Round(v.y / WeldMetres), (long)Math.Round(v.z / WeldMetres));
                if (firstAt.TryGetValue(key, out int first))
                    Union(first, i);
                else
                    firstAt[key] = i;
            }
            for (int s = 0; s < submeshes.Count; s++)
            {
                int baseVertex = (int)source.GetBaseVertex(s);
                var indices = submeshes[s];
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Union(baseVertex + indices[i], baseVertex + indices[i + 1]);
                    Union(baseVertex + indices[i], baseVertex + indices[i + 2]);
                }
            }

            var byRoot = new Dictionary<int, Piece>();
            var pieces = new List<Piece>();
            pieceOfTriangle = new List<int[]>();
            for (int s = 0; s < submeshes.Count; s++)
            {
                int baseVertex = (int)source.GetBaseVertex(s);
                var indices = submeshes[s];
                var owner = new int[indices.Length / 3];
                for (int t = 0; t < owner.Length; t++)
                {
                    int root = Find(baseVertex + indices[3 * t]);
                    if (!byRoot.TryGetValue(root, out var piece))
                    {
                        piece = new Piece { Index = pieces.Count, Bounds = new Bounds(vertices[baseVertex + indices[3 * t]], Vector3.zero) };
                        byRoot[root] = piece;
                        pieces.Add(piece);
                    }
                    piece.Triangles++;
                    for (int k = 0; k < 3; k++)
                        piece.Bounds.Encapsulate(vertices[baseVertex + indices[3 * t + k]]);
                    owner[t] = piece.Index;
                }
                pieceOfTriangle.Add(owner);
            }
            return pieces;
        }

        /// §15.16.8's assertions, logged so the sign-off regeneration log carries them.
        static void Assert(Mesh source, Mesh derived, List<Vector3> vertices, Piece skull, int before, int after)
        {
            var problems = new List<string>();
            if (before - after != skull.Triangles)
                problems.Add($"triangles {before} -> {after} removed {before - after}, but the skull piece has {skull.Triangles}");

            int inBox = 0;
            float nearest = float.MaxValue;
            for (int s = 0; s < derived.subMeshCount; s++)
            {
                int baseVertex = (int)derived.GetBaseVertex(s);
                var indices = derived.GetIndices(s, false);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var centroid = (vertices[baseVertex + indices[i]] + vertices[baseVertex + indices[i + 1]] + vertices[baseVertex + indices[i + 2]]) / 3f;
                    if (SkullBox.Contains(centroid))
                        inBox++;
                    var d = centroid - SkullBox.center;
                    nearest = Mathf.Min(nearest, Mathf.Max(Mathf.Abs(d.x), Mathf.Max(Mathf.Abs(d.y), Mathf.Abs(d.z))));
                }
            }
            if (inBox > 0)
                problems.Add($"{inBox} remaining triangle(s) have their centroid inside SkullBox");

            if (derived.vertexCount != source.vertexCount)
                problems.Add($"vertex count {derived.vertexCount}, source {source.vertexCount}");
            bool streamsEqual = derived.vertexBufferCount == source.vertexBufferCount;
            using (var a = Mesh.AcquireReadOnlyMeshData(source))
            using (var b = Mesh.AcquireReadOnlyMeshData(derived))
            {
                for (int stream = 0; streamsEqual && stream < source.vertexBufferCount; stream++)
                    streamsEqual = a[0].GetVertexData<byte>(stream).SequenceEqual(b[0].GetVertexData<byte>(stream));
            }
            if (!streamsEqual)
                problems.Add("the vertex streams differ from the source's");
            var weights = source.GetAllBoneWeights();
            var derivedWeights = derived.GetAllBoneWeights();
            bool weightsEqual = weights.Length == derivedWeights.Length && weights.SequenceEqual(derivedWeights)
                && source.GetBonesPerVertex().SequenceEqual(derived.GetBonesPerVertex());
            if (!weightsEqual)
                problems.Add($"bone weights differ ({derivedWeights.Length} against the source's {weights.Length})");
            bool bindposesEqual = source.bindposes.SequenceEqual(derived.bindposes);
            if (!bindposesEqual)
                problems.Add($"bindposes differ ({derived.bindposes.Length} against the source's {source.bindposes.Length})");
            if (derived.bounds != source.bounds)
                problems.Add("bounds were recalculated");

            Debug.Log(Tag + string.Format(CultureInfo.InvariantCulture,
                "{0}: skull = piece {1}, centre {2} size {3} (mesh-local m), {4} triangles; triangles {5} -> {6} (removed {7}); " +
                "remaining centroids in SkullBox {8} (half-extents {19}; the nearest is {20:F4} m from its centre on the widest axis); vertices {9} = {10}, vertex streams byte-identical {11}, bone weights {12} = {13} equal {14}, " +
                "bindposes {15} = {16} equal {17}, bounds copied {18}",
                source.name, skull.Index, V(skull.Bounds.center), V(skull.Bounds.size), skull.Triangles, before, after, before - after,
                inBox, derived.vertexCount, source.vertexCount, streamsEqual, derivedWeights.Length, weights.Length, weightsEqual,
                derived.bindposes.Length, source.bindposes.Length, bindposesEqual, derived.bounds == source.bounds, V(SkullBox.extents), nearest));
            if (problems.Count > 0)
                throw new InvalidOperationException(Tag + string.Join("; ", problems));
        }

        /// Loaded and overwritten in place, as every generated asset is, so the GUID stays and an unchanged run writes the
        /// same bytes.
        static Mesh Save(Mesh derived)
        {
            var before = CombatantGenerator.ReadBytes(DerivedPath);
            CombatantGenerator.EnsureFolder(System.IO.Path.GetDirectoryName(DerivedPath)!.Replace('\\', '/'));
            var asset = AssetDatabase.LoadAssetAtPath<Mesh>(DerivedPath);
            if (asset == null)
            {
                asset = Object.Instantiate(derived);
                asset.name = DerivedName;
                AssetDatabase.CreateAsset(asset, DerivedPath);
            }
            else
            {
                EditorUtility.CopySerialized(derived, asset);
            }
            CombatantGenerator.Save(asset, DerivedPath, before);
            if (asset.isReadable)
                throw new InvalidOperationException($"{Tag}{DerivedPath} was saved readable");
            return asset;
        }

        static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:F4}, {1:F4}, {2:F4})", v.x, v.y, v.z);
    }
}
