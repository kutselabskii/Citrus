using Lime;
using Lime.PolygonMesh;
using System;
using System.Collections.Generic;
using Tangerine.Core;
using Tangerine.UI.SceneView.Utils.PolygonMesh;
using TopologyDataBinding = System.ValueTuple<Tangerine.UI.SceneView.Utils.PolygonMesh.PrimitiveType, int>;

namespace Tangerine.UI.SceneView
{
	public class PolygonMeshManager
	{
		public enum ModificationState
		{
			None,

		// PolygonMesh.ModificationContext.Animation:
			Animation,

		// PolygonMesh.ModificationContext.Setup:
			Triangulation,
			Creation,
			Removal
		}

		private static class TypePolicies
		{
			public static class RenderingResolutions
			{
				public static readonly PrimitiveType[] RenderingOrder = new[] {
					PrimitiveType.Edge,
					PrimitiveType.Vertex
				};

				public static readonly Dictionary<ModificationState, HashSet<PrimitiveType>> HoveringAllowedTypes =
					new Dictionary<ModificationState, HashSet<PrimitiveType>> {
						{
							ModificationState.None, new HashSet<PrimitiveType> { }
						},
						{
							ModificationState.Animation, new HashSet<PrimitiveType> {
								PrimitiveType.Vertex,
								PrimitiveType.Edge,
								PrimitiveType.Face
							}
						},
						{
							ModificationState.Triangulation, new HashSet<PrimitiveType> {
								PrimitiveType.Vertex,
							}
						},
						{
							ModificationState.Creation, new HashSet<PrimitiveType> {
								PrimitiveType.Vertex,
								PrimitiveType.Edge,
								PrimitiveType.Face
							}
						},
						{
							ModificationState.Removal, new HashSet<PrimitiveType> {
								PrimitiveType.Vertex,
							}
						},
					};
			}

			public static class HitTestingResolutions
			{
				public static readonly Dictionary<ModificationState, PrimitiveType[]> OrderedAllowedTypes =
					new Dictionary<ModificationState, PrimitiveType[]> {
						{
							ModificationState.None, new PrimitiveType[] { }
						},
						{
							ModificationState.Animation, new[] {
								PrimitiveType.Vertex,
								PrimitiveType.Edge,
								PrimitiveType.Face
							}
						},
						{
							ModificationState.Triangulation, new[] {
								PrimitiveType.Vertex,
							}
						},
						{
							ModificationState.Creation, new[] {
								PrimitiveType.Vertex,
								PrimitiveType.Edge,
								PrimitiveType.Face
							}
						},
						{
							ModificationState.Removal, new[] {
								PrimitiveType.Vertex,
							}
						},
					};
			}
		}

		private PolygonMesh mesh;
		private TopologyData data;
		private ITopology topology;
		private Matrix32? renderTransformation;
		private Matrix32? hitTestTransformation;
		private TopologyDataBinding? primaryHitTestTarget;
		private List<TopologyDataBinding> adjacentHitTestTargets;

		private readonly TopologyProvider topologyProvider = new TopologyProvider();
		private readonly TopologyDataProvider dataProvider = new TopologyDataProvider();

		private static PolygonMeshManager instance;
		public static PolygonMeshManager Instance => instance ?? (instance = new PolygonMeshManager());

		public ModificationState CurrentState { get; set; }

		public Vector2? MousePosition => hitTestTransformation?.TransformVector(SceneView.Instance.MousePosition) ?? null;

		public void SetTargetMesh(PolygonMesh mesh)
		{
			this.mesh = mesh;
			topology = topologyProvider.TryGetCachedTopology(mesh);
			data = dataProvider.TryGetCachedData(topology);
			renderTransformation = mesh.CalcTransitionToSpaceOf(SceneView.Instance.Frame);
			hitTestTransformation = SceneView.Instance.Scene.CalcTransitionToSpaceOf(mesh);
		}

		#region Rendering
		public void RenderTarget()
		{
			if (mesh == null) {
				return;
			}
			if (primaryHitTestTarget.HasValue && primaryHitTestTarget.Value.Item1 == PrimitiveType.Face) {
				data[PrimitiveType.Face][primaryHitTestTarget.Value.Item2].RenderHovered(renderTransformation.Value, CurrentState, MousePosition.Value);
			}
			foreach (var type in TypePolicies.RenderingResolutions.RenderingOrder) {
				for (var i = 0; i < data[type].Count; ++i) {
					if (primaryHitTestTarget.HasValue) {
						(PrimitiveType targetType, int targetIndex) = primaryHitTestTarget.Value;
						if (targetType == type && targetIndex == i && TypePolicies.RenderingResolutions.HoveringAllowedTypes[CurrentState].Contains(type)) {
							data[type][i].RenderHovered(renderTransformation.Value, CurrentState, MousePosition.Value);
							continue;
						}
					}
					data[type][i].Render(renderTransformation.Value);
				}
			}
		}
		#endregion Rendering

