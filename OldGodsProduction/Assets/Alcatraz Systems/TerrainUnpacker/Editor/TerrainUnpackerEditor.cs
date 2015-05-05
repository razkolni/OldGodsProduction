//Terrain Unpacker [v1.2] by Alcatraz. 
//Flight Dream Studio (C) 2015. 
//alcatrazalex@gmail.com

using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor(typeof(TerrainUnpacker))]
public class TerrainUnpackerEditor : Editor {

	Texture2D image, add, delete;
	GUISkin skin;

	public override void OnInspectorGUI() {

		TerrainUnpacker t = (TerrainUnpacker)target;
		bool allowSceneObjects = !EditorUtility.IsPersistent (t);
		skin = (GUISkin)(Resources.LoadAssetAtPath ("Assets/Alcatraz Systems/TerrainUnpacker/Editor/GUISkin.guiskin", typeof(GUISkin)));
		if (!image) {
			image = Resources.LoadAssetAtPath ("Assets/Alcatraz Systems/TerrainUnpacker/Editor/fdsLogo.png", typeof(Texture2D)) as Texture2D;
		}
		bool show = true;
		if(t.darkSkin) {
		GUI.skin = skin;
		}
		//GUILayout.BeginVertical("Box");
	
		GUILayout.BeginVertical("Box");
		GUI.DrawTexture ( GUILayoutUtility.GetRect ( 64.0f, 64.0f ) , image ); 
		EditorGUILayout.HelpBox("Terrain Unpacker [v 1.2]",MessageType.None);
		GUILayout.EndVertical();
		GUILayout.Space (10f);
		if(!t.terrain) {
			GUILayout.Space(10f);
			EditorGUILayout.HelpBox("Script must be attached to a Terrain object, which you want to edit.",MessageType.Error);
			GUILayout.Space(10f);	
			t.GetTerrain();
		}

		GUILayout.BeginHorizontal ();
		GUILayout.Space(10f);
		if(t.terrain) {
		t.hided = EditorGUILayout.Foldout (t.hided, "Terrain '"+t.terrain.gameObject.name+"':");
		}
		GUILayout.EndHorizontal ();
		if(t.terrain && t.hided) {
			GUILayout.BeginVertical("Box");
			if(t.terrain && t.terrain.terrainData.treeInstances.Length>0) {
				t.UpdateData();
				EditorGUILayout.HelpBox("Terrain Data", MessageType.None);
				EditorGUILayout.LabelField("", "Amount of trees: "+t.treesC, "label");
				EditorGUILayout.LabelField("", "Types of trees: "+t.treesT, "label");
				GUILayout.Space(10f);

				GUILayout.BeginVertical("Box");
				GUILayout.BeginHorizontal ();
				GUILayout.Space(10);
				t.unpacking = EditorGUILayout.Foldout (t.unpacking, "Unpacking");
				GUILayout.EndHorizontal ();
				if(t.unpacking) {
				EditorGUILayout.HelpBox("Unpack Settings", MessageType.None);
					GUILayout.Space(10f);
				t.del = EditorGUILayout.Toggle("Delete old trees:", t.del);
					GUILayout.Space(10f);
				t.col= EditorGUILayout.Toggle("Add Collider:", t.col);
				if(t.col) {
					t.colType = (TerrainUnpacker.ColType)EditorGUILayout.EnumPopup("Collider:", t.colType);
						GUILayout.Space(10f);
				}
					GUILayout.Space(10f);
				EditorGUILayout.HelpBox("Tags for trees", MessageType.None);
				t.tags = EditorGUILayout.Toggle("Add Tag", t.tags);
				if(t.tags) {
					t.tag2 = EditorGUILayout.TextField("Tag:", t.tag2);
				}
				
					GUILayout.Space(10f);
				EditorGUILayout.HelpBox("Will be copied from the original tree", MessageType.None);
				t.colorr = EditorGUILayout.Toggle("Copy Color", t.colorr);
					GUILayout.Space(10f);
				t.wh= EditorGUILayout.Toggle("Copy Width and Height", t.wh);
				EditorGUILayout.HelpBox("Copy 'Tree Width' and 'Tree Height'", MessageType.Info);
					GUILayout.Space(10f);
				EditorGUILayout.HelpBox("LOD Groups", MessageType.None);
				t.lod = EditorGUILayout.Toggle("Add LOD Groups to trees:", t.lod);
				if(t.lod) {
					t.lodsCount = EditorGUILayout.IntSlider("Amount of LODs: ",t.lodsCount,1,8);
				}
					GUILayout.Space(10f);
				t.lim = EditorGUILayout.IntSlider("Limit: ",t.lim,1,100);
				EditorGUILayout.HelpBox("Amount of trees which will be unpacked per one 'Unpack' operation", MessageType.Info);

					GUILayout.Space(10f);
			
			
			if(t.terrain) {
				if(t.terrain && t.terrain.terrainData.treeInstances.Length>0) {
					show = false;
					GUI.color = Color.green;
				}else{
					GUI.color = Color.red;
							GUILayout.Space(10f);
					if(t.terrain.terrainData.treeInstances.Length==0) {	
						EditorGUILayout.LabelField("", "Amount of trees = 0!", "label");
					}else{
						EditorGUILayout.LabelField("", "Please Assign Terrain!", "label");
					}
							GUILayout.Space(10f);	
				}
			}
			
			
			if(GUILayout.Button("Unpack Trees")) {
				if(t.terrain && t.terrain.terrainData.treeInstances.Length>0 ) {
					t.UnpackTerrain(t.del, t.col, t.tags,t.colorr, t.wh, t.lod, t.limit);
				}else{
					show = true;
					if(t.terrain.terrainData.treeInstances.Length==0) {
						Debug.LogWarning("Amount of trees - 0!");
					}
				}
				
			}
			GUI.color = Color.white;
			
				}
				GUILayout.EndVertical();
			}

			GUILayout.Space(10f);
			GUILayout.BeginVertical("Box");
			GUILayout.BeginHorizontal ();
			GUILayout.Space(10);
			t.packing = EditorGUILayout.Foldout (t.packing, "Packing back");
			GUILayout.EndHorizontal ();
			if(t.packing) {
			EditorGUILayout.HelpBox("Pack Settings", MessageType.None);
			t._color = EditorGUILayout.Toggle("Copy Color", t._color);
	     	t._del= EditorGUILayout.Toggle("Remove unpacked trees", t._del);
				GUILayout.Space(10f);
			if(t.PackTerrainCheck() > 0) {
				EditorGUILayout.LabelField("", "Amount of Unpacked Trees: "+t.PackTerrainCheck(), "label");
				GUI.color = Color.green;
			}else{
				GUI.color = Color.white;
				EditorGUILayout.LabelField("", "Amount of Unpacked Trees: 0", "label");
				GUI.color = Color.red;
			}
			
			if(GUILayout.Button("Pack Trees Back")) {
				if(t.terrain) {
					t.PackTerrain(t._color, t._del);
				}else{
					show = true;
					Debug.LogWarning("Please, Assign terrain in inspector!");
				}
				
			}

			}
			GUILayout.EndVertical();
			GUI.color = Color.white;

			GUILayout.Space(10f);
			GUILayout.BeginVertical("box");
			GUILayout.BeginHorizontal ();
			GUILayout.Space(10);
			t.other = EditorGUILayout.Foldout (t.other, "Other");
			GUILayout.EndHorizontal ();
			if(t.other) {
				EditorGUILayout.HelpBox("Settings", MessageType.None);
				t.darkSkin = EditorGUILayout.Toggle("Inspector Dark Style:", t.darkSkin);
			EditorGUILayout.HelpBox("Another functions", MessageType.None);
				GUILayout.Space(10f);

			GUI.color = Color.yellow;
			if(GUILayout.Button("Remove All trees from 'Terrain'")) {
				if(t.terrain) {
					t.Clean();
				}else{
					show = true;
					Debug.LogWarning("Please, Assign terrain in inspector!");
				}
				
			}
			GUI.color = Color.white;
			EditorGUILayout.HelpBox("You can't reset this operation.", MessageType.Warning);
			GUI.color = Color.yellow;
			if(GUILayout.Button("Remove All Unpacked Trees")) {
				if(t.terrain) {
					t.Clean2();
				}else{
					show = true;
					Debug.LogWarning("Please, Assign terrain in inspector!");
				}
				
			}
			GUI.color = Color.white;
			EditorGUILayout.HelpBox("You can't reset this operation.", MessageType.Warning);
			GUILayout.EndVertical();
				GUILayout.Space(10f);
			}
			GUILayout.EndVertical();
		}

		if (GUI.changed){
			EditorUtility.SetDirty (t);
		}


	
}
}