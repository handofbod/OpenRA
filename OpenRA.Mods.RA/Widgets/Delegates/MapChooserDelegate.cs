#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made 
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.IO;
using System.Linq;
using OpenRA.FileFormats;
using OpenRA.Network;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA.Widgets.Delegates
{
	public class MapChooserDelegate : IWidgetDelegate
	{
		Map Map = null;
		Widget scrollpanel;
		Widget itemTemplate;
		
		[ObjectCreator.UseCtor]
		internal MapChooserDelegate(
			[ObjectCreator.Param( "widget" )] Widget bg,
			[ObjectCreator.Param] OrderManager orderManager,
			[ObjectCreator.Param] string mapName )
		{
			if (mapName != null)
				Map = Game.modData.AvailableMaps[mapName];
			else
				Map = Game.modData.AvailableMaps.FirstOrDefault(m => m.Value.Selectable).Value;

			bg.GetWidget<MapPreviewWidget>("MAPCHOOSER_MAP_PREVIEW").Map = () => Map;
			bg.GetWidget<LabelWidget>("CURMAP_TITLE").GetText = () => Map.Title;
			bg.GetWidget<LabelWidget>("CURMAP_AUTHOR").GetText = () => Map.Author;
			bg.GetWidget<LabelWidget>("CURMAP_DESC").GetText = () => Map.Description;
			bg.GetWidget<LabelWidget>("CURMAP_DESC_LABEL").IsVisible = () => Map.Description != null;
			bg.GetWidget<LabelWidget>("CURMAP_SIZE").GetText = () => "{0}x{1}".F(Map.Bounds.Width, Map.Bounds.Height);
			bg.GetWidget<LabelWidget>("CURMAP_THEATER").GetText = () => Rules.TileSets[Map.Tileset].Name;
			bg.GetWidget<LabelWidget>("CURMAP_PLAYERS").GetText = () => Map.PlayerCount.ToString();

			bg.GetWidget<ButtonWidget>("BUTTON_OK").OnMouseUp = mi =>
			{
				orderManager.IssueOrder(Order.Command("map " + Map.Uid));
				Widget.CloseWindow();
				return true;
			};

			bg.GetWidget<ButtonWidget>("BUTTON_CANCEL").OnMouseUp = mi =>
			{
				Widget.CloseWindow();
				return true;
			};
			
			bg.GetWidget("BUTTON_INSTALL").IsVisible = () => false;
			//bg.GetWidget<ButtonWidget>("BUTTON_INSTALL").OnMouseUp = mi => InstallMap();
			scrollpanel = bg.GetWidget<ScrollPanelWidget>("MAP_LIST");
			itemTemplate = scrollpanel.GetWidget<ContainerWidget>("MAP_TEMPLATE");
			EnumerateMaps();
		}
		
		void EnumerateMaps()
		{
			scrollpanel.RemoveChildren();
			foreach (var kv in Game.modData.AvailableMaps.OrderBy(kv => kv.Value.Title).OrderBy(kv => kv.Value.PlayerCount))
			{
				var map = kv.Value;
				if (!map.Selectable)
					continue;

				var template = itemTemplate.Clone() as ContainerWidget;
				template.Id = "MAP_{0}".F(map.Uid);
				template.GetBackground = () => ((Map == map) ? "dialog2" : null);
				template.OnMouseDown = mi => { if (mi.Button != MouseButton.Left) return false;  Map = map; return true; };
				template.IsVisible = () => true;
				template.GetWidget<LabelWidget>("TITLE").GetText = () => map.Title;
				template.GetWidget<LabelWidget>("PLAYERS").GetText = () => "{0}".F(map.PlayerCount);
				template.GetWidget<LabelWidget>("TYPE").GetText = () => map.Type;
				scrollpanel.AddChild(template);
			}
		}
		
		bool InstallMap()
		{
			Game.Utilities.PromptFilepathAsync("Select an OpenRA map file", path =>
			{
				if (!string.IsNullOrEmpty(path))
					Game.RunAfterTick(() => InstallMapInner(P.E(path)));
			});
			return true;
		}
		
		void InstallMapInner(PathElement path)
		{
            var mod = Game.modData.Manifest.Mods[0];
            var toPath = Game.SupportDir / "maps" / mod / path.BaseName();
			
			// Create directory if required
            toPath.DirName().Create();
			
			// TODO: Attempt to mount the map and verify that
			// it is a valid Game.modData.Manifest.Mods[0] map.
			File.Copy(path.ToString(), toPath.ToString(), true);
			Game.modData.ReloadMaps();
			EnumerateMaps();
		}
	}
}