		#region HitTesting
		public bool HitTestTarget()
		{
			var scale = SceneView.Instance.Scene.Scale.X;
			foreach (var type in TypePolicies.HitTestingResolutions.OrderedAllowedTypes[CurrentState]) {
				if (LocalHitTest(MousePosition.Value, scale, type)) {
					return true;
				}
			}
			return false;
		}

		private bool LocalHitTest(Vector2 position, float scale, PrimitiveType type)
		{
			var primitives = data[type];
			var targetIndex = -1;
			var minDistance = float.MaxValue;
			for (var i = 0; i < primitives.Count; ++i) {
				if (primitives[i].HitTest(position, out var distance, scale)) {
					if (distance < minDistance) {
						minDistance = distance;
						targetIndex = i;
					}
				}
			}
			var success = targetIndex != -1;
			if (success) {
				primaryHitTestTarget = (type, targetIndex);
				adjacentHitTestTargets = GetAdjacentHitTestTargets();
			}
			return success;
		}

		private List<TopologyDataBinding> GetAdjacentHitTestTargets()
		{
			if (primaryHitTestTarget == null) {
				return null;
			}
			switch (primaryHitTestTarget.Value.Item1) {
				case PrimitiveType.Vertex:
					return data.VertexAdjacency[primaryHitTestTarget.Value.Item2];
				case PrimitiveType.Edge:
					return data.EdgeAdjacency[primaryHitTestTarget.Value.Item2];
				case PrimitiveType.Face:
					return data.FaceAdjacency[primaryHitTestTarget.Value.Item2];
				default:
					throw new NotSupportedException();
			}
		}
		#endregion HitTesting

		#region ModificationTasks
		public IEnumerator<object> ProcessModification()
		{
			while (primaryHitTestTarget == null) {
				yield return null;
			}
			UI.Utils.ChangeCursorIfDefault(MouseCursor.Hand);
			if (SceneView.Instance.Input.ConsumeKeyPress(Key.Mouse0)) {
				switch (CurrentState) {
					case ModificationState.Animation:
						yield return AnimationTask();
						break;
					case ModificationState.Triangulation:
						yield return TriangulationTask();
						break;
					case ModificationState.Creation:
						yield return CreationTask();
						break;
					case ModificationState.Removal:
						yield return RemovalTask();
						break;
				}
			}
		}

		private IEnumerator<object> AnimationTask()
		{
			var cursor = WidgetContext.Current.MouseCursor;
			var currentPos = MousePosition;
			var type = primaryHitTestTarget.Value.Item1;
			var index = primaryHitTestTarget.Value.Item2;
			using (Document.Current.History.BeginTransaction()) {
				while (SceneView.Instance.Input.IsMousePressed()) {
					UI.Utils.ChangeCursorIfDefault(cursor);
					var positionDelta = MousePosition - currentPos;
					var uvDelta = positionDelta / mesh.Size;
					currentPos = MousePosition;
					// +UV
					data[type][index].Translate(positionDelta.Value);
					Core.Operations.SetAnimableProperty.Perform(
						mesh,
						$"{nameof(PolygonMesh.AnimatorVertices)}",
						new List<Lime.Vertex>(topology.Vertices),
						createAnimatorIfNeeded: true,
						createInitialKeyframeForNewAnimator: true
					);
					yield return null;
				}
				mesh.Animators.Invalidate();
				Document.Current.History.CommitTransaction();
			}
		}

		private IEnumerator<object> TriangulationTask()
		{
			var cursor = WidgetContext.Current.MouseCursor;
			var currentPos = MousePosition;
			var type = primaryHitTestTarget.Value.Item1;
			var index = primaryHitTestTarget.Value.Item2;
			using (Document.Current.History.BeginTransaction()) {
				while (SceneView.Instance.Input.IsMousePressed()) {
					UI.Utils.ChangeCursorIfDefault(cursor);
					var positionDelta = MousePosition - currentPos;
					var uvDelta = positionDelta / mesh.Size;
					currentPos = MousePosition;
					Core.Operations.PolygonMeshModification.Deform.Perform(
						mesh,
						positionDelta.Value,
						uvDelta.Value,
						((Utils.PolygonMesh.Vertex)data[type][index]).TopologicalIndex
					);
					yield return null;
				}
				Document.Current.History.CommitTransaction();
			}
			yield return null;
		}

