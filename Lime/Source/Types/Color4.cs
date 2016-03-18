﻿using System;
using ProtoBuf;
using System.Runtime.InteropServices;

namespace Lime
{
	/// <summary>
	/// Representation of 4-byte color (RGBA).
	/// </summary>
	[ProtoContract]
	[System.Diagnostics.DebuggerStepThrough]
	[StructLayout(LayoutKind.Explicit)]
	public struct Color4 : IEquatable<Color4>
	{
		[FieldOffset(0)]
		public byte R;

		[FieldOffset(1)]
		public byte G;

		[FieldOffset(2)]
		public byte B;

		[FieldOffset(3)]
		public byte A;

		[ProtoMember(1)]
		[FieldOffset(0)]
		public uint ABGR;

		public static readonly Color4 Red = new Color4(255, 0, 0, 255);
		public static readonly Color4 Green = new Color4(0, 255, 0, 255);
		public static readonly Color4 Blue = new Color4(0, 0, 255, 255);
		public static readonly Color4 White = new Color4(255, 255, 255, 255);
		public static readonly Color4 Black = new Color4(0, 0, 0, 255);
		public static readonly Color4 Gray = new Color4(128, 128, 128, 255);
		public static readonly Color4 DarkGray = new Color4(64, 64, 64, 255);
		public static readonly Color4 Yellow = new Color4(255, 255, 0, 255);
		public static readonly Color4 Orange = new Color4(255, 128, 0, 255);
		public static readonly Color4 Transparent = new Color4(255, 255, 255, 0);

		public Color4(uint abgr)
		{
			R = G = B = A = 0;
			ABGR = abgr;
		}

		public Color4(byte r, byte g, byte b, byte a = 255)
		{
			ABGR = 0;
			R = r;
			G = g;
			B = b;
			A = a;
		}

		public static byte GetAlphaComponent(int color)
		{
			return (byte)((uint)color >> 24);
		}

		public static byte GetRedComponent(int color)
		{
			return (byte)((color >> 16) & 0xFF);
		}

		public static byte GetGreenComponent(int color)
		{
			return (byte)((color >> 8) & 0xFF);
		}

		public static byte GetBlueComponent(int color)
		{
			return (byte)(color & 0xFF);
		}

		// Unity mono compiler doesn't allow another constructor because of ambiguity
		public static Color4 FromFloats(float r, float g, float b, float a = 1)
		{
			return new Color4 {
				R = (byte)(Mathf.Clamp(r, 0, 1) * 255),
				G = (byte)(Mathf.Clamp(g, 0, 1) * 255),
				B = (byte)(Mathf.Clamp(b, 0, 1) * 255),
				A = (byte)(Mathf.Clamp(a, 0, 1) * 255)
			};
		}

		/// <summary>
		/// Componentwise multiplication of two colors.
		/// </summary>
		public static Color4 operator *(Color4 lhs, Color4 rhs)
		{
			if (lhs.ABGR == 0xFFFFFFFF)
				return rhs;
			if (rhs.ABGR == 0xFFFFFFFF)
				return lhs;
			return new Color4
			{
				R = (byte) ((rhs.R * ((lhs.R << 8) + lhs.R) + 255) >> 16),
				G = (byte) ((rhs.G * ((lhs.G << 8) + lhs.G) + 255) >> 16),
				B = (byte) ((rhs.B * ((lhs.B << 8) + lhs.B) + 255) >> 16),
				A = (byte) ((rhs.A * ((lhs.A << 8) + lhs.A) + 255) >> 16)
			};
		}

		/// <summary>
		/// Multiplies every component of color with its alpha.
		/// </summary>
		public static Color4 PremulAlpha(Color4 color)
		{
			int a = color.A;
			if (a >= 255) {
				return color;
			}
			a = (a << 8) + a;
			color.R = (byte)((color.R * a + 255) >> 16);
			color.G = (byte)((color.G * a + 255) >> 16);
			color.B = (byte)((color.B * a + 255) >> 16);
			return color;
		}

		/// <summary>
		/// Creates a new <see cref="Color4"/> that contains
		/// linear interpolation of the specified colors.
		/// </summary>
		/// <param name="amount">Weighting value(between 0.0 and 1.0).</param>
		/// <param name="value1">The first color.</param>
		/// <param name="value2">The second color.</param>
		public static Color4 Lerp(float amount, Color4 value1, Color4 value2)
		{
			if (value1.ABGR == value2.ABGR) {
				return value1;
			}
			int x, z;
			x = (int)(amount * 255);
			x = (x < 0) ? 0 :((x > 255) ? 255 : x);
			var r = new Color4();
			z = (value1.R << 8) - value1.R + (value2.R - value1.R) * x;
			r.R = (byte)(((z << 8) + z + 255) >> 16);
			z = (value1.G << 8) - value1.G + (value2.G - value1.G) * x;
			r.G = (byte)(((z << 8) + z + 255) >> 16);
			z = (value1.B << 8) - value1.B + (value2.B - value1.B) * x;
			r.B = (byte)(((z << 8) + z + 255) >> 16);
			z = (value1.A << 8) - value1.A + (value2.A - value1.A) * x;
			r.A = (byte)(((z << 8) + z + 255) >> 16);
			return r;
		}

		public bool Equals(Color4 other)
		{
			return ABGR == other.ABGR;
		}

		/// <summary>
		/// Returns the <see cref="string"/> representation of this <see cref="Color4"/>
		/// in the format: "#R.G.B.A", where R, G, B, A are represented by hexademical numbers.
		/// </summary>
		public override string ToString()
		{
			return string.Format("#{0:X2}.{1:X2}.{2:X2}.{3:X2}", R, G, B, A);
		}
	}
}