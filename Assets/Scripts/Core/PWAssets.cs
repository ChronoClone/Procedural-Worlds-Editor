﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PW.Biomator;
using System.IO;
using System.Linq;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PW.Core
{
	public class PWAssets {
	
		public static Texture2DArray	GenerateOrLoadTexture2DArray(BiomeSwitchTree bst, string fName)
		{
			Texture2DArray		ret;
			bool				isLinear = false;
			int					i;

			if (String.IsNullOrEmpty(fName))
				fName = "PW";
			string assetFile = Path.Combine(PWConstants.resourcePath, fName + ".asset");

			ret = Resources.Load< Texture2DArray >(assetFile);
			if (ret != null)
				return ret;
			
			#if UNITY_EDITOR
				//generate and store Texture2DArray if not found
				var biomeTextures = bst.GetBiomes().OrderBy(kp => kp.Key).Select(kp => kp.Value.surfaceMaps.albedo).Where(a => a != null);
				int	biomeTextureCount = biomeTextures.Count();
				if (biomeTextureCount == 0)
				{
					Debug.LogWarning("no texture detected in any biomes");
					return null;
				}
				var firstTexture = biomeTextures.First();
				ret = new Texture2DArray(firstTexture.width, firstTexture.height, biomeTextureCount, firstTexture.format, firstTexture.mipmapCount > 1, isLinear);
				i = 0;
				foreach (var tex in biomeTextures)
				{
					if (tex.width != firstTexture.width || tex.height != firstTexture.height)
					{
						Debug.LogError("Texture " + tex + " does not match with first biome texture size w:" + firstTexture.width + "/h:" + firstTexture.height);
						continue ;
					}
					for (int j = 0; j < tex.mipmapCount; j++)
						Graphics.CopyTexture(tex, 0, j, ret, i, j);
					i++;
				}
				ret.anisoLevel = firstTexture.anisoLevel;
				ret.filterMode = firstTexture.filterMode;
				ret.Apply();
				AssetDatabase.CreateAsset(ret, assetFile);
				AssetDatabase.SaveAssets();

				return ret;
			#else
				Debug.LogError("Texture2DArray asset not found at path: " + assetFile);
				return null;
			#endif
		}
	}
}