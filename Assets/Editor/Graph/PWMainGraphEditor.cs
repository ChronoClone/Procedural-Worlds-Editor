﻿// #define		DEBUG_GRAPH

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using PW;
using PW.Core;
using PW.Node;
using Object = UnityEngine.Object;

[System.Serializable]
public partial class PWMainGraphEditor : PWGraphEditor {

	PWMainGraph						mainGraph;
	
	[SerializeField]
	public HorizontalSplitView		h1;
	[SerializeField]
	public HorizontalSplitView		h2;
	[SerializeField]
	public string					searchString = "";
	
	//graph, node, anchors and links control and 
	public bool			graphNeedReload = false;
	public bool			graphNeedReloadOnce = false;
	bool				previewMouseDrag = false;
	bool				editorNeedRepaint = false;
	[System.NonSerializedAttribute]
	PWNode				mouseAboveNode;
	
	//events fields
	Vector2				lastMousePosition;
	Vector2				windowSize;
	[System.NonSerializedAttribute]
	Vector2				currentMousePosition;

	//terrain materializer
	PWTerrainBase		terrainMaterializer;
	int					chunkRenderDistance = 4; //chunk render distance

	//multi-node selection
	[System.NonSerializedAttribute]
	Rect				selectionRect;
	[System.NonSerializedAttribute]
	bool				selecting = false;

	//current and parent graph
	[SerializeField]
	public PWMainGraph	currentGraph;

	//node selector and his subclasses
	[System.NonSerializedAttribute]
	Dictionary< string, PWNodeStorageCategory > nodeSelectorList = new Dictionary< string, PWNodeStorageCategory >();

	[System.SerializableAttribute]
	private class PWNodeStorageCategory
	{
		public string					color;
		public List< PWNodeStorage >	nodes;

		public PWNodeStorageCategory(string color) : this()
		{
			this.color = color;
		}

		public PWNodeStorageCategory()
		{
			nodes = new List< PWNodeStorage >();
		}
	}

	[System.SerializableAttribute]
	private class PWNodeStorage
	{
		public string		name;
		public System.Type	nodeType;
		public GUIStyle		windowStyle;
		public GUIStyle		windowSelectedStyle;
		
		public PWNodeStorage(string n, System.Type type, GUIStyle ws, GUIStyle wss)
		{
			name = n;
			nodeType = type;
			windowStyle = ws;
			windowSelectedStyle = wss;
		}
	}

#region Internal editor styles and textures

	private static Texture2D	resizeHandleTexture;

	private static Texture2D	preset2DSideViewTexture;
	private static Texture2D	preset2DTopDownViewTexture;
	private static Texture2D	preset3DPlaneTexture;
	private static Texture2D	preset3DSphericalTexture;
	private static Texture2D	preset3DCubicTexture;
	private static Texture2D	preset1DDensityFieldTexture;
	private static Texture2D	preset2DDensityFieldTexture;
	private static Texture2D	preset3DDensityFieldTexture;
	private static Texture2D	presetMeshTetxure;

	private static Texture2D	rencenterIconTexture;
	private static Texture2D	fileIconTexture;
	private static Texture2D	pauseIconTexture;
	private static Texture2D	eyeIconTexture;
	
#endregion

#region Initialization and data baking

	[MenuItem("Window/Procedural Worlds")]
	static void Init()
	{
		PWMainGraphEditor window = (PWMainGraphEditor)EditorWindow.GetWindow (typeof (PWMainGraphEditor));

		window.Show();
	}

/*	void InitializeNewGraph(PWNodeGraph graph)
	{
		//setup splitted panels:
		graph.h1 = new HorizontalSplitView(resizeHandleTexture, position.width * 0.85f, position.width / 2, position.width - 4);
		graph.h2 = new HorizontalSplitView(resizeHandleTexture, position.width * 0.25f, 4, position.width / 2);

		graph.graphDecalPosition = Vector2.zero;

		graph.realMode = false;
		graph.presetChoosed = false;
		
		graph.localNodeIdCount = 0;
		graph.chunkSize = 16;
		graph.step = 1;
		graph.maxStep = 4;
		graph.geologicTerrainStep = 8;
		graph.geologicDistanceCheck = 2;
		
		graph.outputNode = CreateNewNode(typeof(PWNodeGraphOutput), new Vector2(position.width - 100, (int)(position.height / 2)));
		graph.inputNode = CreateNewNode(typeof(PWNodeGraphInput), new Vector2(50, (int)(position.height / 2)));

		graph.firstInitialization = "initialized";
		graph.PWGUI = new PWGUIManager();

		graph.saveName = null;
		graph.externalName = "New ProceduralWorld";

		graph.processMode = PWGraphProcessMode.Normal;
	}*/

