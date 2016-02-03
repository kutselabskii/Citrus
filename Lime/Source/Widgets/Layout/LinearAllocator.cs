﻿using System;
using System.Collections.Generic;

namespace Lime
{
	static class LinearAllocator
	{
		public static float[] Allocate(float availableSize, Constraints[] constraints, bool roundSizes)
		{
			var indices = new List<int>(constraints.Length);
			for (int i = 0; i < constraints.Length; i++) {
				indices.Add(i);
			}
			var sizes = new float[constraints.Length];
			while (indices.Count != 0) {
				var totalStretch = CalcTotalStretch(constraints, indices);
				if (totalStretch < 1e-5) {
					foreach (var i in indices) {
						sizes[i] = constraints[i].MinSize;
					}
					return sizes;
				}
				float allocatedSize = 0;
				foreach (var i in indices) {
					var c = constraints[i];
					var s = availableSize * c.Stretch / totalStretch;
					s = Mathf.Clamp(s, c.MinSize, c.MaxSize);
					allocatedSize += (sizes[i] = s);
				}
				if (Math.Abs(allocatedSize - availableSize) < 0.1f) {
					// We perfectly fit the items into the designated space.
					return sizes;
				}
				var containerOverfilled = allocatedSize > availableSize;
				for (int i = indices.Count - 1; i >= 0; i--) {
					var t = constraints[i];
					if (sizes[i] == (containerOverfilled ? t.MinSize : t.MaxSize)) {
						availableSize = Math.Max(0, availableSize - sizes[i]);
						indices.RemoveAt(i);
					}
				}
			}
			if (roundSizes) {
				for (int i = 0; i < sizes.Length; i++) {
					sizes[i] = sizes[i].Round();
				}
			}
			return sizes;
		}
		
		private static float CalcTotalStretch(Constraints[] constraints, List<int> indices)
		{
			float ts = 0;
			foreach (var i in indices) {
				ts += constraints[i].Stretch;
			}
			return ts;
		}

		public struct Constraints
		{
			public float MinSize;
			public float MaxSize;
			public float Stretch;
		}
	}
}