		private IEnumerator<object> CreationTask()
		{
			var initialTarget = primaryHitTestTarget;
			var initialType = primaryHitTestTarget.Value.Item1;
			var initialIndex = primaryHitTestTarget.Value.Item2;
			using (Document.Current.History.BeginTransaction()) {
				if (initialType != PrimitiveType.Vertex) {
					CreateVertex();
				}

				using (Document.Current.History.BeginTransaction()) {
					while (SceneView.Instance.Input.IsMousePressed()) {
						Document.Current.History.RollbackTransaction();
						HitTestTarget();
						if (primaryHitTestTarget == null) {
							yield return null;
							continue;
						}
						var constrainIndex = data.Vertices.Count;
						var type = primaryHitTestTarget.Value.Item1;
						var index = primaryHitTestTarget.Value.Item2;
						switch (type) {
							case PrimitiveType.Vertex:
								constrainIndex = ((Utils.PolygonMesh.Vertex)data[type][index]).TopologicalIndex;
								break;
							case PrimitiveType.Edge:
							case PrimitiveType.Face:
								CreateVertex();
								break;
							default:
								throw new NotSupportedException();
						}
						Core.Operations.PolygonMeshModification.Constrain.Perform(
							mesh,
							initialIndex,
							constrainIndex
						);
						Window.Current.Invalidate();
						SceneView.Instance.Input.ConsumeKeyPress(Key.Mouse0);
						yield return null;
					}
				}
				Document.Current.History.CommitTransaction();
			}
			yield return null;
		}

		private void CreateVertex()
		{
			using (Document.Current.History.BeginTransaction()) {
				var cursor = WidgetContext.Current.MouseCursor;
				var currentPos = MousePosition;
				var type = primaryHitTestTarget.Value.Item1;
				var index = primaryHitTestTarget.Value.Item2;
				if (data[type][index] is Edge e) {
					var v0 = topology.Vertices[e.TopologicalIndex0];
					var v1 = topology.Vertices[e.TopologicalIndex1];
					currentPos = PolygonMeshUtils.PointProjectionToLine(currentPos.Value, v0.Pos, v1.Pos, out var isInside);
				}
				var animatedPos = currentPos.Value;
				var deformedPos = currentPos.Value;
				var animatedVertex = new Lime.Vertex() { Pos = animatedPos, UV1 = data[type][index].InterpolateUV(animatedPos), Color = mesh.Color };
				var deformedVertex = new Lime.Vertex() { Pos = deformedPos, UV1 = data[type][index].InterpolateUV(deformedPos), Color = mesh.Color };
				Core.Operations.PolygonMeshModification.Create.Perform(mesh, animatedVertex, deformedVertex);
				Window.Current.Invalidate();
				Document.Current.History.CommitTransaction();

				data = dataProvider.TryGetCachedData(topology);
				HitTestTarget();
			}
		}

		private IEnumerator<object> RemovalTask()
		{
			if (topology.Vertices.Count == 4) {
				new AlertDialog("Mesh can't contain less than 3 vertices", "Ok :(").Show();
				yield return null;
			} else {
				var type = primaryHitTestTarget.Value.Item1;
				var index = primaryHitTestTarget.Value.Item2;
				var topologicalIndex = ((Utils.PolygonMesh.Vertex)data[type][index]).TopologicalIndex;
				using (Document.Current.History.BeginTransaction()) {
					Core.Operations.PolygonMeshModification.Remove.Perform(
						mesh,
						//=====================================//
						mesh.AnimatorVertices[topologicalIndex],
						//=====================================//
						mesh.TriangulationVertices[topologicalIndex],
						topologicalIndex
					);
					Document.Current.History.CommitTransaction();
				}
				Window.Current.Invalidate();
				yield return null;
			}
		}
		#endregion ModificationTasks

		public void Invalidate()
		{
			mesh = null;
			CurrentState = default;
			primaryHitTestTarget = null;
			adjacentHitTestTargets = null;
			hitTestTransformation = Matrix32.Identity;
			renderTransformation = Matrix32.Identity;
		}
	}
}
