//Terrain Unpacker [v1.2] by Alexey Dudkin (Alcatraz). 
//Flight Dream Studio (C) 2014. 
//http://flight-dream.com
//alcatraz-rus@mail.ru
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[AddComponentMenu ("Alcatraz Systems/Terrain Unpacker")]
[ExecuteInEditMode()]
public class TerrainUnpacker : MonoBehaviour {

	public enum ColType {
		meshCollider = 0,
		boxCollider = 1,
		capsuleCollider = 2,
		sphereCollider = 3,
		wheelCollider = 4
	}

	public bool hided = true, packing = true, unpacking = true, other = true, darkSkin = true;
	public Terrain terrain;
	public TreeInstance[] trees;
	public string tag2 = "Untagged";
	public bool del = true;
	public bool col = true;
	public bool tags = true;
	public ColType colType = ColType.capsuleCollider;
	public bool colorr = true;
	public bool wh = true;
	public bool lod = true;
	public bool limit = true;
	public int lim = 1000;
	public int lodsCount = 3;
	public int removedTrees;
	public int treesC;
	public int treesT;
	public bool  _color = true;
	public bool  _del = true;
	
	public void  UpdateData (){
		Terrain ter = terrain;
		TerrainData td;
		td = ter.terrainData;
		TreeInstance[] tr;
		tr = td.treeInstances;
		TreePrototype[] tp;
		tp = td.treePrototypes;
		treesC = tr.Length;
		treesT = tp.Length;
	}
	

	public bool Reduce (int b ,string n){
		bool st = true, res = true;
		for(int o = 0; o < b; o++) {
			if(terrain.gameObject.transform.GetChild(o).gameObject.name == n) {
				res = true;
				st = false;
			}
		}
		if(st) {
			res = false;
		}
		return res;
	}
	
	public GameObject GetChildOfName(string n) {
		int tc = terrain.gameObject.transform.childCount;
		GameObject go = gameObject;
		for(int o = 0; o < tc; o++) {
			if(terrain.gameObject.transform.GetChild(o).gameObject.name == n) {
				go = terrain.gameObject.transform.GetChild(o).gameObject;
			}
		}
		return go;
	}
	
