﻿using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using System.Reflection;

namespace Tangerine.Core.Operations
{
	public class SetProperty : Operation
	{
		public readonly object Obj;
		public readonly object Value;
		public readonly PropertyInfo Property;

		public override bool IsChangingDocument => true;

		public static void Perform(object obj, string propertyName, object value)
		{
			Document.Current.History.Perform(new SetProperty(obj, propertyName, value));
		}

		protected SetProperty(object obj, string propertyName, object value)
		{
			Obj = obj;
			Value = value;
			Property = obj.GetType().GetProperty(propertyName);
		}

		public class Processor : OperationProcessor<SetProperty>
		{
			class Backup { public object Value; }

			protected override void InternalRedo(SetProperty op)
			{
				op.Save(new Backup { Value = op.Property.GetValue(op.Obj, null) });
				op.Property.SetValue(op.Obj, op.Value, null);
			}

			protected override void InternalUndo(SetProperty op)
			{
				var v = op.Restore<Backup>().Value;
				op.Property.SetValue(op.Obj, v, null);
			}
		}
	}

	public class SetAnimableProperty
	{
		public static void Perform(object @object, string propertyName, object value)
		{
			SetProperty.Perform(@object, propertyName, value);
			IAnimator animator;
			var animable = @object as IAnimable;
			if (animable != null && animable.Animators.TryFind(propertyName, out animator, Document.Current.AnimationId)) {
				var type = animable.GetType().GetProperty(propertyName).PropertyType;
				var key =
					animator.ReadonlyKeys.FirstOrDefault(i => i.Frame == Document.Current.AnimationFrame)?.Clone() ??
					Keyframe.CreateForType(type);
				key.Frame = Document.Current.AnimationFrame;
				key.Value = value;
				SetKeyframe.Perform(animable, propertyName, Document.Current.AnimationId, key);
			}
		}
	}

	public class RemoveKeyframe : Operation
	{
		public readonly int Frame;
		public readonly IAnimator Animator;

		public override bool IsChangingDocument => true;

		public static void Perform(IAnimator animator, int frame)
		{
			Document.Current.History.Perform(new RemoveKeyframe(animator, frame));
		}

		private RemoveKeyframe(IAnimator animator, int frame)
		{
			Frame = frame;
			Animator = animator;
		}

		public class Processor : OperationProcessor<RemoveKeyframe>
		{
			class Backup { public IKeyframe Keyframe; }

			protected override void InternalRedo(RemoveKeyframe op)
			{
				var kf = op.Animator.Keys.FirstOrDefault(k => k.Frame == op.Frame);
				op.Save(new Backup { Keyframe = kf });
				op.Animator.Keys.Remove(kf);
			}

			protected override void InternalUndo(RemoveKeyframe op)
			{
				op.Animator.Keys.AddOrdered(op.Restore<Backup>().Keyframe);
			}
		}
	}

	public class SetKeyframe : Operation
	{
		public readonly IAnimable Animable;
		public readonly string PropertyName;
		public readonly string AnimationId;
		public readonly IKeyframe Keyframe;

		public override bool IsChangingDocument => true;

		public static void Perform(IAnimable animable, string propertyName, string animationId, IKeyframe keyframe)
		{
			Document.Current.History.Perform(new SetKeyframe(animable, propertyName, animationId, keyframe));
		}

		public static void Perform(IAnimator animator, IKeyframe keyframe)
		{
			Perform(animator.Owner, animator.TargetProperty, animator.AnimationId, keyframe);
		}

		private SetKeyframe(IAnimable animable, string propertyName, string animationId, IKeyframe keyframe)
		{
			Animable = animable;
			PropertyName = propertyName;
			Keyframe = keyframe;
			AnimationId = animationId;
		}

		public class Processor : OperationProcessor<SetKeyframe>
		{
			class Backup
			{
				public IKeyframe Keyframe;
				public bool AnimatorExists;
				public IAnimator Animator;
			}

			protected override void InternalRedo(SetKeyframe op)
			{
				var animator = op.Animable.Animators[op.PropertyName, op.AnimationId];
				op.Save(new Backup {
					AnimatorExists = op.Animable.Animators.Any(a => a.TargetProperty == op.PropertyName && a.AnimationId == op.AnimationId),
					Animator = animator,
					Keyframe = animator.Keys.FirstOrDefault(k => k.Frame == op.Keyframe.Frame)
				});
				animator.Keys.AddOrdered(op.Keyframe);
			}

			protected override void InternalUndo(SetKeyframe op)
			{
				var b = op.Restore<Backup>();
				if (!b.Animator.Keys.Remove(op.Keyframe)) {
					throw new InvalidOperationException();
				}
				if (b.Keyframe != null) {
					b.Animator.Keys.AddOrdered(b.Keyframe);
				}
				if (!b.AnimatorExists) {
					op.Animable.Animators.Remove(b.Animator);
				}
			}
		}
	}

	public class InsertFolderItem : Operation
	{
		public readonly Node Container;
		public readonly FolderItemLocation Location;
		public readonly IFolderItem Item;

		public override bool IsChangingDocument => true;

		public static void Perform(IFolderItem item)
		{
			Document.Current.History.Perform(new InsertFolderItem(Document.Current.Container, GetNewFolderItemLocation(), item));
		}

		public static void Perform(Node container, FolderItemLocation location, IFolderItem item)
		{
			Document.Current.History.Perform(new InsertFolderItem(container, location, item));
		}