	public override void OnEnable()
	{
		base.OnEnable();

		mainGraph = graph as PWMainGraph;
		
		//load style: to move
		resizeHandleTexture = CreateTexture2DColor(resizeHandleColor);

		//loading preset panel images
		preset2DSideViewTexture = CreateTexture2DFromFile("preview2DSideView");
		preset2DTopDownViewTexture = CreateTexture2DFromFile("preview2DTopDownView");
		preset3DPlaneTexture = CreateTexture2DFromFile("preview3DPlane");
		preset3DSphericalTexture = CreateTexture2DFromFile("preview3DSpherical");
		preset3DCubicTexture = CreateTexture2DFromFile("preview3DCubic");
		presetMeshTetxure = CreateTexture2DFromFile("previewMesh");
		preset1DDensityFieldTexture= CreateTexture2DFromFile("preview1DDensityField");
		preset2DDensityFieldTexture = CreateTexture2DFromFile("preview2DDensityField");
		preset3DDensityFieldTexture = CreateTexture2DFromFile("preview3DDensityField");
		
		//icons and utils
		rencenterIconTexture = CreateTexture2DFromFile("ic_recenter");
		fileIconTexture = CreateTexture2DFromFile("ic_file");
		pauseIconTexture = CreateTexture2DFromFile("ic_pause");
		eyeIconTexture = CreateTexture2DFromFile("ic_eye");

		GeneratePWAssets();

		InitializeNodeSelector();

		//force graph to reload all chunks (just after compiled)
		graphNeedReload = true;
	}

#endregion

#region Global GUI rendering

	//call all rendering methods:
    public override void OnGUI()
    {
		//render the whole graph
		base.OnGUI();

		var e = Event.current;

		//prevent popup events to influence the rest of the GUI
		PWPopup.eventType = e.type;
		PWGUIManager.editorWindowRect = position;
		if (PWPopup.mouseAbove && e.type != EventType.Repaint && e.type != EventType.Layout)
			e.type = EventType.Ignore;

		//update the current GUI settings storage and clear drawed popup list:
		currentGraph.PWGUI.StartFrame();
		if (e.type == EventType.Layout)
			PWPopup.ClearAll();

		//text colors:
		whiteText = new GUIStyle();
		whiteText.normal.textColor = Color.white;
		whiteBoldText = new GUIStyle();
		whiteBoldText.fontStyle = FontStyle.Bold;
		whiteBoldText.normal.textColor = Color.white;

		if (windowSize != Vector2.zero && windowSize != position.size)
			OnWindowResize();
		
		windowSize = position.size;
		
		if (!currentGraph.presetChoosed)
		{
			DrawPresetPanel();
			return ;
		}
		
		//esc key event:
		if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
		{
			if (draggingLink)
				StopDragLink(false);
			selecting = false;
			draggingSelectedNodes = false;
			draggingSelectedNodesFromContextMenu = false;
			draggingNode = false;
		}

		if (e.type == EventType.Layout)
			ProcessPreviewScene(currentGraph.outputType);

		if (terrainMaterializer == null)
		{
			GameObject gtm = GameObject.Find("PWPreviewTerrain");
			if (gtm != null)
				terrainMaterializer = gtm.GetComponent< PWTerrainBase >();
		}

		h1.UpdateMinMax(position.width / 2, position.width - 3);
		h2.UpdateMinMax(50, position.width / 2);

		h1.Begin();
		Rect p1 = h2.Begin(defaultBackgroundTexture);
		DrawLeftBar(p1);
		Rect g = h2.Split(resizeHandleTexture);
		DrawNodeGraphHeader(g);
		h2.End();
		Rect p2 = h1.Split(resizeHandleTexture);
		DrawSelector(p2);
		h1.End();

		//FIXME
		if (!editorNeedRepaint)
			editorNeedRepaint = e.isMouse || e.type == EventType.ScrollWheel;

		//if event, repaint
		if ((editorNeedRepaint))
		{
			Repaint();
			editorNeedRepaint = false;
		}

		//render all opened popups (at the end cause the have to be above other infos)
		PWPopup.RenderAll(ref editorNeedRepaint);
    }

#endregion

//Manage to do something with this:

/*			if (e.type == EventType.Layout)
			{
				graph.ForeachAllNodes(n => n.BeginFrameUpdate(), true, true);

				if (graphNeedReload)
				{
					graphNeedReload = false;
					
					terrainMaterializer.DestroyAllChunks();

					//load another instance of the current graph to separate calls:
					if (terrainMaterializer.graph != null && terrainMaterializer.graph.GetHashCode() != graph.GetHashCode())
						DestroyImmediate(terrainMaterializer.graph);
					terrainMaterializer.InitGraph(CloneGraph(graph));

					Debug.Log("graph: " + graph.GetHashCode() + " , terrainMat: " + terrainMaterializer.graph.GetHashCode());
					//process the instance of the graph in our editor so we can see datas on chunk 0, 0, 0
					graph.realMode = false;
					graph.ForeachAllNodes(n => n.Updategraph(graph));
					graph.UpdateChunkPosition(Vector3.zero);

					if (graphNeedReloadOnce)
						graph.ProcessGraphOnce();
					graphNeedReloadOnce = false;

					graph.ProcessGraph();
				}
				//updateChunks will update and generate new chunks if needed.
				//TODOMAYBE: remove this when workers will be added to the Terrain.
				terrainMaterializer.UpdateChunks();
			}*/

#region Node and OrderingGroup Utils

	void OnWindowResize()
	{
		//calcul the ratio for the window move:
		float r = position.size.x / windowSize.x;

		h1.handlerPosition *= r;
		h2.handlerPosition *= r;
	}

#endregion

}