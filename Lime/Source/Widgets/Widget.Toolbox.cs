﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lime
{
	public partial class Widget : Node
	{
		public void RenderToTexture(ITexture texture)
		{
			if (Width > 0 && Height > 0) {
				texture.SetAsRenderTarget();
				Viewport vp = Renderer.Viewport;
				Renderer.Viewport = new Viewport { X = 0, Y = 0, Width = texture.ImageSize.Width, Height = texture.ImageSize.Height };
				Renderer.PushProjectionMatrix();
				Renderer.SetOrthogonalProjection(0, Height, Width, 0);

				var savedTransform2 = Renderer.Transform2;
				Renderer.Transform2 = globalMatrix.CalcInversed();
				var chain = new RenderChain();
				AddToRenderChain(chain);
				chain.RenderAndClear();
				Renderer.Transform2 = savedTransform2;

				texture.RestoreRenderTarget();
				Renderer.Viewport = vp;
				Renderer.PopProjectionMatrix();
			}
		}

		public static bool AreWidgetsIntersected(Widget a, Widget b)
		{
			Vector2[] rect = new Vector2[4] {
				new Vector2(0, 0), new Vector2(1, 0),
				new Vector2(1, 1), new Vector2(0, 1)
			};
			var sizes = new Vector2[2] { a.Size, b.Size };
			var matrices = new Matrix32[2] { a.GlobalMatrix, b.GlobalMatrix };
			var det = new float[2] { matrices[0].CalcDeterminant(), matrices[1].CalcDeterminant() };
			if (det[0] == 0 || det[1] == 0) {
				return false;
			}
			for (int k = 0; k < 2; k++)
				for (int i = 0; i < 4; i++) {
					var ptA = matrices[k] * (rect[i] * sizes[k]);
					var ptB = matrices[k] * (rect[(i + 1) % 4] * sizes[k]);
					var isOutside = true;
					for (int j = 0; j < 4; j++) {
						var pt = matrices[1 - k] * (rect[j] * sizes[1 - k]);
						isOutside = isOutside && CalcPointHalfPlane(pt, ptA, ptB) * det[k] < 0;
					}
					if (isOutside)
						return false;
				}
			return true;
		}

		private static int CalcPointHalfPlane(Vector2 point, Vector2 lineA, Vector2 lineB)
		{
			float s = (lineB.X - lineA.X) * (point.Y - lineA.Y) - (lineB.Y - lineA.Y) * (point.X - lineA.X);
			return Math.Sign(s);
		}

		public void CenterOnParent()
		{
			if (Parent == null) {
				throw new Lime.Exception("Parent must not be null");
			}
			Position = Parent.Widget.Size * 0.5f;
			Pivot = Vector2.Half;
		}

		public Matrix32 CalcTransitionMatrixToSpaceOf(Widget container)
		{
			RecalcGlobalMatrixAndColor();
			container.RecalcGlobalMatrixAndColor();
			Matrix32 mtx1 = container.GlobalMatrix.CalcInversed();
			Matrix32 mtx2 = GlobalMatrix;
			return mtx2 * mtx1;
		}

		public Vector2[] CalcHullInSpaceOf(Widget container)
		{
			Vector2[] vertices = new Vector2[4];
			var basis = CalcBasisInSpaceOf(container);
			vertices[0] = basis.Position - basis.U * Size.X * Pivot.X - basis.V * Size.Y * Pivot.Y;
			vertices[1] = vertices[0] + basis.U * Size.X;
			vertices[2] = vertices[0] + basis.U * Size.X + basis.V * Size.Y;
			vertices[3] = vertices[0] + basis.V * Size.Y;
			return vertices;
		}

		public struct Basis
		{
			public Vector2 Position;
			public Vector2 Scale;
			public float Rotation;
			public Vector2 U;
			public Vector2 V;
		}

		public Basis CalcBasisInSpaceOf(Widget container)
		{
			container.RecalcGlobalMatrixAndColor();
			RecalcGlobalMatrixAndColor();
			var v1 = new Vector2(1, 0);
			var v2 = new Vector2(0, 1);
			var v3 = new Vector2(0, 0);
			Matrix32 mtx = CalcTransitionMatrixToSpaceOf(container);
			v1 = mtx.TransformVector(v1);
			v2 = mtx.TransformVector(v2);
			v3 = mtx.TransformVector(v3);
			v1 = v1 - v3;
			v2 = v2 - v3;
			Basis basis;
			basis.Position = mtx.TransformVector(Pivot * Size);
			basis.Scale = new Vector2(v1.Length, v2.Length);
			basis.Rotation = v1.Atan2Degrees;
			basis.U = v1;
			basis.V = v2;
			return basis;
		}
	}
}
