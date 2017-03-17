﻿#define DEBUG_WINDOW

using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace PW
{
	[System.SerializableAttribute]
	public class PWNode : ScriptableObject
	{
		public string	nodeTypeName;
		public Rect		windowRect;
		public Rect		externalWindowRect;
		public bool		useExternalWinowRect = false;
		public int		windowId;
		public bool		renamable;
		public int		computeOrder;

		static Color	defaultAnchorBackgroundColor = new Color(.75f, .75f, .75f, 1);
		static GUIStyle	boxAnchorStyle = null;
		
		static Texture2D	disabledTexture = null;
		static Texture2D	highlightNewTexture = null;
		static Texture2D	highlightReplaceTexture = null;
		static Texture2D	highlightAddTexture = null;

		[SerializeField]
		int		viewHeight;
		[SerializeField]
		Vector2	graphDecal;
		[SerializeField]
		int		maxAnchorRenderHeight;
		[SerializeField]
		string	firstInitialization;

		bool	windowShouldClose = false;
		bool	firstRenderLoop;

		public static int	windowRenderOrder = 0;

		[SerializeField]
		List< PWLink > links = new List< PWLink >();
		[SerializeField]
		List< int >		depencendies = new List< int >();

		[System.SerializableAttribute]
		public class PropertyDataDictionary : SerializableDictionary< string, PWAnchorData > {}
		[SerializeField]
		PropertyDataDictionary propertyDatas = new PropertyDataDictionary();

		public void OnDestroy()
		{
			Debug.Log("node " + nodeTypeName + " detroyed !");
		}

		public void UpdateGraphDecal(Vector2 graphDecal)
		{
			this.graphDecal = graphDecal;
		}
		
		public void OnEnable()
		{
			hideFlags = HideFlags.HideAndDontSave;

			firstRenderLoop = true;
			disabledTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			disabledTexture.SetPixel(0, 0, new Color(.4f, .4f, .4f, .5f));
			disabledTexture.Apply();
			highlightNewTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			highlightNewTexture.SetPixel(0, 0, new Color(0, .5f, 0, .4f));
			highlightNewTexture.Apply();
			highlightReplaceTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			highlightReplaceTexture.SetPixel(0, 0, new Color(.5f, .5f, 0, .4f));
			highlightReplaceTexture.Apply();
			highlightAddTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			highlightAddTexture.SetPixel(0, 0, new Color(0f, .0f, 0.5f, .4f));
			highlightAddTexture.Apply();
			LoadFieldAttributes();
			
			//this will be true only if the object instance does not came from a serialized object.
			if (firstInitialization == null)
			{
				computeOrder = 0;
				windowRect = new Rect(400, 400, 200, 50);
				externalWindowRect = new Rect(400, 400, 200, 50);
				viewHeight = 0;
				renamable = false;
				maxAnchorRenderHeight = 0;
				
				OnNodeCreate();

				firstInitialization = "initialized";
			}
		}

		public virtual void OnNodeCreate()
		{
		}

		void ForeachPWAnchors(Action< PWAnchorData, PWAnchorData.PWAnchorMultiData, int > callback)
		{
			foreach (var PWAnchorData in propertyDatas)
			{
				var data = PWAnchorData.Value;
				if (data.multiple)
				{
					int anchorCount = Mathf.Max(data.minMultipleValues, data.multipleValueCount);
					if (data.displayHiddenMultipleAnchors)
						anchorCount++;
					for (int i = 0; i < anchorCount; i++)
					{
						//if multi-anchor instance does not exists, create it:
						if (data.displayHiddenMultipleAnchors && i == anchorCount - 1)
							data.multi[i].additional = true;
						else
							data.multi[i].additional = false;
						callback(data, data.multi[i], i);
					}
				}
				else
					callback(data, data.first, -1);
			}
		}

		void LoadFieldAttributes()
		{
			//get input variables
			System.Reflection.FieldInfo[] fInfos = GetType().GetFields();

			List< string > actualFields = new List< string >();
			foreach (var field in fInfos)
			{
				actualFields.Add(field.Name);
				if (!propertyDatas.ContainsKey(field.Name))
					propertyDatas[field.Name] = new PWAnchorData(field.Name, field.Name.GetHashCode());
				
				PWAnchorData	data = propertyDatas[field.Name];
				Color			backgroundColor = defaultAnchorBackgroundColor;
				PWAnchorType	anchorType = PWAnchorType.None;
				string			name = field.Name;
				Vector2			offset = Vector2.zero;

				data.anchorInstance = field.GetValue(this);
				System.Object[] attrs = field.GetCustomAttributes(true);
				foreach (var attr in attrs)
				{
					PWInput		inputAttr = attr as PWInput;
					PWOutput	outputAttr = attr as PWOutput;
					PWColor		colorAttr = attr as PWColor;
					PWOffset	offsetAttr = attr as PWOffset;
					PWMultiple	multipleAttr = attr as PWMultiple;
					PWGeneric	genericAttr = attr as PWGeneric;
					PWMirror	mirrorAttr = attr as PWMirror;

					if (inputAttr != null)
					{
						anchorType = PWAnchorType.Input;
						if (inputAttr.name != null)
							name = inputAttr.name;
					}
					if (outputAttr != null)
					{
						anchorType = PWAnchorType.Output;
						if (outputAttr.name != null)
							name = outputAttr.name;
					}
					if (colorAttr != null)
						backgroundColor = colorAttr.color;
					if (offsetAttr != null)
						offset = offsetAttr.offset;
					if (multipleAttr != null)
					{
						//check if field is PWValues type otherwise do not implement multi-anchor
						var multipleValueInstance = field.GetValue(this) as PWValues;
						if (multipleValueInstance != null)
						{
							data.generic = true;
							data.multiple = true;
							data.allowedTypes = multipleAttr.allowedTypes;
							data.minMultipleValues = multipleAttr.minValues;
							data.maxMultipleValues = multipleAttr.maxValues;
						}
					}
					if (genericAttr != null)
					{
						data.allowedTypes = genericAttr.allowedTypes;
						data.generic = true;
					}
					if (mirrorAttr != null)
						data.mirroredField = mirrorAttr.fieldName;
				}
				if (anchorType == PWAnchorType.None) //field does not have a PW attribute
					propertyDatas.Remove(field.Name);
				else
				{
					if (anchorType == PWAnchorType.Output && data.multiple)
					{
						Debug.LogWarning("PWMultiple attribute is only valid on input variables");
						data.multiple = false;
					}
					data.classAQName = GetType().AssemblyQualifiedName;
					data.fieldName = field.Name;
					data.anchorType = anchorType;
					data.type = (SerializableType)field.FieldType;
					data.first.color = (SerializableColor)backgroundColor;
					data.first.name = name;
					data.first.offset = offset;
					data.windowId = windowId;

					//add missing values to instance of list:
					if (data.multiple)
					{
						//add minimum number of anchors to render:
						if (data.multipleValueCount < data.minMultipleValues)
							for (int i = data.multipleValueCount; i < data.minMultipleValues; i++)
								data.AddNewAnchor(backgroundColor, field.Name.GetHashCode() + i + 1);

						var PWValuesInstance = data.anchorInstance as PWValues;

						while (PWValuesInstance.Count < data.multipleValueCount)
							PWValuesInstance.Add(null);
					}
				}
			}

			//Check mirrored fields compatibility:
			foreach (var kp in propertyDatas)
				if (kp.Value.mirroredField != null)
				{
					if (propertyDatas.ContainsKey(kp.Value.mirroredField))
					{
						var type = propertyDatas[kp.Value.mirroredField].type;
						if (type != kp.Value.type)
						{
							Debug.LogWarning("incompatible mirrored type in " + GetType());
							kp.Value.mirroredField = null;
						}
					}
					else
						kp.Value.mirroredField = null;
				}

			//remove inhexistants dictionary entries:
			foreach (var kp in propertyDatas)
				if (!actualFields.Contains(kp.Key))
				{
					Debug.Log("removed " + kp.Key);
					propertyDatas.Remove(kp.Key);
				}
		}

		public void SetWindowId(int id)
		{
			windowId = id;
			ForeachPWAnchors((data, singleAnchor, i) => {
				data.windowId = id;
			});
		}

		public void OnGUI()
		{
			EditorGUILayout.LabelField("You are on the wrong window !");
		}

		public void OnWindowGUI(int id)
		{
			if (boxAnchorStyle == null)
			{
				boxAnchorStyle =  new GUIStyle(GUI.skin.box);
				boxAnchorStyle.padding = new RectOffset(0, 0, 1, 1);
			}

			// set the header of the window as draggable:
			int width = (int)((useExternalWinowRect) ? externalWindowRect.width : windowRect.width);
			Rect dragRect = new Rect(0, 0, width, 20);
			if (id != -1)
				GUI.DragWindow(dragRect);

			int	debugViewH = 0;
			#if DEBUG_WINDOW
				GUIStyle debugstyle = new GUIStyle();
				debugstyle.normal.background = highlightAddTexture;

				EditorGUILayout.BeginVertical(debugstyle);
				EditorGUILayout.LabelField("Id: " + windowId + " | Compute order: " + computeOrder);
				EditorGUILayout.LabelField("Render order: " + windowRenderOrder++);
				EditorGUILayout.EndVertical();
				debugViewH = (int)GUILayoutUtility.GetLastRect().height + 6; //add the padding and margin
			#endif

			GUILayout.BeginVertical();
			{
				RectOffset savedmargin = GUI.skin.label.margin;
				GUI.skin.label.margin = new RectOffset(2, 2, 5, 7);
				OnNodeGUI();
				GUI.skin.label.margin = savedmargin;
			}
			GUILayout.EndVertical();

			int viewH = (int)GUILayoutUtility.GetLastRect().height;
			if (Event.current.type == EventType.Repaint)
				viewHeight = viewH + debugViewH;

			if (!firstRenderLoop)
				viewHeight = Mathf.Max(viewHeight, maxAnchorRenderHeight);

			if (useExternalWinowRect)
				externalWindowRect.height = viewHeight + 24; //add the window header and footer size
			else
				windowRect.height = viewHeight + 24; //add the window header and footer size

			firstRenderLoop = false;
		}
	
		public virtual void	OnNodeGUI()
		{
			EditorGUILayout.LabelField("empty node");
		}

		public void Process()
		{
			foreach (var kp in propertyDatas)
				if (kp.Value.mirroredField != null)
				{
					var val = kp.Value.anchorInstance;
					var mirroredProp = propertyDatas[kp.Value.mirroredField];
					//TODO: optimize
					((Type)kp.Value.type).GetField(mirroredProp.fieldName).SetValue(this, val);
				}
			OnNodeProcess();
		}

		public virtual void OnNodeProcess()
		{
		}

		void ProcessAnchor(
			PWAnchorData data,
			PWAnchorData.PWAnchorMultiData singleAnchor,
			ref Rect inputAnchorRect,
			ref Rect outputAnchorRect,
			ref PWAnchorInfo ret,
			int index = -1)
		{
			Rect anchorRect = (data.anchorType == PWAnchorType.Input) ? inputAnchorRect : outputAnchorRect;
			anchorRect.position += graphDecal;

			singleAnchor.anchorRect = anchorRect;

			if (!ret.mouseAbove)
				ret = new PWAnchorInfo(data.fieldName, anchorRect,
					singleAnchor.color, data.type,
					data.anchorType, windowId, singleAnchor.id,
					data.classAQName, index,
					data.generic, data.allowedTypes);
			if (anchorRect.Contains(Event.current.mousePosition))
				ret.mouseAbove = true;
		}

		public PWAnchorInfo ProcessAnchors()
		{
			PWAnchorInfo ret = new PWAnchorInfo();
			
			int		anchorWidth = 38;
			int		anchorHeight = 16;

			Rect	winRect = (useExternalWinowRect) ? externalWindowRect : windowRect;
			Rect	inputAnchorRect = new Rect(winRect.xMin - anchorWidth + 2, winRect.y + 20, anchorWidth, anchorHeight);
			Rect	outputAnchorRect = new Rect(winRect.xMax - 2, winRect.y + 20, anchorWidth, anchorHeight);
			ForeachPWAnchors((data, singleAnchor, i) => {
				//process anchor event and calcul rect position if visible
				if (singleAnchor.visibility != PWVisibility.Gone)
				{
					if (singleAnchor.visibility == PWVisibility.Visible)
						ProcessAnchor(data, singleAnchor, ref inputAnchorRect, ref outputAnchorRect, ref ret, i);
					if (singleAnchor.visibility != PWVisibility.Gone)
					{
						if (data.anchorType == PWAnchorType.Input)
							inputAnchorRect.position += singleAnchor.offset + Vector2.up * 18;
						else if (data.anchorType == PWAnchorType.Output)
							outputAnchorRect.position += singleAnchor.offset + Vector2.up * 18;
					}
				}
			});
			maxAnchorRenderHeight = (int)Mathf.Max(inputAnchorRect.yMin - winRect.y - 20, outputAnchorRect.yMin - windowRect.y - 20);
			return ret;
		}
		
		void RenderAnchor(PWAnchorData data, PWAnchorData.PWAnchorMultiData singleAnchor, int index)
		{
			string anchorName = (singleAnchor.name.Length > 4) ? singleAnchor.name.Substring(0, 4) : singleAnchor.name;

			if (data.multiple)
			{
				//TODO: better
				if (singleAnchor.additional)
					anchorName = "+";
				else
					anchorName += index;
			}
			Color savedBackground = GUI.backgroundColor;
			GUI.backgroundColor = singleAnchor.color;
			GUI.Box(singleAnchor.anchorRect, anchorName, boxAnchorStyle);
			GUI.backgroundColor = savedBackground;
			if (!singleAnchor.enabled)
				GUI.DrawTexture(singleAnchor.anchorRect, disabledTexture);
			else
				switch (singleAnchor.highlighMode)
				{
					case PWAnchorHighlight.AttachNew:
						GUI.DrawTexture(singleAnchor.anchorRect, highlightNewTexture);
						break ;
					case PWAnchorHighlight.AttachReplace:
						GUI.DrawTexture(singleAnchor.anchorRect, highlightReplaceTexture);
						break ;
					case PWAnchorHighlight.AttachAdd:
						GUI.DrawTexture(singleAnchor.anchorRect, highlightAddTexture);
						break ;
				}
			//reset the Highlight:
			singleAnchor.highlighMode = PWAnchorHighlight.None;

			//if window is renamable, render a text input above the window:
			if (renamable)
			{
				GUIStyle centeredText = new GUIStyle(GUI.skin.textField);
				centeredText.alignment = TextAnchor.UpperCenter;
				centeredText.margin.top += 2;

				Rect renameRect = (useExternalWinowRect) ? externalWindowRect : windowRect;
				renameRect.position += graphDecal - Vector2.up * 18;
				GUI.SetNextControlName("renameWindow");
				name = GUI.TextField(renameRect, name, centeredText);

				if (Event.current.type == EventType.MouseDown && !renameRect.Contains(Event.current.mousePosition))
					GUI.FocusControl(null);
				if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.Escape))
					GUI.FocusControl(null);
			}

			#if DEBUG_WINDOW
				Rect anchorSideRect = singleAnchor.anchorRect;
				if (data.anchorType == PWAnchorType.Input)
				{
					anchorSideRect.position += Vector2.left * 90;
					anchorSideRect.size += Vector2.right * 100;
				}
				else
				{
					anchorSideRect.position -= Vector2.left * 40;
					anchorSideRect.size += Vector2.right * 100;
				}
				GUI.Label(anchorSideRect, "id: " + (long)singleAnchor.id);
			#endif
		}
		
		public void RenderAnchors()
		{
			if (highlightAddTexture == null)
				OnEnable();
			
			ForeachPWAnchors((data, singleAnchor, i) => {
				//draw anchor:
				if (singleAnchor.visibility != PWVisibility.Gone)
				{
					if (singleAnchor.visibility == PWVisibility.Visible)
						RenderAnchor(data, singleAnchor, i);
					if (singleAnchor.visibility == PWVisibility.InvisibleWhenLinking)
						singleAnchor.visibility = PWVisibility.Visible;
				}
			});
		}

		public List< PWLink > GetLinks()
		{
			return links;
		}

		public List< int >	GetDependencies()
		{
			return depencendies;
		}
		
		bool			AnchorAreAssignable(Type fromType, PWAnchorType fromAnchorType, bool fromGeneric, SerializableType[] fromAllowedTypes, PWAnchorInfo to, bool verbose = false)
		{
			if (fromType.IsAssignableFrom(to.fieldType) || fromType == typeof(object) || to.fieldType == typeof(object))
			{
				if (verbose)
					Debug.Log(fromType.ToString() + " is assignable from " + to.fieldType.ToString());
				return true;
			}
			
			if (fromAnchorType == PWAnchorType.Input)
			{
				if (fromGeneric)
				{
					if (verbose)
						Debug.Log("Generic variable, check all allowed types:");
					foreach (var st in fromAllowedTypes)
					{
						Type t = st;
						if (verbose)
							Debug.Log("check castable from " + to.fieldType + " to " + t);
						if (to.fieldType.IsAssignableFrom(t))
						{
							if (verbose)
								Debug.Log(to.fieldType + " is castable from " + t);
							return true;
						}
					}
				}
			}
			else
			{
				if (to.generic)
				{
					foreach (var st in to.allowedTypes)
					{
						Type t = st;
						if (verbose)
							Debug.Log("check castable from " + fromType + " to " + t);
						if (fromType.IsAssignableFrom(t))
						{
							if (verbose)
								Debug.Log(fromType + " is castable from " + t);
							return true;
						}
					}
				}
			}
			return false;
		}

		bool			AnchorAreAssignable(PWAnchorInfo from, PWAnchorInfo to, bool verbose = false)
		{
			return AnchorAreAssignable(from.fieldType, from.anchorType, from.generic, from.allowedTypes, to, verbose);
		}

		public void		AttachLink(PWAnchorInfo from, PWAnchorInfo to)
		{
			//from is othen me and with an anchor type of Output.

			//quit if types are not compatible
			if (!AnchorAreAssignable(from, to))
				return ;
			if (from.anchorType == to.anchorType)
				return ;
			if (from.windowId == to.windowId)
				return ;

			//we store output links:
			if (from.anchorType == PWAnchorType.Output)
			{
				links.Add(new PWLink(
					to.windowId, to.anchorId, to.name, to.classAQName, to.propIndex,
					from.windowId, from.anchorId, from.name, from.classAQName, from.anchorColor)
				);
				//mark local output anchors as linked:
				ForeachPWAnchors((data, singleAnchor, i) => {
					if (singleAnchor.id == from.anchorId)
						singleAnchor.linkCount++;
				});
			}
			else //input links are stored as depencencies:
			{
				ForeachPWAnchors((data, singleAnchor, i) => {
					if (singleAnchor.id == from.anchorId)
					{
						singleAnchor.linkCount++;
						//if data was added to multi-anchor:
						if (data.multiple)
						{
							if (i == data.multipleValueCount)
								data.AddNewAnchor(data.fieldName.GetHashCode() + i + 1);
						}
						if (data.mirroredField != null)
						{
							//TODO: find the field and add a value to his PWValues if it's a PWValue type.

							var mirroredProp = propertyDatas[data.mirroredField];
							if ((Type)mirroredProp.type == typeof(PWValues))
								mirroredProp.AddNewAnchor(mirroredProp.fieldName.GetHashCode() + i + 1);
						}
					}
				});
				depencendies.Add(to.windowId);
			}
		}

		public void		RemoveLink(int anchorId)
		{
			links.RemoveAll(l => l.localAnchorId == anchorId);
			PWAnchorData.PWAnchorMultiData singleAnchorData;
			GetAnchorData(anchorId, out singleAnchorData);
			singleAnchorData.linkCount--;
		}

		public PWAnchorData	GetAnchorData(int id, out PWAnchorData.PWAnchorMultiData singleAnchorData)
		{
			PWAnchorData					ret = null;
			PWAnchorData.PWAnchorMultiData	s = null;

			ForeachPWAnchors((data, singleAnchor, i) => {
				if (singleAnchor.id == id)
				{
					s = singleAnchor;
					ret = data;
				}
			});
			singleAnchorData = s;
			return ret;
		}

		public Rect?	GetAnchorRect(int id)
		{
			var matches =	from p in propertyDatas
							from p2 in p.Value.multi
							where p2.id == id
							select p2;

			if (matches.Count() == 0)
				return null;
			return matches.First().anchorRect;
		}

		PWAnchorType	InverAnchorType(PWAnchorType type)
		{
			if (type == PWAnchorType.Input)
				return PWAnchorType.Output;
			else if (type == PWAnchorType.Output)
				return PWAnchorType.Input;
			return PWAnchorType.None;
		}

		public void		HighlightLinkableAnchorsTo(PWAnchorInfo toLink)
		{
			PWAnchorType anchorType = InverAnchorType(toLink.anchorType);

			ForeachPWAnchors((data, singleAnchor, i) => {
				//Hide anchors and highlight when mouse hover
				// Debug.Log(data.fieldName + ": " + AnchorAreAssignable(data.type, data.anchorType, data.generic, data.allowedTypes, toLink, true));
				if (data.windowId != toLink.windowId
					&& data.anchorType == anchorType
					&& AnchorAreAssignable(data.type, data.anchorType, data.generic, data.allowedTypes, toLink, false))
				{
					Debug.Log("olol");
					if (data.multiple)
					{
						//display additional anchor to attach on next rendering
						data.displayHiddenMultipleAnchors = true;
					}
					if (singleAnchor.anchorRect.Contains(Event.current.mousePosition))
						if (singleAnchor.visibility == PWVisibility.Visible)
						{
							singleAnchor.highlighMode = PWAnchorHighlight.AttachNew;
							if (singleAnchor.linkCount > 0)
							{
								//if anchor is locked:
								if (data.anchorType == PWAnchorType.Input)
									singleAnchor.highlighMode = PWAnchorHighlight.AttachReplace;
								else
									singleAnchor.highlighMode = PWAnchorHighlight.AttachAdd;
							}
						}
				}
				else if (singleAnchor.visibility == PWVisibility.Visible
					&& singleAnchor.id != toLink.anchorId
					&& singleAnchor.linkCount == 0)
					singleAnchor.visibility = PWVisibility.InvisibleWhenLinking;
			});
		}

		public bool		WindowShouldClose()
		{
			return windowShouldClose;
		}

		public void		DisplayHiddenMultipleAnchors(bool display = true)
		{
			ForeachPWAnchors((data, singleAnchor, i)=> {
				if (data.multiple)
					data.displayHiddenMultipleAnchors = display;
			});
		}

		/* Utils function to manipulate PWnode variables */

		public void		UpdatePropEnabled(string propertyName, bool enabled)
		{
			if (propertyDatas.ContainsKey(propertyName))
				propertyDatas[propertyName].first.enabled = true;
		}

		public void		UpdatePropName(string propertyName, string newName)
		{
			if (propertyDatas.ContainsKey(propertyName))
				propertyDatas[propertyName].first.name = newName;
		}

		public void		UpdatePropBackgroundColor(string propertyName, Color newColor)
		{
			if (propertyDatas.ContainsKey(propertyName))
				propertyDatas[propertyName].first.color = (SerializableColor)newColor;
		}

		public void		UpdatePropVisibility(string propertyName, PWVisibility visibility)
		{
			if (propertyDatas.ContainsKey(propertyName))
				propertyDatas[propertyName].first.visibility = visibility;
		}

		public int		GetPropLinkCount(string propertyName)
		{
			if (propertyDatas.ContainsKey(propertyName))
				return propertyDatas[propertyName].first.linkCount;
			return -1;
		}
    }
}