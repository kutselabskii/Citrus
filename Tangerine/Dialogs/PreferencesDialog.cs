using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Lime;
using Tangerine.UI;
using Tangerine.Core;
using System;
using System.IO;
using Tangerine.Dialogs;

namespace Tangerine
{
	public class PreferencesDialog
	{
		readonly Window window;
		readonly WindowWidget rootWidget;
		readonly Button okButton;
		readonly Button cancelButton;
		readonly Button resetButton;
		readonly ColorThemeEnum theme;
		readonly Frame Frame;
		readonly TabbedWidget Content;
		private static readonly Thickness contentPadding = new Thickness {
			Left = 10,
			Top = 10,
			Bottom = 10,
			Right = 15,
		};

		private bool saved = false;
		private HotkeyProfile currentProfile;

		public PreferencesDialog()
		{
			theme = AppUserPreferences.Instance.Theme;
			window = new Window(new WindowOptions {
				ClientSize = new Vector2(800, 600),
				FixedSize = false,
				Title = "Preferences",
				MinimumDecoratedSize = new Vector2(400, 300)
			});
			Frame = new ThemedFrame {
				Padding = new Thickness(8),
				LayoutCell = new LayoutCell { StretchY = float.MaxValue },
				Layout = new StackLayout(),
			};
			Content = new TabbedWidget();
			Content.AddTab("General", CreateGeneralPane(), true);
			Content.AddTab("Appearance", CreateColorsPane());
			Content.AddTab("Keyboard shortcuts", CreateKeyboardPane());

			rootWidget = new ThemedInvalidableWindowWidget(window) {
				Padding = new Thickness(8),
				Layout = new VBoxLayout(),
				Nodes = {
					Content,
					new Widget {
						Layout = new HBoxLayout { Spacing = 8 },
						LayoutCell = new LayoutCell(Alignment.LeftCenter),
						Padding = new Thickness { Top = 5 },
						Nodes = {
							(resetButton = new ThemedButton { Text = "Reset To Defaults", MinMaxWidth = 150f }),
							new Widget { MinMaxHeight = 0 },
							(okButton = new ThemedButton { Text = "Ok" }),
							(cancelButton = new ThemedButton { Text = "Cancel" }),
						}
					}
				}
			};
			HotkeyRegistry.CurrentProfile.Save();
			okButton.Clicked += () => {
				saved = true;
				window.Close();
				if (theme != AppUserPreferences.Instance.Theme) {
					AlertDialog.Show("The color theme change will take effect next time you run Tangerine.");
				}
				Core.UserPreferences.Instance.Save();
			};
			resetButton.Clicked += () => {
				if (new AlertDialog($"Are you sure you want to reset to defaults?", "Yes", "Cancel").Show() == 0) {
					ResetToDefaults();
				}
			};
			cancelButton.Clicked += () => {
				window.Close();
				Core.UserPreferences.Instance.Load();
			};
			rootWidget.FocusScope = new KeyboardFocusScope(rootWidget);
			rootWidget.LateTasks.AddLoop(() => {
				if (rootWidget.Input.ConsumeKeyPress(Key.Escape)) {
					window.Close();
					Core.UserPreferences.Instance.Load();
				}
			});
			okButton.SetFocus();

			window.Closed += () => {
				if (saved) {
					foreach (var profile in HotkeyRegistry.Profiles) {
						profile.Save();
					}
					HotkeyRegistry.CurrentProfile = currentProfile;
				} else {
					foreach (var profile in HotkeyRegistry.Profiles) {
						profile.Load();
					}
					HotkeyRegistry.CurrentProfile = HotkeyRegistry.CurrentProfile;
				}
			};

			foreach (var command in HotkeyRegistry.CurrentProfile.Commands) {
				command.Command.Shortcut = new Shortcut(Key.Unknown);
			}
		}

		private void ResetToDefaults()
		{
			AppUserPreferences.Instance.ResetToDefaults();
			UI.SceneView.SceneUserPreferences.Instance.ResetToDefaults();
			UI.Timeline.TimelineUserPreferences.Instance.ResetToDefaults();
			Core.CoreUserPreferences.Instance.ResetToDefaults();
			HotkeyRegistry.ResetToDefaults();
		}

