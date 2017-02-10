﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Yuzu;

namespace Lime
{
	public class Mesh3D : Node3D
	{
		[YuzuCompact]
		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
		public struct BlendIndices
		{
			[YuzuMember("0")]
			public byte Index0;
	
			[YuzuMember("1")]
			public byte Index1;
	
			[YuzuMember("2")]
			public byte Index2;
	
			[YuzuMember("3")]
			public byte Index3;
		}
	
		[YuzuCompact]
		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct BlendWeights
		{
			[YuzuMember("0")]
			public float Weight0;
	
			[YuzuMember("1")]
			public float Weight1;
	
			[YuzuMember("2")]
			public float Weight2;
	
			[YuzuMember("3")]
			public float Weight3;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
		public struct Vertex
		{
			[YuzuMember("0")]
			public Vector3 Pos;
			
			[YuzuMember("1")]
			public Color4 Color;
	
			[YuzuMember("2")]
			public Vector2 UV1;
	
			[YuzuMember("3")]
			public BlendIndices BlendIndices;
			
			[YuzuMember("4")]
			public BlendWeights BlendWeights;
		}
		
		private static Matrix44[] sharedBoneTransforms = new Matrix44[0];

		[YuzuMember]
		public Submesh3DCollection Submeshes { get; private set; }

		[YuzuMember]
		public BoundingSphere BoundingSphere { get; set; }

		[YuzuMember]
		public CullMode CullMode { get; set; }

		[YuzuMember]
		public Vector3 Center { get; set; }

		public bool SkipRender { get; set; }

		public Vector3 GlobalCenter
		{
			get { return GlobalTransform.TransformVector(Center); }
		}

		public Mesh3D()
		{
			Submeshes = new Submesh3DCollection(this);
			CullMode = CullMode.CullClockwise;
		}

		public override void Render()
		{
			if (SkipRender) {
				return;
			}
			Renderer.World = GlobalTransform;
			Renderer.CullMode = CullMode;
			var invWorld = GlobalTransform.CalcInverted();
			foreach (var sm in Submeshes) {
				var skin = sm.Material as IMaterialSkin;
				if (skin != null && sm.Bones.Count > 0) {
					skin.SkinEnabled = sm.Bones.Count > 0;
					if (skin.SkinEnabled) {
						if (sharedBoneTransforms.Length < sm.Bones.Count) {
							sharedBoneTransforms = new Matrix44[sm.Bones.Count];
						}
						for (var i = 0; i < sm.Bones.Count; i++) {
							sharedBoneTransforms[i] = sm.BoneBindPoses[i] * sm.Bones[i].GlobalTransform * invWorld;
						}
						skin.SetBones(sharedBoneTransforms, sm.Bones.Count);
					}
				}
				sm.Material.ColorFactor = GlobalColor;
				sm.Material.Apply();
				PlatformRenderer.DrawTriangles(sm.Mesh, 0, sm.Mesh.IndexBuffer.Data.Length);
			}
		}

		internal protected override bool PartialHitTest (ref HitTestArgs args)
		{
			float distance;
			if (!HitTestTarget) {
				return false;
			}
			if (!HitTestBoundingSphere(args.Ray, out distance) || distance > args.Distance) {
				return false;
			}
			if (!HitTestGeometry(args.Ray, out distance) || distance > args.Distance) {
				return false;
			}
			args.Node = this;
			args.Distance = distance;
			return true;
		}

		private bool HitTestBoundingSphere(Ray ray, out float distance)
		{
			distance = default(float);
			var boundingSphereInWorldSpace = BoundingSphere.Transform(GlobalTransform);
			var d = ray.Intersects(boundingSphereInWorldSpace);
			if (d != null) {
				distance = d.Value;
				return true;
			}
			return false;
		}

		private bool HitTestGeometry(Ray ray, out float distance)
		{
			var hit = false;
			distance = float.MaxValue;
			ray = ray.Transform(GlobalTransform.CalcInverted());
			foreach (var submesh in Submeshes) {
				var vertices = ((IVertexBuffer<Vertex>)submesh.Mesh.VertexBuffers[0]).Data;
				for (int i = 0; i <= vertices.Length - 3; i += 3) {
					var d = ray.IntersectsTriangle(vertices[i].Pos, vertices[i + 1].Pos, vertices[i + 2].Pos);
					if (d != null && d.Value < distance) {
						distance = d.Value;
						hit = true;
					}
				}
			}
			return hit;
		}

		public override float CalcDistanceToCamera(Camera3D camera)
		{
			return camera.View.TransformVector(GlobalCenter).Z;
		}

		public void RecalcBounds()
		{
			BoundingSphere = BoundingSphere.CreateFromPoints(GetVertexPositions());
		}

		public void RecalcCenter()
		{
			Center = Vector3.Zero;
			var n = 0;
			foreach (var vp in GetVertexPositions()) {
				Center += vp;
				n++;
			}
			Center /= n;
		}
		
		private IEnumerable<Vector3> GetVertexPositions()
		{
			foreach (var sm in Submeshes) {
				var vertices = ((IVertexBuffer<Vertex>)sm.Mesh.VertexBuffers[0]).Data;
				foreach (var v in vertices) {
					yield return v.Pos;
				}
			}
		}

		public override Node Clone()
		{
			var clone = base.Clone() as Mesh3D;
			clone.Submeshes = Submeshes.Clone(clone);
			clone.BoundingSphere = BoundingSphere;
			clone.Center = Center;
			clone.CullMode = CullMode;
			clone.SkipRender = SkipRender;
			return clone;
		}

		public override void Dispose()
		{
			Submeshes.Clear();
			base.Dispose();
		}
	}

