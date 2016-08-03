using System;

namespace Lime
{
	/// <summary>
	/// Root of the widgets hierarchy.
	/// </summary>
	public class WindowWidget : Widget
	{
		private RenderChain renderChain = new RenderChain();

		public IWindow Window { get; private set; }
		public bool LayoutBasedWindowSize { get; set; }

		public WindowWidget(IWindow window)
		{
			Window = window;
			Window.Context = new CombinedContext(Window.Context, new WidgetContext(this));
			Theme.Current.Apply(this);
			window.Rendering += () => {
				Renderer.BeginFrame();
				Renderer.SetOrthogonalProjection(Vector2.Zero, Size);
				RenderAll();
				Renderer.EndFrame();
			};
			window.Updating += delta => {
				if (LayoutBasedWindowSize) {
					Size = window.ClientSize = EffectiveMinSize;
				} else {
					Size = (Vector2)window.ClientSize;
				}
				Update(delta);
			};
			window.VisibleChanging += showing => {
				if (showing && LayoutBasedWindowSize) {
					Update(0); // Update widgets in order to deduce EffectiveMinSize.
					Size = window.ClientSize = EffectiveMinSize;
					window.Center();
				}
			};
		}

		protected virtual bool ContinuousRendering() { return true; }

		public override void Update(float delta)
		{
			if (ContinuousRendering()) {
				Window.Invalidate();
			}
			WidgetContext.Current.MouseCursor = MouseCursor.Default;
			base.Update(delta);
			if (Window.Input.WasKeyPressed(Key.DismissSoftKeyboard)) {
				KeyboardFocus.Instance.SetFocus(null);
			}
			Window.Cursor = WidgetContext.Current.MouseCursor;
			LayoutManager.Instance.Layout();
			renderChain.Clear();
			AddContentsToRenderChain(renderChain);
			var hitTestArgs = new HitTestArgs(Window.Input.MousePosition);
			renderChain.HitTest(ref hitTestArgs);
			WidgetContext.Current.NodeUnderMouse = hitTestArgs.Node;
		}

		public virtual void RenderAll()
		{
			SetViewport();
			renderChain.Render();
		}

		public void SetViewport()
		{
			Renderer.Viewport = new WindowRect {
				X = 0, Y = 0,
				Width = (int)(Window.ClientSize.X * Window.PixelScale),
				Height = (int)(Window.ClientSize.Y * Window.PixelScale)
			};
		}
	}

	[Obsolete("Use WindowWidget instead")]
	public class DefaultWindowWidget : WindowWidget
	{
		public DefaultWindowWidget(Window window)
			: base(window)
		{
			Theme.Current.Apply(this, typeof(WindowWidget));
		}
	}

	public class InvalidableWindowWidget : WindowWidget
	{
		public bool RedrawMarkVisible { get; set; }

		public InvalidableWindowWidget(Window window)
			: base(window)
		{
			Theme.Current.Apply(this, typeof(WindowWidget));
		}

		protected override bool ContinuousRendering() { return false; }

		public override void RenderAll ()
		{
			base.RenderAll ();
			if (RedrawMarkVisible) {
				RenderRedrawMark();
			}
		}

		void RenderRedrawMark()
		{
			Renderer.Transform1 = Matrix32.Identity;
			Renderer.Blending = Blending.Alpha;
			Renderer.Shader = ShaderId.Diffuse;
			Renderer.DrawRect(Vector2.Zero, Vector2.One * 4, RandomColor());
		}

		private Color4 RandomColor()
		{
			return new Color4(RandomByte(), RandomByte(), RandomByte());
		}

		private byte RandomByte()
		{
			return (byte)Mathf.RandomInt(0, 255);
		}
	}
}