		private Widget CreateColorsPane()
		{
			var pane = new ThemedScrollView();
			pane.Content.Layout = new VBoxLayout { Spacing = 4 };
			pane.Content.Padding = contentPadding;
			new EnumPropertyEditor<ColorThemeEnum>(
			new PropertyEditorParams(pane.Content, AppUserPreferences.Instance, nameof(Tangerine.AppUserPreferences.Theme), "User interface theme"));
			var tmp = new BooleanPropertyEditor(
				new PropertyEditorParams(pane.Content, UI.SceneView.SceneUserPreferences.Instance, nameof(UI.SceneView.SceneUserPreferences.EnableChessBackground), "Chess background"));
			tmp.ContainerWidget.AddChangeWatcher(
				() => UI.SceneView.SceneUserPreferences.Instance.EnableChessBackground, (v) => Application.InvalidateWindows());
			tmp = new BooleanPropertyEditor(
				new PropertyEditorParams(pane.Content, UI.SceneView.SceneUserPreferences.Instance, nameof(UI.SceneView.SceneUserPreferences.DrawFrameBorder), "Draw frame border"));
			tmp.ContainerWidget.AddChangeWatcher(
				() => UI.SceneView.SceneUserPreferences.Instance.DrawFrameBorder, (v) => Application.InvalidateWindows());
			CreateColorPropertyEditor(
				nameof(UI.SceneView.SceneUserPreferences.BackgroundColorA),
				"Background color A",
				Core.UserPreferences.Instance.Get<UI.SceneView.SceneUserPreferences>(),
				() => ColorTheme.Current.SceneView.BackgroundColorA,
				pane);
			CreateColorPropertyEditor(
				nameof(UI.SceneView.SceneUserPreferences.BackgroundColorB),
				"Background color B",
				Core.UserPreferences.Instance.Get<UI.SceneView.SceneUserPreferences>(),
				() => ColorTheme.Current.SceneView.BackgroundColorB,
				pane);
			CreateColorPropertyEditor(
				nameof(UI.SceneView.SceneUserPreferences.RootWidgetOverlayColor),
				"Root overlay color",
				Core.UserPreferences.Instance.Get<UI.SceneView.SceneUserPreferences>(),
				() => ColorTheme.Current.SceneView.RootWidgetOverlayColor,
				pane);
			CreateColorPropertyEditor(
				nameof(UI.SceneView.SceneUserPreferences.AnimationPreviewBackground),
				"Animation preview background",
				Core.UserPreferences.Instance.Get<UI.SceneView.SceneUserPreferences>(),
				() => Color4.Black.Transparentify(0.6f),
				pane);
			return pane;
		}

		public void CreateColorPropertyEditor(string targetProperty, string text, object source, System.Func<object> valueGetter, ThemedScrollView container)
		{
			var tmp = new Color4PropertyEditor(
				new PropertyEditorParams(
					container.Content,
					source,
					targetProperty,
					text
				) {
					DefaultValueGetter = valueGetter
				});
			tmp.ContainerWidget.AddChangeWatcher(
				new Property<Color4>(source, targetProperty), (v) => Application.InvalidateWindows());
		}

		Widget CreateGeneralPane()
		{
			var pane = new ThemedScrollView();
			pane.Content.Layout = new VBoxLayout { Spacing = 4 };
			pane.Content.Padding = contentPadding;
			new Vector2PropertyEditor(
				new PropertyEditorParams(pane.Content, Tangerine.AppUserPreferences.Instance, nameof(Tangerine.AppUserPreferences.DefaultSceneDimensions), "Default scene dimensions"));
			new BooleanPropertyEditor(
				new PropertyEditorParams(pane.Content, CoreUserPreferences.Instance, nameof(CoreUserPreferences.AutoKeyframes), "Automatic keyframes"));
			new BooleanPropertyEditor(
				new PropertyEditorParams(pane.Content, CoreUserPreferences.Instance, nameof(CoreUserPreferences.AnimationMode), "Animation mode"));
			new IntPropertyEditor(
				new PropertyEditorParams(pane.Content, Tangerine.AppUserPreferences.Instance, nameof(Tangerine.AppUserPreferences.AutosaveDelay), "Autosave delay"));
			var boneWidthPropertyEditor = new FloatPropertyEditor(
				new PropertyEditorParams(pane.Content, UI.SceneView.SceneUserPreferences.Instance, nameof(UI.SceneView.SceneUserPreferences.DefaultBoneWidth), "Bone Width"));
			boneWidthPropertyEditor.ContainerWidget.AddChangeWatcher(
				() => UI.SceneView.SceneUserPreferences.Instance.DefaultBoneWidth, (v) => Application.InvalidateWindows());
			return pane;
		}
		
