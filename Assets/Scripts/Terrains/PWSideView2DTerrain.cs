﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PW;

public class PWSideView2DTerrain : PWTerrainBase< SideView2DData > {

	ChunkStorage< SideView2DData > chunks = new ChunkStorage< SideView2DData >();

	Vector3		pos = Vector3.zero;

	// Use this for initialization
	void Start () {
		InitGraph();
	}
	
	// Update is called once per frame
	void Update () {
		if (!chunks.isLoaded(pos))
		{
			var data = chunks.AddChunk(pos, RequestChunk(pos, 42));

			GameObject g = GameObject.CreatePrimitive(PrimitiveType.Quad);
			g.SetActive(true);
		}
	}
}