		internal static FolderItemLocation GetNewFolderItemLocation()
		{
			var doc = Document.Current;
			var fi = doc.SelectedFolderItems().FirstOrDefault();
			return fi != null ? doc.Container.RootFolder().Find(fi) : new FolderItemLocation(doc.Container.RootFolder(), 0);
		}

		private InsertFolderItem(Node container, FolderItemLocation location, IFolderItem item)
		{
			Container = container;
			Location = location;
			Item = item;
		}

		public class Processor : OperationProcessor<InsertFolderItem>
		{
			protected override void InternalRedo(InsertFolderItem op)
			{
				op.Location.Folder.Items.Insert(op.Location.Index, op.Item);
				op.Container.SyncFolderDescriptorsAndNodes();
			}

			protected override void InternalUndo(InsertFolderItem op)
			{
				op.Location.Folder.Items.Remove(op.Item);
				op.Container.SyncFolderDescriptorsAndNodes();
			}
		}
	}

	public static class CreateNode
	{
		public static Node Perform(Type nodeType)
		{
			return Perform(Document.Current.Container, InsertFolderItem.GetNewFolderItemLocation(), nodeType);
		}

		public static Node Perform(Node container, FolderItemLocation location, Type nodeType)
		{
			if (!nodeType.IsSubclassOf(typeof(Node))) {
				throw new InvalidOperationException();
			}
			var ctr = nodeType.GetConstructor(Type.EmptyTypes);
			Node node;
			using (Theme.Push(DefaultTheme.Instance)) {
				node = (Node)ctr.Invoke(new object[] {});
			}
			foreach (var d in Document.NodeDecorators) {
				d(node);
			}
			var attrs = ClassAttributes<TangerineClassAttribute>.Get(nodeType);
			if (attrs?.BuilderMethodName != null) {
				var builder = nodeType.GetMethod(attrs.BuilderMethodName, BindingFlags.NonPublic | BindingFlags.Instance);
				builder.Invoke(node, new object[] {});
			}
			node.Id = GenerateNodeId(container, nodeType);
			InsertFolderItem.Perform(container, location, node);
			ClearRowSelection.Perform();
			SelectNode.Perform(node);
			return node;
		}

		static string GenerateNodeId(Node container, Type nodeType)
		{
			int c = 1;
			var id = nodeType.Name;
			while (container.Nodes.Any(i => i.Id == id)) {
				id = nodeType.Name + c;
				c++;
			}
			return id;
		}
	}

	public class UnlinkFolderItem : Operation
	{
		public readonly Node Container;
		public readonly IFolderItem Item;

		public override bool IsChangingDocument => true;

		public static void Perform(Node container, IFolderItem item)
		{
			Document.Current.History.Perform(new UnlinkFolderItem(container, item));
		}

		private UnlinkFolderItem(Node container, IFolderItem item)
		{
			Container = container;
			Item = item;
		}

		public class Processor : OperationProcessor<UnlinkFolderItem>
		{
			class Backup
			{
				public Node Container;
				public FolderItemLocation Location;
			}

			protected override void InternalRedo(UnlinkFolderItem op)
			{
				var loc = op.Container.RootFolder().Find(op.Item);
				op.Save(new Backup { Container = op.Container, Location = loc });
				loc.Folder.Items.Remove(op.Item);
				op.Container.SyncFolderDescriptorsAndNodes();
			}

			protected override void InternalUndo(UnlinkFolderItem op)
			{
				var b = op.Restore<Backup>();
				b.Location.Folder.Items.Insert(b.Location.Index, op.Item);
				b.Container.SyncFolderDescriptorsAndNodes();
			}
		}
	}

	public class SetMarker : Operation
	{
		public readonly MarkerCollection Collection;
		public readonly Marker Marker;

		public override bool IsChangingDocument => true;

		public static void Perform(MarkerCollection collection, Marker marker)
		{
			Document.Current.History.Perform(new SetMarker(collection, marker));
		}

		private SetMarker(MarkerCollection collection, Marker marker)
		{
			Collection = collection;
			Marker = marker;
		}

		public class Processor : OperationProcessor<SetMarker>
		{
			class Backup { public Marker Marker; }

			protected override void InternalRedo(SetMarker op)
			{
				op.Save(new Backup { Marker = op.Collection.FirstOrDefault(i => i.Frame == op.Marker.Frame) });
				op.Collection.AddOrdered(op.Marker);
			}

			protected override void InternalUndo(SetMarker op)
			{
				op.Collection.Remove(op.Marker);
				var b = op.Restore<Backup>();
				if (b.Marker != null) {
					op.Collection.AddOrdered(b.Marker);
				}
			}
		}
	}

	public class DeleteMarker : Operation
	{
		public readonly MarkerCollection Collection;
		public readonly Marker Marker;

		public override bool IsChangingDocument => true;

		public static void Perform(MarkerCollection collection, Marker marker)
		{
			Document.Current.History.Perform(new DeleteMarker(collection, marker));
		}

		private DeleteMarker(MarkerCollection collection, Marker marker)
		{
			Collection = collection;
			Marker = marker;
		}

		public class Processor : OperationProcessor<DeleteMarker>
		{
			protected override void InternalRedo(DeleteMarker op)
			{
				op.Collection.Remove(op.Marker);
			}

			protected override void InternalUndo(DeleteMarker op)
			{
				op.Collection.AddOrdered(op.Marker);
			}
		}
	}
}