		private Widget CreateKeyboardPane()
		{
			var hotkeyEditor = new HotkeyEditor();
			var pane = new Widget {
				Layout = new VBoxLayout { Spacing = 10 },
				Padding = contentPadding,
				Awoken = node => hotkeyEditor.SetFocus()
			};

			var profileLabel = new ThemedSimpleText("Profile: ") {
				VAlignment = VAlignment.Center,
				HAlignment = HAlignment.Right,
				LayoutCell = new LayoutCell(Alignment.RightCenter, 0)
			};
			var profilePicker = new ThemedDropDownList();
			profilePicker.TextWidget.Padding = new Thickness(3, 0);

			var exportButton = new ThemedButton("Export...");
			exportButton.Clicked = () => {
				var dlg = new FileDialog {
					Mode = FileDialogMode.Save,
					InitialFileName = currentProfile.Name
				};
				if (dlg.RunModal()) {
					currentProfile.Save(dlg.FileName);
				}
			};
			var importButton = new ThemedButton("Import...");
			importButton.Clicked = () => {
				var dlg = new FileDialog { Mode = FileDialogMode.Open };
				if (dlg.RunModal()) {
					string name = Path.GetFileName(dlg.FileName);
					if (HotkeyRegistry.Profiles.Any(i => i.Name == name)) {
						if (new AlertDialog($"Profile with name \"{name}\" already exists. Do you want to rewrite it?", "Yes", "Cancel").Show() != 0) {
							return;
						}
						else {
							profilePicker.Items.Remove(profilePicker.Items.First(i => i.Text == name));
						}
					}
					var profile = HotkeyRegistry.CreateProfile(Path.GetFileName(dlg.FileName));
					profile.Load(dlg.FileName);
					profile.Save();
					HotkeyRegistry.Profiles.Add(profile);
					profilePicker.Items.Add(new CommonDropDownList.Item(profile.Name, profile));
					profilePicker.Value = profile;
				}
			};
			var deleteButton = new ThemedButton("Delete");
			deleteButton.Clicked = () => {
				if (new AlertDialog($"Are you sure you want to delete profile \"{currentProfile.Name}\"?", "Yes", "Cancel").Show() == 0) {
					currentProfile.Delete();
					profilePicker.Items.Remove(profilePicker.Items.First(i => i.Value == currentProfile));
					profilePicker.Index = 0;
					HotkeyRegistry.CurrentProfile = profilePicker.Value as HotkeyProfile;
					foreach (var command in HotkeyRegistry.CurrentProfile.Commands) {
						command.Command.Shortcut = new Shortcut(Key.Unknown);
					}
				}
			};
			
			var categoryLabel = new ThemedSimpleText("Commands: ") {
				VAlignment = VAlignment.Center,
				HAlignment = HAlignment.Right,
				LayoutCell = new LayoutCell(Alignment.RightCenter, 0)
			};
			var categoryPicker = new ThemedDropDownList();
			categoryPicker.TextWidget.Padding = new Thickness(3, 0);

			var allShortcutsView = new ThemedScrollView();
			allShortcutsView.Content.Padding = contentPadding;
			allShortcutsView.Content.Layout = new VBoxLayout { Spacing = 4 };

			var selectedShortcutsView = new ThemedScrollView();
			selectedShortcutsView.Content.Padding = contentPadding;
			selectedShortcutsView.Content.Layout = new VBoxLayout { Spacing = 4 };

			hotkeyEditor.SelectedShortcutChanged = () => {
				selectedShortcutsView.Content.Nodes.Clear();
				var commands = hotkeyEditor.SelectedCommands.ToLookup(i => i.Category);
				foreach (var category in commands) {
					selectedShortcutsView.Content.AddNode(new ThemedSimpleText {
						Text = category.Key.Name,
						VAlignment = VAlignment.Center,
						Color = Theme.Colors.GrayText
					});
					foreach (var command in category) {
						var shortcut = new ThemedSimpleText {
							Text = command.Shortcut.ToString(),
							VAlignment = VAlignment.Center,
							LayoutCell = new LayoutCell(Alignment.LeftCenter, 1)
						};
						var name = new ThemedSimpleText {
							Text = command.Name,
							VAlignment = VAlignment.Center,
							LayoutCell = new LayoutCell(Alignment.LeftCenter, 2)
						};
						selectedShortcutsView.Content.AddNode(new Widget {
							Layout = new TableLayout { Spacing = 4, RowCount = 1, ColCount = 2 },
							Nodes = { shortcut, name },
							Padding = new Thickness(15, 0)
						});
					}
				}
				selectedShortcutsView.ScrollPosition = allShortcutsView.MinScrollPosition;
			};

			categoryPicker.Changed += args => {
				hotkeyEditor.Category = (args.Value as CommandCategory);
				hotkeyEditor.SetFocus();
				int index = -1;
				foreach (var node in allShortcutsView.Content.Nodes.SelectMany(i => i.Nodes)) {
					var button = node as ThemedExpandButton;
					if (button == null) {
						continue;
					}
					index++;
					if (index == args.Index) {
						if (!button.Expanded) {
							button.Clicked?.Invoke();
						}
						allShortcutsView.ScrollPosition = button.ParentWidget.Position.Y;
						break;
					}
				}
			};
			profilePicker.Changed += args => {
				var profile = args.Value as HotkeyProfile;
				if (profile != null) {
					hotkeyEditor.Profile = profile;
					UpdateAllShortcutsView(allShortcutsView, hotkeyEditor);
					deleteButton.Enabled = profile.Name != HotkeyRegistry.DefaultProfileName && profilePicker.Items.Count > 1;
					categoryPicker.Items.Clear();
					foreach (var category in profile.Categories) {
						categoryPicker.Items.Add(new CommonDropDownList.Item(category.Name, category));
					}
					categoryPicker.Value = null;
					categoryPicker.Value = profile.Categories.First();
					currentProfile = profile;
				}
			};
			UpdateProfiles(profilePicker);

			HotkeyRegistry.Reseted = () => UpdateProfiles(profilePicker);

			pane.AddNode(new Widget {
				Layout = new TableLayout { Spacing = 4, RowCount = 2, ColCount = 3 },
				Nodes = {
					profileLabel, profilePicker, 
					new Widget {
						Layout = new HBoxLayout { Spacing = 4 },
						Nodes = { exportButton, importButton, deleteButton }
					},
					categoryLabel, categoryPicker
				},
				LayoutCell = new LayoutCell { StretchY = 0 }
			});

			pane.AddNode(hotkeyEditor);
			pane.AddNode(new Widget {
				Layout = new HBoxLayout { Spacing = 4 },
				Nodes = {
					new Widget {
						Layout = new VBoxLayout {Spacing = 4 },
						Nodes = {
							new ThemedSimpleText("All commands:") { LayoutCell = new LayoutCell { StretchY = 0 } },
							new ThemedFrame {
								Nodes = { allShortcutsView },
								Layout = new VBoxLayout()
							}
						}
					},
					new Widget {
						Layout = new VBoxLayout {Spacing = 4 },
						Nodes = {
							new ThemedSimpleText("Selected commands:") { LayoutCell = new LayoutCell { StretchY = 0 } },
							new ThemedFrame {
								Nodes = { selectedShortcutsView },
								Layout = new VBoxLayout()
							}
						}
					}
				}
			});

			return pane;
		}