	public void  UnpackTerrain (bool del ,bool col,bool t,bool c,bool whh,bool l,bool li){
		Terrain ter = terrain;
		TerrainData td;
		td = ter.terrainData;
		TreeInstance[] tr;
		tr = td.treeInstances;
		List<TreeInstance> trees = new List<TreeInstance>();
		TreePrototype[] tp;
		tp = td.treePrototypes;
		for(int z = 0; z < tr.Length; z++) {
			trees.Add(tr[z]);
		}
		GameObject[] pr = new GameObject[tp.Length];
		int tc1 = terrain.gameObject.transform.childCount;
		for(int z3 = 0; z3 < tp.Length; z3++) {
			if(!Reduce(tc1, tp[z3].prefab.gameObject.name)) {
				GameObject ob = new GameObject(tp[z3].prefab.gameObject.name);
				ob.AddComponent<ParentObject>();
				pr[z3] = ob;
			}else{
				pr[z3] = GetChildOfName(tp[z3].prefab.gameObject.name);
			}
		}
		bool  can = true;
		for(int j = 0; j < tr.Length; j++) {
			if(can) {
				int id;
				id = tr[j].prototypeIndex;
				Vector3 offset;
				Vector3 cPos = Vector3.Scale(tr[j].position, td.size);
		
				GameObject o = Instantiate(tp[id].prefab, cPos ,Quaternion.identity) as GameObject;

				if(li) {
					if((j+1)>=lim) {
						can = false;
					}
				}
				if(c) {
					if(o.GetComponent<Renderer>()) {
						Material[] mats;
						mats = o.GetComponent<Renderer>().sharedMaterials;
						for(int e = 0; e < mats.Length; e++) {
							mats[e].color = tr[j].color;
						}
					}else{
						Renderer[] ccc= GetComponentsInChildren<Renderer>();
						if(ccc.Length>0) {
							for(int q = 0; q < ccc.Length; q++) {
								Material[] mats2 = new Material[1];
								mats2 = ccc[q].GetComponent<Renderer>().sharedMaterials;
								for(int e2 = 0; e2 < mats2.Length; e2++) {
									mats2[e2].color = tr[j].color;
								}
								
							}
						}
					}
				}
				
				if(col) {
					if(o.GetComponent<Renderer>()) {
						if(colType == ColType.meshCollider) {
							o.AddComponent<MeshCollider>();
						}
						if(colType == ColType.boxCollider) {
							o.AddComponent<BoxCollider>();
						}
						if(colType == ColType.capsuleCollider) {
							o.AddComponent<CapsuleCollider>();
						}
						if(colType == ColType.sphereCollider) {
							o.AddComponent<SphereCollider>();
						}
						if(colType == ColType.wheelCollider) {
							o.AddComponent<WheelCollider>();
						}	
					}else{
						Renderer[] ccc2= GetComponentsInChildren<Renderer>();
						if(ccc2.Length>0) {
							for(int q2 = 0; q2 < ccc2.Length; q2++) {
								if(colType == ColType.meshCollider) {
									ccc2[q2].gameObject.AddComponent<MeshCollider>();
								}
								
								if(colType == ColType.boxCollider) {
									ccc2[q2].gameObject.AddComponent<BoxCollider>();
								}
								
								if(colType == ColType.capsuleCollider) {
									ccc2[q2].gameObject.AddComponent<CapsuleCollider>();
								}
								
								if(colType == ColType.sphereCollider) {
									ccc2[q2].gameObject.AddComponent<SphereCollider>();
								}
								
								if(colType == ColType.wheelCollider) {
									ccc2[q2].gameObject.AddComponent<WheelCollider>();
								}
								
							}
						}
					}
				}
				
				if(t) {
					if(o.GetComponent<Renderer>()) {
						o.transform.tag = tag2;
					}else{
						Renderer[] ccc3= GetComponentsInChildren<Renderer>();
						if(ccc3.Length>0) {
							for(int q3 = 0; q3 < ccc3.Length; q3++) {
								ccc3[q3].transform.tag = tag2;
							}
						}
					}
				}
				
				if(wh) {
					float tempWidth;
					float tempHeight;
					Vector3 result;
					
					tempWidth = tr[j].widthScale;
					tempHeight = tr[j].heightScale;
					result = new Vector3(tempWidth,tempHeight,tempWidth);
					o.transform.localScale = result;
					
				}
				
				
				
				if(del) {
					TreeInstance[] currTrees;
					currTrees = trees.ToArray();
					
					for(int s = 0; s < currTrees.Length; s++) {
						Vector3 cPos2 = Vector3.Scale(tr[j].position, td.size);
						Vector3 cPos3 = Vector3.Scale(currTrees[s].position, td.size);
						if(cPos2 == cPos3) {
							trees.RemoveAt(s);
							
						}
					}
					td.treeInstances = trees.ToArray();
					
				}
				
				if(l) {
					
					if(o.GetComponent<Renderer>()) {
						
						LODGroup group= o.AddComponent<LODGroup>();
						int tc = lodsCount-1;
						LOD[] lods= new LOD[tc];
						for(int x = 0; x < lods.Length; x++) {
							Renderer[] renderers= new Renderer[1];
							renderers[0] = o.gameObject.GetComponent<Renderer>();
							lods[x] = new LOD (1.0f / (x+1f), renderers);
						}
						
						group.SetLODS(lods);
						group.RecalculateBounds();
						
						
					}else{
						Renderer[] ccc4= GetComponentsInChildren<Renderer>();
						if(ccc4.Length>0) {
							for(int q4 = 0; q4 < ccc4.Length; q4++) {	
								LODGroup group= ccc4[q4].gameObject.AddComponent<LODGroup>();
								int tc2 = lodsCount-1;
								LOD[] lods= new LOD[tc2];
								for(int x2 = 0; x2 < lods.Length; x2++) {
									Renderer[] renderers2= new Renderer[1];
									renderers2[0] = ccc4[q4].gameObject.GetComponent<Renderer>();
									lods[x2] = new LOD(1.0f / (x2+1), renderers2);
								}
								group.SetLODS(lods);
								group.RecalculateBounds();
								
							}
						}
					}
					
				}
				
				for(int v = 0; v < pr.Length; v++) {
					pr[v].gameObject.transform.parent = terrain.gameObject.transform;
					pr[v].gameObject.transform.localPosition = new Vector3(0f,0f,0f);
				}
				
				SetUpTreeInstance(o, id, j, pr, tr, tp);
				
				
			}
			
		}
		
	}
	
