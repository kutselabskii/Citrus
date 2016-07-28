﻿#if WIN || MAC
using System;
using System.Collections.Generic;

namespace Lime
{
	public interface IMenu : IList<ICommand>
	{
		/// <summary>
		/// Shows the menu at the current position of the cursor.
		/// </summary>
		void Popup();

		/// <summary>
		/// Shows the menu at the given position relative to the window left top corner.
		/// </summary>
		/// <param name="position">The location in the window coordinate system to display the menu item.</param>
		/// <param name="command">The menu item to be positioned at the specified location in the window.</param>
		void Popup(IWindow window, Vector2 position, float minimumWidth, ICommand command);

		/// <summary>
		/// Refreshes the menu according to its internal state.
		/// </summary>
		void Refresh();
	}
}
#endif