		private void UpdateAllShortcutsView(ThemedScrollView allShortcutsView, HotkeyEditor hotkeyEditor)
		{
			allShortcutsView.Content.Nodes.Clear();
			if (hotkeyEditor.Profile == null) {
				return;
			}
			foreach (var category in hotkeyEditor.Profile.Categories) {
				var expandableContent = new Frame {
					Padding = new Thickness(15, 0),
					Layout = new VBoxLayout { Spacing = 4 },
					Visible = false
				};
				var expandButton = new ThemedExpandButton {
					Anchors = Anchors.Left,
					MinMaxSize = Vector2.One * 20f,
					Expanded = expandableContent.Visible
				};
				var title = new ThemedSimpleText {
					Text = category.Name,
					VAlignment = VAlignment.Center,
					LayoutCell = new LayoutCell(Alignment.LeftCenter, stretchX: 0)
				};
				expandButton.Clicked += () => {
					expandableContent.Visible = !expandableContent.Visible;
					expandButton.Expanded = expandableContent.Visible;
				};
				var header = new Widget {
					Padding = new Thickness(4),
					Layout = new HBoxLayout(),
					Nodes = { expandButton, title }
				};
				allShortcutsView.Content.AddNode(header);
				allShortcutsView.Content.AddNode(expandableContent);
				foreach (var command in category.Commands) {
					var editor = new ShortcutPropertyEditor(
						new PropertyEditorParams(expandableContent, command, "Shortcut", command.Name));
					editor.PropertyLabel.OverflowMode = TextOverflowMode.Ellipsis;
					editor.PropertyLabel.LayoutCell = new LayoutCell(Alignment.LeftCenter, 1);
					editor.ContainerWidget.LayoutCell = new LayoutCell(Alignment.LeftCenter, 1);
					editor.PropertyChanged = () => {
						hotkeyEditor.UpdateButtonCommands();
						hotkeyEditor.UpdateShortcuts();
					};
				}
			}
		}

		private void UpdateProfiles(ThemedDropDownList profilePicker)
		{
			profilePicker.Items.Clear();
			foreach (var profile in HotkeyRegistry.Profiles) {
				profilePicker.Items.Add(new CommonDropDownList.Item(profile.Name, profile));
			}
			profilePicker.Value = HotkeyRegistry.CurrentProfile;
		}
	}
}