	public class Submesh3D
	{
		[YuzuMember]
		public IMaterial Material = new CommonMaterial();

		[YuzuMember]
		public IMesh Mesh { get; set; }

		[YuzuMember]
		public List<Matrix44> BoneBindPoses { get; private set; }

		[YuzuMember]
		public List<string> BoneNames { get; private set; }
		public List<Node3D> Bones { get; private set; }

		public Mesh3D Owner { get; internal set; }

		public Submesh3D()
		{
			BoneBindPoses = new List<Matrix44>();
			BoneNames = new List<string>();
			Bones = new List<Node3D>();
		}
		
		[YuzuAfterDeserialization]
		public void AfterDeserialization()
		{
			Mesh.Attributes = new[] { new[] {
				ShaderPrograms.Attributes.Pos1,
				ShaderPrograms.Attributes.Color1,
				ShaderPrograms.Attributes.UV1,
				ShaderPrograms.Attributes.BlendIndices,
				ShaderPrograms.Attributes.BlendWeights
			} };
		}

		public void RebuildSkeleton()
		{
			RebuildSkeleton(Owner.FindModel());
		}

		internal void RebuildSkeleton(Model3D model)
		{
			Bones.Clear();
			foreach (var boneName in BoneNames) {
				Bones.Add(model.Find<Node3D>(boneName));
			}
		}

		public Submesh3D Clone()
		{
			var clone = new Submesh3D();
			clone.Mesh = Mesh.ShallowClone();
			clone.BoneNames = new List<string>(BoneNames);
			clone.BoneBindPoses = new List<Matrix44>(BoneBindPoses);
			clone.Material = Material.Clone();
			clone.Owner = null;
			return clone;
		}
	}

	public class Submesh3DCollection : IList<Submesh3D>
	{
		private Mesh3D owner;
		private List<Submesh3D> list = new List<Submesh3D>();

		public Submesh3DCollection() { }
		public Submesh3DCollection(Mesh3D owner)
		{
			this.owner = owner;
		}

		public IEnumerator<Submesh3D> GetEnumerator()
		{
			return list.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public void Add(Submesh3D item)
		{
			item.Owner = owner;
			list.Add(item);
		}

		public void Clear()
		{
			list.Clear();
		}

		public bool Contains(Submesh3D item)
		{
			return list.Contains(item);
		}

		public void CopyTo(Submesh3D[] array, int arrayIndex)
		{
			list.CopyTo(array, arrayIndex);
		}

		public bool Remove(Submesh3D item)
		{
			return list.Remove(item);
		}

		public int Count { get { return list.Count; } }
		public bool IsReadOnly { get { return false; } }
		public int IndexOf(Submesh3D item)
		{
			return list.IndexOf(item);
		}

		public void Insert(int index, Submesh3D item)
		{
			list.Insert(index, item);
		}

		public void RemoveAt(int index)
		{
			list.RemoveAt(index);
		}

		public Submesh3D this[int index]
		{
			get { return list[index]; }
			set { list[index] = value; }
		}

		public Submesh3DCollection Clone(Mesh3D owner)
		{
			var clone = new Submesh3DCollection(owner);
			for (int i = 0; i < Count; i++) {
				clone.Add(this[i].Clone());
			}
			return clone;
		}
	}
}