	public void  SetUpTreeInstance ( GameObject o ,   int id ,   int j ,   GameObject[] pr ,   TreeInstance[] tr ,   TreePrototype[] tp  ){
		int j2;
		j2 = j;
		int co = terrain.gameObject.transform.childCount;
		
		for(int i = 0; i < co; i++) {
			
			if(terrain.gameObject.transform.GetChild(i).transform.childCount > 0) {
				int c = terrain.gameObject.transform.GetChild(i).transform.childCount;
				
				for(int i2 = 0; i2 < c; i2++) {
					if(terrain.gameObject.transform.GetChild(i).transform.GetChild(i2).GetComponent<TreeCopy>()) {
						j2 = terrain.gameObject.transform.GetChild(i).transform.GetChild(i2).GetComponent<TreeCopy>().treeNumber;
					}
				}
				
			}
			
			
		}
		
		TreeCopy a= o.AddComponent<TreeCopy>();
		a.originPos = tr[j].position;
		o.transform.parent = pr[id].gameObject.transform;
		o.gameObject.name = (j2+1).ToString() + "_" + tp[id].prefab.gameObject.name;
		o.gameObject.GetComponent<TreeCopy>().treeNumber = j2+1;
		
	}
	
	
	public int PackTerrainCheck (){
		int packTypes = 0;
		TerrainData td = terrain.terrainData;
		int count= terrain.gameObject.transform.childCount;
		List<GameObject> objs = new List<GameObject> ();
		
		for(int i = 0; i < count; i++) {
			if(terrain.gameObject.transform.GetChild(i).transform.gameObject.GetComponent<ParentObject>()) {
				if(terrain.gameObject.transform.GetChild(i).transform.childCount>0) {
					objs.Add(terrain.gameObject.transform.GetChild(i).transform.gameObject);
				}
			}
		}  
		packTypes = objs.Count;
		
		GameObject[] currTree;
		currTree = objs.ToArray();
		int range = 0;
		for(int q = 0; q < currTree.Length; q++) {
			int tempChild;
			tempChild = currTree[q].gameObject.transform.childCount;
			
			if(tempChild > 0) {
				range +=tempChild;
			}else{
				return 0;
			}
			
		}
		
		return range;
		
	}

	
	public void PackTerrain (bool c,bool d){
		int packTypes;
		TerrainData td = terrain.terrainData;
		int count= terrain.gameObject.transform.childCount;
		List<GameObject> objs = new List<GameObject> ();
		List<int> prot = new List<int> ();
		bool  can = true;
		for(int i = 0; i < count; i++) {
			if(terrain.gameObject.transform.GetChild(i).transform.gameObject.GetComponent<ParentObject>()) {
				objs.Add(terrain.gameObject.transform.GetChild(i).transform.gameObject);
				prot.Add(i);
			}
			
		}

		packTypes = objs.Count;
		GameObject[] currTree;
		currTree = objs.ToArray();
		for(int q = 0; q < currTree.Length; q++) {
			if(can) {
			    int[] p;
				p = prot.ToArray();	
				int tempChild;
				tempChild = currTree[q].gameObject.transform.childCount;
				if(tempChild > 0) {
					for(int q2 = 0; q2 < tempChild; q2++) {
						
						TreeInstance ttd = new TreeInstance();
						TreeCopy cPos = currTree[q].gameObject.transform.GetChild(q2).gameObject.GetComponent<TreeCopy>();
						Vector3 pos = cPos.originPos;
						ttd.position = pos;
						
						if(_color) {
							Color _tc = Color.white;
							if(currTree[q].gameObject.transform.GetChild(q2).GetComponent<Renderer>()) {
								_tc = currTree[q].gameObject.transform.GetChild(q2).GetComponent<Renderer>().sharedMaterial.color;
							}else{
								Renderer[] rend = currTree[q].gameObject.transform.GetChild(q2).GetComponentsInChildren<Renderer>();
								if(rend.Length > 0) {
									_tc = rend[0].GetComponent<Renderer>().sharedMaterial.color;
								}
								
							}
							
							ttd.color = _tc;
							ttd.lightmapColor = _tc;
						}
						ttd.widthScale = currTree[q].transform.localScale.x;
						ttd.heightScale = currTree[q].transform.localScale.y;
						ttd.prototypeIndex = p[q];
						terrain.AddTreeInstance(ttd);
						
					}
				}
				
			}
			
		}
		Clean2();		
	}
	
	
	
	public void Clean (){
		List<TreeInstance> td = new List<TreeInstance> ();
		TreeInstance[] ts;
		int lv = terrain.terrainData.treeInstances.Length;
		for(int l = 0; l < lv; l++) {
			td.Add(terrain.terrainData.treeInstances[l]);
		}
		for(int l2 = 0; l2 < lv; l2++) {
			td.RemoveAt(0);
			TreeInstance[] tdd;
			tdd = td.ToArray();
			ts = tdd;
			terrain.terrainData.treeInstances = ts;
		}
	}
	
	public void  Clean2 (){
		if(terrain) {
			TerrainData td = terrain.terrainData;
			int count;
			List<GameObject> objs = new List<GameObject> ();
			count = terrain.gameObject.transform.childCount;
			for(int i = 0; i < count; i++) {
				if(terrain.gameObject.transform.GetChild(i).transform.gameObject.GetComponent<ParentObject>()) {
					objs.Add(terrain.gameObject.transform.GetChild(i).transform.gameObject);
				}
			}  
			GameObject[] ob;
			ob = objs.ToArray();
			int l = ob.Length;
			for(int b = 0; b < l; b++) {
				DestroyImmediate(ob[b],true);
			}
		}

	}
	
	
	public void GetTerrain (){
		if(gameObject.GetComponent<Terrain>())  {
			terrain = gameObject.GetComponent<Terrain>();
		}
	}

}
