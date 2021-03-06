﻿using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Sprites;
using System.Collections.Generic;

// DefaultPackerPolicy will pack rectangles no matter what Sprite mesh type is unless their packing tag contains "[TIGHT]".
class MipMapPolicy : UnityEditor.Sprites.IPackerPolicy
{
	protected class Entry
	{
		public Sprite sprite;
		public AtlasSettings settings;
		public string atlasName;
		public SpritePackingMode packingMode;
	}

	public virtual int GetVersion() { return 1; }

	protected virtual string TagPrefix { get { return "[TIGHT]"; } }
	protected virtual bool AllowTightWhenTagged { get { return true; } }

	public void OnGroupAtlases(BuildTarget target, PackerJob job, int[] textureImporterInstanceIDs)
	{
		List<Entry> entries = new List<Entry>();

		foreach (int instanceID in textureImporterInstanceIDs)
		{
			TextureImporter ti = EditorUtility.InstanceIDToObject(instanceID) as TextureImporter;

			TextureImportInstructions ins = new TextureImportInstructions();
			ti.ReadTextureImportInstructions(ins, target);

			TextureImporterSettings tis = new TextureImporterSettings();
			ti.ReadTextureSettings(tis);

			Sprite[] sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(ti.assetPath).Select(x => x as Sprite).Where(x => x != null).ToArray();
			foreach (Sprite sprite in sprites)
			{
				Entry entry = new Entry();
				entry.sprite = sprite;
				entry.settings.format = ins.desiredFormat;
				entry.settings.usageMode = ins.usageMode;
				entry.settings.colorSpace = ins.colorSpace;
				entry.settings.compressionQuality = ins.compressionQuality;
				entry.settings.filterMode = Enum.IsDefined(typeof(FilterMode), ti.filterMode) ? ti.filterMode : FilterMode.Bilinear;
				entry.settings.generateMipMaps = true;
				entry.settings.paddingPower = 4;
				entry.settings.maxWidth = 2048;
				entry.settings.maxHeight = 2048;
				entry.atlasName = ParseAtlasName(ti.spritePackingTag);
				entry.packingMode = GetPackingMode(ti.spritePackingTag, tis.spriteMeshType);

				entries.Add(entry);
			}

			Resources.UnloadAsset(ti);
		}

		// First split sprites into groups based on atlas name
		var atlasGroups =
			from e in entries
			group e by e.atlasName;
		foreach (var atlasGroup in atlasGroups)
		{
			int page = 0;
			// Then split those groups into smaller groups based on texture settings
			var settingsGroups =
				from t in atlasGroup
				group t by t.settings;
			foreach (var settingsGroup in settingsGroups)
			{
				string atlasName = atlasGroup.Key;
				if (settingsGroups.Count() > 1)
					atlasName += string.Format(" (Group {0})", page);

				job.AddAtlas(atlasName, settingsGroup.Key);
				foreach (Entry entry in settingsGroup)
				{
					job.AssignToAtlas(atlasName, entry.sprite, entry.packingMode, SpritePackingRotation.None);
				}

				++page;
			}
		}
	}

	protected bool IsTagPrefixed(string packingTag)
	{
		packingTag = packingTag.Trim();
		if (packingTag.Length < TagPrefix.Length)
			return false;
		return (packingTag.Substring(0, TagPrefix.Length) == TagPrefix);
	}

	private string ParseAtlasName(string packingTag)
	{
		string name = packingTag.Trim();
		if (IsTagPrefixed(name))
			name = name.Substring(TagPrefix.Length).Trim();
		return (name.Length == 0) ? "(unnamed)" : name;
	}

	private SpritePackingMode GetPackingMode(string packingTag, SpriteMeshType meshType)
	{
		if (meshType == SpriteMeshType.Tight)
			if (IsTagPrefixed(packingTag) == AllowTightWhenTagged)
				return SpritePackingMode.Tight;
		return SpritePackingMode.Rectangle;
	}
}
