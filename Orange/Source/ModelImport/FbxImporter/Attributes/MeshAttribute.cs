using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Lime;
#pragma warning disable 649

namespace Orange.FbxImporter
{
	public class Bone
	{
		public Matrix44 Offset { get; set; }

		public string Name { get; set; }
	}

	public class MeshAttribute : NodeAttribute
	{
		public List<Submesh> Submeshes { get; private set; } = new List<Submesh>();

		public override FbxNodeType Type { get; } = FbxNodeType.Mesh;

		public MeshAttribute() : base(IntPtr.Zero)
		{
		}

		public static MeshAttribute FromSubmesh(IntPtr submeshPtr)
		{
			return new MeshAttribute {
				Submeshes = { new Submesh(submeshPtr) }
			};
		}

		public static MeshAttribute Combine(MeshAttribute meshAttribute1, MeshAttribute meshAttribute2)
		{
			var sm = new List<Submesh>();
			sm.AddRange(meshAttribute1.Submeshes);
			sm.AddRange(meshAttribute2.Submeshes);
			return new MeshAttribute {
				Submeshes = sm
			};
		}
	}

	public class Submesh : NodeAttribute
	{
		private const float NoWeight = -1;

		public int[] Indices { get; }

		public int MaterialIndex { get; }

		public Vector3[] Normals { get; }

		public Mesh3D.Vertex[] Vertices { get; }

		public Bone[] Bones { get; }

		public Submesh(IntPtr ptr) : base(ptr)
		{
			var native = FbxNodeGetMeshAttribute(NativePtr, true);
			if (native == IntPtr.Zero) {
				throw new FbxAtributeImportException(Type);
			}
			var mesh = native.ToStruct<MeshData>();
			var indices = mesh.Vertices.ToStruct<SizedArray>().GetData<int>();
			var controlPoints = mesh.Points.ToStruct<SizedArray>().GetData<ControlPoint>();
			var bones = mesh.Bones.ToStruct<SizedArray>().GetData<BoneData>();
			var colorsContainer = mesh.Colors.ToStruct<Element>();
			var normalsContainer = mesh.Normals.ToStruct<Element>();
			var uvContainer = mesh.UV.ToStruct<Element>();
			var colors = colorsContainer.GetData<Vec4>();
			var normals = normalsContainer.GetData<Vec3>();
			var uv = uvContainer.GetData<Vec2>();
			MaterialIndex = mesh.MaterialIndex;
			Indices = new int[indices.Length];
			Vertices = new Mesh3D.Vertex[Indices.Length];
			Normals = new Vector3[Indices.Length];
			Bones = new Bone[bones.Length];

			for (var i = 0; i < bones.Length; i++) {
				Bones[i] = new Bone {
					Name = bones[i].Name,
					Offset = bones[i].OffsetMatrix.ToStruct<Mat4x4>().ToLime()
				};
			}

			for (var i = 0; i < Indices.Length; i++) {
				var controlPoint = controlPoints[indices[i]];
				Indices[i] = i;
				Vertices[i].Pos = controlPoint.Position.ToLime();
				if (colorsContainer.Size != 0 && colorsContainer.Mode != ReferenceMode.None) {
					Vertices[i].Color = colorsContainer.Mode == ReferenceMode.ControlPoint ?
						colors[indices[i]].ToLimeColor() : colors[i].ToLimeColor();
				} else {
					Vertices[i].Color = Color4.White;
				}

				if (normalsContainer.Size != 0 && normalsContainer.Mode != ReferenceMode.None) {
					Vertices[i].Normal = normalsContainer.Mode == ReferenceMode.ControlPoint ?
						normals[indices[i]].ToLime() : normals[i].ToLime();
				}

				if (uvContainer.Size != 0 && uvContainer.Mode != ReferenceMode.None) {
					Vertices[i].UV1 = normalsContainer.Mode == ReferenceMode.ControlPoint ?
						uv[indices[i]].ToLime() : uv[i].ToLime();
					Vertices[i].UV1.Y = 1 - Vertices[i].UV1.Y;
				}

				byte index;
				float weight;

				for (var j = 0; j < ImportConfig.BoneLimit; j++) {
					if (controlPoint.WeightData.Weights[j] == NoWeight) continue;
					index = controlPoint.WeightData.Indices[j];
					weight = controlPoint.WeightData.Weights[j];
					switch (j) {
						case 0:
							Vertices[i].BlendIndices.Index0 = index;
							Vertices[i].BlendWeights.Weight0 = weight;
							break;
						case 1:
							Vertices[i].BlendIndices.Index1 = index;
							Vertices[i].BlendWeights.Weight1 = weight;
							break;
						case 2:
							Vertices[i].BlendIndices.Index2 = index;
							Vertices[i].BlendWeights.Weight2 = weight;
							break;
						case 3:
							Vertices[i].BlendIndices.Index3 = index;
							Vertices[i].BlendWeights.Weight3 = weight;
							break;
					}
				}
			}
		}

		#region Pinvokes

		[DllImport(ImportConfig.LibName, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr FbxNodeGetMeshAttribute(IntPtr node, bool limitBoneWeights);

		[DllImport(ImportConfig.LibName, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr FbxNodeGetMeshMaterial(IntPtr pMesh, int idx);

		#endregion

		#region MarshalingStructures

		private enum ReferenceMode
		{
			None,
			ControlPoint,
			PolygonVertex
		}

		[StructLayout(LayoutKind.Sequential)]
		private class MeshData
		{
			public IntPtr Vertices;

			public IntPtr Points;

			public IntPtr Colors;

			public IntPtr UV;

			public IntPtr Normals;

			public IntPtr Bones;

			[MarshalAs(UnmanagedType.I4)]
			public int MaterialIndex;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = ImportConfig.Charset)]
		private class BoneData
		{
			public string Name;

			public IntPtr OffsetMatrix;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = ImportConfig.Charset)]
		private class ControlPoint
		{
			[MarshalAs(UnmanagedType.Struct)]
			public Vec3 Position;

			[MarshalAs(UnmanagedType.Struct)]
			public WeightData WeightData;
		}

		[StructLayout(LayoutKind.Sequential)]
		private class WeightData
		{
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = ImportConfig.BoneLimit)]
			public byte[] Indices;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = ImportConfig.BoneLimit)]
			public float[] Weights;
		}

		[StructLayout(LayoutKind.Sequential)]
		private class Element : SizedArray
		{
			public ReferenceMode Mode;
		}

		#endregion
	}
#pragma warning restore 649
}
